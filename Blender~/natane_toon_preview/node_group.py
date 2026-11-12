# SPDX-License-Identifier: MIT
"""Builds the "Natane Toon" shader node groups.

Lighting design: N.L is NOT taken from Blender's lighting. The main group reads a light
direction / colour from the shared group "Natane Toon Light", whose values are driven
(plain drivers, no Python expressions) by one Sun object. That keeps the toon ramp
identical to the Unity formulas; Shader to RGB is used only for the optional cast
shadow mask.
"""

import bpy

GROUP_NAME = "Natane Toon"
LIGHT_GROUP_NAME = "Natane Toon Light"
MATCAP_UV_GROUP_NAME = "Natane MatCap UV"
GROUP_VERSION = 1

# Fallback light when the scene has no Sun (direction points towards the light).
DEFAULT_LIGHT_DIRECTION = (0.35, -0.5, 0.79)

# (socket name, socket type, default, min, max, subtype, Unity property)
INPUTS = (
    ("Color", "NodeSocketColor", (1.0, 1.0, 1.0, 1.0), None, None, None, "_Color"),
    ("Color Alpha", "NodeSocketFloat", 1.0, 0.0, 1.0, "FACTOR", "_Color.a"),
    ("Main Tex", "NodeSocketColor", (1.0, 1.0, 1.0, 1.0), None, None, None, "_MainTex"),
    ("Main Tex Alpha", "NodeSocketFloat", 1.0, 0.0, 1.0, "FACTOR", "_MainTex.a"),
    ("Shadow Color", "NodeSocketColor", (0.5, 0.5, 0.5, 1.0), None, None, None, "_ShadowColor"),
    ("Shadow Offset", "NodeSocketFloat", 0.0, -1.0, 1.0, None, "_ShadowOffset"),
    ("Shadow Sharpness", "NodeSocketFloat", 0.1, 0.001, 1.0, "FACTOR", "_ShadowSharpness"),
    ("Wrap Amount", "NodeSocketFloat", 1.0, 0.0, 1.0, "FACTOR", "_WrapAmount"),
    ("Receive Shadows", "NodeSocketFloat", 0.0, 0.0, 1.0, "FACTOR", ""),
    ("Use Specular", "NodeSocketFloat", 0.0, 0.0, 1.0, "FACTOR", "_Specular"),
    ("Specular Color", "NodeSocketColor", (1.0, 1.0, 1.0, 1.0), None, None, None, "_SpecularColor"),
    ("Specular Intensity", "NodeSocketFloat", 0.8, 0.0, 5.0, None, "_SpecularIntensity"),
    ("Specular Size", "NodeSocketFloat", 0.1, 0.0, 1.0, "FACTOR", "_SpecularSize"),
    ("Specular Softness", "NodeSocketFloat", 0.05, 0.001, 1.0, "FACTOR", "_SpecularSoftness"),
    ("Use Rim", "NodeSocketFloat", 0.0, 0.0, 1.0, "FACTOR", "_RimLight"),
    ("Rim Color", "NodeSocketColor", (1.0, 1.0, 1.0, 1.0), None, None, None, "_RimColor"),
    ("Rim Power", "NodeSocketFloat", 3.0, 0.1, 10.0, None, "_RimPower"),
    ("Rim Intensity", "NodeSocketFloat", 1.0, 0.0, 5.0, None, "_RimIntensity"),
    ("Use MatCap", "NodeSocketFloat", 0.0, 0.0, 1.0, "FACTOR", "_MatCap"),
    ("MatCap Tex", "NodeSocketColor", (0.0, 0.0, 0.0, 1.0), None, None, None, "_MatCapTex"),
    ("MatCap Intensity", "NodeSocketFloat", 1.0, 0.0, 2.0, None, "_MatCapIntensity"),
    ("MatCap Blend", "NodeSocketFloat", 1.0, 0.0, 1.0, "FACTOR", "_MatCapBlend"),
    ("MatCap Blend Mode", "NodeSocketFloat", 0.0, 0.0, 2.0, None, "_MatCapBlendMode"),
    ("Use Emission", "NodeSocketFloat", 0.0, 0.0, 1.0, "FACTOR", "_Emission"),
    ("Emission Color", "NodeSocketColor", (0.0, 0.0, 0.0, 1.0), None, None, None, "_EmissionColor"),
    ("Emission Map", "NodeSocketColor", (1.0, 1.0, 1.0, 1.0), None, None, None, "_EmissionMap"),
    ("Alpha Mode", "NodeSocketFloat", 0.0, 0.0, 2.0, None, ""),
    ("Cutoff", "NodeSocketFloat", 0.5, 0.0, 1.0, "FACTOR", "_Cutoff"),
    ("Normal", "NodeSocketVector", (0.0, 0.0, 0.0), None, None, None, "_BumpMap"),
)

ALPHA_MODE_VALUES = {"OPAQUE": 0.0, "CUTOUT": 1.0, "TRANSPARENT": 2.0}


class _Builder:
    """Small helper so the graph below reads like the shader formulas."""

    def __init__(self, tree):
        self.tree = tree
        self.nodes = tree.nodes
        self.links = tree.links
        self._count = 0

    def node(self, bl_idname, label=""):
        n = self.nodes.new(bl_idname)
        n.location = (220 * (self._count // 6), -170 * (self._count % 6))
        self._count += 1
        if label:
            n.label = label
        return n

    def _set(self, socket, value):
        if value is None:
            return
        if isinstance(value, bpy.types.NodeSocket):
            self.links.new(value, socket)
        elif isinstance(value, (int, float)):
            if socket.type == 'VALUE':
                socket.default_value = float(value)
            else:
                socket.default_value = (float(value),) * len(socket.default_value)
        else:
            socket.default_value = tuple(value)

    def math(self, op, a, b=None, c=None, clamp=False, label=""):
        n = self.node('ShaderNodeMath', label)
        n.operation = op
        n.use_clamp = clamp
        for i, v in enumerate((a, b, c)):
            self._set(n.inputs[i], v)
        return n.outputs[0]

    def vmath(self, op, a, b=None, scale=None, label=""):
        """Vector Math (also used for colour arithmetic). Returns the meaningful output."""
        n = self.node('ShaderNodeVectorMath', label)
        n.operation = op
        self._set(n.inputs[0], a)
        self._set(n.inputs[1], b)
        self._set(n.inputs[3], scale)
        return n.outputs[1] if op in {'DOT_PRODUCT', 'LENGTH', 'DISTANCE'} else n.outputs[0]

    def mix(self, fac, a, b, vector=True, label=""):
        """lerp(a, b, fac), factor clamped."""
        n = self.node('ShaderNodeMix', label)
        n.data_type = 'VECTOR' if vector else 'FLOAT'
        n.clamp_factor = True
        self._set(n.inputs[0], fac)
        self._set(n.inputs[4 if vector else 2], a)
        self._set(n.inputs[5 if vector else 3], b)
        return n.outputs[1 if vector else 0]

    def smoothstep(self, edge0, edge1, x, label=""):
        n = self.node('ShaderNodeMapRange', label)
        n.interpolation_type = 'SMOOTHSTEP'
        self._set(n.inputs[0], x)
        self._set(n.inputs[1], edge0)
        self._set(n.inputs[2], edge1)
        n.inputs[3].default_value = 0.0
        n.inputs[4].default_value = 1.0
        return n.outputs[0]


def _clear_tree(tree):
    tree.nodes.clear()
    tree.interface.clear()


def _new_socket(tree, name, in_out, socket_type):
    return tree.interface.new_socket(name=name, in_out=in_out, socket_type=socket_type)


# ---------------------------------------------------------------- light group

def ensure_light_group():
    tree = bpy.data.node_groups.get(LIGHT_GROUP_NAME)
    if tree is not None and tree.get("natane_version") == GROUP_VERSION:
        return tree
    if tree is None:
        tree = bpy.data.node_groups.new(LIGHT_GROUP_NAME, 'ShaderNodeTree')
    else:
        _clear_tree(tree)
    _new_socket(tree, "Direction", 'OUTPUT', 'NodeSocketVector')
    _new_socket(tree, "Color", 'OUTPUT', 'NodeSocketColor')
    b = _Builder(tree)
    direction = b.node('ShaderNodeCombineXYZ', "Light Direction (to light)")
    direction.name = "Light Direction"
    color = b.node('ShaderNodeCombineXYZ', "Light Color RGB")
    color.name = "Light Color"
    energy = b.node('ShaderNodeValue', "Light Energy")
    energy.name = "Light Energy"
    out = b.node('NodeGroupOutput')
    b.links.new(b.vmath('NORMALIZE', direction.outputs[0]), out.inputs["Direction"])
    b.links.new(b.vmath('SCALE', color.outputs[0], scale=energy.outputs[0]), out.inputs["Color"])
    tree["natane_version"] = GROUP_VERSION
    _set_light_defaults(tree)
    return tree


def _set_light_defaults(tree):
    for i in range(3):
        tree.nodes["Light Direction"].inputs[i].default_value = DEFAULT_LIGHT_DIRECTION[i]
        tree.nodes["Light Color"].inputs[i].default_value = 1.0
    tree.nodes["Light Energy"].outputs[0].default_value = 1.0


def _light_driver_paths():
    paths = []
    for i in range(3):
        # RNA matrices are column-major: matrix_world[2][i] is component i of the local Z axis.
        paths.append(('nodes["Light Direction"].inputs[%d].default_value' % i, 'OBJECT', 'matrix_world[2][%d]' % i))
        paths.append(('nodes["Light Color"].inputs[%d].default_value' % i, 'LIGHT', 'color[%d]' % i))
    paths.append(('nodes["Light Energy"].outputs[0].default_value', 'LIGHT', 'energy'))
    return paths


def bound_sun(tree=None):
    """The Sun object currently driving the light group, or None."""
    tree = tree or bpy.data.node_groups.get(LIGHT_GROUP_NAME)
    if tree is None or tree.animation_data is None:
        return None
    for fc in tree.animation_data.drivers:
        for var in fc.driver.variables:
            target = var.targets[0].id
            if isinstance(target, bpy.types.Object):
                return target
    return None


def bind_sun(sun):
    """Drive the light group from a Sun object (None: remove drivers, use the fallback light)."""
    tree = ensure_light_group()
    if tree.animation_data is not None:
        for fc in list(tree.animation_data.drivers):
            tree.animation_data.drivers.remove(fc)
    if sun is None or sun.type != 'LIGHT':
        _set_light_defaults(tree)
        return
    for prop_path, id_type, data_path in _light_driver_paths():
        fc = tree.driver_add(prop_path)
        drv = fc.driver
        drv.type = 'SUM'
        var = drv.variables.new()
        var.name = "v"
        var.type = 'SINGLE_PROP'
        target = var.targets[0]
        target.id_type = id_type
        target.id = sun if id_type == 'OBJECT' else sun.data
        target.data_path = data_path
    # Seed the static values too so the result is right before the first depsgraph evaluation.
    for i in range(3):
        tree.nodes["Light Direction"].inputs[i].default_value = sun.matrix_world[i][2]
        tree.nodes["Light Color"].inputs[i].default_value = sun.data.color[i]
    tree.nodes["Light Energy"].outputs[0].default_value = sun.data.energy


def find_sun(scene):
    """Sun chosen in the scene settings, else the first Sun (by name) in the scene."""
    chosen = getattr(scene, "natane_toon_sun", None)
    if chosen is not None and chosen.type == 'LIGHT' and chosen.name in scene.objects:
        return chosen
    suns = [o for o in scene.objects if o.type == 'LIGHT' and o.data.type == 'SUN']
    return min(suns, key=lambda o: o.name) if suns else None


def sync_sun(scene, force=False):
    """Rebind the light group when the wanted Sun changed. Returns the bound Sun."""
    tree = bpy.data.node_groups.get(LIGHT_GROUP_NAME)
    if tree is None:
        if not force:
            return None
        tree = ensure_light_group()
    current = bound_sun(tree)
    chosen = getattr(scene, "natane_toon_sun", None)
    if chosen is None and current is not None and current.name in scene.objects and not force:
        return current
    wanted = find_sun(scene)
    # drivers whose Sun was deleted keep a dangling target: clear them
    stale = current is None and tree.animation_data is not None and len(tree.animation_data.drivers) > 0
    if wanted != current or force or stale:
        bind_sun(wanted)
    return wanted


# ---------------------------------------------------------------- matcap uv group

def ensure_matcap_uv_group():
    tree = bpy.data.node_groups.get(MATCAP_UV_GROUP_NAME)
    if tree is not None and tree.get("natane_version") == GROUP_VERSION:
        return tree
    if tree is None:
        tree = bpy.data.node_groups.new(MATCAP_UV_GROUP_NAME, 'ShaderNodeTree')
    else:
        _clear_tree(tree)
    _new_socket(tree, "Normal", 'INPUT', 'NodeSocketVector')
    _new_socket(tree, "UV", 'OUTPUT', 'NodeSocketVector')
    b = _Builder(tree)
    gi = b.node('NodeGroupInput')
    geo = b.node('ShaderNodeNewGeometry')
    use_custom = b.math('GREATER_THAN', b.vmath('LENGTH', gi.outputs["Normal"]), 1e-4)
    normal = b.mix(use_custom, geo.outputs["Normal"], gi.outputs["Normal"])
    xform = b.node('ShaderNodeVectorTransform', "World -> View")
    xform.vector_type = 'NORMAL'
    xform.convert_from = 'WORLD'
    xform.convert_to = 'CAMERA'
    b.links.new(normal, xform.inputs[0])
    # uv = normal_view.xy * 0.5 + 0.5
    uv = b.vmath('MULTIPLY_ADD', b.vmath('NORMALIZE', xform.outputs[0]), (0.5, 0.5, 0.0))
    b._set(uv.node.inputs[2], (0.5, 0.5, 0.0))
    out = b.node('NodeGroupOutput')
    b.links.new(uv, out.inputs["UV"])
    tree["natane_version"] = GROUP_VERSION
    return tree


# ---------------------------------------------------------------- main group

def ensure_main_group():
    tree = bpy.data.node_groups.get(GROUP_NAME)
    if tree is not None and tree.get("natane_version") == GROUP_VERSION:
        return tree
    light_tree = ensure_light_group()
    if tree is None:
        tree = bpy.data.node_groups.new(GROUP_NAME, 'ShaderNodeTree')
    else:
        _clear_tree(tree)

    for name, socket_type, default, vmin, vmax, subtype, unity in INPUTS:
        sock = _new_socket(tree, name, 'INPUT', socket_type)
        sock.default_value = default
        if vmin is not None:
            sock.min_value = vmin
            sock.max_value = vmax
        if subtype:
            sock.subtype = subtype
        if unity:
            sock.description = "Unity: " + unity
        if name == "Normal":
            sock.hide_value = True
    _new_socket(tree, "Shader", 'OUTPUT', 'NodeSocketShader')
    _new_socket(tree, "Color", 'OUTPUT', 'NodeSocketColor')
    _new_socket(tree, "Alpha", 'OUTPUT', 'NodeSocketFloat')

    b = _Builder(tree)
    gi = b.node('NodeGroupInput')
    inp = gi.outputs
    geo = b.node('ShaderNodeNewGeometry')
    light = b.node('ShaderNodeGroup', "Light")
    light.node_tree = light_tree
    L = light.outputs["Direction"]
    light_color = light.outputs["Color"]
    V = geo.outputs["Incoming"]

    # N: custom normal when linked (non-zero), else the geometry normal
    use_custom = b.math('GREATER_THAN', b.vmath('LENGTH', inp["Normal"]), 1e-4)
    N = b.vmath('NORMALIZE', b.mix(use_custom, geo.outputs["Normal"], inp["Normal"]), label="N")

    # ---- toon diffuse
    ndl = b.vmath('DOT_PRODUCT', N, L, label="N.L")
    # wrap: (ndl + w) / (1 + w); w = 1 is half-Lambert
    wrapped = b.math('DIVIDE', b.math('ADD', ndl, inp["Wrap Amount"]), b.math('ADD', inp["Wrap Amount"], 1.0))
    x = b.math('ADD', wrapped, inp["Shadow Offset"], clamp=True, label="saturate(hl + offset)")
    r = b.math('MAXIMUM', b.math('MULTIPLY', inp["Shadow Sharpness"], 0.5), 0.0005, label="r")
    shade = b.smoothstep(b.math('SUBTRACT', 0.5, r), b.math('ADD', 0.5, r), x, label="shade")

    # ---- optional cast shadows: Diffuse BSDF facing the light -> Shader to RGB gives
    # light visibility only (no N.L term); thresholded against the sun's own radiance.
    diffuse = b.node('ShaderNodeBsdfDiffuse', "Shadow probe")
    diffuse.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    b.links.new(L, diffuse.inputs["Normal"])
    to_rgb = b.node('ShaderNodeShaderToRGB')
    b.links.new(diffuse.outputs[0], to_rgb.inputs[0])
    lit_lum = b.vmath('DOT_PRODUCT', to_rgb.outputs[0], (0.2126, 0.7152, 0.0722))
    sun_lum = b.math('MAXIMUM', b.vmath('DOT_PRODUCT', light_color, (0.2126, 0.7152, 0.0722)), 1e-4)
    visibility = b.smoothstep(0.45, 0.55, b.math('DIVIDE', lit_lum, sun_lum), label="shadow mask")
    shade = b.math('MULTIPLY', shade, b.mix(inp["Receive Shadows"], 1.0, visibility, vector=False))

    # albedo * lerp(shadowColor, 1, shade) * lightColor
    albedo = b.vmath('MULTIPLY', inp["Main Tex"], inp["Color"], label="albedo")
    shade_color = b.mix(shade, inp["Shadow Color"], (1.0, 1.0, 1.0))
    col = b.vmath('MULTIPLY', b.vmath('MULTIPLY', albedo, shade_color), light_color, label="diffuse")

    # ---- toon specular: smoothstep(1 - size - soft, 1 - size + soft, N.H)
    H = b.vmath('NORMALIZE', b.vmath('ADD', L, V), label="H")
    ndh = b.math('MAXIMUM', b.vmath('DOT_PRODUCT', N, H), 0.0, label="N.H")
    soft = b.math('MAXIMUM', inp["Specular Softness"], 0.001)
    edge = b.math('SUBTRACT', 1.0, inp["Specular Size"])
    spec = b.smoothstep(b.math('SUBTRACT', edge, soft), b.math('ADD', edge, soft), ndh, label="spec")
    spec = b.math('MULTIPLY', b.math('MULTIPLY', spec, inp["Specular Intensity"]), inp["Use Specular"])
    col = b.vmath('ADD', col, b.vmath('SCALE', inp["Specular Color"], scale=spec), label="+ specular")

    # ---- rim: pow(1 - N.V, power) * intensity * color
    ndv = b.math('MULTIPLY', b.vmath('DOT_PRODUCT', N, V), 1.0, clamp=True, label="saturate(N.V)")
    rim = b.math('POWER', b.math('SUBTRACT', 1.0, ndv), inp["Rim Power"])
    rim = b.math('MULTIPLY', b.math('MULTIPLY', rim, inp["Rim Intensity"]), inp["Use Rim"])
    col = b.vmath('ADD', col, b.vmath('SCALE', inp["Rim Color"], scale=rim), label="+ rim")

    # ---- matcap (0 add, 1 multiply, 2 replace), then lerp by _MatCapBlend
    matcap = b.vmath('SCALE', inp["MatCap Tex"], scale=inp["MatCap Intensity"])
    strength = b.math('MULTIPLY', inp["MatCap Intensity"], 1.0, clamp=True)
    mc_add = b.vmath('ADD', col, b.vmath('SCALE', matcap, scale=strength))
    mc_mul = b.mix(strength, col, b.vmath('MULTIPLY', col, matcap))
    mc_rep = b.mix(strength, col, matcap)
    is_replace = b.math('GREATER_THAN', inp["MatCap Blend Mode"], 1.5)
    is_multiply = b.math('SUBTRACT', b.math('GREATER_THAN', inp["MatCap Blend Mode"], 0.5), is_replace)
    mc = b.mix(is_replace, b.mix(is_multiply, mc_add, mc_mul), mc_rep)
    col = b.mix(b.math('MULTIPLY', inp["MatCap Blend"], inp["Use MatCap"]), col, mc, label="matcap")

    # ---- emission (unlit)
    emission = b.vmath('MULTIPLY', inp["Emission Map"], inp["Emission Color"])
    col = b.vmath('ADD', col, b.vmath('SCALE', emission, scale=inp["Use Emission"]), label="+ emission")

    # ---- alpha: 0 opaque, 1 cutout (clip(a - _Cutoff)), 2 blend
    alpha = b.math('MULTIPLY', inp["Main Tex Alpha"], inp["Color Alpha"], clamp=True, label="alpha")
    cut = b.math('SUBTRACT', 1.0, b.math('LESS_THAN', alpha, inp["Cutoff"]))
    is_blend = b.math('GREATER_THAN', inp["Alpha Mode"], 1.5)
    is_cutout = b.math('SUBTRACT', b.math('GREATER_THAN', inp["Alpha Mode"], 0.5), is_blend)
    out_alpha = b.mix(is_blend, b.mix(is_cutout, 1.0, cut, vector=False), alpha, vector=False, label="final alpha")

    surface = b.node('ShaderNodeEmission', "Unlit output")
    surface.inputs["Strength"].default_value = 1.0
    b.links.new(col, surface.inputs["Color"])
    transparent = b.node('ShaderNodeBsdfTransparent')
    mix_shader = b.node('ShaderNodeMixShader')
    b.links.new(out_alpha, mix_shader.inputs[0])
    b.links.new(transparent.outputs[0], mix_shader.inputs[1])
    b.links.new(surface.outputs[0], mix_shader.inputs[2])

    go = b.node('NodeGroupOutput')
    b.links.new(mix_shader.outputs[0], go.inputs["Shader"])
    b.links.new(col, go.inputs["Color"])
    b.links.new(out_alpha, go.inputs["Alpha"])
    tree["natane_version"] = GROUP_VERSION
    return tree


def validate_tree(tree):
    """Returns a list of problems (invalid links, hidden/disabled sockets in use)."""
    problems = []
    for link in tree.links:
        if not link.is_valid:
            problems.append("invalid link: %s.%s -> %s.%s" % (
                link.from_node.name, link.from_socket.name, link.to_node.name, link.to_socket.name))
        for sock in (link.from_socket, link.to_socket):
            if not sock.enabled:
                problems.append("link uses disabled socket: %s.%s" % (sock.node.name, sock.name))
    for node in tree.nodes:
        if node.bl_idname == 'ShaderNodeGroup' and node.node_tree is None:
            problems.append("group node without tree: " + node.name)
        if node.bl_idname == 'NodeUndefined':
            problems.append("undefined node: " + node.name)
    return problems


# ---------------------------------------------------------------- material helpers

MAIN_NODE_NAME = "Natane Toon"
TEX_NODE_NAMES = {
    "_MainTex": "Natane _MainTex",
    "_MatCapTex": "Natane _MatCapTex",
    "_EmissionMap": "Natane _EmissionMap",
    "_BumpMap": "Natane _BumpMap",
}


def find_main_node(mat):
    """The Natane Toon group node of a material, or None."""
    if mat is None or mat.node_tree is None:
        return None
    node = mat.node_tree.nodes.get(MAIN_NODE_NAME)
    if node is not None and node.bl_idname == 'ShaderNodeGroup' and node.node_tree is not None \
            and node.node_tree.name.startswith(GROUP_NAME):
        return node
    for node in mat.node_tree.nodes:
        if node.bl_idname == 'ShaderNodeGroup' and node.node_tree is not None \
                and node.node_tree.get("natane_version") is not None \
                and node.node_tree.name.startswith(GROUP_NAME) \
                and not node.node_tree.name.startswith(LIGHT_GROUP_NAME):
            return node
    return None


def apply_material_settings(mat, alpha_mode, cull_back=True):
    """Blend / culling settings matching the alpha mode (EEVEE Next and 5.0 names)."""
    blended = alpha_mode == 'TRANSPARENT'
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = 'BLENDED' if blended else 'DITHERED'
    elif hasattr(mat, "blend_method"):
        mat.blend_method = {'OPAQUE': 'OPAQUE', 'CUTOUT': 'CLIP', 'TRANSPARENT': 'BLEND'}[alpha_mode]
    if hasattr(mat, "use_transparency_overlap"):
        mat.use_transparency_overlap = True
    mat.use_backface_culling = cull_back
    if hasattr(mat, "use_backface_culling_shadow"):
        mat.use_backface_culling_shadow = False


def _find_source_image(mat):
    """Image and colour of an existing Principled setup, so conversion keeps the look."""
    image, color = None, None
    if mat.node_tree is None:
        return image, color
    for node in mat.node_tree.nodes:
        if node.bl_idname == 'ShaderNodeBsdfPrincipled':
            base = node.inputs.get("Base Color")
            if base is None:
                continue
            if base.is_linked:
                src = base.links[0].from_node
                if src.bl_idname == 'ShaderNodeTexImage':
                    image = src.image
            else:
                color = tuple(base.default_value)
            break
    return image, color


def setup_material(mat, keep_base=True):
    """Replace the material's node tree with the Natane Toon setup. Returns the group node."""
    if bpy.app.version < (5, 0, 0):
        mat.use_nodes = True
    image, color = _find_source_image(mat) if keep_base else (None, None)
    tree = mat.node_tree
    tree.nodes.clear()
    group = tree.nodes.new('ShaderNodeGroup')
    group.name = MAIN_NODE_NAME
    group.label = MAIN_NODE_NAME
    group.node_tree = ensure_main_group()
    group.location = (0, 0)
    group.width = 240
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    out.location = (320, 0)
    tree.links.new(group.outputs["Shader"], out.inputs["Surface"])
    if color is not None:
        group.inputs["Color"].default_value = color
    if image is not None:
        set_texture(mat, "_MainTex", image)
    apply_material_settings(mat, 'OPAQUE')
    return group


def set_texture(mat, unity_prop, image, scale=(1.0, 1.0), offset=(0.0, 0.0), bump_scale=1.0):
    """Create / update / remove (image=None) the texture node feeding one group input."""
    group = find_main_node(mat)
    if group is None:
        return None
    tree = mat.node_tree
    name = TEX_NODE_NAMES[unity_prop]
    # remove the previous chain
    for suffix in ("", " Mapping", " Coord", " UV", " NormalMap"):
        old = tree.nodes.get(name + suffix)
        if old is not None:
            tree.nodes.remove(old)
    if image is None:
        return None

    row = list(TEX_NODE_NAMES).index(unity_prop)
    tex = tree.nodes.new('ShaderNodeTexImage')
    tex.name = name
    tex.label = unity_prop
    tex.image = image
    tex.location = (-380, 200 - 300 * row)

    if unity_prop == "_MatCapTex":
        tex.extension = 'EXTEND'
        uv = tree.nodes.new('ShaderNodeGroup')
        uv.name = name + " UV"
        uv.node_tree = ensure_matcap_uv_group()
        uv.location = (-600, 200 - 300 * row)
        tree.links.new(uv.outputs["UV"], tex.inputs["Vector"])
        bump = tree.nodes.get(TEX_NODE_NAMES["_BumpMap"] + " NormalMap")
        if bump is not None:
            tree.links.new(bump.outputs[0], uv.inputs["Normal"])
        tree.links.new(tex.outputs["Color"], group.inputs["MatCap Tex"])
        return tex

    if tuple(scale) != (1.0, 1.0) or tuple(offset) != (0.0, 0.0):
        coord = tree.nodes.new('ShaderNodeTexCoord')
        coord.name = name + " Coord"
        coord.location = (-820, 200 - 300 * row)
        mapping = tree.nodes.new('ShaderNodeMapping')
        mapping.name = name + " Mapping"
        mapping.location = (-600, 200 - 300 * row)
        mapping.inputs["Scale"].default_value = (scale[0], scale[1], 1.0)
        mapping.inputs["Location"].default_value = (offset[0], offset[1], 0.0)
        tree.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        tree.links.new(mapping.outputs[0], tex.inputs["Vector"])

    if unity_prop == "_MainTex":
        tree.links.new(tex.outputs["Color"], group.inputs["Main Tex"])
        tree.links.new(tex.outputs["Alpha"], group.inputs["Main Tex Alpha"])
    elif unity_prop == "_EmissionMap":
        tree.links.new(tex.outputs["Color"], group.inputs["Emission Map"])
    elif unity_prop == "_BumpMap":
        nmap = tree.nodes.new('ShaderNodeNormalMap')
        nmap.name = name + " NormalMap"
        nmap.location = (-180, 200 - 300 * row)
        nmap.inputs["Strength"].default_value = bump_scale
        tree.links.new(tex.outputs["Color"], nmap.inputs["Color"])
        tree.links.new(nmap.outputs[0], group.inputs["Normal"])
        uv = tree.nodes.get(TEX_NODE_NAMES["_MatCapTex"] + " UV")
        if uv is not None:
            tree.links.new(nmap.outputs[0], uv.inputs["Normal"])
    return tex
