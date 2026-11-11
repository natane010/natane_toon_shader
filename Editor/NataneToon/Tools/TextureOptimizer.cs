using UnityEngine;
using UnityEditor;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Texture optimization tool
    /// テクスチャ最適化ツール
    ///
    /// The core UI/logic now lives in the shared <see cref="TextureOptimizerDrawer"/>
    /// so it can also be embedded in the Texture Studio window. This window remains
    /// as a thin delegator to keep the legacy menu path working with no breakage.
    /// 実装は共有ドローワ <see cref="TextureOptimizerDrawer"/> に移動し、テクスチャスタジオ
    /// でも再利用できる。旧メニューを壊さないため、このウィンドウは薄い委譲役として残す。
    /// </summary>
    public class TextureOptimizer : EditorWindow
    {
        private TextureOptimizerDrawer drawer;

        [MenuItem("Tools/Natane/最適化 Optimization/テクスチャ最適化 Texture Optimizer", false, 32)]
        public static void ShowWindow()
        {
            var window = GetWindow<TextureOptimizer>(L("テクスチャ最適化", "Texture Optimizer"));
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnEnable()
        {
            drawer = new TextureOptimizerDrawer();
        }

        private void OnGUI()
        {
            NataneToonShaderGUIUtility.DrawToolHeader("テクスチャ最適化ツール", "Texture Optimizer", "TextureOptimizer");
            if (drawer == null)
            {
                drawer = new TextureOptimizerDrawer();
            }
            drawer.DrawGUI();
        }
    }
}
