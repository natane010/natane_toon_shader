#!/usr/bin/env bash
#
# Create a throwaway URP project that references this package with a file: path, then
# compile every pass of every URP SubShader in it (Tests/CI/ValidateURP.cs.txt).
#
# The Built-in RP gate (setup-validation-project.sh) never touches the URP SubShaders:
# they are skipped unless the URP package is installed. This is the matching gate for URP.
# The URP version is the one bundled with the given editor, so run it once per editor you
# care about (Unity 6.3 LTS or newer for the Unity 6 SubShader, 2022.3 for the URP 14 one).
#
# Usage:
#   Tests/CI/setup-urp-validation-project.sh --unity <path-to-Unity-executable> [options]
#
# Options:
#   --unity PATH      Unity executable (required)
#   --out DIR         Where to create the project (default: ../NataneToonValidationURP)
#   --repo DIR        Package root, the directory holding package.json
#                     (default: two levels above this script)
#   --no-run          Only create the project; skip the Unity run
#
# Exit codes:
#   0  URP SubShaders compiled with zero errors and zero warnings
#   1  shader errors or warnings were reported
#   2  the run never reached the validation method (usually a C# compile error)
#   3  bad arguments / setup failure

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
OUT_DIR=""
UNITY_BIN=""
RUN_UNITY=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --unity) UNITY_BIN="$2"; shift 2 ;;
    --out)   OUT_DIR="$2"; shift 2 ;;
    --repo)  REPO_DIR="$(cd "$2" && pwd)"; shift 2 ;;
    --no-run) RUN_UNITY=0; shift ;;
    -h|--help) sed -n '2,26p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 3 ;;
  esac
done

if [[ ! -f "${REPO_DIR}/package.json" ]]; then
  echo "ERROR: no package.json in ${REPO_DIR}. Pass --repo <package root>." >&2
  exit 3
fi
if [[ -z "${UNITY_BIN}" || ! -e "${UNITY_BIN}" ]]; then
  echo "ERROR: pass --unity <path to the Unity executable>." >&2
  exit 3
fi

# On Windows "python3" can be the Microsoft Store stub, which exists but does not run.
PYTHON_BIN=""
for candidate in python3 python; do
  if "${candidate}" -c "import json" >/dev/null 2>&1; then PYTHON_BIN="${candidate}"; break; fi
done
if [[ -z "${PYTHON_BIN}" ]]; then
  echo "ERROR: python is required." >&2
  exit 3
fi

PACKAGE_NAME="$(grep -o '"name"[[:space:]]*:[[:space:]]*"[^"]*"' "${REPO_DIR}/package.json" \
  | head -1 | sed 's/.*"\([^"]*\)"$/\1/')"
[[ -n "${OUT_DIR}" ]] || OUT_DIR="${REPO_DIR}/../NataneToonValidationURP"

# The URP package ships inside the editor; use exactly that version.
EDITOR_DIR="$(dirname "${UNITY_BIN}")"
URP_MANIFEST=""
for candidate in \
  "${EDITOR_DIR}/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal/package.json" \
  "${EDITOR_DIR}/../Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal/package.json"; do
  if [[ -f "${candidate}" ]]; then URP_MANIFEST="${candidate}"; break; fi
done
if [[ -z "${URP_MANIFEST}" ]]; then
  echo "ERROR: this editor does not bundle the URP package." >&2
  exit 3
fi
URP_VERSION="$(grep -o '"version"[[:space:]]*:[[:space:]]*"[^"]*"' "${URP_MANIFEST}" | head -1 | sed 's/.*"\([^"]*\)"$/\1/')"
EDITOR_VERSION="$(basename "$(dirname "${EDITOR_DIR}")")"

mkdir -p "${OUT_DIR}/Packages" "${OUT_DIR}/Assets/Editor" "${OUT_DIR}/ProjectSettings"

"${PYTHON_BIN}" - "${OUT_DIR}/Packages/manifest.json" "${REPO_DIR}" "${OUT_DIR}/Packages" "${PACKAGE_NAME}" "${URP_VERSION}" "$(dirname "$(dirname "${URP_MANIFEST}")")" <<'PY'
import json, os, sys
manifest, repo, packages_dir, name, urp, builtin_dir = sys.argv[1:7]
relative = os.path.relpath(repo, packages_dir).replace(os.sep, "/")
deps = {name: "file:" + relative, "com.unity.render-pipelines.universal": urp}
modules = ("animation assetbundle audio cloth director imageconversion imgui jsonserialize "
           "particlesystem physics physics2d screencapture terrain terrainphysics tilemap ui "
           "uielements unitywebrequest unitywebrequesttexture video vr wind xr")
for module in modules.split():
    # Built-in modules come and go between editors (com.unity.modules.vr is gone in 6000.5).
    if os.path.isdir(os.path.join(builtin_dir, "com.unity.modules." + module)):
        deps["com.unity.modules." + module] = "1.0.0"
with open(manifest, "w", encoding="utf-8") as handle:
    json.dump({"dependencies": deps}, handle, indent=2)
PY

cp "${SCRIPT_DIR}/ValidateURP.cs.txt" "${OUT_DIR}/Assets/Editor/ValidateURP.cs"
if [[ ! -f "${OUT_DIR}/ProjectSettings/ProjectVersion.txt" ]]; then
  echo "m_EditorVersion: ${EDITOR_VERSION}" > "${OUT_DIR}/ProjectSettings/ProjectVersion.txt"
fi

echo "Project: ${OUT_DIR} (Unity ${EDITOR_VERSION}, URP ${URP_VERSION})"
if [[ "${RUN_UNITY}" -eq 0 ]]; then
  exit 0
fi

LOG="${OUT_DIR}/validate-urp.log"
rm -f "${LOG}"
set +e
"${UNITY_BIN}" -batchmode -nographics -projectPath "${OUT_DIR}" \
  -executeMethod NataneToonCI.ValidateURP.RunFromBatch -logFile "${LOG}"
UNITY_EXIT=$?
set -e

if grep -q "error CS" "${LOG}"; then
  echo "C# compile errors:"
  grep "error CS" "${LOG}" | sort -u | head -40
fi
grep -aE "^URP_(VERSION|PIPELINE|SHADER_TOTAL|SHADERS_WITH_URP_SUBSHADER|SUBSHADERS|VARIANTS_COMPILED|ERROR_COUNT|WARNING_COUNT|RESULT)" "${LOG}" | sort -u || true
sed -n '/^URP_REPORT_BEGIN/,/^URP_REPORT_END/p' "${LOG}" | awk '!seen[$0]++' | head -120

if ! grep -q "^URP_RESULT=" "${LOG}"; then
  echo "ERROR: the validation method never ran (see ${LOG})." >&2
  exit 2
fi
if grep -q "^URP_RESULT=PASS" "${LOG}"; then
  exit 0
fi
echo "Unity exit code: ${UNITY_EXIT}" >&2
exit 1
