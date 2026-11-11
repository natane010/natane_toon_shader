using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// lilToon からの移行を「今いる場所」から 1 操作で始めるための入口。
    ///
    /// - Project でマテリアル / フォルダを右クリック → 複製して変換（元は残る）
    /// - Hierarchy でアバターを右クリック → 使用中の lilToon マテリアルを複製変換して差し替え
    /// - マテリアルの歯車メニュー → その場で変換（Undo 可・バックアップ付き）
    /// - シェーダー欄で lilToon から Natane Toon に切り替え → 設定を自動で引き継ぐ
    ///
    /// いずれもウィンドウと同じ変換処理（LilToonMigrationTool）を呼ぶ。
    /// </summary>
    [InitializeOnLoad]
    internal static class LilToonQuickMigrationMenu
    {
        private const string AssetsMenu = "Assets/Natane/lilToon から変換 Convert from lilToon";
        private const string GameObjectMenu = "GameObject/Natane/lilToon から変換 (アバター全体) Convert from lilToon";
        private const string ContextMenu = "CONTEXT/Material/Natane Toon へ変換 (lilToon) Convert to Natane Toon";

        static LilToonQuickMigrationMenu()
        {
            NataneLilToonMigrationBridge.ConvertInPlace = ConvertInPlace;
        }

        private static bool ConvertInPlace(Material material, Shader forcedShader)
        {
            var result = LilToonMigrationTool.ConvertMaterials(
                new[] { material },
                new LilToonMigrationTool.QuickOptions
                {
                    replaceOriginal = true,
                    // シェーダー欄の切り替えは Undo で戻せる。バックアップアセットは増やさない。
                    createBackup = false,
                    forcedShader = forcedShader
                });
            return result.succeeded == 1;
        }

        // ----------------------------------------------------------------
        // Project ウィンドウ: マテリアル / フォルダ
        // ----------------------------------------------------------------

        private static List<Material> CollectSelectedLilToonMaterials()
        {
            var found = new HashSet<Material>();
            var folders = new List<string>();

            foreach (Object obj in Selection.objects)
            {
                Material material = obj as Material;
                if (material != null)
                {
                    if (LilToonMigrationTool.IsLilToonMaterial(material)) found.Add(material);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)) folders.Add(path);
            }

            if (folders.Count > 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", folders.ToArray()))
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (LilToonMigrationTool.IsLilToonMaterial(material)) found.Add(material);
                }
            }

            return found.Where(m => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))).ToList();
        }

        [MenuItem(AssetsMenu, true)]
        private static bool ConvertSelectedAssetsValidate()
        {
            // フォルダの中身まで毎回走査すると右クリックが重くなるので、
            // フォルダが含まれていれば有効にして、実行時に数える。
            foreach (Object obj in Selection.objects)
            {
                if (LilToonMigrationTool.IsLilToonMaterial(obj as Material)) return true;
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)) return true;
            }
            return false;
        }

        [MenuItem(AssetsMenu, false, 90)]
        private static void ConvertSelectedAssets()
        {
            List<Material> materials = CollectSelectedLilToonMaterials();
            if (materials.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    L("lilToon から変換", "Convert from lilToon"),
                    L("選択範囲に lilToon のマテリアルが見つかりませんでした。", "No lilToon materials were found in the selection."),
                    "OK");
                return;
            }

            if (materials.Count > 1 && !EditorUtility.DisplayDialog(
                    L("lilToon から変換", "Convert from lilToon"),
                    L($"{materials.Count} 個の lilToon マテリアルを Natane Toon に変換します。\n\n元のマテリアルは変更しません。変換後のマテリアルは、それぞれ隣の「NataneToon」フォルダに作成されます。",
                      $"Convert {materials.Count} lilToon materials to Natane Toon.\n\nThe originals are left untouched. Converted materials are created in a \"NataneToon\" folder next to each one."),
                    L("変換する", "Convert"),
                    L("キャンセル", "Cancel")))
            {
                return;
            }

            var result = LilToonMigrationTool.ConvertMaterials(materials, new LilToonMigrationTool.QuickOptions());

            Object[] converted = result.mapping.Values.Cast<Object>().ToArray();
            if (converted.Length > 0)
            {
                Selection.objects = converted;
                EditorGUIUtility.PingObject(converted[0]);
            }

            LilToonMigrationTool.ShowQuickResultDialog(
                result,
                L("元のマテリアルはそのまま残っています。", "The original materials were left untouched."));
        }

        // ----------------------------------------------------------------
        // Hierarchy: アバター全体
        // ----------------------------------------------------------------

        private static int CountLilToonMaterials(GameObject root)
        {
            if (root == null) return 0;
            return root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(LilToonMigrationTool.IsLilToonMaterial)
                .Distinct()
                .Count();
        }

        [MenuItem(GameObjectMenu, true)]
        private static bool ConvertAvatarValidate()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(GameObjectMenu, false, 90)]
        private static void ConvertAvatar(MenuCommand command)
        {
            // 複数選択だと選択数だけ呼ばれるので、最初の 1 回だけ処理する。
            if (Selection.objects.Length > 1 && command.context != null && command.context != Selection.objects[0]) return;

            GameObject[] roots = Selection.gameObjects;
            int count = roots.Sum(CountLilToonMaterials);
            if (count == 0)
            {
                EditorUtility.DisplayDialog(
                    L("lilToon から変換", "Convert from lilToon"),
                    L("選択したオブジェクトは lilToon のマテリアルを使っていません。", "The selected objects do not use any lilToon materials."),
                    "OK");
                return;
            }

            string rootName = roots.Length == 1 ? roots[0].name : L($"{roots.Length} 個のオブジェクト", $"{roots.Length} objects");
            if (!EditorUtility.DisplayDialog(
                    L("lilToon から変換", "Convert from lilToon"),
                    L($"「{rootName}」が使っている lilToon マテリアル {count} 個を Natane Toon に変換し、シーン上のこのオブジェクトに割り当てます。\n\n・元のマテリアルと Prefab アセットは変更しません\n・Ctrl+Z で割り当てを元に戻せます",
                      $"Convert the {count} lilToon material(s) used by \"{rootName}\" to Natane Toon and assign them to this object in the scene.\n\n- The original materials and the prefab asset are not modified\n- Ctrl+Z restores the previous assignment"),
                    L("変換する", "Convert"),
                    L("キャンセル", "Cancel")))
            {
                return;
            }

            var total = new LilToonMigrationTool.QuickResult();
            foreach (GameObject root in roots)
            {
                var result = LilToonMigrationTool.ConvertRenderersUnder(root, new LilToonMigrationTool.QuickOptions());
                total.total += result.total;
                total.succeeded += result.succeeded;
                total.warningCount += result.warningCount;
                total.failedNames.AddRange(result.failedNames);
            }

            LilToonMigrationTool.ShowQuickResultDialog(
                total,
                L("Ctrl+Z で lilToon の割り当てに戻せます。", "Ctrl+Z restores the lilToon assignment."));
        }

        // ----------------------------------------------------------------
        // マテリアルの歯車メニュー
        // ----------------------------------------------------------------

        [MenuItem(ContextMenu, true)]
        private static bool ConvertContextValidate(MenuCommand command)
        {
            return LilToonMigrationTool.IsLilToonMaterial(command.context as Material);
        }

        [MenuItem(ContextMenu, false, 2000)]
        private static void ConvertContext(MenuCommand command)
        {
            Material material = command.context as Material;
            if (!LilToonMigrationTool.IsLilToonMaterial(material)) return;

            var result = LilToonMigrationTool.ConvertMaterials(
                new[] { material },
                new LilToonMigrationTool.QuickOptions { replaceOriginal = true, createBackup = true });

            if (result.succeeded == 0 || result.warningCount > 0)
            {
                LilToonMigrationTool.ShowQuickResultDialog(
                    result,
                    L("Ctrl+Z で lilToon に戻せます。元の状態は「_lilToon_backup」としても保存してあります。",
                      "Ctrl+Z restores lilToon. The original is also saved as \"_lilToon_backup\"."));
            }
        }
    }
}
