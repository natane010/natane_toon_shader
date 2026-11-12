# SPDX-License-Identifier: MIT
"""Property groups, add-on preferences and handlers."""

import bpy
from bpy.app.handlers import persistent
from bpy.props import (
    BoolProperty, EnumProperty, FloatProperty, FloatVectorProperty, PointerProperty, StringProperty,
)

from . import node_group, outline

ALPHA_MODE_ITEMS = (
    ('OPAQUE', "不透明 (Opaque)", "Natane/Toon Shader"),
    ('CUTOUT', "カットアウト (Cutout)", "Natane/Toon Shader (Cutout): _Cutoff"),
    ('TRANSPARENT', "半透明 (Transparent)", "Natane/Toon Shader (Transparent)"),
)

_TOGGLES = {
    "use_specular": "Use Specular",
    "use_rim": "Use Rim",
    "use_matcap": "Use MatCap",
    "use_emission": "Use Emission",
}


def push_settings(mat):
    """Write the property group state into the node group inputs / material settings."""
    node = node_group.find_main_node(mat)
    if node is None:
        return
    settings = mat.natane_toon
    for prop, socket in _TOGGLES.items():
        node.inputs[socket].default_value = 1.0 if getattr(settings, prop) else 0.0
    node.inputs["Alpha Mode"].default_value = node_group.ALPHA_MODE_VALUES[settings.alpha_mode]
    node_group.apply_material_settings(mat, settings.alpha_mode, settings.cull_back)


def _update_settings(self, context):
    push_settings(self.id_data)


def _update_outline(self, context):
    scene = context.scene if context is not None else None
    outline.resync_material_users(self.id_data, scene)


class NataneToonMaterialSettings(bpy.types.PropertyGroup):
    use_specular: BoolProperty(
        name="スペキュラ (Specular)", description="Unity: _Specular / _SPECULAR", update=_update_settings)
    use_rim: BoolProperty(
        name="リムライト (Rim Light)", description="Unity: _RimLight / _RIM_LIGHT", update=_update_settings)
    use_matcap: BoolProperty(
        name="マットキャップ (MatCap)", description="Unity: _MatCap / _MATCAP", update=_update_settings)
    use_emission: BoolProperty(
        name="エミッション (Emission)", description="Unity: _Emission / _EMISSION", update=_update_settings)
    alpha_mode: EnumProperty(
        name="描画モード (Render Mode)", items=ALPHA_MODE_ITEMS, default='OPAQUE', update=_update_settings)
    cull_back: BoolProperty(
        name="裏面カリング (Cull Back)", description="Unity: _Cull = Back", default=True, update=_update_settings)
    use_outline: BoolProperty(
        name="アウトライン (Outline)", description="Unity: _Outline / _OUTLINE", update=_update_outline)
    outline_color: FloatVectorProperty(
        name="アウトライン色 (Outline Color)", description="Unity: _OutlineColor", subtype='COLOR',
        size=3, min=0.0, max=1.0, default=(0.0, 0.0, 0.0), update=_update_outline)
    outline_width: FloatProperty(
        name="アウトライン幅 (Outline Width)", description="Unity: _OutlineWidth (幅の換算は近似)",
        min=0.0, max=1.0, default=0.1, precision=4, step=0.1, update=_update_outline)
    outline_material: PointerProperty(type=bpy.types.Material, name="Outline Material")
    source_mat: StringProperty(name="Unity .mat", description="読み込んだ Unity マテリアルのパス")
    # Round trip data for the exporter (JSON): everything read from the .mat, and the
    # Blender-side values right after the import (to detect what the user changed).
    unity_data: StringProperty(options={'HIDDEN'})
    unity_baseline: StringProperty(options={'HIDDEN'})


class NataneToonPreferences(bpy.types.AddonPreferences):
    bl_idname = __package__

    unity_project_root: StringProperty(
        name="Unity プロジェクト (Unity Project)",
        description="Assets フォルダを含む Unity プロジェクトのフォルダ。空なら .mat の場所から自動で探す",
        subtype='DIR_PATH', default="")

    def draw(self, context):
        layout = self.layout
        layout.prop(self, "unity_project_root")
        layout.operator("natane_toon.rescan_guids", icon='FILE_REFRESH')


def get_project_root_pref(context):
    """Preference value, or '' when the add-on runs without a preferences entry."""
    try:
        addon = context.preferences.addons.get(__package__)
        return addon.preferences.unity_project_root if addon else ""
    except (AttributeError, KeyError):
        return ""


def _sun_poll(self, obj):
    return obj.type == 'LIGHT' and obj.data.type == 'SUN'


def _update_sun(self, context):
    node_group.sync_sun(self, force=True)


def _update_outline_scale(self, context):
    outline.resync_all(self)


_syncing = False


@persistent
def _sync_sun_handler(scene, *_args):
    """Rebind the light drivers when the bound Sun disappeared or a first Sun was added."""
    global _syncing
    if _syncing:
        return
    _syncing = True
    try:
        if not isinstance(scene, bpy.types.Scene):
            scene = bpy.context.scene
        if scene is not None:
            node_group.sync_sun(scene)
    except Exception as ex:  # never break the depsgraph because of the preview
        print("Natane Toon Preview: sun sync failed:", ex)
    finally:
        _syncing = False


@persistent
def _load_post_handler(*_args):
    _sync_sun_handler(bpy.context.scene)


classes = (NataneToonMaterialSettings, NataneToonPreferences)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Material.natane_toon = PointerProperty(type=NataneToonMaterialSettings)
    bpy.types.Scene.natane_toon_sun = PointerProperty(
        type=bpy.types.Object, name="ライト (Sun)", poll=_sun_poll, update=_update_sun,
        description="陰影の向きと色に使う Sun。空ならシーン内で名前順に最初の Sun")
    bpy.types.Scene.natane_toon_outline_scale = FloatProperty(
        name="アウトライン倍率 (Outline Scale)", update=_update_outline_scale,
        description="プレビュー用のアウトライン幅の倍率。Unity 側の太さはカメラ設定で変わるため近似",
        min=0.0, soft_max=20.0, default=1.0)
    bpy.app.handlers.depsgraph_update_post.append(_sync_sun_handler)
    bpy.app.handlers.load_post.append(_load_post_handler)


def unregister():
    for handler_list, handler in ((bpy.app.handlers.depsgraph_update_post, _sync_sun_handler),
                                  (bpy.app.handlers.load_post, _load_post_handler)):
        if handler in handler_list:
            handler_list.remove(handler)
    del bpy.types.Scene.natane_toon_outline_scale
    del bpy.types.Scene.natane_toon_sun
    del bpy.types.Material.natane_toon
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
