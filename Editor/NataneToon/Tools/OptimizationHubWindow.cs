using UnityEngine;
using UnityEditor;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// 最適化ハブウィンドウのタブ列挙型
    /// </summary>
    public enum OptimizationHubTab
    {
        TextureOptimizer,
        OutlineOptimizer,
        RefractionBalancer,
        // 末尾に追加（タブの永続化はインデックス保存なので、既存の並びは変えない）
        LiteConversion
    }

    /// <summary>
    /// Optimization Hub Window
    /// 最適化ハブ
    /// Consolidates 3 optimization tools into a single tabbed window:
    ///   Texture Optimizer / Outline Optimizer / Refraction Quality Balancer
    /// </summary>
    public class OptimizationHubWindow : NataneTabbedToolWindow<OptimizationHubTab>
    {
        // ===== Window metadata =====
        protected override string WindowTitleJP => "最適化ハブ";
        protected override string WindowTitleEN => "Optimization Hub";
        protected override Vector2 DefaultMinSize => new Vector2(600, 500);

        // ===== Tab factory =====
        protected override INataneToolTab CreateTab(OptimizationHubTab tab)
        {
            switch (tab)
            {
                case OptimizationHubTab.TextureOptimizer:    return new TextureOptimizerStubTab();
                case OptimizationHubTab.OutlineOptimizer:    return new OutlineOptimizerStubTab();
                case OptimizationHubTab.RefractionBalancer:  return new RefractionBalancerStubTab();
                case OptimizationHubTab.LiteConversion:      return new LiteConversionTab();
                default:                                     return new TextureOptimizerStubTab();
            }
        }

        // ===== Menu items =====
        [MenuItem("Tools/Natane/最適化 Optimization/最適化ハブ Optimization Hub", false, 10)]
        public static void ShowWindow()
        {
            var w = GetWindow<OptimizationHubWindow>();
            w.OpenToTab(OptimizationHubTab.TextureOptimizer);
        }

        /// <summary>
        /// 指定タブを開いた状態でウィンドウを表示する。
        /// オプションでマテリアルコンテキストを渡せる。
        /// </summary>
        public static void ShowTab(OptimizationHubTab tab, Material material = null)
        {
            var w = GetWindow<OptimizationHubWindow>();
            w.OpenToTab(tab, material);
        }

        // =================================================================
        //  Stub tab adapters (placeholder until full migration)
        // =================================================================

        /// <summary>
        /// テクスチャオプティマイザー スタブタブ
        /// </summary>
        private sealed class TextureOptimizerStubTab : INataneToolTab
        {
            public string TabLabel   => L("テクスチャ", "Texture");
            public string TabTooltip => L("テクスチャの最適化と圧縮設定", "Texture optimization and compression settings");
            public string HelpToolKey => "TextureOptimizer";
            public bool RequiresMaterial => false;

            public void OnTabEnable(EditorWindow parentWindow) { }
            public void OnTabDisable() { }
            public void OnTabDestroy() { }

            public void OnTabGUI(Material contextMaterial)
            {
                EditorGUILayout.HelpBox(
                    L("テクスチャオプティマイザー — このタブは統合中です",
                      "Texture Optimizer — This tab is being integrated"),
                    MessageType.Info);

                EditorGUILayout.Space(5);
                if (GUILayout.Button(
                    L("旧テクスチャオプティマイザーを開く", "Open Legacy Texture Optimizer"),
                    GUILayout.Height(25)))
                {
                    NataneToolMenuPaths.TryOpenByToolKey("TextureOptimizer");
                }
            }
        }

        /// <summary>
        /// アウトラインオプティマイザー スタブタブ
        /// </summary>
        private sealed class OutlineOptimizerStubTab : INataneToolTab
        {
            public string TabLabel   => L("アウトライン", "Outline");
            public string TabTooltip => L("アウトラインパラメータの最適化", "Outline parameter optimization");
            public string HelpToolKey => "OutlineOptimizer";
            public bool RequiresMaterial => true;

            public void OnTabEnable(EditorWindow parentWindow) { }
            public void OnTabDisable() { }
            public void OnTabDestroy() { }

            public void OnTabGUI(Material contextMaterial)
            {
                EditorGUILayout.HelpBox(
                    L("アウトラインオプティマイザー — このタブは統合中です",
                      "Outline Optimizer — This tab is being integrated"),
                    MessageType.Info);

                EditorGUILayout.Space(5);
                if (GUILayout.Button(
                    L("旧アウトラインオプティマイザーを開く", "Open Legacy Outline Optimizer"),
                    GUILayout.Height(25)))
                {
                    NataneToolMenuPaths.TryOpenByToolKey("OutlineOptimizer");
                }
            }
        }

        /// <summary>
        /// 屈折クオリティバランサー スタブタブ
        /// </summary>
        private sealed class RefractionBalancerStubTab : INataneToolTab
        {
            public string TabLabel   => L("屈折", "Refraction");
            public string TabTooltip => L("屈折品質とパフォーマンスのバランス調整", "Balance refraction quality and performance");
            public string HelpToolKey => "RefractionQualityBalancer";
            public bool RequiresMaterial => true;

            public void OnTabEnable(EditorWindow parentWindow) { }
            public void OnTabDisable() { }
            public void OnTabDestroy() { }

            public void OnTabGUI(Material contextMaterial)
            {
                EditorGUILayout.HelpBox(
                    L("屈折クオリティバランサー — このタブは統合中です",
                      "Refraction Quality Balancer — This tab is being integrated"),
                    MessageType.Info);

                EditorGUILayout.Space(5);
                if (GUILayout.Button(
                    L("旧屈折クオリティバランサーを開く", "Open Legacy Refraction Quality Balancer"),
                    GUILayout.Height(25)))
                {
                    NataneToolMenuPaths.TryOpenByToolKey("RefractionQualityBalancer");
                }
            }
        }

        /// <summary>
        /// Lite 一括変換タブ。
        /// 非 Lite バリアントは GrabPass（毎フレームの画面コピー）と hull / domain ステージを常に持つ。
        /// どちらの機能も使っていないマテリアルを Lite に切り替えると、見た目を変えずにそのコストが消える。
        /// </summary>
        private sealed class LiteConversionTab : INataneToolTab
        {
            public string TabLabel   => L("Lite 変換", "Lite Conversion");
            public string TabTooltip => L("GrabPass / テッセレーションを使っていないマテリアルを Lite バリアントに一括変換", "Batch-convert materials that use neither GrabPass features nor tessellation to the Lite variants");
            public string HelpToolKey => "OptimizationHub";
            public bool RequiresMaterial => false;

            private readonly System.Collections.Generic.List<Material> convertible = new System.Collections.Generic.List<Material>();
            private readonly System.Collections.Generic.List<Material> blocked = new System.Collections.Generic.List<Material>();
            private readonly System.Collections.Generic.List<string> blockedReasons = new System.Collections.Generic.List<string>();
            private Vector2 scroll;
            private bool scanned;
            private string scanSource = string.Empty;
            private int lastConverted = -1;

            public void OnTabEnable(EditorWindow parentWindow) { }
            public void OnTabDisable() { }
            public void OnTabDestroy() { }

            private void Scan(System.Collections.Generic.List<Material> materials, string source)
            {
                convertible.Clear();
                blocked.Clear();
                blockedReasons.Clear();
                foreach (Material material in materials)
                {
                    if (material == null || material.shader == null) continue;
                    if (!NataneLiteVariantUtility.Counterparts.ContainsKey(material.shader.name)) continue;

                    string liteName, blockingProperty;
                    if (NataneLiteVariantUtility.CanConvert(material, out liteName, out blockingProperty))
                    {
                        convertible.Add(material);
                    }
                    else
                    {
                        blocked.Add(material);
                        blockedReasons.Add(blockingProperty ?? string.Empty);
                    }
                }
                scanned = true;
                scanSource = source;
                lastConverted = -1;
            }

            public void OnTabGUI(Material contextMaterial)
            {
                EditorGUILayout.HelpBox(
                    L("通常バリアントは GrabPass（毎フレームの画面コピー）と hull / domain ステージを常に実行します。\n" +
                      "屈折 / ソフトフィルタ / クワハラ / カラーブリーディング / 色収差 / テッセレーションを使っていないマテリアルは、" +
                      "Lite バリアントに切り替えるだけで見た目を変えずに軽くなります。プロパティはそのまま引き継がれ、Undo できます。",
                      "The regular variants always run a GrabPass (a full-screen copy every frame) and the hull / domain stages.\n" +
                      "Materials that use none of Refraction / Soft Filter / Kuwahara / Color Bleeding / Chromatic Aberration / Tessellation " +
                      "get cheaper by switching to the Lite variant, with no visual change. Properties carry over and the change can be undone."),
                    MessageType.Info);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(L("選択から検索", "Scan Selection"), GUILayout.Height(24)))
                    {
                        Scan(NataneLiteVariantUtility.CollectFromSelection(), L("選択", "Selection"));
                    }
                    if (GUILayout.Button(L("プロジェクト全体を検索", "Scan Whole Project"), GUILayout.Height(24)))
                    {
                        Scan(NataneLiteVariantUtility.CollectFromProject(), L("プロジェクト", "Project"));
                    }
                }

                if (!scanned)
                {
                    EditorGUILayout.LabelField(
                        L("アバターのルートやマテリアルを選択して「選択から検索」を押してください。", "Select an avatar root or materials, then press Scan Selection."),
                        EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    string.Format(L("{0}: 変換可能 {1} 件 / 機能使用中のため対象外 {2} 件", "{0}: {1} convertible / {2} kept (feature in use)"),
                        scanSource, convertible.Count, blocked.Count),
                    EditorStyles.boldLabel);

                scroll = EditorGUILayout.BeginScrollView(scroll);
                using (new EditorGUI.DisabledScope(true))
                {
                    foreach (Material material in convertible)
                    {
                        if (material == null) continue;
                        EditorGUILayout.ObjectField(material, typeof(Material), false);
                    }
                }
                if (blocked.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(L("対象外（Lite に無い機能を使用中）", "Kept (uses a feature the Lite variant lacks)"), EditorStyles.miniBoldLabel);
                    for (int i = 0; i < blocked.Count; i++)
                    {
                        if (blocked[i] == null) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.ObjectField(blocked[i], typeof(Material), false);
                            }
                            EditorGUILayout.LabelField(blockedReasons[i], EditorStyles.miniLabel, GUILayout.Width(170f));
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(convertible.Count == 0))
                {
                    if (GUILayout.Button(
                        string.Format(L("{0} 件を Lite バリアントに変換", "Convert {0} material(s) to Lite"), convertible.Count),
                        GUILayout.Height(28)))
                    {
                        Undo.IncrementCurrentGroup();
                        Undo.SetCurrentGroupName(L("Lite バリアントに一括変換", "Batch Convert to Lite Variant"));
                        lastConverted = NataneLiteVariantUtility.ConvertToLite(new System.Collections.Generic.List<Material>(convertible));
                        AssetDatabase.SaveAssets();
                        convertible.Clear();
                        GUIUtility.ExitGUI();
                    }
                }

                if (lastConverted >= 0)
                {
                    EditorGUILayout.HelpBox(
                        string.Format(L("{0} 件を変換しました。Ctrl+Z で元に戻せます。", "Converted {0} material(s). Ctrl+Z reverts."), lastConverted),
                        MessageType.None);
                }
            }
        }
    }
}
