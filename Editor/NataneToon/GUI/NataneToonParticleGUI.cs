using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Custom ShaderGUI for "Natane/Toon Shader (Particle)".
    /// Small, dedicated inspector: blend mode dropdown drives _SrcBlend/_DstBlend/_BlendOp.
    /// </summary>
    public class NataneToonParticleGUI : ShaderGUI
    {
        private enum ParticleBlendMode
        {
            Alpha = 0,
            Additive = 1,
            Premultiplied = 2,
            Multiply = 3
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material material = materialEditor.target as Material;
            if (material == null) return;

            NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate budget = NataneToonSamplerBudgetEstimator.Estimate(material);
            NataneToonInspectorComponents.DrawInspectorHeader(
                "Natane Toon Shader",
                L("軽量トゥーンパーティクル", "Lightweight Toon Particle"),
                "Particle",
                $"S {budget.EstimatedSamplers}/{budget.Limit}",
                budget.IsOverLimit
                    ? NataneInspectorStatus.Error
                    : budget.IsWarning ? NataneInspectorStatus.Warning : NataneInspectorStatus.Success,
                () =>
                {
                    NataneToonLocalization.ToggleLanguage();
                    materialEditor.Repaint();
                },
                null);

            // ===== Main =====
            NataneToonInspectorComponents.DrawGroupHeader(L("メイン", "Main"));
            MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
            MaterialProperty color = FindProperty("_Color", properties, false);
            if (mainTex != null)
            {
                materialEditor.TexturePropertySingleLine(
                    new GUIContent(L("メインテクスチャ", "Main Texture"),
                        L("テクスチャ × 頂点カラー × カラーで合成されます", "Combined as Texture x Vertex Color x Color")),
                    mainTex, color);
                materialEditor.TextureScaleOffsetProperty(mainTex);
            }

            EditorGUILayout.Space(8);

            // ===== Blend Mode =====
            NataneToonInspectorComponents.DrawGroupHeader(L("ブレンド", "Blending"));
            MaterialProperty blendMode = FindProperty("_BlendMode", properties, false);
            if (blendMode != null)
            {
                var mode = (ParticleBlendMode)(int)blendMode.floatValue;
                EditorGUI.showMixedValue = blendMode.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                var newMode = (ParticleBlendMode)EditorGUILayout.EnumPopup(
                    new GUIContent(L("ブレンドモード", "Blend Mode"),
                        L("Alpha: 半透明 / Additive: 加算(光) / Premultiplied: 事前乗算 / Multiply: 乗算(煙・影)",
                          "Alpha: transparency / Additive: glow / Premultiplied / Multiply: smoke, darkening")),
                    mode);
                if (EditorGUI.EndChangeCheck())
                {
                    materialEditor.RegisterPropertyChangeUndo(L("ブレンドモード変更", "Change Blend Mode"));
                    blendMode.floatValue = (float)newMode;
                    foreach (Object target in blendMode.targets)
                    {
                        ApplyBlendMode((Material)target, newMode);
                    }
                }
                EditorGUI.showMixedValue = false;
            }

            EditorGUILayout.Space(8);

            // ===== Toon Lighting =====
            NataneToonInspectorComponents.DrawGroupHeader(L("トゥーンライティング", "Toon Lighting"));
            MaterialProperty toonLighting = FindProperty("_ParticleToonLighting", properties, false);
            if (toonLighting != null)
            {
                materialEditor.ShaderProperty(toonLighting,
                    new GUIContent(L("トゥーンライティングを有効化", "Enable Toon Lighting"),
                        L("メインディレクショナルライト + SHアンビエントに対する2段トゥーンシェーディング。OFFでUnlit(通常のパーティクル)",
                          "2-step toon shading vs main directional light + SH ambient. Off = unlit (classic particle)")));
                if (toonLighting.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    DrawIfFound(materialEditor, properties, "_ShadowColor", L("影色", "Shadow Color"));
                    DrawIfFound(materialEditor, properties, "_ShadowThreshold", L("影のしきい値", "Shadow Threshold"));
                    DrawIfFound(materialEditor, properties, "_ShadowSmoothness", L("影の滑らかさ", "Shadow Smoothness"));
                    EditorGUILayout.HelpBox(
                        L("ライティングには Renderer の Custom Vertex Streams に Normal が必要です。",
                          "Lighting requires the Normal stream in the Renderer's Custom Vertex Streams."),
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(8);

            // ===== Soft Particles =====
            NataneToonInspectorComponents.DrawGroupHeader(L("ソフトパーティクル", "Soft Particles"));
            MaterialProperty softParticles = FindProperty("_SoftParticlesEnabled", properties, false);
            if (softParticles != null)
            {
                materialEditor.ShaderProperty(softParticles,
                    new GUIContent(L("ソフトパーティクルを有効化", "Enable Soft Particles"),
                        L("地面などとの交差を滑らかにフェードします。カメラのデプステクスチャが必要です",
                          "Softly fades intersections with geometry. Requires the camera depth texture")));
                if (softParticles.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    DrawIfFound(materialEditor, properties, "_SoftParticleFadeDistance", L("フェード距離", "Fade Distance"));
                    EditorGUILayout.HelpBox(
                        L("デプステクスチャが有効な環境でのみ動作します（VRChatではシャドウ付きディレクショナルライトがあるワールドなど）。",
                          "Works only when the depth texture is available (e.g. VRChat worlds with a shadowed directional light)."),
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(8);

            // ===== Flipbook =====
            NataneToonInspectorComponents.DrawGroupHeader(L("フリップブック", "Flipbook"));
            MaterialProperty flipbook = FindProperty("_FlipbookBlending", properties, false);
            if (flipbook != null)
            {
                materialEditor.ShaderProperty(flipbook,
                    new GUIContent(L("フリップブック補間を有効化", "Enable Flipbook Blending"),
                        L("Texture Sheet Animation のフレーム間を滑らかに補間します",
                          "Smoothly blends between Texture Sheet Animation frames")));
                if (flipbook.floatValue > 0.5f)
                {
                    EditorGUILayout.HelpBox(
                        L("Renderer の Custom Vertex Streams に UV2 と AnimBlend を追加してください（Position / Normal / Color / UV / UV2 / AnimBlend の順）。",
                          "Add UV2 and AnimBlend to the Renderer's Custom Vertex Streams (order: Position / Normal / Color / UV / UV2 / AnimBlend)."),
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(8);

            // ===== Emission =====
            NataneToonInspectorComponents.DrawGroupHeader(L("エミッション", "Emission"));
            MaterialProperty emission = FindProperty("_EmissionEnabled", properties, false);
            if (emission != null)
            {
                materialEditor.ShaderProperty(emission,
                    new GUIContent(L("エミッションを有効化", "Enable Emission")));
                if (emission.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    MaterialProperty emissionMap = FindProperty("_EmissionMap", properties, false);
                    MaterialProperty emissionColor = FindProperty("_EmissionColor", properties, false);
                    if (emissionMap != null)
                    {
                        materialEditor.TexturePropertySingleLine(
                            new GUIContent(L("エミッションマップ", "Emission Map")),
                            emissionMap, emissionColor);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(8);

            // ===== Camera Fade =====
            NataneToonInspectorComponents.DrawGroupHeader(L("カメラフェード", "Camera Fade"));
            MaterialProperty cameraFade = FindProperty("_CameraFadeEnabled", properties, false);
            if (cameraFade != null)
            {
                materialEditor.ShaderProperty(cameraFade,
                    new GUIContent(L("カメラフェードを有効化", "Enable Camera Fade"),
                        L("カメラ至近距離でフェードアウトし、ニアクリップでのパッと消える現象を防ぎます",
                          "Fades out near the camera to avoid near-plane popping")));
                if (cameraFade.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    DrawIfFound(materialEditor, properties, "_CameraFadeNear", L("フェード開始距離", "Fade Near"));
                    DrawIfFound(materialEditor, properties, "_CameraFadeFar", L("フェード完了距離", "Fade Far"));
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(12);

            // ===== Advanced =====
            NataneToonInspectorComponents.DrawGroupHeader(L("出力", "Output"));
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            if (material != null && material.HasProperty("_BlendMode"))
            {
                ApplyBlendMode(material, (ParticleBlendMode)(int)material.GetFloat("_BlendMode"));
            }
        }

        private static void DrawIfFound(MaterialEditor materialEditor, MaterialProperty[] properties, string name, string label)
        {
            MaterialProperty prop = FindProperty(name, properties, false);
            if (prop != null)
            {
                materialEditor.ShaderProperty(prop, label);
            }
        }

        private static void ApplyBlendMode(Material material, ParticleBlendMode mode)
        {
            if (material == null) return;

            switch (mode)
            {
                case ParticleBlendMode.Alpha:
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_BlendOp", (float)BlendOp.Add);
                    break;
                case ParticleBlendMode.Additive:
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.One);
                    material.SetFloat("_BlendOp", (float)BlendOp.Add);
                    break;
                case ParticleBlendMode.Premultiplied:
                    material.SetFloat("_SrcBlend", (float)BlendMode.One);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_BlendOp", (float)BlendOp.Add);
                    break;
                case ParticleBlendMode.Multiply:
                    material.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
                    material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    material.SetFloat("_BlendOp", (float)BlendOp.Add);
                    break;
            }
        }
    }
}
