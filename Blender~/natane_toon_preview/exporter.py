# SPDX-License-Identifier: MIT
"""Export for Unity: <name>.fbx + <name>.natanetoon.json (+ textures/).

The JSON is read by Unity's JsonUtility: arrays of objects only, every key always present.
For a material that came from a .mat, everything the importer read is written back and only
the values that differ from the state right after the import are replaced.
"""

import contextlib
import filecmp
import json
import os
import re
import shutil
import sys

import bpy

from . import importer, node_group, outline, unity_mat

FORMAT_NAME = "natane-toon-material-set"
FORMAT_VERSION = 1
JSON_SUFFIX = ".natanetoon.json"
TEXTURE_DIR = "textures"

_UNSAFE_RE = re.compile(r'[<>:"/\\|?*\x00-\x1f]')


def _close(a, b):
    return abs(a - b) <= 1e-6 * max(1.0, abs(a), abs(b))


def _clean(value):
    """float32 socket value -> short decimal (0.30000001192 -> 0.3)."""
    return float("%.7g" % value)


def _generator():
    package = sys.modules.get(__package__)
    version = getattr(package, "bl_info", {}).get("version", (0, 0, 0))
    return "natane_toon_preview %s / Blender %s" % (".".join(str(v) for v in version), bpy.app.version_string)


# ---------------------------------------------------------------- textures

class TextureWriter:
    """Copies / saves textures next to the JSON and hands out relative paths."""

    def __init__(self, json_dir, copy):
        self.json_dir = json_dir
        self.copy = copy
        self.by_source = {}
        self.used_names = set()
        self.copied = 0
        self.unchanged = 0
        self.warnings = []

    def _unique_name(self, name):
        name = _UNSAFE_RE.sub("_", name) or "texture"
        stem, ext = os.path.splitext(name)
        candidate, n = name, 1
        while candidate.lower() in self.used_names:
            candidate = "%s_%d%s" % (stem, n, ext)
            n += 1
        self.used_names.add(candidate.lower())
        return candidate

    def _target(self, name):
        directory = os.path.join(self.json_dir, TEXTURE_DIR)
        os.makedirs(directory, exist_ok=True)
        name = self._unique_name(name)
        return os.path.join(directory, name), TEXTURE_DIR + "/" + name

    def add_file(self, source):
        """Path (relative to the JSON) for a texture file on disk; '' if it cannot be provided."""
        if not source:
            return ""
        key = os.path.normcase(os.path.abspath(source))
        if key in self.by_source:
            return self.by_source[key]
        result = ""
        if not os.path.isfile(source):
            self.warnings.append("テクスチャがありません: " + source)
        elif not self.copy:
            try:
                result = os.path.relpath(source, self.json_dir).replace(os.sep, "/")
            except ValueError:   # other drive: no relative path exists
                self.warnings.append("別ドライブのため相対パスにできません: " + source)
        else:
            dest, result = self._target(os.path.basename(source))
            try:
                if os.path.isfile(dest) and filecmp.cmp(source, dest, shallow=False):
                    self.unchanged += 1
                else:
                    shutil.copy2(source, dest)
                    self.copied += 1
            except OSError as ex:
                self.warnings.append("コピーできません: %s (%s)" % (source, ex))
                result = ""
        self.by_source[key] = result
        return result

    def add_image(self, image):
        """Like add_file for a Blender image; packed / generated images are saved as PNG."""
        if image is None:
            return ""
        path = importer.image_abspath(image)
        if image.source == 'FILE' and image.packed_file is None and path and os.path.isfile(path):
            return self.add_file(path)
        key = ("image", image.name)
        if key in self.by_source:
            return self.by_source[key]
        result = ""
        if not self.copy:
            self.warnings.append("ファイルの無い画像はテクスチャのコピーが必要です: " + image.name)
        elif not image.has_data and image.size[0] == 0:
            self.warnings.append("画像にデータがありません: " + image.name)
        else:
            dest, result = self._target(os.path.splitext(image.name)[0] + ".png")
            if not _save_png(image, dest):
                self.warnings.append("PNG を保存できません: " + image.name)
                result = ""
            else:
                self.copied += 1
        self.by_source[key] = result
        return result


def _save_png(image, dest):
    """Write the pixels of any image to a PNG without touching the image itself."""
    width, height = image.size
    if width == 0 or height == 0:
        return False
    copy = bpy.data.images.new("natane_export_tmp", width, height, alpha=True, float_buffer=image.is_float)
    try:
        import numpy as np
        # Settings first: changing the colour space regenerates a generated image (blank).
        copy.colorspace_settings.name = image.colorspace_settings.name
        copy.alpha_mode = image.alpha_mode
        pixels = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        copy.pixels.foreach_set(pixels)
        copy.update()
        copy.file_format = 'PNG'
        copy.filepath_raw = dest
        copy.save()
        return os.path.isfile(dest)
    except (RuntimeError, TypeError, ValueError):
        return False
    finally:
        bpy.data.images.remove(copy)


# ---------------------------------------------------------------- materials

def _stored(mat):
    """(UnityMaterial, baseline) saved by the importer, or (None, None)."""
    settings = mat.natane_toon
    if not settings.unity_data or not settings.unity_baseline:
        return None, None
    try:
        return (unity_mat.UnityMaterial.from_dict(json.loads(settings.unity_data)),
                json.loads(settings.unity_baseline))
    except (ValueError, TypeError, AttributeError):
        return None, None


def build_material_entry(mat, writer):
    """One element of "materials". Also returns the list of Unity properties that were overridden."""
    state = importer.read_state(mat)
    um, baseline = _stored(mat)
    scratch = um is None
    if scratch:
        um, baseline = unity_mat.UnityMaterial(), {}
    changed = []

    floats = dict(um.floats)
    colors = {name: tuple(value) for name, value in um.colors.items()}
    keywords = set(um.keywords or ())

    # ---- floats
    base_floats = baseline.get("floats", {})
    for name, value in state["floats"].items():
        if scratch or name not in base_floats or not _close(value, base_floats[name]):
            floats[name] = _clean(value)
            changed.append(name)

    # ---- colours: Blender holds linear RGB, the .mat holds sRGB-encoded values
    base_colors = baseline.get("colors", {})
    for name, rgb in state["colors"].items():
        old = base_colors.get(name)
        if scratch or old is None or not all(_close(a, b) for a, b in zip(rgb, old)):
            alpha = colors.get(name, importer.UNITY_COLOR_DEFAULTS.get(name, (0.0, 0.0, 0.0, 1.0)))[3]
            colors[name] = tuple(_clean(unity_mat.linear_to_srgb(c)) for c in rgb) + (alpha,)
            changed.append(name)
    if scratch or not _close(state["color_alpha"], baseline.get("color_alpha", -1.0)):
        c = colors.get("_Color", (1.0, 1.0, 1.0, 1.0))
        colors["_Color"] = (c[0], c[1], c[2], _clean(state["color_alpha"]))
        changed.append("_Color.a")

    # ---- feature toggles: keyword + its float
    base_toggles = baseline.get("toggles", {})
    for keyword, _attr, toggle in importer.TOGGLES:
        enabled = state["toggles"][keyword]
        if scratch or base_toggles.get(keyword) != enabled:
            keywords.discard(keyword)
            if enabled:
                keywords.add(keyword)
            floats[toggle] = 1.0 if enabled else 0.0
            changed.append(keyword)

    # ---- shader variant
    mode = state["alpha_mode"]
    if scratch or mode != baseline.get("alpha_mode"):
        shader, shader_guid = unity_mat.BASE_SHADERS[mode]
        render_queue = -1
        changed.append("shader")
    else:
        shader_guid = um.shader_guid
        shader = unity_mat.SHADER_NAMES.get(shader_guid, unity_mat.BASE_SHADERS[mode][0])
        render_queue = um.render_queue

    # ---- textures
    textures = {name: dict(entry) for name, entry in um.textures.items()}
    base_textures = baseline.get("textures", {})
    current_images = {}
    for prop, _colorspace in importer.TEXTURES:
        cur = state["textures"][prop]
        old = base_textures.get(prop)
        image = importer.texture_image(mat, prop)
        if scratch and image is None:
            continue
        entry = textures.setdefault(prop, {"guid": "", "scale": (1.0, 1.0), "offset": (0.0, 0.0), "path": ""})
        if scratch or old is None or cur["image"] != old["image"] or cur["path"] != old["path"]:
            entry["guid"] = ""
            entry["path"] = ""
            current_images[prop] = image      # may be None: texture removed
            changed.append(prop)
        elif image is not None:
            current_images[prop] = image
        if scratch or old is None or not all(_close(a, b) for a, b in
                                             zip(cur["scale"] + cur["offset"], old["scale"] + old["offset"])):
            entry["scale"] = tuple(_clean(v) for v in cur["scale"])
            entry["offset"] = tuple(_clean(v) for v in cur["offset"])
            if not scratch:
                changed.append(prop + "_ST")

    texture_list = []
    for name, entry in textures.items():
        if name in current_images:
            path = writer.add_image(current_images[name])
        else:
            path = writer.add_file(entry.get("path", ""))
        texture_list.append({
            "name": name, "path": path, "guid": entry.get("guid", ""),
            "scaleX": entry["scale"][0], "scaleY": entry["scale"][1],
            "offsetX": entry["offset"][0], "offsetY": entry["offset"][1],
        })

    entry = {
        "name": mat.name,
        "shader": shader,
        "shaderGuid": shader_guid,
        "sourceMaterialGuid": um.source_guid,
        "renderQueue": int(render_queue),
        "keywords": sorted(keywords),
        "floats": [{"name": name, "value": value} for name, value in floats.items()],
        "colors": [{"name": name, "r": c[0], "g": c[1], "b": c[2], "a": c[3]} for name, c in colors.items()],
        "textures": texture_list,
    }
    return entry, changed


# ---------------------------------------------------------------- objects / FBX

def export_objects(context, selected_only):
    return list(context.selected_objects if selected_only else context.visible_objects)


@contextlib.contextmanager
def outline_suspended(objects):
    """Hide the add-on's outline (Solidify + trailing outline slots) and put it back afterwards."""
    modifiers = []     # (object name, show_viewport, show_render)
    slots = []         # (mesh, [outline materials])
    seen = set()
    for obj in objects:
        if obj.type != 'MESH':
            continue
        mod = obj.modifiers.get(outline.MODIFIER_NAME)
        if mod is not None:
            modifiers.append((obj.name, mod.show_viewport, mod.show_render))
            mod.show_viewport = False
            mod.show_render = False
        mesh = obj.data
        if mesh.name_full in seen:
            continue
        seen.add(mesh.name_full)
        removed = []
        materials = mesh.materials
        while len(materials) > 0 and outline.is_outline_material(materials[len(materials) - 1]):
            removed.insert(0, materials.pop(index=len(materials) - 1))
        if removed:
            slots.append((mesh, removed))
    try:
        yield
    finally:
        for mesh, removed in slots:
            for mat in removed:
                mesh.materials.append(mat)
        for name, show_viewport, show_render in modifiers:
            obj = bpy.data.objects.get(name)
            mod = obj.modifiers.get(outline.MODIFIER_NAME) if obj is not None else None
            if mod is not None:
                mod.show_viewport = show_viewport
                mod.show_render = show_render


def write_fbx(filepath, selected_only):
    """FBX with settings for Unity. Raises RuntimeError when the FBX add-on is unavailable."""
    if not hasattr(bpy.ops.export_scene, "fbx"):
        raise RuntimeError("FBX エクスポーター (io_scene_fbx) が有効になっていません")
    result = bpy.ops.export_scene.fbx(
        filepath=filepath,
        check_existing=False,
        use_selection=selected_only,
        use_visible=not selected_only,
        object_types={'EMPTY', 'ARMATURE', 'MESH', 'OTHER'},
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
    )
    if 'FINISHED' not in result:
        raise RuntimeError("FBX の書き出しに失敗しました")


def split_base_path(filepath):
    """'dir/Name.fbx' or 'dir/Name.natanetoon.json' -> 'dir/Name'."""
    lower = filepath.lower()
    for suffix in (JSON_SUFFIX, ".fbx", ".json"):
        if lower.endswith(suffix):
            return filepath[:-len(suffix)]
    return filepath


def export_set(context, filepath, selected_only=True, export_fbx=True, copy_textures=True):
    """Write the material set (and FBX). Returns a result dict; raises RuntimeError on failure."""
    base = split_base_path(bpy.path.abspath(filepath))
    json_dir = os.path.dirname(base)
    name = os.path.basename(base)
    if not name:
        raise RuntimeError("ファイル名がありません")
    objects = export_objects(context, selected_only)
    meshes = [o for o in objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError("書き出すメッシュがありません")
    os.makedirs(json_dir, exist_ok=True)

    writer = TextureWriter(json_dir, copy_textures)
    entries, skipped, overrides = [], [], {}
    seen = set()
    for obj in meshes:
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None or mat.name_full in seen or outline.is_outline_material(mat):
                continue
            seen.add(mat.name_full)
            if node_group.find_main_node(mat) is None:
                skipped.append(mat.name)
                continue
            entry, changed = build_material_entry(mat, writer)
            entries.append(entry)
            overrides[mat.name] = changed

    fbx_path = base + ".fbx"
    if export_fbx:
        with outline_suspended(objects):
            write_fbx(fbx_path, selected_only)

    data = {
        "format": FORMAT_NAME,
        "version": FORMAT_VERSION,
        "generator": _generator(),
        "model": os.path.basename(fbx_path) if export_fbx else "",
        "materials": entries,
    }
    json_path = base + JSON_SUFFIX
    with open(json_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1, allow_nan=False)
        f.write("\n")

    warnings = list(writer.warnings)
    if skipped:
        warnings.append("Natane Toon ではないため JSON に含めていないマテリアル: " + ", ".join(skipped))
    return {
        "json": json_path, "fbx": fbx_path if export_fbx else "", "materials": [e["name"] for e in entries],
        "skipped": skipped, "overrides": overrides, "textures_copied": writer.copied,
        "textures_unchanged": writer.unchanged, "warnings": warnings,
    }
