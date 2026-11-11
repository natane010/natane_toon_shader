using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Mask Painter
    /// マスクペインター (単体ウィンドウ)
    ///
    /// シーンビューで Natane マテリアルのマスクテクスチャを直接ペイントするツール。
    /// 実体は埋め込み可能な <see cref="NataneMaskPainterCore"/> にあり、
    /// このウィンドウとテクスチャスタジオの「マスクペイント」タブが同じコアを共有する。
    ///
    /// Standalone window that paints Natane mask textures directly in the Scene view.
    /// The behaviour lives in the reusable <see cref="NataneMaskPainterCore"/>, shared
    /// with the Texture Studio "Mask Paint" tab.
    /// </summary>
    public class NataneMaskPainter : EditorWindow
    {
        private NataneMaskPainterCore core;
        private Vector2 scroll;

        [MenuItem(NataneToolMenuPaths.MaskPainter, false, 70)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneMaskPainter>(L("マスクペインター", "Mask Painter"));
            window.minSize = new Vector2(360, 480);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(L("マスクペインター", "Mask Painter"));
            core = new NataneMaskPainterCore();
            core.Activate();
        }

        private void OnDisable()
        {
            if (core != null)
            {
                core.Dispose();
                core = null;
            }
        }

        private void OnGUI()
        {
            NataneToonShaderGUIUtility.DrawToolHeader("マスクペインター", "Mask Painter", "MaskPainter");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            core.DrawControls();
            EditorGUILayout.EndScrollView();
        }
    }
}
