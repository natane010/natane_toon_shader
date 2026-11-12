# SPDX-License-Identifier: MIT
"""Maps a parsed Unity material onto a Natane Toon Blender material."""

import json
import os
import re

import bpy

from . import node_group, outline, props, unity_mat

# Shader defaults for properties missing from the .mat (NataneToonShader.shader).
UNITY_DEFAULTS = {
    "_ShadowOffset": 0.0, "_ShadowSharpness": 0.1, "_WrapAmount": 0.0, "_ShadowNaturalness": 0.0,
    "_SpecularIntensity": 0.8, "_SpecularSize": 0.1, "_SpecularSoftness": 0.05,
    "_RimPower": 3.0, "_RimIntensity": 1.0,
    "_MatCapIntensity": 1.0, "_MatCapBlend": 1.0, "_MatCapBlendMode": 0.0,
    "_Cutoff": 0.5, "_Cull": 2.0, "_OutlineWidth": 0.1, "_BumpScale": 1.0,
}
UNITY_COLOR_DEFAULTS = {
    "_Color": (1.0, 1.0, 1.0, 1.0), "_ShadowColor": (0.5, 0.5, 0.5, 1.0),
    "_SpecularColor": (1.0, 1.0, 1.0, 1.0), "_RimColor": (1.0, 1.0, 1.0, 1.0),
    "_EmissionColor": (0.0, 0.0, 0.0, 1.0), "_OutlineColor": (0.0, 0.0, 0.0, 1.0),
}

# (group input, Unity float)
FLOAT_MAP = (
    ("Shadow Offset", "_ShadowOffset"),
    ("Shadow Sharpness", "_ShadowSharpness"),
    ("Specular Intensity", "_SpecularIntensity"),
    ("Specular Size", "_SpecularSize"),
    ("Specular Softness", "_SpecularSoftness"),
    ("Rim Power", "_RimPower"),
    ("Rim Intensity", "_RimIntensity"),
    ("MatCap Intensity", "_MatCapIntensity"),
    ("MatCap Blend", "_MatCapBlend"),
    ("MatCap Blend Mode", "_MatCapBlendMode"),
    ("Cutoff", "_Cutoff"),
)
# (group input, Unity colour)
COLOR_MAP = (
    ("Color", "_Color"),
    ("Shadow Color", "_ShadowColor"),
    ("Specular Color", "_SpecularColor"),
    ("Rim Color", "_RimColor"),
    ("Emission Color", "_EmissionColor"),
)
# Keywords the preview reproduces or that do not change the look.
HANDLED_KEYWORDS = {
    "_SPECULAR", "_RIM_LIGHT", "_MATCAP", "_EMISSION", "_OUTLINE", "_NORMALMAP",
    "_PIXEL_VERTEX_LIGHTS", "_USE_LIGHT_VOLUME",
}
# (Unity texture, colour space)
TEXTURES = (
    ("_MainTex", "sRGB"),
    ("_MatCapTex", "sRGB"),
    ("_EmissionMap", "sRGB"),
    ("_BumpMap", "Non-Color"),
)

_BLENDER_SUFFIX_RE = re.compile(r"\.\d{3,}$")


def resolve_project_root(context, mat_path, explicit_root=""):
    """Explicit operator value > add-on preference > walk up from the .mat."""
    for candidate in (explicit_root, props.get_project_root_pref(context)):
        if candidate:
            candidate = bpy.path.abspath(candidate)
            if os.path.isdir(candidate):
                return unity_mat.normalize_project_root(candidate)
    return unity_mat.find_project_root(mat_path)


def _load_image(path, colorspace):
    try:
        image = bpy.data.images.load(path, check_existing=True)
    except RuntimeError as ex:
        return None, str(ex)
    try:
        image.colorspace_settings.name = colorspace
    except TypeError:
        pass
    return image, ""


# (keyword, settings attribute or None, float toggle property)
TOGGLES = (
    ("_SPECULAR", "use_specular", "_Specular"),
    ("_RIM_LIGHT", "use_rim", "_RimLight"),
    ("_MATCAP", "use_matcap", "_MatCap"),
    ("_EMISSION", "use_emission", "_Emission"),
    ("_OUTLINE", "use_outline", "_Outline"),
    ("_NORMALMAP", None, "_UseNormalMap"),
)


def texture_image(mat, unity_prop):
    node = mat.node_tree.nodes.get(node_group.TEX_NODE_NAMES[unity_prop])
    return node.image if node is not None and node.bl_idname == 'ShaderNodeTexImage' else None


def image_abspath(image):
    if image is None or not image.filepath:
        return ""
    return os.path.normpath(bpy.path.abspath(image.filepath, library=image.library))


def read_state(mat):
    """Current Blender-side values keyed by Unity property name (inverse of the importer)."""
    group = node_group.find_main_node(mat)
    settings = mat.natane_toon
    nodes = mat.node_tree.nodes
    floats = {prop: float(group.inputs[socket].default_value) for socket, prop in FLOAT_MAP}
    floats["_WrapAmount"] = float(group.inputs["Wrap Amount"].default_value)
    floats["_OutlineWidth"] = float(settings.outline_width)
    floats["_Cull"] = 2.0 if settings.cull_back else 0.0
    normal_map = nodes.get(node_group.TEX_NODE_NAMES["_BumpMap"] + " NormalMap")
    if normal_map is not None:
        floats["_BumpScale"] = float(normal_map.inputs["Strength"].default_value)
    colors = {prop: [float(c) for c in group.inputs[socket].default_value[:3]] for socket, prop in COLOR_MAP}
    colors["_OutlineColor"] = [float(c) for c in settings.outline_color]
    toggles = {}
    for keyword, attr, _toggle in TOGGLES:
        toggles[keyword] = bool(getattr(settings, attr)) if attr else texture_image(mat, "_BumpMap") is not None
    textures = {}
    for prop, _colorspace in TEXTURES:
        image = texture_image(mat, prop)
        mapping = nodes.get(node_group.TEX_NODE_NAMES[prop] + " Mapping")
        entry = {"image": image.name if image is not None else "", "path": os.path.normcase(image_abspath(image)),
                 "scale": [1.0, 1.0], "offset": [0.0, 0.0]}
        if mapping is not None:
            entry["scale"] = [float(v) for v in mapping.inputs["Scale"].default_value[:2]]
            entry["offset"] = [float(v) for v in mapping.inputs["Location"].default_value[:2]]
        textures[prop] = entry
    return {"floats": floats, "colors": colors, "color_alpha": float(group.inputs["Color Alpha"].default_value),
            "toggles": toggles, "textures": textures, "alpha_mode": settings.alpha_mode}


def apply_unity_material(mat, um, project_root="", load_textures=True, alpha_override='AUTO'):
    """Apply a parsed UnityMaterial to a Blender material. Returns a report dict."""
    report = {"material": mat.name, "source": um.path, "inputs": {}, "textures": {}, "warnings": []}
    warn = report["warnings"].append

    group = node_group.find_main_node(mat)
    if group is None:
        group = node_group.setup_material(mat, keep_base=False)
    settings = mat.natane_toon
    settings.source_mat = um.path

    if um.shader_guid in unity_mat.UNSUPPORTED_SHADER_GUIDS:
        warn("シェーダー '%s' はプレビュー非対応です（基本モデルとして読み込みます）"
             % unity_mat.UNSUPPORTED_SHADER_GUIDS[um.shader_guid])

    def fval(name):
        return um.floats.get(name, UNITY_DEFAULTS.get(name, 0.0))

    def set_input(name, value):
        group.inputs[name].default_value = value
        report["inputs"][name] = tuple(round(v, 5) for v in value) if isinstance(value, tuple) else round(value, 5)

    for socket, prop in FLOAT_MAP:
        set_input(socket, fval(prop))
    # the shader raises the wrap floor with _ShadowNaturalness
    set_input("Wrap Amount", max(fval("_WrapAmount"), fval("_ShadowNaturalness") * 0.35))
    for socket, prop in COLOR_MAP:
        raw = um.colors.get(prop, UNITY_COLOR_DEFAULTS[prop])
        linear = unity_mat.color_to_linear(raw)
        set_input(socket, (linear[0], linear[1], linear[2], 1.0))
        if prop == "_Color":
            set_input("Color Alpha", min(max(raw[3], 0.0), 1.0))

    # ---- feature toggles
    settings.use_specular = um.has_keyword("_SPECULAR", "_Specular")
    settings.use_rim = um.has_keyword("_RIM_LIGHT", "_RimLight")
    settings.use_matcap = um.has_keyword("_MATCAP", "_MatCap") and um.floats.get("_MatCap", 1.0) >= 0.5
    settings.use_emission = um.has_keyword("_EMISSION", "_Emission")
    use_normal_map = um.has_keyword("_NORMALMAP", "_UseNormalMap")

    # ---- alpha / culling
    mode, reason = um.alpha_mode()
    if alpha_override != 'AUTO':
        mode, reason = alpha_override, "override"
    settings.alpha_mode = mode
    settings.cull_back = int(fval("_Cull")) == 2
    report["alpha_mode"] = "%s (%s)" % (mode, reason)

    # ---- textures
    index = {}
    if load_textures:
        if project_root:
            index = unity_mat.build_guid_index(project_root)
        else:
            warn("Unity プロジェクトのフォルダが見つからないためテクスチャを解決できません")
    # remember where every texture lives (also the ones the preview does not show)
    for entry in um.textures.values():
        if entry["guid"] and not entry.get("path"):
            entry["path"] = index.get(entry["guid"], "")
    for prop, colorspace in TEXTURES:
        entry = um.textures.get(prop)
        guid = entry["guid"] if entry else ""
        wanted = bool(guid) and load_textures
        if prop == "_BumpMap" and not use_normal_map:
            wanted = False
        if prop == "_MatCapTex" and not settings.use_matcap:
            wanted = False
        if prop == "_EmissionMap" and not settings.use_emission:
            wanted = False
        image = None
        if wanted:
            path = index.get(guid)
            if path is None:
                warn("%s: GUID %s の画像が見つかりません" % (prop, guid))
            else:
                image, error = _load_image(path, colorspace)
                if image is None:
                    warn("%s: 画像を読み込めません (%s)" % (prop, error or path))
                elif prop == "_MainTex":
                    # keep RGB of fully transparent texels like Unity does
                    image.alpha_mode = 'CHANNEL_PACKED'
        if image is not None or load_textures:
            node_group.set_texture(mat, prop, image, entry["scale"] if entry else (1.0, 1.0),
                                   entry["offset"] if entry else (0.0, 0.0), fval("_BumpScale"))
        report["textures"][prop] = image.filepath if image is not None else ("" if not guid else "<%s>" % guid)
    # shader default for a missing MatCap texture is "black"
    group.inputs["MatCap Tex"].default_value = (0.0, 0.0, 0.0, 1.0)

    # ---- outline
    oc = unity_mat.color_to_linear(um.colors.get("_OutlineColor", UNITY_COLOR_DEFAULTS["_OutlineColor"]))
    settings.outline_color = [min(max(c, 0.0), 1.0) for c in oc[:3]]
    settings.outline_width = min(max(fval("_OutlineWidth"), 0.0), 1.0)
    settings.use_outline = um.has_keyword("_OUTLINE", "_Outline")
    report["outline"] = {"enabled": settings.use_outline, "width": round(settings.outline_width, 5),
                         "color": tuple(round(c, 5) for c in settings.outline_color)}
    report["toggles"] = {k: getattr(settings, k) for k in ("use_specular", "use_rim", "use_matcap", "use_emission")}
    report["toggles"]["normal_map"] = use_normal_map

    props.push_settings(mat)

    # ---- round trip data for the exporter
    baseline = read_state(mat)
    baseline["alpha_mode"] = um.alpha_mode()[0]   # an import-time override counts as a user change
    settings.unity_data = json.dumps(um.to_dict(), ensure_ascii=False)
    settings.unity_baseline = json.dumps(baseline, ensure_ascii=False)

    if um.keywords:
        ignored = sorted(um.keywords - HANDLED_KEYWORDS)
        if ignored:
            report["ignored_keywords"] = ignored
            warn("プレビュー非対応の機能: " + ", ".join(ignored))
    return report


def import_mat_file(context, mat, path, project_root="", load_textures=True, alpha_override='AUTO'):
    um = unity_mat.read_mat(path)
    root = resolve_project_root(context, path, project_root) if load_textures else ""
    report = apply_unity_material(mat, um, root, load_textures, alpha_override)
    report["project_root"] = root
    return report


def find_mat_files(directory, recursive=True):
    """lower-case material name -> .mat path."""
    found = {}
    if recursive:
        for dirpath, _dirnames, filenames in os.walk(directory):
            for name in filenames:
                if name.lower().endswith(".mat"):
                    found.setdefault(name[:-4].lower(), os.path.join(dirpath, name))
    else:
        for name in os.listdir(directory):
            if name.lower().endswith(".mat"):
                found.setdefault(name[:-4].lower(), os.path.join(directory, name))
    return found


def match_mat_file(material_name, files):
    """<name>.mat, also trying the name without Blender's '.001' suffix."""
    key = material_name.lower()
    if key in files:
        return files[key]
    stripped = _BLENDER_SUFFIX_RE.sub("", key)
    return files.get(stripped)

