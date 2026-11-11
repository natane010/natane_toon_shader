#!/usr/bin/env python3
"""変種間パリティ検査 (W3) の Unity 非依存版。

`Properties` の `[Toggle(KEYWORD)]` が立てるキーワードを、どのパスも
`#pragma shader_feature*` でコンパイルしていない状態を検出する。
インスペクタのトグルが何も変えず、誰も読まないキーワードだけが
マテリアルへ書き込まれる状態を防ぐ。

判定ロジックは C# 版 (`Editor/NataneToon/Integration/NataneShaderParityChecker.cs`)
と同じ。**宣言テーブルは C# 側を単一の真実として直接読み取る**ので、
片方だけ更新して食い違うことがない。

使い方:
    python Tests/CI/check_shader_parity.py [--repo-root PATH]

終了コード:
    0 … 未宣言の検出なし
    1 … 未宣言の検出あり、または宣言テーブルが腐っている
    2 … 入力が見つからない等、検査自体が成立しなかった
"""

import argparse
import io
import os
import re
import sys

# Windows の既定コンソールは cp932 で、日本語メッセージ中の記号（—）が出力できずに落ちる。
# CI ログでもローカルでも同じ出力になるよう UTF-8 に固定する。
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
else:  # Python 3.6 以前向けのフォールバック
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

BACKSLASH = chr(92)
NEWLINE = chr(10)

CHECKER_CS = os.path.join(
    "Editor", "NataneToon", "Integration", "NataneShaderParityChecker.cs")

SHADER_DIRS = [
    os.path.join("Shaders", "NataneToon"),
    os.path.join("Shaders", "NataneToon", "Variants"),
]

SHADER_PREFIX = "NataneToonShader"
# Particle は Properties が別系統（Effects 系機能を持たない）ため対象外。
# FakeShadow は NataneToonCore.hlsl を include しない単一パスの極小シェーダーで、
# 本体の機能セットを持たないため同じく対象外。
# NataneToonVariantLocator の除外条件と揃えている。
EXCLUDED_FILES = {
    "NataneToonShader_Particle.shader",
    "NataneToonShader_FakeShadow.shader",
}

TOGGLE_RE = re.compile(r"\[\s*Toggle\s*\(\s*([_A-Za-z][A-Za-z0-9_]*)\s*\)\s*\]")
PROPERTY_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*([_A-Za-z][A-Za-z0-9_]*)\s*\(")
PRAGMA_RE = re.compile(r"^\s*#\s*pragma\s+(shader_feature\w*)\s+(.*)$")


def strip_comments(src):
    """文字列リテラルを保護しつつ // と /* */ を除去する。改行は保持する。

    C# 版 NataneShaderSourceParser.StripComments と同じ挙動。
    """
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        if c == '"':
            out.append(c)
            i += 1
            while i < n:
                out.append(src[i])
                if src[i] == BACKSLASH and i + 1 < n:
                    out.append(src[i + 1])
                    i += 2
                    continue
                if src[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != NEWLINE:
                i += 1
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            i += 2
            while i < n and not (src[i] == "*" and i + 1 < n and src[i + 1] == "/"):
                if src[i] == NEWLINE:
                    out.append(NEWLINE)
                i += 1
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


def join_line_continuations(src):
    """行末バックスラッシュによる継続行を 1 行へ畳む（複数行 pragma 対応）。"""
    return re.sub(re.escape(BACKSLASH) + r"[^\S" + chr(13) + NEWLINE + r"]*" + chr(13) + "?" + NEWLINE, " ", src)


def parse_declaration_tables(checker_path):
    """C# 側の宣言テーブルを読み取る。

    `{ "KEY", "理由" },` 形式の行を、直前に現れたテーブル名で振り分ける。
    C# を厳密にパースするのではなく、テーブル境界だけを見る素朴な方法にしてある
    （テーブルの書式が変わったら検出できるよう、件数 0 を異常として扱う）。
    """
    with open(checker_path, encoding="utf-8") as f:
        text = f.read()

    entry_re = re.compile(r'\{\s*"([^"]+)"\s*,\s*"[^"]*"\s*\}')
    everywhere = set()
    in_variant = set()
    current = None

    for line in text.split(NEWLINE):
        if "KnownUncompiledEverywhere" in line and "=" in line:
            current = everywhere
            continue
        if "KnownUncompiledInVariant" in line and "=" in line:
            current = in_variant
            continue
        # テーブルの終端（メソッド定義の開始）で振り分けを止める。
        if current is not None and re.match(r"\s*(public|private|internal|///|//\s*----)", line):
            if "{" not in line and "}" not in line:
                current = None
        m = entry_re.search(line)
        if m and current is not None:
            current.add(m.group(1))

    return everywhere, in_variant


def collect_shader_files(repo_root):
    files = []
    for rel_dir in SHADER_DIRS:
        directory = os.path.join(repo_root, rel_dir)
        if not os.path.isdir(directory):
            continue
        for name in sorted(os.listdir(directory)):
            if not name.startswith(SHADER_PREFIX):
                continue
            if not name.endswith(".shader"):
                continue
            if name in EXCLUDED_FILES:
                continue
            files.append((name, os.path.join(directory, name)))
    # 同名ファイルが両ディレクトリに現れることは無い前提だが、
    # 万一重複しても最初の 1 件だけを見る（報告の安定性を優先）。
    seen = set()
    unique = []
    for name, path in files:
        if name in seen:
            continue
        seen.add(name)
        unique.append((name, path))
    return unique


def analyze(shader_files):
    """ファイル名 -> (toggle キーワード -> プロパティ名) と、コンパイル済みキーワード集合。"""
    toggles_by_file = {}
    compiled_by_file = {}
    compiled_anywhere = set()

    for name, path in shader_files:
        with open(path, encoding="utf-8") as f:
            raw = f.read()
        cleaned = join_line_continuations(strip_comments(raw))

        toggles = {}
        compiled = set()
        for line in cleaned.split(NEWLINE):
            tm = TOGGLE_RE.search(line)
            if tm:
                keyword = tm.group(1)
                if keyword not in toggles:
                    pm = PROPERTY_RE.match(line)
                    toggles[keyword] = pm.group(1) if pm else None
            pg = PRAGMA_RE.match(line)
            if pg:
                for token in pg.group(2).split():
                    if token not in ("__", "_"):
                        compiled.add(token)

        toggles_by_file[name] = toggles
        compiled_by_file[name] = compiled
        compiled_anywhere |= compiled

    return toggles_by_file, compiled_by_file, compiled_anywhere


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root", default=os.getcwd(),
        help="パッケージルート（package.json のあるディレクトリ）")
    args = parser.parse_args()

    repo_root = os.path.abspath(args.repo_root)
    checker_path = os.path.join(repo_root, CHECKER_CS)

    if not os.path.isfile(checker_path):
        print("PARITY_ERROR=宣言テーブル " + CHECKER_CS + " が見つかりません")
        return 2

    everywhere_decl, in_variant_decl = parse_declaration_tables(checker_path)
    if not everywhere_decl and not in_variant_decl:
        print("PARITY_ERROR=宣言テーブルを 1 件も読み取れませんでした（書式が変わった可能性）")
        return 2

    shader_files = collect_shader_files(repo_root)
    if not shader_files:
        print("PARITY_ERROR=対象シェーダーが見つかりません")
        return 2

    toggles_by_file, compiled_by_file, compiled_anywhere = analyze(shader_files)

    undeclared = []
    actual_everywhere = set()
    actual_in_variant = set()

    for name, _ in shader_files:
        for keyword in sorted(toggles_by_file[name]):
            if keyword in compiled_by_file[name]:
                continue

            prop = toggles_by_file[name][keyword] or "(プロパティ名不明)"
            if keyword in compiled_anywhere:
                actual_in_variant.add(name + " :: " + keyword)
                if (name + " :: " + keyword) not in in_variant_decl:
                    undeclared.append(
                        name + " :: [Toggle(" + keyword + ")] " + prop +
                        " — 他変種ではコンパイルされているがこの変種には無い")
            else:
                actual_everywhere.add(keyword)
                if keyword not in everywhere_decl:
                    undeclared.append(
                        name + " :: [Toggle(" + keyword + ")] " + prop +
                        " — どの変種のどのパスもコンパイルしていない")

    stale = sorted(
        [k for k in everywhere_decl if k not in actual_everywhere] +
        [k for k in in_variant_decl if k not in actual_in_variant])

    print("PARITY_SHADERS=" + str(len(shader_files)))
    print("PARITY_UNDECLARED_COUNT=" + str(len(undeclared)))
    print("PARITY_STALE_COUNT=" + str(len(stale)))

    if undeclared:
        print()
        print("■ 未宣言の未コンパイル Toggle キーワード")
        for item in undeclared:
            print("  " + item)
        print()
        print("  対応は次のいずれか:")
        print("    (a) 該当パスへ #pragma shader_feature_local を足して実際に効かせる")
        print("    (b) Uniform 分岐で実装し [Toggle(KEYWORD)] を [Toggle] へ変更する")
        print("    (c) 意図的なら NataneShaderParityChecker の宣言テーブルへ理由付きで登録する")

    if stale:
        print()
        print("■ 宣言テーブルの腐り（既に解消済みなので削除してよい）")
        for item in stale:
            print("  " + item)

    return 1 if (undeclared or stale) else 0


if __name__ == "__main__":
    sys.exit(main())
