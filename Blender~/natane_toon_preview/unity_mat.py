# SPDX-License-Identifier: MIT
"""Unity .mat (text YAML) reader and texture GUID resolver. No bpy / PyYAML dependency."""

import os
import re

# Natane shader variant GUID -> alpha mode. Anything unknown falls back to heuristics.
SHADER_GUID_MODES = {
    "e19aa1698bfce6940b905a2756b553db": "OPAQUE",       # Toon Shader
    "557a523b09cd1404fa1cb1bf9dd3197a": "OPAQUE",       # Lite
    "fcfa5cb27a6ea014f806a929db7dc44c": "OPAQUE",       # Background
    "11e4d15f686245489c15efda77cf7ae2": "OPAQUE",       # ScreenEdgeSplit
    "d7fe6d748deaaf34f8716d464460b268": "CUTOUT",       # Cutout
    "16ff5a7ff6d4ae14090d9bb36085c805": "CUTOUT",       # Cutout Lite
    "3660775d58854822a089206831f359d3": "CUTOUT",       # Sticker
    "60e1faedb0f14fd9bcc9236de42bb193": "CUTOUT",       # XRay
    "aaf4564822ce2c34291df8d85372eb21": "TRANSPARENT",  # Transparent
    "a14e61f254e4bad4b9bf99c0605d8434": "TRANSPARENT",  # Transparent Lite
    "e4e2a0747aa1391448de1c53c40a85b4": "TRANSPARENT",  # Fur
    "99db8a1b1acbe584ba55dec38bfef611": "TRANSPARENT",  # Fur Lite
    "a0f11a19bf9940728a35ae4905b8bef1": "TRANSPARENT",  # Ghost
}
# Variants the preview cannot represent at all (the importer warns).
UNSUPPORTED_SHADER_GUIDS = {
    "c8e0d47b31a94a5f9b26f3d80e15c7a2": "FakeShadow",
    "a9c17fa1a8b94334bcc008bbbd4f361d": "Particle",
}

# Shader GUID -> exact Unity shader name (from the "Shader" line of each variant).
SHADER_NAMES = {
    "e19aa1698bfce6940b905a2756b553db": "Natane/Toon Shader",
    "557a523b09cd1404fa1cb1bf9dd3197a": "Natane/Toon Shader (Lite)",
    "fcfa5cb27a6ea014f806a929db7dc44c": "Natane/Toon Shader (Background)",
    "11e4d15f686245489c15efda77cf7ae2": "Natane/Toon Shader (ScreenEdge Split)",
    "d7fe6d748deaaf34f8716d464460b268": "Natane/Toon Shader (Cutout)",
    "16ff5a7ff6d4ae14090d9bb36085c805": "Natane/Toon Shader (Cutout Lite)",
    "3660775d58854822a089206831f359d3": "Natane/Toon Shader (Sticker)",
    "60e1faedb0f14fd9bcc9236de42bb193": "Natane/Toon Shader (X-Ray)",
    "aaf4564822ce2c34291df8d85372eb21": "Natane/Toon Shader (Transparent)",
    "a14e61f254e4bad4b9bf99c0605d8434": "Natane/Toon Shader (Transparent Lite)",
    "e4e2a0747aa1391448de1c53c40a85b4": "Natane/Toon Shader (Fur)",
    "99db8a1b1acbe584ba55dec38bfef611": "Natane/Toon Shader (Fur Lite)",
    "a0f11a19bf9940728a35ae4905b8bef1": "Natane/Toon Shader (Ghost)",
    "c8e0d47b31a94a5f9b26f3d80e15c7a2": "Natane/Toon Shader FakeShadow",
    "a9c17fa1a8b94334bcc008bbbd4f361d": "Natane/Toon Shader (Particle)",
}
# Alpha mode -> (shader name, guid) used for new materials and when the mode was changed.
BASE_SHADERS = {
    "OPAQUE": ("Natane/Toon Shader", "e19aa1698bfce6940b905a2756b553db"),
    "CUTOUT": ("Natane/Toon Shader (Cutout)", "d7fe6d748deaaf34f8716d464460b268"),
    "TRANSPARENT": ("Natane/Toon Shader (Transparent)", "aaf4564822ce2c34291df8d85372eb21"),
}

IMAGE_EXTENSIONS = (
    ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".tif", ".tiff", ".exr", ".hdr",
    ".psd", ".webp", ".dds",
)

_KEY_RE = re.compile(r"^(\s*)(- )?([^\s:#][^:]*):(?:\s+(.*)|)$")
_GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$")


class UnityMaterial:
    """Parsed subset of a Unity Material asset."""

    def __init__(self):
        self.path = ""
        self.name = ""
        self.shader_guid = ""
        self.render_queue = -1
        self.keywords = None      # set of str, or None when the file has no keyword list
        self.tags = {}            # stringTagMap
        self.floats = {}          # name -> float (m_Floats and m_Ints)
        self.colors = {}          # name -> (r, g, b, a), raw values as stored
        self.textures = {}        # name -> {"guid", "scale", "offset"} (+ "path" once resolved)
        self.source_guid = ""     # guid of the .mat itself (from its .meta)

    def to_dict(self):
        """JSON-serialisable copy (stored on the Blender material for the exporter)."""
        return {
            "path": self.path, "name": self.name, "shader_guid": self.shader_guid,
            "source_guid": self.source_guid, "render_queue": self.render_queue,
            "keywords": None if self.keywords is None else sorted(self.keywords),
            "tags": dict(self.tags), "floats": dict(self.floats),
            "colors": {k: list(v) for k, v in self.colors.items()},
            "textures": {k: {"guid": v.get("guid", ""), "scale": list(v.get("scale", (1.0, 1.0))),
                             "offset": list(v.get("offset", (0.0, 0.0))), "path": v.get("path", "")}
                         for k, v in self.textures.items()},
        }

    @classmethod
    def from_dict(cls, data):
        mat = cls()
        mat.path = data.get("path", "")
        mat.name = data.get("name", "")
        mat.shader_guid = data.get("shader_guid", "")
        mat.source_guid = data.get("source_guid", "")
        mat.render_queue = int(data.get("render_queue", -1))
        keywords = data.get("keywords")
        mat.keywords = None if keywords is None else set(keywords)
        mat.tags = dict(data.get("tags", {}))
        mat.floats = dict(data.get("floats", {}))
        mat.colors = {k: tuple(v) for k, v in data.get("colors", {}).items()}
        mat.textures = {k: {"guid": v.get("guid", ""), "scale": tuple(v.get("scale", (1.0, 1.0))),
                            "offset": tuple(v.get("offset", (0.0, 0.0))), "path": v.get("path", "")}
                        for k, v in data.get("textures", {}).items()}
        return mat

    def has_keyword(self, keyword, toggle_prop=None):
        """Keyword state; falls back to the float toggle when no keyword list was saved."""
        if self.keywords is not None:
            return keyword in self.keywords
        if toggle_prop is not None:
            return self.floats.get(toggle_prop, 0.0) >= 0.5
        return False

    def alpha_mode(self):
        """Returns (mode, reason). mode is OPAQUE / CUTOUT / TRANSPARENT."""
        mode = SHADER_GUID_MODES.get(self.shader_guid)
        if mode:
            return mode, "shader guid"
        render_type = self.tags.get("RenderType", "")
        if render_type == "TransparentCutout":
            return "CUTOUT", "RenderType tag"
        if render_type == "Transparent":
            return "TRANSPARENT", "RenderType tag"
        kw = self.keywords or set()
        if "_ALPHABLEND_ON" in kw or "_ALPHAPREMULTIPLY_ON" in kw:
            return "TRANSPARENT", "keyword"
        if "_ALPHATEST_ON" in kw:
            return "CUTOUT", "keyword"
        std_mode = self.floats.get("_Mode")
        if std_mode is not None:
            if std_mode >= 1.5:
                return "TRANSPARENT", "_Mode"
            if std_mode >= 0.5:
                return "CUTOUT", "_Mode"
        # SrcAlpha(5) / OneMinusSrcAlpha(10) blending
        if self.floats.get("_SrcBlend", 1.0) == 5.0 and self.floats.get("_DstBlend", 0.0) == 10.0:
            return "TRANSPARENT", "_SrcBlend/_DstBlend"
        if self.render_queue > 2500:
            return "TRANSPARENT", "render queue"
        if self.render_queue >= 2450:
            return "CUTOUT", "render queue"
        return "OPAQUE", "default"


def _parse_scalar(text):
    text = text.strip()
    if len(text) >= 2 and text[0] == text[-1] and text[0] in "'\"":
        return text[1:-1]
    return text


def _parse_float(text, default=0.0):
    try:
        return float(_parse_scalar(text))
    except (TypeError, ValueError):
        return default


def _parse_flow_map(text):
    """'{r: 1, g: 0.5}' -> {'r': '1', 'g': '0.5'} (flat maps only)."""
    text = text.strip()
    if not (text.startswith("{") and text.endswith("}")):
        return {}
    result = {}
    for part in text[1:-1].split(","):
        if ":" in part:
            key, value = part.split(":", 1)
            result[key.strip()] = value.strip()
    return result


def _join_wrapped_lines(lines):
    """Unity wraps long flow mappings over several lines; glue them back together."""
    out = []
    pending = None
    for line in lines:
        line = line.rstrip("\r\n")
        if pending is not None:
            pending += " " + line.strip()
            if pending.count("{") <= pending.count("}"):
                out.append(pending)
                pending = None
            continue
        if line.count("{") > line.count("}"):
            pending = line
            continue
        out.append(line)
    if pending is not None:
        out.append(pending)
    return out


def parse_mat_text(text):
    """Parse the text of a Unity .mat file. Raises ValueError if it is not a text material."""
    if "\x00" in text[:256]:
        raise ValueError("binary asset (Unity: Edit > Project Settings > Editor > Asset Serialization = Force Text)")
    lines = _join_wrapped_lines(text.splitlines())
    mat = UnityMaterial()

    in_material = False
    section = None       # top-level list/map currently being read (key name)
    section_indent = -1
    saved_indent = -1    # indent of m_SavedProperties, -1 when outside
    prop_kind = None     # m_TexEnvs / m_Floats / m_Ints / m_Colors
    tex_name = None

    for line in lines:
        if line.startswith("--- "):
            # Only the first Material document is read.
            if in_material:
                break
            continue
        stripped = line.strip()
        if not stripped or stripped.startswith(("%", "#")):
            continue
        if stripped == "Material:":
            in_material = True
            continue
        if not in_material:
            continue

        # plain list item such as "  - _MATCAP"
        if stripped.startswith("- ") and ":" not in stripped:
            if section == "m_ValidKeywords":
                mat.keywords.add(_parse_scalar(stripped[2:]))
            continue

        m = _KEY_RE.match(line)
        if not m:
            continue
        indent = len(m.group(1))
        is_item = m.group(2) is not None
        key = m.group(3).strip()
        value = (m.group(4) or "").strip()

        if saved_indent >= 0 and indent > saved_indent:
            if not is_item and key in ("m_TexEnvs", "m_Floats", "m_Ints", "m_Colors"):
                prop_kind = key
                tex_name = None
            elif prop_kind == "m_TexEnvs":
                if is_item:
                    tex_name = key
                    mat.textures[tex_name] = {"guid": "", "scale": (1.0, 1.0), "offset": (0.0, 0.0)}
                elif tex_name is not None:
                    fm = _parse_flow_map(value)
                    entry = mat.textures[tex_name]
                    if key == "m_Texture":
                        entry["guid"] = fm.get("guid", "").lower()
                    elif key == "m_Scale":
                        entry["scale"] = (_parse_float(fm.get("x"), 1.0), _parse_float(fm.get("y"), 1.0))
                    elif key == "m_Offset":
                        entry["offset"] = (_parse_float(fm.get("x")), _parse_float(fm.get("y")))
            elif prop_kind in ("m_Floats", "m_Ints") and is_item:
                mat.floats[key] = _parse_float(value)
            elif prop_kind == "m_Colors" and is_item:
                fm = _parse_flow_map(value)
                mat.colors[key] = tuple(_parse_float(fm.get(c), 1.0 if c == "a" else 0.0) for c in "rgba")
            continue
        saved_indent = -1
        prop_kind = None

        # entries of a top-level map (stringTagMap)
        if section == "stringTagMap" and indent > section_indent and not is_item:
            mat.tags[key] = _parse_scalar(value)
            continue
        section = None

        if key == "m_Name":
            mat.name = _parse_scalar(value)
        elif key == "m_Shader":
            mat.shader_guid = _parse_flow_map(value).get("guid", "").lower()
        elif key == "m_CustomRenderQueue":
            mat.render_queue = int(_parse_float(value, -1))
        elif key == "m_ValidKeywords":
            mat.keywords = set() if mat.keywords is None else mat.keywords
            section, section_indent = key, indent
        elif key == "m_ShaderKeywords":
            # Unity <= 2021.1: one space separated string
            mat.keywords = set() if mat.keywords is None else mat.keywords
            mat.keywords.update(_parse_scalar(value).split())
        elif key == "stringTagMap":
            section, section_indent = key, indent
        elif key == "m_SavedProperties":
            saved_indent = indent

    if not in_material:
        raise ValueError("no 'Material:' document found")
    return mat


def read_mat(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    mat = parse_mat_text(text)
    mat.path = path
    mat.source_guid = read_meta_guid(path + ".meta")
    if not mat.name:
        mat.name = os.path.splitext(os.path.basename(path))[0]
    return mat


# ---------------------------------------------------------------- colour

def srgb_to_linear(c):
    """sRGB EOTF. Values above 1 (HDR) continue with the 2.4 power curve like Unity."""
    if c <= 0.04045:
        return max(c, 0.0) / 12.92
    return ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c):
    """Inverse of srgb_to_linear (also above 1)."""
    if c <= 0.0031308:
        return max(c, 0.0) * 12.92
    return 1.055 * (c ** (1.0 / 2.4)) - 0.055


def color_to_linear(rgba):
    r, g, b = (srgb_to_linear(v) for v in rgba[:3])
    a = rgba[3] if len(rgba) > 3 else 1.0
    return (r, g, b, a)


# ---------------------------------------------------------------- GUID index

_guid_cache = {}   # normalised project root -> {guid: image path}


def find_project_root(path):
    """Walk up from a file to the folder that contains 'Assets'. Returns '' if not found."""
    current = os.path.dirname(os.path.abspath(path))
    while True:
        parent, tail = os.path.split(current)
        if tail.lower() == "assets" and parent:
            return parent
        if parent == current or not parent:
            return ""
        current = parent


def normalize_project_root(root):
    """Accept either the project folder or its Assets folder."""
    root = os.path.abspath(root)
    if os.path.basename(root).lower() == "assets":
        root = os.path.dirname(root)
    return root


def read_meta_guid(meta_path):
    try:
        with open(meta_path, "r", encoding="utf-8", errors="replace") as f:
            for _ in range(4):
                m = _GUID_RE.match(f.readline())
                if m:
                    return m.group(1).lower()
    except OSError:
        pass
    return ""


def build_guid_index(project_root, force=False):
    """guid -> image path for every image under <root>/Assets and <root>/Packages (cached)."""
    root = normalize_project_root(project_root)
    key = os.path.normcase(root)
    if not force and key in _guid_cache:
        return _guid_cache[key]
    index = {}
    for sub in ("Assets", "Packages"):
        base = os.path.join(root, sub)
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            # folders Unity itself ignores
            dirnames[:] = [d for d in dirnames if not d.endswith("~") and not d.startswith(".")]
            for name in filenames:
                if not name.endswith(".meta"):
                    continue
                asset = name[:-5]
                if not asset.lower().endswith(IMAGE_EXTENSIONS):
                    continue
                guid = read_meta_guid(os.path.join(dirpath, name))
                if guid:
                    index[guid] = os.path.join(dirpath, asset)
    _guid_cache[key] = index
    return index


def clear_guid_cache():
    _guid_cache.clear()


def cached_roots():
    return {root: len(index) for root, index in _guid_cache.items()}
