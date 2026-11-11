using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// ウィンドウを開かずに lilToon → Natane Toon 変換を呼ぶための入口。
    ///
    /// 変換ロジック本体はウィンドウのインスタンスメソッド（設定をフィールドから読む）なので、
    /// ここでは非表示のインスタンスを一時的に作り、設定を流し込んでから同じ処理を呼ぶ。
    /// マッピングを二重に持たないための形で、右クリックメニュー・シェーダー欄の切り替え・
    /// ウィンドウのどこから変換しても結果が一致する。
    /// </summary>
    public partial class LilToonMigrationTool
    {
        public sealed class QuickOptions
        {
            /// <summary>true: 元マテリアルをその場で書き換える（Undo 可）。false: 隣の NataneToon フォルダへ複製を作る。</summary>
            public bool replaceOriginal;
            /// <summary>replaceOriginal のときだけ意味を持つ。</summary>
            public bool createBackup;
            /// <summary>変換後の自動調整（影境界・色の分離など）。メッシュのベイクは行わない。</summary>
            public bool autoOptimize = true;
            /// <summary>null なら元シェーダーから不透明 / カットアウト / 半透明を自動判定する。</summary>
            public Shader forcedShader;
        }

        public sealed class QuickResult
        {
            public int total;
            public int succeeded;
            public int warningCount;
            /// <summary>元マテリアル → 変換後マテリアル。直接置換では同じオブジェクトを指す。</summary>
            public readonly Dictionary<Material, Material> mapping = new Dictionary<Material, Material>();
            public readonly List<string> failedNames = new List<string>();
        }

        public static bool IsLilToonMaterial(Material material)
        {
            return material != null && material.shader != null &&
                   NataneShaderCatalog.IsLilToonShader(material.shader.name);
        }

        /// <summary>lilToon マテリアルをまとめて変換する。lilToon 以外と null は黙って飛ばす。</summary>
        public static QuickResult ConvertMaterials(IEnumerable<Material> materials, QuickOptions options)
        {
            var result = new QuickResult();
            if (materials == null) return result;
            if (options == null) options = new QuickOptions();

            List<Material> targets = materials.Where(IsLilToonMaterial).Distinct().ToList();
            result.total = targets.Count;
            if (targets.Count == 0) return result;

            LilToonMigrationTool tool = CreateInstance<LilToonMigrationTool>();
            tool.hideFlags = HideFlags.HideAndDontSave;
            bool previousAllowMeshBake = NataneLilToonAutoFixer.AllowMeshBake;

            try
            {
                tool.replaceOriginal = options.replaceOriginal;
                tool.createBackup = options.createBackup;
                tool.autoOptimize = options.autoOptimize;
                tool.showPreview = false;
                tool.conversionMode = ConversionMode.VisualMatch;
                tool.forcedTargetShader = options.forcedShader;
                // ワンクリック変換は「マテリアルだけが変わる」ことを期待される。
                // メッシュアセットの生成と Renderer の差し替えはウィンドウから明示的に行う。
                NataneLilToonAutoFixer.AllowMeshBake = false;

                for (int i = 0; i < targets.Count; i++)
                {
                    Material source = targets[i];
                    if (targets.Count > 1)
                    {
                        EditorUtility.DisplayProgressBar(
                            L("lilToon から変換中", "Converting from lilToon"),
                            $"{i + 1}/{targets.Count}: {source.name}",
                            (float)i / targets.Count);
                    }

                    ConversionReport report = tool.ConvertMaterialWithReport(source);
                    result.warningCount += report.warnings.Count;

                    if (!report.success)
                    {
                        result.failedNames.Add(source.name);
                        continue;
                    }

                    result.succeeded++;
                    if (options.replaceOriginal)
                    {
                        result.mapping[source] = source;
                    }
                    else
                    {
                        string convertedPath = GetConvertedMaterialPath(AssetDatabase.GetAssetPath(source));
                        Material converted = AssetDatabase.LoadAssetAtPath<Material>(convertedPath);
                        if (converted != null) result.mapping[source] = converted;
                    }
                }
            }
            finally
            {
                NataneLilToonAutoFixer.AllowMeshBake = previousAllowMeshBake;
                EditorUtility.ClearProgressBar();
                DestroyImmediate(tool);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>
        /// root 以下の Renderer が使っている lilToon マテリアルを複製変換し、シーン上の参照を差し替える。
        /// 元のマテリアルと Prefab アセットには触れない（差し替えは Prefab のオーバーライドとして残り、Undo できる）。
        /// </summary>
        public static QuickResult ConvertRenderersUnder(GameObject root, QuickOptions options)
        {
            if (root == null) return new QuickResult();
            if (options == null) options = new QuickOptions();
            // 参照の差し替えで元へ戻せることが前提なので、ここでは常に複製する。
            options.replaceOriginal = false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            IEnumerable<Material> used = renderers.SelectMany(r => r.sharedMaterials);

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("lilToon → Natane Toon");

            QuickResult result = ConvertMaterials(used, options);

            foreach (Renderer renderer in renderers)
            {
                Material[] shared = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    Material converted;
                    if (shared[i] != null && result.mapping.TryGetValue(shared[i], out converted) && converted != shared[i])
                    {
                        shared[i] = converted;
                        changed = true;
                    }
                }

                if (!changed) continue;
                Undo.RecordObject(renderer, "lilToon → Natane Toon");
                renderer.sharedMaterials = shared;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            Undo.CollapseUndoOperations(undoGroup);
            return result;
        }

        /// <summary>結果を 1 つのダイアログにまとめる。ワンクリック導線の共通の締め。</summary>
        public static void ShowQuickResultDialog(QuickResult result, string extraLine = null)
        {
            if (result == null) return;

            var text = new System.Text.StringBuilder();
            text.AppendLine(L(
                $"{result.succeeded} / {result.total} 個のマテリアルを変換しました。",
                $"Converted {result.succeeded} of {result.total} materials."));
            if (!string.IsNullOrEmpty(extraLine)) text.AppendLine(extraLine);

            if (result.failedNames.Count > 0)
            {
                text.AppendLine();
                text.AppendLine(L("変換できなかったもの:", "Could not convert:"));
                foreach (string name in result.failedNames.Take(8)) text.AppendLine("  - " + name);
                if (result.failedNames.Count > 8) text.AppendLine("  …");
            }

            if (result.warningCount > 0)
            {
                text.AppendLine();
                text.AppendLine(L(
                    $"手動での確認をおすすめする項目が {result.warningCount} 件あります（詳細は Console）。\nMatCap は見た目の差が出やすいため、テクスチャだけ引き継いでオフにしてあります。",
                    $"{result.warningCount} item(s) are worth a manual check (details in the Console).\nMatCap is carried over but left off, because it is the most likely to look different."));
            }

            EditorUtility.DisplayDialog(L("lilToon から変換", "Convert from lilToon"), text.ToString(), "OK");
        }
    }
}
