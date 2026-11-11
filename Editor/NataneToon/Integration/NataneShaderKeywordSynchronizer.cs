using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// シェーダーリインポート時にNataneマテリアルのキーワード状態を自動修正する。
    /// [Toggle(_KEYWORD)] プロパティの値とキーワード状態のズレ（reimport後の古いキーワード残留）を防止。
    /// </summary>
    public sealed class NataneShaderKeywordSynchronizer : AssetPostprocessor
    {
        /// <summary>
        /// Toggle プロパティ名 → シェーダーキーワード のマッピング。
        /// NataneToonShaderGUI.ValidateAndFixKeywords() と同一のリストを維持すること。
        /// </summary>
        public static readonly (string propertyName, string keyword)[] KeywordMappings =
        {
            // Main Texture Animation
            ("_MainTexAnimation", "_MAIN_TEX_ANIMATION"),

            // Makeup Textures
            ("_Use2ndTexture", "_2ND_TEXTURE"),
            ("_Use3rdTexture", "_3RD_TEXTURE"),
            ("_Use4thTexture", "_4TH_TEXTURE"),
            ("_Use5thTexture", "_5TH_TEXTURE"),

            // Shading
            ("_UseRamp", "_USE_RAMP"),
            ("_UseMultiShadow", "_USE_MULTI_SHADOW"),
            ("_UseShadowReceiveMask", "_SHADOW_RECEIVE_MASK"),
            ("_UseAO", "_USE_AO"),
            ("_UseDithering", "_USE_DITHERING"),
            ("_UseSDFMap", "_SDF_MAP"),
            ("_UseGradeMap", "_SHADING_GRADE_MAP"),

            // Lighting
            ("_SoftLightingMode", "_SOFT_LIGHTING_MODE"),
            ("_UseLightVolume", "_USE_LIGHT_VOLUME"),
            ("_LightVolumeSpecular", "_LIGHT_VOLUME_SPECULAR"),
            ("_UsePixelVertexLights", "_PIXEL_VERTEX_LIGHTS"),

            // Specular
            ("_Specular", "_SPECULAR"),
            ("_SpecularDither", "_SPECULAR_DITHER"),
            ("_HairSpecular", "_HAIR_SPECULAR"),
            ("_UseHairSpecMask", "_HAIR_SPEC_MASK"),
            ("_UseHairSpecShiftTex", "_HAIR_SPEC_SHIFT_TEX"),
            ("_QuestLite", "_QUEST_LITE"),

            // Rim Light
            ("_RimLight", "_RIM_LIGHT"),
            ("_RimLight2", "_RIM_LIGHT_2"),
            ("_OffsetRimLight", "_OFFSET_RIM_LIGHT"),

            // SSS
            ("_SSS", "_SSS"),

            // MatCap
            ("_MatCap", "_MATCAP"),
            ("_MatCap2", "_MATCAP_2"),
            ("_MatCap3", "_MATCAP_3"),

            // Glitter
            ("_Glitter", "_GLITTER"),
            ("_GlintsAdvanced", "_GLINTS_ADVANCED"),

            // Reflection
            ("_Reflection", "_REFLECTION"),

            // Iridescence
            ("_Iridescence", "_IRIDESCENCE"),

            // Environmental Rim
            ("_EnvRim", "_ENV_RIM"),

            // Outline
            ("_Outline", "_OUTLINE"),
            ("_OutlineTextureColor", "_OUTLINE_TEXTURE_COLOR"),
            ("_SmoothNormal", "_SMOOTH_NORMAL"),

            // Emission
            ("_Emission", "_EMISSION"),

            // Normal Map
            ("_UseNormalMap", "_NORMALMAP"),

            // Virtual Expression
            ("_Dissolve", "_DISSOLVE"),
            ("_UseAlphaMask", "_ALPHA_MASK"),
            ("_HueShiftEnable", "_HUE_SHIFT"),

            // Parallax
            ("_Parallax", "_PARALLAX"),

            // Refraction
            ("_Refraction", "_REFRACTION"),

            // AudioLink
            ("_AudioLink", "_AUDIOLINK"),

            // Distance Fade
            ("_DistanceFade", "_DISTANCE_FADE"),

            // Vertex Animation
            ("_VertexAnimation", "_VERTEX_ANIMATION"),

            // VAT
            ("_VAT", "_VAT"),
            ("_VATNormal", "_VAT_NORMAL"),

            // Water Drip
            ("_WaterDrip", "_WATER_DRIP"),

            // Smear
            ("_Smear", "_SMEAR"),

            // Hologram / Glitch
            ("_Hologram", "_HOLOGRAM"),
            ("_Glitch", "_GLITCH"),
            ("_GlitchStretch", "_GLITCH_STRETCH"),
            ("_UseHologramNoise", "_HOLOGRAM_NOISE"),

            // Illustration Style
            ("_UseColorQuantize", "_COLOR_QUANTIZE"),
            ("_UseLUT3D", "_LUT_3D"),
            ("_UseHatching", "_HATCHING"),
            ("_UseWatercolor", "_WATERCOLOR"),
            ("_UseSoftFilter", "_SOFT_FILTER"),
            ("_UseKuwahara", "_KUWAHARA_FILTER"),
            ("_UseScreenEdge", "_SCREEN_EDGE"),
            ("_UseColorBleeding", "_COLOR_BLEEDING"),
            ("_UseChromaticAberration", "_CHROMATIC_ABERRATION"),
            ("_UseHandDrawnOutline", "_OUTLINE_HAND_DRAWN"),

            // Decal
            ("_Decal", "_DECAL"),

            // Backface Texture
            ("_BackfaceTexture", "_BACKFACE_TEXTURE"),

            // Video Texture
            ("_VideoTexture", "_VIDEO_TEXTURE"),

            // LTCGI
            ("_LTCGI", "_LTCGI"),

            // Screen Tone
            ("_ScreenTone", "_SCREEN_TONE"),

            // Gradient Base Color
            ("_GradientBaseColor", "_GRADIENT_BASE_COLOR"),

            // Blue Noise Dither
            ("_BlueNoiseDither", "_BLUE_NOISE_DITHER"),

            // Dithering Alpha
            ("_DitheringAlpha", "_DITHERING_ALPHA"),
            ("_HashedAlpha", "_HASHED_ALPHA"),

            // PCSS Soft Shadow
            ("_UsePCSS", "_PCSS"),

            // Outline sub-keywords
            ("_UseOutlineMask", "_OUTLINE_MASK"),
            ("_UseOutlineWidthMap", "_OUTLINE_WIDTH_MAP"),
            ("_OutlineMultiColor", "_OUTLINE_MULTI_COLOR"),

            // Halftone Shadow
            ("_HalftoneShadow", "_HALFTONE_SHADOW"),

            // Shadow Edge Noise
            ("_ShadowEdgeNoise", "_SHADOW_EDGE_NOISE"),

            // Cast Shadow Color
            ("_CastShadowColorEnable", "_CAST_SHADOW_COLOR"),

            // Light Snap
            ("_LightSnap", "_LIGHT_SNAP"),

            // Procedural MatCap
            ("_ProceduralMatCap", "_PROCEDURAL_MATCAP"),

            // Fake Reflection
            ("_FakeReflection", "_FAKE_REFLECTION"),

            // Perspective Flat
            ("_PerspectiveFlat", "_PERSPECTIVE_FLAT"),
            ("_FaceOrtho", "_FACE_ORTHO"),
            ("_UseFaceOrthoMask", "_FACE_ORTHO_MASK"),
            ("_MirrorTexture", "_MIRROR_TEXTURE"),

            // Height Fade / Intersection Fade
            ("_HeightFade", "_HEIGHT_FADE"),
            ("_IntersectionFade", "_INTERSECTION_FADE"),

            // Tessellation
            ("_Tessellation", "_TESSELLATION"),
            ("_TessDisplacement", "_TESS_DISPLACEMENT"),

            // Depth Color Fade
            ("_DepthColorFade", "_DEPTH_COLOR_FADE"),

            // Mirror / Camera Control
            ("_MirrorControl", "_MIRROR_CONTROL"),

            // Angel Ring
            ("_AngelRing", "_ANGEL_RING"),

            // Detail Map
            ("_DetailMap", "_DETAIL_MAP"),

            // Procedural AO
            ("_ProceduralAO", "_PROCEDURAL_AO"),

            // Normal Warp
            ("_NormalWarp", "_NORMAL_WARP"),

            // Specular Anti-Aliasing
            ("_SpecularAA", "_SPECULAR_AA"),

            // Vertex Color Shadow
            ("_VertexColorShadow", "_VERTEX_COLOR_SHADOW"),

            // Face SDF Rotation
            ("_FaceSDFRotation", "_FACE_SDF_ROTATION"),

            // Sheen
            ("_Sheen", "_SHEEN"),

            // SSS LUT
            ("_SSSLUT", "_SSS_LUT"),

            // Surface Cover
            ("_SurfaceCover", "_SURFACE_COVER"),

            // Triplanar
            ("_Triplanar", "_TRIPLANAR"),

            // Height Fog
            ("_HeightFog", "_HEIGHT_FOG"),

            // Fur (Fur variant only)
            ("_Fur", "_FUR"),

            // Expression Effects (v1.6.0 batch 1)
            ("_LineBoil", "_LINE_BOIL"),
            ("_ShapedHighlight", "_SHAPED_HIGHLIGHT"),
            ("_Topographic", "_TOPOGRAPHIC"),
            ("_FXModulator", "_FX_MODULATOR"),

            // Shadow Shape Rig (NPR2026 P1)
            // ここが単一の真実。ビルド最適化・VariantStripper・
            // NataneToonBuildSettings.hlsl の #undef ガードは全てこの行から派生する。
            ("_ShadowShapeRig", "_SHADOW_SHAPE_RIG"),

            // Expression Effects (v1.6.0 batch 2)
            ("_Lenticular", "_LENTICULAR"),
            ("_Caustics", "_CAUSTICS"),
            ("_PixelArt", "_PIXEL_ART"),

            // Expression Effects (影の玉ボケ)
            ("_ShadowBokeh", "_SHADOW_BOKEH"),
        };

        // SaveAssets による再インポート→再同期の無限ループを防止
        private static bool _isSynchronizing;

        // 起動/スクリプトリロード毎に1回だけ実行するためのフラグ（EditorPrefsに永続化しない＝ドメインリロード毎に走る）
        private static bool _startupSyncScheduled;

        /// <summary>
        /// エディタ起動/スクリプトリロード時に全 Natane マテリアルのキーワードを自動同期する。
        /// これにより Tools/Natane/Fix All Material Keywords の手動実行を不要にする。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ScheduleStartupSynchronization()
        {
            if (_startupSyncScheduled) return;
            _startupSyncScheduled = true;

            // AssetDatabase が未準備の状態で走らないよう delayCall で1フレーム遅延
            EditorApplication.delayCall += RunStartupSynchronization;
        }

        private static void RunStartupSynchronization()
        {
            // 既にコンパイル中/インポート中なら完了まで待つ
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunStartupSynchronization;
                return;
            }

            SynchronizeAllNataneMaterials();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (_isSynchronizing)
                return;

            // Natane シェーダー or HLSL ファイルがリインポートされた場合 → 全マテリアル同期＆保存
            bool nataneShaderReimported = importedAssets.Any(path =>
                (path.EndsWith(".shader", System.StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".hlsl", System.StringComparison.OrdinalIgnoreCase)) &&
                path.Contains("NataneToon"));

            if (nataneShaderReimported)
            {
                // シェーダーコンパイル完了後に全マテリアルを同期（保存あり）
                EditorApplication.delayCall += SynchronizeAllNataneMaterials;
                return;
            }

            // マテリアルファイルがリインポートされた場合 → 該当マテリアルのみメモリ上で修正
            // SaveAssets は呼ばない（無限ループ防止）。ユーザーがプロジェクト保存時に永続化される。
            var reimportedMaterials = importedAssets
                .Where(path => path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (reimportedMaterials.Length > 0)
            {
                EditorApplication.delayCall += () => SynchronizeReimportedMaterialsInMemory(reimportedMaterials);
            }
        }

        /// <summary>
        /// リインポートされたマテリアルのキーワードをメモリ上で修正する。
        /// ディスクには書き込まない（SaveAssets を呼ばない）ため無限ループが発生しない。
        /// </summary>
        private static void SynchronizeReimportedMaterialsInMemory(string[] materialPaths)
        {
            int fixedCount = 0;

            foreach (string path in materialPaths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                    continue;

                if (!NataneShaderCatalog.IsNataneShader(material.shader.name))
                    continue;

                if (SynchronizeMaterialKeywords(material))
                {
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                Debug.Log($"[NataneToonShader] マテリアルキーワード修正 (in-memory): {fixedCount} マテリアル");
            }
        }

        /// <summary>
        /// プロジェクト内のすべての Natane マテリアルのキーワードを同期する。
        /// アセットインデックスから Natane マテリアルのみを取得し、プロジェクト全体のスキャンを回避する。
        /// </summary>
        [MenuItem("Tools/Natane/Fix All Material Keywords")]
        public static void SynchronizeAllNataneMaterials()
        {
            _isSynchronizing = true;
            try
            {
                NataneAssetIndexService.EnsureLoaded();
                var nataneEntries = NataneAssetIndexService
                    .EnumerateMaterialEntries(e => e.isNataneShader)
                    .ToList();

                int fixedCount = 0;

                foreach (var entry in nataneEntries)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(entry.path);

                    if (material == null || material.shader == null)
                        continue;

                    if (SynchronizeMaterialKeywords(material))
                    {
                        EditorUtility.SetDirty(material);
                        fixedCount++;
                    }
                }

                if (fixedCount > 0)
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[NataneToonShader] キーワード同期完了: {fixedCount}/{nataneEntries.Count} マテリアルを修正しました");
                }
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        /// <summary>
        /// 単一マテリアルのキーワードを同期する。変更があった場合 true を返す。
        /// </summary>
        public static bool SynchronizeMaterialKeywords(Material material)
        {
            if (material == null)
                return false;

            bool anyChanges = false;

            foreach (var mapping in KeywordMappings)
            {
                if (!material.HasProperty(mapping.propertyName))
                    continue;

                float propertyValue = material.GetFloat(mapping.propertyName);
                bool shouldBeEnabled = propertyValue >= 0.5f;
                bool isEnabled = material.IsKeywordEnabled(mapping.keyword);

                // Sync keyword state from property value
                if (shouldBeEnabled != isEnabled)
                {
                    if (shouldBeEnabled)
                        material.EnableKeyword(mapping.keyword);
                    else
                        material.DisableKeyword(mapping.keyword);

                    anyChanges = true;
                }

                // OFF の場合のみプロパティを 0 にクリーンアップ。
                // ON の場合は元の値を維持する（中途半端な値でも意図的な設定の可能性がある）。
                if (!shouldBeEnabled && propertyValue != 0.0f)
                {
                    material.SetFloat(mapping.propertyName, 0.0f);
                    anyChanges = true;
                }
            }

            return anyChanges;
        }
    }

    /// <summary>
    /// Undo の記録イベントを監視して、Natane マテリアルのトグルプロパティ/キーワードが
    /// 変更された瞬間にキーワードを再同期する。
    /// - インスペクタのデバッグモード、アニメーションウィンドウ、外部スクリプトなど、
    ///   Natane 専用 GUI を経由しない変更経路でもズレを即座に修正する。
    /// </summary>
    [InitializeOnLoad]
    internal static class NataneMaterialChangeWatcher
    {
        // 再入防止（EnableKeyword / SetFloat 自体が別の変更通知をトリガしても無限ループしないように）
        private static bool _isApplying;

        static NataneMaterialChangeWatcher()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (_isApplying || modifications == null || modifications.Length == 0)
                return modifications;

            HashSet<Material> touched = null;

            for (int i = 0; i < modifications.Length; i++)
            {
                var cur = modifications[i].currentValue;
                if (cur == null) continue;

                var mat = cur.target as Material;
                if (mat == null || mat.shader == null) continue;

                // Natane シェーダー以外は無視
                if (!NataneShaderCatalog.IsNataneShader(mat.shader.name))
                    continue;

                // material プロパティのパス形式例:
                //   "m_SavedProperties.m_Floats.Array.data[N].second"
                //   "m_ShaderKeywords"
                // どちらもキーワード同期対象として取り扱う。
                string path = cur.propertyPath ?? string.Empty;
                bool relevant =
                    path.Contains("m_Floats") ||
                    path.Contains("m_ShaderKeywords");

                if (!relevant) continue;

                if (touched == null) touched = new HashSet<Material>();
                touched.Add(mat);
            }

            if (touched != null)
            {
                _isApplying = true;
                try
                {
                    foreach (var mat in touched)
                    {
                        if (NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(mat))
                        {
                            EditorUtility.SetDirty(mat);
                        }
                    }
                }
                finally
                {
                    _isApplying = false;
                }
            }

            return modifications;
        }
    }
}
