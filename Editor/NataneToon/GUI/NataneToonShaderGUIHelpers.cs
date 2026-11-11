using UnityEditor;
using UnityEngine;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Helper methods for NataneToonShaderGUI.
    /// </summary>
    public static class NataneToonShaderGUIHelpers
    {
        private const float GRADIENT_MODE_THRESHOLD = 0.5f;
        private const float PBR_LIKE_MODE_THRESHOLD = 2.5f;

        public delegate bool DrawToggleDelegate(string keyword, string propertyName, string label);
        public delegate void DrawPropertyDelegate(string propertyName, string label);
        public delegate bool DrawHelpToggleDelegate(string sectionKey, string helpText, MessageType messageType = MessageType.Info);
        public delegate void SaveFoldoutStatesDelegate();
        public delegate MaterialProperty FindPropertyDelegate(string propertyName, MaterialProperty[] properties, bool propertyIsMandatory);

        public static void DrawShadingSection(
            ref bool showShading,
            Material material,
            MaterialProperty[] properties,
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle,
            SaveFoldoutStatesDelegate saveFoldoutStates,
            FindPropertyDelegate findProperty)
        {
            _ = showShading;
            _ = saveFoldoutStates;
            DrawShadingSectionContent(material, properties, drawToggle, drawProperty, drawHelpToggle, findProperty);
        }

        public static void DrawShadingSectionContent(
            Material material,
            MaterialProperty[] properties,
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle,
            FindPropertyDelegate findProperty,
            System.Func<string, bool> isSectionAvailable = null)
        {
            drawHelpToggle(
                "ShadingSection",
                L("Anime-style cel shading with clean and controllable shadow transitions.",
                  "Anime-style cel shading with clean and controllable shadow transitions."),
                MessageType.None);

            EditorGUILayout.Space(5);
            DrawShadingModeControls(properties, drawToggle, drawProperty, drawHelpToggle, findProperty);

            EditorGUILayout.Space(10);
            DrawShadowReceiveMaskControls(drawToggle, drawProperty, drawHelpToggle);

            EditorGUILayout.Space(10);
            drawHelpToggle(
                "LightShadowTabMigration",
                L("AO and dithering settings have been moved to the Light & Shadow tab.",
                  "AO and dithering settings have been moved to the Light & Shadow tab."),
                MessageType.None);

            if (isSectionAvailable == null || isSectionAvailable("SDFMap"))
            {
                EditorGUILayout.Space(10);
                DrawSDFShadowMapControls(material, drawToggle, drawProperty, drawHelpToggle);
            }

            if (isSectionAvailable == null || isSectionAvailable("ShadowShapeRig"))
            {
                EditorGUILayout.Space(10);
                DrawShadowShapeRigControls(material, drawToggle, drawProperty, drawHelpToggle);
            }

            EditorGUILayout.Space(10);
            DrawShadingGradeMapControls(drawToggle, drawProperty, drawHelpToggle);

            EditorGUILayout.Space(10);
            DrawShadowColorTextureControls(drawToggle, drawProperty, drawHelpToggle);
        }

        public static void DrawShadingModeControls(
            MaterialProperty[] properties,
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle,
            FindPropertyDelegate findProperty)
        {
            drawHelpToggle(
                "ShadowSoftnessGuide",
                L("To soften shadows, start with Shadow Blend around 0.3 to 0.5, then adjust Lit Softness if needed.",
                  "To soften shadows, start with Shadow Blend around 0.3 to 0.5, then adjust Lit Softness if needed."),
                MessageType.None);

            bool useRamp = drawToggle("_USE_RAMP", "_UseRamp", L("Use Ramp Texture", "Use Ramp Texture"));
            if (useRamp)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("Ramp Texture Settings", "Ramp Texture Settings"), EditorStyles.boldLabel);
                drawProperty("_RampTex", L("Ramp Texture", "Ramp Texture"));
                drawHelpToggle(
                    "RampTexture",
                    L("The ramp texture should run from dark on the left to bright on the right.",
                      "The ramp texture should run from dark on the left to bright on the right."),
                    MessageType.Info);
            }
            else
            {
                MaterialProperty shadingModeProp = findProperty("_ShadingMode", properties, false);
                float shadingModeValue = shadingModeProp != null ? shadingModeProp.floatValue : 0f;
                bool isGradientMode = IsGradientMode(shadingModeValue);
                bool isPbrLikeMode = IsPbrLikeMode(shadingModeValue);

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField(L("見た目の方向性", "Shading Style"), EditorStyles.boldLabel);
                if (shadingModeProp != null)
                {
                    DrawUserFacingShadingModePopup(shadingModeProp);
                }
                else
                {
                    drawProperty("_ShadingMode", L("モード", "Mode"));
                }

                drawHelpToggle(
                    "ShadingMode",
                    L("Toon はくっきりしたアニメ調、Gradient はやわらかい陰影、PBR-Like は立体感を少し強めた見た目です。",
                      "Toon gives stepped cel shading. Gradient gives softer transitions. PBR-Like adds a bit more volume."),
                    MessageType.None);

                EditorGUILayout.Space(5);
                if (isGradientMode)
                {
                    DrawGradientModeSettings(drawProperty, drawHelpToggle);
                }
                else if (isPbrLikeMode)
                {
                    drawHelpToggle(
                        "PbrLikeMode",
                        L("PBR-Like はトゥーン感を残しつつ立体感を強める方向です。影の調整は通常のトゥーン設定で行えます。",
                          "PBR-Like keeps the toon workflow but pushes the lighting toward stronger volume. Use the regular toon controls below to tune shadows."),
                        MessageType.None);
                    DrawToonModeSettings(drawProperty, drawHelpToggle, drawToggle);
                }
                else
                {
                    DrawToonModeSettings(drawProperty, drawHelpToggle, drawToggle);
                }
            }

            EditorGUILayout.Space(5);
            drawProperty("_ShadowHueShift", L("Shadow Hue Shift", "Shadow Hue Shift"));
            drawProperty("_ShadowSaturation", L("Shadow Saturation", "Shadow Saturation"));
            drawHelpToggle(
                "ShadowHSVShift",
                L("Adjust shadow hue and saturation in HSV space.",
                  "Adjust shadow hue and saturation in HSV space."),
                MessageType.Info);

            EditorGUILayout.Space(5);
            drawProperty("_ShadowOffset", L("Shadow Offset", "Shadow Offset"));
            drawHelpToggle(
                "ShadowOffset",
                L("Positive values brighten the shadow boundary. Negative values deepen it.",
                  "Positive values brighten the shadow boundary. Negative values deepen it."),
                MessageType.Info);

            drawProperty("_WrapAmount", L("Wrap Amount", "Wrap Amount"));
            drawHelpToggle(
                "WrapAmount",
                L("Controls how much light wraps around the form.",
                  "Controls how much light wraps around the form."),
                MessageType.Info);

            EditorGUILayout.Space(5);
            drawProperty("_LitSoftness", L("Lit Softness", "Lit Softness"));
            drawHelpToggle(
                "LitSoftness",
                L("Softens transitions on lit areas.",
                  "Softens transitions on lit areas."),
                MessageType.Info);

            EditorGUILayout.Space(5);
            DrawBlendParameter(
                "_ShadowBlend",
                "ShadowBlend",
                L("Shadow Blend", "Shadow Blend"),
                L("Higher values soften and widen the shadow transition.",
                  "Higher values soften and widen the shadow transition."),
                drawProperty,
                drawHelpToggle);

            MaterialProperty shadowBlendProp = findProperty("_ShadowBlend", properties, false);
            if (shadowBlendProp != null && shadowBlendProp.floatValue > 0.7f)
            {
                drawHelpToggle(
                    "ShadowBlendHigh",
                    L("Shadow Blend is set high. Check other softness values if the result feels too soft.",
                      "Shadow Blend is set high. Check other softness values if the result feels too soft."),
                    MessageType.Info);
            }

            EditorGUILayout.Space(10);
            bool vertexColorShadow = drawToggle(
                "_VERTEX_COLOR_SHADOW",
                "_VertexColorShadow",
                L("Vertex Color Shadow Threshold", "Vertex Color Shadow Threshold"));
            if (vertexColorShadow)
            {
                EditorGUI.indentLevel++;
                drawProperty("_VCShadowThreshold", L("Shadow Threshold", "Shadow Threshold"));
                drawProperty("_VCShadowPush", L("Shadow Push", "Shadow Push"));
                drawHelpToggle(
                    "VertexColorShadow",
                    L("Uses vertex color R to offset the shadow threshold per area.",
                      "Uses vertex color R to offset the shadow threshold per area."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }

        public static void DrawBlendParameter(
            string propertyName,
            string sectionKey,
            string label,
            string helpText,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            drawProperty(propertyName, label);
            drawHelpToggle(sectionKey, helpText, MessageType.Info);
        }

        public static void DrawGradientModeSettings(
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            EditorGUILayout.LabelField(L("Gradient Settings", "Gradient Settings"), EditorStyles.boldLabel);
            drawProperty("_ShadowColor", L("Shadow Color", "Shadow Color"));
            drawProperty("_ShadingGradientWidth", L("Gradient Width", "Gradient Width"));
            drawHelpToggle(
                "ShadingGradientWidth",
                L("Controls how soft the light-to-shadow transition is.",
                  "Controls how soft the light-to-shadow transition is."),
                MessageType.Info);
        }

        private static bool IsGradientMode(float shadingModeValue)
        {
            return shadingModeValue >= GRADIENT_MODE_THRESHOLD &&
                   shadingModeValue < PBR_LIKE_MODE_THRESHOLD;
        }

        private static bool IsPbrLikeMode(float shadingModeValue)
        {
            return shadingModeValue >= PBR_LIKE_MODE_THRESHOLD;
        }

        private static void DrawUserFacingShadingModePopup(MaterialProperty shadingModeProp)
        {
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = shadingModeProp.hasMixedValue;
            EditorGUI.BeginChangeCheck();

            int currentIndex = GetUserFacingShadingModeIndex(shadingModeProp.floatValue);
            int nextIndex = EditorGUILayout.Popup(
                L("モード", "Mode"),
                currentIndex,
                GetUserFacingShadingModeLabels());

            EditorGUI.showMixedValue = previousMixedValue;

            if (EditorGUI.EndChangeCheck())
            {
                shadingModeProp.floatValue = GetShadingModeValueFromUserFacingIndex(nextIndex);
            }
        }

        private static int GetUserFacingShadingModeIndex(float shadingModeValue)
        {
            if (IsPbrLikeMode(shadingModeValue))
            {
                return 2;
            }

            if (IsGradientMode(shadingModeValue))
            {
                return 1;
            }

            return 0;
        }

        private static float GetShadingModeValueFromUserFacingIndex(int index)
        {
            switch (index)
            {
                case 1:
                    return 1f;
                case 2:
                    return 3f;
                default:
                    return 0f;
            }
        }

        private static string[] GetUserFacingShadingModeLabels()
        {
            return new[]
            {
                L("トゥーン", "Toon"),
                L("グラデーション", "Gradient"),
                L("PBRライク", "PBR-Like")
            };
        }

        public static void DrawMultiToneShadowSettings(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useMultiShadow = drawToggle(
                "_USE_MULTI_SHADOW",
                "_UseMultiShadow",
                L("Use Multi-Tone Shadow", "Use Multi-Tone Shadow"));
            if (!useMultiShadow)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("Multi-Tone Shadow Settings", "Multi-Tone Shadow Settings"), EditorStyles.boldLabel);

            drawProperty("_Shadow2ndColor", L("Shadow Color (2nd)", "Shadow Color (2nd)"));
            drawProperty("_Shadow2ndBorder", L("2nd Border", "2nd Border"));
            drawHelpToggle(
                "Shadow2ndBorder",
                L("Sets where the second shadow tone begins.",
                  "Sets where the second shadow tone begins."),
                MessageType.None);

            EditorGUILayout.Space();
            drawProperty("_Shadow3rdColor", L("Shadow Color (3rd)", "Shadow Color (3rd)"));
            drawProperty("_Shadow3rdBorder", L("3rd Border", "3rd Border"));
            drawHelpToggle(
                "Shadow3rdBorder",
                L("Sets where the deepest shadow tone begins.",
                  "Sets where the deepest shadow tone begins."),
                MessageType.None);

            EditorGUILayout.Space();
            drawHelpToggle(
                "MultiShadowUsage",
                L("Use descending border values: 1st > 2nd > 3rd.",
                  "Use descending border values: 1st > 2nd > 3rd."),
                MessageType.Info);
        }

        public static void DrawToonModeSettings(
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle,
            DrawToggleDelegate drawToggle)
        {
            EditorGUILayout.LabelField(L("Cel Shading Settings", "Cel Shading Settings"), EditorStyles.boldLabel);
            drawProperty("_ShadowColor", L("Shadow Color (1st)", "Shadow Color (1st)"));

            EditorGUILayout.Space();
            DrawMultiToneShadowSettings(drawToggle, drawProperty, drawHelpToggle);

            EditorGUILayout.Space();
            drawProperty("_ShadowSteps", L("Shadow Steps", "Shadow Steps"));
            drawHelpToggle(
                "ShadowSteps",
                L("Recommended values are usually 2 to 3 steps.",
                  "Recommended values are usually 2 to 3 steps."),
                MessageType.Info);

            drawProperty("_ShadowSharpness", L("Shadow Sharpness", "Shadow Sharpness"));
            drawHelpToggle(
                "ShadowSharpness",
                L("Lower values sharpen the border. Higher values soften it.",
                  "Lower values sharpen the border. Higher values soften it."),
                MessageType.Info);

            drawProperty("_StepBorderSmooth", L("Step Border Smoothing", "Step Border Smoothing"));
            drawHelpToggle(
                "StepBorderSmooth",
                L("Smooths the borders between multi-tone shadow steps.",
                  "Smooths the borders between multi-tone shadow steps."),
                MessageType.Info);
        }

        public static void DrawShadowReceiveMaskControls(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useShadowReceiveMask = drawToggle(
                "_SHADOW_RECEIVE_MASK",
                "_UseShadowReceiveMask",
                L("Use Shadow Receive Mask", "Use Shadow Receive Mask"));
            if (!useShadowReceiveMask)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("Shadow Receive Mask Settings", "Shadow Receive Mask Settings"), EditorStyles.boldLabel);
            drawProperty("_ShadowReceiveMask", L("Shadow Receive Mask", "Shadow Receive Mask"));
            drawHelpToggle(
                "ShadowReceiveMask",
                L("White areas receive less shadow. Black areas receive full shadow.",
                  "White areas receive less shadow. Black areas receive full shadow."),
                MessageType.Info);
        }

        public static void DrawAmbientOcclusionControls(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useAO = drawToggle("_USE_AO", "_UseAO", L("Use Ambient Occlusion (AO)", "Use Ambient Occlusion (AO)"));
            if (!useAO)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("AO Settings", "AO Settings"), EditorStyles.boldLabel);
            drawProperty("_AOMap", L("AO Map", "AO Map"));
            drawProperty("_AOIntensity", L("AO Intensity", "AO Intensity"));
            drawHelpToggle(
                "AmbientOcclusion",
                L("Darkens creases and recessed areas using an AO map.",
                  "Darkens creases and recessed areas using an AO map."),
                MessageType.Info);
        }

        public static void DrawDitheringControls(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useDithering = drawToggle("_USE_DITHERING", "_UseDithering", L("Use Dithering (Halftone)", "Use Dithering (Halftone)"));
            if (!useDithering)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("Dithering Settings", "Dithering Settings"), EditorStyles.boldLabel);
            drawProperty("_DitheringScale", L("Dithering Scale", "Dithering Scale"));
            drawProperty("_DitheringStrength", L("Dithering Strength", "Dithering Strength"));
            drawHelpToggle(
                "Dithering",
                L("Adds a halftone-like pattern to shadow transitions.",
                  "Adds a halftone-like pattern to shadow transitions."),
                MessageType.Info);
        }

        /// <summary>
        /// Shadow Shape Rig (NPR2026 P1) のスロットUI。
        ///
        /// シェーダー側は 1 スロット = Vector 2 本にパックしてある（CBUFFER を節約するため）が、
        /// 生の Vector4 のまま出すと「x が中心 X で z が半径 X」という対応を暗記させることになる。
        /// ラベル付きの 2 行へ分解して描く。
        /// </summary>
        public static void DrawShadowShapeRigControls(
            Material material,
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool enabled = drawToggle("_SHADOW_SHAPE_RIG", "_ShadowShapeRig",
                L("影シェイプリグを有効化", "Enable Shadow Shape Rig"));

            drawHelpToggle(
                "ShadowShapeRig",
                L("影の境界を楕円で局所的に押し出す／へこませる機能です。" +
                  "半径が 0 のスロットは無効として扱われ、コストもほぼかかりません。",
                  "Locally pushes the shading boundary out or in with ellipses. " +
                  "A slot with zero radius is treated as unused and costs almost nothing."),
                MessageType.Info);

            if (!enabled) return;

            EditorGUI.indentLevel++;

            for (int slot = 0; slot < 4; slot++)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    L($"リグ {slot}", $"Rig {slot}"), EditorStyles.boldLabel);

                DrawRigSlot(material, slot);
            }

            EditorGUILayout.Space(6);
            drawProperty("_ShadowRigFollowScale", L("ライト追従の移動量", "Light Follow Scale"));
            drawProperty("_ShadowRigMask", L("適用マスク (R)", "Rig Mask (R)"));
            drawProperty("_ShadowRigMaskStrength", L("マスクの効き", "Mask Strength"));

            EditorGUI.indentLevel--;
        }

        private static void DrawRigSlot(Material material, int slot)
        {
            string paramsName = "_ShadowRigParams" + slot;
            string shapeName = "_ShadowRigShape" + slot;

            if (material == null || !material.HasProperty(paramsName) || !material.HasProperty(shapeName))
            {
                return;
            }

            Vector4 prm = material.GetVector(paramsName);
            Vector4 shape = material.GetVector(shapeName);

            EditorGUI.BeginChangeCheck();

            var center = EditorGUILayout.Vector2Field(L("中心 (UV)", "Center (UV)"), new Vector2(prm.x, prm.y));
            var radius = EditorGUILayout.Vector2Field(L("半径 X / Y", "Radius X / Y"), new Vector2(prm.z, prm.w));
            float rotation = EditorGUILayout.Slider(L("回転 (度)", "Rotation (deg)"), shape.x, 0f, 360f);
            float strength = EditorGUILayout.Slider(L("強さ（負で影を増やす）", "Strength (negative grows the shadow)"), shape.y, -1f, 1f);
            float falloff = EditorGUILayout.Slider(L("ふちの減衰", "Falloff"), shape.z, 0f, 1f);
            float lightFollow = EditorGUILayout.Slider(L("ライト追従", "Light Follow"), shape.w, -1f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Edit Shadow Rig Slot");
                material.SetVector(paramsName, new Vector4(center.x, center.y, Mathf.Max(radius.x, 0f), Mathf.Max(radius.y, 0f)));
                material.SetVector(shapeName, new Vector4(rotation, strength, falloff, lightFollow));
                EditorUtility.SetDirty(material);
            }

            if (radius.x <= 0f || radius.y <= 0f)
            {
                EditorGUILayout.LabelField(
                    L("  （半径が 0 のため無効）", "  (disabled: radius is zero)"),
                    EditorStyles.miniLabel);
            }
        }

        public static void DrawSDFShadowMapControls(
            Material material,
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useSDFMap = drawToggle("_SDF_MAP", "_UseSDFMap", L("Use SDF Shadow Map", "Use SDF Shadow Map"));
            EditorGUILayout.Space(4);

            // この生成器が作るのは「マスクの符号付き距離場」で、回転追従が要求する
            // 「ライト角のフィールド」ではない。両者は意味が違うので、回転追従が有効な
            // マテリアルでは Map Generator のベイクへ誘導する。
            bool rotationTracking = NataneToonSdfAutoGenerator.IsRotationTrackingMaterial(material);

            if (GUILayout.Button(L("Mask SDF を生成（回転追従なし）", "Generate Mask SDF (no rotation tracking)"), GUILayout.Height(22)))
            {
                bool generated = NataneToonSdfAutoGenerator.TryGenerateAndAssign(material, out string message);
                if (generated)
                {
                    useSDFMap = true;
                }

                EditorUtility.DisplayDialog(
                    generated ? L("Mask SDF 生成", "Mask SDF Generation") : L("Mask SDF 生成に失敗", "Mask SDF Generation Failed"),
                    message,
                    L("閉じる", "Close"));
                GUI.changed = true;
            }

            EditorGUILayout.HelpBox(
                L("Shadow Receive Mask を優先し、なければ Main Texture の alpha / グレースケールから" +
                  "「マスクの符号付き距離場」を生成します。ライト角の情報は含まれないため、" +
                  "Face SDF Rotation と組み合わせても影の遷移順序は正しくなりません。",
                  "Generates the signed distance field of a mask, using Shadow Receive Mask first and " +
                  "falling back to Main Texture alpha or grayscale. It carries no light-angle information, " +
                  "so it cannot drive Face SDF Rotation correctly."),
                MessageType.None);

            if (rotationTracking)
            {
                EditorGUILayout.HelpBox(
                    L("Face SDF Rotation が有効です。回転追従には Map Generator の" +
                      "「顔SDF影マップ」ベイクを使ってください。上のボタンで作れるマスク SDF では" +
                      "影の遷移順序が破綻します。",
                      "Face SDF Rotation is enabled. Use the Map Generator's \"Face SDF Shadow Map\" bake " +
                      "for rotation tracking — the mask SDF above will make the shadow transition in the " +
                      "wrong order."),
                    MessageType.Warning);

                if (GUILayout.Button(L("Map Generator を開く", "Open Map Generator"), GUILayout.Height(20)))
                {
                    EditorApplication.ExecuteMenuItem("Tools/MapGenerator/Map Generator Window");
                }
            }

            if (useSDFMap)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("SDF Shadow Map Settings", "SDF Shadow Map Settings"), EditorStyles.boldLabel);
                drawProperty("_SDFMap", L("SDF Map", "SDF Map"));
                drawProperty("_SDFIntensity", L("SDF Intensity", "SDF Intensity"));
                drawProperty("_SDFSoftness", L("SDF Softness", "SDF Softness"));
                drawProperty("_SDFOffset", L("SDF Offset", "SDF Offset"));
                drawHelpToggle(
                    "SDFShadowMap",
                    L("Controls shadow placement using an SDF texture.",
                      "Controls shadow placement using an SDF texture."),
                    MessageType.Info);
            }

            bool faceSDFRotation = drawToggle(
                "_FACE_SDF_ROTATION",
                "_FaceSDFRotation",
                L("Enable Face SDF Rotation Tracking", "Enable Face SDF Rotation Tracking"));
            if (!faceSDFRotation)
            {
                return;
            }

            EditorGUI.indentLevel++;
            drawProperty("_FaceForwardDirection", L("Face Forward Direction", "Face Forward Direction"));
            drawProperty("_FaceRightDirection", L("Face Right Direction", "Face Right Direction"));
            drawHelpToggle(
                "FaceSDFRotation",
                L("Lets face SDF shadows rotate with light direction.",
                  "Lets face SDF shadows rotate with light direction."),
                MessageType.Info);
            EditorGUI.indentLevel--;
        }

        public static void DrawShadingGradeMapControls(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useShadingGradeMap = drawToggle(
                "_SHADING_GRADE_MAP",
                "_UseGradeMap",
                L("Use Shading Grade Map", "Use Shading Grade Map"));
            if (!useShadingGradeMap)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("Shading Grade Map Settings", "Shading Grade Map Settings"), EditorStyles.boldLabel);
            drawProperty("_ShadingGradeMap", L("シェーディンググレードマップ", "Shading Grade Map"));
            drawProperty("_ShadingGradeScale", L("Grade Scale", "Grade Scale"));
            drawHelpToggle(
                "ShadingGradeMap",
                L("Adjusts shadow strength locally using a texture map.",
                  "Adjusts shadow strength locally using a texture map."),
                MessageType.Info);
        }

        public static void DrawPCSSControls(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool usePCSS = drawToggle("_PCSS", "_UsePCSS", L("Use PCSS Soft Shadows", "Use PCSS Soft Shadows"));
            if (!usePCSS)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("PCSS Settings", "PCSS Settings"), EditorStyles.boldLabel);
            drawProperty("_PCSSLightSize", L("Light Size", "Light Size"));
            drawProperty("_PCSSSoftness", L("Softness", "Softness"));
            drawProperty("_PCSSBlockerSearchRadius", L("Blocker Search Radius", "Blocker Search Radius"));
            drawProperty("_PCSSMinFilterRadius", L("Min Filter Radius", "Min Filter Radius"));
            drawProperty("_PCSSMaxFilterRadius", L("Max Filter Radius", "Max Filter Radius"));
            drawProperty("_PCSSSampleCount", L("Sample Quality", "Sample Quality"));

            EditorGUILayout.Space(5);
            drawProperty("_PCSSBlendMode", L("Blend Mode", "Blend Mode"));
            drawProperty("_PCSSBlend", L("Blend", "Blend"));
            drawProperty("_PCSSBlur", L("Blur", "Blur"));
            drawHelpToggle(
                "PCSS",
                L("PCSS adds softer, distance-aware shadows for directional lights.",
                  "PCSS adds softer, distance-aware shadows for directional lights."),
                MessageType.Info);
        }

        public static void DrawShadowColorTextureControls(
            DrawToggleDelegate drawToggle,
            DrawPropertyDelegate drawProperty,
            DrawHelpToggleDelegate drawHelpToggle)
        {
            bool useShadowColorTex = drawToggle(
                "_SHADOW_COLOR_TEX",
                "_UseShadowColorTex",
                L("Use Shadow Color Texture", "Use Shadow Color Texture"));
            if (!useShadowColorTex)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("Shadow Color Texture Settings", "Shadow Color Texture Settings"), EditorStyles.boldLabel);
            drawProperty("_ShadowColorTex", L("影カラーテクスチャ", "Shadow Color Texture"));
            drawProperty("_ShadowColorTexStrength", L("Apply Strength", "Apply Strength"));
            drawHelpToggle(
                "ShadowColorTexture",
                L("Uses a texture to control shadow colors. Apply Strength controls how strongly the texture affects the result.",
                  "Uses a texture to control shadow colors. Apply Strength controls how strongly the texture affects the result."),
                MessageType.Info);
        }
    }
}
