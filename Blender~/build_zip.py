#!/usr/bin/env python
# SPDX-License-Identifier: MIT
"""Create natane_toon_preview.zip next to this script (for Blender's "Install from Disk").

    python build_zip.py
"""

import os
import sys
import zipfile

ADDON = "natane_toon_preview"
EXCLUDE_DIRS = {"__pycache__"}
EXCLUDE_SUFFIXES = (".pyc", ".pyo", ".meta")


def build(base_dir):
    source = os.path.join(base_dir, ADDON)
    target = os.path.join(base_dir, ADDON + ".zip")
    if not os.path.isfile(os.path.join(source, "__init__.py")):
        raise SystemExit("add-on folder not found: " + source)
    count = 0
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
        for dirpath, dirnames, filenames in os.walk(source):
            dirnames[:] = sorted(d for d in dirnames if d not in EXCLUDE_DIRS)
            for name in sorted(filenames):
                if name.endswith(EXCLUDE_SUFFIXES):
                    continue
                path = os.path.join(dirpath, name)
                # keep the top-level folder: works for legacy add-on and extension install
                arcname = os.path.join(ADDON, os.path.relpath(path, source)).replace(os.sep, "/")
                archive.write(path, arcname)
                count += 1
    return target, count


if __name__ == "__main__":
    out, files = build(os.path.dirname(os.path.abspath(__file__)))
    print("wrote %s (%d files)" % (out, files))
    sys.exit(0)
