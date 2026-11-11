using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// テクスチャスタジオ統合ウィンドウのタブ。
    /// </summary>
    public enum TextureStudioTab
    {
        MaskPaint,
        TextureOptimize,
        Generators
    }

    /// <summary>
    /// Texture Studio Window
    /// テクスチャスタジオ
    ///
    /// テクスチャ/マスク系ツールを1つのタブ付きウィンドウへ統合する:
    ///   1. マスクペイント  — シーンビュー マスクペインター (埋め込み)
    ///   2. テクスチャ最適化 — TextureOptimizer の共有ドローワ (埋め込み)
    ///   3. 生成ツール       — Dissolve / GPU Particle / Smooth Normal / Map Generator のクイック起動
    ///
    /// Consolidates the texture/mask tool cluster into a single tabbed window.
    /// </summary>
    public class NataneTextureStudioWindow : NataneTabbedToolWindow<TextureStudioTab>
    {
        protected override string WindowTitleJP => "テクスチャスタジオ";
        protected override string WindowTitleEN => "Texture Studio";
        protected override Vector2 DefaultMinSize => new Vector2(680, 620);

        protected override INataneToolTab CreateTab(TextureStudioTab tab)
        {
            switch (tab)
            {
                case TextureStudioTab.MaskPaint:       return new MaskPaintTab();
                case TextureStudioTab.TextureOptimize: return new TextureOptimizeTab();
                case TextureStudioTab.Generators:      return new GeneratorsTab();
                default:                               return new MaskPaintTab();
            }
        }

        [MenuItem(NataneToolMenuPaths.TextureStudio, false, 71)]
        public static void ShowWindow()
        {
            var w = GetWindow<NataneTextureStudioWindow>();
            w.OpenToTab(TextureStudioTab.MaskPaint);
        }

        /// <summary>指定タブを開いた状態で表示する。旧メニューのリダイレクトから使用。</summary>
        public static void ShowTab(TextureStudioTab tab)
        {
            var w = GetWindow<NataneTextureStudioWindow>();
            w.OpenToTab(tab);
        }

        // =================================================================
        //  Tab 1: Mask Paint (embeds the reusable painter core)
        // =================================================================
        private sealed class MaskPaintTab : INataneToolTab
        {
            private NataneMaskPainterCore core;

            public string TabLabel   => L("マスクペイント", "Mask Paint");
            public string TabTooltip => L("シーンビューでマスクテクスチャを直接ペイント", "Paint mask textures directly in the Scene view");
            public string HelpToolKey => "MaskPainter";
            public bool RequiresMaterial => false;

            public void OnTabEnable(EditorWindow parentWindow)
            {
                if (core == null)
                {
                    core = new NataneMaskPainterCore();
                }
                core.Activate();
            }

            public void OnTabDisable()
            {
                core?.Deactivate();
            }

            public void OnTabDestroy()
            {
                core?.Dispose();
                core = null;
            }

            public void OnTabGUI(Material contextMaterial)
            {
                if (core == null)
                {
                    core = new NataneMaskPainterCore();
                    core.Activate();
                }
                core.DrawControls();
            }
        }

        // =================================================================
        //  Tab 2: Texture Optimize (embeds the shared drawer)
        // =================================================================
        private sealed class TextureOptimizeTab : INataneToolTab
        {
            private TextureOptimizerDrawer drawer;

            public string TabLabel   => L("テクスチャ最適化", "Texture Optimize");
            public string TabTooltip => L("テクスチャサイズ・圧縮・ミップマップを最適化", "Optimize texture size, compression, and mipmaps");
            public string HelpToolKey => "TextureOptimizer";
            public bool RequiresMaterial => false;

            public void OnTabEnable(EditorWindow parentWindow)
            {
                if (drawer == null)
                {
                    drawer = new TextureOptimizerDrawer();
                }
            }

            public void OnTabDisable() { }
            public void OnTabDestroy() { drawer = null; }

            public void OnTabGUI(Material contextMaterial)
            {
                if (drawer == null)
                {
                    drawer = new TextureOptimizerDrawer();
                }
                drawer.DrawGUI();
            }
        }

        // =================================================================
        //  Tab 3: Generators (quick-launch cards, menu-open shortcuts)
        // =================================================================
        private sealed class GeneratorsTab : INataneToolTab
        {
            private const string GpuParticleMenu = "Tools/Natane/メッシュ Mesh/GPUパーティクルメッシュ生成 GPU Particle Mesh";
            private const string MapGeneratorMenu = "Tools/MapGenerator/Map Generator Window";

            public string TabLabel   => L("生成ツール", "Generators");
            public string TabTooltip => L("テクスチャ/メッシュ生成ツールを起動", "Launch texture/mesh generation tools");
            public string HelpToolKey => "DissolvePatternGenerator";
            public bool RequiresMaterial => false;

            public void OnTabEnable(EditorWindow parentWindow) { }
            public void OnTabDisable() { }
            public void OnTabDestroy() { }

            public void OnTabGUI(Material contextMaterial)
            {
                EditorGUILayout.HelpBox(
                    L("関連する生成ツールをここから起動できます。各ツールは個別ウィンドウで開きます。",
                      "Launch related generation tools from here. Each opens in its own window."),
                    MessageType.Info);
                EditorGUILayout.Space(SPACE_SMALL);

                DrawCard(
                    L("ディゾルブパターン生成", "Dissolve Pattern Generator"),
                    L("ディゾルブ/溶解エフェクト用のノイズパターンテクスチャを生成します。",
                      "Generate noise pattern textures for dissolve effects."),
                    NataneToolMenuPaths.DissolvePatternGenerator);

                DrawCard(
                    L("ハッチング/水彩素材生成", "Hatching & Watercolor Generator"),
                    L("ハッチング6段TAMと、水彩の粒状感/紙目テクスチャを生成します。",
                      "Generate 6-level hatching TAMs and watercolor granulation / paper textures."),
                    NataneToolMenuPaths.HatchingToneGenerator);

                DrawCard(
                    L("GPUパーティクルメッシュ生成", "GPU Particle Mesh Generator"),
                    L("GPU パーティクル表現用のベイク済みメッシュを生成します。",
                      "Generate baked meshes for GPU particle effects."),
                    GpuParticleMenu);

                DrawCard(
                    L("スムース法線ベイク", "Smooth Normal Baker"),
                    L("アウトライン用のスムース法線を接線などに書き込みます。",
                      "Bake smooth normals for stable outlines into a mesh."),
                    NataneToolMenuPaths.SmoothNormalBaker);

                DrawCard(
                    L("マップジェネレーター", "Map Generator"),
                    L("ノーマル/AO/スムースネスなどの補助マップを生成します。",
                      "Generate normal / AO / smoothness and other auxiliary maps."),
                    MapGeneratorMenu);
            }

            private void DrawCard(string title, string description, string menuPath)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(L("開く", "Open"), GUILayout.Height(BUTTON_HEIGHT_SMALL)))
                {
                    LaunchMenu(menuPath, title);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(SPACE_TINY);
            }

            private void LaunchMenu(string menuPath, string title)
            {
                // Prefer the registry-aware launcher; fall back to a raw menu execution
                // for tools that live outside the Natane menu tree (e.g. MapGenerator).
                if (NataneToolMenuPaths.TryExecute(menuPath))
                {
                    return;
                }
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                {
                    EditorUtility.DisplayDialog(
                        L("起動できません", "Cannot Launch"),
                        string.Format("{0}\n{1}", title, menuPath),
                        "OK");
                }
            }
        }
    }
}
