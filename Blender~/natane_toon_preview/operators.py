# SPDX-License-Identifier: MIT
"""Operators."""

import os

import bpy
from bpy.props import BoolProperty, EnumProperty, StringProperty
from bpy_extras.io_utils import ExportHelper, ImportHelper

from . import exporter, importer, node_group, outline, props, unity_mat

ALPHA_OVERRIDE_ITEMS = (('AUTO', "自動 (Auto)", "シェーダー GUID・キーワード・レンダーキューから判定"),) \
    + props.ALPHA_MODE_ITEMS

_TEXTURE_ITEMS = (
    ('_MainTex', "Main Tex", ""),
    ('_MatCapTex', "MatCap Tex", ""),
    ('_EmissionMap', "Emission Map", ""),
    ('_BumpMap', "Normal Map", ""),
)


def _mesh_objects(context):
    objs = [o for o in context.selected_objects if o.type == 'MESH']
    active = context.active_object
    if not objs and active is not None and active.type == 'MESH':
        objs = [active]
    return objs


def _active_material(context, create_name=""):
    """Active material of the active object; optionally create and assign one."""
    obj = context.active_object
    mat = obj.active_material if obj is not None else None
    if mat is None and create_name:
        mat = bpy.data.materials.new(create_name)
        if obj is not None and hasattr(obj.data, "materials"):
            if obj.material_slots and obj.active_material_index < len(obj.material_slots):
                obj.material_slots[obj.active_material_index].material = mat
            else:
                obj.data.materials.append(mat)
    return mat


class NATANE_OT_convert_material(bpy.types.Operator):
    bl_idname = "natane_toon.convert_material"
    bl_label = "Natane Toon に変換 (Convert)"
    bl_description = "アクティブなマテリアルを Natane Toon ノードグループに置き換える。なければ新規作成"
    bl_options = {'REGISTER', 'UNDO'}

    keep_base: BoolProperty(
        name="ベースカラーを引き継ぐ (Keep Base Color)", default=True,
        description="Principled BSDF のベースカラー / テクスチャを _Color / _MainTex に引き継ぐ")

    def execute(self, context):
        mat = _active_material(context, create_name="NataneToon")
        if mat is None:
            self.report({'ERROR'}, "マテリアルを作成できません")
            return {'CANCELLED'}
        node_group.setup_material(mat, keep_base=self.keep_base)
        props.push_settings(mat)
        node_group.sync_sun(context.scene, force=True)
        if node_group.bound_sun() is None:
            self.report({'WARNING'}, "シーンに Sun がありません。既定の向きのライトで表示します")
        return {'FINISHED'}


class NATANE_OT_import_mat(bpy.types.Operator, ImportHelper):
    bl_idname = "natane_toon.import_mat"
    bl_label = "Unity の .mat を読み込む (Import Unity .mat)"
    bl_description = "Unity のマテリアル (.mat, テキスト形式) の値をアクティブなマテリアルに適用する"
    bl_options = {'REGISTER', 'UNDO'}

    filename_ext = ".mat"
    filter_glob: StringProperty(default="*.mat", options={'HIDDEN'})
    project_root: StringProperty(
        name="Unity プロジェクト (Project)", subtype='DIR_PATH', default="",
        description="テクスチャ GUID の解決に使う Unity プロジェクト。空ならプリファレンス、それも空なら .mat から上へ探す")
    load_textures: BoolProperty(name="テクスチャを読み込む (Load Textures)", default=True)
    alpha_override: EnumProperty(name="描画モード (Render Mode)", items=ALPHA_OVERRIDE_ITEMS, default='AUTO')
    sync_outline: BoolProperty(
        name="アウトラインを反映 (Sync Outline)", default=True,
        description="選択中のメッシュのアウトライン (Solidify) を作り直す")

    def execute(self, context):
        if not os.path.isfile(self.filepath):
            self.report({'ERROR'}, "ファイルがありません: " + self.filepath)
            return {'CANCELLED'}
        name = os.path.splitext(os.path.basename(self.filepath))[0]
        mat = _active_material(context, create_name=name)
        try:
            report = importer.import_mat_file(context, mat, self.filepath, self.project_root,
                                              self.load_textures, self.alpha_override)
        except (ValueError, OSError) as ex:
            self.report({'ERROR'}, ".mat を読み込めません: %s" % ex)
            return {'CANCELLED'}
        node_group.sync_sun(context.scene, force=True)
        if self.sync_outline:
            outline.resync_material_users(mat, context.scene)
        for message in report["warnings"]:
            self.report({'WARNING'}, message)
        self.report({'INFO'}, "%s を読み込みました (%s)" % (name, report["alpha_mode"]))
        return {'FINISHED'}


class NATANE_OT_batch_import(bpy.types.Operator):
    bl_idname = "natane_toon.batch_import"
    bl_label = "フォルダから一括読み込み (Batch Import)"
    bl_description = "選択オブジェクトの各マテリアルに、同名の <マテリアル名>.mat をフォルダから探して適用する"
    bl_options = {'REGISTER', 'UNDO'}

    directory: StringProperty(name="フォルダ (Folder)", subtype='DIR_PATH')
    filter_folder: BoolProperty(default=True, options={'HIDDEN'})
    recursive: BoolProperty(name="サブフォルダも探す (Recursive)", default=True)
    project_root: StringProperty(name="Unity プロジェクト (Project)", subtype='DIR_PATH', default="")
    load_textures: BoolProperty(name="テクスチャを読み込む (Load Textures)", default=True)
    sync_outline: BoolProperty(name="アウトラインを反映 (Sync Outline)", default=True)

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

    def execute(self, context):
        directory = bpy.path.abspath(self.directory)
        if not os.path.isdir(directory):
            self.report({'ERROR'}, "フォルダがありません: " + directory)
            return {'CANCELLED'}
        objs = _mesh_objects(context)
        files = importer.find_mat_files(directory, self.recursive)
        done, missing, failed = [], [], []
        seen = set()
        for obj in objs:
            for slot in obj.material_slots:
                mat = slot.material
                if mat is None or mat.name in seen or outline.is_outline_material(mat):
                    continue
                seen.add(mat.name)
                path = importer.match_mat_file(mat.name, files)
                if path is None:
                    missing.append(mat.name)
                    continue
                try:
                    report = importer.import_mat_file(context, mat, path, self.project_root, self.load_textures)
                except (ValueError, OSError) as ex:
                    failed.append("%s (%s)" % (mat.name, ex))
                    continue
                done.append(mat.name)
                for message in report["warnings"]:
                    print("Natane Toon Preview: %s: %s" % (mat.name, message))
        node_group.sync_sun(context.scene, force=True)
        if self.sync_outline:
            for obj in objs:
                outline.sync_outline(obj, context.scene)
        if missing:
            self.report({'WARNING'}, ".mat が見つからないマテリアル: " + ", ".join(missing))
        if failed:
            self.report({'WARNING'}, "読み込み失敗: " + ", ".join(failed))
        self.report({'INFO'}, "%d 個のマテリアルに適用しました" % len(done))
        return {'FINISHED'} if done else {'CANCELLED'}


class NATANE_OT_sync_outline(bpy.types.Operator):
    bl_idname = "natane_toon.sync_outline"
    bl_label = "アウトラインを作成 / 更新 (Sync Outline)"
    bl_description = "選択メッシュに背面法のアウトライン (Solidify モディファイアーとアウトライン用マテリアル) を作成・更新する"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        count = sum(1 for obj in _mesh_objects(context) if outline.sync_outline(obj, context.scene))
        self.report({'INFO'}, "%d 個のオブジェクトにアウトラインを設定しました" % count)
        return {'FINISHED'}


class NATANE_OT_remove_outline(bpy.types.Operator):
    bl_idname = "natane_toon.remove_outline"
    bl_label = "アウトラインを削除 (Remove Outline)"
    bl_description = "選択メッシュからアウトライン用のモディファイアーとマテリアルスロットを取り除く"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        for obj in _mesh_objects(context):
            outline.remove_outline(obj)
        return {'FINISHED'}


class NATANE_OT_set_texture(bpy.types.Operator, ImportHelper):
    bl_idname = "natane_toon.set_texture"
    bl_label = "テクスチャを設定 (Set Texture)"
    bl_description = "画像を読み込んでテクスチャノードを接続する"
    bl_options = {'REGISTER', 'UNDO'}

    filter_glob: StringProperty(default="*.png;*.jpg;*.jpeg;*.tga;*.bmp;*.tif;*.tiff;*.exr;*.hdr;*.psd;*.webp",
                                options={'HIDDEN'})
    filename_ext = ""
    unity_prop: EnumProperty(items=_TEXTURE_ITEMS, options={'HIDDEN'})

    def execute(self, context):
        mat = _active_material(context)
        if node_group.find_main_node(mat) is None:
            return {'CANCELLED'}
        try:
            image = bpy.data.images.load(self.filepath, check_existing=True)
        except RuntimeError as ex:
            self.report({'ERROR'}, str(ex))
            return {'CANCELLED'}
        if self.unity_prop == '_BumpMap':
            image.colorspace_settings.name = 'Non-Color'
        node_group.set_texture(mat, self.unity_prop, image)
        return {'FINISHED'}


class NATANE_OT_clear_texture(bpy.types.Operator):
    bl_idname = "natane_toon.clear_texture"
    bl_label = "テクスチャを外す (Clear Texture)"
    bl_description = "テクスチャノードを取り除く"
    bl_options = {'REGISTER', 'UNDO'}

    unity_prop: EnumProperty(items=_TEXTURE_ITEMS, options={'HIDDEN'})

    def execute(self, context):
        mat = _active_material(context)
        if node_group.find_main_node(mat) is None:
            return {'CANCELLED'}
        node_group.set_texture(mat, self.unity_prop, None)
        return {'FINISHED'}


class NATANE_OT_setup_scene(bpy.types.Operator):
    bl_idname = "natane_toon.setup_scene"
    bl_label = "シーンを Unity 寄りに設定 (Setup Scene)"
    bl_description = ("レンダーエンジンを EEVEE、ビュー変換を Standard にし、Sun がなければ作成する。"
                      "AgX / Filmic のままだと Unity と色が合わない")
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        engines = {item.identifier for item in
                   bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items}
        # 4.2 - 4.5: BLENDER_EEVEE_NEXT, 5.0+: BLENDER_EEVEE
        for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
            if engine in engines:
                scene.render.engine = engine
                break
        scene.view_settings.view_transform = 'Standard'
        scene.view_settings.look = 'None'
        if node_group.find_sun(scene) is None:
            data = bpy.data.lights.new("NataneToonSun", 'SUN')
            data.energy = 1.0
            sun = bpy.data.objects.new("NataneToonSun", data)
            sun.rotation_euler = (0.87, 0.0, 0.52)
            scene.collection.objects.link(sun)
        node_group.sync_sun(scene, force=True)
        return {'FINISHED'}


class NATANE_OT_rescan_guids(bpy.types.Operator):
    bl_idname = "natane_toon.rescan_guids"
    bl_label = "GUID キャッシュを破棄 (Rescan Textures)"
    bl_description = "Unity プロジェクトの .meta から作った GUID とテクスチャの対応表を破棄し、次回の読み込みで作り直す"

    def execute(self, context):
        unity_mat.clear_guid_cache()
        self.report({'INFO'}, "GUID キャッシュを破棄しました")
        return {'FINISHED'}


class NATANE_OT_export_unity(bpy.types.Operator, ExportHelper):
    bl_idname = "natane_toon.export_unity"
    bl_label = "Unity 向けに書き出す (Export for Unity)"
    bl_description = ("FBX と Natane Toon のパラメーター (<名前>.natanetoon.json) を並べて書き出す。"
                      "アウトライン用のモディファイアーとマテリアルは FBX に含めない")
    bl_options = {'REGISTER'}

    filename_ext = ".fbx"
    filter_glob: StringProperty(default="*.fbx;*.json", options={'HIDDEN'})
    target: EnumProperty(
        name="対象 (Objects)", default='SELECTED',
        items=(('SELECTED', "選択オブジェクト (Selected)", "選択中のオブジェクトだけを書き出す"),
               ('VISIBLE', "表示中すべて (All Visible)", "表示されているオブジェクトをすべて書き出す")))
    export_fbx: BoolProperty(
        name="FBX を書き出す (Write FBX)", default=True,
        description="オフにすると JSON とテクスチャだけを書き出す")
    copy_textures: BoolProperty(
        name="テクスチャをコピー (Copy Textures)", default=True,
        description="JSON の隣の textures フォルダにコピーする。パック済み・生成画像は PNG で保存する")

    def invoke(self, context, event):
        if not self.filepath:
            obj = context.active_object
            name = bpy.path.display_name_from_filepath(bpy.data.filepath) or (obj.name if obj else "NataneToon")
            self.filepath = bpy.path.clean_name(name) + ".fbx"
        return super().invoke(context, event)

    def execute(self, context):
        try:
            result = exporter.export_set(context, self.filepath, self.target == 'SELECTED',
                                         self.export_fbx, self.copy_textures)
        except (RuntimeError, OSError) as ex:
            self.report({'ERROR'}, "書き出せません: %s" % ex)
            return {'CANCELLED'}
        for message in result["warnings"]:
            self.report({'WARNING'}, message)
        self.report({'INFO'}, "%d 個のマテリアルを書き出しました: %s"
                    % (len(result["materials"]), os.path.basename(result["json"])))
        return {'FINISHED'}


def menu_func_export(self, context):
    self.layout.operator(NATANE_OT_export_unity.bl_idname, text="Unity (Natane Toon) (.fbx + .json)")


classes = (
    NATANE_OT_export_unity,
    NATANE_OT_convert_material,
    NATANE_OT_import_mat,
    NATANE_OT_batch_import,
    NATANE_OT_sync_outline,
    NATANE_OT_remove_outline,
    NATANE_OT_set_texture,
    NATANE_OT_clear_texture,
    NATANE_OT_setup_scene,
    NATANE_OT_rescan_guids,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.TOPBAR_MT_file_export.append(menu_func_export)


def unregister():
    bpy.types.TOPBAR_MT_file_export.remove(menu_func_export)
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
