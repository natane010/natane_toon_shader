using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// <c>Natane/Toon Shader FakeShadow</c> 専用の小さなインスペクタ。
    ///
    /// 本体の <see cref="NataneToonShaderGUI"/> は 990 プロパティ・146 機能を前提に
    /// 組まれているため、13 プロパティしか持たないこのシェーダーへ当てると
    /// 存在しないプロパティを大量に引きに行くことになる。専用に用意するほうが安全で、
    /// ユーザーにとっても迷わない。
    /// </summary>
    public class NataneFakeShadowShaderGUI : ShaderGUI
    {
        private bool _foldStencil;
        private bool _foldRender;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            if (material == null)
            {
                // 防御: マルチ編集や不正な状態でも例外を出さない。
                base.OnGUI(materialEditor, properties);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("フェイクシャドウ（前髪の落ち影）", "Fake Shadow (hair drop shadow)"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("ライティングに依存せず、板ポリまたは複製メッシュで落ち影を描きます。",
                  "Draws a drop shadow with a quad or a duplicated mesh, independent of lighting."),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("影", "Shadow"), EditorStyles.boldLabel);
            Draw(materialEditor, properties, "_ShadowColor", L("影色", "Shadow Color"));
            Draw(materialEditor, properties, "_ShadowAlpha", L("不透明度", "Shadow Alpha"));
            Draw(materialEditor, properties, "_ShadowTex", L("影の形（アルファ）", "Shadow Shape (alpha)"));
            DrawBlendMode(materialEditor, properties);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("ライト方向へずらす", "Light Shift"), EditorStyles.boldLabel);
            Draw(materialEditor, properties, "_LightShift", L("ずらす量 (m)", "Shift Amount (m)"));
            Draw(materialEditor, properties, "_LightShiftBias", L("向きの補正 (オブジェクト空間)", "Direction Bias (object space)"));
            EditorGUILayout.HelpBox(
                L("髪の複製メッシュに使うと、光の反対側へずれて顔に落ち影が出ます。板ポリなら 0 のままで構いません。",
                  "On a duplicated hair mesh this shifts it away from the light so it lands on the face. Leave at 0 for a quad."),
                MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("ライトへの追従", "Light Response"), EditorStyles.boldLabel);
            Draw(materialEditor, properties, "_LightColorFollow", L("ライト色追従", "Light Color Follow"));
            EditorGUILayout.HelpBox(
                L("0 で完全にライト非依存。暗いワールドで落ち影だけが浮くのを避けるため、既定は弱めです。",
                  "0 is fully light-independent. The default is deliberately low so the shadow does not stand out in dark worlds."),
                MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("フェード", "Fade"), EditorStyles.boldLabel);
            Draw(materialEditor, properties, "_FadeByViewAngle", L("視線角度でフェード", "Fade By View Angle"));

            EditorGUILayout.Space(6);
            _foldRender = EditorGUILayout.Foldout(_foldRender, L("描画設定", "Render Settings"), true);
            if (_foldRender)
            {
                EditorGUI.indentLevel++;
                Draw(materialEditor, properties, "_Cull", L("カリング", "Cull"));
                Draw(materialEditor, properties, "_OffsetFactor", L("深度オフセット係数", "Depth Offset Factor"));
                Draw(materialEditor, properties, "_OffsetUnits", L("深度オフセット単位", "Depth Offset Units"));
                EditorGUILayout.HelpBox(
                    L("顔と重なって Z ファイティングが出る場合は、深度オフセットを -1 程度に下げてください。",
                      "If it z-fights with the face, lower the depth offset to about -1."),
                    MessageType.None);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            _foldStencil = EditorGUILayout.Foldout(_foldStencil, L("ステンシル", "Stencil"), true);
            if (_foldStencil)
            {
                EditorGUI.indentLevel++;
                Draw(materialEditor, properties, "_StencilRef", L("参照値", "Reference"));
                Draw(materialEditor, properties, "_StencilComp", L("比較", "Comparison"));
                Draw(materialEditor, properties, "_StencilOp", L("通過時の操作", "Pass Operation"));
                Draw(materialEditor, properties, "_StencilFail", L("失敗時の操作", "Fail Operation"));
                Draw(materialEditor, properties, "_StencilZFail", L("Z失敗時の操作", "ZFail Operation"));
                Draw(materialEditor, properties, "_StencilReadMask", L("読み出しマスク", "Read Mask"));
                Draw(materialEditor, properties, "_StencilWriteMask", L("書き込みマスク", "Write Mask"));
                EditorGUILayout.HelpBox(
                    L("顔が書いたステンシル領域に限定すると、落ち影が顔からはみ出しません。\n" +
                      "ステンシルプリセットツールから両者の参照値をまとめて設定できます。",
                      "Restricting to the region the face wrote keeps the shadow from spilling off the face.\n" +
                      "The Stencil Preset tool can set both reference values together."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button(L("フェイクシャドウ設定ツールを開く", "Open Fake Shadow Setup Tool"), GUILayout.Height(24)))
            {
                EditorApplication.ExecuteMenuItem(NataneToolMenuPaths.FakeShadowSetup);
            }

            EditorGUILayout.Space(4);
            materialEditor.RenderQueueField();
            materialEditor.DoubleSidedGIField();
            materialEditor.EnableInstancingField();
        }

        /// <summary>合成モードに合わせて Blend ステートも書き換える。</summary>
        private static void DrawBlendMode(MaterialEditor editor, MaterialProperty[] properties)
        {
            MaterialProperty mode = FindProperty("_ShadowBlendMode", properties, false);
            if (mode == null) return;

            EditorGUI.BeginChangeCheck();
            editor.ShaderProperty(mode, L("合成", "Blend Mode"));
            if (!EditorGUI.EndChangeCheck()) return;

            foreach (Object target in editor.targets)
            {
                var material = target as Material;
                if (material == null) continue;
                ApplyBlendMode(material, material.GetFloat("_ShadowBlendMode") > 0.5f);
            }
        }

        /// <summary>乗算は DstColor * Zero、通常はアルファ合成。</summary>
        public static void ApplyBlendMode(Material material, bool multiply)
        {
            if (material == null) return;
            if (material.HasProperty("_ShadowBlendMode")) material.SetFloat("_ShadowBlendMode", multiply ? 1f : 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)(multiply ? UnityEngine.Rendering.BlendMode.DstColor : UnityEngine.Rendering.BlendMode.SrcAlpha));
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)(multiply ? UnityEngine.Rendering.BlendMode.Zero : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        }

        /// <summary>
        /// プロパティが無い場合は黙って飛ばす。将来プロパティを削っても
        /// インスペクタが例外で落ちないようにするため。
        /// </summary>
        private static void Draw(MaterialEditor editor, MaterialProperty[] properties, string name, string label)
        {
            MaterialProperty property = FindProperty(name, properties, false);
            if (property == null) return;
            editor.ShaderProperty(property, label);
        }
    }
}
