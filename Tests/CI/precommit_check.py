#!/usr/bin/env python3
"""コミット前チェック（Unity 不要）。

3 つを検査する。

1. `.meta` の欠落と孤児 `.meta`
   Unity は `.meta` で GUID を保持しているため、欠けると参照が切れ、
   孤児が残ると再インポートのたびにノイズになる。現時点では 0 件だが、
   検査する仕組みが無いと予防できない。

2. `Documentation~/` の未追跡ファイル
   グローバル gitignore の `*~` が `Documentation~/` にマッチするため、
   ここへの新規ファイルは `git add -f` が必須。`git add -A` では静かにスキップされ、
   書いたはずのドキュメントがコミットに含まれない事故が起きる。

3. バージョン表記の一致
   `package.json` を単一の真実とし、README のバッジ・本文、CHANGELOG の見出し、
   Website の表示が食い違っていないかを見る。

使い方:
    python Tests/CI/precommit_check.py [--repo-root PATH] [--skip-git]

終了コード:
    0 … 問題なし
    1 … 問題あり
    2 … 検査自体が成立しなかった
"""

import argparse
import io
import os
import re
import subprocess
import sys

NEWLINE = chr(10)

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
else:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

# Unity が .meta を要求するのは Assets / Packages 配下の資産だけ。
# ドキュメント・CI スクリプト・リポジトリ管理用ファイルは対象外。
META_CHECK_DIRS = ["Runtime", "Editor", "Shaders", "Tests/Editor", "ShaderVariants"]

# .meta を持たないのが正しいもの。
META_EXEMPT_NAMES = {".DS_Store", "Thumbs.db"}
META_EXEMPT_SUFFIXES = (".meta",)
META_EXEMPT_DIR_NAMES = {".git", "Documentation~"}


def iter_asset_paths(repo_root):
    """`.meta` を要求するファイルとディレクトリを列挙する。"""
    for rel_dir in META_CHECK_DIRS:
        base = os.path.join(repo_root, rel_dir)
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in META_EXEMPT_DIR_NAMES]
            for d in dirnames:
                yield os.path.join(dirpath, d)
            for f in filenames:
                if f in META_EXEMPT_NAMES:
                    continue
                if f.endswith(META_EXEMPT_SUFFIXES):
                    continue
                yield os.path.join(dirpath, f)


def check_meta(repo_root):
    """`.meta` の欠落と孤児を返す。"""
    missing = []
    for path in iter_asset_paths(repo_root):
        if not os.path.exists(path + ".meta"):
            missing.append(os.path.relpath(path, repo_root).replace(os.sep, "/"))

    orphans = []
    for rel_dir in META_CHECK_DIRS:
        base = os.path.join(repo_root, rel_dir)
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in META_EXEMPT_DIR_NAMES]
            for f in filenames:
                if not f.endswith(".meta"):
                    continue
                target = os.path.join(dirpath, f[:-5])
                if not os.path.exists(target):
                    orphans.append(
                        os.path.relpath(os.path.join(dirpath, f), repo_root).replace(os.sep, "/"))

    return sorted(missing), sorted(orphans)


def check_documentation_untracked(repo_root):
    """`Documentation~/` に未追跡ファイルが残っていないか。"""
    doc_dir = os.path.join(repo_root, "Documentation~")
    if not os.path.isdir(doc_dir):
        return []

    try:
        # --ignored を付けないと *~ の gitignore に隠されて何も出てこない。
        out = subprocess.check_output(
            ["git", "status", "--porcelain", "--ignored", "--untracked-files=all", "--", "Documentation~"],
            cwd=repo_root, stderr=subprocess.DEVNULL).decode("utf-8", "replace")
    except (subprocess.CalledProcessError, OSError):
        return None  # git が使えない環境ではスキップ扱い

    untracked = []
    for line in out.split(NEWLINE):
        if not line.strip():
            continue
        status = line[:2]
        path = line[3:].strip()
        if status.strip() in ("??", "!!"):
            untracked.append(path)

    return sorted(untracked)


def read_package_version(repo_root):
    path = os.path.join(repo_root, "package.json")
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as f:
        text = f.read()
    m = re.search(r'"version"\s*:\s*"([^"]+)"', text)
    return m.group(1) if m else None


def check_versions(repo_root, version):
    """バージョン表記の食い違いを返す。"""
    problems = []

    def read(rel):
        path = os.path.join(repo_root, rel)
        if not os.path.isfile(path):
            return None
        with open(path, encoding="utf-8") as f:
            return f.read()

    readme = read("README.md")
    if readme is None:
        problems.append("README.md が見つかりません")
    else:
        if ("version-" + version + "-blue") not in readme:
            problems.append(
                "README.md の shields.io バッジが " + version + " と一致しません")
        if ("`" + version + "`") not in readme:
            problems.append(
                "README.md 本文の「パッケージバージョン」が " + version + " と一致しません")

    changelog = read("CHANGELOG.md")
    if changelog is None:
        problems.append("CHANGELOG.md が見つかりません")
    elif ("## [" + version + "]") not in changelog:
        # 未リリース分を書いている最中は見出しが無いのが正常なので情報扱いにする。
        problems.append(
            "CHANGELOG.md に見出し ## [" + version + "] がありません"
            "（リリース前なら想定内）")

    index = read(os.path.join("Website", "index.html"))
    if index is None:
        problems.append("Website/index.html が見つかりません")
    else:
        found = re.findall(r"<span data-release-version>v([^<]+)</span>", index)
        mismatched = [v for v in found if v != version]
        if not found:
            problems.append("Website/index.html に data-release-version がありません")
        elif mismatched:
            problems.append(
                "Website/index.html の表示バージョンが一致しません: " + ", ".join(sorted(set(mismatched))))

    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=os.getcwd(),
                        help="パッケージルート（package.json のあるディレクトリ）")
    parser.add_argument("--skip-git", action="store_true",
                        help="git を使う検査（Documentation~ の未追跡）を飛ばす")
    args = parser.parse_args()

    repo_root = os.path.abspath(args.repo_root)
    version = read_package_version(repo_root)
    if version is None:
        print("PRECOMMIT_ERROR=package.json の version を読み取れません")
        return 2

    failed = False

    missing, orphans = check_meta(repo_root)
    print("PRECOMMIT_META_MISSING=" + str(len(missing)))
    print("PRECOMMIT_META_ORPHAN=" + str(len(orphans)))
    if missing:
        failed = True
        print()
        print("■ .meta が欠けているファイル/ディレクトリ")
        for p in missing:
            print("  " + p)
    if orphans:
        failed = True
        print()
        print("■ 対応ファイルの無い孤児 .meta")
        for p in orphans:
            print("  " + p)

    if not args.skip_git:
        untracked = check_documentation_untracked(repo_root)
        if untracked is None:
            print("PRECOMMIT_DOC_UNTRACKED=skipped")
        else:
            print("PRECOMMIT_DOC_UNTRACKED=" + str(len(untracked)))
            if untracked:
                failed = True
                print()
                print("■ Documentation~/ の未追跡ファイル")
                print("  グローバル gitignore の *~ にマッチするため git add -f が必要です。")
                for p in untracked:
                    print("  " + p)

    version_problems = check_versions(repo_root, version)
    print("PRECOMMIT_VERSION=" + version)
    print("PRECOMMIT_VERSION_PROBLEMS=" + str(len(version_problems)))
    if version_problems:
        failed = True
        print()
        print("■ バージョン表記の食い違い（package.json が単一の真実）")
        for p in version_problems:
            print("  " + p)

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
