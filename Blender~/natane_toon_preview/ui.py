# SPDX-License-Identifier: MIT
"""Properties > Material panel."""

import bpy

from . import node_group, outline

# (group input, label)
_SECTION_BASE = (
    ("Color", "色 (_Color)"),
    ("Color Alpha", "不透明度 (_Color.a)"),
)
_SECTION_SHADOW = (
    ("Shadow Color", "影色 (_ShadowColor)"),
    ("Shadow Offset", "影の位置 (_ShadowOffset)"),
    ("Shadow Sharpness", "境界のぼかし (_ShadowSharpness)"),
    ("Wrap Amount", "回り込み (_WrapAmount)"),
    ("Receive Shadows", "落ち影を受ける (Blender のみ)"),
)
_SECTION_SPECULAR = (
    ("Specular Color", "色 (_SpecularColor)"),
    ("Specular Intensity", "強さ (_SpecularIntensity)"),
    ("Specular Size", "大きさ (_SpecularSize)"),
    ("Specular Softness", "ぼかし (_SpecularSoftness)"),
)
_SECTION_RIM = (
    ("Rim Color", "色 (_RimColor)"),
    ("Rim Power", "鋭さ (_RimPower)"),
    ("Rim Intensity", "強さ (_RimIntensity)"),
)
_SECTION_MATCAP = (
    ("MatCap Intensity", "強さ (_MatCapIntensity)"),
    ("MatCap Blend", "ブレンド (_MatCapBlend)"),
    ("MatCap Blend Mode", "モード 0加算 1乗算 2置換 (_MatCapBlendMode)"),
)
_SECTION_EMISSION = (
    ("Emission Color", "色 (_EmissionColor)"),
)


def _draw_inputs(layout, node, section):
    for name, label in section:
        sock = node.inputs.get(name)
        if sock is None:
            continue
        if sock.is_linked:
            layout.label(text=label + ": ノード接続中", icon='LINKED')
        else:
            layout.prop(sock, "default_value", text=label)


def _draw_texture(layout, mat, unity_prop, label):
    tex = mat.node_tree.nodes.get(node_group.TEX_NODE_NAMES[unity_prop])
    row = layout.row(align=True)
    if tex is not None and tex.bl_idname == 'ShaderNodeTexImage':
        row.prop(tex, "image", text=label)
        row.operator("natane_toon.clear_texture", text="", icon='X').unity_prop = unity_prop
    else:
        row.label(text=label)
        row.operator("natane_toon.set_texture", text="画像を開く", icon='FILE_FOLDER').unity_prop = unity_prop


def _section(layout, settings, toggle, title=""):
    box = layout.box()
    if toggle:
        box.prop(settings, toggle)
        col = box.column()
        col.active = getattr(settings, toggle)
    else:
        box.label(text=title)
        col = box.column()
    col.use_property_split = True
    col.use_property_decorate = False
    return col


class NATANE_PT_material(bpy.types.Panel):
    bl_idname = "NATANE_PT_material"
    bl_label = "Natane Toon"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "material"

    @classmethod
    def poll(cls, context):
        return context.object is not None

    def draw(self, context):
        layout = self.layout
        mat = context.material if hasattr(context, "material") else None
        if mat is None and context.object is not None:
            mat = context.object.active_material
        scene = context.scene

        if outline.is_outline_material(mat):
            layout.label(text="アウトライン用マテリアルです (自動生成)", icon='INFO')
            return

        node = node_group.find_main_node(mat)
        row = layout.row(align=True)
        row.operator("natane_toon.convert_material", icon='SHADING_RENDERED',
                     text="Natane Toon に変換 (Convert)" if node is None else "作り直す (Rebuild)")
        layout.operator("natane_toon.import_mat", icon='IMPORT')
        layout.operator("natane_toon.batch_import", icon='FILE_FOLDER')
        layout.operator("natane_toon.export_unity", icon='EXPORT')
        if node is None:
            return
        settings = mat.natane_toon
        if settings.source_mat:
            layout.label(text=bpy.path.basename(settings.source_mat), icon='FILE')

        col = _section(layout, settings, "", "ベース (Base)")
        col.prop(settings, "alpha_mode")
        if settings.alpha_mode == 'CUTOUT':
            _draw_inputs(col, node, (("Cutoff", "カットオフ (_Cutoff)"),))
        col.prop(settings, "cull_back")
        _draw_inputs(col, node, _SECTION_BASE)
        _draw_texture(col, mat, "_MainTex", "テクスチャ (_MainTex)")
        _draw_texture(col, mat, "_BumpMap", "ノーマルマップ (_BumpMap)")

        col = _section(layout, settings, "", "影 (Shadow)")
        _draw_inputs(col, node, _SECTION_SHADOW)

        col = _section(layout, settings, "use_specular")
        _draw_inputs(col, node, _SECTION_SPECULAR)

        col = _section(layout, settings, "use_rim")
        _draw_inputs(col, node, _SECTION_RIM)

        col = _section(layout, settings, "use_matcap")
        _draw_texture(col, mat, "_MatCapTex", "テクスチャ (_MatCapTex)")
        _draw_inputs(col, node, _SECTION_MATCAP)

        col = _section(layout, settings, "use_emission")
        _draw_inputs(col, node, _SECTION_EMISSION)
        _draw_texture(col, mat, "_EmissionMap", "テクスチャ (_EmissionMap)")

        col = _section(layout, settings, "use_outline")
        col.prop(settings, "outline_color", text="色 (_OutlineColor)")
        col.prop(settings, "outline_width", text="幅 (_OutlineWidth)")
        col.prop(scene, "natane_toon_outline_scale", text="プレビュー倍率")
        obj = context.object
        has_outline = obj is not None and obj.type == 'MESH' and obj.modifiers.get(outline.MODIFIER_NAME) is not None
        row = col.row(align=True)
        row.operator("natane_toon.sync_outline", icon='MOD_SOLIDIFY',
                     text="更新 (Sync)" if has_outline else "作成 (Create)")
        row.operator("natane_toon.remove_outline", icon='X', text="削除 (Remove)")

        col = _section(layout, settings, "", "シーン (Scene)")
        col.prop(scene, "natane_toon_sun")
        sun = node_group.bound_sun()
        col.label(text="使用中: " + (sun.name if sun is not None else "なし (既定の向き)"),
                  icon='LIGHT_SUN' if sun is not None else 'ERROR')
        if scene.view_settings.view_transform != 'Standard':
            col.label(text="ビュー変換が Standard ではありません", icon='ERROR')
        col.operator("natane_toon.setup_scene", icon='SCENE_DATA')


classes = (NATANE_PT_material,)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
