#!/usr/bin/env bash
#
# Create a throwaway Unity project that references this package with a file: path, then
# run the shader compilation gate inside it.
#
# The validation recipe used to live only in the docs ("make a mini project, add a file:
# reference, run ShaderUtil.GetShaderMessages"), which meant rebuilding it by hand every
# time. This script is that recipe, executable, so CI and a local run are the same command.
#
# Usage:
#   Tests/CI/setup-validation-project.sh --unity <path-to-Unity-executable> [options]
#
# Options:
#   --unity PATH      Unity executable (required)
#   --out DIR         Where to create the project (default: ../NataneToonValidation)
#   --repo DIR        Package root, the directory holding package.json
#                     (default: two levels above this script)
#   --keep            Do not delete an existing project directory first
#   --no-run          Only create the project; skip the Unity run
#
# Exit codes:
#   0  shaders compiled with zero errors and zero warnings
#   1  shader errors or warnings were reported
#   2  the run never reached the validation method (usually a C# compile error)
#   3  bad arguments / setup failure

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
OUT_DIR=""
UNITY_BIN=""
KEEP_EXISTING=0
RUN_UNITY=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --unity) UNITY_BIN="$2"; shift 2 ;;
    --out)   OUT_DIR="$2"; shift 2 ;;
    --repo)  REPO_DIR="$(cd "$2" && pwd)"; shift 2 ;;
    --keep)  KEEP_EXISTING=1; shift ;;
    --no-run) RUN_UNITY=0; shift ;;
    -h|--help) sed -n '2,25p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 3 ;;
  esac
done

if [[ ! -f "${REPO_DIR}/package.json" ]]; then
  echo "ERROR: no package.json in ${REPO_DIR}. Pass --repo <package root>." >&2
  exit 3
fi

PACKAGE_NAME="$(grep -o '"name"[[:space:]]*:[[:space:]]*"[^"]*"' "${REPO_DIR}/package.json" \
  | head -1 | sed 's/.*"\([^"]*\)"$/\1/')"
if [[ -z "${PACKAGE_NAME}" ]]; then
  echo "ERROR: could not read the package name from package.json." >&2
  exit 3
fi

if [[ -z "${OUT_DIR}" ]]; then
  OUT_DIR="$(cd "${REPO_DIR}/.." && pwd)/NataneToonValidation"
fi

if [[ ${RUN_UNITY} -eq 1 && -z "${UNITY_BIN}" ]]; then
  echo "ERROR: --unity <path> is required (or pass --no-run)." >&2
  exit 3
fi

echo "package : ${PACKAGE_NAME}"
echo "repo    : ${REPO_DIR}"
echo "project : ${OUT_DIR}"

if [[ -d "${OUT_DIR}" && ${KEEP_EXISTING} -eq 0 ]]; then
  echo "removing existing project directory"
  rm -rf "${OUT_DIR}"
fi

mkdir -p "${OUT_DIR}/Packages"
mkdir -p "${OUT_DIR}/Assets/Editor"
mkdir -p "${OUT_DIR}/ProjectSettings"

# A file: dependency must be relative to the project's Packages folder, otherwise Unity
# resolves it against the wrong root on some platforms.
# Windows の "python3" は実行できない Store スタブのことがある。
PYTHON_BIN=""
for candidate in python3 python; do
  if "${candidate}" -c "import json" >/dev/null 2>&1; then PYTHON_BIN="${candidate}"; break; fi
done
if [[ -z "${PYTHON_BIN}" ]]; then
  echo "ERROR: python is required." >&2
  exit 3
fi
REL_REPO="$("${PYTHON_BIN}" -c "import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]).replace(os.sep,'/'))" \
  "${REPO_DIR}" "${OUT_DIR}/Packages")"

# パッケージの C# はビルトインモジュール (Physics / ParticleSystem など) を使う。
# エディターごとに有無が違うので、存在するものだけ足す。
BUILTIN_DIR=""
for candidate in \
  "$(dirname "${UNITY_BIN}")/Data/Resources/PackageManager/BuiltInPackages" \
  "$(dirname "${UNITY_BIN}")/../Resources/PackageManager/BuiltInPackages"; do
  if [[ -d "${candidate}" ]]; then BUILTIN_DIR="${candidate}"; break; fi
done
"${PYTHON_BIN}" - "${OUT_DIR}/Packages/manifest.json" "${PACKAGE_NAME}" "file:${REL_REPO}" "${BUILTIN_DIR}" <<'PY'
import json, os, sys
manifest, name, source, builtin_dir = sys.argv[1:5]
deps = {name: source}
modules = ("animation assetbundle audio cloth director imageconversion imgui jsonserialize "
           "particlesystem physics physics2d screencapture terrain terrainphysics tilemap ui "
           "uielements unitywebrequest unitywebrequesttexture video vr wind xr")
for module in modules.split():
    if builtin_dir and os.path.isdir(os.path.join(builtin_dir, "com.unity.modules." + module)):
        deps["com.unity.modules." + module] = "1.0.0"
with open(manifest, "w", encoding="utf-8") as handle:
    json.dump({"dependencies": deps}, handle, indent=2)
PY

# Stored as .txt so Unity does not compile it inside the package itself (it would land in
# Assembly-CSharp and break player builds). Rename on the way in.
cp "${SCRIPT_DIR}/ValidateShaders.cs.txt" "${OUT_DIR}/Assets/Editor/ValidateShaders.cs"

# Built-in Render Pipeline and Linear color space, matching what the package targets.
cat > "${OUT_DIR}/ProjectSettings/ProjectVersion.txt" <<'EOF'
m_EditorVersion: 2022.3.28f1
EOF

echo "project created"

if [[ ${RUN_UNITY} -eq 0 ]]; then
  exit 0
fi

LOG_FILE="${OUT_DIR}/validate.log"
echo "running Unity (log: ${LOG_FILE})"

set +e
"${UNITY_BIN}" \
  -batchmode \
  -quit \
  -nographics \
  -projectPath "${OUT_DIR}" \
  -executeMethod NataneToonCI.ValidateShaders.RunFromBatch \
  -logFile "${LOG_FILE}"
UNITY_EXIT=$?
set -e

# A C# compile error stops Unity before -executeMethod runs. Distinguishing that from a
# clean pass matters: otherwise a broken editor script looks like a green shader gate.
if grep -q "error CS" "${LOG_FILE}"; then
  echo "SHADER_RESULT=CSHARP_COMPILE_ERROR"
  echo "C# compile errors prevented the shader gate from running:"
  grep -m 20 "error CS" "${LOG_FILE}" || true
  exit 2
fi

if ! grep -q "SHADER_RESULT=" "${LOG_FILE}"; then
  echo "SHADER_RESULT=DID_NOT_RUN"
  echo "The validation method never reported a result. Unity exit code: ${UNITY_EXIT}"
  tail -40 "${LOG_FILE}" || true
  exit 2
fi

grep -E "^SHADER_(TOTAL|ERROR_COUNT|WARNING_COUNT|RESULT)=" "${LOG_FILE}" || true

if grep -q "SHADER_RESULT=PASS" "${LOG_FILE}"; then
  exit 0
fi

echo "shader validation failed:"
sed -n '/\[Natane CI\]/,/^$/p' "${LOG_FILE}" | head -80 || true
exit 1
