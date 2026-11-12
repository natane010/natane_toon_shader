# SPDX-License-Identifier: MIT
"""Inverted hull outline: Solidify modifier + per-material outline materials."""

import bpy

from . import node_group

# Unity: view-space offset = _OutlineWidth * 0.1 * depthScale. depthScale is the camera
# near plane in Legacy mode (0.01 - 0.3 depending on the camera) and 0.05 in Stable mode
# (NATANE_OUTLINE_REF_NEAR). The preview uses the Stable value; this is APPROXIMATE and
# ignores the shader's distance compensation. Tune with the scene "outline scale" slider.
OUTLINE_WIDTH_FACTOR = 0.1
OUTLINE_REF_NEAR = 0.05
OUTLINE_UNITY_TO_METERS = OUTLINE_WIDTH_FACTOR * OUTLINE_REF_NEAR

MODIFIER_NAME = "NataneToonOutline"
OUTLINE_SUFFIX = ".NataneOutline"
NONE_MATERIAL_NAME = "NataneOutline.None"
OUTLINE_FLAG = "natane_outline"


def is_outline_material(mat):
    return mat is not None and mat.get(OUTLINE_FLAG) is not None


def _prepare_outline_material(mat):
    if bpy.app.version < (5, 0, 0):
        mat.use_nodes = True
    mat[OUTLINE_FLAG] = 1
    mat.use_backface_culling = True
    if hasattr(mat, "use_backface_culling_shadow"):
        mat.use_backface_culling_shadow = True
    if hasattr(mat, "shadow_method"):
        mat.shadow_method = 'NONE'
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    out.location = (300, 0)
    return tree, out


def _none_material():
    """Invisible material for slots that have no outline."""
    mat = bpy.data.materials.get(NONE_MATERIAL_NAME)
    if mat is None:
        mat = bpy.data.materials.new(NONE_MATERIAL_NAME)
        tree, out = _prepare_outline_material(mat)
        transparent = tree.nodes.new('ShaderNodeBsdfTransparent')
        tree.links.new(transparent.outputs[0], out.inputs["Surface"])
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = 'DITHERED'
        elif hasattr(mat, "blend_method"):
            mat.blend_method = 'CLIP'
        if hasattr(mat, "use_transparent_shadow"):
            mat.use_transparent_shadow = True
    return mat


def ensure_outline_material(mat):
    """Outline material (emission of the outline colour) paired with a Natane material."""
    settings = mat.natane_toon
    outline = settings.outline_material
    if outline is None:
        outline = bpy.data.materials.new(mat.name + OUTLINE_SUFFIX)
        settings.outline_material = outline
    node = outline.node_tree.nodes.get("Outline Color") if outline.node_tree else None
    if node is None:
        tree, out = _prepare_outline_material(outline)
        node = tree.nodes.new('ShaderNodeEmission')
        node.name = "Outline Color"
        node.inputs["Strength"].default_value = 1.0
        tree.links.new(node.outputs[0], out.inputs["Surface"])
    update_outline_color(mat)
    return outline


def update_outline_color(mat):
    outline = mat.natane_toon.outline_material
    if outline is None or outline.node_tree is None:
        return
    node = outline.node_tree.nodes.get("Outline Color")
    if node is not None:
        c = mat.natane_toon.outline_color
        node.inputs["Color"].default_value = (c[0], c[1], c[2], 1.0)
        outline.diffuse_color = (c[0], c[1], c[2], 1.0)


def outline_thickness(width, scene):
    """Unity _OutlineWidth -> Blender metres (approximate)."""
    scale = getattr(scene, "natane_toon_outline_scale", 1.0) if scene is not None else 1.0
    return width * OUTLINE_UNITY_TO_METERS * scale


def _strip_outline_slots(obj):
    materials = obj.data.materials
    while len(materials) > 0 and is_outline_material(materials[len(materials) - 1]):
        materials.pop(index=len(materials) - 1)


def remove_outline(obj):
    if obj is None or obj.type != 'MESH':
        return
    mod = obj.modifiers.get(MODIFIER_NAME)
    if mod is not None:
        obj.modifiers.remove(mod)
    _strip_outline_slots(obj)


def sync_outline(obj, scene):
    """(Re)build the outline of one mesh object from its materials. Returns True if it has one."""
    if obj is None or obj.type != 'MESH':
        return False
    _strip_outline_slots(obj)
    base = list(obj.data.materials)
    widths = []
    outline_slots = []
    for mat in base:
        if mat is not None and node_group.find_main_node(mat) is not None \
                and mat.natane_toon.use_outline and mat.natane_toon.outline_width > 0.0:
            outline_slots.append(ensure_outline_material(mat))
            widths.append(mat.natane_toon.outline_width)
        else:
            outline_slots.append(None)
    mod = obj.modifiers.get(MODIFIER_NAME)
    if not widths:
        if mod is not None:
            obj.modifiers.remove(mod)
        return False

    for outline in outline_slots:
        obj.data.materials.append(outline if outline is not None else _none_material())
    if mod is None:
        mod = obj.modifiers.new(MODIFIER_NAME, 'SOLIDIFY')
    # negative thickness + offset -1: the shell grows outwards along the normals
    # One modifier per object: materials with different widths share the largest one.
    mod.thickness = -outline_thickness(max(widths), scene)
    mod.offset = -1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.use_even_offset = True
    mod.use_quality_normals = True
    mod.material_offset = len(base)
    mod.show_in_editmode = False
    return True


def resync_material_users(mat, scene):
    """Rebuild the outline of every mesh object that uses this material."""
    update_outline_color(mat)
    for obj in bpy.data.objects:
        if obj.type == 'MESH' and any(slot.material == mat for slot in obj.material_slots):
            sync_outline(obj, scene)


def resync_all(scene):
    for obj in scene.objects:
        if obj.type == 'MESH' and obj.modifiers.get(MODIFIER_NAME) is not None:
            sync_outline(obj, scene)
