using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using NataneToon.Editor;
using static NataneToon.Editor.NataneToonLocalization;

/// <summary>
/// Custom shader GUI for Natane Toon Shader
///
/// Provides a user-friendly, organized interface for the NataneToon shader system
/// with support for:
/// - Material presets and sharing
/// - Performance indicators
/// - Makeup texture layers (up to 4 layers with HSV adjustment)
/// - Advanced shading and lighting controls
/// - VRC Light Volumes integration
/// - Multiple surface effects (Specular, Rim Light, SSS, MatCap, etc.)
///
/// The GUI is organized into logical categories with foldout sections for better
/// navigation and reduced clutter.
/// </summary>
public class NataneToonShaderGUI : ShaderGUI
{
    // ===== CONSTANTS =====
    // --- UI Layout Constants ---
    /// <summary>Height of the main tab toolbar</summary>
    private const int TAB_HEIGHT = 30;

    /// <summary>Vertical spacing between sections</summary>
    private const int SECTION_SPACING = 5;

    /// <summary>Threshold for float comparisons (e.g., checking if a toggle is enabled)</summary>
    private const float FLOAT_COMPARISON_THRESHOLD = 0.5f;

    /// <summary>Number of makeup texture layers supported (2nd, 3rd, 4th, 5th)</summary>
    private const int MAKEUP_TEXTURE_COUNT = 4;

    /// <summary>Number of multi-tone shadow levels supported</summary>
    private const int MULTI_SHADOW_LEVELS = 3; // 1st, 2nd, 3rd

    /// <summary>Minimum value to consider a parameter active (avoid floating point issues)</summary>
    private const float MIN_PARAMETER_VALUE = 0.001f;
    private const float PbrLikeShadingModeThreshold = 2.5f;
    private const string DefaultOpaqueShaderName = "Natane/Toon Shader";
    private const string ScreenEdgeSplitShaderName = "Natane/Toon Shader (ScreenEdge Split)";

    private static readonly string[] ScreenEdgeSplitUnsupportedKeywords =
    {
        "_ALPHA_MASK",
        "_HEIGHT_FADE",
        "_INTERSECTION_FADE",
        "_DISTANCE_FADE",
        "_DITHERING_ALPHA",
        "_HASHED_ALPHA"
    };

    private enum LookMode
    {
        Legacy = 0,
        Toon = 1,
        NPR = 2,
        PBR = 3,
        Hybrid = 4
    }

    private enum LilToonMigrationMode
    {
        Unknown = 0,
        ExactCompatibility = 1,
        VisualMatch = 2,
        MinimalSafe = 3
    }

    [System.Flags]
    private enum LilToonParityFlags
    {
        None = 0,
        RimShadeUnsupported = 1 << 0,
        Emission2ndUnsupported = 1 << 1,
        ShadowBorderRangeUnsupported = 1 << 2,
        ShadowMaskTypeUnsupported = 1 << 3,
        BackfaceForceShadowUnsupported = 1 << 4,
        ShadowPostAOUnsupported = 1 << 5,
        MatCapNeedsReview = 1 << 6,
        OutlineNeedsReview = 1 << 7
    }

    private static readonly string[] LookMixerNprKeywords =
    {
        "_COLOR_QUANTIZE",
        "_LUT_3D",
        "_HATCHING",
        "_WATERCOLOR",
        "_SOFT_FILTER",
        "_KUWAHARA_FILTER",
        "_SCREEN_EDGE",
        "_COLOR_BLEEDING",
        "_CHROMATIC_ABERRATION",
        "_OUTLINE_HAND_DRAWN"
    };

    // ===== CACHED GUI STYLES =====
    private static GUIStyle _cachedHeaderTitleStyle;
    private static GUIStyle CachedHeaderTitleStyle
    {
        get
        {
            if (_cachedHeaderTitleStyle == null)
            {
                _cachedHeaderTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                _cachedHeaderTitleStyle.fontSize = 14;
            }
            return _cachedHeaderTitleStyle;
        }
    }

    private static GUIStyle _cachedCategoryBoxStyle;
    private static GUIStyle CachedCategoryBoxStyle
    {
        get
        {
            if (_cachedCategoryBoxStyle == null)
            {
                _cachedCategoryBoxStyle = new GUIStyle(GUI.skin.box);
                _cachedCategoryBoxStyle.padding = new RectOffset(10, 10, 5, 5);
            }
            return _cachedCategoryBoxStyle;
        }
    }

    private static GUIStyle _cachedCategoryTitleStyle;
    private static GUIStyle CachedCategoryTitleStyle
    {
        get
        {
            if (_cachedCategoryTitleStyle == null)
            {
                _cachedCategoryTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                _cachedCategoryTitleStyle.fontSize = 13;
            }
            return _cachedCategoryTitleStyle;
        }
    }

    private static GUIStyle _cachedCategoryDescStyle;
    private static GUIStyle CachedCategoryDescStyle
    {
        get
        {
            if (_cachedCategoryDescStyle == null)
            {
                _cachedCategoryDescStyle = new GUIStyle(EditorStyles.miniLabel);
                _cachedCategoryDescStyle.wordWrap = true;
            }
            return _cachedCategoryDescStyle;
        }
    }

    private static GUIStyle _cachedHDRToggleStyle;
    private static GUIStyle CachedHDRToggleStyle
    {
        get
        {
            if (_cachedHDRToggleStyle == null)
            {
                _cachedHDRToggleStyle = new GUIStyle(EditorStyles.miniButton);
                _cachedHDRToggleStyle.fontSize = 9;
                _cachedHDRToggleStyle.fixedWidth = 36;
                _cachedHDRToggleStyle.fixedHeight = 16;
                _cachedHDRToggleStyle.padding = new RectOffset(2, 2, 1, 1);
            }
            return _cachedHDRToggleStyle;
        }
    }

    private static GUIStyle _cachedMakeupBadgeStyleOn;
    private static GUIStyle CachedMakeupBadgeStyleOn
    {
        get
        {
            if (_cachedMakeupBadgeStyleOn == null)
            {
                _cachedMakeupBadgeStyleOn = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            return _cachedMakeupBadgeStyleOn;
        }
    }

    private static GUIStyle _cachedMakeupBadgeStyleOff;
    private static GUIStyle CachedMakeupBadgeStyleOff
    {
        get
        {
            if (_cachedMakeupBadgeStyleOff == null)
            {
                _cachedMakeupBadgeStyleOff = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Normal
                };
            }

            return _cachedMakeupBadgeStyleOff;
        }
    }

    // ===== FIELD REFERENCES =====
    /// <summary>All shader properties for the current material</summary>
    private MaterialProperty[] properties;

    /// <summary>Unity's material editor instance</summary>
    private MaterialEditor materialEditor;

    /// <summary>Target material being edited</summary>
    private Material targetMaterial;
    private bool workflowShouldReturn;
    private bool workflowIsNonToon;

    /// <summary>Whether the user has dismissed the dependency install status message this session</summary>
    private static bool _dismissedDependencyWarning;

    // ===== P-9: Quick Setup Wizard State =====
    private int quickSetupWizardStep = 0;
    private int quickSetupWizardMode = 0; // 0 = wizard/guided, 1 = show-all, 2 = category templates

    // ===== P-10: Onboarding Panel State =====
    private static bool _onboardingDismissed;
    private static readonly string OnboardingPrefsKey = "NataneToon_OnboardingDismissed_v3";

    // ===== RENDERING MODE =====
    public enum RenderingMode
    {
        Opaque = 0,
        Cutout = 1,
        Transparent = 2,
        Fur = 3,
        Background = 4
    }

    private static string[] RenderingModeLabels => new string[]
    {
        L("Opaque", "Opaque"),
        L("Cutout", "Cutout"),
        L("Transparent", "Transparent"),
        L("Fur", "Fur"),
        L("Background", "Background")
    };

    // ===== UI STATE =====
    // Tab index for category navigation
    private int selectedTab = 0;
    private string[] TabNames => new string[]
    {
        L("Texture & Color", "Texture & Color"), L("Light & Shadow", "Light & Shadow"), L("Effects", "Effects"), L("Environment & Reflection", "Environment & Reflection"), L("Advanced", "Advanced")
    };

    // ===== INSPECTOR MODE (Feature 1: Simple/Advanced) =====
    private enum InspectorMode { Simple = 0, Advanced = 1 }
    private InspectorMode inspectorMode = InspectorMode.Simple;
    private const string InspectorModePrefsKey = "NataneToon_InspectorMode";

    private static readonly HashSet<string> SimpleModeVisibleSections = new HashSet<string>
    {
        "QuickSetup", "MainTexture", "Shading",                            // Tab 0
        "AdvancedLighting",                                                  // Tab 1
        "Specular", "RimLight", "MatCap", "Outline", "Emission",           // Tab 2
        "Reflection", "EnvironmentalRim",                                    // Tab 3
        "NormalMap", "DistanceFade", "Rendering",                           // Tab 4
        "CurrentState", "Presets", "FeatureOverview", "Performance",        // Shared
    };

    // ===== ACTIVE ONLY FILTER (Feature 2) =====
    private bool showActiveOnly = false;
    private const string ShowActiveOnlyPrefsKey = "NataneToon_ShowActiveOnly";

    /// <summary>
    /// Section key → shader keyword mapping for active/inactive detection.
    /// Sections without a toggle keyword (e.g. MainTexture, Shading) are always considered active.
    /// </summary>
    private static readonly Dictionary<string, string> sectionToggleKeywords = new Dictionary<string, string>
    {
        { "ScreenTone", "_SCREEN_TONE" },
        { "HalftoneShadow", "_HALFTONE_SHADOW" },
        { "ShadowEdgeNoise", "_SHADOW_EDGE_NOISE" },
        { "GradientBaseColor", "_GRADIENT_BASE_COLOR" },
        { "LightVolume", "_USE_LIGHT_VOLUME" },
        { "LTCGI", "_LTCGI" },
        { "CastShadowColor", "_CAST_SHADOW_COLOR" },
        { "LightSnap", "_LIGHT_SNAP" },
        { "AO", "_USE_AO" },
        { "Dithering", "_USE_DITHERING" },
        { "PBR", "_PBR" },
        { "Specular", "_SPECULAR" },
        { "HairSpecular", "_HAIR_SPECULAR" },
        { "RimLight", "_RIM_LIGHT" },
        { "SSS", "_SSS" },
        { "MatCap", "_MATCAP" },
        { "ProceduralMatCap", "_PROCEDURAL_MATCAP" },
        { "Glitter", "_GLITTER" },
        { "Drip", "_WATER_DRIP" },
        { "Smear", "_SMEAR" },
        { "Fur", "_FUR" },
        { "Decal", "_DECAL" },
        { "SurfaceCover", "_SURFACE_COVER" },
        { "Hologram", "_HOLOGRAM" },
        { "IllustrationStyle", "_COLOR_QUANTIZE" },
        { "Outline", "_OUTLINE" },
        { "Emission", "_EMISSION" },
        { "AudioLink", "_AUDIOLINK" },
        { "NormalMap", "_NORMALMAP" },
        { "Parallax", "_PARALLAX" },
        { "DetailMap", "_DETAIL_MAP" },
        { "Triplanar", "_TRIPLANAR" },
        { "VertexAnimation", "_VERTEX_ANIMATION" },
        { "VAT", "_VAT" },
        { "Tessellation", "_TESSELLATION" },
        { "Backface", "_BACKFACE_TEXTURE" },
        { "Video", "_VIDEO_TEXTURE" },
        { "HeightFade", "_HEIGHT_FADE" },
        { "IntersectionFade", "_INTERSECTION_FADE" },
        { "DistanceFade", "_DISTANCE_FADE" },
        { "HeightFog", "_HEIGHT_FOG" },
        { "Reflection", "_REFLECTION" },
        { "FakeReflection", "_FAKE_REFLECTION" },
        { "Iridescence", "_IRIDESCENCE" },
        { "EnvironmentalRim", "_ENV_RIM" },
        { "Refraction", "_REFRACTION" },
        { "DepthColorFade", "_DEPTH_COLOR_FADE" },
        { "MirrorControl", "_MIRROR_CONTROL" },
        { "QuestLite", "_QUEST_LITE" },
        { "PerspectiveFlat", "_PERSPECTIVE_FLAT" },
    };

    // Reverse lookup: keyword → section key (for auto-expand on toggle ON)
    private static Dictionary<string, string> _keywordToSectionKey;
    private static Dictionary<string, string> KeywordToSectionKey
    {
        get
        {
            if (_keywordToSectionKey == null)
            {
                _keywordToSectionKey = new Dictionary<string, string>();
                foreach (var kvp in sectionToggleKeywords)
                    _keywordToSectionKey[kvp.Value] = kvp.Key;
            }
            return _keywordToSectionKey;
        }
    }

    // ===== CATEGORY PRESETS (Feature 3) =====
    private struct CategoryPreset
    {
        public string nameJP, nameEN, descJP, descEN;
        public string[] enableKeywords;
        public bool switchToSimpleMode;
    }

    private static readonly CategoryPreset[] categoryPresets = new CategoryPreset[]
    {
        new CategoryPreset
        {
            nameJP = "キャラクター肌", nameEN = "Character Skin",
            descJP = "Shading + SSS + リムライト", descEN = "Shading + SSS + Rim Light",
            enableKeywords = new[] { "_SSS", "_RIM_LIGHT" },
            switchToSimpleMode = true
        },
        new CategoryPreset
        {
            nameJP = "衣装", nameEN = "Clothing",
            descJP = "Shading + ノーマルマップ + アウトライン", descEN = "Shading + Normal Map + Outline",
            enableKeywords = new[] { "_NORMALMAP", "_OUTLINE" },
            switchToSimpleMode = true
        },
        new CategoryPreset
        {
            nameJP = "髪", nameEN = "Hair",
            descJP = "ヘアスペキュラー + アウトライン", descEN = "Hair Specular + Outline",
            enableKeywords = new[] { "_HAIR_SPECULAR", "_OUTLINE" },
            switchToSimpleMode = true
        },
        new CategoryPreset
        {
            nameJP = "目", nameEN = "Eyes",
            descJP = "パララックス + エミッション + MatCap", descEN = "Parallax + Emission + MatCap",
            enableKeywords = new[] { "_PARALLAX", "_EMISSION", "_MATCAP" },
            switchToSimpleMode = true
        },
        new CategoryPreset
        {
            nameJP = "背景・小物", nameEN = "Background / Props",
            descJP = "ノーマルマップ + トライプレーナー + ハイトフォグ", descEN = "Normal Map + Triplanar + Height Fog",
            enableKeywords = new[] { "_NORMALMAP", "_TRIPLANAR", "_HEIGHT_FOG" },
            switchToSimpleMode = false
        },
        new CategoryPreset
        {
            nameJP = "エフェクト", nameEN = "Effects",
            descJP = "エミッション + ホログラム + ディゾルブ", descEN = "Emission + Hologram + Dissolve",
            enableKeywords = new[] { "_EMISSION", "_HOLOGRAM" },
            switchToSimpleMode = false
        },
    };

    // ===== SEARCH STATE =====
    private string searchQuery = "";
    /// <summary>
    /// Section search data: pairs of (display name, keywords) used for search matching.
    /// </summary>
    private static readonly string[][] sectionSearchData = new string[][]
    {
        // { drawMethodSuffix, displayName, keywords }
        // --- Core: Workflow & Base ---
        new[] { "CurrentState", "編集ワークフロー", "workflow current state summary shader status mode migration rendering workflow liltoon natane quick overview 編集ワークフロー 現在の状態 シェーダー モード 移行 描画タイプ" },
        new[] { "MainTexture", "Main Texture", "main texture color" },
        new[] { "MakeupTextures", "Makeup Textures", "makeup texture layer 2nd 3rd 4th 5th" },
        new[] { "NormalMap", "Normal Map", "normal map bump" },
        new[] { "Backface", "Backface", "backface texture back" },
        // --- Core: Shading & Lighting ---
        new[] { "Shading", "Shading", "shading toon shadow" },
        new[] { "AdvancedLighting", "Advanced Lighting", "advanced lighting" },
        new[] { "AO", "Ambient Occlusion", "ambient occlusion ao" },
        new[] { "LightVolume", "VRC Light Volume", "vrc light volume" },
        new[] { "LTCGI", "LTCGI", "ltcgi area light" },
        new[] { "LightSnap", "Light Snap", "light snap direction stabilize flicker" },
        new[] { "CastShadowColor", "Cast Shadow Color", "cast shadow color tint intensity" },
        new[] { "ShadowEdgeNoise", "Shadow Edge Noise", "shadow edge noise hand-drawn analog" },
        // --- Effects: Surface ---
        new[] { "Specular", "Specular", "specular reflection" },
        new[] { "HairSpecular", "Hair Specular", "hair specular highlight kajiya" },
        new[] { "RimLight", "Rim Light", "rim light edge offset" },
        new[] { "EnvironmentalRim", "Environmental Rim", "environmental rim" },
        new[] { "SSS", "Subsurface Scattering", "sss subsurface scattering translucent" },
        new[] { "MatCap", "MatCap", "matcap sphere map" },
        new[] { "ProceduralMatCap", "Procedural MatCap", "procedural matcap texture-free mathematical gradient fresnel" },
        new[] { "Reflection", "Reflection", "reflection cubemap" },
        new[] { "FakeReflection", "Fake Reflection", "fake reflection environment sky ground cubemap-free lightweight" },
        new[] { "Iridescence", "Iridescence", "iridescence" },
        new[] { "Refraction", "Refraction", "refraction ior" },
        new[] { "PBR", "PBR", "pbr metallic smoothness reflection probe" },
        // --- Effects: Artistic ---
        new[] { "ScreenTone", "Screen Tone", "screen tone halftone dot pattern overlay" },
        new[] { "Dithering", "Dithering", "dithering screen tone" },
        new[] { "Glitter", "Glitter", "glitter sparkle" },
        new[] { "Emission", "Emission", "emission glow" },
        new[] { "Decal", "Decal", "decal sticker" },
        new[] { "Outline", "Outline", "outline contour smooth normal" },
        new[] { "Hologram", "Hologram", "hologram glitch" },
        new[] { "Drip", "Drip", "drip water drop" },
        new[] { "Smear", "Smear", "smear afterimage stretch trail glow" },
        new[] { "VirtualExpression", "Virtual Expression", "virtual expression dissolve" },
        new[] { "AudioLink", "AudioLink", "audiolink music reactive" },
        new[] { "GradientBaseColor", "Gradient Base Color", "gradient base color position tint" },
        new[] { "DepthColorFade", "Depth Color Fade", "depth color fade aerial perspective atmosphere desaturation distance" },
        new[] { "PerspectiveFlat", "Perspective Flat", "perspective flat flatten depth compression 2d illustration" },
        // --- Effects: Geometry & Animation ---
        new[] { "Parallax", "Parallax", "parallax height map" },
        new[] { "VertexAnimation", "Vertex Animation", "vertex animation wind breath pulse" },
        new[] { "VAT", "VAT", "vat vertex animation texture houdini" },
        new[] { "Tessellation", "Tessellation", "tessellation smoothing phong" },
        new[] { "Fur", "Fur", "fur shell hair strand pelt" },
        new[] { "DetailMap", "Detail Map", "detail map secondary uv close-up" },
        new[] { "Triplanar", "Triplanar", "triplanar projection no uv rock terrain" },
        new[] { "SurfaceCover", "Surface Cover", "surface cover snow sand accumulation" },
        // --- Advanced: Rendering & Optimization ---
        new[] { "Rendering", "Rendering", "rendering mode opaque cutout transparent" },
        new[] { "HeightFade", "Height Fade", "height fade local position transparency" },
        new[] { "IntersectionFade", "Intersection Fade", "intersection fade depth contact" },
        new[] { "DistanceFade", "Distance Fade", "distance fade lod" },
        new[] { "HeightFog", "Height Fog", "height fog material fog mist atmosphere" },
        new[] { "Stencil", "Stencil", "stencil mask buffer" },
        new[] { "Video", "Video", "video texture render" },
        new[] { "BackgroundLightmap", "Background Lightmap", "lightmap background bake gi" },
        new[] { "MirrorControl", "Mirror / Camera Control", "mirror camera control vrchat reflection photo" },
        new[] { "QuestLite", "Quest Lite", "quest lite mobile performance optimization" },
    };

    // ===== SHADER TYPE DRAWER INSTANCES =====
    private NataneToon.Editor.NataneToonEyeDrawer eyeDrawer;
    private NataneToon.Editor.NataneToonWirelightDrawer wirelightDrawer;
    // private NataneToon.Editor.NataneToonScreenFXDrawer screenFXDrawer;

    // ===== FOLDOUT STATE MANAGEMENT =====
    // Foldout states are per-material and persisted using EditorPrefs via Dictionary
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

    // Mapping from foldout key to EditorPrefs suffix (preserves backward-compatible key names)
    // Most keys follow "Show" + key pattern, but some have legacy names
    private static readonly Dictionary<string, string> foldoutPrefsKeys = new Dictionary<string, string>
    {
        { "CurrentState", "ShowCurrentState" },
        { "Presets", "ShowPresets" },
        { "Performance", "ShowPerformance" },
        { "MainTexture", "ShowBasic" },
        { "MakeupTextures", "ShowMakeupTextures" },
        { "Shading", "ShowShading" },
        { "AdvancedLighting", "ShowAdvancedLighting" },
        { "LightVolume", "ShowLightVolume" },
        { "Specular", "ShowSpecular" },
        { "HairSpecular", "ShowHairSpecular" },
        { "RimLight", "ShowRimLight" },
        { "SSS", "ShowSSS" },
        { "MatCap", "ShowMatCap" },
        { "Glitter", "ShowGlitter" },
        { "Drip", "ShowDrip" },
        { "Smear", "ShowSmear" },
        { "Hologram", "ShowHologram" },
        { "Outline", "ShowOutline" },
        { "Emission", "ShowEmission" },
        { "VirtualExpression", "ShowVirtualExpression" },
        { "NormalMap", "ShowNormalMap" },
        { "Reflection", "ShowReflection" },
        { "Iridescence", "ShowIridescence" },
        { "EnvironmentalRim", "ShowEnvironmentalRim" },
        { "Parallax", "ShowParallax" },
        { "Refraction", "ShowRefraction" },
        { "Rendering", "ShowAdvanced" },
        { "VertexAnimation", "ShowVertexAnimation" },
        { "VAT", "ShowVAT" },
        { "Tessellation", "ShowTessellation" },
        { "LTCGI", "ShowLTCGI" },
        { "AO", "ShowAO" },
        { "Dithering", "ShowDithering" },
        { "Decal", "ShowDecal" },
        { "Backface", "ShowBackface" },
        { "Video", "ShowVideo" },
        { "AudioLink", "ShowAudioLink" },
        { "DistanceFade", "ShowDistanceFade" },
        { "GradientBaseColor", "ShowGradientBaseColor" },
        { "HeightFade", "ShowHeightFade" },
        { "IntersectionFade", "ShowIntersectionFade" },
        { "FeatureOverview", "ShowFeatureOverview" },
        { "Fur", "ShowFur" },
        { "BackgroundLightmap", "ShowBackgroundLightmap" },
        { "PBR", "ShowPBR" },
            { "HalftoneShadow", "ShowHalftoneShadow" },
            { "ShadowEdgeNoise", "ShowShadowEdgeNoise" },
            { "CastShadowColor", "ShowCastShadowColor" },
            { "LightSnap", "ShowLightSnap" },
            { "ProceduralMatCap", "ShowProceduralMatCap" },
            { "FakeReflection", "ShowFakeReflection" },
            { "PerspectiveFlat", "ShowPerspectiveFlat" },
            { "DepthColorFade", "ShowDepthColorFade" },
    };

    // Default values: keys listed here default to true; all others default to false
    private static readonly HashSet<string> foldoutDefaultTrue = new HashSet<string>
    {
        "CurrentState", "Presets", "Performance", "MainTexture", "Shading"
    };

    private bool GetFoldout(string key)
    {
        if (!foldoutStates.ContainsKey(key))
        {
            bool defaultVal = foldoutDefaultTrue.Contains(key);
            string prefsKey = foldoutPrefsKeys.ContainsKey(key) ? foldoutPrefsKeys[key] : "Show" + key;
            foldoutStates[key] = EditorPrefs.GetBool(
                NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, prefsKey), defaultVal);
        }
        return foldoutStates[key];
    }

    private void SetFoldout(string key, bool value)
    {
        foldoutStates[key] = value;
        string prefsKey = foldoutPrefsKeys.ContainsKey(key) ? foldoutPrefsKeys[key] : "Show" + key;
        EditorPrefs.SetBool(
            NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, prefsKey), value);
    }

    // Foldout state initialization tracking
    private int _lastLoadedMaterialInstanceId = -1;
    private int _lastValidatedMaterialInstanceId = -1;
    private bool _keywordValidationRequested = true;
    private bool _hasCachedSamplerBudgetEstimate;
    private NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate _cachedSamplerBudgetEstimate;
    private readonly Dictionary<string, NataneToonSamplerBudgetEstimator.ToggleEvaluation> _toggleEvaluationCache =
        new Dictionary<string, NataneToonSamplerBudgetEstimator.ToggleEvaluation>();

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        try
        {
            this.materialEditor = materialEditor;
            this.properties = properties;
            this.targetMaterial = materialEditor.target as Material;
            _cachedAllTargetMaterials = null; // Reset per-frame cache

            // Validate that we have valid references
            if (this.materialEditor == null || this.properties == null || this.targetMaterial == null)
            {
                EditorGUILayout.HelpBox(L("Failed to initialize material editor.", "Failed to initialize material editor."), MessageType.Error);
                return;
            }

            // Clear foldout cache on material change so GetFoldout re-reads EditorPrefs
            int currentMaterialId = this.targetMaterial.GetInstanceID();
            if (_lastLoadedMaterialInstanceId != currentMaterialId)
            {
                foldoutStates.Clear();
                _lastLoadedMaterialInstanceId = currentMaterialId;
                _lastValidatedMaterialInstanceId = -1;
                _keywordValidationRequested = true;
                searchQuery = "";
                InvalidateInspectorCaches();
            }
            LoadUIState();
            EnsureKeywordsValidatedForCurrentMaterial();

            // ===== Compact Header =====
            DrawCompactHeader();

            // P-10: Onboarding guide (first time only)
            if (!_onboardingDismissed && !EditorPrefs.GetBool(OnboardingPrefsKey, false))
            {
                DrawOnboardingPanel();
            }

            workflowShouldReturn = false;
            workflowIsNonToon = false;
            SafeDrawSection(DrawCurrentStateSection, L("編集ワークフロー", "Workflow"));
            if (workflowShouldReturn)
            {
                SaveUIState();
                GUIUtility.ExitGUI();
                return;
            }
            if (workflowIsNonToon)
            {
                DrawNonToonShaderGUI(NataneToon.Editor.NataneToonShaderTypeSwitcher.DetectShaderType(targetMaterial));
                return;
            }

            DrawDependencyInspectorWarnings();
            DrawSamplerBudgetInspectorWarning();
            DrawGrabPassSuggestionBanner();
            NataneToonShaderGUIUtility.DrawCompactPerformanceSummary(targetMaterial, GetCurrentSamplerBudgetEstimate());

            // ===== Multi-material editing indicator (Feature 4: show variant names) =====
            if (materialEditor.targets != null && materialEditor.targets.Length > 1)
            {
                // Collect unique shader variant names
                var variantNames = new HashSet<string>();
                foreach (Material mat in GetAllTargetMaterials())
                {
                    if (mat != null && mat.shader != null)
                        variantNames.Add(mat.shader.name);
                }

                string variantInfo = variantNames.Count > 1
                    ? L($"{materialEditor.targets.Length} 個のマテリアルを同時編集中（{variantNames.Count}バリアント: {string.Join(", ", variantNames)}）",
                        $"Editing {materialEditor.targets.Length} materials ({variantNames.Count} variants: {string.Join(", ", variantNames)})")
                    : L($"{materialEditor.targets.Length} 個のマテリアルを同時編集中です。混在する値は「-」で表示されます。",
                        $"Editing {materialEditor.targets.Length} materials simultaneously. Mixed values are shown as '-'.");

                EditorGUILayout.HelpBox(variantInfo, MessageType.Info);
            }

            // Cross-variant editor button: show when Selection contains Natane materials with different variants
            if (NataneToon.Editor.NataneCrossVariantEditor.HasCrossVariantSelection())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(
                    L("異なるバリアントのマテリアルが選択されています。一括編集ウインドウを使うと共通パラメータを一括変更できます。",
                      "Materials with different variants are selected. Use the cross-variant editor to bulk-edit common parameters."),
                    MessageType.Info);
                if (GUILayout.Button(
                    L("一括編集\nウインドウ", "Cross-Variant\nEditor"),
                    GUILayout.Width(80), GUILayout.Height(38)))
                {
                    NataneToon.Editor.NataneCrossVariantEditor.ShowWindow();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // ===== Tab Navigation =====
            // P-22: タブのキーボードショートカット (Ctrl+1~5)
            if (Event.current.type == EventType.KeyDown && Event.current.control)
            {
                int targetTab = -1;
                switch (Event.current.keyCode)
                {
                    case KeyCode.Alpha1: targetTab = 0; break;
                    case KeyCode.Alpha2: targetTab = 1; break;
                    case KeyCode.Alpha3: targetTab = 2; break;
                    case KeyCode.Alpha4: targetTab = 3; break;
                    case KeyCode.Alpha5: targetTab = 4; break;
                }
                if (targetTab >= 0 && targetTab != selectedTab)
                {
                    selectedTab = targetTab;
                    SaveUIState();
                    Event.current.Use();
                    if (materialEditor != null) materialEditor.Repaint();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.BeginChangeCheck();
            bool narrowView = EditorGUIUtility.currentViewWidth < 420f;
            string[] displayTabNames = narrowView
                ? new[] { L("色", "Tex"), L("光", "Light"), L("FX", "FX"), L("環境", "Env"), L("詳細", "Adv") }
                : TabNames;
            selectedTab = GUILayout.Toolbar(selectedTab, displayTabNames, GUILayout.Height(TAB_HEIGHT));
            if (EditorGUI.EndChangeCheck())
            {
                SaveUIState();
                GUI.FocusControl(null); // Clear focus to update UI

                // Force repaint and exit GUI to prevent layout conflicts
                if (materialEditor != null)
                {
                    materialEditor.Repaint();
                }
                GUIUtility.ExitGUI();
            }

            // ===== Search Bar =====
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            // P-12: 全タブ横断検索であることを明示するラベル
            GUILayout.Label(L("\ud83d\udd0d 全タブ検索", "\ud83d\udd0d Search All"), EditorStyles.miniLabel, GUILayout.Width(75));
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
            if (!string.IsNullOrEmpty(searchQuery) && GUILayout.Button(L("クリア", "Clear"), EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }
            // P-14: ジャンプメニュー - 検索バーの右端に配置
            if (GUILayout.Button(L("\u25bc ジャンプ", "\u25bc Jump"), EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                ShowJumpMenu();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

            // ===== Tab Content or Search Results =====
            if (!string.IsNullOrEmpty(searchQuery))
            {
                DrawSharedInspectorSections();
                EditorGUILayout.Space(SECTION_SPACING);
                DrawSearchResults(searchQuery);
            }
            else
            {
                DrawSharedInspectorSections();
                EditorGUILayout.Space(SECTION_SPACING);

                switch (selectedTab)
                {
                    case 0:
                        DrawBasicTab();
                        break;
                    case 1:
                        DrawLightingTab();
                        break;
                    case 2:
                        DrawEffectsTab();
                        break;
                    case 3:
                        DrawEnvironmentTab();
                        break;
                    case 4:
                        DrawAdvancedTab();
                        break;
                }
            }

            if (GUI.changed)
            {
                SynchronizeKeywordsAndRefreshInspectorCaches();
            }

            // Foldout states are persisted immediately via SetFoldout() - no batch save needed.
        }
        catch (ExitGUIException)
        {
            throw; // ExitGUIException is used internally by Unity IMGUI - must not be caught
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox(L($"An error occurred while drawing the inspector: {e.Message}", $"An error occurred while drawing the inspector: {e.Message}"), MessageType.Error);
            UnityEngine.Debug.LogException(e);
        }
    }

    private void InvalidateInspectorCaches()
    {
        _hasCachedSamplerBudgetEstimate = false;
        _toggleEvaluationCache.Clear();
        _cachedAllTargetMaterials = null;
    }

    private NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate GetCurrentSamplerBudgetEstimate()
    {
        if (!_hasCachedSamplerBudgetEstimate)
        {
            _cachedSamplerBudgetEstimate = NataneToonSamplerBudgetEstimator.Estimate(targetMaterial);
            _hasCachedSamplerBudgetEstimate = true;
        }

        return _cachedSamplerBudgetEstimate;
    }

    private NataneToonSamplerBudgetEstimator.ToggleEvaluation GetCachedToggleEvaluation(string keyword)
    {
        if (!_toggleEvaluationCache.TryGetValue(keyword, out NataneToonSamplerBudgetEstimator.ToggleEvaluation evaluation))
        {
            evaluation = NataneToonSamplerBudgetEstimator.EvaluateEnable(targetMaterial, keyword);
            _toggleEvaluationCache[keyword] = evaluation;
        }

        return evaluation;
    }

    private void EnsureKeywordsValidatedForCurrentMaterial()
    {
        if (targetMaterial == null)
        {
            return;
        }

        int materialId = targetMaterial.GetInstanceID();
        if (!_keywordValidationRequested && _lastValidatedMaterialInstanceId == materialId)
        {
            return;
        }

        ValidateAndFixKeywords();
        _lastValidatedMaterialInstanceId = materialId;
        _keywordValidationRequested = false;
        InvalidateInspectorCaches();
    }

    internal void SynchronizeKeywordsAndRefreshInspectorCaches()
    {
        ValidateAndFixKeywords();
        _lastValidatedMaterialInstanceId = targetMaterial != null ? targetMaterial.GetInstanceID() : -1;
        _keywordValidationRequested = false;
        InvalidateInspectorCaches();
    }

    private void SafeDrawSection(System.Action drawAction, string sectionName)
    {
        try
        {
            drawAction?.Invoke();
        }
        catch (ExitGUIException)
        {
            throw;
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox(L($"Error drawing {sectionName} section: {e.Message}", $"Error drawing {sectionName} section: {e.Message}"), MessageType.Warning);
            UnityEngine.Debug.LogWarning($"[NataneToonShaderGUI] Error drawing {sectionName} section: {e.Message}");
        }
    }

    // ===== FEATURE 1 & 2: Section visibility filtering =====

    /// <summary>
    /// Determines whether a section should be shown based on current inspector mode and active-only filter.
    /// </summary>
    private bool ShouldShowSection(string sectionKey)
    {
        // Simple mode: only show whitelisted sections
        if (inspectorMode == InspectorMode.Simple && !SimpleModeVisibleSections.Contains(sectionKey))
            return false;

        // Active Only filter (Advanced mode only): hide inactive toggle sections
        if (showActiveOnly && inspectorMode == InspectorMode.Advanced && !IsSectionActive(sectionKey))
            return false;

        return true;
    }

    /// <summary>
    /// Whether the section's toggle keyword is enabled on at least one target material.
    /// Sections without a toggle keyword are always considered active.
    /// </summary>
    private bool IsSectionActive(string sectionKey)
    {
        if (!sectionToggleKeywords.TryGetValue(sectionKey, out string keyword))
            return true; // No toggle → always active

        // Multi-material: if any target has the keyword enabled, consider active
        if (materialEditor != null && materialEditor.targets != null)
        {
            foreach (var target in materialEditor.targets)
            {
                Material mat = target as Material;
                if (mat != null && mat.IsKeywordEnabled(keyword))
                    return true;
            }
            return false;
        }

        return targetMaterial != null && targetMaterial.IsKeywordEnabled(keyword);
    }

    /// <summary>
    /// Wrapper around SafeDrawSection that respects section visibility filtering.
    /// In Advanced mode, non-active sections are auto-collapsed and dimmed.
    /// </summary>
    private void FilteredDrawSection(System.Action drawAction, string sectionName, string sectionKey)
    {
        if (!ShouldShowSection(sectionKey))
            return;

        // In Advanced mode, auto-collapse non-active sections (only if they have a toggle)
        if (inspectorMode == InspectorMode.Advanced && !IsSectionActive(sectionKey) && sectionToggleKeywords.ContainsKey(sectionKey))
        {
            // Dim the section by reducing alpha
            Color oldColor = GUI.color;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, 0.6f);
            SafeDrawSection(drawAction, sectionName);
            GUI.color = oldColor;
            return;
        }

        SafeDrawSection(drawAction, sectionName);
    }

    // ===== BOXED SECTION STYLES (Cached) =====
    private static GUIStyle _boxedSectionOuter;
    private static GUIStyle BoxedSectionOuter
    {
        get
        {
            if (_boxedSectionOuter == null)
            {
                _boxedSectionOuter = new GUIStyle(EditorStyles.helpBox);
                _boxedSectionOuter.padding = new RectOffset(0, 0, 0, 0);
                _boxedSectionOuter.margin = new RectOffset(0, 0, 2, 2);
            }
            return _boxedSectionOuter;
        }
    }

    private static GUIStyle _boxedHeaderFoldout;
    private static GUIStyle BoxedHeaderFoldout
    {
        get
        {
            if (_boxedHeaderFoldout == null)
            {
                _boxedHeaderFoldout = new GUIStyle(EditorStyles.foldoutHeader);
                _boxedHeaderFoldout.fontStyle = FontStyle.Bold;
                _boxedHeaderFoldout.fontSize = 12;
            }
            return _boxedHeaderFoldout;
        }
    }

    private static GUIStyle _badgeStyleOn;
    private static GUIStyle BadgeStyleOn
    {
        get
        {
            if (_badgeStyleOn == null)
            {
                _badgeStyleOn = new GUIStyle(EditorStyles.miniLabel);
                _badgeStyleOn.normal.textColor = Color.white;
                _badgeStyleOn.fontStyle = FontStyle.Bold;
                _badgeStyleOn.fontSize = 9;
                _badgeStyleOn.alignment = TextAnchor.MiddleCenter;
                _badgeStyleOn.padding = new RectOffset(4, 4, 1, 1);
            }
            return _badgeStyleOn;
        }
    }

    private static GUIStyle _badgeStyleOff;
    private static bool _badgeStyleOffWasDark;
    private static GUIStyle BadgeStyleOff
    {
        get
        {
            bool isDark = EditorGUIUtility.isProSkin;
            if (_badgeStyleOff == null || _badgeStyleOffWasDark != isDark)
            {
                _badgeStyleOff = new GUIStyle(EditorStyles.miniLabel);
                _badgeStyleOff.normal.textColor = isDark
                    ? new Color(0.6f, 0.6f, 0.6f, 0.8f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.6f);
                _badgeStyleOff.fontSize = 9;
                _badgeStyleOff.alignment = TextAnchor.MiddleCenter;
                _badgeStyleOff.padding = new RectOffset(4, 4, 1, 1);
                _badgeStyleOffWasDark = isDark;
            }
            return _badgeStyleOff;
        }
    }

    // ===== BOXED SECTION DRAWING =====

    /// <summary>
    /// Draw a boxed section with colored header, accent bar, and optional ON/OFF badge.
    /// lilToon boxOuter/boxInner pattern.
    /// Returns the foldout state.
    /// </summary>
    private bool DrawBoxedSection(string title, bool foldout, SectionCategory category, string toggleKeyword = null)
    {
        Color catColor = NataneToonShaderGUIStyles.GetSectionColor(category);
        bool isDark = EditorGUIUtility.isProSkin;

        // Outer box
        EditorGUILayout.Space(2);
        Rect outerRect = EditorGUILayout.BeginVertical(BoxedSectionOuter);

        // Reserve header space (single row, fixed height)
        Rect headerRect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));

        // Draw header background + accent bar on Repaint
        if (Event.current.type == EventType.Repaint)
        {
            float bgAlpha = isDark ? 0.10f : 0.05f;
            Color headerBg = new Color(catColor.r, catColor.g, catColor.b, bgAlpha);
            EditorGUI.DrawRect(headerRect, headerBg);

            // Left accent bar (3px)
            Rect accentRect = new Rect(headerRect.x, headerRect.y, 3, headerRect.height);
            EditorGUI.DrawRect(accentRect, catColor);
        }

        // Handle click on entire header to toggle foldout
        if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
        {
            foldout = !foldout;
            Event.current.Use();
        }

        // Draw foldout arrow + title (non-interactive, just visual)
        // P-16: セクションカテゴリに応じたアイコンプレフィックスを追加
        string iconTitle = GetSectionIcon(category) + " " + title;
        Rect foldoutRect = new Rect(headerRect.x + 6, headerRect.y + 2, headerRect.width - 50, headerRect.height - 4);
        EditorGUI.Foldout(foldoutRect, foldout, iconTitle, true, BoxedHeaderFoldout);

        // ON/OFF badge (drawn at right side of header) — Feature 4: 3-state for multi-material
        if (!string.IsNullOrEmpty(toggleKeyword))
        {
            Rect badgeRect = new Rect(headerRect.xMax - 38, headerRect.y + 4, 32, 16);

            // Count how many targets have this keyword enabled
            int enabledCount = 0;
            int totalCount = 0;
            Material[] allTargets = GetAllTargetMaterials();
            foreach (Material mat in allTargets)
            {
                if (mat != null)
                {
                    totalCount++;
                    if (mat.IsKeywordEnabled(toggleKeyword)) enabledCount++;
                }
            }

            if (enabledCount == totalCount && totalCount > 0)
            {
                // All ON
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f, 0.8f);
                GUI.Label(badgeRect, "ON", BadgeStyleOn);
                GUI.backgroundColor = oldBg;
            }
            else if (enabledCount > 0)
            {
                // Mixed state
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.7f, 0.2f, 0.8f);
                GUI.Label(badgeRect, "---", BadgeStyleOn);
                GUI.backgroundColor = oldBg;
            }
            else
            {
                // All OFF
                GUI.Label(badgeRect, "OFF", BadgeStyleOff);
                // P-17: Dependency hint for disabled features
                Rect hintRect = new Rect(badgeRect.x - 120, badgeRect.y, 115, badgeRect.height);
                GUI.Label(hintRect, L("\u25B6 \u6709\u52B9\u306B\u3057\u3066\u4F7F\u7528", "\u25B6 Enable to use"), NataneToonShaderGUIStyles.DependencyHintLabel);
            }
        }

        EditorGUILayout.Space(2);

        if (foldout)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(2);
        }

        return foldout;
    }

    /// <summary>
    /// End a boxed section. Must be called after DrawBoxedSection.
    /// </summary>
    private void EndBoxedSection(bool wasExpanded)
    {
        if (wasExpanded)
        {
            GUILayout.Space(4);
            EditorGUILayout.EndVertical(); // content area
        }
        EditorGUILayout.EndVertical(); // outer box
    }

    private void DrawMainTextureSection()
    {
        SetFoldout("MainTexture", DrawBoxedSection(L("メインテクスチャ", "Main Texture"), GetFoldout("MainTexture"), SectionCategory.Basic));
        if (GetFoldout("MainTexture"))
        {
            DrawProperty("_MainTex", L("メインテクスチャ", "Main Texture"));
            DrawColorProperty("_Color", L("色", "Color"));

            EditorGUILayout.Space(SECTION_SPACING);
            bool mainTexAnim = DrawToggle("_MAIN_TEX_ANIMATION", "_MainTexAnimation", L("メインテクスチャアニメーション", "Main Texture Animation"));
            if (mainTexAnim)
            {
                DrawUVAnimationSettings("_MainTexScrollSpeed", "_MainTexRotateSpeed", L("メインテクスチャ", "Main Texture"));
                DrawHelpToggle(
                    "MainTexAnimation",
                    L("メインテクスチャの UV をスクロール・回転させます。",
                      "Scrolls and rotates the main texture UVs."),
                    MessageType.Info);
            }

            EditorGUILayout.Space(10);
            DrawSubGroupHeader(L("色保持と補正", "Color Preservation & Enhancement"));

            DrawProperty("_AlbedoPreservation", L("テクスチャ色保持", "Texture Color Preservation"));
            DrawHelpToggle(
                "AlbedoPreservation",
                L("ライティングによる明るさ変化を加えつつ、元のテクスチャ色を保ちやすくします。",
                  "Preserves the original texture color while applying lighting brightness."),
                MessageType.Info);

            EditorGUILayout.Space();
            DrawProperty("_Saturation", L("Saturation", "Saturation"));
            DrawHelpToggle(
                "Saturation",
                L("最終的な色の鮮やかさを調整します。",
                  "Adjusts final color vividness."),
                MessageType.Info);

            EditorGUILayout.Space();
            DrawProperty("_Brightness", L("全体の明るさ", "Overall Brightness"));
            DrawHelpToggle(
                "Brightness",
                L("最終出力の明るさを調整します。",
                  "Adjusts the final output brightness."),
                MessageType.Info);

            EditorGUILayout.Space(10);
            DrawSubGroupHeader(L("最終色ブレンド", "Final Color Blending"));
            DrawHelpToggle(
                "FinalColorBlending",
                L("すべての効果を重ねたあとでも、白飛びや黒つぶれを抑えやすくします。",
                  "Helps prevent highlight blow-out and shadow crush after all effects are applied."),
                MessageType.None);

            EditorGUILayout.Space();
            DrawProperty("_FinalHighlightBlend", L("Highlight Blend (Prevent Blow-out)", "Highlight Blend (Prevent Blow-out)"));
            DrawProperty("_HighlightThreshold", L("Highlight Threshold", "Highlight Threshold"));
            DrawHelpToggle(
                "HighlightBlend",
                L("しきい値を超えた明るすぎる部分をやわらげます。",
                  "Softens overly bright areas above the threshold."),
                MessageType.Info);

            EditorGUILayout.Space();
            DrawProperty("_FinalShadowBlend", L("Shadow Blend (Prevent Crush)", "Shadow Blend (Prevent Crush)"));
            DrawProperty("_ShadowThreshold", L("Shadow Threshold", "Shadow Threshold"));
            DrawHelpToggle(
                "ShadowBlend",
                L("しきい値より暗い部分のつぶれをやわらげます。",
                  "Softens very dark areas below the threshold."),
                MessageType.Info);
        }
        EndBoxedSection(GetFoldout("MainTexture"));
    }

    private void DrawSurfaceFinishSection()
    {
        DrawProperty("_Glossiness", L("Glossiness", "Glossiness"));
        DrawHelpToggle(
            "Glossiness",
            L("Controls the intensity of reflective effects such as specular, rim, MatCap, and reflection.",
              "Controls the intensity of reflective effects such as specular, rim, MatCap, and reflection."),
            MessageType.Info);

        EditorGUILayout.Space();
        DrawProperty("_MatteEffect", L("Matte Effect (Additional Gloss Reduction)", "Matte Effect (Additional Gloss Reduction)"));
        DrawHelpToggle(
            "MatteEffect",
            L("Further reduces gloss on top of the Glossiness control.",
              "Further reduces gloss on top of the Glossiness control."),
            MessageType.Info);
    }

    private void DrawMakeupTexturesSection()
    {
        SetFoldout("MakeupTextures", DrawBoxedSection(L("追加テクスチャ (2nd-5th)", "Additional Textures (2nd-5th)"), GetFoldout("MakeupTextures"), SectionCategory.Basic));
        if (GetFoldout("MakeupTextures"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ レイヤースタジオで開く", "→ Open in Layer Studio",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.EffectStudio, 4, targetMaterial));
            }
            EditorGUILayout.BeginHorizontal();
            string[] layerNames = { "2nd", "3rd", "4th", "5th" };
            string[] layerKeywords = { "_2ND_TEXTURE", "_3RD_TEXTURE", "_4TH_TEXTURE", "_5TH_TEXTURE" };
            for (int i = 0; i < layerNames.Length; i++)
            {
                bool layerOn = targetMaterial.IsKeywordEnabled(layerKeywords[i]);
                Color badgeCol = layerOn ? new Color(0.2f, 0.7f, 0.3f, 0.9f) : new Color(0.4f, 0.4f, 0.4f, 0.4f);
                Color textCol = layerOn ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                string label = $"{layerNames[i]}:{(layerOn ? "ON" : "OFF")}";
                Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.miniLabel, GUILayout.Height(18));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rect, badgeCol);
                }

                Color oldColor = GUI.contentColor;
                GUI.contentColor = textCol;
                GUI.Label(rect, label, layerOn ? CachedMakeupBadgeStyleOn : CachedMakeupBadgeStyleOff);
                GUI.contentColor = oldColor;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(SECTION_SPACING);

            DrawHelpToggle(
                "MakeupTextures",
                L("追加テクスチャを最大 4 枚まで重ねられます。適用範囲はアルファチャンネルで制御します。",
                  "Composite up to four additional textures. The alpha channel controls the application area."),
                MessageType.None);

            EditorGUILayout.Space(SECTION_SPACING);
            DrawMakeupTextureLayer("2nd", "2ND_TEXTURE", "2ND_TEX_MASK", L("Highlights, Eye Shadow", "Highlights, Eye Shadow"));
            EditorGUILayout.Space(10);
            DrawMakeupTextureLayer("3rd", "3RD_TEXTURE", "3RD_TEX_MASK", L("Blush", "Blush"));
            EditorGUILayout.Space(10);
            DrawMakeupTextureLayer("4th", "4TH_TEXTURE", "4TH_TEX_MASK", L("Glitter, Shadow", "Glitter, Shadow"));
            EditorGUILayout.Space(10);
            DrawMakeupTextureLayer("5th", "5TH_TEXTURE", "5TH_TEX_MASK", L("Detail Highlights, Detail Shadows", "Detail Highlights, Detail Shadows"));
        }
        EndBoxedSection(GetFoldout("MakeupTextures"));
    }

    /// <summary>
    /// Helper method to draw a single makeup texture layer with all its properties.
    /// </summary>
    private void DrawMakeupTextureLayer(string layerName, string textureKeyword, string maskKeyword, string usageHint)
    {
        bool useTexture = DrawToggle($"_{textureKeyword}", $"_Use{layerName}Texture", L($"Enable {layerName} Texture", $"Enable {layerName} Texture"));
        if (!useTexture)
        {
            return;
        }

        EditorGUILayout.Space(SECTION_SPACING);
        EditorGUILayout.LabelField(L($"{layerName} Texture Settings", $"{layerName} Texture Settings"), EditorStyles.boldLabel);

        DrawProperty($"_{layerName}Tex", $"{layerName} Texture");
        DrawProperty($"_{layerName}TexHueShift", "Hue Shift");
        DrawProperty($"_{layerName}TexSaturation", "Saturation");
        DrawProperty($"_{layerName}TexValue", "Brightness");
        DrawHelpToggle(
            "MakeupTextureHSV",
            L("Adjust hue, saturation, and brightness for this layer.",
              "Adjust hue, saturation, and brightness for this layer."),
            MessageType.None);

        DrawProperty($"_{layerName}TexIntensity", L("Intensity", "Intensity"));
        DrawProperty($"_{layerName}TexBlendMode", L("Blend Mode", "Blend Mode"));
        DrawHelpToggle(
            "MakeupTextureBlendMode",
            L($"Choose how the layer blends. Suggested usage: {usageHint}.",
              $"Choose how the layer blends. Suggested usage: {usageHint}."),
            MessageType.Info);

        EditorGUILayout.Space();
        DrawProperty($"_{layerName}TexMask", L($"{layerName} Tex Mask", $"{layerName} Tex Mask"));
        DrawHelpToggle(
            "MakeupTextureMask",
            L("White applies the texture. Black disables it. The mask is multiplied by the texture alpha.",
              "White applies the texture. Black disables it. The mask is multiplied by the texture alpha."),
            MessageType.Info);

        DrawUVAnimationSettings($"_{layerName}TexScrollSpeed", $"_{layerName}TexRotateSpeed", $"{layerName} Texture");
    }

    private void DrawScreenToneSection()
    {
        SetFoldout("ScreenTone", DrawBoxedSection(L("スクリーントーン (ハーフトーン重ね)", "Screen Tone (Halftone Overlay)"), GetFoldout("ScreenTone"), SectionCategory.Basic, "_SCREEN_TONE"));
        if (GetFoldout("ScreenTone"))
        {
            bool enableScreenTone = DrawToggle("_SCREEN_TONE", "_ScreenTone", L("スクリーントーンを有効化", "Enable Screen Tone"));
            if (enableScreenTone)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_ScreenToneColor", L("Tone Color", "Tone Color"));
                DrawProperty("_ScreenToneMask", L("Mask Texture", "Mask Texture"));
                DrawProperty("_ScreenToneScale", L("Pattern Size (Dot Size)", "Pattern Size (Dot Size)"));
                DrawProperty("_ScreenToneThreshold", L("Dot Density (0=None to 1=Full)", "Dot Density (0=None to 1=Full)"));
                DrawHelpToggle(
                    "ScreenTone",
                    L("マテリアル表面にスクリーントーン風の重ね表現を加えます。",
                      "Applies a halftone overlay to the material surface."),
                    MessageType.Info);
                DrawBlendControls(materialEditor, targetMaterial, "_ScreenToneBlend", "_ScreenToneBlendMode", "_ScreenToneBlur");
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("ScreenTone"));
    }

    private void DrawShadingSection()
    {
        SetFoldout("Shading", DrawBoxedSection(L("Toon Shading", "Toon Shading"), GetFoldout("Shading"), SectionCategory.Shading));
        if (GetFoldout("Shading"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ シャドウスタジオで開く", "→ Open in Shadow Studio",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.EffectStudio, 0, targetMaterial));
            }
            DrawLilToonMigrationCardIfNeeded();

            if (IsLilToonMigratedMaterial(targetMaterial))
            {
                EditorGUILayout.Space(8);
            }

            // Delegate content to helper class for better code organization
            DrawLookMixerControls();

            EditorGUILayout.Space(8);

            NataneToonShaderGUIHelpers.DrawShadingSectionContent(
                targetMaterial,
                properties,
                DrawToggle,
                DrawProperty,
                DrawHelpToggle,
                FindProperty
            );

            // Shadow Map auto-generation button
            EditorGUILayout.Space(8);
            bool shadowHasMesh = NataneMeshAnalyzer.FindMeshForMaterial(targetMaterial) != null;
            EditorGUI.BeginDisabledGroup(!shadowHasMesh);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L("Shadow Map を自動生成", "Auto-Generate Shadow Map"), GUILayout.Height(22), GUILayout.Width(200)))
            {
                MapGeneratorGUIBridge.GenerateShadowForMaterial(targetMaterial);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (!shadowHasMesh)
            {
                EditorGUILayout.HelpBox(L("シーン内にメッシュが必要です", "Mesh required in scene"), MessageType.Info);
            }
            EditorGUI.EndDisabledGroup();
        }
        EndBoxedSection(GetFoldout("Shading"));
    }

    private void DrawLilToonMigrationCardIfNeeded()
    {
        if (!IsLilToonMigratedMaterial(targetMaterial))
        {
            return;
        }

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.75f, 1.0f, 0.3f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = prevBg;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            L("lilToon からの移行マテリアル", "Migrated from lilToon"),
            EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(L("再最適化", "Re-optimize"), EditorStyles.miniButton))
        {
            quickSetupWizardMode = 3;
            SetFoldout("QuickSetup", true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawLookMixerControls()
    {
        MaterialProperty lookModeProp = FindProperty("_LookMode", properties, false);
        MaterialProperty toonWeightProp = FindProperty("_ToonWeight", properties, false);
        MaterialProperty nprWeightProp = FindProperty("_NprWeight", properties, false);
        MaterialProperty pbrWeightProp = FindProperty("_PbrWeight", properties, false);

        if (lookModeProp == null || toonWeightProp == null || nprWeightProp == null || pbrWeightProp == null)
        {
            EditorGUILayout.HelpBox(
                L("このマテリアルには Look Mixer プロパティがありません。シェーダーが Look Mixer に対応しているか確認してください。",
                  "Look Mixer properties are not available on this material. Please verify the shader supports Look Mixer."),
                MessageType.Warning);
            return;
        }

        ResolveLookMixerForDisplay(
            lookModeProp,
            toonWeightProp,
            nprWeightProp,
            pbrWeightProp,
            out int currentLookMode,
            out float currentToon,
            out float currentNpr,
            out float currentPbr,
            out bool isLegacy);

        EditorGUILayout.LabelField(L("見た目ミキサー", "Look Mixer"), EditorStyles.boldLabel);
        DrawHelpToggle(
            "LookMixer",
            L("Toon と PBR はベースの陰影バランス、NPR はその上に重ねる作風エフェクトの強さです。 (Ctrl+Z で元に戻せます)",
              "Toon and PBR blend the base shading. NPR controls the post-style stack layered on top. (Ctrl+Z to undo)"),
            MessageType.None);

        if (isLegacy)
        {
            EditorGUILayout.HelpBox(
                L("レガシーモードです。スライダー操作で自動的に切り替わります。",
                  "Legacy mode. Adjusting sliders will auto-upgrade the mode."),
                MessageType.Warning);

            if (GUILayout.Button(L("→ 明示モードに切り替え", "→ Switch to Explicit Mode"), GUILayout.Height(22)))
            {
                LookMode suggested = SuggestLookMode(currentToon, currentNpr, currentPbr);
                if (suggested == LookMode.Legacy) suggested = LookMode.Hybrid;
                ApplyLookMixerToSelectedMaterials(
                    "Upgrade to Explicit LookMode",
                    suggested,
                    currentToon,
                    currentNpr,
                    currentPbr);
                materialEditor?.Repaint();
            }
        }

        EditorGUI.BeginChangeCheck();
        int nextLookMode = EditorGUILayout.Popup(L("見た目プリセット", "Look Mode"), currentLookMode, GetLookModeLabels());
        float nextToon = EditorGUILayout.Slider(L("トゥーン寄り", "Toon Weight"), currentToon, 0f, 1f);
        float nextNpr = EditorGUILayout.Slider(L("作風エフェクト", "NPR Weight"), currentNpr, 0f, 1f);
        float nextPbr = EditorGUILayout.Slider(L("立体感", "PBR Weight"), currentPbr, 0f, 1f);

        if (EditorGUI.EndChangeCheck())
        {
            LookMode resolvedLookMode = nextLookMode <= (int)LookMode.Legacy
                ? SuggestLookMode(nextToon, nextNpr, nextPbr)
                : (LookMode)nextLookMode;

            // F-6: Auto-upgrade from Legacy mode when sliders are adjusted
            if (isLegacy && resolvedLookMode != LookMode.Legacy)
            {
                foreach (UnityEngine.Object target in materialEditor.targets)
                {
                    Material mat = target as Material;
                    if (mat != null && mat.HasProperty("_LookMode"))
                    {
                        Undo.RecordObject(mat, "Adjust Look Mixer");
                        mat.SetFloat("_LookMode", (float)resolvedLookMode);
                    }
                }
            }

            ApplyLookMixerToSelectedMaterials(
                "Adjust Look Mixer",
                resolvedLookMode,
                nextToon,
                nextNpr,
                nextPbr);
            materialEditor?.Repaint();
        }
    }

    private void DrawLilToonParityFlagLine(LilToonParityFlags currentFlags, LilToonParityFlags flag, string message)
    {
        if ((currentFlags & flag) == 0)
        {
            return;
        }

        EditorGUILayout.LabelField($"- {message}", EditorStyles.miniLabel);
    }

    private void OpenLilToonMigrationTool()
    {
        const string migrationToolTypeName = "NataneToon.Editor.LilToonMigrationTool, NataneToon.Editor.Migration";
        try
        {
            var migrationToolType = Type.GetType(migrationToolTypeName);
            if (migrationToolType != null)
            {
                var showWindowMethod = migrationToolType.GetMethod(
                    "ShowWindow",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (showWindowMethod != null)
                {
                    showWindowMethod.Invoke(null, null);
                    return;
                }

                EditorWindow.GetWindow(migrationToolType);
                return;
            }
        }
        catch (ExitGUIException) { throw; }
        catch (Exception ex)
        {
            EditorGUILayout.HelpBox(
                L("lilToon Migration ツールの読み込みに失敗しました: " + ex.Message,
                  "Failed to load lilToon Migration tool: " + ex.Message),
                MessageType.Error);
            return;
        }

        if (NataneToolMenuPaths.TryOpenByToolKey("LilToonMigration"))
        {
            return;
        }

        EditorUtility.DisplayDialog(
            L("LilToon Migration", "LilToon Migration"),
            L("Could not open the lilToon migration tool. Please check whether the migration assembly compiled successfully.", "Could not open the lilToon migration tool. Please check whether the migration assembly compiled successfully."),
            "OK");
    }

    private void DrawLookMixerPresetButtons()
    {
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("Starter Presets", "Starter Presets"), EditorStyles.miniBoldLabel);

            bool narrowLayout = EditorGUIUtility.currentViewWidth < 430f;

            if (narrowLayout)
            {
                DrawLookMixerPresetButton(L("Pure Toon", "Pure Toon"), "Apply Pure Toon Look", LookMode.Toon, 1f, 0f, 0f, 0f);
                DrawLookMixerPresetButton(L("Soft NPR", "Soft NPR"), "Apply Soft NPR Look", LookMode.NPR, 0.85f, 1f, 0.1f, 1f);
                DrawLookMixerPresetButton(L("Toon-PBR Hybrid", "Toon-PBR Hybrid"), "Apply Toon-PBR Hybrid Look", LookMode.Hybrid, 0.8f, 0.2f, 0.6f, 0f);
                DrawLookMixerPresetButton(L("Near PBR", "Near PBR"), "Apply Near PBR Look", LookMode.PBR, 0.15f, 0.1f, 1f, 3f);

                // Game character style (applies comprehensive settings beyond LookMixer weights)
                if (GUILayout.Button(L("⭐ ゲームキャラクター風", "⭐ Game Character Style"), GUILayout.Height(24)))
                {
                    ApplyGameCharacterLookMixerToSelectedMaterials();
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawLookMixerPresetButton(L("Pure Toon", "Pure Toon"), "Apply Pure Toon Look", LookMode.Toon, 1f, 0f, 0f, 0f);
            DrawLookMixerPresetButton(L("Soft NPR", "Soft NPR"), "Apply Soft NPR Look", LookMode.NPR, 0.85f, 1f, 0.1f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawLookMixerPresetButton(L("Toon-PBR Hybrid", "Toon-PBR Hybrid"), "Apply Toon-PBR Hybrid Look", LookMode.Hybrid, 0.8f, 0.2f, 0.6f, 0f);
            DrawLookMixerPresetButton(L("Near PBR", "Near PBR"), "Apply Near PBR Look", LookMode.PBR, 0.15f, 0.1f, 1f, 3f);
            EditorGUILayout.EndHorizontal();

            // Game character style (applies comprehensive settings beyond LookMixer weights)
            if (GUILayout.Button(L("⭐ ゲームキャラクター風", "⭐ Game Character Style"), GUILayout.Height(24)))
            {
                ApplyGameCharacterLookMixerToSelectedMaterials();
            }
        }
    }

    private void DrawLookMixerPresetButton(
        string label,
        string undoLabel,
        LookMode lookMode,
        float toonWeight,
        float nprWeight,
        float pbrWeight,
        float shadingModeOverride)
    {
        if (GUILayout.Button(label, GUILayout.Height(24)))
        {
            ApplyLookMixerToSelectedMaterials(
                undoLabel,
                lookMode,
                toonWeight,
                nprWeight,
                pbrWeight,
                shadingModeOverride);
        }
    }

    /// <summary>
    /// Applies game-character-style preset to all selected materials via Look Mixer.
    /// Sets LookMode to Toon, applies comprehensive shading/rim/specular/outline,
    /// then synchronizes keywords.
    /// </summary>
    private void ApplyGameCharacterLookMixerToSelectedMaterials()
    {
        if (materialEditor == null || materialEditor.targets == null || materialEditor.targets.Length == 0)
        {
            return;
        }

        Undo.RecordObjects(materialEditor.targets, "Apply Game Character Style");

        foreach (UnityEngine.Object target in materialEditor.targets)
        {
            Material material = target as Material;
            if (material == null)
            {
                continue;
            }

            // Set LookMixer weights (Pure Toon base)
            ApplyLookMixerValuesToMaterial(material, LookMode.Toon, 1f, 0f, 0f, 0f);
            // Apply comprehensive game character shading parameters
            ApplyGameCharacterStyle(material);
        }

        SynchronizeKeywordsAndRefreshInspectorCaches();
    }

    private void ResolveLookMixerForDisplay(
        MaterialProperty lookModeProp,
        MaterialProperty toonWeightProp,
        MaterialProperty nprWeightProp,
        MaterialProperty pbrWeightProp,
        out int lookMode,
        out float toonWeight,
        out float nprWeight,
        out float pbrWeight,
        out bool isLegacy)
    {
        lookMode = Mathf.RoundToInt(lookModeProp.floatValue);
        toonWeight = Mathf.Clamp01(toonWeightProp.floatValue);
        nprWeight = Mathf.Clamp01(nprWeightProp.floatValue);
        pbrWeight = Mathf.Clamp01(pbrWeightProp.floatValue);
        isLegacy = lookMode <= (int)LookMode.Legacy;

        if (!isLegacy || targetMaterial == null)
        {
            return;
        }

        ResolveLegacyLookMixer(targetMaterial, out toonWeight, out nprWeight, out pbrWeight);
    }

    private void ResolveLegacyLookMixer(Material material, out float toonWeight, out float nprWeight, out float pbrWeight)
    {
        bool legacyPbr =
            (material.HasProperty("_ShadingMode") && material.GetFloat("_ShadingMode") >= 2.5f) ||
            material.IsKeywordEnabled("_PBR_LIKE") ||
            material.IsKeywordEnabled("_PBR");

        toonWeight = legacyPbr ? 0f : 1f;
        pbrWeight = legacyPbr ? 1f : 0f;
        nprWeight = HasLookMixerNprFeatures(material) ? 1f : 0f;
    }

    private bool HasLookMixerNprFeatures(Material material)
    {
        foreach (string keyword in LookMixerNprKeywords)
        {
            if (material.IsKeywordEnabled(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private LookMode SuggestLookMode(float toonWeight, float nprWeight, float pbrWeight)
    {
        if (pbrWeight >= 0.75f && toonWeight <= 0.25f && nprWeight <= 0.25f)
        {
            return LookMode.PBR;
        }

        if (nprWeight >= 0.75f && pbrWeight <= 0.25f)
        {
            return LookMode.NPR;
        }

        if (toonWeight >= 0.75f && nprWeight <= 0.25f && pbrWeight <= 0.25f)
        {
            return LookMode.Toon;
        }

        return LookMode.Hybrid;
    }

    private bool IsLilToonMigratedMaterial(Material material)
    {
        if (material == null)
        {
            return false;
        }

        if (material.HasProperty("_LilToonMigrated") && material.GetFloat("_LilToonMigrated") > 0.5f)
        {
            return true;
        }

        if (material.HasProperty("_LilToonMigrationMode") && material.GetFloat("_LilToonMigrationMode") > 0.5f)
        {
            return true;
        }

        if (material.HasProperty("_LilToonParityFlags") && material.GetFloat("_LilToonParityFlags") > 0.5f)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(material.GetTag("NataneLilToonSourceShader", false, string.Empty)))
        {
            return true;
        }

        return false;
    }

    private float GetNataneShadingModeForMaterial(Material material)
    {
        if (material == null)
        {
            return 0f;
        }

        bool pbrLikeSignal =
            material.IsKeywordEnabled("_PBR_LIKE") ||
            material.IsKeywordEnabled("_PBR");
        if (pbrLikeSignal)
        {
            return 3f;
        }

        int lookMode = material.HasProperty("_LookMode")
            ? Mathf.RoundToInt(material.GetFloat("_LookMode"))
            : (int)LookMode.Legacy;
        float nprWeight = material.HasProperty("_NprWeight") ? Mathf.Clamp01(material.GetFloat("_NprWeight")) : 0f;
        float pbrWeight = material.HasProperty("_PbrWeight") ? Mathf.Clamp01(material.GetFloat("_PbrWeight")) : 0f;

        switch ((LookMode)lookMode)
        {
            case LookMode.PBR:
                return 3f;
            case LookMode.NPR:
                return 1f;
            case LookMode.Hybrid:
                return pbrWeight >= 0.6f ? 3f : 1f;
            case LookMode.Legacy:
                if (material.HasProperty("_STShadowBlur"))
                {
                    float compatibilityBlur = Mathf.Clamp01(material.GetFloat("_STShadowBlur"));
                    if (compatibilityBlur >= 0.18f)
                    {
                        return 1f;
                    }
                }

                if (pbrWeight >= 0.6f || material.IsKeywordEnabled("_PBR_LIKE") || material.IsKeywordEnabled("_PBR"))
                {
                    return 3f;
                }

                if (nprWeight >= 0.35f || HasLookMixerNprFeatures(material))
                {
                    return 1f;
                }
                break;
        }

        return 0f;
    }

    private LilToonMigrationMode GetLilToonMigrationMode(Material material)
    {
        if (material == null || !material.HasProperty("_LilToonMigrationMode"))
        {
            return LilToonMigrationMode.Unknown;
        }

        return (LilToonMigrationMode)Mathf.RoundToInt(material.GetFloat("_LilToonMigrationMode"));
    }

    private LilToonParityFlags GetLilToonParityFlags(Material material)
    {
        if (material == null || !material.HasProperty("_LilToonParityFlags"))
        {
            return LilToonParityFlags.None;
        }

        return (LilToonParityFlags)Mathf.RoundToInt(material.GetFloat("_LilToonParityFlags"));
    }

    private int CountLilToonParityFlags(LilToonParityFlags flags)
    {
        int value = (int)flags;
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private string GetLilToonSourceShader(Material material)
    {
        if (material == null)
        {
            return "lilToon";
        }

        string sourceShader = material.GetTag("NataneLilToonSourceShader", false, string.Empty);
        return string.IsNullOrEmpty(sourceShader) ? "lilToon" : sourceShader;
    }

    private string GetLilToonMigrationVersion(Material material)
    {
        if (material == null)
        {
            return string.Empty;
        }

        return material.GetTag("NataneLilToonMigrationVersion", false, string.Empty);
    }

    private string GetLilToonMigrationModeLabel(LilToonMigrationMode mode)
    {
        switch (mode)
        {
            case LilToonMigrationMode.ExactCompatibility:
                return L("(廃止)", "(Deprecated)");
            case LilToonMigrationMode.VisualMatch:
                return L("見た目寄せ", "Visual Match");
            case LilToonMigrationMode.MinimalSafe:
                return L("安全寄り", "Minimal Safe");
            default:
                return L("不明", "Unknown");
        }
    }

    private string[] GetLookModeLabels()
    {
        return new[]
        {
            L("自動判定", "Legacy (Auto)"),
            L("トゥーン", "Toon"),
            L("作風エフェクト", "NPR"),
            L("立体感重視", "PBR"),
            L("ハイブリッド", "Hybrid")
        };
    }

    private void ApplyLookMixerToSelectedMaterials(
        string undoLabel,
        LookMode lookMode,
        float toonWeight,
        float nprWeight,
        float pbrWeight,
        float? shadingModeOverride = null)
    {
        if (materialEditor == null || materialEditor.targets == null || materialEditor.targets.Length == 0)
        {
            return;
        }

        Undo.RecordObjects(materialEditor.targets, undoLabel);

        foreach (UnityEngine.Object target in materialEditor.targets)
        {
            Material material = target as Material;
            if (material == null)
            {
                continue;
            }

            ApplyLookMixerValuesToMaterial(material, lookMode, toonWeight, nprWeight, pbrWeight, shadingModeOverride);
            ApplyLookMixerShadingParams(material, lookMode);
            EditorUtility.SetDirty(material);
        }

        if (targetMaterial != null)
        {
            SynchronizeKeywordsAndRefreshInspectorCaches();
        }
    }

    private void ApplyLookMixerValuesToMaterial(
        Material material,
        LookMode lookMode,
        float toonWeight,
        float nprWeight,
        float pbrWeight,
        float? shadingModeOverride = null)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_LookMode")) material.SetFloat("_LookMode", (float)lookMode);
        if (material.HasProperty("_ToonWeight")) material.SetFloat("_ToonWeight", Mathf.Clamp01(toonWeight));
        if (material.HasProperty("_NprWeight")) material.SetFloat("_NprWeight", Mathf.Clamp01(nprWeight));
        if (material.HasProperty("_PbrWeight")) material.SetFloat("_PbrWeight", Mathf.Clamp01(pbrWeight));
        if (shadingModeOverride.HasValue && material.HasProperty("_ShadingMode")) material.SetFloat("_ShadingMode", shadingModeOverride.Value);
    }

    /// <summary>
    /// Applies shading parameters that correspond to a Look Mixer preset.
    /// This ensures that switching presets produces a visible difference
    /// by adjusting shadow, softness, and blend parameters.
    /// </summary>
    private void ApplyLookMixerShadingParams(Material material, LookMode lookMode)
    {
        if (material == null)
        {
            return;
        }

        float shadowSteps, shadowSharpness, shadingGradientWidth, litSoftness, wrapAmount, shadowBlend;

        switch (lookMode)
        {
            case LookMode.Toon:
                shadowSteps = 3f;
                shadowSharpness = 0.05f;
                shadingGradientWidth = 0.2f;
                litSoftness = 0f;
                wrapAmount = 0f;
                shadowBlend = 0f;
                break;
            case LookMode.NPR:
                shadowSteps = 2f;
                shadowSharpness = 0.3f;
                shadingGradientWidth = 0.65f;
                litSoftness = 0.3f;
                wrapAmount = 0.15f;
                shadowBlend = 0.2f;
                break;
            case LookMode.Hybrid:
                shadowSteps = 2f;
                shadowSharpness = 0.15f;
                shadingGradientWidth = 0.45f;
                litSoftness = 0.2f;
                wrapAmount = 0.2f;
                shadowBlend = 0.3f;
                break;
            case LookMode.PBR:
                shadowSteps = 1f;
                shadowSharpness = 0.8f;
                shadingGradientWidth = 0.8f;
                litSoftness = 0.5f;
                wrapAmount = 0.3f;
                shadowBlend = 0.5f;
                break;
            default:
                // Legacy or unknown — do not touch shading params
                return;
        }

        if (material.HasProperty("_ShadowSteps")) material.SetFloat("_ShadowSteps", shadowSteps);
        if (material.HasProperty("_ShadowSharpness")) material.SetFloat("_ShadowSharpness", shadowSharpness);
        if (material.HasProperty("_ShadingGradientWidth")) material.SetFloat("_ShadingGradientWidth", shadingGradientWidth);
        if (material.HasProperty("_LitSoftness")) material.SetFloat("_LitSoftness", litSoftness);
        if (material.HasProperty("_WrapAmount")) material.SetFloat("_WrapAmount", wrapAmount);
        if (material.HasProperty("_ShadowBlend")) material.SetFloat("_ShadowBlend", shadowBlend);
    }

    private void DrawAdvancedLightingSection()
    {
        SetFoldout("AdvancedLighting", DrawBoxedSection(L("ライティング詳細", "Advanced Lighting"), GetFoldout("AdvancedLighting"), SectionCategory.Lighting));
        if (GetFoldout("AdvancedLighting"))
        {

            // Soft Lighting Mode
            DrawSubGroupHeader(L("ソフトライティングモード", "Soft Lighting Mode"));
            bool softLightingMode = DrawToggle("_SOFT_LIGHTING_MODE", "_SoftLightingMode", L("ソフトライティングモード（グローバル）", "Soft Lighting Mode (Global)"));
            if (softLightingMode)
            {
                DrawProperty("_SoftLightingIntensity", L("ソフトライティング強度", "Soft Lighting Intensity"));
                DrawHelpToggle("SoftLightingMode",
                    L("🌟 ソフトライティングモード:\n" +
                    "すべてのライティングとシェーディングを柔らかくします。\n" +
                    "このモードを有効にすると、影の境界、ライトの強さ、\n" +
                    "ハイライトなどが全体的に柔らかく調整されます。\n\n" +
                    "• 強度 0.3-0.5 = 適度な柔らかさ（推奨）\n" +
                    "• 強度 0.5-0.7 = かなり柔らかい\n" +
                    "• 強度 0.7-1.0 = 非常に柔らかい\n\n" +
                    "💡 イラスト調や水彩画風の柔らかな表現に最適です。",
                    "🌟 Soft Lighting Mode:\n" +
                    "Softens all lighting and shading globally.\n" +
                    "When enabled, shadow boundaries, light intensity,\n" +
                    "and highlights are all softened.\n\n" +
                    "• Intensity 0.3-0.5 = Moderate softness (Recommended)\n" +
                    "• Intensity 0.5-0.7 = Quite soft\n" +
                    "• Intensity 0.7-1.0 = Very soft\n\n" +
                    "💡 Ideal for illustration or watercolor-style soft expressions."),
                    MessageType.Info);
                EditorGUILayout.Space();
            }

            // Global Light Controls
            DrawSubGroupHeader(L("グローバルライト制御", "Global Light Controls"));
            DrawProperty("_LightIntensity", L("ライト強度（グローバル）", "Light Intensity (Global)"));
            DrawHelpToggle("LightIntensity", L("全体的なライティングの強さを制御します。0 = ライトなし、1 = 標準、1.73 = 移行補正値、5 = 最大", "Controls overall lighting strength. 0 = No light, 1 = Standard, 1.73 = Migration correction, 5 = Maximum"), MessageType.Info);

            DrawProperty("_IndirectLightIntensity", L("間接光の強度", "Indirect Light Intensity"));
            DrawHelpToggle("IndirectLightIntensity", L("環境光やライトプローブからの間接照明の強さを制御します。", "Controls the intensity of indirect illumination from ambient light and light probes."), MessageType.Info);

            DrawProperty("_GIIntensity", L("GI強度（環境反射）", "GI Intensity (Environment Reflection)"));
            DrawHelpToggle("GIIntensity", L("環境反射（Light Probes/GI）の影響度を制御します。\n• 0 = 環境反射を完全に無効化（環境光の影響を受けない）\n• 0.5 = 環境反射を50%に軽減\n• 1 = 通常通り環境反射を適用\nVRChatで暗いワールドやライティングが強すぎるワールドで、見た目を安定させるために使用します。", "Controls the influence of environment reflection (Light Probes/GI).\n• 0 = Fully disable environment reflection\n• 0.5 = Reduce to 50%\n• 1 = Apply normally\nUse to stabilize appearance in dark or overly bright VRChat worlds."), MessageType.Info);

            // Indirect Lighting Controls
            DrawColorProperty("_IndirectLightMinColor", L("間接光の最低色", "Indirect Light Minimum Color"));
            DrawHelpToggle("IndirectLightMinColor", L("間接光の最低保証カラーです。\n暗いワールドでもキャラクターが真っ黒にならないよう、間接光の下限を設定します。\n• 黒 (0,0,0) = 制限なし（環境光に完全依存）\n• 暗いグレー = 最低限の明るさを保証\nNatural ライティングパイプラインで使用されます。", "Minimum guaranteed indirect light color.\nSets a lower bound so characters don't turn completely black in dark worlds.\n• Black (0,0,0) = No limit (fully depends on ambient)\n• Dark gray = Guarantees minimum brightness\nUsed in the Natural lighting pipeline."), MessageType.Info);

            DrawProperty("_ShadowEnvStrength", L("影への環境色反映", "Shadow Environment Color"));
            DrawHelpToggle("ShadowEnvStrength", L("影の色に環境光の色をどの程度反映するかを制御します。\n• 0 = 影は純粋に暗くなるだけ（従来の挙動）\n• 1 = 影に環境光の色が完全に反映される\n環境光の色味を影にも反映させることで、より自然なライティングを実現します。", "Controls how much ambient color is reflected in shadows.\n• 0 = Shadows simply darken (traditional behavior)\n• 1 = Ambient color fully reflected in shadows\nReflecting ambient color in shadows achieves more natural lighting."), MessageType.Info);

            DrawProperty("_LightColorInfluence", L("ライトカラー影響度", "Light Color Influence"));
            DrawHelpToggle("LightColorInfluence", L("ライトの色がマテリアルに与える影響を制御します。\n• 0 = ライトの色を無視（白色光として処理）\n• 1 = ライトの色を完全に反映\n• 0.5 = 中間（推奨）", "Controls how much light color affects the material.\n• 0 = Ignore light color (treat as white)\n• 1 = Fully reflect light color\n• 0.5 = Middle (Recommended)"), MessageType.Info);

            EditorGUILayout.Space(10);
            DrawSubGroupHeader(L("シャドウ設定", "Shadow Settings"));
            DrawProperty("_ShadowReceive", L("影の受け取り", "Shadow Receive"));
            DrawHelpToggle("ShadowReceive", L("他のオブジェクトからの影がこのマテリアルに与える影響を制御します。1 = 完全な影、0 = 影なし。", "Controls how shadows from other objects affect this material. 1 = Full shadow, 0 = No shadow."), MessageType.Info);

            DrawProperty("_ShadowSmoothing", L("影のスムージング", "Shadow Smoothing"));
            DrawHelpToggle("ShadowSmoothing",
                L("シャドウスムージング:\n" +
                "影のジャギーや諧調をなじませます。\n\n" +
                "【多段階トゥーンシェーディング】\n" +
                "ステップ境界の諧調を連続的なグラデーションに\n" +
                "ブレンドし、段差を目立たなくします。\n\n" +
                "【シャドウマップ（ディレクショナルライト）】\n" +
                "PCF 9タップで影エッジを滑らかにします。\n\n" +
                "【シャドウマップ（ポイント/スポットライト）】\n" +
                "適応型スムージングで影エッジをぼかします。\n\n" +
                "• 0 = スムージングなし（デフォルト）\n" +
                "• 0.1-0.3 = 軽いスムージング（推奨）\n" +
                "• 0.5-1.0 = 強いスムージング\n\n" +
                "多段階影の諧調が気になる場合や、影エッジが\n" +
                "ギザギザな場合に使用してください。",
                "Shadow Smoothing:\n" +
                "Blends shadow jaggies and banding for smoother results.\n\n" +
                "[Multi-step Toon Shading]\n" +
                "Blends step boundaries into continuous gradations\n" +
                "to reduce visible steps.\n\n" +
                "[Shadow Map (Directional Light)]\n" +
                "Smooths shadow edges with PCF 9-tap.\n\n" +
                "[Shadow Map (Point/Spot Light)]\n" +
                "Blurs shadow edges with adaptive smoothing.\n\n" +
                "• 0 = No smoothing (Default)\n" +
                "• 0.1-0.3 = Light smoothing (Recommended)\n" +
                "• 0.5-1.0 = Heavy smoothing\n\n" +
                "Use when multi-step shadow banding is visible or\n" +
                "shadow edges appear jagged."),
                MessageType.Info);

            // PCSS (Percentage Closer Soft Shadows)
            EditorGUILayout.Space(5);
            NataneToonShaderGUIHelpers.DrawPCSSControls(DrawToggle, DrawProperty, DrawHelpToggle);

            DrawProperty("_ShadowMaxDarkness", L("影の最大暗さ", "Shadow Maximum Darkness"));
            DrawHelpToggle("ShadowMaxDarkness", L("影の最小明るさです。0 = 完全に暗い、1 = 暗くならない。影が真っ黒になりすぎるのを防ぎます。", "Minimum shadow brightness. 0 = Fully dark, 1 = No darkening. Prevents shadows from becoming too black."), MessageType.Info);

            EditorGUILayout.Space(10);
            DrawSubGroupHeader(L("ライトカラー制限", "Light Color Limits"));
            DrawProperty("_LightColorMin", L("ライト色の下限", "Light Color Min"));
            DrawProperty("_LightColorMax", L("ライト色の上限", "Light Color Max"));
            DrawHelpToggle("LightColorLimits", L(
                "ライトカラーの値域をクランプします（lilToonの_LightMinLimit/_LightMaxLimitに相当）。\n" +
                "• 下限 (default=0): 暗いワールドでもキャラが最低限見える保証\n" +
                "  - 0.05 = VRChat推奨（真っ暗を防止）\n" +
                "• 上限 (default=1): 強いライトでテクスチャが白飛びするのを防止\n" +
                "  - 1.0 = ライト色がそのまま（標準）\n" +
                "  - 0.8 = やや抑えめ（白飛び防止）",
                "Clamps effective light color (equivalent to lilToon's _LightMinLimit/_LightMaxLimit).\n" +
                "• Min (default=0): Guarantees minimum visibility in dark worlds\n" +
                "  - 0.05 = VRChat recommended (prevents total darkness)\n" +
                "• Max (default=1): Prevents texture white-out under bright lights\n" +
                "  - 1.0 = Light color as-is (standard)\n" +
                "  - 0.8 = Slightly reduced (prevents blowout)"), MessageType.Info);
            DrawProperty("_MonochromeLighting", L("モノクロライティング", "Monochrome Lighting"));
            DrawHelpToggle("MonochromeLighting", L(
                "ライトカラーの色味を除去してグレースケール化します（lilToonの_MonochromeLightingに相当）。\n" +
                "• 0 = ライトの色をそのまま適用\n" +
                "• 1 = ライトの明るさのみ（色なし）\n" +
                "色付きライトでテクスチャの色味が変わりすぎる場合に使用します。",
                "Removes color tint from light and converts to grayscale (equivalent to lilToon's _MonochromeLighting).\n" +
                "• 0 = Apply light color as-is\n" +
                "• 1 = Light brightness only (no color)\n" +
                "Use when colored lights distort texture colors too much."), MessageType.Info);

            EditorGUILayout.Space(10);
            DrawSubGroupHeader(L("ライト影響範囲", "Light Influence Range"));
            DrawProperty("_LightMinInfluence", L("ライトの最小影響", "Light Min Influence"));
            DrawProperty("_LightMaxInfluence", L("ライトの最大影響", "Light Max Influence"));
            DrawHelpToggle("LightInfluenceRange", L("直接光の輝度の最小/最大を制御します。最小値は暗くなりすぎを防ぎ、最大値は露出オーバーを防ぎます。", "Controls min/max luminance of the direct light. Min prevents excessive darkness, max prevents overexposure."), MessageType.Info);
            DrawProperty("_DirectLightClampMode", L("直接光クランプ方式", "Direct Light Clamp Mode"));
            DrawHelpToggle("DirectLightClampMode", L(
                "Legacy: 従来の normalize() ベース。白色光でも輝度が約0.58倍になり、ライトの色によって明るさが変わります。既存マテリアルの見た目を維持する互換モードです。\n" +
                "HuePreserving: 色相を保ったまま輝度のみをクランプします。白色光は等倍で通り、色付きライトも明るさが揃います。新規マテリアルにはこちらを推奨します。",
                "Legacy: original normalize()-based clamp. White light comes out at ~0.58x and brightness depends on the light's hue. Compatibility mode that keeps existing materials unchanged.\n" +
                "HuePreserving: clamps luminance only while keeping the hue. White light passes at 1x and colored lights keep consistent brightness. Recommended for new materials."), MessageType.Info);

            EditorGUILayout.Space();
            DrawBlendParameter(
                "_LightBlend",
                L("ライトのなじませ（柔らかさ）", "Light Blending (Softness)"),
                L("✨ ライトのなじませ調整:\n" +
                "ライティングの変化を周囲となじませて、より滑らかにします。\n" +
                "• 0 = シャープな変化（デフォルト）\n" +
                "• 0.3-0.5 = 適度な柔らかさ（推奨）\n" +
                "• 0.7-1.0 = 非常に柔らかい変化\n\n" +
                "💡 ライトの強弱が急激すぎる場合に調整してください。",
                "✨ Light Blending Adjustment:\n" +
                "Blends lighting changes with surroundings for smoother results.\n" +
                "• 0 = Sharp changes (Default)\n" +
                "• 0.3-0.5 = Moderate softness (Recommended)\n" +
                "• 0.7-1.0 = Very soft changes\n\n" +
                "💡 Adjust when light intensity changes are too abrupt."));

            EditorGUILayout.Space();
            DrawBlendParameter(
                "_HighlightSoftness",
                L("ハイライトの柔らかさ", "Highlight Softness"),
                L("✨ ハイライトのなじませ調整:\n" +
                "明るい部分（ハイライト）を周囲となじませます。\n" +
                "• 0 = シャープなハイライト（デフォルト）\n" +
                "• 0.3-0.5 = 適度な柔らかさ（推奨）\n" +
                "• 0.7-1.0 = 非常に柔らかいハイライト\n\n" +
                "💡 ハイライトが強すぎる場合や、\n" +
                "より柔らかい印象が欲しい場合に調整してください。",
                "✨ Highlight Blending Adjustment:\n" +
                "Blends bright areas (highlights) with surroundings.\n" +
                "• 0 = Sharp highlights (Default)\n" +
                "• 0.3-0.5 = Moderate softness (Recommended)\n" +
                "• 0.7-1.0 = Very soft highlights\n\n" +
                "💡 Adjust when highlights are too strong or\n" +
                "you want a softer impression."));

            // 3.1 Parameter Interaction Warning: Edge Softness System
            float shadowBlend = targetMaterial.GetFloat("_ShadowBlend");
            float lightBlend = targetMaterial.GetFloat("_LightBlend");
            float highlightSoftness = targetMaterial.GetFloat("_HighlightSoftness");

            int softnessCount = 0;
            if (shadowBlend > 0.5f) softnessCount++;
            if (lightBlend > 0.5f) softnessCount++;
            if (highlightSoftness > 0.5f) softnessCount++;

            if (softnessCount >= 3)
            {
                EditorGUILayout.HelpBox(
                    L("⚠️ 過度なソフトネス警告\n\n" +
                    "複数のソフトネスパラメータが高い値です。\n" +
                    "シェーディングが非常にぼやけます。\n\n" +
                    "推奨:\n" +
                    "メインパラメータ1-2個を選び、\n" +
                    "他は 0.3 以下に抑えてください。",
                    "⚠️ Excessive Softness Warning\n\n" +
                    "Multiple softness parameters are set to high values.\n" +
                    "Shading will become very blurry.\n\n" +
                    "Recommended:\n" +
                    "Choose 1-2 main parameters and\n" +
                    "keep others below 0.3."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawProperty("_BacklightIntensity", L("逆光の強さ", "Backlight Intensity"));
            if (targetMaterial.GetFloat("_BacklightIntensity") > 0)
            {
                DrawColorProperty("_BacklightColor", L("逆光の色", "Backlight Color"));
                DrawHelpToggle("Backlight", L("逆光はオブジェクトの背後に光がある時に照明を追加し、リムライトのような効果を作ります。", "Backlight adds illumination when light comes from behind the object, creating a rim-light-like effect."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_BacklightBlend", "_BacklightBlendMode", "_BacklightBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_BacklightDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }
            }

            EditorGUILayout.Space();
            DrawProperty("_AdditionalLightIntensity", L("追加ライトの強さ", "Additional Light Intensity"));
            DrawHelpToggle("AdditionalLight", L("追加ライト（ForwardAddパス）の強度を制御します。低い値は複数のライトを使用する際の明るくなりすぎを防ぎます。0 = 追加ライトなし、1 = 最大強度。", "Controls additional light (ForwardAdd pass) intensity. Lower values prevent over-brightness with multiple lights. 0 = No additional lights, 1 = Max intensity."), MessageType.Info);

            // --- Vertex Light Settings ---
            EditorGUILayout.Space();
            DrawSubGroupHeader(L("頂点ライト設定", "Vertex Light Settings"));

            bool enablePixelVertexLights = DrawToggle("_PIXEL_VERTEX_LIGHTS", "_UsePixelVertexLights", L("ピクセル精度の頂点ライト", "Pixel-Precision Vertex Lights"));
            DrawHelpToggle("PixelVertexLights",
                L("頂点ライト（最大4灯の追加Point/Spotライト）をフラグメントシェーダーでトゥーンスタイルに計算します。\n" +
                "品質は向上しますが、負荷が増加します。\n" +
                "オフの場合でも頂点ライトは通常精度（頂点補間）で処理されます。\n" +
                "※ ForwardBaseパスでのみ有効です。",
                "Calculates vertex lights (up to 4 additional Point/Spot lights) in toon-style in the fragment shader.\n" +
                "Quality improves but increases GPU load.\n" +
                "When off, vertex lights are still processed at normal precision (vertex interpolation).\n" +
                "※ Only active in ForwardBase pass."),
                MessageType.Info);
        }
        EndBoxedSection(GetFoldout("AdvancedLighting"));
    }

    // The GUI only needs package presence, so keep this independent from detector side effects.
    private static bool IsVRCLightVolumesPackageInstalled()
    {
        return NataneDependencyStatus.IsInstalled(NataneDependencyStatus.VRCLightVolumes);
    }

    private static bool IsLTCGIPackageInstalled()
    {
        return NataneDependencyStatus.IsInstalled(NataneDependencyStatus.LTCGI);
    }

    private static bool IsMaterialToggleEnabled(Material material, string propertyName)
    {
        return material != null &&
               material.HasProperty(propertyName) &&
               material.GetFloat(propertyName) > FLOAT_COMPARISON_THRESHOLD;
    }

    private static void OpenDependencySetupWindow()
    {
        NataneDependencySetupWindow.ShowWindow();
    }

    private static void DrawDependencyInstallStatus()
    {
        if (_dismissedDependencyWarning || !NataneDependencyInstaller.HasStatusMessage)
        {
            return;
        }

        EditorGUILayout.HelpBox(NataneDependencyInstaller.StatusMessage, NataneDependencyInstaller.StatusType);
        if (GUILayout.Button(L("この通知を閉じる", "Dismiss"), EditorStyles.miniButton, GUILayout.Width(80)))
        {
            _dismissedDependencyWarning = true;
        }
    }

    private static void DrawDependencyActionButtons(in NataneDependencyInfo dependency, bool drawStatusMessage = true)
    {
        if (drawStatusMessage)
        {
            DrawDependencyInstallStatus();
        }

        EditorGUILayout.LabelField(dependency.DisplayName, EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(L("VCC Listing", "VCC Listing"), GUILayout.Height(20)))
            {
                NataneDependencyInstaller.OpenVccListing(dependency);
            }

            using (new EditorGUI.DisabledScope(NataneDependencyInstaller.IsInstallInProgress))
            {
                string installLabel = NataneDependencyInstaller.IsInstallInProgress
                    ? L("インストール中...", "Installing...")
                    : L("UPM Git で導入", "Install via UPM Git");
                if (GUILayout.Button(installLabel, GUILayout.Height(20)))
                {
                    NataneDependencyInstaller.TryStartInstall(dependency);
                }
            }
        }

        if (GUILayout.Button(L("依存セットアップを開く", "Open Dependency Setup"), GUILayout.Height(20)))
        {
            OpenDependencySetupWindow();
        }
    }

    private void DrawDependencyInspectorWarnings()
    {
        bool missingLightVolumePackage = IsMaterialToggleEnabled(targetMaterial, "_UseLightVolume") &&
                                         !IsVRCLightVolumesPackageInstalled();
        bool missingLtcgiPackage = IsMaterialToggleEnabled(targetMaterial, "_LTCGI") &&
                                   !IsLTCGIPackageInstalled();

        if (!missingLightVolumePackage && !missingLtcgiPackage)
        {
            return;
        }

        DrawDependencyInstallStatus();

        if (missingLightVolumePackage)
        {
            EditorGUILayout.HelpBox(
                L("このマテリアルは Light Volume を有効化しています。VRC Light Volumes パッケージは未導入ですが、同梱版フォールバックで動作は継続します。完全対応に寄せるなら package の導入を推奨します。",
                  "This material has Light Volume enabled. The VRC Light Volumes package is missing, but the bundled fallback remains active. Install the package if you want full package integration."),
                MessageType.Warning);
            DrawDependencyActionButtons(NataneDependencyStatus.VRCLightVolumes, false);
            EditorGUILayout.Space(4);
        }

        if (missingLtcgiPackage)
        {
            EditorGUILayout.HelpBox(
                L("このマテリアルは LTCGI を有効化していますが、LTCGI パッケージが未導入です。設定は保持されますが、効果は無効のままです。",
                  "This material has LTCGI enabled, but the LTCGI package is missing. Settings are preserved, but the effect stays disabled."),
                MessageType.Warning);
            DrawDependencyActionButtons(NataneDependencyStatus.LTCGI, false);
        }

        EditorGUILayout.Space(6);
    }

    // Shader name mapping: non-Lite → Lite counterpart (for GrabPass-free performance).
    // Background and ScreenEdge Split intentionally omitted (no Lite counterpart exists).
    private static readonly System.Collections.Generic.Dictionary<string, string> s_LiteShaderCounterparts =
        new System.Collections.Generic.Dictionary<string, string>
        {
            { "Natane/Toon Shader",               "Natane/Toon Shader (Lite)" },
            { "Natane/Toon Shader (Cutout)",      "Natane/Toon Shader (Cutout Lite)" },
            { "Natane/Toon Shader (Transparent)", "Natane/Toon Shader (Transparent Lite)" },
            { "Natane/Toon Shader (Fur)",         "Natane/Toon Shader (Fur Lite)" },
        };

    // Keys are the float property names (source of truth per keyword synchronizer).
    private static readonly string[] s_GrabPassFeatureToggles =
    {
        "_Refraction",
        "_UseSoftFilter",
        "_UseKuwahara",
        "_UseColorBleeding",
        "_UseChromaticAberration",
    };

    private void DrawGrabPassSuggestionBanner()
    {
        if (targetMaterial == null || targetMaterial.shader == null) return;

        // Only suggest when the current shader is a non-Lite variant that has a Lite counterpart.
        if (!s_LiteShaderCounterparts.TryGetValue(targetMaterial.shader.name, out string liteShaderName))
            return;

        // Skip the banner if any GrabPass-dependent feature is actually in use.
        foreach (var prop in s_GrabPassFeatureToggles)
        {
            if (IsMaterialToggleEnabled(targetMaterial, prop))
                return;
        }

        EditorGUILayout.HelpBox(
            L(
                "このマテリアルは GrabPass 依存機能（屈折/ソフトフィルタ/クワハラ/カラーブリーディング/色収差）を使用していません。\n" +
                "Lite バリアントに切り替えると GrabPass が無くなり、画面コピーのコストを丸ごと削減できます（VRChat Quest 向けに特に有効）。",
                "This material doesn't use any GrabPass-dependent feature (Refraction/Soft Filter/Kuwahara/Color Bleeding/Chromatic Aberration).\n" +
                "Switch to the Lite variant to drop the GrabPass and save a full-screen copy per frame (especially valuable on VRChat Quest)."),
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L($"Lite バリアントに変換 ({liteShaderName})", $"Convert to Lite ({liteShaderName})"), GUILayout.Height(22)))
            {
                ConvertTargetsToLite(liteShaderName);
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.Space(6);
    }

    private void ConvertTargetsToLite(string liteShaderName)
    {
        Shader liteShader = Shader.Find(liteShaderName);
        if (liteShader == null)
        {
            Debug.LogError($"[NataneToonShader] Lite シェーダーが見つかりません: {liteShaderName}");
            return;
        }

        foreach (Material mat in GetAllTargetMaterials())
        {
            if (mat == null || mat.shader == null) continue;
            if (!s_LiteShaderCounterparts.TryGetValue(mat.shader.name, out string expectedLite)) continue;
            if (expectedLite != liteShaderName) continue;  // Mixed-shader selection — only convert matching ones.

            Undo.RecordObject(mat, L("Lite バリアントに変換", "Convert to Lite Variant"));
            mat.shader = liteShader;
            EditorUtility.SetDirty(mat);
        }
    }

    private void DrawSamplerBudgetInspectorWarning()
    {
        var estimate = GetCurrentSamplerBudgetEstimate();
        if (!estimate.IsWarning && !estimate.HasLightVolumeLtcgiCombo && !estimate.HasCriticalLightingCombo && !estimate.HasScreenSpaceLightingCombo)
        {
            return;
        }

        if (estimate.IsOverLimit)
        {
            EditorGUILayout.HelpBox(
                L(
                    $"このマテリアルは推定 Sampler 上限を超えています ({estimate.EstimatedSamplers}/{estimate.Limit})。\n見た目は保持されますが、新しい重い機能は有効化できません。不要な機能を無効化して上限内に戻してください。",
                    $"This material is over the estimated sampler limit ({estimate.EstimatedSamplers}/{estimate.Limit}).\nThe current look is preserved, but new heavy features cannot be enabled. Disable some features to get back under the limit."),
                MessageType.Warning);
            return;
        }

        if (estimate.IsNearLimit || estimate.HasCriticalLightingCombo || estimate.HasScreenSpaceLightingCombo)
        {
            EditorGUILayout.HelpBox(
                L(
                    $"推定 Sampler 数が上限付近です ({estimate.EstimatedSamplers}/{estimate.Limit})。Light Volume / LTCGI / Screen Edge などの重い機能は、組み合わせ次第で有効化できなくなります。",
                    $"Estimated sampler usage is near the limit ({estimate.EstimatedSamplers}/{estimate.Limit}). Heavy features such as Light Volume, LTCGI, or Screen Edge may become unavailable depending on the combination."),
                MessageType.Warning);
            return;
        }

        if (estimate.HasLightVolumeLtcgiCombo)
        {
            EditorGUILayout.HelpBox(
                L(
                    "Light Volume と LTCGI を同時に使う場合は Sampler 予算に注意してください。さらに重い機能を足すと追加できなくなることがあります。",
                    "When using Light Volume and LTCGI together, watch the sampler budget closely. Adding more heavy features may be blocked."),
                MessageType.Info);
        }
    }

    private void DrawLightVolumeSection()
    {
        SetFoldout("LightVolume", DrawBoxedSection(L("VRC ライトボリューム", "VRC Light Volumes"), GetFoldout("LightVolume"), SectionCategory.Lighting, "_USE_LIGHT_VOLUME"));
        if (GetFoldout("LightVolume"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ VRChat統合で開く", "→ Open VRChat Integration",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.VRChatIntegration, 0, targetMaterial));
            }
            bool packageInstalled = IsVRCLightVolumesPackageInstalled();
            if (packageInstalled)
            {
                EditorGUILayout.HelpBox(
                    L("VRC Light Volumes パッケージ: 検出済み\n" +
                      "パッケージ版 LightVolumes.cginc を使用します（対応ワールドで自動的に動作）",
                      "VRC Light Volumes Package: Detected\n" +
                      "Using the package LightVolumes.cginc (works automatically in compatible worlds)"),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    L("VRC Light Volumes パッケージ: 未検出\n" +
                      "同梱版 LightVolumes.cginc を使用します。\n\n" +
                      "パッケージ版を使うには:\n" +
                      "VCC から red.sim.lightvolumes をインストール\n" +
                      "https://redsim.github.io/vpmlisting/",
                      "VRC Light Volumes Package: Not Detected\n" +
                      "Using the bundled LightVolumes.cginc.\n\n" +
                      "To use the package version:\n" +
                      "Install red.sim.lightvolumes from VCC\n" +
                      "https://redsim.github.io/vpmlisting/"),
                    MessageType.Info);
                DrawDependencyActionButtons(NataneDependencyStatus.VRCLightVolumes);
            }

            EditorGUILayout.Space(3);

            bool enableLightVolume = DrawToggle("_USE_LIGHT_VOLUME", "_UseLightVolume", L("Light Volumeを有効化", "Enable Light Volume"));
            if (enableLightVolume)
            {
                EditorGUI.indentLevel++;

                DrawHelpToggle("LightVolumeIntro",
                    L("VRC Light Volumes はボクセルベースの次世代ライティングシステムです。\n" +
                      "対応ワールドでは高品質な局所照明を自動的に受け取れます。\n",
                      "VRC Light Volumes is a voxel-based next-generation lighting system.\n" +
                      "High-quality local lighting is applied automatically in compatible worlds.\n") +
                    (packageInstalled
                        ? L("現在はパッケージ版 LightVolumes.cginc を使用しています。", "Currently using the package LightVolumes.cginc.")
                        : L("現在は同梱版 LightVolumes.cginc を使用しています。", "Currently using the bundled LightVolumes.cginc.")),
                    MessageType.Info);

                if (!packageInstalled)
                {
                    EditorGUILayout.HelpBox(
                        L("パッケージ未導入のため、現在は同梱版 LightVolumes.cginc を使用しています。対応ワールドでは動作しますが、完全対応に寄せるなら red.sim.lightvolumes の導入を推奨します。",
                          "The package is not installed, so the bundled LightVolumes.cginc is active. It still works in compatible worlds, but installing red.sim.lightvolumes is recommended for full integration."),
                        MessageType.Warning);
                }

                EditorGUILayout.Space();
                DrawProperty("_LightVolumeIntensity", L("Light Volume 強度", "Light Volume Intensity"));
                DrawHelpToggle("LightVolumeIntensity",
                    L("Light Volume ライティングの強度を制御します。\n1 = 完全強度、0 = 無効。",
                      "Controls Light Volume lighting intensity.\n1 = Full strength, 0 = Disabled."),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawProperty("_LightVolumeBlendMode", L("ブレンドモード", "Blend Mode"));
                DrawHelpToggle("LightVolumeBlendMode",
                    L("Light Volume ブレンドモード:\n" +
                      "・ Add(0): 直接光を加算し、間接光をリム風に合成\n" +
                      "・ Multiply(1): 既存ライティングに乗算\n" +
                      "・ Replace(2): Light Volume の結果で置き換え\n" +
                      "・ Natural(3): 間接光として max() 合成に参加\n" +
                      "  環境色を全メッシュへ安定して反映したい場合に推奨",
                      "Light Volume Blend Mode:\n" +
                      "- Add(0): Adds direct light and uses indirect light like rim lighting\n" +
                      "- Multiply(1): Multiplies with existing lighting\n" +
                      "- Replace(2): Replaces lighting with the Light Volume result\n" +
                      "- Natural(3): Participates in max() composition as indirect light\n" +
                      "  Recommended when you want stable environment color across meshes"),
                    MessageType.Info);

                EditorGUILayout.Space();
                bool enableSpecular = DrawToggle("_LIGHT_VOLUME_SPECULAR", "_LightVolumeSpecular", L("Light Volume スペキュラー", "Light Volume Specular"));
                if (enableSpecular)
                {
                    DrawHelpToggle("LightVolumeSpecular",
                        L("Light Volume から色付きスペキュラーを生成します。\n",
                          "Generates colored specular from Light Volume.\n") +
                        (packageInstalled
                            ? L("パッケージ版 LightVolumeSpecular を使用しています。アバター用途に推奨です。",
                                "Using the package LightVolumeSpecular function. Recommended for avatars.")
                            : L("同梱版 LightVolumeSpecular を使用しています。",
                                "Using the bundled LightVolumeSpecular function.")),
                        MessageType.Info);
                }

                EditorGUILayout.Space();
                DrawHelpToggle("LightVolumeNotes",
                    L("注意:\n" +
                      "・ワールド側とアバター側の両方が対応している必要があります\n" +
                      "・非対応環境では LightVolumes.cginc 側で Unity Light Probes に自動フォールバックします\n" +
                      "・ハッシュタグ #VRCLightVolumesReady で対応ワールドを探せます",
                      "Notes:\n" +
                      "- Both the world and avatar must support VRC Light Volumes\n" +
                      "- In unsupported environments, LightVolumes.cginc falls back to Unity light probes automatically\n" +
                      "- Search compatible worlds with #VRCLightVolumesReady") +
                    (packageInstalled
                        ? string.Empty
                        : L("\n・Tools > Natane > VRChat > VRC Light Volumes 再検出 から手動検出できます",
                            "\n- Manual detection is available via Tools > Natane > VRChat > VRC Light Volumes Re-detect")),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_LightVolumeBlend");
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("LightVolume"));
    }

    private void DrawLTCGISection()
    {
        SetFoldout("LTCGI", DrawBoxedSection(L("LTCGI（リアルタイムエリアライト）", "LTCGI (Real-time Area Light)"), GetFoldout("LTCGI"), SectionCategory.Lighting, "_LTCGI"));
        if (GetFoldout("LTCGI"))
        {
            bool packageInstalled = IsLTCGIPackageInstalled();
            if (packageInstalled)
            {
                EditorGUILayout.HelpBox(
                    L("LTCGI パッケージ: 検出済み\n" +
                      "LTCGI.cginc を使用します（対応ワールドで自動的に動作）",
                      "LTCGI Package: Detected\n" +
                      "Using LTCGI.cginc (works automatically in compatible worlds)"),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    L("LTCGI パッケージ: 未検出\n" +
                      "lilToon と同様に、パッケージ未導入時は LTCGI 機能は無効です。\n" +
                      "本物の LTCGI を使うには VCC から at.pimaker.ltcgi をインストールしてください。",
                      "LTCGI Package: Not Detected\n" +
                      "LTCGI stays disabled when the package is missing, matching lilToon behavior.\n" +
                      "Install at.pimaker.ltcgi from VCC to use real LTCGI."),
                    MessageType.Info);
                DrawDependencyActionButtons(NataneDependencyStatus.LTCGI);
            }

            EditorGUILayout.Space(3);

            bool enableLTCGI = DrawToggle("_LTCGI", "_LTCGI", L("LTCGIを有効化", "Enable LTCGI"));
            if (enableLTCGI)
            {
                EditorGUI.indentLevel++;

                DrawHelpToggle("LTCGIIntro",
                    packageInstalled
                        ? L("LTCGI は Linearly Transformed Cosines によるリアルタイムエリアライトシステムです。\n" +
                            "対応ワールドではスクリーンやエリアライトからの照明を自動で受け取ります。",
                            "LTCGI is a real-time area light system based on Linearly Transformed Cosines.\n" +
                            "Compatible worlds can illuminate the avatar from screens and area lights automatically.")
                        : L("LTCGI パッケージが未導入のため、lilToon と同様に機能は無効です。\n" +
                            "設定値は保持されますが、見た目には反映されません。\n\n" +
                            "本物の LTCGI を使うには at.pimaker.ltcgi をインストールしてください。",
                            "The LTCGI package is not installed, so the feature stays disabled just like lilToon.\n" +
                            "Settings are preserved, but no visual effect will appear.\n\n" +
                            "Install at.pimaker.ltcgi to use real LTCGI."),
                    MessageType.Info);

                if (!packageInstalled)
                {
                    EditorGUILayout.HelpBox(
                        L("LTCGI パッケージ未導入のため、現在の設定は見た目に反映されません。at.pimaker.ltcgi を導入するまで LTCGI は無効のままです。",
                          "The LTCGI package is missing, so the current settings do not affect the rendered result. LTCGI remains disabled until at.pimaker.ltcgi is installed."),
                        MessageType.Warning);
                }

                EditorGUILayout.Space();
                DrawProperty("_LTCGIIntensity", L("LTCGI 強度", "LTCGI Intensity"));
                DrawHelpToggle("LTCGIIntensity",
                    L("LTCGI ライティング全体の強度を制御します。\n1 = 完全強度、0 = 無効。",
                      "Controls the overall LTCGI lighting intensity.\n1 = Full strength, 0 = Disabled."),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawProperty("_LTCGISpecular", L("LTCGI スペキュラー", "LTCGI Specular"));
                DrawHelpToggle("LTCGISpecular",
                    L("LTCGI 由来のスペキュラー強度を制御します。\nエリアライトの映り込み表現に使います。",
                      "Controls specular intensity contributed by LTCGI.\nUsed for area-light reflection rendering."),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawHelpToggle("LTCGINotes",
                    L("注意:\n" +
                      "・ワールド側に LTCGI の設定が必要です\n" +
                      "・スクリーン、エリアライトなどのリアルタイム照明に対応します\n" +
                      "・AudioLink 対応ワールドでは音連動演出も可能です\n" +
                      "・Tools > Natane > VRChat > LTCGI 再検出 から手動検出できます",
                      "Notes:\n" +
                      "- LTCGI must be configured in the world\n" +
                      "- Supports real-time illumination from screens, area lights, and similar sources\n" +
                      "- AudioLink-compatible worlds can drive music-linked lighting\n" +
                      "- Manual detection is available via Tools > Natane > VRChat > LTCGI Re-detect"),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_LTCGIBlend", "_LTCGIBlendMode");
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("LTCGI"));
    }

    private void DrawSpecularSection()
    {
        SetFoldout("Specular", DrawBoxedSection(L("スペキュラー反射", "Specular Reflection"), GetFoldout("Specular"), SectionCategory.Effects, "_SPECULAR"));
        if (GetFoldout("Specular"))
        {

            bool enableSpecular = DrawToggle("_SPECULAR", "_Specular", L("スペキュラーを有効化", "Enable Specular"));

            if (enableSpecular)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_SpecularColor", L("スペキュラーの色", "Specular Color"));
                DrawProperty("_SpecularSize", L("スペキュラーのサイズ", "Specular Size"));
                DrawProperty("_SpecularSoftness", L("スペキュラーの柔らかさ", "Specular Softness"));
                DrawProperty("_SpecularIntensity", L("スペキュラー強度", "Specular Intensity"));

                EditorGUILayout.Space();
                DrawProperty("_SpecularMask", L("スペキュラーマスク", "Specular Mask"));
                DrawHelpToggle("SpecularMask", L("白 = スペキュラーあり、黒 = スペキュラーなし", "White = Specular on, Black = Specular off"), MessageType.Info);
                DrawUVAnimationSettings("_SpecularMaskScrollSpeed", "_SpecularMaskRotateSpeed", L("スペキュラーマスク", "Specular Mask"));

                DrawBlendControls(materialEditor, targetMaterial, "_SpecularBlend", "_SpecularBlendMode", "_SpecularBlur");

                // 境界ディザリング
                EditorGUILayout.Space(SECTION_SPACING);
                bool enableDither = DrawToggle("_SPECULAR_DITHER", "_SpecularDither",
                    L("境界ディザリング", "Boundary Dithering"));
                if (enableDither)
                {
                    DrawProperty("_SpecularDitherScale",
                        L("ディザリングスケール", "Dither Scale"));
                    DrawProperty("_SpecularDitherStrength",
                        L("ディザリング強度", "Dither Strength"));
                }

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_SpecularDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                // Roughness Map auto-generation button
                EditorGUILayout.Space(SECTION_SPACING);
                bool specRoughnessHasAlbedo = targetMaterial != null && targetMaterial.HasProperty("_MainTex") && targetMaterial.GetTexture("_MainTex") != null;
                EditorGUI.BeginDisabledGroup(!specRoughnessHasAlbedo);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("Roughness を自動生成", "Auto-Generate Roughness"), GUILayout.Height(22), GUILayout.Width(200)))
                {
                    MapGeneratorGUIBridge.GenerateRoughnessForMaterial(targetMaterial);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Specular"));
    }

    private void DrawHairSpecularSection()
    {
        SetFoldout("HairSpecular", DrawBoxedSection(L("ヘアハイライト（Kajiya-Kay）", "Hair Highlight (Kajiya-Kay)"), GetFoldout("HairSpecular"), SectionCategory.Effects, "_HAIR_SPECULAR"));
        if (GetFoldout("HairSpecular"))
        {

            bool enableHairSpec = DrawToggle("_HAIR_SPECULAR", "_HairSpecular", L("ヘアスペキュラーを有効化", "Enable Hair Specular"));

            if (enableHairSpec)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField(L("プライマリローブ", "Primary Lobe"), EditorStyles.boldLabel);
                DrawColorProperty("_HairSpecColor1", L("プライマリスペキュラー色", "Primary Specular Color"));
                DrawProperty("_HairSpecShift1", L("プライマリタンジェントシフト", "Primary Tangent Shift"));
                DrawProperty("_HairSpecWidth1", L("プライマリスペキュラー幅", "Primary Specular Width"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("セカンダリローブ", "Secondary Lobe"), EditorStyles.boldLabel);
                DrawColorProperty("_HairSpecColor2", L("セカンダリスペキュラー色", "Secondary Specular Color"));
                DrawProperty("_HairSpecShift2", L("セカンダリタンジェントシフト", "Secondary Tangent Shift"));
                DrawProperty("_HairSpecWidth2", L("セカンダリスペキュラー幅", "Secondary Specular Width"));

                EditorGUILayout.Space(SECTION_SPACING);
                DrawProperty("_HairSpecIntensity", L("ヘアスペキュラー強度", "Hair Specular Intensity"));

                EditorGUILayout.Space(SECTION_SPACING);
                DrawProperty("_HairSpecMask", L("ヘアスペキュラーマスク", "Hair Specular Mask"));
                DrawProperty("_HairSpecShiftTex", L("シフトテクスチャ", "Shift Texture"));

                bool flowHasMesh = NataneMeshAnalyzer.FindMeshForMaterial(targetMaterial) != null;
                EditorGUI.BeginDisabledGroup(!flowHasMesh);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("Flow Map を自動生成", "Auto-Generate Flow Map"), GUILayout.Height(22), GUILayout.Width(200)))
                {
                    bool ok = NataneToonMapGenerator.TryGenerateFlowMap(targetMaterial, out string msg);
                    EditorUtility.DisplayDialog(ok ? L("完了", "Done") : L("失敗", "Failed"), msg, L("閉じる", "Close"));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                if (!flowHasMesh)
                {
                    EditorGUILayout.HelpBox(L("シーンにこのマテリアルを使用しているメッシュが必要です。", "A mesh using this material must be present in the scene."), MessageType.Info);
                }

                DrawBlendControls(materialEditor, targetMaterial, "_HairSpecBlend", "_HairSpecBlendMode", null);

                DrawHelpToggle("HairSpecular",
                    L("💇 ヘアスペキュラー（Kajiya-Kay）:\n" +
                    "髪の毛専用の異方性スペキュラーハイライトです。\n" +
                    "「天使の輪」（エンジェルリング）効果を再現します。\n\n" +
                    "• プライマリローブ: メインのハイライト\n" +
                    "• セカンダリローブ: 補助的なハイライト\n" +
                    "• タンジェントシフト: ハイライトの位置をずらす\n" +
                    "• シフトテクスチャ: ピクセルごとのシフト制御\n\n" +
                    "💡 使い方:\n" +
                    "髪のマテリアルに使用し、光源に応じたリアルな\n" +
                    "髪のハイライトを表現します。",
                    "💇 Hair Specular (Kajiya-Kay):\n" +
                    "Anisotropic specular highlights specifically for hair.\n" +
                    "Reproduces the 'Angel Ring' (halo) effect.\n\n" +
                    "• Primary Lobe: Main highlight\n" +
                    "• Secondary Lobe: Secondary highlight\n" +
                    "• Tangent Shift: Shifts highlight position\n" +
                    "• Shift Texture: Per-pixel shift control\n\n" +
                    "💡 Usage:\n" +
                    "Use on hair materials for realistic\n" +
                    "light-responsive hair highlights."),
                    MessageType.Info);

                // 境界ディザリング（スペキュラーと共通設定）
                if (targetMaterial.IsKeywordEnabled("_SPECULAR_DITHER"))
                {
                    EditorGUILayout.Space(SECTION_SPACING);
                    EditorGUILayout.LabelField(
                        L("境界ディザリング（有効）", "Boundary Dithering (Active)"),
                        EditorStyles.miniLabel);
                }

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_HairSpecDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("HairSpecular"));
    }

    private void DrawRimLightSection()
    {
        SetFoldout("RimLight", DrawBoxedSection(L("リムライト（輪郭光）", "Rim Light (Edge Light)"), GetFoldout("RimLight"), SectionCategory.Effects, "_RIM_LIGHT"));
        if (GetFoldout("RimLight"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ リムライトスタジオで開く", "→ Open in RimLight Studio",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.EffectStudio, 2, targetMaterial));
            }

            bool enableRimLight = DrawToggle("_RIM_LIGHT", "_RimLight", L("リムライトを有効化", "Enable Rim Light"));

            if (enableRimLight)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_RimColor", L("リムライトの色", "Rim Light Color"));
                DrawProperty("_RimPower", L("リムライトのパワー", "Rim Light Power"));
                DrawProperty("_RimIntensity", L("リムライトの強さ", "Rim Light Intensity"));

                EditorGUILayout.Space();
                DrawProperty("_RimSpread", L("リムの広がり（グロー）", "Rim Spread (Glow)"));
                DrawHelpToggle("RimSpread",
                    L("✨ リムの広がり（グロー効果）:\n" +
                    "リムライトを滲ませて広げ、柔らかく発光している\n" +
                    "ような効果を追加します。\n\n" +
                    "• 0 = 広がりなし（シャープなリム）\n" +
                    "• 0.3-0.5 = 適度な広がり（推奨）\n" +
                    "• 0.7-1.0 = 大きな広がり（強いグロー）",
                    "✨ Rim Spread (Glow Effect):\n" +
                    "Bleeds and spreads the rim light for a soft\n" +
                    "glowing effect.\n\n" +
                    "• 0 = No spread (sharp rim)\n" +
                    "• 0.3-0.5 = Moderate spread (Recommended)\n" +
                    "• 0.7-1.0 = Large spread (strong glow)"),
                    MessageType.None);

                EditorGUILayout.Space();
                DrawProperty("_RimMask", L("リムマスク", "Rim Mask"));
                DrawHelpToggle("RimMask", L("白 = リムライトあり、黒 = リムライトなし", "White = Rim light on, Black = Rim light off"), MessageType.Info);
                DrawUVAnimationSettings("_RimMaskScrollSpeed", "_RimMaskRotateSpeed", L("リムマスク", "Rim Mask"));

                DrawBlendControls(materialEditor, targetMaterial, "_RimBlend", "_RimBlendMode", "_RimBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_RimDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Rim Light 2
            bool enableRimLight2 = DrawToggle("_RIM_LIGHT_2", "_RimLight2", L("リムライト2を有効化（多段階リム）", "Enable Rim Light 2 (Multi-layer)"));

            if (enableRimLight2)
            {
                EditorGUI.indentLevel++;
                DrawHelpToggle("RimLight2",
                    L("🌟 多段階リムライト:\n" +
                    "2つのリムライトレイヤーを重ねることで、\n" +
                    "より複雑で立体的なリムライト表現が可能です。\n\n" +
                    "💡 おすすめ設定:\n" +
                    "リムライト1: 明るい色、低いパワー（内側の輪郭）\n" +
                    "リムライト2: 淡い色、高いパワー（外側の輪郭）",
                    "🌟 Multi-layer Rim Light:\n" +
                    "Layer two rim lights for more complex\n" +
                    "and three-dimensional rim light effects.\n\n" +
                    "💡 Recommended:\n" +
                    "Rim Light 1: Bright color, low power (inner contour)\n" +
                    "Rim Light 2: Soft color, high power (outer contour)"),
                    MessageType.None);

                DrawColorProperty("_RimColor2", L("リムライト2の色", "Rim Light 2 Color"));
                DrawProperty("_RimPower2", L("リムライト2のパワー", "Rim Light 2 Power"));
                DrawProperty("_RimIntensity2", L("リムライト2の強さ", "Rim Light 2 Intensity"));

                EditorGUILayout.Space();
                DrawProperty("_RimSpread2", L("リムの広がり2（グロー）", "Rim Spread 2 (Glow)"));
                DrawHelpToggle("RimSpread2",
                    L("✨ リムの広がり（グロー効果）:\n" +
                    "リムライト2を滲ませて広げます。",
                    "✨ Rim Spread (Glow Effect):\n" +
                    "Bleeds and spreads Rim Light 2."),
                    MessageType.None);

                EditorGUILayout.Space();
                DrawProperty("_RimMask2", L("リムマスク2", "Rim Mask 2"));
                DrawHelpToggle("RimMask2", L("白 = リムライトあり、黒 = リムライトなし", "White = Rim light on, Black = Rim light off"), MessageType.Info);
                DrawUVAnimationSettings("_RimMask2ScrollSpeed", "_RimMask2RotateSpeed", L("リムマスク2", "Rim Mask 2"));

                DrawBlendControls(materialEditor, targetMaterial, "_RimBlend2", "_RimBlendMode2", "_Rim2Blur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_Rim2DistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Offset Rim Light
            bool enableOffsetRimLight = DrawToggle("_OFFSET_RIM_LIGHT", "_OffsetRimLight", L("オフセットリムライトを有効化", "Enable Offset Rim Light"));

            if (enableOffsetRimLight)
            {
                EditorGUI.indentLevel++;
                DrawHelpToggle("OffsetRimLight",
                    L("🌙 オフセットリムライト:\n" +
                    "通常のリムライトとは異なり、リムの発生位置を\n" +
                    "特定の方向にオフセット（偏移）させます。\n" +
                    "2Dアニメのような指向性のあるハイライト表現が可能です。\n\n" +
                    "💡 おすすめ設定:\n" +
                    "• X=0.3, Y=0.1: 右上からの光を表現\n" +
                    "• ライト方向連動ON: 自動的にライトに追従\n" +
                    "• シャープネス=0.5: トゥーン調のくっきりリム",
                    "🌙 Offset Rim Light:\n" +
                    "Unlike regular rim light, offsets the rim\n" +
                    "occurrence position in a specific direction.\n" +
                    "Enables directional highlights like 2D anime.\n\n" +
                    "💡 Recommended:\n" +
                    "• X=0.3, Y=0.1: Light from upper right\n" +
                    "• Light Direction Link ON: Auto-follows light\n" +
                    "• Sharpness=0.5: Crisp toon-style rim"),
                    MessageType.None);

                DrawColorProperty("_OffsetRimColor", L("オフセットリムカラー", "Offset Rim Color"));
                DrawProperty("_OffsetRimPower", L("パワー（幅）", "Power (Width)"));
                DrawProperty("_OffsetRimIntensity", L("強度", "Intensity"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(L("オフセット方向", "Offset Direction"), EditorStyles.boldLabel);
                DrawProperty("_OffsetRimOffsetX", L("X方向オフセット", "X Direction Offset"));
                DrawProperty("_OffsetRimOffsetY", L("Y方向オフセット", "Y Direction Offset"));
                DrawHelpToggle("OffsetRimDir",
                    L("📐 オフセット方向:\n" +
                    "ビュー空間でリムの発生位置をずらします。\n" +
                    "• X正 = 右方向にリム, X負 = 左方向\n" +
                    "• Y正 = 上方向にリム, Y負 = 下方向\n" +
                    "• 例: X=0.3, Y=0 で右側にリムが偏る",
                    "📐 Offset Direction:\n" +
                    "Shifts rim occurrence position in view space.\n" +
                    "• X+ = Rim to right, X- = to left\n" +
                    "• Y+ = Rim upward, Y- = downward\n" +
                    "• Example: X=0.3, Y=0 biases rim to the right"),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawProperty("_OffsetRimUseLightDir", L("ライト方向連動", "Light Direction Linked"));
                DrawProperty("_OffsetRimLightDirStrength", L("ライト方向の強さ", "Light Direction Strength"));
                DrawHelpToggle("OffsetRimLightDir",
                    L("💡 ライト方向連動:\n" +
                    "ONにすると、メインライトの方向に基づいて\n" +
                    "自動的にリムのオフセットが計算されます。\n" +
                    "手動オフセットと併用可能です。",
                    "💡 Light Direction Link:\n" +
                    "When ON, rim offset is automatically calculated\n" +
                    "based on main light direction.\n" +
                    "Can be used alongside manual offset."),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawProperty("_OffsetRimSharpness", L("シャープネス（トゥーン）", "Sharpness (Toon)"));
                DrawHelpToggle("OffsetRimSharpness",
                    L("✂ シャープネス:\n" +
                    "• 0 = 柔らかいグラデーション\n" +
                    "• 0.3-0.5 = 適度なトゥーン調（推奨）\n" +
                    "• 0.8-1.0 = くっきりセル調",
                    "✂ Sharpness:\n" +
                    "• 0 = Soft gradient\n" +
                    "• 0.3-0.5 = Moderate toon-style (Recommended)\n" +
                    "• 0.8-1.0 = Crisp cel-style"),
                    MessageType.Info);

                DrawProperty("_OffsetRimShadowMask", L("影マスク強度", "Shadow Mask Intensity"));
                DrawHelpToggle("OffsetRimShadowMask",
                    L("🌑 影マスク:\n" +
                    "影になっている部分のリムを抑制します。\n" +
                    "• 0 = 影でもリムが出る\n" +
                    "• 0.5 = 影部分で半減（推奨）\n" +
                    "• 1.0 = 影部分でリムが完全に消える",
                    "🌑 Shadow Mask:\n" +
                    "Suppresses rim in shadowed areas.\n" +
                    "• 0 = Rim appears even in shadow\n" +
                    "• 0.5 = Halved in shadow (Recommended)\n" +
                    "• 1.0 = Rim fully disappears in shadow"),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawProperty("_OffsetRimMask", L("マスクテクスチャ", "Mask Texture"));
                DrawHelpToggle("OffsetRimMask", L("白 = リムあり、黒 = リムなし", "White = Rim on, Black = Rim off"), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_OffsetRimBlend", "_OffsetRimBlendMode", "_OffsetRimBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_OffsetRimDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Rim Light Direction Masking (lilToon-style)
            DrawProperty("_RimDirStrength", L("ライト方向追従", "Light Direction Follow"));
            DrawProperty("_RimShadowMask", L("影マスク", "Shadow Mask"));
            DrawHelpToggle("RimDirMasking",
                L("💡 リムライト方向制御:\n" +
                "• ライト方向追従: ライトの方向にリムを追従させる強度\n" +
                "  0=全方向にリム、1=ライト側のみリム\n" +
                "• 影マスク: 影の部分でリムを抑制する強度\n" +
                "  0=影でもリム表示、1=影でリム消失\n\n" +
                "※ ポイントライト・スポットライトにも対応",
                "💡 Rim Light Direction Control:\n" +
                "• Light Direction Follow: Rim follows light direction\n" +
                "  0=Rim in all directions, 1=Rim only on lit side\n" +
                "• Shadow Mask: Suppresses rim in shadow\n" +
                "  0=Rim in shadow, 1=Rim disappears in shadow\n\n" +
                "※ Also supports Point/Spot lights"),
                MessageType.Info);

            EditorGUILayout.Space(SECTION_SPACING);

            // Rim Direction Control (manual)
            bool enableRimDirControl = DrawToggle("_RIM_DIRECTION_CONTROL", "_RimDirectionControl", L("手動方向制御を有効化", "Enable Manual Direction Control"));

            if (enableRimDirControl)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_RimLightDirection", L("リムライト方向", "Rim Light Direction"));
                DrawProperty("_RimDirectionRange", L("方向範囲", "Direction Range"));
                DrawHelpToggle("RimDirectionControl",
                    L("🧭 手動リム方向制御:\n" +
                    "リムライトの発生方向を手動で指定して制限します。\n" +
                    "ライトの位置に関係なく、特定の方向からのみ\n" +
                    "リムが見えるように制御します。\n\n" +
                    "• リムライト方向: リムを発生させる方向ベクトル\n" +
                    "• 方向範囲: 許容する角度の広さ（0=狭い、1=広い）",
                    "🧭 Manual Rim Direction Control:\n" +
                    "Manually specify and restrict rim light direction.\n" +
                    "Controls rim visibility from specific directions\n" +
                    "regardless of light position.\n\n" +
                    "• Rim Light Direction: Direction vector for rim generation\n" +
                    "• Direction Range: Allowed angle width (0=narrow, 1=wide)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("RimLight"));
    }

    private void DrawSSSSection()
    {
        SetFoldout("SSS", DrawBoxedSection(L("半透明表現（SSS）", "Subsurface Scattering (SSS)"), GetFoldout("SSS"), SectionCategory.Effects, "_SSS"));
        if (GetFoldout("SSS"))
        {

            bool enableSSS = DrawToggle("_SSS", "_SSS", L("SSSを有効化", "Enable SSS"));

            if (enableSSS)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_SSSColor", L("SSSの色", "SSS Color"));
                DrawProperty("_SSSIntensity", L("SSSの強さ", "SSS Intensity"));
                DrawProperty("_SSSPower", L("SSSのパワー", "SSS Power"));
                DrawProperty("_SSSDistortion", L("SSSの歪み", "SSS Distortion"));

                EditorGUILayout.Space();
                DrawProperty("_ThicknessMap", L("厚さマップ", "Thickness Map"));

                bool thicknessHasMesh = NataneMeshAnalyzer.FindMeshForMaterial(targetMaterial) != null;
                EditorGUI.BeginDisabledGroup(!thicknessHasMesh);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("Thickness を自動生成", "Auto-Generate Thickness"), GUILayout.Height(22), GUILayout.Width(200)))
                {
                    bool ok = NataneToonMapGenerator.TryGenerateThickness(targetMaterial, out string msg);
                    EditorUtility.DisplayDialog(ok ? L("完了", "Done") : L("失敗", "Failed"), msg, L("閉じる", "Close"));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                if (!thicknessHasMesh)
                {
                    EditorGUILayout.HelpBox(L("シーンにこのマテリアルを使用しているメッシュが必要です。", "A mesh using this material must be present in the scene."), MessageType.Info);
                }

                DrawHelpToggle("ThicknessMap", L("白 = 薄い（SSSが強い）、黒 = 厚い（SSSが弱い）", "White = Thin (strong SSS), Black = Thick (weak SSS)"), MessageType.Info);

                DrawProperty("_ThicknessScale", L("厚さのスケール", "Thickness Scale"));

                EditorGUILayout.Space();
                DrawProperty("_SSSMask", L("SSSマスク", "SSS Mask"));
                DrawHelpToggle("SSSMask", L("白 = SSSあり、黒 = SSSなし", "White = SSS on, Black = SSS off"), MessageType.Info);

                DrawHelpToggle("SSSInfo", L("SSSはオブジェクトを通過する光をシミュレートします。肌、葉、薄い素材に最適です。", "SSS simulates light passing through objects. Ideal for skin, leaves, and thin materials."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_SSSBlend", "_SSSBlendMode", "_SSSBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_SSSDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("SSS"));
    }

    private void DrawMatCapSection()
    {
        SetFoldout("MatCap", DrawBoxedSection(L("マットキャップ（MatCap）", "MatCap"), GetFoldout("MatCap"), SectionCategory.Effects, "_MATCAP"));
        if (GetFoldout("MatCap"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ MatCapスタジオで開く", "→ Open in MatCap Studio",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.EffectStudio, 1, targetMaterial));
            }

            bool enableMatCap = DrawToggle("_MATCAP", "_MatCap", L("MatCapを有効化", "Enable MatCap"));

            if (enableMatCap)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_MatCapTex", L("MatCapテクスチャ", "MatCap Texture"));
                DrawProperty("_MatCapIntensity", L("MatCapの強さ", "MatCap Intensity"));
                DrawProperty("_MatCapBlendMode", L("MatCapのブレンドモード", "MatCap Blend Mode"));

                EditorGUILayout.Space();
                DrawProperty("_MatCapMask", L("MatCapマスク", "MatCap Mask"));
                DrawHelpToggle("MatCapMask", L("白 = MatCapあり、黒 = MatCapなし", "White = MatCap on, Black = MatCap off"), MessageType.Info);

                DrawHelpToggle("MatCapInfo", L("MatCapテクスチャは球面反射マップである必要があります。", "MatCap textures must be spherical reflection maps."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_MatCapBlend", null, "_MatCapBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_MatCapDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // MatCap 2
            bool enableMatCap2 = DrawToggle("_MATCAP_2", "_MatCap2", L("MatCap 2を有効化", "Enable MatCap 2"));

            if (enableMatCap2)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_MatCapTex2", L("MatCap 2 テクスチャ", "MatCap 2 Texture"));
                DrawProperty("_MatCapIntensity2", L("MatCap 2の強さ", "MatCap 2 Intensity"));
                DrawProperty("_MatCapBlendMode2", L("MatCap 2のブレンドモード", "MatCap 2 Blend Mode"));

                EditorGUILayout.Space();
                DrawProperty("_MatCapMask2", L("MatCap 2マスク", "MatCap 2 Mask"));
                DrawHelpToggle("MatCapMask2", L("白 = MatCap適用、黒 = 適用なし", "White = MatCap applied, Black = Not applied"), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_MatCapBlend2", null, "_MatCap2Blur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_MatCap2DistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // MatCap 3
            bool enableMatCap3 = DrawToggle("_MATCAP_3", "_MatCap3", L("MatCap 3を有効化", "Enable MatCap 3"));

            if (enableMatCap3)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_MatCapTex3", L("MatCap 3 テクスチャ", "MatCap 3 Texture"));
                DrawProperty("_MatCapIntensity3", L("MatCap 3の強さ", "MatCap 3 Intensity"));
                DrawProperty("_MatCapBlendMode3", L("MatCap 3のブレンドモード", "MatCap 3 Blend Mode"));

                EditorGUILayout.Space();
                DrawProperty("_MatCapMask3", L("MatCap 3マスク", "MatCap 3 Mask"));
                DrawHelpToggle("MatCapMask3", L("白 = MatCap適用、黒 = 適用なし", "White = MatCap applied, Black = Not applied"), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_MatCapBlend3", null, "_MatCap3Blur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_MatCap3DistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("MatCap"));
    }

    private void DrawGlitterSection()
    {
        SetFoldout("Glitter", DrawBoxedSection(L("グリッター（ラメ）", "Glitter"), GetFoldout("Glitter"), SectionCategory.Effects, "_GLITTER"));
        if (GetFoldout("Glitter"))
        {

            bool enableGlitter = DrawToggle("_GLITTER", "_Glitter", L("グリッターを有効化", "Enable Glitter"));

            if (enableGlitter)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("グリッター設定", "Glitter Settings"), EditorStyles.boldLabel);

                DrawColorProperty("_GlitterColor", L("グリッター色", "Glitter Color"));
                DrawProperty("_GlitterSize", L("グリッターサイズ", "Glitter Size"));
                DrawProperty("_GlitterDensity", L("グリッター密度", "Glitter Density"));
                DrawProperty("_GlitterSpeed", L("グリッター速度", "Glitter Speed"));
                DrawProperty("_GlitterIntensity", L("グリッター強度", "Glitter Intensity"));

                EditorGUILayout.Space(SECTION_SPACING);

                DrawProperty("_GlitterMask", L("グリッターマスク", "Glitter Mask"));
                DrawHelpToggle("GlitterMask",
                    L("グリッターマスクのR(赤)チャンネルを使用してグリッターの表示領域を制御します。\n" +
                    "• 白 (1.0): グリッターを完全に表示\n" +
                    "• 黒 (0.0): グリッターを非表示\n" +
                    "• グレー: 部分的に表示",
                    "Uses the R (red) channel of the glitter mask to control display area.\n" +
                    "• White (1.0): Fully show glitter\n" +
                    "• Black (0.0): Hide glitter\n" +
                    "• Gray: Partially show"),
                    MessageType.Info);
                DrawUVAnimationSettings("_GlitterMaskScrollSpeed", "_GlitterMaskRotateSpeed", L("グリッターマスク", "Glitter Mask"));

                EditorGUILayout.Space(SECTION_SPACING);
                DrawHelpToggle("GlitterInfo",
                    L("📍 グリッター:\n" +
                    "キラキラと輝くハイライト効果を追加します。\n\n" +
                    "• サイズ: グリッターの粒子サイズ\n" +
                    "• 密度: グリッターの出現頻度（0=少ない、1=多い）\n" +
                    "• 速度: グリッターの点滅速度\n" +
                    "• 強度: グリッターの明るさ\n\n" +
                    "💡 使い方:\n" +
                    "衣装やアクセサリーに華やかさを追加したい場合に使用します。",
                    "📍 Glitter:\n" +
                    "Adds sparkling highlight effects.\n\n" +
                    "• Size: Glitter particle size\n" +
                    "• Density: Glitter frequency (0=few, 1=many)\n" +
                    "• Speed: Glitter blink speed\n" +
                    "• Intensity: Glitter brightness\n\n" +
                    "💡 Usage:\n" +
                    "Use to add sparkle to costumes and accessories."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_GlitterBlend", "_GlitterBlendMode", "_GlitterBlur");

                // Advanced Glints sub-section
                EditorGUILayout.Space(SECTION_SPACING);
                bool enableGlints = DrawToggle("_GLINTS_ADVANCED", "_GlintsAdvanced", L("高度なグリンツを有効化", "Enable Advanced Glints"));
                if (enableGlints)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_GlintsSharpness", L("グリンツシャープネス", "Glints Sharpness"));
                    DrawProperty("_GlintsTemporal", L("時間変動速度", "Temporal Speed"));
                    DrawProperty("_GlintsNormalJitter", L("法線ジッター", "Normal Jitter"));
                    DrawHelpToggle("GlintsAdvanced",
                        L("✨ 高度なグリンツ:\n" +
                        "通常のグリッターに加え、法線の微細変動による\n" +
                        "よりリアルな煌めきを追加します。\n\n" +
                        "• シャープネス: グリンツの鋭さ（8=ぼんやり〜512=超シャープ）\n" +
                        "• 時間変動速度: 煌めきの時間変化速度\n" +
                        "• 法線ジッター: 法線のランダム変動量\n\n" +
                        "💡 宝石やビーズなど微細な煌めきに最適です。",
                        "✨ Advanced Glints:\n" +
                        "Adds more realistic sparkle via normal micro-variations\n" +
                        "on top of standard glitter.\n\n" +
                        "• Sharpness: Glint sharpness (8=soft, 512=ultra sharp)\n" +
                        "• Temporal Speed: sparkle time variation speed\n" +
                        "• Normal Jitter: random normal variation amount\n\n" +
                        "💡 Perfect for gems, beads, and fine sparkle effects."),
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_GlitterDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Glitter"));
    }

    private void DrawDripSection()
    {
        SetFoldout("Drip", DrawBoxedSection(L("雫エフェクト", "Drip Effect"), GetFoldout("Drip"), SectionCategory.Effects, "_WATER_DRIP"));
        if (GetFoldout("Drip"))
        {

            bool enableDrip = DrawToggle("_WATER_DRIP", "_WaterDrip", L("雫エフェクトを有効化", "Enable Drip Effect"));

            if (enableDrip)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("雫エフェクト設定", "Drip Effect Settings"), EditorStyles.boldLabel);

                DrawColorProperty("_DripColor", L("雫の色", "Drip Color"));
                DrawProperty("_DripSpeed", L("雫の速度", "Drip Speed"));
                DrawProperty("_DripDensity", L("雫の密度", "Drip Density"));
                DrawProperty("_DripSize", L("雫のサイズ", "Drip Size"));
                DrawProperty("_DripTrailLength", L("雫の尾の長さ", "Drip Trail Length"));
                DrawProperty("_DripIntensity", L("雫の強度", "Drip Intensity"));
                DrawProperty("_DripSharpness", L("雫のシャープさ", "Drip Sharpness"));

                EditorGUILayout.Space(SECTION_SPACING);

                DrawProperty("_DripMask", L("雫マスク", "Drip Mask"));
                DrawHelpToggle("DripMask",
                    L("雫マスクのR(赤)チャンネルを使用して雫の表示領域を制御します。\n" +
                    "• 白 (1.0): 雫を完全に表示\n" +
                    "• 黒 (0.0): 雫を非表示\n" +
                    "• グレー: 部分的に表示",
                    "Uses the R (red) channel of the drip mask to control display area.\n" +
                    "• White (1.0): Fully show drips\n" +
                    "• Black (0.0): Hide drips\n" +
                    "• Gray: Partially show"),
                    MessageType.Info);
                DrawUVAnimationSettings("_DripMaskScrollSpeed", "_DripMaskRotateSpeed", L("雫マスク", "Drip Mask"));

                EditorGUILayout.Space(SECTION_SPACING);
                DrawHelpToggle("DripInfo",
                    L("💧 雫エフェクト（ウォータードリップ）:\n" +
                    "水滴が表面を滴り落ちるような表現を追加します。\n\n" +
                    "• 速度: 雫が落ちる速度\n" +
                    "• 密度: 雫の出現頻度（0=少ない、1=多い）\n" +
                    "• サイズ: 雫の大きさ\n" +
                    "• 尾の長さ: 雫が残す水跡の長さ\n" +
                    "• 強度: エフェクトの明るさ\n" +
                    "• シャープさ: 雫の輪郭の鮮明さ\n\n" +
                    "💡 使い方:\n" +
                    "雨に濡れた衣装やガラス表面の表現に適しています。\n" +
                    "マスクテクスチャで雫が流れる領域を制限できます。",
                    "💧 Drip Effect (Water Drip):\n" +
                    "Adds water droplet dripping down surfaces.\n\n" +
                    "• Speed: Drip falling speed\n" +
                    "• Density: Drip frequency (0=few, 1=many)\n" +
                    "• Size: Drip size\n" +
                    "• Tail Length: Length of water trail\n" +
                    "• Intensity: Effect brightness\n" +
                    "• Sharpness: Drip edge clarity\n\n" +
                    "💡 Usage:\n" +
                    "Ideal for rain-soaked clothing or glass surfaces.\n" +
                    "Use mask texture to limit drip areas."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_DripBlend", "_DripBlendMode", "_DripBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_DripDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Drip"));
    }

    private void DrawSmearSection()
    {
        SetFoldout("Smear", DrawBoxedSection(L("スミア（残像エフェクト）", "Smear (Afterimage Effect)"), GetFoldout("Smear"), SectionCategory.Effects, "_SMEAR"));
        if (GetFoldout("Smear"))
        {

            bool enableSmear = DrawToggle("_SMEAR", "_Smear", L("スミア (残像エフェクト) を有効化", "Enable Smear (Afterimage)"));

            if (enableSmear)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("スミア基本設定", "Smear Basic Settings"), EditorStyles.boldLabel);

                DrawProperty("_SmearStretch", L("ストレッチ量 (Auto時は最大値)", "Stretch Amount (Max when Auto)"));
                DrawProperty("_SmearDirection", L("スミア方向 (Animator制御)", "Smear Direction (Animator Control)"));
                DrawProperty("_SmearNoiseScale", L("ノイズスケール", "Noise Scale"));
                DrawProperty("_SmearNoiseStrength", L("ノイズ強度", "Noise Intensity"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("速度自動検出モード", "Speed Auto-Detection Mode"), EditorStyles.boldLabel);

                DrawProperty("_SmearAutoMagnitude", L("速度自動検出 (Auto Magnitude)", "Speed Auto-Detect (Auto Magnitude)"));
                DrawProperty("_SmearMotionSensitivity", L("モーション感度", "Motion Sensitivity"));

                // VAT Velocity toggle: only show when VAT is enabled
                if (targetMaterial.IsKeywordEnabled("_VAT"))
                {
                    DrawProperty("_SmearVATVelocity", L("VAT速度連動", "VAT Velocity Linked"));
                }

                DrawHelpToggle("SmearAutoMode",
                    L("🏃 スミア自動モード:\n" +
                    "「速度自動検出」をONにすると、_SmearDirectionベクトルの大きさから\n" +
                    "ストレッチ量を自動計算します。ストレッチ量は最大値として機能します。\n\n" +
                    "【VRChat】Animatorで VelocityX/Y/Z → _SmearDirection にマッピング\n" +
                    "【VAT連動】VAT有効時、アニメーションの動きから自動でスミア方向と強度を算出",
                    "🏃 Smear Auto Mode:\n" +
                    "When 'Auto Velocity Detection' is ON, stretch amount is\n" +
                    "auto-calculated from _SmearDirection vector magnitude.\n" +
                    "Stretch amount acts as maximum value.\n\n" +
                    "[VRChat] Map VelocityX/Y/Z to _SmearDirection in Animator\n" +
                    "[VAT Link] When VAT is enabled, auto-calculates smear direction and intensity from animation"),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("トレイル設定", "Trail Settings"), EditorStyles.boldLabel);

                DrawProperty("_SmearTrailLength", L("トレイル長", "Trail Length"));
                DrawProperty("_SmearTrailFade", L("トレイル減衰", "Trail Fade"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("グロー・エミッション", "Glow & Emission"), EditorStyles.boldLabel);

                DrawColorProperty("_SmearGlowColor", L("グローカラー", "Glow Color"));
                DrawProperty("_SmearGlowIntensity", L("グロー強度", "Glow Intensity"));
                DrawProperty("_SmearGlowPower", L("フレネルパワー", "Fresnel Power"));
                DrawProperty("_SmearEmission", L("エミッション強度", "Emission Intensity"));
                DrawColorProperty("_SmearEmissionColor", L("エミッションカラー", "Emission Color"));

                EditorGUILayout.Space(SECTION_SPACING);

                DrawProperty("_SmearMask", L("スミアマスク", "Smear Mask"));
                DrawHelpToggle("SmearMask",
                    L("スミアマスクのR(赤)チャンネルを使用してスミアの表示領域を制御します。\n" +
                    "• 白 (1.0): スミアを完全に表示\n" +
                    "• 黒 (0.0): スミアを非表示\n" +
                    "• グレー: 部分的に表示",
                    "Uses the R (red) channel of the smear mask to control display area.\n" +
                    "• White (1.0): Fully show smear\n" +
                    "• Black (0.0): Hide smear\n" +
                    "• Gray: Partially show"),
                    MessageType.Info);
                DrawUVAnimationSettings("_SmearMaskScrollSpeed", "_SmearMaskRotateSpeed", L("スミアマスク", "Smear Mask"));

                EditorGUILayout.Space(SECTION_SPACING);
                DrawHelpToggle("SmearInfo",
                    L("💨 スミア（残像エフェクト）:\n" +
                    "高速移動時の残像・ストレッチ表現を追加します。\n\n" +
                    "• ストレッチ量: 頂点の引き伸ばし量 (Auto時は最大値)\n" +
                    "• スミア方向: Animatorで制御する移動方向ベクトル\n" +
                    "• 速度自動検出: ベクトルの大きさからストレッチ量を自動計算\n" +
                    "• モーション感度: 速度→ストレッチ変換の感度\n" +
                    "• VAT速度連動: VATアニメーションの速度から自動でスミア駆動\n" +
                    "• ノイズ: ストレッチにランダムな歪みを加える\n" +
                    "• トレイル: 残像の長さと減衰\n" +
                    "• グロー: フレネルベースの光沢効果\n" +
                    "• エミッション: 残像部分の発光\n\n" +
                    "💡 使い方:\n" +
                    "アクションシーンやダッシュ演出に適しています。\n" +
                    "Animatorから_SmearDirectionを制御して動的な残像を表現できます。\n" +
                    "VRChatではVelocityX/Y/Zを_SmearDirectionにマッピングし、\n" +
                    "速度自動検出ONで移動速度に連動したスミアが実現できます。\n" +
                    "マスクテクスチャでスミアが適用される領域を制限できます。",
                    "💨 Smear (Afterimage Effect):\n" +
                    "Adds motion blur/stretch effects for fast movement.\n\n" +
                    "• Stretch Amount: Vertex stretch amount (max value in Auto mode)\n" +
                    "• Smear Direction: Movement direction vector controlled by Animator\n" +
                    "• Auto Velocity Detection: Auto-calculates stretch from vector magnitude\n" +
                    "• Motion Sensitivity: Velocity-to-stretch conversion sensitivity\n" +
                    "• VAT Velocity Link: Auto-drives smear from VAT animation velocity\n" +
                    "• Noise: Adds random distortion to stretch\n" +
                    "• Trail: Afterimage length and decay\n" +
                    "• Glow: Fresnel-based gloss effect\n" +
                    "• Emission: Afterimage glow\n\n" +
                    "💡 Usage:\n" +
                    "Ideal for action scenes and dash effects.\n" +
                    "Control _SmearDirection from Animator for dynamic afterimages.\n" +
                    "In VRChat, map VelocityX/Y/Z to _SmearDirection and\n" +
                    "enable Auto Velocity Detection for speed-linked smear.\n" +
                    "Use mask texture to limit smear areas."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_SmearBlend", "_SmearBlendMode", "_SmearBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_SmearDistFade", L("スミア距離フェード強度", "Smear Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Smear"));
    }

    private void DrawFurSection()
    {
        SetFoldout("Fur", DrawBoxedSection(L("ファー（シェルベース毛皮）", "Fur (Shell-Based)"), GetFoldout("Fur"), SectionCategory.Effects, "_FUR"));
        if (GetFoldout("Fur"))
        {
            bool enableFur = DrawToggle("_FUR", "_Fur", L("ファーを有効化", "Enable Fur"));

            // Auto shader switching: _FUR ON on non-Fur shader → switch to Fur variant
            if (enableFur && targetMaterial != null && targetMaterial.shader != null)
            {
                bool isFurShader = targetMaterial.shader.name.Contains("Fur");
                if (!isFurShader)
                {
                    SetRenderingMode(RenderingMode.Fur);
                }
            }

            if (enableFur)
            {
                EditorGUI.indentLevel++;

                // Performance warning
                EditorGUILayout.HelpBox(
                    L("ファーは非常にGPU負荷が高い機能です。\n" +
                    "16シェルパスにより描画コールが大幅に増加します。\n" +
                    "VRChat Questでは使用しないでください。",
                    "Fur is a very GPU-intensive feature.\n" +
                    "16 shell passes significantly increase draw calls.\n" +
                    "Do not use on VRChat Quest."),
                    MessageType.Warning);

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("基本設定", "Basic Settings"), EditorStyles.boldLabel);
                DrawProperty("_FurLength", L("ファーの長さ", "Fur Length"));
                DrawProperty("_FurDensity", L("ファー密度", "Fur Density"));
                DrawProperty("_FurAlphaCutoff", L("アルファカットオフ", "Alpha Cutoff"));
                DrawProperty("_FurNoiseTex", L("ノイズテクスチャ", "Noise Texture"));
                DrawProperty("_FurMask", L("ファーマスク", "Fur Mask"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("カラー", "Color"), EditorStyles.boldLabel);
                DrawColorProperty("_FurRootColor", L("根元カラー", "Root Color"));
                DrawColorProperty("_FurTipColor", L("先端カラー", "Tip Color"));
                DrawProperty("_FurColorBlend", L("メインカラー混合比", "Main Color Mix Ratio"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("物理", "Physics"), EditorStyles.boldLabel);
                DrawProperty("_FurGravity", L("重力", "Gravity"));
                DrawProperty("_FurWindStrength", L("風の強さ", "Wind Strength"));
                DrawProperty("_FurWindSpeed", L("風の速度", "Wind Speed"));
                DrawProperty("_FurWindDirection", L("風の方向", "Wind Direction"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("シェーディング", "Shading"), EditorStyles.boldLabel);
                DrawProperty("_FurAO", L("セルフオクルージョン", "Self Occlusion"));
                DrawProperty("_FurShadowStrength", L("セルフシャドウ強度", "Self Shadow Intensity"));
                DrawProperty("_FurSpecular", L("スペキュラ", "Specular"));
                DrawProperty("_FurRimLight", L("リムライト", "Rim Light"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("パフォーマンス", "Performance"), EditorStyles.boldLabel);
                DrawProperty("_FurLODDistance", L("LOD開始距離", "LOD Start Distance"));
                DrawProperty("_FurLODMinLayers", L("最小レイヤー数", "Minimum Layers"));

                DrawHelpToggle("Fur",
                    L("ファー（シェルベース毛皮）:\n\n" +
                    "メッシュを法線方向に複数レイヤーで押し出し、\n" +
                    "ノイズテクスチャでアルファカットオフすることで\n" +
                    "毛皮の外見を再現します。\n\n" +
                    "【基本設定】\n" +
                    "・ファーの長さ: 毛の長さ（0.01〜0.05推奨）\n" +
                    "・ファー密度: ノイズテクスチャのタイリング\n" +
                    "・アルファカットオフ: 低い値=密な毛、高い値=まばらな毛\n" +
                    "・ノイズテクスチャ: 毛の分布パターン\n" +
                    "・ファーマスク: 白=毛あり、黒=毛なし\n\n" +
                    "【カラー】\n" +
                    "・根元カラー: 毛の根元の色（暗め推奨）\n" +
                    "・先端カラー: 毛の先端の色\n" +
                    "・メインカラー混合比: メインテクスチャとの混合\n\n" +
                    "【物理】\n" +
                    "・重力: 毛が下に垂れる強さ\n" +
                    "・風: 風による揺れアニメーション\n\n" +
                    "【シェーディング】\n" +
                    "・セルフオクルージョン: 根元が暗くなる効果\n" +
                    "・セルフシャドウ: 毛の内部の影\n\n" +
                    "【パフォーマンス】\n" +
                    "・LOD距離: この距離からレイヤーを減らし始めます\n" +
                    "・最小レイヤー: 遠距離での最低レイヤー数\n\n" +
                    "※ 描画タイプを「ファー」に設定してください。\n" +
                    "※ ファーは16シェルパスを使用し、GPUに高負荷です。\n" +
                    "※ VRChat Questでは使用しないでください。",
                    "Fur (Shell-Based Fur):\n\n" +
                    "Extrudes mesh in normal direction across multiple layers,\n" +
                    "using noise texture alpha cutoff to reproduce\n" +
                    "the appearance of fur.\n\n" +
                    "[Basic Settings]\n" +
                    "- Fur Length: Hair length (0.01-0.05 recommended)\n" +
                    "- Fur Density: Noise texture tiling\n" +
                    "- Alpha Cutoff: Low=dense fur, High=sparse fur\n" +
                    "- Noise Texture: Fur distribution pattern\n" +
                    "- Fur Mask: White=fur, Black=no fur\n\n" +
                    "[Color]\n" +
                    "- Root Color: Color at fur base (darker recommended)\n" +
                    "- Tip Color: Color at fur tips\n" +
                    "- Main Color Mix: Blend with main texture\n\n" +
                    "[Physics]\n" +
                    "- Gravity: How much fur droops downward\n" +
                    "- Wind: Wind sway animation\n\n" +
                    "[Shading]\n" +
                    "- Self Occlusion: Darkening at fur base\n" +
                    "- Self Shadow: Shadow inside fur\n\n" +
                    "[Performance]\n" +
                    "- LOD Distance: Start reducing layers from this distance\n" +
                    "- Min Layers: Minimum layers at far distance\n\n" +
                    "* Set rendering type to 'Fur'.\n" +
                    "* Fur uses 16 shell passes and is GPU-intensive.\n" +
                    "* Do not use on VRChat Quest."),
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Fur"));
    }

    private void DrawBackgroundLightmapSection()
    {
        RenderingMode currentMode = GetCurrentRenderingMode();
        if (currentMode != RenderingMode.Background) return;

        SetFoldout("BackgroundLightmap", DrawBoxedSection(L("背景ライトマップ設定", "Background Lightmap Settings"), GetFoldout("BackgroundLightmap"), SectionCategory.Lighting));
        if (GetFoldout("BackgroundLightmap"))
        {
            EditorGUI.indentLevel++;
            DrawProperty("_LightmapToonInfluence", L("ライトマップ トゥーン影響度", "Lightmap Toon Influence"));
            DrawProperty("_LightmapIntensity", L("ライトマップ明度", "Lightmap Brightness"));
            EditorGUILayout.HelpBox(
                L("ライトマップを使用するには Mesh Renderer の \"Contribute GI\" を有効にしてベイクしてください。\n" +
                "0=リアルGI, 0.5=ハイブリッド推奨, 1=フルトゥーン",
                "To use lightmaps, enable \"Contribute GI\" on the Mesh Renderer and bake.\n" +
                "0=Real GI, 0.5=Hybrid (Recommended), 1=Full Toon"),
                MessageType.Info);
            EditorGUI.indentLevel--;
        }
        EndBoxedSection(GetFoldout("BackgroundLightmap"));
    }

    private void DrawPBRSection()
    {
        RenderingMode currentMode = GetCurrentRenderingMode();
        if (currentMode != RenderingMode.Background) return;

        SetFoldout("PBR", DrawBoxedSection(L("PBR マテリアル設定", "PBR Material Settings"), GetFoldout("PBR"), SectionCategory.Lighting, "_PBR"));
        if (GetFoldout("PBR"))
        {
            bool enablePBR = DrawToggle("_PBR", "_EnablePBR", L("PBR ライティング", "PBR Lighting"));
            if (enablePBR)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_PBR_MetallicGlossMap", L("メタリック/スムーズネスマップ", "Metallic/Smoothness Map"));
                DrawProperty("_PBR_Metallic", L("メタリック", "Metallic"));
                DrawProperty("_PBR_Smoothness", L("スムーズネス", "Smoothness"));
                EditorGUILayout.Space(SECTION_SPACING);
                DrawProperty("_PBR_OcclusionMap", L("アンビエントオクルージョンマップ", "Ambient Occlusion Map"));
                DrawProperty("_PBR_OcclusionStrength", L("AO 強度", "AO Intensity"));
                EditorGUILayout.Space(SECTION_SPACING);
                DrawProperty("_PBR_ReflectionIntensity", L("リフレクションプローブ強度", "Reflection Probe Intensity"));
                EditorGUILayout.HelpBox(
                    L("PBR はトゥーンシェーディングの上に物理ベースの反射を加えます。\n" +
                    "金属/光沢テクスチャで質感を表現できます。",
                    "PBR adds physically-based reflections on top of toon shading.\n" +
                    "Express material quality with metallic/smoothness textures."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("PBR"));
    }

    private void DrawHologramSection()
    {
        SetFoldout("Hologram", DrawBoxedSection(L("ホログラム / グリッチ", "Hologram / Glitch"), GetFoldout("Hologram"), SectionCategory.Effects, "_HOLOGRAM"));
        if (GetFoldout("Hologram"))
        {

            bool enableHolo = DrawToggle("_HOLOGRAM", "_Hologram", L("ホログラムを有効化", "Enable Hologram"));
            if (enableHolo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("ホログラム基本設定", "Hologram Basic Settings"), EditorStyles.boldLabel);
                DrawColorProperty("_HologramColor", L("ホログラム色", "Hologram Color"));
                DrawProperty("_HologramMonochrome", L("モノクロ化", "Monochrome"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("スキャンライン", "Scanlines"), EditorStyles.boldLabel);
                DrawProperty("_HologramScanlineSpeed", L("スキャンライン速度", "Scanline Speed"));
                DrawProperty("_HologramScanlineIntensity", L("スキャンライン強度", "Scanline Intensity"));
                DrawProperty("_HologramScanlineDensity", L("スキャンライン密度", "Scanline Density"));
                DrawProperty("_HologramScanlineWidth", L("スキャンライン幅", "Scanline Width"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("エッジグロウ (Fresnel)", "Edge Glow (Fresnel)"), EditorStyles.boldLabel);
                DrawProperty("_HologramEdgeGlowPower", L("エッジグロウ範囲", "Edge Glow Range"));
                DrawProperty("_HologramEdgeGlowIntensity", L("エッジグロウ強度", "Edge Glow Intensity"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("透明度・フリッカー", "Transparency & Flicker"), EditorStyles.boldLabel);
                DrawProperty("_HologramAlpha", L("ホログラム透明度", "Hologram Alpha"));
                DrawProperty("_HologramFlickerSpeed", L("フリッカー速度", "Flicker Speed"));
                DrawProperty("_HologramFlickerAmount", L("フリッカー量", "Flicker Amount"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("ノイズ歪み", "Noise Distortion"), EditorStyles.boldLabel);
                DrawProperty("_HologramNoiseIntensity", L("ノイズ強度", "Noise Intensity"));
                DrawProperty("_HologramNoiseSpeed", L("ノイズ速度", "Noise Speed"));

                DrawProperty("_HologramMask", L("ホログラムマスク", "Hologram Mask"));
                DrawUVAnimationSettings("_HologramMaskScrollSpeed", "_HologramMaskRotateSpeed", L("ホログラムマスク", "Hologram Mask"));

                bool useNoiseTex = DrawToggle("_HOLOGRAM_NOISE", "_UseHologramNoise", L("ノイズテクスチャを使用", "Use Noise Texture"));
                if (useNoiseTex)
                    DrawProperty("_HologramNoiseTex", L("ノイズテクスチャ", "Noise Texture"));

                DrawBlendControls(materialEditor, targetMaterial, "_HologramBlend", "_HologramBlendMode", "_HologramBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_HologramDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                DrawHelpToggle("Hologram",
                    L("🔷 ホログラム:\n" +
                      "SF/サイバーパンク風のホログラム投影効果を追加します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• ホログラム色: 全体のティント色（青緑=定番SF風、紫=魔法風）\n" +
                      "• モノクロ化: テクスチャ色をホログラム色で上書き（0=テクスチャ保持、1=完全モノクロ）\n" +
                      "• スキャンライン: 走査線エフェクト\n" +
                      "  - 速度: スキャンライン移動速度\n" +
                      "  - 強度: スキャンライン明暗差（0.5〜0.8推奨）\n" +
                      "  - 密度: 走査線の本数（10〜50推奨）\n" +
                      "  - 幅: 各走査線の太さ（0.3〜0.7推奨）\n" +
                      "• エッジグロウ: Fresnelベースの輪郭発光\n" +
                      "  - 範囲: 低い=広い発光、高い=細いエッジのみ（2〜5推奨）\n" +
                      "  - 強度: 発光の明るさ（0.5〜2.0推奨）\n" +
                      "• 透明度: ホログラム全体の透明度\n" +
                      "• フリッカー: ランダムな明滅（速度+量で制御）\n" +
                      "• ノイズ歪み: UV歪みによるちらつき\n\n" +
                      "💡 Tips:\n" +
                      "• Transparentバリアントと組み合わせると半透明ホログラム投影に\n" +
                      "• エッジグロウ強め + 透明度低め = ゴースト/幽霊表現\n" +
                      "• ノイズテクスチャONで信号劣化風のホログラムに\n" +
                      "⚡ パフォーマンス: 軽〜中程度（スキャンライン+Fresnel計算）",
                      "🔷 Hologram:\n" +
                      "Adds sci-fi/cyberpunk holographic projection effects.\n\n" +
                      "📋 Parameters:\n" +
                      "• Hologram Color: overall tint (cyan=classic sci-fi, purple=magic)\n" +
                      "• Monochrome: override texture color (0=keep texture, 1=full monochrome)\n" +
                      "• Scanlines: scanning line effect\n" +
                      "  - Speed: scanline scroll speed\n" +
                      "  - Intensity: light/dark contrast (0.5-0.8 recommended)\n" +
                      "  - Density: number of lines (10-50 recommended)\n" +
                      "  - Width: thickness per line (0.3-0.7 recommended)\n" +
                      "• Edge Glow: Fresnel-based rim glow\n" +
                      "  - Range: lower=wider glow, higher=thin edge only (2-5 recommended)\n" +
                      "  - Intensity: glow brightness (0.5-2.0 recommended)\n" +
                      "• Alpha: overall hologram transparency\n" +
                      "• Flicker: random brightness variation (speed + amount)\n" +
                      "• Noise Distortion: UV warp for interference\n\n" +
                      "💡 Tips:\n" +
                      "• Use with Transparent variant for see-through hologram\n" +
                      "• Strong edge glow + low alpha = ghost/phantom effect\n" +
                      "• Enable Noise Texture for degraded signal look\n" +
                      "⚡ Performance: Light-Medium (scanlines + Fresnel)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);
            bool enableGlitch = DrawToggle("_GLITCH", "_Glitch", L("グリッチを有効化", "Enable Glitch"));
            if (enableGlitch)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("グリッチ設定", "Glitch Settings"), EditorStyles.boldLabel);
                DrawProperty("_GlitchIntensity", L("グリッチ強度", "Glitch Intensity"));
                DrawProperty("_GlitchSpeed", L("グリッチ速度", "Glitch Speed"));
                DrawProperty("_GlitchBlockSize", L("ブロックサイズ", "Block Size"));
                DrawProperty("_GlitchRGBSplitIntensity", L("RGBスプリット強度", "RGB Split Intensity"));
                DrawProperty("_GlitchFrequency", L("グリッチ発生頻度", "Glitch Frequency"));

                DrawBlendControls(materialEditor, targetMaterial, "_GlitchBlend", "_GlitchBlendMode", "_GlitchBlur");

                EditorGUILayout.Space(3);
                DrawProperty("_GlitchMask", L("グリッチマスク", "Glitch Mask"));
                DrawProperty("_GlitchMaskScale", L("マスクスケール", "Mask Scale"));
                DrawProperty("_GlitchMaskAffectsRGBSplit", L("マスクがRGBスプリットに影響", "Mask Affects RGB Split"));
                DrawProperty("_GlitchMaskAffectsFrequency", L("マスクが発生頻度に影響", "Mask Affects Frequency"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("ノイズテクスチャ", "Noise Texture"), EditorStyles.boldLabel);
                DrawProperty("_GlitchNoiseTex", L("ノイズテクスチャ", "Noise Texture"));
                DrawProperty("_GlitchNoiseIntensity", L("ノイズ強度", "Noise Intensity"));
                DrawProperty("_GlitchNoiseScrollSpeed", L("ノイズスクロール速度", "Noise Scroll Speed"));
                DrawProperty("_GlitchNoiseMode", L("ノイズモード", "Noise Mode"));

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_GlitchDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                DrawHelpToggle("Glitch",
                    L("⚡ グリッチ:\n" +
                      "ランダムなUV歪みとRGB色ずれによるデジタル信号破損を表現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• グリッチ強度: UV歪みの大きさ（0.1〜0.5=微弱、1.0=標準、2.0+=激しい）\n" +
                      "• グリッチ速度: 歪みの変化スピード\n" +
                      "• ブロックサイズ: グリッチのブロック粒度（小さい=細かいノイズ、大きい=大きな帯状）\n" +
                      "• RGBスプリット強度: RGB各チャンネルのずれ量（0.01〜0.05=微細、0.1+=激しい色ずれ）\n" +
                      "• 発生頻度: グリッチが起こる確率（0.5=半分の時間、1.0=常時）\n\n" +
                      "📋 マスク設定:\n" +
                      "• グリッチマスク: 白=グリッチ適用 / 黒=適用しない\n" +
                      "• マスクスケール: マスク効果の増幅（1=等倍、5=最大5倍ブースト）\n" +
                      "• マスクがRGBスプリットに影響: マスクで色ずれも部位制御\n" +
                      "• マスクが発生頻度に影響: マスクで発生率も部位制御\n\n" +
                      "📋 ノイズテクスチャ:\n" +
                      "• UV Distortion: ノイズでUVをさらに歪ませる\n" +
                      "• Color Corruption: ノイズ色を混ぜてカラー崩壊\n" +
                      "• Block Noise: ブロック状のノイズパターン適用\n\n" +
                      "💡 Tips:\n" +
                      "• ホログラムと併用でSFホログラム通信の乱れを表現\n" +
                      "• 色収差と併用でよりリアルなデジタル破損に\n" +
                      "• マスクで目や手だけグリッチさせると「バグったアバター」演出\n" +
                      "⚡ パフォーマンス: 軽量（UV演算 + テクスチャ数サンプル）",
                      "⚡ Glitch:\n" +
                      "Creates digital signal corruption with random UV distortion and RGB shift.\n\n" +
                      "📋 Parameters:\n" +
                      "• Glitch Intensity: UV distortion amount (0.1-0.5=subtle, 1.0=standard, 2.0+=heavy)\n" +
                      "• Glitch Speed: distortion change rate\n" +
                      "• Block Size: glitch block granularity (small=fine noise, large=wide bands)\n" +
                      "• RGB Split Intensity: RGB channel offset (0.01-0.05=subtle, 0.1+=heavy)\n" +
                      "• Frequency: glitch occurrence probability (0.5=half the time, 1.0=constant)\n\n" +
                      "📋 Mask Settings:\n" +
                      "• Glitch Mask: white=apply / black=skip\n" +
                      "• Mask Scale: amplifies mask effect (1=normal, 5=max boost)\n" +
                      "• Mask Affects RGB Split: per-area color shift control\n" +
                      "• Mask Affects Frequency: per-area occurrence control\n\n" +
                      "📋 Noise Texture:\n" +
                      "• UV Distortion: additional UV warping from noise\n" +
                      "• Color Corruption: mix noise colors for color breakdown\n" +
                      "• Block Noise: block-pattern noise overlay\n\n" +
                      "💡 Tips:\n" +
                      "• Combine with Hologram for sci-fi communication glitch\n" +
                      "• Add Chromatic Aberration for more realistic digital corruption\n" +
                      "• Mask specific areas (eyes/hands) for 'bugged avatar' effect\n" +
                      "⚡ Performance: Light (UV math + few texture samples)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // Stretch Glitch (independent from normal Glitch)
            EditorGUILayout.Space(SECTION_SPACING);
            bool enableStretchGlitch = DrawToggle("_GLITCH_STRETCH", "_GlitchStretch", L("ストレッチグリッチを有効化", "Enable Stretch Glitch"));
            if (enableStretchGlitch)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("ストレッチグリッチ設定", "Stretch Glitch Settings"), EditorStyles.boldLabel);
                DrawProperty("_GlitchStretchIntensity", L("ストレッチ強度", "Stretch Intensity"));
                DrawProperty("_GlitchStretchSpeed", L("ストレッチ速度", "Stretch Speed"));
                DrawProperty("_GlitchStretchBlockSize", L("ブロックサイズ", "Block Size"));
                DrawProperty("_GlitchStretchFrequency", L("発生頻度", "Frequency"));
                EditorGUILayout.Space(3);
                DrawProperty("_GlitchStretchMask", L("ストレッチマスク", "Stretch Mask"));
                DrawProperty("_GlitchStretchMaskScale", L("マスクスケール", "Mask Scale"));
                DrawHelpToggle("StretchGlitch",
                    L("📐 ストレッチグリッチ:\n" +
                      "テクスチャだけを横に伸縮させるグリッチです（通常グリッチとは独立）。\n\n" +
                      "📋 パラメータ:\n" +
                      "• ストレッチ強度: 伸縮の大きさ（0.5=微細、2.0=標準、5.0=最大）\n" +
                      "• ストレッチ速度: 伸縮アニメーション速度\n" +
                      "• ブロックサイズ: 伸縮するブロックの高さ\n" +
                      "• 発生頻度: ストレッチが起こる確率\n" +
                      "• マスク/マスクスケール: 適用範囲と強度の制御\n\n" +
                      "💡 Tips:\n" +
                      "• 通常グリッチと組み合わせると激しいデジタル崩壊に\n" +
                      "• 単体使用で「VHSテープの横ずれ」風レトロ演出\n" +
                      "⚡ パフォーマンス: 極めて軽量（UV演算のみ）",
                      "📐 Stretch Glitch:\n" +
                      "Horizontally stretches the texture in blocks (independent from normal Glitch).\n\n" +
                      "📋 Parameters:\n" +
                      "• Stretch Intensity: stretch amount (0.5=subtle, 2.0=standard, 5.0=max)\n" +
                      "• Stretch Speed: animation speed\n" +
                      "• Block Size: height of stretch blocks\n" +
                      "• Frequency: occurrence probability\n" +
                      "• Mask/Mask Scale: area and intensity control\n\n" +
                      "💡 Tips:\n" +
                      "• Combine with normal Glitch for intense digital corruption\n" +
                      "• Use alone for retro 'VHS tape horizontal shift' effect\n" +
                      "⚡ Performance: Very light (UV math only)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);
            DrawHelpToggle("HologramInfo",
                L("🔷 ホログラム＆グリッチ:\n" +
                "SF/サイバーパンク風のホログラム表現を追加します。\n\n" +
                "• スキャンライン: 3層構成の走査線エフェクト\n" +
                "• エッジグロウ: Fresnelベースの縁光り\n" +
                "• モノクロ化: 色をホログラム色に統一\n" +
                "• 透明度: Fresnel連動の自動透明化\n" +
                "• グリッチ: ランダムなUV歪み＋RGB色ずれ（強度最大3.0）\n" +
                "  → マスクで部位ごとにグリッチ強度を制御可能\n" +
                "  → マスクスケール: マスク効果を増幅（1=等倍、5=最大5倍）\n" +
                "  → RGBスプリット・発生頻度もマスクで部位制御可能\n" +
                "• ノイズテクスチャ: グリッチにテクスチャベースのノイズを追加\n" +
                "  → UV Distortion: ノイズでUVをさらに歪ませる\n" +
                "  → Color Corruption: ノイズ色を混ぜてカラーグリッチ\n" +
                "  → Block Noise: ブロック状にノイズパターンを適用\n" +
                "• ストレッチグリッチ: テクスチャだけ横に伸縮（強度最大5.0）\n" +
                "  → 専用マスクで適用範囲を制御\n\n" +
                "💡 Transparent バリアントとの組み合わせで\n" +
                "よりリアルなホログラム投影を実現できます。",
                "🔷 Hologram & Glitch:\n" +
                "Adds sci-fi/cyberpunk hologram effects.\n\n" +
                "• Scanlines: 3-layer scanline effect\n" +
                "• Edge Glow: Fresnel-based edge glow\n" +
                "• Monochrome: Unify colors to hologram tint\n" +
                "• Transparency: Fresnel-linked auto transparency\n" +
                "• Glitch: Random UV distortion + RGB shift (max intensity 3.0)\n" +
                "  → Mask controls intensity per area\n" +
                "  → Mask Scale amplifies mask effect (1=normal, 5=max boost)\n" +
                "  → RGB Split & Frequency also affected by mask\n" +
                "• Noise Texture: Add texture-based noise to glitch\n" +
                "  → UV Distortion: Further UV warping from noise\n" +
                "  → Color Corruption: Mix noise colors for color glitch\n" +
                "  → Block Noise: Block-pattern noise overlay\n" +
                "• Stretch Glitch: Horizontal UV stretch (max intensity 5.0)\n" +
                "  → Dedicated mask for area control\n\n" +
                "💡 Combine with Transparent variant for\n" +
                "more realistic hologram projection."),
                MessageType.Info);
        }
        EndBoxedSection(GetFoldout("Hologram"));
    }

    private void DrawIllustrationStyleSection()
    {
        SetFoldout("IllustrationStyle", DrawBoxedSection(L("イラスト調スタイル", "Illustration Style"), GetFoldout("IllustrationStyle"), SectionCategory.Effects, "_COLOR_QUANTIZE"));
        if (GetFoldout("IllustrationStyle"))
        {
            // --- Color Quantization ---
            bool useQuantize = DrawToggle("_COLOR_QUANTIZE", "_UseColorQuantize",
                L("色の量子化", "Color Quantization"));
            if (useQuantize)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_QuantizeMode", L("量子化モード", "Quantize Mode"));
                float quantizeMode = targetMaterial.HasProperty("_QuantizeMode") ? targetMaterial.GetFloat("_QuantizeMode") : 0f;
                if (quantizeMode > FLOAT_COMPARISON_THRESHOLD) // HSV mode
                {
                    DrawProperty("_QuantizeHueLevels", L("色相レベル", "Hue Levels"));
                    DrawProperty("_QuantizeSatLevels", L("彩度レベル", "Saturation Levels"));
                    DrawProperty("_QuantizeValLevels", L("明度レベル", "Value Levels"));
                }
                else // RGB mode
                {
                    DrawProperty("_QuantizeLevels", L("量子化レベル", "Quantize Levels"));
                }
                DrawProperty("_QuantizeDither", L("ディザ量", "Dither Amount"));
                DrawProperty("_QuantizeBlend", L("ブレンド", "Blend"));
                DrawProperty("_QuantizeMask", L("マスク", "Mask"));
                DrawHelpToggle("ColorQuantize",
                    L("🎨 色の量子化 (Color Quantization):\n" +
                      "出力色のレベル数を減らし、デジタルイラストの「塗り分け」感を実現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• 量子化モード: RGB=均一な色数削減 / HSV=色相・彩度・明度を個別に制御（推奨）\n" +
                      "• HSVモード時:\n" +
                      "  - 色相レベル: 色の種類数（12=標準、6=レトロ、36=自然）\n" +
                      "  - 彩度レベル: 鮮やかさの段階数（4〜8推奨）\n" +
                      "  - 明度レベル: 明暗の段階数（4〜8推奨）\n" +
                      "• RGBモード時: 量子化レベル（8=標準、4=強いポスタライズ、16〜32=微細）\n" +
                      "• ディザ量: バンディング（段差模様）を防止するノイズ量（0.3〜0.5推奨）\n" +
                      "• ブレンド: 0=効果なし、1=完全適用\n" +
                      "• マスク: 白=量子化適用 / 黒=元の色を維持\n\n" +
                      "💡 Tips:\n" +
                      "• セル塗りイラスト風: HSVモード、明度4〜6、彩度4〜6\n" +
                      "• ポップアート風: RGBモード、レベル3〜4\n" +
                      "• 3D LUT と組み合わせると映画的な色彩制限が可能\n" +
                      "⚡ パフォーマンス: 極めて軽量（ALU演算のみ）",
                      "🎨 Color Quantization:\n" +
                      "Reduces color levels to create a flat, illustrated look.\n\n" +
                      "📋 Parameters:\n" +
                      "• Quantize Mode: RGB=uniform reduction / HSV=separate H/S/V control (recommended)\n" +
                      "• HSV mode:\n" +
                      "  - Hue Levels: number of hue steps (12=standard, 6=retro, 36=natural)\n" +
                      "  - Saturation Levels: saturation steps (4-8 recommended)\n" +
                      "  - Value Levels: brightness steps (4-8 recommended)\n" +
                      "• RGB mode: Quantize Levels (8=standard, 4=strong posterize, 16-32=subtle)\n" +
                      "• Dither: prevents banding artifacts (0.3-0.5 recommended)\n" +
                      "• Blend: 0=no effect, 1=full\n" +
                      "• Mask: white=apply / black=keep original\n\n" +
                      "💡 Tips:\n" +
                      "• Cel-shaded look: HSV mode, Value 4-6, Saturation 4-6\n" +
                      "• Pop art: RGB mode, Levels 3-4\n" +
                      "• Combine with 3D LUT for cinematic color restriction\n" +
                      "⚡ Performance: Very light (ALU only)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- 3D LUT ---
            bool useLUT = DrawToggle("_LUT_3D", "_UseLUT3D",
                L("3D LUT カラーグレーディング", "3D LUT Color Grading"));
            if (useLUT)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_LUT3DTex", L("LUTテクスチャ", "LUT Texture"));
                DrawProperty("_LUT3DIntensity", L("強度", "Intensity"));
                DrawProperty("_LUT3DSize", L("LUTサイズ", "LUT Size"));
                DrawHelpToggle("LUT3D",
                    L("🎬 3D LUT カラーグレーディング:\n" +
                      "LUT（ルックアップテーブル）テクスチャで映画的・絵画的な色調変換を行います。\n\n" +
                      "📋 パラメータ:\n" +
                      "• LUTテクスチャ: 横長のストリップテクスチャ（32x32x32=1024x32px が標準）\n" +
                      "• 強度: 0=元の色、1=LUT完全適用（0.5〜0.8で自然な調整）\n" +
                      "• LUTサイズ: テクスチャのグリッドサイズ（通常32。テクスチャに合わせて設定）\n\n" +
                      "💡 Tips:\n" +
                      "• LUTテクスチャはPhotoshop/GIMP等のカラー調整をLUTとして書き出して作成\n" +
                      "• フリーのLUTパックも多数利用可能（映画風、ヴィンテージ風、アニメ風等）\n" +
                      "• 色の量子化と組み合わせて世界観統一に最適\n" +
                      "• Filter Modeを「Point」に設定すると色の階段化が鮮明に\n" +
                      "⚡ パフォーマンス: 極めて軽量（テクスチャ2サンプル）",
                      "🎬 3D LUT Color Grading:\n" +
                      "Applies cinematic color transformation using a LUT texture.\n\n" +
                      "📋 Parameters:\n" +
                      "• LUT Texture: horizontal strip (32x32x32 = 1024x32px standard)\n" +
                      "• Intensity: 0=original, 1=full LUT (0.5-0.8 for natural look)\n" +
                      "• LUT Size: grid size matching your texture (usually 32)\n\n" +
                      "💡 Tips:\n" +
                      "• Create LUTs by exporting color adjustments from Photoshop/GIMP\n" +
                      "• Many free LUT packs available (cinematic, vintage, anime styles)\n" +
                      "• Great with Color Quantization for unified art direction\n" +
                      "• Set Filter Mode to 'Point' for sharp color banding\n" +
                      "⚡ Performance: Very light (2 texture samples)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Hatching ---
            bool useHatching = DrawToggle("_HATCHING", "_UseHatching",
                L("ハッチング", "Hatching"));
            if (useHatching)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_HatchTex0", L("ハッチテクスチャ 0 (RGBA=L1-4)", "Hatch Texture 0"));
                DrawProperty("_HatchTex1", L("ハッチテクスチャ 1 (RG=L5-6)", "Hatch Texture 1"));
                DrawProperty("_HatchingTiling", L("タイリング", "Tiling"));
                DrawProperty("_HatchingColor", L("ハッチング色", "Hatching Color"));
                DrawProperty("_HatchingBlend", L("ブレンド", "Blend"));
                DrawProperty("_HatchingMask", L("マスク", "Mask"));
                DrawHelpToggle("Hatching",
                    L("✏️ ハッチング (Tonal Art Maps):\n" +
                      "明暗に応じて斜線パターンを適用し、鉛筆画・エッチング風のシェーディングを表現します。\n" +
                      "6段階のTAM（Tonal Art Map）方式で、明→暗で線が徐々に密になります。\n\n" +
                      "📋 パラメータ:\n" +
                      "• ハッチテクスチャ 0: RGBAチャンネルにレベル1〜4をパック\n" +
                      "  R=最も薄い線（明部）、G/B=中間、A=やや密な線\n" +
                      "• ハッチテクスチャ 1: RGチャンネルにレベル5〜6をパック\n" +
                      "  R=密な線、G=最も密な線（暗部）\n" +
                      "• タイリング: テクスチャの繰り返し数（5〜15推奨、モデルサイズで調整）\n" +
                      "• ハッチング色: 線の色（黒=鉛筆風、茶色=セピア風、青=設計図風）\n" +
                      "• マスク: 白=ハッチング適用 / 黒=適用しない（肌のみ除外等）\n\n" +
                      "💡 Tips:\n" +
                      "• テクスチャ作成: 45度/135度の斜線パターンを密度違いで6枚用意\n" +
                      "• 色の量子化(2段=白黒)と組み合わせて銅版画風に\n" +
                      "• エッジ検出と合わせるとコミック/マンガ調に\n" +
                      "⚡ パフォーマンス: 非常に軽量（テクスチャ2枚サンプル）",
                      "✏️ Hatching (Tonal Art Maps):\n" +
                      "Applies cross-hatch patterns based on brightness for a pencil/etching look.\n" +
                      "Uses 6-level TAM: lines get denser from bright to dark areas.\n\n" +
                      "📋 Parameters:\n" +
                      "• Hatch Texture 0: RGBA channels = levels 1-4\n" +
                      "  R=lightest lines, G/B=medium, A=denser\n" +
                      "• Hatch Texture 1: RG channels = levels 5-6\n" +
                      "  R=dense, G=densest (shadow areas)\n" +
                      "• Tiling: repetition count (5-15 recommended, adjust per model size)\n" +
                      "• Hatching Color: line color (black=pencil, brown=sepia, blue=blueprint)\n" +
                      "• Mask: white=apply / black=skip (e.g., exclude skin)\n\n" +
                      "💡 Tips:\n" +
                      "• Create textures: 45/135 degree line patterns at 6 density levels\n" +
                      "• Combine with Color Quantize (2 levels=B&W) for engraving look\n" +
                      "• Add Screen Edge for comic/manga style\n" +
                      "⚡ Performance: Very light (2 texture samples)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Watercolor ---
            bool useWC = DrawToggle("_WATERCOLOR", "_UseWatercolor",
                L("水彩シミュレーション", "Watercolor Simulation"));
            if (useWC)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_WCEdgeDarkening", L("エッジダークニング", "Edge Darkening"));
                DrawProperty("_WCWetEdge", L("ウェットエッジ", "Wet Edge"));
                DrawProperty("_WCGranulation", L("粒子感", "Granulation"));
                DrawProperty("_WCGranulationTex", L("粒子感テクスチャ", "Granulation Texture"));
                DrawProperty("_WCPaperTex", L("紙テクスチャ", "Paper Texture"));
                DrawProperty("_WCPaperIntensity", L("紙の強度", "Paper Intensity"));
                DrawProperty("_WCPaperTiling", L("紙タイリング", "Paper Tiling"));
                DrawProperty("_WCBlend", L("ブレンド", "Blend"));
                DrawProperty("_WCMask", L("マスク", "Mask"));
                DrawHelpToggle("Watercolor",
                    L("🎨 水彩シミュレーション:\n" +
                      "GrabPassを利用し、エッジダークニング・ウェットエッジ・紙のテクスチャを組み合わせて\n" +
                      "リアルな水彩画の質感を再現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• エッジダークニング: 色境界を暗くする強度（0.3〜0.7推奨）\n" +
                      "  → 水彩絵具がエッジに溜まる「ダークエッジ」現象を再現\n" +
                      "• ウェットエッジ: 輪郭部に色が溜まる「ウェットインウェット」効果（0.2〜0.5推奨）\n" +
                      "• 粒子感: 紙の凹凸による絵具の粒子感（0.3〜0.6推奨）\n" +
                      "• 粒子感テクスチャ: ノイズテクスチャ（ガウシアンノイズ等）\n" +
                      "• 紙テクスチャ: 画用紙/水彩紙のテクスチャ（凹凸のあるもの推奨）\n" +
                      "• 紙の強度: 紙テクスチャの影響度（0.1〜0.4推奨。高すぎると紙が目立ちすぎる）\n" +
                      "• 紙タイリング: 紙テクスチャの繰り返し数（2〜5推奨）\n" +
                      "• マスク: 白=水彩効果適用 / 黒=適用しない\n\n" +
                      "💡 Tips:\n" +
                      "• 色の量子化（低レベル）と合わせるとポスターカラー風に\n" +
                      "• エッジダークニング強め+ウェットエッジ弱めで「乾いた水彩」表現\n" +
                      "• 紙テクスチャは実際の水彩紙をスキャンしたものが最適\n" +
                      "• ソフトフィルターと併用でさらに柔らかい印象に\n" +
                      "⚡ パフォーマンス: 中程度（GrabPass + テクスチャ3〜4サンプル）",
                      "🎨 Watercolor Simulation:\n" +
                      "Combines edge darkening, wet edges, and paper texture using GrabPass\n" +
                      "to create realistic watercolor painting effects.\n\n" +
                      "📋 Parameters:\n" +
                      "• Edge Darkening: darkens color boundaries (0.3-0.7 recommended)\n" +
                      "  → Simulates paint pooling at edges\n" +
                      "• Wet Edge: color accumulation at contours (0.2-0.5 recommended)\n" +
                      "• Granulation: paper roughness effect on paint (0.3-0.6 recommended)\n" +
                      "• Granulation Texture: noise texture (Gaussian noise etc.)\n" +
                      "• Paper Texture: watercolor paper texture (bumpy texture recommended)\n" +
                      "• Paper Intensity: paper influence (0.1-0.4 recommended; too high makes paper too visible)\n" +
                      "• Paper Tiling: paper texture repeat count (2-5 recommended)\n" +
                      "• Mask: white=apply / black=skip\n\n" +
                      "💡 Tips:\n" +
                      "• Combine with Color Quantize (low levels) for poster paint look\n" +
                      "• High edge darkening + low wet edge = 'dry watercolor' look\n" +
                      "• Best paper textures are scanned from real watercolor paper\n" +
                      "• Add Soft Filter for even softer impression\n" +
                      "⚡ Performance: Medium (GrabPass + 3-4 texture samples)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Soft Filter ---
            bool useSoft = DrawToggle("_SOFT_FILTER", "_UseSoftFilter",
                L("ソフトフィルター / Diffusion", "Soft Filter / Diffusion"));
            if (useSoft)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_SoftFilterMode", L("フィルターモード", "Filter Mode"));
                DrawProperty("_SoftFilterRadius", L("半径", "Radius"));
                DrawProperty("_SoftFilterBlend", L("ブレンド", "Blend"));
                float softMode = targetMaterial.HasProperty("_SoftFilterMode") ? targetMaterial.GetFloat("_SoftFilterMode") : 0f;
                if (softMode > FLOAT_COMPARISON_THRESHOLD)
                    DrawProperty("_SoftFilterThreshold", L("Bloom閾値", "Bloom Threshold"));
                DrawHelpToggle("SoftFilter",
                    L("✨ ソフトフィルター / Diffusion:\n" +
                      "GrabPassに対してガウシアンブラーを適用し、\n" +
                      "劇場版アニメや映画のような柔らかいDiffusion効果を実現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• フィルターモード:\n" +
                      "  0=Gaussian: 均一なソフトフォーカス（柔らかい空気感）\n" +
                      "  1=Bloom Mix: 高輝度部分のみブラーをブレンド（ハイライトが輝く）\n" +
                      "• 半径: ブラー強度（1〜3=微細な柔らかさ、5〜8=ドリーミー、10+=強いディフュージョン）\n" +
                      "• ブレンド: 0=効果なし、1=完全適用（0.3〜0.6で自然な仕上がり）\n" +
                      "• Bloom閾値（Bloom Mixモード時）: この輝度以上のピクセルのみブラー適用\n" +
                      "  （0.5=中間輝度以上、0.8=高輝度のみ、0.3=広範囲にBloom）\n\n" +
                      "💡 Tips:\n" +
                      "• 劇場版アニメ風: Gaussianモード、半径2〜4、ブレンド0.3〜0.5\n" +
                      "• ドリーミー回想シーン: 半径8+、ブレンド0.6+\n" +
                      "• Bloom Mixモードは画面の印象を大きく変えずにハイライトに輝きを追加\n" +
                      "⚡ パフォーマンス: 中〜高（13タップ × 2パス ガウシアンブラー）",
                      "✨ Soft Filter / Diffusion:\n" +
                      "Applies Gaussian blur to GrabPass for a soft, dreamy look\n" +
                      "like theatrical anime or cinematic diffusion.\n\n" +
                      "📋 Parameters:\n" +
                      "• Filter Mode:\n" +
                      "  0=Gaussian: uniform soft focus (soft atmosphere)\n" +
                      "  1=Bloom Mix: blurs only bright areas (glowing highlights)\n" +
                      "• Radius: blur strength (1-3=subtle, 5-8=dreamy, 10+=heavy diffusion)\n" +
                      "• Blend: 0=off, 1=full (0.3-0.6 for natural result)\n" +
                      "• Bloom Threshold (Bloom Mix mode): only blur pixels above this brightness\n" +
                      "  (0.5=mid-bright, 0.8=highlights only, 0.3=wide bloom)\n\n" +
                      "💡 Tips:\n" +
                      "• Theatrical anime: Gaussian mode, radius 2-4, blend 0.3-0.5\n" +
                      "• Dreamy flashback: radius 8+, blend 0.6+\n" +
                      "• Bloom Mix adds glow to highlights without changing overall impression\n" +
                      "⚡ Performance: Medium-High (13-tap × 2-pass Gaussian blur)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Kuwahara Filter ---
            bool useKuwahara = DrawToggle("_KUWAHARA_FILTER", "_UseKuwahara",
                L("油絵フィルター (Kuwahara)", "Oil Paint Filter (Kuwahara)"));
            if (useKuwahara)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_KuwaharaRadius", L("半径", "Radius"));
                DrawProperty("_KuwaharaBlend", L("ブレンド", "Blend"));
                DrawHelpToggle("Kuwahara",
                    L("🖌️ 油絵フィルター (Kuwahara):\n" +
                      "4象限分散ベースのKuwaharaフィルターで、エッジを保持しながら\n" +
                      "油絵・厚塗り風のストローク表現を実現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• 半径: フィルターの探索範囲（2〜4=微細な油絵感、6〜8=はっきりした筆跡、10+=抽象画風）\n" +
                      "• ブレンド: 0=効果なし、1=完全適用（0.5〜0.8推奨）\n\n" +
                      "💡 Tips:\n" +
                      "• 半径を大きくするほど「平筆で塗った」ような大胆なストロークに\n" +
                      "• 色の量子化と合わせるとデジタル厚塗りイラスト風\n" +
                      "• ソフトフィルターの代わりに使うと印象派風の仕上がり\n" +
                      "• エッジ検出と併用で「絵画の中のキャラクター」表現\n" +
                      "⚠️ 半径を大きくしすぎるとディテールが失われるので注意\n" +
                      "⚡ パフォーマンス: 高（半径に応じてサンプル数が二次的に増加。半径8以下推奨）",
                      "🖌️ Oil Paint Filter (Kuwahara):\n" +
                      "4-quadrant variance-based Kuwahara filter that preserves edges\n" +
                      "while creating oil painting / impasto brush stroke effects.\n\n" +
                      "📋 Parameters:\n" +
                      "• Radius: search range (2-4=subtle, 6-8=visible strokes, 10+=abstract)\n" +
                      "• Blend: 0=off, 1=full (0.5-0.8 recommended)\n\n" +
                      "💡 Tips:\n" +
                      "• Larger radius = bolder, more visible brush strokes\n" +
                      "• Combine with Color Quantize for digital impasto illustration\n" +
                      "• Use instead of Soft Filter for impressionist look\n" +
                      "• Add Screen Edge for 'character in a painting' effect\n" +
                      "⚠️ Very large radius may lose fine detail\n" +
                      "⚡ Performance: High (samples grow quadratically with radius; keep ≤8)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Screen Edge Detection ---
            bool useEdge = DrawToggle("_SCREEN_EDGE", "_UseScreenEdge",
                L("スクリーンエッジ検出", "Screen Edge Detection"));
            if (useEdge)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_EdgeColor", L("エッジ色", "Edge Color"));
                DrawProperty("_EdgeWidth", L("エッジ幅", "Edge Width"));
                DrawProperty("_EdgeDepthSensitivity", L("深度感度", "Depth Sensitivity"));
                DrawProperty("_EdgeNormalSensitivity", L("法線感度", "Normal Sensitivity"));
                DrawProperty("_EdgeBlend", L("ブレンド", "Blend"));
                DrawHelpToggle("ScreenEdge",
                    L("🔲 スクリーンエッジ検出:\n" +
                      "深度バッファと法線バッファからSobelフィルターでエッジを抽出し、\n" +
                      "スクリーンスペースの輪郭線を描画します。\n" +
                      "通常のアウトラインとは異なり、画面全体にかかるポスプロ的な効果です。\n\n" +
                      "📋 パラメータ:\n" +
                      "• エッジ色: 輪郭線の色（黒=インク線、白=光る輪郭、色付き=アーティスティック）\n" +
                      "• エッジ幅: 検出に使うピクセル幅（1=細い線、2〜3=標準、5+=太い線）\n" +
                      "• 深度感度: 奥行き差によるエッジ検出の敏感さ（1〜5推奨）\n" +
                      "  → 高すぎると遠景にもエッジが出るので注意\n" +
                      "• 法線感度: 面の向き差によるエッジ検出の敏感さ（1〜3推奨）\n" +
                      "  → 曲面の変化を拾いたい場合に上げる\n" +
                      "• ブレンド: 0=効果なし、1=完全適用\n\n" +
                      "💡 Tips:\n" +
                      "• 通常のアウトラインと併用で「太い外周線 + 細いディテール線」表現\n" +
                      "• ハッチングと合わせるとマンガ風に\n" +
                      "• 法線感度のみ高めにすると、面の角だけに線が入る「モデリング線」風\n" +
                      "• 深度感度のみ高めにすると、前景・背景の分離線に\n" +
                      "⚡ パフォーマンス: 中程度（Sobel 3x3カーネル × 深度+法線の2パス）",
                      "🔲 Screen Edge Detection:\n" +
                      "Extracts edges from depth and normal buffers using Sobel filter\n" +
                      "for screen-space contour lines (post-process style).\n" +
                      "Unlike regular outline, this applies across the entire screen.\n\n" +
                      "📋 Parameters:\n" +
                      "• Edge Color: line color (black=ink, white=glowing, colored=artistic)\n" +
                      "• Edge Width: pixel width for detection (1=thin, 2-3=standard, 5+=thick)\n" +
                      "• Depth Sensitivity: edge detection from depth differences (1-5 recommended)\n" +
                      "  → Too high catches edges in distant scenery\n" +
                      "• Normal Sensitivity: edge detection from surface angle changes (1-3 recommended)\n" +
                      "  → Increase to catch curved surface details\n" +
                      "• Blend: 0=off, 1=full\n\n" +
                      "💡 Tips:\n" +
                      "• Use with regular Outline for 'thick contour + thin detail lines'\n" +
                      "• Combine with Hatching for manga look\n" +
                      "• High normal sensitivity only = 'modeling wireframe' style\n" +
                       "• High depth sensitivity only = foreground/background separation lines\n" +
                       "⚡ Performance: Medium (Sobel 3x3 kernel × 2 passes: depth + normal)"),
                    MessageType.Info);

                bool isSplitShader = IsScreenEdgeSplitShader(targetMaterial);
                bool canUseSplit = CanUseScreenEdgeSplitVariant(targetMaterial, out string splitReason);

                EditorGUILayout.Space(4);
                if (isSplitShader)
                {
                    EditorGUILayout.HelpBox(
                        L(
                            "現在は Screen Edge 分離バリアントを使用中です。Base pass の Sampler 負荷は下がりますが、描画パスは 1 つ増えます。",
                            "The material is currently using the Screen Edge split variant. This lowers base-pass sampler pressure, but adds one extra draw pass."),
                        MessageType.Info);

                    if (!canUseSplit)
                    {
                        EditorGUILayout.HelpBox(
                            L(
                                $"現在の構成では分離バリアントを安全に維持できません。通常 Opaque へ戻すことをおすすめします。\n理由: {splitReason}",
                                $"The current configuration is no longer safe for the split variant. Switching back to the standard opaque shader is recommended.\nReason: {splitReason}"),
                            MessageType.Warning);
                    }

                    if (GUILayout.Button(L("通常 Opaque シェーダーに戻す", "Switch Back to Standard Opaque")))
                    {
                        if (SetScreenEdgeSplitVariant(false))
                        {
                            return;
                        }
                    }
                }
                else if (canUseSplit)
                {
                    EditorGUILayout.HelpBox(
                        L(
                            "Screen Edge を分離バリアントへ逃がすと、Base pass の Sampler 負荷を下げやすくなります。代わりに描画パスは 1 つ増えます。",
                            "Moving Screen Edge to the split variant lowers base-pass sampler pressure, but adds one extra draw pass."),
                        MessageType.Info);

                    if (GUILayout.Button(L("Screen Edge 分離バリアントに切替", "Switch to Screen Edge Split Variant")))
                    {
                        if (SetScreenEdgeSplitVariant(true))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        L(
                            $"現在の構成では Screen Edge 分離バリアントを使えません。\n理由: {splitReason}",
                            $"The current configuration cannot use the Screen Edge split variant.\nReason: {splitReason}"),
                        MessageType.Warning);
                }
                EditorGUI.indentLevel--;
            }
            else if (IsScreenEdgeSplitShader(targetMaterial))
            {
                EditorGUILayout.HelpBox(
                    L(
                        "Screen Edge は無効ですが、分離バリアントの追加パスは残っています。不要なら通常 Opaque シェーダーへ戻してください。",
                        "Screen Edge is disabled, but the split variant still keeps its extra pass. Switch back to the standard opaque shader if you no longer need it."),
                    MessageType.Info);

                if (GUILayout.Button(L("通常 Opaque シェーダーに戻す", "Switch Back to Standard Opaque")))
                {
                    if (SetScreenEdgeSplitVariant(false))
                    {
                        return;
                    }
                }
            }
            else
            {
                bool canUseSplit = CanUseScreenEdgeSplitVariant(targetMaterial, out string splitReason);
                NataneToonSamplerBudgetEstimator.ToggleEvaluation edgeToggleEvaluation = GetCachedToggleEvaluation("_SCREEN_EDGE");

                if (!edgeToggleEvaluation.CanEnable && canUseSplit)
                {
                    EditorGUILayout.HelpBox(
                        L(
                            $"通常 Opaque シェーダーでは Screen Edge を有効化できません ({edgeToggleEvaluation.AfterEnable.EstimatedSamplers}/{edgeToggleEvaluation.AfterEnable.Limit})。先に分離バリアントへ切り替えると有効化できる可能性があります。",
                            $"Screen Edge cannot be enabled on the standard opaque shader ({edgeToggleEvaluation.AfterEnable.EstimatedSamplers}/{edgeToggleEvaluation.AfterEnable.Limit}). Switching to the split variant first may allow it."),
                        MessageType.Info);

                    if (GUILayout.Button(L("先に Screen Edge 分離バリアントへ切替", "Switch to Screen Edge Split Variant First")))
                    {
                        if (SetScreenEdgeSplitVariant(true))
                        {
                            return;
                        }
                    }
                }
                else if (!edgeToggleEvaluation.CanEnable && !string.IsNullOrEmpty(splitReason))
                {
                    EditorGUILayout.HelpBox(
                        L(
                            $"現在の構成では Screen Edge を通常 shader でも分離バリアントでも安全に追加できません。\n理由: {splitReason}",
                            $"The current configuration cannot safely add Screen Edge on either the standard shader or the split variant.\nReason: {splitReason}"),
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Color Bleeding ---
            bool useBleed = DrawToggle("_COLOR_BLEEDING", "_UseColorBleeding",
                L("色のにじみ", "Color Bleeding"));
            if (useBleed)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_BleedingRadius", L("にじみ半径", "Bleeding Radius"));
                DrawProperty("_BleedingBlend", L("ブレンド", "Blend"));
                DrawHelpToggle("ColorBleeding",
                    L("💧 色のにじみ (Color Bleeding):\n" +
                      "GrabPassの周囲8方向からカラーサンプリングし、\n" +
                      "水彩絵具や印刷のインクがにじむような色の拡散効果を実現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• にじみ半径: にじみの広がり（ピクセル単位）\n" +
                      "  1〜3=微細なにじみ（印刷風）、5〜10=はっきりしたにじみ（水彩風）\n" +
                      "• ブレンド: 0=効果なし、1=完全適用（0.3〜0.6推奨）\n\n" +
                      "💡 Tips:\n" +
                      "• 水彩シミュレーションと併用で本格的な水彩表現\n" +
                      "• 弱めのにじみ(半径2、ブレンド0.2)でアナログ印刷風のにじみ\n" +
                      "• 色の量子化と合わせると版画風の独特な色のにじみに\n" +
                      "• 強すぎると全体がぼやけるので、ブレンド値で調整\n" +
                      "⚡ パフォーマンス: 中程度（8方向サンプリング）",
                      "💧 Color Bleeding:\n" +
                      "Samples GrabPass colors from 8 surrounding directions\n" +
                      "to create watercolor/print ink bleeding effects.\n\n" +
                      "📋 Parameters:\n" +
                      "• Bleeding Radius: spread amount in pixels\n" +
                      "  1-3=subtle (print-like), 5-10=visible (watercolor-like)\n" +
                      "• Blend: 0=off, 1=full (0.3-0.6 recommended)\n\n" +
                      "💡 Tips:\n" +
                      "• Combine with Watercolor for authentic watercolor look\n" +
                      "• Light bleeding (radius 2, blend 0.2) for analog print effect\n" +
                      "• Add Color Quantize for unique woodblock print bleeding\n" +
                      "• Too strong makes everything blurry; adjust with Blend\n" +
                      "⚡ Performance: Medium (8-direction sampling)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Chromatic Aberration ---
            bool useCA = DrawToggle("_CHROMATIC_ABERRATION", "_UseChromaticAberration",
                L("色収差", "Chromatic Aberration"));
            if (useCA)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_CAIntensity", L("強度", "Intensity"));
                DrawProperty("_CABlend", L("ブレンド", "Blend"));
                DrawHelpToggle("ChromaticAberration",
                    L("🌈 色収差 (Chromatic Aberration):\n" +
                      "GrabPassのRGBチャンネルをスクリーン中心からの距離に応じてずらし、\n" +
                      "光学レンズの色収差（フリンジング）を再現します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• 強度: RGBのずれ量（0.001〜0.005=写真風の微細な収差、\n" +
                      "  0.01〜0.02=アニメ演出風、0.05+=極端なグリッチ/サイケデリック）\n" +
                      "• ブレンド: 0=効果なし、1=完全適用\n\n" +
                      "💡 Tips:\n" +
                      "• 画面の端ほど色ずれが強くなります（レンズの特性を再現）\n" +
                      "• 微弱な値(0.002)でリアルなカメラレンズ風の表現に\n" +
                      "• 中程度(0.01)でアニメの「見せ場」カット演出\n" +
                      "• グリッチエフェクトと併用でサイバーパンク/デジタル崩壊表現\n" +
                      "• VRでは酔いの原因になり得るので控えめに\n" +
                      "⚡ パフォーマンス: 軽量（GrabPass 3サンプル）",
                      "🌈 Chromatic Aberration:\n" +
                      "Offsets RGB channels based on distance from screen center\n" +
                      "to simulate optical lens fringing effects.\n\n" +
                      "📋 Parameters:\n" +
                      "• Intensity: RGB offset amount (0.001-0.005=realistic photo lens,\n" +
                      "  0.01-0.02=anime dramatic, 0.05+=extreme glitch/psychedelic)\n" +
                      "• Blend: 0=off, 1=full\n\n" +
                      "💡 Tips:\n" +
                      "• Color shift increases toward screen edges (real lens behavior)\n" +
                      "• Subtle (0.002) for realistic camera lens look\n" +
                      "• Medium (0.01) for anime 'key moment' dramatic shots\n" +
                      "• Combine with Glitch for cyberpunk/digital decay\n" +
                      "• Keep subtle in VR to avoid motion sickness\n" +
                      "⚡ Performance: Light (3 GrabPass samples)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);

            // --- Hand-drawn Outline ---
            bool useHandDrawn = DrawToggle("_OUTLINE_HAND_DRAWN", "_UseHandDrawnOutline",
                L("手書き風アウトライン", "Hand-drawn Outline"));
            if (useHandDrawn)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_OutlineNoiseTex", L("ノイズテクスチャ", "Noise Texture"));
                DrawProperty("_OutlineNoiseTiling", L("ノイズタイリング", "Noise Tiling"));
                DrawProperty("_OutlineWidthVariation", L("太さ変動", "Width Variation"));
                DrawProperty("_OutlineJitterAmount", L("揺れ量", "Jitter Amount"));
                DrawHelpToggle("HandDrawnOutline",
                    L("✏️ 手書き風アウトライン:\n" +
                      "ノイズテクスチャで線の太さをランダムに変動させ、\n" +
                      "頂点ハッシュで位置を微小にブレさせることで、\n" +
                      "手描きペン/インク線のような温かみのあるアウトラインを実現します。\n" +
                      "※ アウトラインセクションの「アウトラインを有効にする」もONにしてください。\n\n" +
                      "📋 パラメータ:\n" +
                      "• ノイズテクスチャ: 線幅の変動パターン（グレースケールノイズ。パーリンノイズ推奨）\n" +
                      "• ノイズタイリング: テクスチャの繰り返し回数（1〜5推奨。高いほど細かい変動）\n" +
                      "• 太さ変動: ノイズによる太さの振れ幅（0.1〜0.3=微細な揺れ、\n" +
                      "  0.5=はっきりした手描き感、0.8〜1.0=ラフスケッチ風）\n" +
                      "• 揺れ量: 頂点位置のランダムずれ（0.5〜2.0=ペンの震え、5+=大胆な歪み）\n\n" +
                      "💡 Tips:\n" +
                      "• 太さ変動だけ使うと「つけペン/Gペン」風の強弱ある線に\n" +
                      "• 揺れ量を足すと「鉛筆ラフスケッチ」風のブレる線に\n" +
                      "• ノイズテクスチャを変えると線の個性が大きく変わる\n" +
                      "• スクリーンエッジ検出と併用で「手描きイラスト + ディテール線」\n" +
                      "• 水彩やハッチングとの組み合わせで統一感のある手描きスタイルに\n" +
                      "⚡ パフォーマンス: 極めて軽量（頂点シェーダーのみ、テクスチャ1サンプル）",
                      "✏️ Hand-drawn Outline:\n" +
                      "Uses noise texture for random width variation and vertex hash for\n" +
                      "micro-jitter, creating warm, hand-drawn pen/ink-style outlines.\n" +
                      "Note: 'Enable Outline' in the Outline section must also be ON.\n\n" +
                      "📋 Parameters:\n" +
                      "• Noise Texture: width variation pattern (grayscale; Perlin noise recommended)\n" +
                      "• Noise Tiling: texture repeat count (1-5 recommended; higher=finer variation)\n" +
                      "• Width Variation: amount of thickness change (0.1-0.3=subtle,\n" +
                      "  0.5=clear hand-drawn feel, 0.8-1.0=rough sketch)\n" +
                      "• Jitter Amount: random vertex offset (0.5-2.0=pen tremor, 5+=bold distortion)\n\n" +
                      "💡 Tips:\n" +
                      "• Width Variation only = 'dip pen/G-pen' style varying thickness\n" +
                      "• Add Jitter = 'pencil rough sketch' style wobbly lines\n" +
                      "• Changing noise texture dramatically changes line character\n" +
                      "• Combine with Screen Edge for 'hand-drawn + detail lines'\n" +
                      "• Pair with Watercolor/Hatching for unified hand-drawn style\n" +
                      "⚡ Performance: Very light (vertex shader only, 1 texture sample)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(SECTION_SPACING);
            DrawHelpToggle("IllustrationStyle",
                L("✏️ イラスト調スタイル:\n" +
                  "• 色の量子化: イラストの塗り分け感を実現\n" +
                  "• 3D LUT: 映画的なカラーグレーディング\n" +
                  "• ハッチング: 鉛筆画風の斜線シェーディング\n" +
                  "• 水彩: エッジダークニング+ウェットエッジ+紙テクスチャ\n" +
                  "• ソフトフィルター: 劇場版アニメのDiffusion効果\n" +
                  "• Kuwahara: 油絵/厚塗り風フィルター\n" +
                  "• エッジ検出: 深度+法線ベースの輪郭線\n" +
                  "• 色のにじみ: 水彩的な色の拡散\n" +
                  "• 色収差: レンズの色ずれ効果\n" +
                  "• 手書きアウトライン: ノイズによる太さ揺れ",
                  "✏️ Illustration Style:\n" +
                  "• Color Quantize: Flat illustration look\n" +
                  "• 3D LUT: Cinematic color grading\n" +
                  "• Hatching: Pencil-style cross-hatching\n" +
                  "• Watercolor: Edge darkening + wet edges + paper texture\n" +
                  "• Soft Filter: Anime diffusion effect\n" +
                  "• Kuwahara: Oil painting filter\n" +
                  "• Screen Edge: Depth+normal edge detection\n" +
                  "• Color Bleeding: Watercolor-like color spread\n" +
                  "• Chromatic Aberration: Lens color shift\n" +
                  "• Hand-drawn Outline: Noise-modulated line width"),
                MessageType.Info);
        }
        EndBoxedSection(GetFoldout("IllustrationStyle"));
    }

    private void DrawOutlineSection()
    {
        SetFoldout("Outline", DrawBoxedSection(L("アウトライン（輪郭線）", "Outline (Contour)"), GetFoldout("Outline"), SectionCategory.Effects, "_OUTLINE"));
        if (GetFoldout("Outline"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ アウトライン最適化で開く", "→ Open Outline Optimizer",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.OptimizationHub, 1, targetMaterial));
            }

            bool enableOutline = DrawToggle("_OUTLINE", "_Outline", L("アウトラインを有効化", "Enable Outline"));

            if (enableOutline)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("アウトライン設定", "Outline Settings"), EditorStyles.boldLabel);

                DrawProperty("_OutlineMode", L("描画方法", "Draw Method"));
                DrawProperty("_OutlineWidth", L("アウトラインの幅", "Outline Width"));
                DrawProperty("_OutlineDistCompMax", L("距離補正の上限", "Distance Compensation Max"));
                DrawHelpToggle("OutlineDistCompMax",
                    L("カメラから離れたときのアウトライン太さの上限を設定します。\n" +
                    "• 値が小さいほど遠距離でのアウトライン膨張を抑えます\n" +
                    "• デフォルト 3.0: 元の太さの最大4倍まで\n" +
                    "• 0: 距離補正なし（近距離と同じ太さ）\n\n" +
                    "💡 VRChat ミラーで離れたときの違和感を軽減できます。",
                    "Sets the upper limit for outline width distance compensation.\n" +
                    "• Lower value = less outline expansion at distance\n" +
                    "• Default 3.0: up to 4x original width\n" +
                    "• 0: no distance compensation (same width as close-up)\n\n" +
                    "💡 Reduces visual inconsistency when moving away from VRChat mirrors."),
                    MessageType.Info);
                DrawColorProperty("_OutlineColor", L("アウトラインの色", "Outline Color"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("アウトラインマスク", "Outline Mask"), EditorStyles.boldLabel);

                bool useOutlineMask = DrawToggle("_OUTLINE_MASK", "_UseOutlineMask", L("アウトラインマスクを有効化", "Enable Outline Mask"));
                if (useOutlineMask)
                {
                    DrawProperty("_OutlineMask", L("アウトラインマスク (R)", "Outline Mask (R)"));
                }
                DrawHelpToggle("OutlineMask",
                    L("アウトラインマスクのR(赤)チャンネルを使用してアウトラインの表示を制御します。\n" +
                    "• 白 (1.0): アウトラインを完全に表示\n" +
                    "• 黒 (0.0): アウトラインを非表示\n" +
                    "• グレー: 部分的に表示",
                    "Uses the R (red) channel of the outline mask to control outline display.\n" +
                    "• White (1.0): Fully show outline\n" +
                    "• Black (0.0): Hide outline\n" +
                    "• Gray: Partially show"),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("アウトライン幅マップ", "Outline Width Map"), EditorStyles.boldLabel);

                bool useOutlineWidthMap = DrawToggle("_OUTLINE_WIDTH_MAP", "_UseOutlineWidthMap", L("アウトライン幅マップを有効化", "Enable Outline Width Map"));
                if (useOutlineWidthMap)
                {
                    DrawProperty("_OutlineWidthMap", L("アウトライン幅マップ (R)", "Outline Width Map (R)"));
                }
                DrawHelpToggle("OutlineWidthMap",
                    L("アウトライン幅マップのR(赤)チャンネルを使用してアウトラインの幅を制御します。\n" +
                    "• 白 (1.0): 通常の幅\n" +
                    "• 黒 (0.0): 幅ゼロ（アウトライン非表示）\n" +
                    "• グレー: 部分的な幅\n\n" +
                    "💡 使い方:\n" +
                    "部位ごとにアウトラインの太さを調整したい場合に使用します。\n" +
                    "例: 顔は細く、体は太くなど。",
                    "Uses the R (red) channel of the outline width map to control outline width.\n" +
                    "• White (1.0): Normal width\n" +
                    "• Black (0.0): Zero width (no outline)\n" +
                    "• Gray: Partial width\n\n" +
                    "💡 Usage:\n" +
                    "Use to adjust outline thickness per body part.\n" +
                    "Example: Thin on face, thick on body."),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("マルチカラーアウトライン", "Multi-Color Outline"), EditorStyles.boldLabel);

                DrawColorProperty("_OutlineColor2", L("アウトラインの色 2", "Outline Color 2"));
                DrawProperty("_OutlineColorMix", L("カラーミックス", "Color Mix"));
                DrawHelpToggle("OutlineMultiColor",
                    L("📍 マルチカラーアウトライン:\n" +
                    "2色のグラデーションアウトラインを作成します。\n\n" +
                    "• 色 2: 2番目の色\n" +
                    "• カラーミックス: 2色の混合度（0-1）\n\n" +
                    "💡 使い方:\n" +
                    "アウトラインにグラデーションやアニメーション効果を追加したい場合に使用します。",
                    "📍 Multi-Color Outline:\n" +
                    "Creates a 2-color gradient outline.\n\n" +
                    "• Color 2: Second color\n" +
                    "• Color Mix: Blend between 2 colors (0-1)\n\n" +
                    "💡 Usage:\n" +
                    "Use to add gradient or animation effects to outlines."),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("テクスチャカラーアウトライン", "Texture Color Outline"), EditorStyles.boldLabel);

                bool texColor = DrawToggle("_OUTLINE_TEXTURE_COLOR", "_OutlineTextureColor", L("テクスチャ連動カラー", "Texture-Based Color"));
                if (texColor)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_OutlineTexColorBlend", L("テクスチャカラーブレンド", "Texture Color Blend"));
                    DrawProperty("_OutlineTexColorDarken", L("テクスチャカラー暗化", "Texture Color Darken"));
                    DrawHelpToggle("OutlineTextureColor",
                        L("🎨 テクスチャカラーアウトライン:\n" +
                        "アウトラインの色をベーステクスチャから取得します。\n" +
                        "アークナイツ：エンドフィールドスタイルのアウトラインを再現。\n\n" +
                        "• ブレンド: テクスチャ色の混合度\n" +
                        "• 暗化: テクスチャ色をどの程度暗くするか\n\n" +
                        "💡 使い方:\n" +
                        "肌色→暗い肌色、髪色→暗い髪色のように\n" +
                        "自然なアウトラインカラーを自動生成します。\n" +
                        "マルチカラーアウトラインと組み合わせ可能です。",
                        "🎨 Texture Color Outline:\n" +
                        "Gets outline color from the base texture.\n" +
                        "Reproduces Arknights: Endfield style outlines.\n\n" +
                        "• Blend: Texture color mix amount\n" +
                        "• Darken: How much to darken texture color\n\n" +
                        "💡 Usage:\n" +
                        "Auto-generates natural outline colors like\n" +
                        "skin tone to dark skin, hair color to dark hair.\n" +
                        "Can be combined with multi-color outline."),
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("スムース法線（Smooth Normal）", "Smooth Normals"), EditorStyles.boldLabel);

                if (targetMaterial != null)
                {
                    NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                        "→ スムース法線ベイクツールを開く", "→ Open Smooth Normal Baker",
                        () => EditorApplication.ExecuteMenuItem(NataneToolMenuPaths.SmoothNormalBaker));
                }

                bool useSmoothNormal = DrawToggle("_SMOOTH_NORMAL", "_SmoothNormal", L("スムース法線を使用", "Use Smooth Normals"));
                if (useSmoothNormal)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_SmoothNormalMode", L("法線ソース", "Normal Source"));

                    // Show texture field only when Mode 2 (Baked Normal Texture) is selected
                    MaterialProperty smoothNormalMode = FindProperty("_SmoothNormalMode", properties, false);
                    if (smoothNormalMode != null && smoothNormalMode.floatValue > 1.5f)
                    {
                        DrawProperty("_SmoothNormalTex", L("スムース法線テクスチャ", "Smooth Normal Texture"));
                    }

                    EditorGUILayout.Space(3);
                    DrawProperty("_SmoothNormalShadingBlend", L("シェーディングブレンド", "Shading Blend"));

                    DrawHelpToggle("SmoothNormal",
                        L("スムース法線（Smooth Object Normal）:\n" +
                        "ハードエッジのあるモデルでアウトラインが割れる問題を解決します。\n" +
                        "また、影の出方をなじませて滑らかにすることもできます。\n\n" +
                        "• 頂点カラー ObjectSpace: 最もシンプル。ベイクツールで事前にベイク必要。\n" +
                        "• 頂点カラー TangentSpace: ノーマルマップと共存可能。lilToon互換。\n" +
                        "• テクスチャ: 最高品質。ベイクドノーマルマップテクスチャを使用。\n\n" +
                        "シェーディングブレンド:\n" +
                        "0 = アウトラインのみに適用（影には影響なし）\n" +
                        "0.3〜0.5 = 推奨。ハードエッジの影をなじませつつディテールを維持\n" +
                        "1.0 = 完全にスムース法線でシェーディング（影が最も滑らか）\n\n" +
                        "💡 使い方:\n" +
                        "1. Tools > Natane > メッシュ > スムース法線ベイク でベイク実行\n" +
                        "2. ベイクしたメッシュをモデルに適用\n" +
                        "3. ここで「スムース法線を使用」を有効化\n" +
                        "4. シェーディングブレンドで影のなじみ具合を調整",
                        "Smooth Normal (Smooth Object Normal):\n" +
                        "Fixes outline splitting on models with hard edges.\n" +
                        "Also smooths shadow transitions.\n\n" +
                        "• Vertex Color ObjectSpace: Simplest. Requires pre-bake with bake tool.\n" +
                        "• Vertex Color TangentSpace: Compatible with normal maps. lilToon compatible.\n" +
                        "• Texture: Highest quality. Uses baked normal map texture.\n\n" +
                        "Shading Blend:\n" +
                        "0 = Apply to outline only (no effect on shadows)\n" +
                        "0.3-0.5 = Recommended. Smooths hard edge shadows while keeping detail\n" +
                        "1.0 = Full smooth normal shading (smoothest shadows)\n\n" +
                        "💡 Usage:\n" +
                        "1. Bake via Tools > Natane > Mesh > Smooth Normal Bake\n" +
                        "2. Apply baked mesh to model\n" +
                        "3. Enable 'Use Smooth Normal' here\n" +
                        "4. Adjust shadow smoothness with Shading Blend"),
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("コーナーギャップ修正", "Corner Gap Fix"), EditorStyles.boldLabel);

                // _SMOOTH_NORMAL OFF時のみ警告+フォールバック表示
                MaterialProperty smoothNormalProp = FindProperty("_SmoothNormal", properties, false);
                bool isSmoothNormalOff = smoothNormalProp == null || smoothNormalProp.floatValue < 0.5f;
                if (isSmoothNormalOff)
                {
                    EditorGUILayout.HelpBox(
                        L("ハードエッジのモデルでアウトラインが割れる場合、" +
                        "「スムース法線」の使用が最善の解決策です。\n" +
                        "以下のパラメータはスムース法線が使えない場合の簡易対策です。",
                        "If outlines break on hard-edge models, " +
                        "using 'Smooth Normals' is the best solution.\n" +
                        "The following parameters are workarounds when smooth normals cannot be used."),
                        MessageType.Info);
                    DrawProperty("_OutlineCornerSmooth", L("コーナースムージング", "Corner Smoothing"));
                    DrawHelpToggle("OutlineCornerSmooth",
                        L("コーナースムージング:\n" +
                        "頂点法線を頂点位置方向（オブジェクト中心→頂点）にブレンドします。\n" +
                        "ハードエッジのコーナーで法線がバラバラになるのを緩和します。\n\n" +
                        "• 0: 無効（デフォルト）\n" +
                        "• 0.3〜0.5: 推奨。コーナーギャップを軽減しつつ形状を維持\n" +
                        "• 1.0: 完全に位置方向の法線を使用（球体状に膨張）\n\n" +
                        "⚠️ スムース法線ベイクの方がより正確な結果が得られます。",
                        "Corner Smoothing:\n" +
                        "Blends vertex normals towards position direction (center to vertex).\n" +
                        "Reduces normals scattering at hard-edge corners.\n\n" +
                        "• 0: Disabled (Default)\n" +
                        "• 0.3-0.5: Recommended. Reduces corner gaps while maintaining shape\n" +
                        "• 1.0: Fully use position-direction normals (spherical expansion)\n\n" +
                        "⚠️ Smooth normal baking provides more accurate results."),
                        MessageType.Info);
                }

                // エッジ幅補正は常時表示
                DrawProperty("_OutlineEdgeCompensation", L("エッジ幅補正", "Edge Width Compensation"));
                DrawHelpToggle("OutlineEdgeCompensation",
                    L("エッジ幅補正:\n" +
                    "シャープエッジ部分でアウトライン幅を自動縮小します。\n" +
                    "元の法線と使用中の法線の不一致度を検出し、\n" +
                    "差が大きい箇所ほどアウトラインを細くします。\n\n" +
                    "• 0: 無効（デフォルト）\n" +
                    "• 0.5〜1.0: 推奨。コーナーの突出を抑制\n\n" +
                    "💡 スムース法線のON/OFFに関わらず使用可能です。",
                    "Edge Width Compensation:\n" +
                    "Auto-shrinks outline width at sharp edges.\n" +
                    "Detects mismatch between original and current normals,\n" +
                    "making outlines thinner where difference is large.\n\n" +
                    "• 0: Disabled (default)\n" +
                    "• 0.5-1.0: Recommended. Suppresses corner protrusion\n\n" +
                    "💡 Works regardless of Smooth Normal ON/OFF."),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);

                float outlineMode = targetMaterial.GetFloat("_OutlineMode");
                if (outlineMode < FLOAT_COMPARISON_THRESHOLD)
                {
                    DrawHelpToggle("OutlineInvertedHull",
                        L("【反転ハル方式】\n" +
                        "法線方向に頂点を押し出してアウトラインを描画します。\n" +
                        "• 利点: 一般的に安定した結果、距離補正により遠近で一貫した太さ\n" +
                        "• 欠点: ローポリモデルやハードエッジで乱れる場合があります\n" +
                        "• カメラ距離による自動調整機能を搭載",
                        "[Inverted Hull Method]\n" +
                        "Draws outline by pushing vertices along normals.\n" +
                        "• Pros: Generally stable, distance correction gives consistent width\n" +
                        "• Cons: May distort on low-poly models or hard edges\n" +
                        "• Includes auto camera distance adjustment"),
                        MessageType.Info);
                }
                else
                {
                    DrawHelpToggle("OutlineBackface",
                        L("【背面法】\n" +
                        "メッシュを拡大して背面を描画します。\n" +
                        "• 利点: スムーズなアウトライン、ハイポリモデルに適しています\n" +
                        "• 欠点: 内部構造が見える場合があります\n" +
                        "• 距離補正により遠くでも視認性を維持",
                        "[Backface Method]\n" +
                        "Draws outline by scaling mesh and rendering backfaces.\n" +
                        "• Pros: Smooth outlines, ideal for high-poly models\n" +
                        "• Cons: Internal structure may be visible\n" +
                        "• Distance correction maintains visibility at far range"),
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Outline"));
    }

    private void DrawEmissionSection()
    {
        SetFoldout("Emission", DrawBoxedSection(L("エミッション（発光）", "Emission (Glow)"), GetFoldout("Emission"), SectionCategory.Effects, "_EMISSION"));
        if (GetFoldout("Emission"))
        {

            bool enableEmission = DrawToggle("_EMISSION", "_Emission", L("エミッションを有効化", "Enable Emission"));

            if (enableEmission)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_EmissionColor", L("エミッションの色", "Emission Color"), true);
                DrawProperty("_EmissionMap", L("エミッションマップ", "Emission Map"));

                EditorGUILayout.Space();
                DrawEmissionUVAnimationSettings();

                DrawProperty("_EmissionPulseSpeed", L("パルス速度", "Pulse Speed"));
                DrawProperty("_EmissionPulseAmplitude", L("パルスの振幅", "Pulse Amplitude"));

                EditorGUILayout.Space();
                DrawProperty("_EmissionMask", L("エミッションマスク", "Emission Mask"));
                DrawHelpToggle("EmissionMask", L("白 = エミッションあり、黒 = エミッションなし", "White = Emission on, Black = Emission off"), MessageType.Info);
                DrawUVAnimationSettings("_EmissionMaskScrollSpeed", "_EmissionMaskRotateSpeed", L("エミッションマスク", "Emission Mask"));

                EditorGUILayout.Space();
                DrawProperty("_EmissionGlow", L("エミッショングロー（ブルーム）", "Emission Glow (Bloom)"));
                DrawHelpToggle("EmissionGlow",
                    L("✨ エミッショングロー（ブルーム効果）:\n" +
                    "発光部分を滲ませて明るく広げ、柔らかく\n" +
                    "輝いているような効果を追加します。\n\n" +
                    "• 0 = グローなし（シャープな発光）\n" +
                    "• 0.3-0.5 = 適度なグロー（推奨）\n" +
                    "• 0.7-1.0 = 強いグロー（強烈な輝き）\n\n" +
                    "💡 ヒント:\n" +
                    "HDRカラーと組み合わせると、より鮮やかな\n" +
                    "発光効果が得られます。",
                    "✨ Emission Glow (Bloom Effect):\n" +
                    "Bleeds and brightens emission areas for a soft\n" +
                    "glowing effect.\n\n" +
                    "• 0 = No glow (sharp emission)\n" +
                    "• 0.3-0.5 = Moderate glow (Recommended)\n" +
                    "• 0.7-1.0 = Strong glow (intense shine)\n\n" +
                    "💡 Tip:\n" +
                    "Combine with HDR colors for even more\n" +
                    "vivid emission effects."),
                    MessageType.None);

                DrawBlendControls(materialEditor, targetMaterial, "_EmissionBlend", "_EmissionBlendMode", "_EmissionBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_EmissionDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Emission"));
    }

    private void DrawVirtualExpressionSection()
    {
        SetFoldout("VirtualExpression", DrawBoxedSection(L("バーチャル表現", "Virtual Expression"), GetFoldout("VirtualExpression"), SectionCategory.Effects));
        if (GetFoldout("VirtualExpression"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ ディゾルブスタジオで開く", "→ Open in Dissolve Studio",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.EffectStudio, 3, targetMaterial));
            }

            // Dissolve Effect
            bool enableDissolve = DrawToggle("_DISSOLVE", "_Dissolve", L("ディゾルブを有効化", "Enable Dissolve"));
            if (enableDissolve)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_DissolveAmount", L("ディゾルブ量", "Dissolve Amount"));
                DrawHelpToggle("DissolveAmount", L("0 = 完全に表示、1 = 完全に消滅", "0 = Fully visible, 1 = Fully dissolved"), MessageType.Info);

                DrawProperty("_DissolveTex", L("ディゾルブテクスチャ（ノイズ）", "Dissolve Texture (Noise)"));
                DrawUVAnimationSettings("_DissolveTexScrollSpeed", "_DissolveTexRotateSpeed", L("ディゾルブ", "Dissolve"));
                DrawProperty("_DissolveEdgeWidth", L("エッジの幅", "Edge Width"));
                DrawColorProperty("_DissolveEdgeColor", L("エッジの色", "Edge Color"), true);
                DrawProperty("_DissolveEdgeIntensity", L("エッジの強さ", "Edge Intensity"));

                EditorGUILayout.Space();
                DrawProperty("_DissolveMask", L("ディゾルブマスク", "Dissolve Mask"));
                DrawHelpToggle("DissolveMask", L("白 = ディゾルブあり、黒 = ディゾルブなし", "White = Dissolve on, Black = Dissolve off"), MessageType.Info);

                DrawHelpToggle("DissolveInfo", L("ディゾルブはVRChatアバターの出現アニメーションに最適な消滅・分解エフェクトを作成します。ディゾルブ量パラメータをアニメーションさせることで、オブジェクトを出現または消滅させることができます。", "Dissolve creates vanishing/disintegrating effects ideal for VRChat avatar appearance animations. Animate the dissolve amount parameter to make objects appear or disappear."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_DissolveBlend", "_DissolveBlendMode", "_DissolveBlur");

                EditorGUILayout.Space();
                DrawProperty("_DissolveCoordMode", L("座標モード", "Coordinate Mode"));
                float dissolveCoordMode = FindProperty("_DissolveCoordMode", properties).floatValue;
                if (dissolveCoordMode > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_DissolveWorldAxis", L("軸方向", "Axis Direction"));
                    DrawProperty("_DissolveWorldMin", L("最小値", "Minimum Value"));
                    DrawProperty("_DissolveWorldMax", L("最大値", "Maximum Value"));
                    DrawProperty("_DissolveNoiseBlend", L("ノイズテクスチャブレンド", "Noise Texture Blend"));
                    DrawHelpToggle("DissolveCoordMode",
                        L("🌍 ワールド座標ディゾルブ:\n" +
                        "UV座標ではなく、ワールド/ローカル座標で\n" +
                        "ディゾルブを制御します。\n\n" +
                        "• 軸方向: ディゾルブの進行軸（X/Y/Z）\n" +
                        "• 最小/最大値: ディゾルブ範囲の座標\n" +
                        "• ノイズブレンド: テクスチャノイズの混合量\n" +
                        "  0=座標のみ、1=テクスチャと完全混合",
                        "🌍 World Coordinate Dissolve:\n" +
                        "Controls dissolve using world/local coordinates\n" +
                        "instead of UV coordinates.\n\n" +
                        "• Axis: Dissolve progression axis (X/Y/Z)\n" +
                        "• Min/Max: Dissolve range coordinates\n" +
                        "• Noise Blend: Texture noise blend amount\n" +
                        "  0=coordinates only, 1=full texture blend"),
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Hue Shift
            bool enableHueShift = DrawToggle("_HUE_SHIFT", "_HueShiftEnable", L("色相シフトを有効化", "Enable Hue Shift"));
            if (enableHueShift)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_HueShift", L("色相シフト", "Hue Shift"));
                DrawHelpToggle("HueShift", L("マテリアル全体の色相を変更します。0 = 変更なし、0.5 = 補色、1 = 完全な回転。VRChatでの色変更エフェクトに最適です。", "Changes the hue of the entire material. 0 = No change, 0.5 = Complementary, 1 = Full rotation. Ideal for color-changing effects in VRChat."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_HueShiftBlend", null, "_HueShiftBlur");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Alpha Mask
            bool useAlphaMask = DrawToggle("_ALPHA_MASK", "_UseAlphaMask", L("アルファマスクを使用", "Use Alpha Mask"));
            if (useAlphaMask)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_AlphaMask", L("アルファマスク", "Alpha Mask"));
                DrawHelpToggle("AlphaMask",
                    L("🎭 アルファマスク（部分的な半透明）:\n\n" +
                    "テクスチャのRチャンネルを使用して、マテリアルの透明度を\n" +
                    "部分的に制御します。\n\n" +
                    "• 白（1.0）= 完全に不透明\n" +
                    "• グレー（0.5）= 半透明\n" +
                    "• 黒（0.0）= 完全に透明\n\n" +
                    "💡 用途:\n" +
                    "　・ガラスの一部だけを透明に\n" +
                    "　・服の一部を半透明に（シースルー効果）\n" +
                    "　・グラデーションで端を透明に（フェードアウト）\n" +
                    "　・複雑な形状の透明度制御\n\n" +
                    "⚠️ 注意:\n" +
                    "半透明効果を使用するには、レンダリングモードを\n" +
                    "「Transparent」に設定してください。",
                    "🎭 Alpha Mask (Partial Transparency):\n\n" +
                    "Uses the R channel of the texture to partially\n" +
                    "control material transparency.\n\n" +
                    "• White (1.0) = Fully opaque\n" +
                    "• Gray (0.5) = Semi-transparent\n" +
                    "• Black (0.0) = Fully transparent\n\n" +
                    "💡 Uses:\n" +
                    "  - Make only parts of glass transparent\n" +
                    "  - Make parts of clothing see-through\n" +
                    "  - Gradient edge fade-out\n" +
                    "  - Complex shape transparency control\n\n" +
                    "⚠️ Note:\n" +
                    "Set rendering mode to 'Transparent'\n" +
                    "to use semi-transparency effects."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("VirtualExpression"));
    }

    private void DrawNormalMapSection()
    {
        SetFoldout("NormalMap", DrawBoxedSection(L("ノーマルマップ", "Normal Map"), GetFoldout("NormalMap"), SectionCategory.Advanced, "_NORMALMAP"));
        if (GetFoldout("NormalMap"))
        {
            NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                "→ 法線ベイクツールで開く", "→ Open Normal Baker",
                () => NataneToolMenuPaths.TryExecute(NataneToolMenuPaths.SmoothNormalBaker));
            bool useNormalMap = DrawToggle("_NORMALMAP", "_UseNormalMap", L("ノーマルマップを使用", "Use Normal Map"));

            if (useNormalMap)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_BumpMap", L("ノーマルマップ", "Normal Map"));

                // Auto-generate Normal Map button
                bool canAutoGenerateNormal = MapGeneratorGUIBridge.CanGenerateNormalForMaterial(targetMaterial);
                EditorGUI.BeginDisabledGroup(!canAutoGenerateNormal);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("Normal を自動生成", "Auto-Generate Normal"), GUILayout.Height(22), GUILayout.Width(180)))
                {
                    MapGeneratorGUIBridge.GenerateNormalForMaterial(targetMaterial);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                if (!canAutoGenerateNormal)
                {
                    EditorGUILayout.HelpBox(L("Albedo テクスチャ (_MainTex) が必要です。", "Albedo texture (_MainTex) is required."), MessageType.Info);
                }

                DrawProperty("_BumpScale", L("ノーマルのスケール", "Normal Scale"));
                DrawUVAnimationSettings("_BumpMapScrollSpeed", "_BumpMapRotateSpeed", L("ノーマルマップ", "Normal Map"));
                DrawHelpToggle("NormalMap",
                    L("🗺️ ノーマルマップ:\n" +
                      "テクスチャで法線方向を変化させ、ポリゴンを増やさずに凹凸の表現を追加します。\n\n" +
                      "📋 パラメータ:\n" +
                      "• ノーマルマップ: タンジェントスペース法線テクスチャ（青紫色のテクスチャ）\n" +
                      "  → Unity標準のNormal Map形式で「Texture Type: Normal map」を設定\n" +
                      "• スケール: 凹凸の強さ（0=凹凸なし、1=標準、2+=強調）\n" +
                      "  → 負の値で凹凸を反転\n" +
                      "• UVアニメーション: スクロール/回転で流れるような凹凸に\n\n" +
                      "💡 Tips:\n" +
                      "• トゥーンシェーディングの影境界に微妙な凹凸感を加えたい時に\n" +
                      "• スケール 0.5〜1.0 で自然な凹凸（強すぎるとトゥーン感が崩れる場合あり）\n" +
                      "• 服のシワや肌のディテールをローポリモデルに追加するのに最適\n" +
                      "• UVスクロールで水面/溶岩の流れる凹凸を表現可能\n" +
                      "⚡ パフォーマンス: 極めて軽量（テクスチャ1サンプル）",
                      "🗺️ Normal Map:\n" +
                      "Modifies surface normals via texture for bump detail without extra polygons.\n\n" +
                      "📋 Parameters:\n" +
                      "• Normal Map: tangent-space normal texture (blue/purple texture)\n" +
                      "  → Set 'Texture Type: Normal map' in Unity import settings\n" +
                      "• Scale: bump strength (0=flat, 1=standard, 2+=exaggerated)\n" +
                      "  → Negative values invert the bumps\n" +
                      "• UV Animation: scroll/rotate for flowing bump effects\n\n" +
                      "💡 Tips:\n" +
                      "• Adds subtle depth variation to toon shading boundaries\n" +
                      "• Scale 0.5-1.0 for natural bumps (too high may break toon look)\n" +
                      "• Great for adding cloth wrinkles or skin detail to low-poly models\n" +
                      "• UV scroll for flowing water/lava surface bumps\n" +
                      "⚡ Performance: Very light (1 texture sample)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("NormalMap"));
    }

    private void DrawReflectionSection()
    {
        SetFoldout("Reflection", DrawBoxedSection(L("反射 / キューブマップ", "Reflection / Cubemap"), GetFoldout("Reflection"), SectionCategory.Environment, "_REFLECTION"));
        if (GetFoldout("Reflection"))
        {
            bool enableReflection = DrawToggle("_REFLECTION", "_Reflection", L("リフレクションを有効化", "Enable Reflection"));

            if (enableReflection)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_ReflectionCube", L("リフレクションキューブマップ", "Reflection Cubemap"));
                DrawColorProperty("_ReflectionColor", L("リフレクションの色", "Reflection Color"));
                DrawProperty("_ReflectionIntensity", L("リフレクションの強さ", "Reflection Intensity"));
                DrawProperty("_Smoothness", L("滑らかさ（光沢）", "Smoothness (Gloss)"));
                DrawProperty("_Metallic", L("メタリック", "Metallic"));

                // Roughness Map auto-generation button
                bool roughnessHasAlbedo = targetMaterial != null && targetMaterial.HasProperty("_MainTex") && targetMaterial.GetTexture("_MainTex") != null;
                EditorGUI.BeginDisabledGroup(!roughnessHasAlbedo);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("Roughness を自動生成", "Auto-Generate Roughness"), GUILayout.Height(22), GUILayout.Width(200)))
                {
                    MapGeneratorGUIBridge.GenerateRoughnessForMaterial(targetMaterial);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(L("フレネル設定（柔らかい反射）", "Fresnel Settings (Soft Reflection)"), EditorStyles.boldLabel);
                DrawProperty("_FresnelPower", L("フレネルパワー", "Fresnel Power"));
                DrawProperty("_FresnelSoftness", L("フレネルソフトネス", "Fresnel Softness"));
                DrawHelpToggle("FresnelSoftness",
                    L("✨ フレネルソフトネス:\n" +
                    "反射の境界を柔らかくして、より自然で芸術的な反射を実現します。\n" +
                    "• 0 = シャープな境界（現実的）\n" +
                    "• 0.5-0.7 = 柔らかい境界（推奨）\n" +
                    "• 1.0 = 非常に柔らかい境界（芸術的）",
                    "✨ Fresnel Softness:\n" +
                    "Softens reflection boundaries for more natural, artistic reflections.\n" +
                    "• 0 = Sharp boundary (realistic)\n" +
                    "• 0.5-0.7 = Soft boundary (Recommended)\n" +
                    "• 1.0 = Very soft boundary (artistic)"),
                    MessageType.None);

                DrawProperty("_ReflectionBlendMode", L("ブレンドモード", "Blend Mode"));
                DrawHelpToggle("ReflectionBlendMode",
                    L("• 0 = 加算（明るく追加）\n" +
                    "• 1 = オーバーレイ（自然な統合）",
                    "• 0 = Additive (brighter addition)\n" +
                    "• 1 = Overlay (natural integration)"),
                    MessageType.Info);

                EditorGUILayout.Space();
                DrawProperty("_ReflectionMask", L("リフレクションマスク", "Reflection Mask"));
                DrawHelpToggle("ReflectionMask", L("白 = リフレクションあり、黒 = リフレクションなし", "White = Reflection on, Black = Reflection off"), MessageType.Info);

                DrawHelpToggle("ReflectionInfo", L("キューブマップを使用して環境反射をシミュレートします。金属やガラスなどの反射素材に最適です。", "Simulates environment reflections using cubemaps. Ideal for reflective materials like metal and glass."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_ReflectionBlend");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_ReflectionDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Reflection"));
    }

    private void DrawIridescenceSection()
    {
        SetFoldout("Iridescence", DrawBoxedSection(L("イリデッセンス（玉虫色）", "Iridescence"), GetFoldout("Iridescence"), SectionCategory.Environment, "_IRIDESCENCE"));
        if (GetFoldout("Iridescence"))
        {
            bool enableIridescence = DrawToggle("_IRIDESCENCE", "_Iridescence", L("イリデッセンスを有効化", "Enable Iridescence"));

            if (enableIridescence)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("イリデッセンス設定", "Iridescence Settings"), EditorStyles.boldLabel);

                DrawColorProperty("_IridescenceColor", L("イリデッセンスの色", "Iridescence Color"));
                DrawProperty("_IridescenceIntensity", L("強度", "Intensity"));
                DrawProperty("_IridescenceHueShift", L("色相シフト", "Hue Shift"));
                DrawProperty("_IridescenceSize", L("サイズ（周波数）", "Size (Frequency)"));

                EditorGUILayout.Space(SECTION_SPACING);

                DrawProperty("_IridescenceMask", L("イリデッセンスマスク", "Iridescence Mask"));
                DrawHelpToggle("IridescenceMask",
                    L("イリデッセンスマスクのR(赤)チャンネルを使用してイリデッセンスの表示領域を制御します。\n" +
                    "• 白 (1.0): イリデッセンスを完全に表示\n" +
                    "• 黒 (0.0): イリデッセンスを非表示\n" +
                    "• グレー: 部分的に表示",
                    "Uses the R (red) channel of the iridescence mask to control display area.\n" +
                    "• White (1.0): Fully show iridescence\n" +
                    "• Black (0.0): Hide iridescence\n" +
                    "• Gray: Partially show"),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                DrawHelpToggle("IridescenceInfo",
                    L("📍 イリデッセンス（Iridescence）:\n" +
                    "見る角度によって色が変化する玉虫色効果を追加します。\n\n" +
                    "• 色: ベースとなる色調\n" +
                    "• 強度: 効果の明るさ\n" +
                    "• 色相シフト: 色の変化開始位置\n" +
                    "• サイズ: 色の変化の周波数（大きいほど早く変化）\n\n" +
                    "💡 使い方:\n" +
                    "シャボン玉、オイルスリック、昆虫の羽、魚の鱗など、\n" +
                    "光の干渉による虹色効果を表現したい場合に使用します。",
                    "📍 Iridescence:\n" +
                    "Adds color-shifting effect that changes with viewing angle.\n\n" +
                    "• Color: Base color tone\n" +
                    "• Intensity: Effect brightness\n" +
                    "• Hue Shift: Color change start position\n" +
                    "• Size: Color change frequency (larger=faster change)\n\n" +
                    "💡 Usage:\n" +
                    "Use for soap bubbles, oil slick, insect wings, fish scales,\n" +
                    "or any surface requiring rainbow interference effects."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_IridescenceBlend", "_IridescenceBlendMode", "_IridescenceBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_IridescenceDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Iridescence"));
    }

    private void DrawEnvironmentalRimSection()
    {
        SetFoldout("EnvironmentalRim", DrawBoxedSection(L("環境リム", "Environmental Rim"), GetFoldout("EnvironmentalRim"), SectionCategory.Environment, "_ENV_RIM"));
        if (GetFoldout("EnvironmentalRim"))
        {
            bool enableEnvRim = DrawToggle("_ENV_RIM", "_EnvRim", L("環境リムを有効化", "Enable Environmental Rim"));

            if (enableEnvRim)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_EnvRimCube", L("環境キューブマップ", "Environment Cubemap"));
                DrawColorProperty("_EnvRimColor", L("環境リムの色", "Environmental Rim Color"));
                DrawProperty("_EnvRimPower", L("環境リムのパワー", "Environmental Rim Power"));
                DrawProperty("_EnvRimIntensity", L("環境リムの強さ", "Environmental Rim Intensity"));

                EditorGUILayout.Space();
                DrawProperty("_EnvRimMask", L("環境リムマスク", "Environmental Rim Mask"));
                DrawHelpToggle("EnvRimMask", L("白 = 環境リムあり、黒 = 環境リムなし", "White = Environmental rim on, Black = Environmental rim off"), MessageType.Info);

                DrawHelpToggle("EnvRimInfo", L("キューブマップを使用して環境に基づいたリムライト効果を作成します。", "Creates environment-based rim light effects using cubemaps."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_EnvRimBlend", "_EnvRimBlendMode", "_EnvRimBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_EnvRimDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("EnvironmentalRim"));
    }

    private void DrawParallaxSection()
    {
        SetFoldout("Parallax", DrawBoxedSection(L("視差マッピング（パララックス）", "Parallax Mapping"), GetFoldout("Parallax"), SectionCategory.Advanced, "_PARALLAX"));
        if (GetFoldout("Parallax"))
        {
            bool enableParallax = DrawToggle("_PARALLAX", "_Parallax", L("視差マッピングを有効化", "Enable Parallax Mapping"));

            if (enableParallax)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_ParallaxMap", L("高さマップ", "Height Map"));
                DrawProperty("_ParallaxScale", L("視差のスケール", "Parallax Scale"));
                DrawProperty("_ParallaxMinSamples", L("最小サンプル数", "Min Samples"));
                DrawProperty("_ParallaxMaxSamples", L("最大サンプル数", "Max Samples"));

                DrawHelpToggle("ParallaxInfo", L("視差マッピングは高さマップを使用してサーフェスに深度の錯覚を作成します。石や壁などの素材に最適です。", "Parallax mapping uses height maps to create depth illusions on surfaces. Ideal for stone and wall materials."), MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Parallax"));
    }

    private void DrawVertexAnimationSection()
    {
        SetFoldout("VertexAnimation", DrawBoxedSection(L("頂点アニメーション（風/呼吸/脈動）", "Vertex Animation (Wind/Breath/Pulse)"), GetFoldout("VertexAnimation"), SectionCategory.Advanced, "_VERTEX_ANIMATION"));
        if (GetFoldout("VertexAnimation"))
        {
            bool enableVertexAnim = DrawToggle("_VERTEX_ANIMATION", "_VertexAnimation", L("頂点アニメーションを有効化", "Enable Vertex Animation"));

            if (enableVertexAnim)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                DrawProperty("_VertexAnimType", L("アニメーション種類", "Animation Type"));
                DrawProperty("_VertexAnimSpeed", L("速度", "Speed"));
                DrawProperty("_VertexAnimStrength", L("強度", "Intensity"));
                DrawProperty("_VertexAnimFrequency", L("周波数", "Frequency"));

                EditorGUILayout.Space(SECTION_SPACING);
                bool useVertexAnimMask = DrawToggle("_VERTEX_ANIM_MASK", "_UseVertexAnimMask", L("マスク使用", "Use Mask"));
                if (useVertexAnimMask)
                {
                    DrawProperty("_VertexAnimMask", L("頂点アニメーションマスク", "Vertex Animation Mask"));
                }
                DrawHelpToggle("VertexAnimation",
                    L("🌊 頂点アニメーション:\n" +
                      "頂点シェーダーで自動的にメッシュを動かすプロシージャルアニメーションです。\n\n" +
                      "📋 パラメータ:\n" +
                      "• アニメーション種類:\n" +
                      "  0=風揺れ（草木、髪、布向け。Y軸基準で上方ほど大きく揺れる）\n" +
                      "  1=呼吸（キャラ向け。全体が膨張収縮するサイン波）\n" +
                      "  2=脈動（エフェクト向け。法線方向に膨らむ波動）\n" +
                      "• 速度: アニメーションのサイクル速度（1=標準、0.5=ゆっくり、3=速い）\n" +
                      "• 強度: 動きの大きさ（0.01〜0.05=微細な揺れ、0.1=目に見える動き、0.5+=大胆な変形）\n" +
                      "• 周波数: 波の細かさ（低い=大きな波、高い=細かい波。風揺れで1〜5推奨）\n\n" +
                      "📋 マスク:\n" +
                      "• マスクテクスチャ: 白=アニメーション適用 / 黒=固定\n" +
                      "  → 風揺れ時は根元を黒、先端を白にすると自然な揺れに\n" +
                      "  → 呼吸時はお腹周りだけ白にすると呼吸感UP\n\n" +
                      "💡 Tips:\n" +
                      "• 風揺れ: 草木/髪/ケープに。強度0.02〜0.05、周波数2〜4\n" +
                      "• 呼吸: キャラの胸元に。強度0.005〜0.02、速度0.5〜1.0\n" +
                      "• スミア（Smear）と組み合わせて動きの残像を追加可能\n" +
                      "⚡ パフォーマンス: 極めて軽量（頂点シェーダーのsin/cos演算のみ）",
                      "🌊 Vertex Animation:\n" +
                      "Procedural vertex animation that automatically moves the mesh.\n\n" +
                      "📋 Parameters:\n" +
                      "• Animation Type:\n" +
                      "  0=Wind (grass/hair/cloth; Y-axis based, more motion at top)\n" +
                      "  1=Breathing (characters; uniform sine wave expansion)\n" +
                      "  2=Pulse (effects; normal-direction wave)\n" +
                      "• Speed: cycle speed (1=standard, 0.5=slow, 3=fast)\n" +
                      "• Intensity: motion amount (0.01-0.05=subtle, 0.1=visible, 0.5+=dramatic)\n" +
                      "• Frequency: wave detail (low=broad, high=fine; 1-5 for wind)\n\n" +
                      "📋 Mask:\n" +
                      "• Mask Texture: white=animate / black=fixed\n" +
                      "  → For wind: black at roots, white at tips for natural sway\n" +
                      "  → For breathing: white on chest area for natural feel\n\n" +
                      "💡 Tips:\n" +
                      "• Wind: grass/hair/capes. Intensity 0.02-0.05, frequency 2-4\n" +
                      "• Breathing: character chest. Intensity 0.005-0.02, speed 0.5-1.0\n" +
                      "• Combine with Smear for motion trails\n" +
                      "⚡ Performance: Very light (vertex shader sin/cos only)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("VertexAnimation"));
    }

    private void DrawVATSection()
    {
        SetFoldout("VAT", DrawBoxedSection(L("VAT（頂点アニメーション）", "VAT (Vertex Animation Texture)"), GetFoldout("VAT"), SectionCategory.Advanced, "_VAT"));
        if (GetFoldout("VAT"))
        {
            bool enableVAT = DrawToggle("_VAT", "_VAT", L("VATアニメーションを有効化", "Enable VAT Animation"));

            if (enableVAT)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("VAT設定（Houdini互換）", "VAT Settings (Houdini Compatible)"), EditorStyles.boldLabel);

                DrawProperty("_VATPositionMap", L("VAT位置マップ", "VAT Position Map"));
                DrawProperty("_VATNumOfFrames", L("フレーム数", "Number of Frames"));
                DrawProperty("_VATSpeed", L("アニメーション速度", "Animation Speed"));
                DrawProperty("_VATIntensity", L("アニメーション強度", "Animation Intensity"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("VAT位置パラメータ", "VAT Position Parameters"), EditorStyles.boldLabel);
                DrawProperty("_VATPositionMin", L("位置最小値", "Position Min"));
                DrawProperty("_VATPositionMax", L("位置最大値", "Position Max"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("VAT法線マップ（オプション）", "VAT Normal Map (Optional)"), EditorStyles.boldLabel);

                bool useVATNormal = DrawToggle("_VAT_NORMAL", "_VATNormal", L("VAT法線マップを使用", "Use VAT Normal Map"));
                if (useVATNormal)
                {
                    DrawProperty("_VATNormalMap", L("VAT法線マップ", "VAT Normal Map"));
                    DrawProperty("_VATNormalMin", L("法線最小値", "Normal Min"));
                    DrawProperty("_VATNormalMax", L("法線最大値", "Normal Max"));
                }

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("VAT詳細設定", "VAT Advanced Settings"), EditorStyles.boldLabel);
                DrawProperty("_VATPackingMode", L("パッキングモード", "Packing Mode"));
                DrawHelpToggle("VATPackingMode",
                    L("• Absolute (0): 絶対位置モード - 頂点位置を直接置き換えます\n" +
                    "• Offset (1): オフセットモード - 元の位置にオフセットを追加します（推奨）",
                    "• Absolute (0): Absolute position mode - directly replaces vertex positions\n" +
                    "• Offset (1): Offset mode - adds offset to original position (Recommended)"),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                DrawHelpToggle("VATInfo",
                    L("📍 VAT（Vertex Animation Texture）:\n" +
                    "Houdiniで作成した頂点アニメーションをテクスチャとして再生します。\n\n" +
                    "【セットアップ方法】\n" +
                    "1. Houdiniで VAT（ROP Output Driver）を使用してエクスポート\n" +
                    "2. Position Map（.exr）を位置マップとして設定\n" +
                    "3. 必要に応じてNormal Map（.exr）を法線マップとして設定\n" +
                    "4. フレーム数をHoudiniの設定と一致させる\n\n" +
                    "【パラメータ説明】\n" +
                    "• 位置最小値/最大値: Houdiniエクスポート時の範囲（通常 -1 ～ 1）\n" +
                    "• 速度: アニメーション再生速度の倍率\n" +
                    "• 強度: アニメーション効果の強さ（0-2）\n\n" +
                    "💡 使い方:\n" +
                    "流体シミュレーション、布シミュレーション、複雑な変形アニメーションなど、\n" +
                    "リアルタイムでは重すぎる物理演算をベイクして再生できます。\n\n" +
                    "⚠ 注意:\n" +
                    "• VATテクスチャは通常のテクスチャよりメモリを多く使用します\n" +
                    "• VRChatでは適切な解像度とフレーム数を選択してください",
                    "📍 VAT (Vertex Animation Texture):\n" +
                    "Plays vertex animations created in Houdini as textures.\n\n" +
                    "[Setup]\n" +
                    "1. Export using VAT (ROP Output Driver) in Houdini\n" +
                    "2. Set Position Map (.exr) as position map\n" +
                    "3. Optionally set Normal Map (.exr) as normal map\n" +
                    "4. Match frame count with Houdini settings\n\n" +
                    "[Parameters]\n" +
                    "• Position Min/Max: Export range from Houdini (usually -1 to 1)\n" +
                    "• Speed: Animation playback speed multiplier\n" +
                    "• Intensity: Animation effect strength (0-2)\n\n" +
                    "💡 Usage:\n" +
                    "Bake and play back fluid, cloth, and complex deformation\n" +
                    "animations that are too heavy for real-time physics.\n\n" +
                    "⚠ Note:\n" +
                    "• VAT textures use more memory than regular textures\n" +
                    "• Choose appropriate resolution and frame count for VRChat"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("VAT"));
    }

    private void DrawTessellationSection()
    {
        SetFoldout("Tessellation", DrawBoxedSection(L("テッセレーション（曲面スムージング）", "Tessellation (Surface Smoothing)"), GetFoldout("Tessellation"), SectionCategory.Advanced, "_TESSELLATION"));
        if (GetFoldout("Tessellation"))
        {
            bool enableTess = DrawToggle("_TESSELLATION", "_Tessellation", L("テッセレーションを有効化", "Enable Tessellation"));
            if (enableTess)
            {
                DrawProperty("_TessFactor", L("テッセレーション係数", "Tessellation Factor"));
                DrawHelpToggle("TessFactor",
                    L("🔷 テッセレーション係数:\n" +
                    "メッシュの分割数を制御します。\n\n" +
                    "• 1 = 分割なし\n" +
                    "• 2-4 = 軽いスムージング（推奨）\n" +
                    "• 8-16 = 高品質（高負荷）\n\n" +
                    "⚠ VR では両目分のコストがかかります。",
                    "🔷 Tessellation Factor:\n" +
                    "Controls mesh subdivision count.\n\n" +
                    "• 1 = No subdivision\n" +
                    "• 2-4 = Light smoothing (Recommended)\n" +
                    "• 8-16 = High quality (high cost)\n\n" +
                    "⚠ VR incurs cost for both eyes."),
                    MessageType.Info);
                DrawProperty("_TessPhongStrength", L("Phong スムージング強度", "Phong Smoothing Strength"));
                DrawHelpToggle("TessPhong",
                    L("🔷 Phong スムージング:\n" +
                    "頂点法線を使って三角形を曲面に膨らませます。\n\n" +
                    "• 0 = フラット（分割のみ）\n" +
                    "• 0.3-0.5 = 自然な丸み（推奨）\n" +
                    "• 1.0 = 最大スムージング",
                    "🔷 Phong Smoothing:\n" +
                    "Inflates triangles into curved surfaces using vertex normals.\n\n" +
                    "• 0 = Flat (subdivision only)\n" +
                    "• 0.3-0.5 = Natural roundness (Recommended)\n" +
                    "• 1.0 = Maximum smoothing"),
                    MessageType.Info);
                DrawProperty("_TessNormalSmooth", L("法線スムージング強度", "Normal Smoothing Strength"));
                DrawHelpToggle("TessNormalSmooth",
                    L("💡 法線スムージング:\n" +
                    "テッセレーション後の法線の滑らかさを制御します。\n" +
                    "光のあたり方・影の境界の柔らかさに影響します。\n\n" +
                    "• 0 = 元のメッシュ法線をそのまま使用\n" +
                    "• 0.5 = 適度にスムーズ（推奨）\n" +
                    "• 1.0 = 最大スムーズ（光が非常に柔らかくあたる）",
                    "💡 Normal Smoothing:\n" +
                    "Controls normal smoothness after tessellation.\n" +
                    "Affects lighting and shadow boundary softness.\n\n" +
                    "• 0 = Use original mesh normals as-is\n" +
                    "• 0.5 = Moderately smooth (Recommended)\n" +
                    "• 1.0 = Maximum smooth (very soft lighting)"),
                    MessageType.Info);
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("距離LOD", "Distance LOD"), EditorStyles.boldLabel);
                DrawProperty("_TessDistanceMin", L("最小距離（最大テッセレーション）", "Min Distance (Max Tessellation)"));
                DrawProperty("_TessDistanceMax", L("最大距離（テッセレーション無効）", "Max Distance (Tessellation Off)"));
                DrawHelpToggle("TessDistance",
                    L("📏 距離LOD:\n" +
                    "カメラからの距離でテッセレーション係数を自動調整します。\n\n" +
                    "• 最小距離以内 = 設定した係数で分割\n" +
                    "• 最大距離以遠 = テッセレーションOFF\n" +
                    "• その間 = 滑らかに遷移\n\n" +
                    "💡 VRChat ではパフォーマンスのため最大距離を10-20mに設定推奨。",
                    "📏 Distance LOD:\n" +
                    "Auto-adjusts tessellation factor based on camera distance.\n\n" +
                    "• Within min distance = Subdivide at set factor\n" +
                    "• Beyond max distance = Tessellation OFF\n" +
                    "• In between = Smooth transition\n\n" +
                    "💡 For VRChat, set max distance to 10-20m for performance."),
                    MessageType.Info);

                EditorGUILayout.Space(SECTION_SPACING);
                bool enableDisp = DrawToggle("_TESS_DISPLACEMENT", "_TessDisplacement", L("ディスプレイスメントマップを有効化", "Enable Displacement Map"));
                if (enableDisp)
                {
                    DrawProperty("_TessDispMap", L("ディスプレイスメントマップ", "Displacement Map"));
                    DrawProperty("_TessDispStrength", L("ディスプレイスメント強度", "Displacement Strength"));
                    DrawProperty("_TessDispOffset", L("ディスプレイスメントオフセット", "Displacement Offset"));
                    DrawHelpToggle("TessDisp",
                        L("🗻 ディスプレイスメントマップ:\n" +
                        "ハイトマップを使って実際にメッシュの形状を変化させます。\n" +
                        "影や光の反応が変わり、細かい凹凸表現が可能です。\n\n" +
                        "• グレースケールテクスチャを使用（白=高い, 黒=低い）\n" +
                        "• 強度: + = 外側に押し出す, - = 内側に凹む\n" +
                        "• オフセット: 基準面を調整\n\n" +
                        "⚠ テッセレーション係数が低いと効果が粗くなります。",
                        "🗻 Displacement Map:\n" +
                        "Deforms actual mesh shape using a height map.\n" +
                        "Changes shadow and light response for fine detail.\n\n" +
                        "• Uses grayscale texture (white=high, black=low)\n" +
                        "• Intensity: + = push outward, - = push inward\n" +
                        "• Offset: Adjusts reference plane\n\n" +
                        "⚠ Effect looks coarse at low tessellation factors."),
                        MessageType.Info);
                }
            }
        }
        EndBoxedSection(GetFoldout("Tessellation"));
    }

    private void DrawRefractionSection()
    {
        SetFoldout("Refraction", DrawBoxedSection(L("屈折（リフラクション）", "Refraction"), GetFoldout("Refraction"), SectionCategory.Environment, "_REFRACTION"));
        if (GetFoldout("Refraction"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ 屈折最適化で開く", "→ Open Refraction Optimizer",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.OptimizationHub, 2, targetMaterial));
            }
            bool enableRefraction = DrawToggle("_REFRACTION", "_Refraction", L("屈折を有効化", "Enable Refraction"));

            if (enableRefraction)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_RefractionIndex", L("屈折率（IOR）", "Index of Refraction (IOR)"));
                DrawProperty("_RefractionIntensity", L("屈折の強さ", "Refraction Intensity"));
                DrawProperty("_RefractionBlur", L("屈折のぼかし", "Refraction Blur"));

                EditorGUILayout.Space();
                DrawProperty("_RefractionMask", L("屈折マスク", "Refraction Mask"));
                DrawHelpToggle("RefractionMask", L("白 = 屈折あり、黒 = 屈折なし", "White = Refraction on, Black = Refraction off"), MessageType.Info);

                DrawHelpToggle("RefractionInfo", L("屈折はガラスや水などの透明素材で光の曲がりをシミュレートします。透明マテリアルに最適です。", "Refraction simulates light bending in transparent materials like glass and water. Ideal for transparent materials."), MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_RefractionBlend", "_RefractionBlendMode");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_RefractionDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Refraction"));
    }

    private void DrawRenderingSection()
    {
        SetFoldout("Rendering", DrawBoxedSection(L("レンダリング設定", "Rendering Settings"), GetFoldout("Rendering"), SectionCategory.Advanced));
        if (GetFoldout("Rendering"))
        {

            // Rendering Mode Selection
            EditorGUILayout.LabelField(L("レンダリングモード", "Rendering Mode"), EditorStyles.boldLabel);
            RenderingMode currentMode = GetCurrentRenderingMode();

            EditorGUI.BeginChangeCheck();
            RenderingMode newMode = (RenderingMode)(EditorGUILayout.Popup(L("描画タイプ", "Rendering Type"), (int)currentMode, RenderingModeLabels));
            if (EditorGUI.EndChangeCheck())
            {
                SetRenderingMode(newMode);
            }

            DrawHelpToggle("RenderingMode",
                L("🎨 レンダリングモード:\n\n" +
                "• 不透明: 標準的な不透明オブジェクト\n" +
                "  - 肌、服、硬い物体など\n" +
                "  - 最も高速で推奨\n\n" +
                "• カットアウト: アルファ閾値による透過\n" +
                "  - 髪の毛、葉っぱ、フェンスなど\n" +
                "  - アルファ値が0.5以上で表示、未満で非表示\n" +
                "  - 半透明ではなく、完全に透明か不透明かの2択\n\n" +
                "• 半透明: 滑らかな透過\n" +
                "  - ガラス、水、煙、エフェクトなど\n" +
                "  - アルファ値に応じて段階的に透過\n" +
                "  - 最も負荷が高い\n\n" +
                "・ファー: シェルベース毛皮レンダリング\n" +
                "  - 16シェルパスで毛皮を表現\n" +
                "  - GPU負荷が非常に高い（Quest非推奨）\n\n" +
                "• 背景: 背景・環境オブジェクト専用\n" +
                "  - ライトマップ統合（ベイクGI対応）\n" +
                "  - PBRマテリアル（メタリック/スムーズネス）\n" +
                "  - ワールド制作に最適\n\n" +
                "注意:\n" +
                "モードを変更すると、内部的に適切なシェーダーバリアントに\n" +
                "切り替わりますが、すべてのプロパティは保持されます。",
                "🎨 Rendering Mode:\n\n" +
                "• Opaque: Standard opaque objects\n" +
                "  - Skin, clothing, solid objects etc.\n" +
                "  - Fastest, recommended\n\n" +
                "• Cutout: Alpha threshold transparency\n" +
                "  - Hair, leaves, fences etc.\n" +
                "  - Visible at alpha >= 0.5, hidden below\n" +
                "  - Binary: fully transparent or fully opaque\n\n" +
                "• Transparent: Smooth transparency\n" +
                "  - Glass, water, smoke, effects etc.\n" +
                "  - Gradual transparency based on alpha\n" +
                "  - Highest cost\n\n" +
                "• Fur: Shell-based fur rendering\n" +
                "  - 16 shell passes for fur\n" +
                "  - Very high GPU cost (not recommended for Quest)\n\n" +
                "• Background: Background/environment objects\n" +
                "  - Lightmap integration (baked GI support)\n" +
                "  - PBR material (metallic/smoothness)\n" +
                "  - Best for world creation\n\n" +
                "Note:\n" +
                "Changing mode internally switches to the appropriate\n" +
                "shader variant, but all properties are preserved."),
                MessageType.Info);

            // Cutout の場合にAlpha Cutoff スライダーを表示
            if (currentMode == RenderingMode.Cutout)
            {
                EditorGUILayout.Space(SECTION_SPACING);
                MaterialProperty cutoffProp = FindProperty("_Cutoff", properties, false);
                if (cutoffProp != null)
                {
                    DrawProperty("_Cutoff", L("アルファカットオフ", "Alpha Cutoff"));
                    DrawHelpToggle("AlphaCutoff",
                        L("テクスチャの透明度がこの値以下のピクセルを非表示にします。\n" +
                        "• 0.5 = デフォルト（推奨）\n" +
                        "• 値を下げると: より多くのピクセルが表示される\n" +
                        "• 値を上げると: より多くのピクセルが非表示になる",
                        "Hides pixels with alpha below this value.\n" +
                        "• 0.5 = Default (Recommended)\n" +
                        "• Lower value: More pixels become visible\n" +
                        "• Higher value: More pixels become hidden"),
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(10);

            // Advanced Rendering Settings
            EditorGUILayout.LabelField(L("詳細設定", "Advanced Settings"), EditorStyles.boldLabel);
            DrawProperty("_Cull", L("カリングモード", "Culling Mode"));
            DrawProperty("_ZWrite", L("Z書き込み", "Z Write"));

            // Show blend mode properties for Transparent mode
            if (currentMode == RenderingMode.Transparent)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("ブレンドモード", "Blend Mode"), EditorStyles.boldLabel);

                // Blend preset selector
                MaterialProperty srcBlendProp = FindProperty("_SrcBlend", properties, false);
                MaterialProperty dstBlendProp = FindProperty("_DstBlend", properties, false);
                if (srcBlendProp != null && dstBlendProp != null)
                {
                    int currentPreset = GetBlendPresetIndex((int)srcBlendProp.floatValue, (int)dstBlendProp.floatValue);
                    string[] presetNames = new string[] {
                        L("Alpha Blend（標準半透明）", "Alpha Blend (Standard)"),
                        L("Additive（加算）", "Additive"),
                        L("Premultiplied Alpha（事前乗算）", "Premultiplied Alpha"),
                        L("Multiplicative（乗算）", "Multiplicative"),
                        L("カスタム", "Custom")
                    };

                    EditorGUI.BeginChangeCheck();
                    int newPreset = EditorGUILayout.Popup(L("ブレンドプリセット", "Blend Preset"), currentPreset, presetNames);
                    if (EditorGUI.EndChangeCheck() && newPreset != 4) // 4 = Custom, don't change
                    {
                        ApplyBlendPreset(newPreset, srcBlendProp, dstBlendProp);
                    }

                    DrawProperty("_SrcBlend", L("ソースブレンド", "Source Blend"));
                    DrawProperty("_DstBlend", L("宛先ブレンド", "Destination Blend"));
                }

                DrawHelpToggle("BlendMode",
                    L("🎨 ブレンドモード:\n" +
                    "半透明の合成方法を制御します。\n\n" +
                    "プリセット:\n" +
                    "• Alpha Blend: 標準的な半透明（SrcAlpha / OneMinusSrcAlpha）\n" +
                    "• Additive: 加算合成・光るエフェクト向け（One / One）\n" +
                    "• Premultiplied Alpha: 事前乗算アルファ（One / OneMinusSrcAlpha）\n" +
                    "• Multiplicative: 乗算合成・影向け（DstColor / Zero）\n" +
                    "• カスタム: SrcBlend/DstBlendを直接指定\n\n" +
                    "💡 通常は Alpha Blend を推奨します。",
                    "🎨 Blend Mode:\n" +
                    "Controls transparency compositing method.\n\n" +
                    "Presets:\n" +
                    "• Alpha Blend: Standard transparency (SrcAlpha / OneMinusSrcAlpha)\n" +
                    "• Additive: Additive blending for glow effects (One / One)\n" +
                    "• Premultiplied Alpha: Pre-multiplied alpha (One / OneMinusSrcAlpha)\n" +
                    "• Multiplicative: Multiply blending for shadows (DstColor / Zero)\n" +
                    "• Custom: Directly specify SrcBlend/DstBlend\n\n" +
                    "💡 Alpha Blend is recommended for most cases."),
                    MessageType.Info);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("Stencil", "Stencil"), EditorStyles.boldLabel);
            DrawProperty("_StencilRef", L("参照値 (Reference)", "Reference Value"));
            DrawProperty("_StencilComp", L("比較関数 (Comparison)", "Comparison Function"));
            DrawProperty("_StencilOp", L("Pass操作（テスト成功時）", "Pass Operation (On Success)"));
            DrawProperty("_StencilFail", L("Fail操作（ステンシル不合格時）", "Fail Operation (Stencil Fail)"));
            DrawProperty("_StencilZFail", L("ZFail操作（深度不合格時）", "ZFail Operation (Depth Fail)"));
            DrawProperty("_StencilReadMask", L("読み取りマスク", "Read Mask"));
            DrawProperty("_StencilWriteMask", L("書き込みマスク", "Write Mask"));
            DrawHelpToggle("Stencil",
                L("🎭 ステンシル:\n" +
                "ステンシルバッファを使用して描画マスクを制御します。\n\n" +
                "• Reference: 比較/書き込み用の参照値（0-255）\n" +
                "• Comparison: バッファ値と参照値の比較方法\n" +
                "  - Always: 常に合格 / Equal: 一致時のみ / NotEqual: 不一致時のみ\n" +
                "• Pass: ステンシル＆深度テスト両方合格時の操作\n" +
                "• Fail: ステンシルテスト不合格時の操作\n" +
                "• ZFail: ステンシル合格・深度テスト不合格時の操作\n" +
                "  - Keep: 変更なし / Replace: 参照値で上書き / Zero: 0にする\n" +
                "• Read/Write Mask: ビットマスク（255=全ビット）\n\n" +
                "💡 デフォルト値（Comp=Always, Op=Keep）では既存動作に影響しません。\n" +
                "💡 同じRef値を持つオブジェクト間でマスク効果を実現できます。\n\n" +
                "使用例:\n" +
                "• 書き込み側: Ref=1, Comp=Always, Pass=Replace\n" +
                "• 読み取り側: Ref=1, Comp=Equal, Pass=Keep",
                "🎭 Stencil:\n" +
                "Controls drawing masks using the stencil buffer.\n\n" +
                "• Reference: Reference value for comparison/writing (0-255)\n" +
                "• Comparison: How to compare buffer value with reference\n" +
                "  - Always: Always pass / Equal: Match only / NotEqual: Mismatch only\n" +
                "• Pass: Operation when both stencil & depth tests pass\n" +
                "• Fail: Operation when stencil test fails\n" +
                "• ZFail: Operation when stencil passes but depth fails\n" +
                "  - Keep: No change / Replace: Overwrite with ref / Zero: Set to 0\n" +
                "• Read/Write Mask: Bit mask (255=all bits)\n\n" +
                "💡 Default values (Comp=Always, Op=Keep) don't affect existing behavior.\n" +
                "💡 Achieve masking effects between objects with matching Ref values.\n\n" +
                "Examples:\n" +
                "• Write side: Ref=1, Comp=Always, Pass=Replace\n" +
                "• Read side: Ref=1, Comp=Equal, Pass=Keep"),
                MessageType.Info);

            // ─── Alpha Dithering ───
            EditorGUILayout.Space(SECTION_SPACING);
            EditorGUILayout.LabelField(L("アルファディザリング", "Alpha Dithering"), EditorStyles.boldLabel);

            bool enableDitheringAlpha = DrawToggle("_DITHERING_ALPHA", "_DitheringAlpha", L("ディザリングアルファを有効化", "Enable Dithering Alpha"));
            if (enableDitheringAlpha)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_DitheringAlphaScale", L("ディザスケール", "Dither Scale"));

                bool enableHashedAlpha = DrawToggle("_HASHED_ALPHA", "_HashedAlpha", L("Hashed Alphaを有効化", "Enable Hashed Alpha"));
                if (enableHashedAlpha)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_HashedAlphaScale", L("Hashed Alphaスケール", "Hashed Alpha Scale"));
                    EditorGUI.indentLevel--;
                }

                bool enableBlueNoise = DrawToggle("_BLUE_NOISE_DITHER", "_BlueNoiseDither", L("Blue Noiseディザを有効化", "Enable Blue Noise Dither"));
                if (enableBlueNoise)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_BlueNoiseTemporal", L("時間変動速度", "Temporal Speed"));
                    DrawProperty("_BlueNoiseAmount", L("Blue Noiseブレンド", "Blue Noise Blend"));
                    EditorGUI.indentLevel--;
                }

                DrawHelpToggle("DitheringAlpha",
                    L("🔲 アルファディザリング:\n" +
                    "カットアウト/半透明の境界をディザパターンで滑らかにします。\n\n" +
                    "• ディザスケール: ディザパターンのスケール（1〜100）\n\n" +
                    "サブ機能:\n" +
                    "• Hashed Alpha: ハッシュベースの半透明（TAA前提）\n" +
                    "  - ノイズっぽい半透明で、TAA適用時に滑らかに見える\n" +
                    "• Blue Noise Dither: 高品質なブルーノイズパターン\n" +
                    "  - 時間変動でフリッカーを軽減\n" +
                    "  - ブレンド量で効果の強さを調整\n\n" +
                    "💡 カットアウトモードでの髪の透過表現に最適です。",
                    "🔲 Alpha Dithering:\n" +
                    "Smooths cutout/transparent boundaries with dither patterns.\n\n" +
                    "• Dither Scale: dither pattern scale (1-100)\n\n" +
                    "Sub-features:\n" +
                    "• Hashed Alpha: Hash-based transparency (requires TAA)\n" +
                    "  - Noisy transparency that looks smooth with TAA\n" +
                    "• Blue Noise Dither: High-quality blue noise pattern\n" +
                    "  - Temporal variation reduces flicker\n" +
                    "  - Blend controls effect strength\n\n" +
                    "💡 Ideal for hair transparency in Cutout mode."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

        }
        EndBoxedSection(GetFoldout("Rendering"));
    }

    private void DrawAOSection()
    {
        SetFoldout("AO", DrawBoxedSection(L("アンビエントオクルージョン（AO）", "Ambient Occlusion (AO)"), GetFoldout("AO"), SectionCategory.Lighting, "_USE_AO"));
        if (GetFoldout("AO"))
        {
            bool enableAO = DrawToggle("_USE_AO", "_UseAO", L("AO を有効化", "Enable AO"));
            if (enableAO)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_AOMap", L("AO マップ", "AO Map"));

                bool aoHasMesh = NataneMeshAnalyzer.FindMeshForMaterial(targetMaterial) != null;
                EditorGUI.BeginDisabledGroup(!aoHasMesh);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("AO を自動生成", "Auto-Generate AO"), GUILayout.Height(22), GUILayout.Width(180)))
                {
                    bool ok = NataneToonMapGenerator.TryGenerateAO(targetMaterial, out string msg);
                    EditorUtility.DisplayDialog(ok ? L("完了", "Done") : L("失敗", "Failed"), msg, L("閉じる", "Close"));
                }
                if (GUILayout.Button(L("Curvature を生成", "Generate Curvature"), GUILayout.Height(22), GUILayout.Width(180)))
                {
                    bool ok = NataneToonMapGenerator.TryGenerateCurvature(targetMaterial, out string msg);
                    EditorUtility.DisplayDialog(ok ? L("完了", "Done") : L("失敗", "Failed"), msg, L("閉じる", "Close"));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Additional map generation buttons (Shadow, Roughness)
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("Shadow Map を生成", "Generate Shadow Map"), GUILayout.Height(22), GUILayout.Width(180)))
                {
                    MapGeneratorGUIBridge.GenerateShadowForMaterial(targetMaterial);
                }
                bool hasAlbedo = targetMaterial.HasProperty("_MainTex") && targetMaterial.GetTexture("_MainTex") != null;
                EditorGUI.BeginDisabledGroup(!hasAlbedo);
                if (GUILayout.Button(L("Roughness を生成", "Generate Roughness"), GUILayout.Height(22), GUILayout.Width(180)))
                {
                    MapGeneratorGUIBridge.GenerateRoughnessForMaterial(targetMaterial);
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // All maps at once button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("★ 全マップ一括生成", "★ Generate All Maps"), GUILayout.Height(26), GUILayout.Width(365)))
                {
                    MapGeneratorGUIBridge.GenerateAllForMaterial(targetMaterial);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUI.EndDisabledGroup();
                if (!aoHasMesh)
                {
                    EditorGUILayout.HelpBox(L("シーンにこのマテリアルを使用しているメッシュが必要です。", "A mesh using this material must be present in the scene."), MessageType.Info);
                }

                DrawProperty("_AOIntensity", L("AO強度", "AO Intensity"));
                DrawHelpToggle("AO",
                    L("🌑 アンビエントオクルージョン (AO):\n" +
                    "テクスチャの暗い部分で陰影を追加し、奥行き感を出します。\n" +
                    "• AO マップ: 白=影なし、黒=完全な影\n" +
                    "• AO強度: 0=効果なし、1=最大効果\n\n" +
                    "💡 キャラクターの脇の下や首元などの影表現に最適です。",
                    "🌑 Ambient Occlusion (AO):\n" +
                    "Adds shading in darker areas for depth.\n" +
                    "• AO Map: White=no shadow, Black=full shadow\n" +
                    "• AO Intensity: 0=no effect, 1=max effect\n\n" +
                    "💡 Ideal for shadow expression under arms or around the neck."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_AOBlend", "_AOBlendMode", "_AOBlur");
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("AO"));
    }

    private void DrawDitheringSection()
    {
        SetFoldout("Dithering", DrawBoxedSection(L("ディザリング（スクリーントーン）", "Dithering (Screen Tone)"), GetFoldout("Dithering"), SectionCategory.Lighting, "_USE_DITHERING"));
        if (GetFoldout("Dithering"))
        {
            bool enableDithering = DrawToggle("_USE_DITHERING", "_UseDithering", L("ディザリング影の境界を有効化", "Enable Dithering Shadow Boundary"));
            if (enableDithering)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_DitheringScale", L("パターンサイズ", "Pattern Size"));
                DrawProperty("_DitheringStrength", L("境界のソフトネス", "Boundary Softness"));
                DrawHelpToggle("Dithering",
                    L("🔲 ディザリング影の境界:\n" +
                    "影の境界にディザパターンを適用して、滑らかなトーン遷移を実現します。\n" +
                    "• パターンサイズ: ディザパターンの大きさ（1-100）\n" +
                    "• ソフトネス: 境界のぼかし具合（0-1）\n\n" +
                    "💡 漫画やイラスト調のスクリーントーン表現に最適です。",
                    "🔲 Dithered Shadow Boundary:\n" +
                    "Applies dither pattern to shadow boundaries for smooth tone transitions.\n" +
                    "• Pattern Size: Dither pattern size (1-100)\n" +
                    "• Softness: Boundary blur amount (0-1)\n\n" +
                    "💡 Ideal for manga/illustration-style screen tone effects."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_DitheringBlend", null, "_DitheringBlur");
                EditorGUI.indentLevel--;
            }

            // ディザ座標安定化（全ディザ共通）
            EditorGUILayout.Space(SECTION_SPACING);
            DrawProperty("_DitherStabilize", L("ディザ座標安定化", "Dither Stabilize (Object-Relative)"));
            DrawHelpToggle("DitherStabilize",
                L("📌 ディザ座標安定化:\n" +
                "オブジェクトの移動時にディザパターンがスライドする現象を抑制します。\n\n" +
                "• 0: スクリーン基準（従来動作）\n" +
                "• 1: オブジェクト相対（メッシュに固定）\n" +
                "• 中間値: ブレンド\n\n" +
                "💡 キャラクター移動時のディザチラつきが気になる場合に有効です。\n" +
                "⚠️ スクリーントーンには影響しません（スクリーン固定が正しい動作）。",
                "📌 Dither Stabilize:\n" +
                "Prevents dither patterns from sliding when the object moves.\n\n" +
                "• 0: Screen-relative (legacy behavior)\n" +
                "• 1: Object-relative (fixed to mesh)\n" +
                "• In-between: Blend\n\n" +
                "💡 Useful when dither flickering is noticeable during character movement.\n" +
                "⚠️ Does not affect Screen Tone (screen-fixed is correct behavior)."),
                MessageType.Info);
        }
        EndBoxedSection(GetFoldout("Dithering"));
    }

    private void DrawDecalSection()
    {
        SetFoldout("Decal", DrawBoxedSection(L("デカール（貼り付け）", "Decal"), GetFoldout("Decal"), SectionCategory.Effects, "_DECAL"));
        if (GetFoldout("Decal"))
        {
            bool enableDecal = DrawToggle("_DECAL", "_Decal", L("デカールを有効化", "Enable Decal"));
            if (enableDecal)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_DecalTex", L("デカールテクスチャ", "Decal Texture"));
                DrawColorProperty("_DecalColor", L("デカールカラー", "Decal Color"));
                DrawProperty("_DecalPosition", L("デカール位置 (XY)", "Decal Position (XY)"));
                DrawProperty("_DecalRotation", L("回転", "Rotation"));
                DrawProperty("_DecalScale", L("スケール", "Scale"));
                DrawProperty("_DecalBlendMode", L("合成モード", "Blend Mode"));
                DrawHelpToggle("Decal",
                    L("🏷️ デカール:\n" +
                    "メッシュ表面にテクスチャを貼り付けます。\n" +
                    "• 位置: UV空間でのデカール位置\n" +
                    "• 合成モード: Add/Multiply/Overlay/Replace\n\n" +
                    "💡 ロゴやマーク、タトゥー表現に最適です。",
                    "🏷️ Decal:\n" +
                    "Applies a texture onto the mesh surface.\n" +
                    "• Position: Decal position in UV space\n" +
                    "• Blend Mode: Add/Multiply/Overlay/Replace\n\n" +
                    "💡 Ideal for logos, marks, and tattoo effects."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_DecalBlend", null, "_DecalBlur");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_DecalDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Decal"));
    }

    private void DrawBackfaceSection()
    {
        SetFoldout("Backface", DrawBoxedSection(L("裏面テクスチャ", "Backface Texture"), GetFoldout("Backface"), SectionCategory.Advanced, "_BACKFACE_TEXTURE"));
        if (GetFoldout("Backface"))
        {
            bool enableBackface = DrawToggle("_BACKFACE_TEXTURE", "_BackfaceTexture", L("裏面テクスチャを有効化", "Enable Backface Texture"));
            if (enableBackface)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_BackfaceTex", L("裏面テクスチャ", "Backface Texture"));
                DrawColorProperty("_BackfaceColor", L("裏面カラー", "Backface Color"));
                DrawHelpToggle("BackfaceTexture",
                    L("🔄 裏面テクスチャ:\n" +
                    "ポリゴンの裏面に別のテクスチャを表示します。\n" +
                    "• カリングが Off の場合に裏面が見えます\n\n" +
                    "💡 服の裏地や紙の裏面表現に最適です。",
                    "🔄 Backface Texture:\n" +
                    "Displays a different texture on polygon backfaces.\n" +
                    "• Backfaces are visible when culling is Off\n\n" +
                    "💡 Ideal for clothing lining or paper backside."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_BackfaceBlend", "_BackfaceBlendMode");
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Backface"));
    }

    private void DrawVideoSection()
    {
        SetFoldout("Video", DrawBoxedSection(L("ビデオテクスチャ", "Video Texture"), GetFoldout("Video"), SectionCategory.Advanced, "_VIDEO_TEXTURE"));
        if (GetFoldout("Video"))
        {
            bool enableVideo = DrawToggle("_VIDEO_TEXTURE", "_VideoTexture", L("ビデオテクスチャを有効化", "Enable Video Texture"));
            if (enableVideo)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_VideoTex", L("ビデオレンダーテクスチャ", "Video Render Texture"));
                DrawProperty("_VideoEmission", L("ビデオエミッション強度", "Video Emission Intensity"));
                DrawHelpToggle("VideoTexture",
                    L("📺 ビデオテクスチャ:\n" +
                    "RenderTexture を使用してビデオ映像をマテリアルに表示します。\n" +
                    "• エミッション: ビデオの発光強度\n\n" +
                    "💡 VRChat でのスクリーン表示やモニター表現に最適です。",
                    "📺 Video Texture:\n" +
                    "Displays video content on materials using RenderTexture.\n" +
                    "• Emission: Video glow intensity\n\n" +
                    "💡 Ideal for VRChat screen displays and monitors."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_VideoBlend", "_VideoBlendMode");
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Video"));
    }

    private void DrawAudioLinkSection()
    {
        SetFoldout("AudioLink", DrawBoxedSection(L("AudioLink（音楽連動）", "AudioLink (Music Reactive)"), GetFoldout("AudioLink"), SectionCategory.Effects, "_AUDIOLINK"));
        if (GetFoldout("AudioLink"))
        {
            bool enableAudioLink = DrawToggle("_AUDIOLINK", "_AudioLink", L("AudioLink を有効化", "Enable AudioLink"));
            if (enableAudioLink)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(L("エミッション連動", "Emission Linked"), EditorStyles.boldLabel);
                DrawProperty("_AudioLinkEmissionBand", L("周波数帯域", "Frequency Band"));
                DrawProperty("_AudioLinkEmissionIntensity", L("エミッション強度", "Emission Intensity"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(L("リムライト連動", "Rim Light Linked"), EditorStyles.boldLabel);
                DrawProperty("_AudioLinkRimBand", L("周波数帯域", "Frequency Band"));
                DrawProperty("_AudioLinkRimIntensity", L("リムライト強度", "Rim Light Intensity"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(L("色相シフト連動", "Hue Shift Linked"), EditorStyles.boldLabel);
                DrawProperty("_AudioLinkHueBand", L("周波数帯域", "Frequency Band"));
                DrawProperty("_AudioLinkHueShiftIntensity", L("色相シフト強度", "Hue Shift Intensity"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(L("ディゾルブ連動", "Dissolve Linked"), EditorStyles.boldLabel);
                DrawProperty("_AudioLinkDissolveBand", L("周波数帯域", "Frequency Band"));
                DrawProperty("_AudioLinkDissolveIntensity", L("ディゾルブ強度", "Dissolve Intensity"));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(L("アウトライン連動", "Outline Linked"), EditorStyles.boldLabel);
                DrawProperty("_AudioLinkOutlineBand", L("周波数帯域", "Frequency Band"));
                DrawProperty("_AudioLinkOutlineIntensity", L("アウトライン強度", "Outline Intensity"));

                DrawHelpToggle("AudioLink",
                    L("🎵 AudioLink:\n" +
                    "AudioLink 対応ワールドで音楽に連動したエフェクトを実現します。\n" +
                    "• 周波数帯域: Bass/Low Mid/High Mid/Treble\n" +
                    "• 各エフェクトを個別に有効化できます\n" +
                    "• Chronotensity: 時間経過によるアニメーション\n\n" +
                    "💡 VRChat のクラブやライブイベント向けに最適です。",
                    "🎵 AudioLink:\n" +
                    "Enables music-reactive effects in AudioLink-compatible worlds.\n" +
                    "• Frequency Bands: Bass/Low Mid/High Mid/Treble\n" +
                    "• Each effect can be enabled individually\n" +
                    "• Chronotensity: Time-based animation\n\n" +
                    "💡 Ideal for VRChat clubs and live events."),
                    MessageType.Info);

                DrawBlendControls(materialEditor, targetMaterial, "_AudioLinkBlend", "_AudioLinkBlendMode");

                // Per-effect distance fade
                if (targetMaterial.IsKeywordEnabled("_DISTANCE_FADE"))
                {
                    DrawProperty("_AudioLinkDistFade", L("距離フェード強度", "Distance Fade Intensity"));
                }

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("AudioLink"));
    }

    private void DrawGradientBaseColorSection()
    {
        SetFoldout("GradientBaseColor", DrawBoxedSection(L("グラデーションベースカラー", "Gradient Base Color"), GetFoldout("GradientBaseColor"), SectionCategory.Basic, "_GRADIENT_BASE_COLOR"));
        if (GetFoldout("GradientBaseColor"))
        {
            bool enableGradient = DrawToggle("_GRADIENT_BASE_COLOR", "_GradientBaseColor", L("グラデーションベースカラーを有効化", "Enable Gradient Base Color"));
            if (enableGradient)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_GradientTopColor", L("上部カラー", "Top Color"));
                DrawColorProperty("_GradientBottomColor", L("下部カラー", "Bottom Color"));
                DrawProperty("_GradientAxis", L("グラデーション軸", "Gradient Axis"));
                DrawProperty("_GradientSpace", L("座標空間", "Coordinate Space"));
                DrawProperty("_GradientStart", L("開始位置", "Start Position"));
                DrawProperty("_GradientEnd", L("終了位置", "End Position"));
                DrawProperty("_GradientBlendMode", L("ブレンドモード", "Blend Mode"));
                DrawProperty("_GradientBlend", L("ブレンド強度", "Blend Intensity"));
                DrawHelpToggle("GradientBaseColor",
                    L("🎨 グラデーションベースカラー:\n" +
                    "ベースカラーに位置ベースのグラデーションを適用します。\n\n" +
                    "• 上部/下部カラー: グラデーションの両端の色\n" +
                    "• 軸: グラデーション方向（X/Y/Z）\n" +
                    "• 座標空間: ローカル（オブジェクト基準）またはワールド\n" +
                    "• ブレンドモード: Normal/Soft/Screen/Overlay\n" +
                    "• ブレンド: 0=元のテクスチャ色、1=フルグラデーション",
                    "🎨 Gradient Base Color:\n" +
                    "Applies position-based gradient to the base color.\n\n" +
                    "• Top/Bottom Color: Colors at gradient endpoints\n" +
                    "• Axis: Gradient direction (X/Y/Z)\n" +
                    "• Coordinate Space: Local (object-based) or World\n" +
                    "• Blend Mode: Normal/Soft/Screen/Overlay\n" +
                    "• Blend: 0=original texture color, 1=full gradient"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("GradientBaseColor"));
    }

    private void DrawHeightFadeSection()
    {
        SetFoldout("HeightFade", DrawBoxedSection(L("高さフェード", "Height Fade"), GetFoldout("HeightFade"), SectionCategory.Advanced, "_HEIGHT_FADE"));
        if (GetFoldout("HeightFade"))
        {
            bool enableHeightFade = DrawToggle("_HEIGHT_FADE", "_HeightFade", L("高さフェードを有効化", "Enable Height Fade"));
            if (enableHeightFade)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_HeightFadeAxis", L("フェード軸", "Fade Axis"));
                DrawProperty("_HeightFadeSpace", L("座標空間", "Coordinate Space"));
                DrawProperty("_HeightFadeStart", L("フェード開始位置", "Fade Start Position"));
                DrawProperty("_HeightFadeEnd", L("フェード終了位置", "Fade End Position"));
                DrawProperty("_HeightFadeInvert", L("方向を反転", "Invert Direction"));
                DrawProperty("_HeightFadeMode", L("フェードモード", "Fade Mode"));
                DrawHelpToggle("HeightFade",
                    L("📐 高さフェード:\n" +
                    "位置（高さ）に応じてオブジェクトをフェードします。\n\n" +
                    "• 軸: フェード方向（X/Y/Z）\n" +
                    "• 座標空間: ローカル（オブジェクト基準）またはワールド\n" +
                    "• Alpha: 透明度でフェード\n" +
                    "• Clip: ハードクリップ\n" +
                    "• Dithering: ディザパターンでクリップ\n\n" +
                    "💡 地面から消えるエフェクトや、高さに応じた表示制御に最適です。",
                    "📐 Height Fade:\n" +
                    "Fades objects based on position (height).\n\n" +
                    "• Axis: Fade direction (X/Y/Z)\n" +
                    "• Coordinate Space: Local (object-based) or World\n" +
                    "• Alpha: Fade with transparency\n" +
                    "• Clip: Hard clip\n" +
                    "• Dithering: Dither pattern clip\n\n" +
                    "💡 Ideal for ground-disappearing effects or height-based visibility."),
                    MessageType.Info);

                // Show blend only in Alpha mode
                float heightFadeMode = FindProperty("_HeightFadeMode", properties).floatValue;
                if (heightFadeMode < 0.5f)
                {
                    DrawProperty("_HeightFadeBlend", L("ブレンド強度", "Blend Intensity"));
                }

                // Show dither scale only in Dithering mode
                if (heightFadeMode > 1.5f)
                {
                    DrawProperty("_HeightFadeDitherScale", L("ディザスケール", "Dither Scale"));
                }

                DrawProperty("_HeightFadeEdgeWidth", L("エッジグロー幅", "Edge Glow Width"));
                float edgeWidth = FindProperty("_HeightFadeEdgeWidth", properties).floatValue;
                if (edgeWidth > MIN_PARAMETER_VALUE)
                {
                    DrawColorProperty("_HeightFadeEdgeColor", L("エッジグローカラー", "Edge Glow Color"), true);
                }
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("HeightFade"));
    }

    private void DrawIntersectionFadeSection()
    {
        SetFoldout("IntersectionFade", DrawBoxedSection(L("オブジェクト交差フェード", "Intersection Fade"), GetFoldout("IntersectionFade"), SectionCategory.Advanced, "_INTERSECTION_FADE"));
        if (GetFoldout("IntersectionFade"))
        {
            bool enableIntersectionFade = DrawToggle("_INTERSECTION_FADE", "_IntersectionFade", L("交差フェードを有効化", "Enable Intersection Fade"));
            if (enableIntersectionFade)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_IntersectionFadeDistance", L("フェード距離", "Fade Distance"));
                DrawProperty("_IntersectionFadeMode", L("フェードモード", "Fade Mode"));
                DrawHelpToggle("IntersectionFade",
                    L("🔀 オブジェクト交差フェード:\n" +
                    "他のオブジェクトとの交差部分を透明にします。\n\n" +
                    "• フェード距離: 交差部分のフェード範囲\n" +
                    "• Alpha: 透明度でフェード\n" +
                    "• Clip: ハードクリップ\n" +
                    "• Dithering: ディザパターンでクリップ\n\n" +
                    "💡 地面との交差部分や、オブジェクト間の滑らかな接続に最適です。\n" +
                    "⚠️ 深度テクスチャが必要です（カメラのDepthTextureMode）。",
                    "🔀 Object Intersection Fade:\n" +
                    "Makes intersection areas with other objects transparent.\n\n" +
                    "• Fade Distance: Intersection fade range\n" +
                    "• Alpha: Fade with transparency\n" +
                    "• Clip: Hard clip\n" +
                    "• Dithering: Dither pattern clip\n\n" +
                    "💡 Ideal for ground intersection or smooth object connections.\n" +
                    "⚠️ Requires depth texture (camera DepthTextureMode)."),
                    MessageType.Info);

                // Show blend only in Alpha mode
                float intersectionFadeMode = FindProperty("_IntersectionFadeMode", properties).floatValue;
                if (intersectionFadeMode < 0.5f)
                {
                    DrawProperty("_IntersectionFadeBlend", L("ブレンド強度", "Blend Intensity"));
                }

                // Show dither scale only in Dithering mode
                if (intersectionFadeMode > 1.5f)
                {
                    DrawProperty("_IntersectionFadeDitherScale", L("ディザスケール", "Dither Scale"));
                }

                DrawProperty("_IntersectionFadeEdgeWidth", L("エッジ幅", "Edge Width"));
                float intersectionEdgeWidth = FindProperty("_IntersectionFadeEdgeWidth", properties).floatValue;
                if (intersectionEdgeWidth > MIN_PARAMETER_VALUE)
                {
                    DrawColorProperty("_IntersectionFadeEdgeColor", L("エッジカラー", "Edge Color"), true);
                }
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("IntersectionFade"));
    }

    private void DrawDistanceFadeSection()
    {
        SetFoldout("DistanceFade", DrawBoxedSection(L("距離フェード", "Distance Fade"), GetFoldout("DistanceFade"), SectionCategory.Advanced, "_DISTANCE_FADE"));
        if (GetFoldout("DistanceFade"))
        {
            bool enableDistanceFade = DrawToggle("_DISTANCE_FADE", "_DistanceFade", L("距離フェードを有効化", "Enable Distance Fade"));
            if (enableDistanceFade)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_DistanceFadeStart", L("フェード開始距離", "Fade Start Distance"));
                DrawProperty("_DistanceFadeEnd", L("フェード終了距離", "Fade End Distance"));
                DrawProperty("_DistanceFadeMode", L("フェードモード", "Fade Mode"));
                DrawHelpToggle("DistanceFade",
                    L("📏 距離フェード:\n" +
                    "カメラからの距離に応じてオブジェクトをフェードします。\n" +
                    "• 開始距離: フェードが始まる距離\n" +
                    "• 終了距離: 完全に消える距離\n" +
                    "• Alpha: 透明度でフェード\n" +
                    "• Simplify: エフェクトを簡略化（ハードクリップ）\n" +
                    "• Dithering: ディザパターンで段階的にクリップ\n\n" +
                    "💡 パフォーマンス最適化に有効です。遠距離のオブジェクト描画負荷を軽減します。\n" +
                    "💡 Ditheringモードは不透明マテリアルでも自然なフェードが可能です。",
                    "📏 Distance Fade:\n" +
                    "Fades objects based on camera distance.\n" +
                    "• Start Distance: Distance where fade begins\n" +
                    "• End Distance: Distance where fully invisible\n" +
                    "• Alpha: Fade with transparency\n" +
                    "• Simplify: Simplified effect (hard clip)\n" +
                    "• Dithering: Gradual dither pattern clip\n\n" +
                    "💡 Effective for performance optimization. Reduces draw cost for distant objects.\n" +
                    "💡 Dithering mode enables natural fade even on opaque materials."),
                    MessageType.Info);

                // Show dither scale only in Dithering mode
                if (targetMaterial.GetFloat("_DistanceFadeMode") > 1.5)
                {
                    DrawProperty("_DistFadeDitherScale", L("ディザスケール", "Dither Scale"));
                }

                DrawBlendControls(materialEditor, targetMaterial, "_DistanceFadeBlend", null, "_DistFadeBlur");

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("近距離フェード", "Near Fade"), EditorStyles.boldLabel);
                DrawProperty("_NearFadeStart", L("開始距離（完全透明）", "Start Distance (Fully Transparent)"));
                DrawProperty("_NearFadeEnd", L("終了距離（完全表示）", "End Distance (Fully Visible)"));
                DrawHelpToggle("NearFade",
                    L("📏 近距離フェード:\n" +
                    "カメラが近すぎるとオブジェクトが透明になります。\n" +
                    "VRChatのアバター近接フェードに最適。\n\n" +
                    "• 開始距離: この距離以下で完全に透明\n" +
                    "• 終了距離: この距離以上で完全に表示\n" +
                    "• 推奨値: 開始0.1〜0.2, 終了0.3〜0.5",
                    "📏 Near Fade:\n" +
                    "Objects become transparent when camera is too close.\n" +
                    "Ideal for VRChat avatar proximity fade.\n\n" +
                    "• Start Distance: Fully transparent below this distance\n" +
                    "• End Distance: Fully visible above this distance\n" +
                    "• Recommended: Start 0.1-0.2, End 0.3-0.5"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("DistanceFade"));
    }

    // ===== Phase 2/3: New Background Shader Features =====

    private void DrawDetailMapSection()
    {
        SetFoldout("DetailMap", DrawBoxedSection(L("ディテールマップ", "Detail Map"), GetFoldout("DetailMap"), SectionCategory.Advanced, "_DETAIL_MAP"));
        if (GetFoldout("DetailMap"))
        {
            bool enableDetailMap = DrawToggle("_DETAIL_MAP", "_DetailMap", L("ディテールマップを有効化", "Enable Detail Map"));
            if (enableDetailMap)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("ディテールマップ設定", "Detail Map Settings"), EditorStyles.boldLabel);

                DrawProperty("_DetailAlbedoMap", L("ディテール アルベド", "Detail Albedo Map"));
                DrawProperty("_DetailAlbedoScale", L("アルベド影響度", "Albedo Scale"));
                DrawProperty("_DetailNormalMap", L("ディテール法線マップ", "Detail Normal Map"));
                DrawProperty("_DetailNormalScale", L("法線マップ強度", "Normal Scale"));
                DrawProperty("_DetailTiling", L("タイリング", "Tiling"));
                DrawProperty("_DetailUVSet", L("UVセット", "UV Set"));

                DrawHelpToggle("DetailMap",
                    L("📍 ディテールマップ:\n" +
                    "近距離でのテクスチャ解像感を向上させます。\n" +
                    "セカンダリUV（UV1）を使用可能。\n\n" +
                    "• アルベド: メインテクスチャに乗算ブレンド\n" +
                    "• 法線マップ: 既存法線に加算ブレンド\n" +
                    "• タイリング: テクスチャの繰り返し回数\n\n" +
                    "💡 背景の壁面や床の細かい質感表現に最適。",
                    "📍 Detail Map:\n" +
                    "Improves texture resolution at close range.\n" +
                    "Can use secondary UV (UV1).\n\n" +
                    "• Albedo: Multiply blend with main texture\n" +
                    "• Normal: Additive blend with existing normals\n" +
                    "• Tiling: Texture repeat count\n\n" +
                    "💡 Perfect for detailed wall and floor textures."),
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("DetailMap"));
    }

    private void DrawTriplanarSection()
    {
        SetFoldout("Triplanar", DrawBoxedSection(L("トライプレーナー", "Triplanar Mapping"), GetFoldout("Triplanar"), SectionCategory.Advanced, "_TRIPLANAR"));
        if (GetFoldout("Triplanar"))
        {
            bool enableTriplanar = DrawToggle("_TRIPLANAR", "_Triplanar", L("トライプレーナーを有効化", "Enable Triplanar Mapping"));
            if (enableTriplanar)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("トライプレーナー設定", "Triplanar Settings"), EditorStyles.boldLabel);

                DrawProperty("_TriplanarScale", L("テクスチャスケール", "Texture Scale"));
                DrawProperty("_TriplanarBlendSharpness", L("ブレンドシャープネス", "Blend Sharpness"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("軸オフセット", "Axis Offset"), EditorStyles.boldLabel);
                DrawProperty("_TriplanarOffsetX", L("Xオフセット", "X Offset"));
                DrawProperty("_TriplanarOffsetY", L("Yオフセット", "Y Offset"));
                DrawProperty("_TriplanarOffsetZ", L("Zオフセット", "Z Offset"));

                DrawHelpToggle("Triplanar",
                    L("📍 トライプレーナーマッピング:\n" +
                    "UV展開なしで3軸からテクスチャを投影します。\n" +
                    "岩、洞窟、地形などUVが不要な場面に最適。\n\n" +
                    "• スケール: テクスチャの大きさ\n" +
                    "• シャープネス: 軸間ブレンドの鋭さ（1=滑らか、8=鋭い）\n\n" +
                    "⚠️ メインテクスチャのUVサンプリングを置き換えます。",
                    "📍 Triplanar Mapping:\n" +
                    "Projects texture from 3 axes without UV unwrapping.\n" +
                    "Perfect for rocks, caves, terrain with no UV needed.\n\n" +
                    "• Scale: Texture size\n" +
                    "• Sharpness: Axis blend sharpness (1=smooth, 8=sharp)\n\n" +
                    "⚠️ Replaces main texture UV sampling."),
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("Triplanar"));
    }

    private void DrawHeightFogSection()
    {
        SetFoldout("HeightFog", DrawBoxedSection(L("ハイトフォグ", "Height Fog"), GetFoldout("HeightFog"), SectionCategory.Environment, "_HEIGHT_FOG"));
        if (GetFoldout("HeightFog"))
        {
            bool enableHeightFog = DrawToggle("_HEIGHT_FOG", "_HeightFog", L("ハイトフォグを有効化", "Enable Height Fog"));
            if (enableHeightFog)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("ハイトフォグ設定", "Height Fog Settings"), EditorStyles.boldLabel);

                DrawColorProperty("_HeightFogColor", L("フォグカラー", "Fog Color"));
                DrawProperty("_HeightFogStart", L("開始高さ (Y)", "Start Height (Y)"));
                DrawProperty("_HeightFogEnd", L("終了高さ (Y)", "End Height (Y)"));
                DrawProperty("_HeightFogDensity", L("フォグ密度", "Fog Density"));
                DrawProperty("_HeightFogMode", L("フォグモード", "Fog Mode"));

                DrawHelpToggle("HeightFog",
                    L("🌫️ ハイトフォグ:\n" +
                    "ワールドY座標に基づくマテリアルレベルのフォグ効果。\n" +
                    "VRChatではポストプロセスが使えないため、マテリアルで実装。\n\n" +
                    "• 開始高さ: フォグが始まるY座標\n" +
                    "• 終了高さ: フォグが完全にかかるY座標\n" +
                    "• Linear: 直線的な減衰\n" +
                    "• Exponential: 指数関数的な減衰（自然な霧）\n\n" +
                    "💡 幻想的な雰囲気や朝霧の表現に最適。",
                    "🌫️ Height Fog:\n" +
                    "Material-level fog based on world Y coordinate.\n" +
                    "Implemented in material since VRChat has no post-processing.\n\n" +
                    "• Start Height: Y coordinate where fog begins\n" +
                    "• End Height: Y coordinate where fog is fully applied\n" +
                    "• Linear: Linear falloff\n" +
                    "• Exponential: Exponential falloff (natural fog)\n\n" +
                    "💡 Perfect for fantasy atmosphere and morning mist."),
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("HeightFog"));
    }

    private void DrawSurfaceCoverSection()
    {
        SetFoldout("SurfaceCover", DrawBoxedSection(L("サーフェスカバー（雪/砂）", "Surface Cover (Snow/Sand)"), GetFoldout("SurfaceCover"), SectionCategory.Effects, "_SURFACE_COVER"));
        if (GetFoldout("SurfaceCover"))
        {
            bool enableCover = DrawToggle("_SURFACE_COVER", "_SurfaceCover", L("サーフェスカバーを有効化", "Enable Surface Cover"));
            if (enableCover)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("サーフェスカバー設定", "Surface Cover Settings"), EditorStyles.boldLabel);

                DrawProperty("_CoverTex", L("カバーテクスチャ", "Cover Texture"));
                DrawColorProperty("_CoverColor", L("カバーカラー", "Cover Color"));
                DrawProperty("_CoverNormalMap", L("カバー法線マップ", "Cover Normal Map"));
                DrawProperty("_CoverAmount", L("カバー量", "Cover Amount"));
                DrawProperty("_CoverThreshold", L("法線閾値", "Normal Threshold"));
                DrawProperty("_CoverBlendSharpness", L("ブレンドシャープネス", "Blend Sharpness"));
                DrawProperty("_CoverTiling", L("タイリング", "Tiling"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("カバー方向", "Cover Direction"), EditorStyles.boldLabel);
                DrawProperty("_CoverDirection", L("方向ベクトル", "Direction Vector"));

                DrawHelpToggle("SurfaceCover",
                    L("❄️ サーフェスカバー:\n" +
                    "ワールド空間の上方向にテクスチャを重ねて雪や砂を表現。\n\n" +
                    "• カバー量: 全体の堆積量 (0=なし、1=最大)\n" +
                    "• 法線閾値: 上向きの面のみにカバー (0=全面、1=真上のみ)\n" +
                    "• シャープネス: ブレンド境界の鋭さ\n" +
                    "• 方向: デフォルト(0,1,0)=上から。変更で斜め方向も可能。\n\n" +
                    "💡 季節表現（雪景色）やファンタジー環境に有効。",
                    "❄️ Surface Cover:\n" +
                    "Overlays texture on upward-facing surfaces for snow/sand.\n\n" +
                    "• Cover Amount: Overall accumulation (0=none, 1=max)\n" +
                    "• Threshold: Only cover upward faces (0=all, 1=top only)\n" +
                    "• Sharpness: Blend edge sharpness\n" +
                    "• Direction: Default (0,1,0)=from above. Change for angled cover.\n\n" +
                    "💡 Great for seasonal (snow) and fantasy environments."),
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("SurfaceCover"));
    }

    private void DrawMirrorControlSection()
    {
        SetFoldout("MirrorControl", DrawBoxedSection(L("ミラー・カメラ制御", "Mirror / Camera Control"), GetFoldout("MirrorControl"), SectionCategory.Advanced, "_MIRROR_CONTROL"));
        if (GetFoldout("MirrorControl"))
        {
            bool enableMirror = DrawToggle("_MIRROR_CONTROL", "_MirrorControl", L("ミラー・カメラ制御を有効化", "Enable Mirror / Camera Control"));
            if (enableMirror)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("ミラー制御", "Mirror Control"), EditorStyles.boldLabel);

                DrawProperty("_MirrorMode", L("ミラー表示モード", "Mirror Display Mode"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("カメラ制御", "Camera Control"), EditorStyles.boldLabel);

                DrawProperty("_CameraMode", L("カメラ表示モード", "Camera Display Mode"));

                EditorGUILayout.Space(SECTION_SPACING);
                EditorGUILayout.LabelField(L("エミッション設定", "Emission Settings"), EditorStyles.boldLabel);

                DrawProperty("_MirrorEmissionMultiplier", L("ミラー内エミッション倍率", "Mirror Emission Multiplier"));

                DrawHelpToggle("MirrorControl",
                    L("🪞 ミラー・カメラ制御:\n" +
                    "VRChatミラー・カメラでの描画を制御します。\n\n" +
                    "【ミラー表示モード】\n" +
                    "• 両方表示: 通常・ミラー両方で表示\n" +
                    "• ミラーのみ: ミラー内でのみ表示\n" +
                    "• ミラー以外のみ: 通常時のみ表示\n\n" +
                    "【カメラ表示モード】\n" +
                    "• 両方表示: 通常・カメラ両方で表示\n" +
                    "• カメラのみ: VRChatカメラ撮影時のみ表示\n" +
                    "• カメラ以外のみ: VRChatカメラ撮影時に非表示\n\n" +
                    "💡 ミラー限定の隠し装飾や、カメラ撮影時だけ見える特殊エフェクトに活用。",
                    "🪞 Mirror / Camera Control:\n" +
                    "Controls rendering in VRChat mirrors and cameras.\n\n" +
                    "[Mirror Display Mode]\n" +
                    "• Both: Show in normal and mirror view\n" +
                    "• Mirror Only: Show only in mirror\n" +
                    "• Non-Mirror Only: Show only in normal view\n\n" +
                    "[Camera Display Mode]\n" +
                    "• Both: Show in normal and camera view\n" +
                    "• Camera Only: Show only when captured by VRChat camera\n" +
                    "• Non-Camera Only: Hide when captured by VRChat camera\n\n" +
                    "💡 Use for hidden decorations in mirrors or special effects visible only in photos."),
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("MirrorControl"));
    }

    private void DrawQuestLiteSection()
    {
        SetFoldout("QuestLite", DrawBoxedSection(L("Quest軽量パス", "Quest Lite"), GetFoldout("QuestLite"), SectionCategory.Advanced, "_QUEST_LITE"));
        if (GetFoldout("QuestLite"))
        {
            bool enableQuestLite = DrawToggle("_QUEST_LITE", "_QuestLite", L("Quest軽量モードを有効化", "Enable Quest Lite Mode"));
            if (enableQuestLite)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    L("⚡ Quest軽量モードが有効です。以下のエフェクトが自動的にスキップされます:\n" +
                    "• MatCap 2nd / 3rd\n" +
                    "• グリッター / 水滴\n" +
                    "• ホログラム / グリッチ\n" +
                    "• インターセクションフェード\n\n" +
                    "これにより、モバイルGPUでのパフォーマンスが30-50%改善します。",
                    "⚡ Quest Lite mode is active. The following effects are automatically skipped:\n" +
                    "• MatCap 2nd / 3rd\n" +
                    "• Glitter / Water Drip\n" +
                    "• Hologram / Glitch\n" +
                    "• Intersection Fade\n\n" +
                    "This improves mobile GPU performance by 30-50%."),
                    MessageType.Warning);

                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("QuestLite"));
    }

    /// <summary>
    /// Screen Edge 分離バリアントを使用中か判定
    /// </summary>
    private static bool IsScreenEdgeSplitShader(Material material)
    {
        return material != null &&
               material.shader != null &&
               material.shader.name == ScreenEdgeSplitShaderName;
    }

    /// <summary>
    /// Screen Edge 分離バリアントへ安全に切り替えられるか判定
    /// </summary>
    private bool CanUseScreenEdgeSplitVariant(Material material, out string reason)
    {
        reason = null;

        if (material == null || material.shader == null)
        {
            reason = L("マテリアルまたはシェーダーが見つかりません。", "Material or shader is missing.");
            return false;
        }

        string shaderName = material.shader.name;
        bool isAlreadySplit = shaderName == ScreenEdgeSplitShaderName;
        if (!isAlreadySplit && shaderName != DefaultOpaqueShaderName)
        {
            reason = L(
                "現在の分離バリアントは通常 Opaque シェーダー専用です。Cutout / Transparent / Fur / Lite / Background では使えません。",
                "The current split variant only supports the standard opaque shader. It cannot be used with Cutout, Transparent, Fur, Lite, or Background.");
            return false;
        }

        for (int i = 0; i < ScreenEdgeSplitUnsupportedKeywords.Length; i++)
        {
            string keyword = ScreenEdgeSplitUnsupportedKeywords[i];
            if (!material.IsKeywordEnabled(keyword))
            {
                continue;
            }

            reason = L(
                $"{keyword} が有効なため、安全に分離できません。アルファやクリップに影響する機能を無効化してから切り替えてください。",
                $"{keyword} is enabled, so the split variant would not be safe. Disable alpha or clip related features before switching.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Screen Edge 分離バリアントへ切り替え / 復帰
    /// </summary>
    private bool SetScreenEdgeSplitVariant(bool enable)
    {
        if (targetMaterial == null)
        {
            NataneToon.Editor.NataneToonErrorDialog.ShowNullMaterialError(L("Screen Edge 分離バリアントの切り替え", "Toggle Screen Edge Split Variant"));
            return false;
        }

        if (enable && !CanUseScreenEdgeSplitVariant(targetMaterial, out string reason))
        {
            EditorUtility.DisplayDialog(
                L("切り替え不可", "Cannot Switch"),
                reason,
                "OK");
            return false;
        }

        string newShaderName = enable ? ScreenEdgeSplitShaderName : DefaultOpaqueShaderName;
        Shader newShader = Shader.Find(newShaderName);
        if (newShader == null)
        {
            Debug.LogError($"[NataneToonShaderGUI] Shader not found: {newShaderName}");
            NataneToon.Editor.NataneToonErrorDialog.ShowShaderNotFoundError(newShaderName);
            return false;
        }

        if (targetMaterial.shader == newShader)
        {
            return true;
        }

        Undo.RecordObject(targetMaterial, enable ? "Enable Screen Edge Split Variant" : "Disable Screen Edge Split Variant");
        targetMaterial.shader = newShader;
        targetMaterial.SetFloat("_ZWrite", 1f);
        targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        EditorUtility.SetDirty(targetMaterial);
        InvalidateInspectorCaches();
        if (materialEditor != null)
        {
            materialEditor.Repaint();
        }

        return true;
    }

    /// <summary>
    /// Get current rendering mode based on shader name
    /// </summary>
    private RenderingMode GetCurrentRenderingMode()
    {
        if (targetMaterial == null || targetMaterial.shader == null)
            return RenderingMode.Opaque;

        string shaderName = targetMaterial.shader.name;

        if (shaderName.Contains("Background"))
            return RenderingMode.Background;
        else if (shaderName.Contains("Fur"))
            return RenderingMode.Fur;
        else if (shaderName.Contains("Transparent"))
            return RenderingMode.Transparent;
        else if (shaderName.Contains("Cutout"))
            return RenderingMode.Cutout;
        else
            return RenderingMode.Opaque;
    }

    /// <summary>
    /// Set rendering mode by switching to appropriate shader variant
    /// </summary>
    private bool SetRenderingMode(RenderingMode mode)
    {
        if (targetMaterial == null)
        {
            NataneToon.Editor.NataneToonErrorDialog.ShowNullMaterialError(L("描画タイプの変更", "Change Rendering Mode"));
            return false;
        }

        // Get base shader name
        string baseShaderName = DefaultOpaqueShaderName;
        string newShaderName = baseShaderName;

        // Determine shader variant based on mode
        switch (mode)
        {
            case RenderingMode.Opaque:
                newShaderName = baseShaderName;
                break;
            case RenderingMode.Cutout:
                newShaderName = baseShaderName + " (Cutout)";
                break;
            case RenderingMode.Transparent:
                newShaderName = baseShaderName + " (Transparent)";
                break;
            case RenderingMode.Fur:
                newShaderName = baseShaderName + " (Fur)";
                break;
            case RenderingMode.Background:
                newShaderName = baseShaderName + " (Background)";
                break;
        }

        // Find the shader
        Shader newShader = Shader.Find(newShaderName);
        if (newShader == null)
        {
            Debug.LogError($"[NataneToonShaderGUI] Shader not found: {newShaderName}");
            NataneToon.Editor.NataneToonErrorDialog.ShowShaderNotFoundError(newShaderName);
            return false;
        }

        // Check if already using this shader
        if (targetMaterial.shader == newShader)
            return true;

        // Record undo
        Undo.RecordObject(targetMaterial, "Change Rendering Mode");

        // Switch shader (properties with same names are preserved)
        targetMaterial.shader = newShader;

        // Disable fur keyword when switching away from Fur mode
        if (mode != RenderingMode.Fur)
        {
            targetMaterial.SetFloat("_Fur", 0);
            targetMaterial.DisableKeyword("_FUR");
        }

        // Set default properties based on mode
        switch (mode)
        {
            case RenderingMode.Opaque:
                targetMaterial.SetFloat("_ZWrite", 1);
                targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                break;

            case RenderingMode.Cutout:
                targetMaterial.SetFloat("_ZWrite", 1);
                targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                break;

            case RenderingMode.Transparent:
                targetMaterial.SetFloat("_ZWrite", 0);
                targetMaterial.SetFloat("_SrcBlend", 5); // SrcAlpha
                targetMaterial.SetFloat("_DstBlend", 10); // OneMinusSrcAlpha
                targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;

            case RenderingMode.Fur:
                targetMaterial.SetFloat("_ZWrite", 0);
                targetMaterial.SetFloat("_Fur", 1);
                targetMaterial.EnableKeyword("_FUR");
                targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;

            case RenderingMode.Background:
                targetMaterial.SetFloat("_ZWrite", 1);
                targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                break;
        }

        // Mark as dirty
        EditorUtility.SetDirty(targetMaterial);

        // Repaint inspector
        if (materialEditor != null)
        {
            materialEditor.Repaint();
        }

        return true;
    }

    /// <summary>
    /// Toon以外のシェーダー用GUIを描画 (委譲パターン)
    /// </summary>
    private void DrawNonToonShaderGUI(NataneToon.Editor.ShaderType type)
    {
        switch (type)
        {
            case NataneToon.Editor.ShaderType.Eye:
                if (eyeDrawer == null) eyeDrawer = new NataneToon.Editor.NataneToonEyeDrawer();
                eyeDrawer.Initialize(materialEditor, properties, targetMaterial);
                eyeDrawer.Draw();
                break;

            case NataneToon.Editor.ShaderType.Wirelight:
                if (wirelightDrawer == null) wirelightDrawer = new NataneToon.Editor.NataneToonWirelightDrawer();
                wirelightDrawer.Initialize(materialEditor, properties, targetMaterial);
                wirelightDrawer.Draw();
                break;
        }
    }

    private void DrawProperty(string propertyName, string label)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);
        if (property != null)
        {
            EditorGUI.BeginChangeCheck();
            materialEditor.ShaderProperty(property, label);
            // Feature 4: Cross-variant sync — propagate changes to non-primary targets
            if (EditorGUI.EndChangeCheck())
            {
                PropagatePropertyToOtherTargets(property);
            }
        }
    }

    // Blend mode preset helpers
    private int GetBlendPresetIndex(int srcBlend, int dstBlend)
    {
        if (srcBlend == 5 && dstBlend == 10) return 0; // Alpha Blend (SrcAlpha / OneMinusSrcAlpha)
        if (srcBlend == 1 && dstBlend == 1) return 1;  // Additive (One / One)
        if (srcBlend == 1 && dstBlend == 10) return 2;  // Premultiplied Alpha (One / OneMinusSrcAlpha)
        if (srcBlend == 2 && dstBlend == 0) return 3;   // Multiplicative (DstColor / Zero)
        return 4; // Custom
    }

    private void ApplyBlendPreset(int preset, MaterialProperty srcBlend, MaterialProperty dstBlend)
    {
        switch (preset)
        {
            case 0: srcBlend.floatValue = 5; dstBlend.floatValue = 10; break; // Alpha Blend
            case 1: srcBlend.floatValue = 1; dstBlend.floatValue = 1; break;  // Additive
            case 2: srcBlend.floatValue = 1; dstBlend.floatValue = 10; break; // Premultiplied Alpha
            case 3: srcBlend.floatValue = 2; dstBlend.floatValue = 0; break;  // Multiplicative
        }
    }

    private void DrawColorProperty(string propertyName, string label, bool defaultHDR = false)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);
        if (property == null) return;

        string prefsKey = "NataneToon_HDR_" + propertyName;
        bool isHDR = EditorPrefs.GetBool(prefsKey, defaultHDR);

        EditorGUILayout.BeginHorizontal();

        // Draw color field with HDR support
        EditorGUI.BeginChangeCheck();
        Rect colorRect = EditorGUILayout.GetControlRect(true);
        Color colorValue = EditorGUI.ColorField(
            colorRect,
            new GUIContent(label),
            property.colorValue,
            true,  // showEyedropper
            true,  // showAlpha
            isHDR  // hdr
        );
        if (EditorGUI.EndChangeCheck())
        {
            property.colorValue = colorValue;
            // Feature 4: Cross-variant sync
            PropagateColorToOtherTargets(propertyName, colorValue);
        }

        // HDR toggle button
        var oldBgColor = GUI.backgroundColor;
        if (isHDR)
        {
            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.2f); // gold
        }
        if (GUILayout.Button("HDR", CachedHDRToggleStyle))
        {
            isHDR = !isHDR;
            EditorPrefs.SetBool(prefsKey, isHDR);
        }
        GUI.backgroundColor = oldBgColor;

        EditorGUILayout.EndHorizontal();
    }

    private bool DrawToggle(string keyword, string propertyName, string label)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);
        if (property == null)
        {
            return false;
        }

        bool enabled = property.floatValue > FLOAT_COMPARISON_THRESHOLD;
        var toggleEvaluation = GetCachedToggleEvaluation(keyword);
        bool canEnable = enabled || toggleEvaluation.CanEnable;
        bool changed = false;
        bool newEnabled = enabled;

        EditorGUILayout.BeginHorizontal();

        // 1. チェックボックス（ラベルなし）
        using (new EditorGUI.DisabledScope(!canEnable))
        {
            EditorGUI.BeginChangeCheck();
            newEnabled = EditorGUILayout.Toggle(GUIContent.none, enabled, GUILayout.Width(16));
            changed = EditorGUI.EndChangeCheck();
        }

        // 2. ステータスアイコン（チェックボックスの直後）
        string statusIcon = newEnabled ? "✓" : (canEnable ? "✗" : "!");
        Color statusColor = newEnabled
            ? NataneToonShaderGUIStyles.ToggleEnabledColor
            : (canEnable ? NataneToonShaderGUIStyles.ToggleDisabledColor : NataneToonShaderGUIStyles.ToggleBlockedColor);

        var oldColor = GUI.color;
        GUI.color = statusColor;
        GUILayout.Label(statusIcon, GUILayout.Width(18));
        GUI.color = oldColor;

        // 3. ラベル
        EditorGUILayout.LabelField(label);

        EditorGUILayout.EndHorizontal();

        if (!enabled && !canEnable)
        {
            DrawSamplerBlockHint(toggleEvaluation);
        }

        if (changed)
        {
            // Feature 4: Apply toggle to ALL target materials (cross-variant support)
            foreach (Material mat in GetAllTargetMaterials())
            {
                if (mat == null || !mat.HasProperty(propertyName)) continue;
                Undo.RecordObject(mat, L("シェーダー機能を切り替え", "Toggle Shader Feature"));

                // Stage 2: Role-Aware toggle — if a SetupRecord exists and the feature is being
                // turned ON, apply role-optimized parameters instead of generic defaults.
                if (newEnabled)
                {
                    bool roleApplied = NataneToon.Editor.NataneAutoSetupHub.ApplyRoleAwareFeatureEnable(mat, keyword);
                    if (!roleApplied)
                    {
                        // Not-recommended dialog was cancelled — revert toggle
                        var record = NataneToon.Editor.AutoSetupRecord.Load(mat);
                        if (record != null && NataneToon.Editor.NataneFeaturePresetTable.Get(record.role, record.look, keyword).notRecommended)
                        {
                            newEnabled = false;
                        }
                    }
                }

                mat.SetFloat(propertyName, newEnabled ? 1.0f : 0.0f);
                if (newEnabled)
                    mat.EnableKeyword(keyword);
                else
                    mat.DisableKeyword(keyword);
                EditorUtility.SetDirty(mat);
            }
            // Keep property in sync for the editor
            property.floatValue = newEnabled ? 1.0f : 0.0f;

            // Feature 2: Auto-expand parent section when toggled ON
            if (newEnabled && KeywordToSectionKey.TryGetValue(keyword, out string sectionKey))
            {
                SetFoldout(sectionKey, true);
            }
        }

        return newEnabled;
    }

    /// <summary>
    /// Count how many of the given shader keywords are currently enabled on the target material.
    /// </summary>
    private int CountEnabledKeywords(params string[] keywords)
    {
        int count = 0;
        foreach (var kw in keywords)
        {
            if (targetMaterial.IsKeywordEnabled(kw)) count++;
        }
        return count;
    }

    private void DrawSamplerBlockHint(NataneToonSamplerBudgetEstimator.ToggleEvaluation evaluation)
    {
        Color oldColor = GUI.color;
        GUI.color = NataneToonShaderGUIStyles.SamplerBlockHintColor;
        EditorGUILayout.LabelField(
            L(
                $"Sampler 制限のため有効化できません (+{evaluation.AddedSamplers}, 推定 {evaluation.AfterEnable.EstimatedSamplers}/{evaluation.AfterEnable.Limit})",
                $"Cannot enable because of the sampler limit (+{evaluation.AddedSamplers}, estimated {evaluation.AfterEnable.EstimatedSamplers}/{evaluation.AfterEnable.Limit})"),
            EditorStyles.wordWrappedMiniLabel);
        GUI.color = oldColor;
    }

    /// <summary>
    /// Draw a blend/softness parameter with help text (delegates to helper class)
    /// </summary>
    private void DrawBlendParameter(string propertyName, string label, string helpText)
    {
        NataneToonShaderGUIHelpers.DrawBlendParameter(
            propertyName,
            propertyName,
            label,
            helpText,
            DrawProperty,
            DrawHelpToggle);
    }

    /// <summary>
    /// Draw blend controls (blend mode + blend amount + optional blur) for an effect section
    /// Groups controls under a sub-section header with proper indentation
    /// </summary>
    private void DrawBlendControls(MaterialEditor materialEditor, Material material, string blendProp, string blendModeProp = null, string blurProp = null)
    {
        bool hasBlend = material.HasProperty(blendProp);
        bool hasBlendMode = blendModeProp != null && material.HasProperty(blendModeProp);
        bool hasBlur = blurProp != null && material.HasProperty(blurProp);

        if (!hasBlend && !hasBlendMode && !hasBlur) return;

        EditorGUILayout.Space(SECTION_SPACING);
        EditorGUILayout.LabelField(L("── ブレンド＆ブラー ──", "── Blend & Blur ──"), EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        if (hasBlendMode)
        {
            MaterialProperty blendModeMat = FindProperty(blendModeProp, properties, false);
            if (blendModeMat != null)
                materialEditor.ShaderProperty(blendModeMat, L("ブレンドモード", "Blend Mode"));
        }
        if (hasBlend)
        {
            MaterialProperty blendMat = FindProperty(blendProp, properties, false);
            if (blendMat != null)
                materialEditor.ShaderProperty(blendMat, L("ブレンド", "Blend"));
        }
        if (hasBlur)
        {
            MaterialProperty blurMat = FindProperty(blurProp, properties, false);
            if (blurMat != null)
                materialEditor.ShaderProperty(blurMat, L("ブラー", "Blur"));
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Draw UV animation settings (scroll speed XY + rotation speed) for any texture
    /// </summary>
    private void DrawUVAnimationSettings(string scrollProp, string rotateProp, string label)
    {
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField(L($"UVアニメーション ({label})", $"UV Animation ({label})"), EditorStyles.miniLabel);
        DrawProperty(scrollProp, L("スクロール速度 XY", "Scroll Speed XY"));
        DrawProperty(rotateProp, L("回転速度", "Rotation Speed"));
    }

    /// <summary>
    /// Draw Emission-specific UV animation settings (backward-compatible Float×2 + Float layout)
    /// </summary>
    private void DrawEmissionUVAnimationSettings()
    {
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField(L("UVアニメーション (エミッション)", "UV Animation (Emission)"), EditorStyles.miniLabel);
        DrawProperty("_EmissionScrollSpeed", L("スクロール速度 X", "Scroll Speed X"));
        DrawProperty("_EmissionScrollSpeedY", L("スクロール速度 Y", "Scroll Speed Y"));
        DrawProperty("_EmissionRotateSpeed", L("回転速度", "Rotation Speed"));
    }

    /// <summary>
    /// Draw presets and sharing section
    /// </summary>

    private void DrawHalftoneShadowSection()
    {
        SetFoldout("HalftoneShadow", DrawBoxedSection(L("ハーフトーンシャドウ", "Halftone Shadow"), GetFoldout("HalftoneShadow"), SectionCategory.Effects, "_HALFTONE_SHADOW"));
        if (GetFoldout("HalftoneShadow"))
        {
            bool enableHalftone = DrawToggle("_HALFTONE_SHADOW", "_HalftoneShadow", L("ハーフトーンシャドウを有効化", "Enable Halftone Shadow"));
            if (enableHalftone)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_HalftoneShadowColor", L("ハーフトーンカラー", "Halftone Color"));
                DrawProperty("_HalftoneShadowScale", L("パターンスケール", "Pattern Scale"));
                DrawProperty("_HalftoneShadowThreshold", L("影閾値", "Shadow Threshold"));
                DrawProperty("_HalftoneShadowSoftness", L("ソフトネス", "Softness"));
                DrawProperty("_HalftoneShadowIntensity", L("強度", "Intensity"));
                DrawProperty("_HalftoneShadowBlend", L("ブレンド", "Blend"));
                DrawHelpToggle("HalftoneShadow",
                    L("ハーフトーンシャドウ:\n" +
                    "影の部分にハーフトーン（網点）パターンを適用します。\n" +
                    "漫画やコミック調のシャドウ表現に最適です。\n\n" +
                    "・ハーフトーンカラー: 影の色味（乗算）\n" +
                    "・パターンスケール: ドットの大きさ（1〜200）\n" +
                    "・影閾値: 影が始まるライティング閾値\n" +
                    "・ソフトネス: 影境界のぼかし量\n" +
                    "・強度: ハーフトーン効果の強さ",
                    "Halftone Shadow:\n" +
                    "Applies halftone (dot pattern) to shadow areas.\n" +
                    "Perfect for manga/comic-style shadow rendering.\n\n" +
                    "• Halftone Color: shadow tint (multiply)\n" +
                    "• Pattern Scale: dot size (1-200)\n" +
                    "• Shadow Threshold: lighting threshold for shadow\n" +
                    "• Softness: shadow edge blur amount\n" +
                    "• Intensity: halftone effect strength"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("HalftoneShadow"));
    }

    private void DrawShadowEdgeNoiseSection()
    {
        SetFoldout("ShadowEdgeNoise", DrawBoxedSection(L("影エッジノイズ", "Shadow Edge Noise"), GetFoldout("ShadowEdgeNoise"), SectionCategory.Effects, "_SHADOW_EDGE_NOISE"));
        if (GetFoldout("ShadowEdgeNoise"))
        {
            bool enableNoise = DrawToggle("_SHADOW_EDGE_NOISE", "_ShadowEdgeNoise", L("影エッジノイズを有効化", "Enable Shadow Edge Noise"));
            if (enableNoise)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_ShadowEdgeNoiseScale", L("ノイズスケール", "Noise Scale"));
                DrawProperty("_ShadowEdgeNoiseIntensity", L("ノイズ強度", "Noise Intensity"));
                DrawProperty("_ShadowEdgeNoiseWidth", L("エッジ幅", "Edge Width"));
                DrawProperty("_ShadowEdgeNoiseTex", L("ノイズテクスチャ", "Noise Texture"));
                DrawHelpToggle("ShadowEdgeNoise",
                    L("影エッジノイズ:\n" +
                    "影の境界にノイズを加え、手描き風のアナログ感を演出します。\n\n" +
                    "• ノイズスケール: ノイズの細かさ\n" +
                    "• ノイズ強度: 効果の強さ\n" +
                    "• エッジ幅: ノイズが適用される影境界の幅\n" +
                    "• ノイズテクスチャ: カスタムノイズパターン",
                    "Shadow Edge Noise:\n" +
                    "Adds noise to shadow edges for a hand-drawn, analog feel.\n\n" +
                    "• Noise Scale: noise granularity\n" +
                    "• Noise Intensity: effect strength\n" +
                    "• Edge Width: shadow boundary width for noise\n" +
                    "• Noise Texture: custom noise pattern"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("ShadowEdgeNoise"));
    }

    private void DrawCastShadowColorSection()
    {
        SetFoldout("CastShadowColor", DrawBoxedSection(L("キャストシャドウカラー", "Cast Shadow Color"), GetFoldout("CastShadowColor"), SectionCategory.Lighting, "_CAST_SHADOW_COLOR"));
        if (GetFoldout("CastShadowColor"))
        {
            bool enableCSC = DrawToggle("_CAST_SHADOW_COLOR", "_CastShadowColorEnable", L("キャストシャドウカラーを有効化", "Enable Cast Shadow Color"));
            if (enableCSC)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_CastShadowTint", L("キャストシャドウ色", "Cast Shadow Tint"));
                DrawProperty("_CastShadowIntensity", L("キャストシャドウ強度", "Cast Shadow Intensity"));
                DrawHelpToggle("CastShadowColor",
                    L("キャストシャドウカラー:\n" +
                    "他のオブジェクトから受ける落ち影の色を調整します。\n\n" +
                    "• シャドウ色: 落ち影の色味\n" +
                    "• 強度: 色の適用量",
                    "Cast Shadow Color:\n" +
                    "Adjusts the color of shadows cast by other objects.\n\n" +
                    "• Shadow Tint: shadow color\n" +
                    "• Intensity: color application amount"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("CastShadowColor"));
    }

    private void DrawLightSnapSection()
    {
        SetFoldout("LightSnap", DrawBoxedSection(L("ライト方向スナップ", "Light Direction Snap"), GetFoldout("LightSnap"), SectionCategory.Lighting, "_LIGHT_SNAP"));
        if (GetFoldout("LightSnap"))
        {
            bool enableSnap = DrawToggle("_LIGHT_SNAP", "_LightSnap", L("ライトスナップを有効化", "Enable Light Snap"));
            if (enableSnap)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_LightSnapAngle", L("スナップ角度（度）", "Snap Angle (degrees)"));
                DrawProperty("_LightSnapSmoothness", L("スナップの滑らかさ", "Snap Smoothness"));
                DrawHelpToggle("LightSnap",
                    L("ライト方向スナップ:\n" +
                    "ライトの方向を離散的な角度にスナップさせ、\n" +
                    "影のちらつきを防止します。\n\n" +
                    "• スナップ角度: スナップする角度刻み（度）\n" +
                    "• 滑らかさ: スナップ間の補間量",
                    "Light Direction Snap:\n" +
                    "Snaps light direction to discrete angles\n" +
                    "to prevent shadow flickering.\n\n" +
                    "• Snap Angle: angle step in degrees\n" +
                    "• Smoothness: interpolation between snap positions"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("LightSnap"));
    }

    private void DrawProceduralMatCapSection()
    {
        SetFoldout("ProceduralMatCap", DrawBoxedSection(L("プロシージャルMatCap", "Procedural MatCap"), GetFoldout("ProceduralMatCap"), SectionCategory.Effects, "_PROCEDURAL_MATCAP"));
        if (GetFoldout("ProceduralMatCap"))
        {
            bool enableProcMatCap = DrawToggle("_PROCEDURAL_MATCAP", "_ProceduralMatCap", L("プロシージャルMatCapを有効化", "Enable Procedural MatCap"));
            if (enableProcMatCap)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_ProcMatCapColor", L("MatCapカラー", "MatCap Color"));
                DrawProperty("_ProcMatCapPower", L("フレネルパワー", "Fresnel Power"));
                DrawProperty("_ProcMatCapIntensity", L("強度", "Intensity"));
                DrawProperty("_ProcMatCapBlend", L("ブレンド", "Blend"));
                DrawBlendControls(materialEditor, targetMaterial, "_ProcMatCapBlend", "_ProcMatCapBlendMode");
                DrawHelpToggle("ProceduralMatCap",
                    L("プロシージャルMatCap:\n" +
                    "テクスチャ不要でフレネルベースのMatCap効果を生成します。\n\n" +
                    "• MatCapカラー: 効果の色\n" +
                    "• フレネルパワー: エッジ強調度（高い値=より鋭いエッジ）\n" +
                    "• 強度: 効果の強さ",
                    "Procedural MatCap:\n" +
                    "Generates Fresnel-based MatCap effect without textures.\n\n" +
                    "• MatCap Color: effect color\n" +
                    "• Fresnel Power: edge emphasis (higher=sharper)\n" +
                    "• Intensity: effect strength"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("ProceduralMatCap"));
    }

    private void DrawFakeReflectionSection()
    {
        SetFoldout("FakeReflection", DrawBoxedSection(L("フェイクリフレクション", "Fake Reflection"), GetFoldout("FakeReflection"), SectionCategory.Environment, "_FAKE_REFLECTION"));
        if (GetFoldout("FakeReflection"))
        {
            bool enableFakeRef = DrawToggle("_FAKE_REFLECTION", "_FakeReflection", L("フェイクリフレクションを有効化", "Enable Fake Reflection"));
            if (enableFakeRef)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_FakeRefSkyColor", L("空カラー", "Sky Color"));
                DrawColorProperty("_FakeRefGroundColor", L("地面カラー", "Ground Color"));
                DrawProperty("_FakeRefIntensity", L("反射強度", "Reflection Intensity"));
                DrawProperty("_FakeRefSmoothness", L("スムースネス", "Smoothness"));
                DrawProperty("_FakeRefBlend", L("ブレンド", "Blend"));
                DrawBlendControls(materialEditor, targetMaterial, "_FakeRefBlend", "_FakeRefBlendMode");
                DrawHelpToggle("FakeReflection",
                    L("フェイクリフレクション:\n" +
                    "キューブマップ不要の軽量環境反射です。\n" +
                    "法線方向に基づいて空と地面の色をブレンドします。\n\n" +
                    "• 空カラー/地面カラー: 環境色\n" +
                    "• 反射強度: 反射の強さ\n" +
                    "• スムースネス: 反射のぼかし度",
                    "Fake Reflection:\n" +
                    "Lightweight environment reflection without cubemaps.\n" +
                    "Blends sky and ground colors based on normal direction.\n\n" +
                    "• Sky/Ground Color: environment colors\n" +
                    "• Reflection Intensity: reflection strength\n" +
                    "• Smoothness: reflection blur amount"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("FakeReflection"));
    }

    private void DrawPerspectiveFlatSection()
    {
        SetFoldout("PerspectiveFlat", DrawBoxedSection(L("パースフラット", "Perspective Flatten"), GetFoldout("PerspectiveFlat"), SectionCategory.Advanced, "_PERSPECTIVE_FLAT"));
        if (GetFoldout("PerspectiveFlat"))
        {
            bool enablePF = DrawToggle("_PERSPECTIVE_FLAT", "_PerspectiveFlat", L("パースフラットを有効化", "Enable Perspective Flatten"));
            if (enablePF)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_PerspectiveFlatAmount", L("フラット量", "Flatten Amount"));
                DrawProperty("_PerspectiveFlatReferenceZ", L("基準Z距離", "Reference Z Distance"));
                DrawHelpToggle("PerspectiveFlat",
                    L("パースフラット:\n" +
                    "パース（遠近感）を圧縮し、2Dイラスト風の平面的な見た目にします。\n\n" +
                    "• フラット量: 0=通常パース、1=完全に平面化\n" +
                    "• 基準Z距離: パース圧縮の基準点\n\n" +
                    "💡 2Dアニメ風の表現に最適です。",
                    "Perspective Flatten:\n" +
                    "Compresses perspective for a 2D illustration-like flat appearance.\n\n" +
                    "• Flatten Amount: 0=normal perspective, 1=fully flattened\n" +
                    "• Reference Z Distance: reference point for compression\n\n" +
                    "💡 Perfect for 2D anime-style rendering."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("PerspectiveFlat"));
    }

    private void DrawDepthColorFadeSection()
    {
        SetFoldout("DepthColorFade", DrawBoxedSection(L("深度カラーフェード", "Depth Color Fade"), GetFoldout("DepthColorFade"), SectionCategory.Environment, "_DEPTH_COLOR_FADE"));
        if (GetFoldout("DepthColorFade"))
        {
            bool enableDCF = DrawToggle("_DEPTH_COLOR_FADE", "_DepthColorFade", L("深度カラーフェードを有効化", "Enable Depth Color Fade"));
            if (enableDCF)
            {
                EditorGUI.indentLevel++;
                DrawColorProperty("_DepthFadeColor", L("大気カラー", "Atmosphere Color"));
                DrawProperty("_DepthFadeStart", L("開始距離", "Fade Start Distance"));
                DrawProperty("_DepthFadeEnd", L("終了距離", "Fade End Distance"));
                DrawProperty("_DepthFadeIntensity", L("フェード強度", "Fade Intensity"));
                DrawProperty("_DepthFadeDesaturation", L("彩度低下", "Desaturation"));
                DrawHelpToggle("DepthColorFade",
                    L("深度カラーフェード:\n" +
                    "カメラからの距離に応じて色と彩度を変化させます。\n" +
                    "空気遠近法（大気パースペクティブ）を再現します。\n\n" +
                    "• 大気カラー: 遠方の色\n" +
                    "• 開始/終了距離: フェードの範囲\n" +
                    "• フェード強度: 効果の強さ\n" +
                    "• 彩度低下: 遠方ほど色が薄くなる量",
                    "Depth Color Fade:\n" +
                    "Changes color and saturation based on camera distance.\n" +
                    "Simulates aerial perspective (atmospheric perspective).\n\n" +
                    "• Atmosphere Color: distant color\n" +
                    "• Start/End Distance: fade range\n" +
                    "• Fade Intensity: effect strength\n" +
                    "• Desaturation: color washout at distance"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
        EndBoxedSection(GetFoldout("DepthColorFade"));
    }

    private void DrawCurrentStateSection()
    {
        SetFoldout("CurrentState", DrawBoxedSection(L("編集ワークフロー", "Workflow"), GetFoldout("CurrentState"), SectionCategory.Basic));
        if (GetFoldout("CurrentState"))
        {
            NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate samplerBudget = GetCurrentSamplerBudgetEstimate();

            EditorGUILayout.LabelField(
                L("最初にここで Shader Type と編集仕様を切り替えて、下の早見表で現在状態を確認できます。 (Ctrl+Z で元に戻せます)",
                  "Switch shader and editing workflow here first, then confirm the current state in the summary below. (Ctrl+Z to undo)"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            DrawWorkflowControls();

            if (!workflowShouldReturn && !workflowIsNonToon)
            {
                EditorGUILayout.Space(6);
                DrawCurrentStateRow(L("シェーダー", "Shader"), GetCurrentShaderLabel(), true);
                DrawCurrentStateRow(L("描画タイプ", "Rendering"), GetRenderingModeLabel(GetCurrentRenderingMode()));
                DrawCurrentStateRow(L("編集仕様", "Editing Workflow"), GetCurrentEditingModeLabel());
                DrawCurrentStateRow(L("ベース表現", "Base Shading"), GetCurrentBaseShadingLabel());
                DrawCurrentStateRow(L("見た目プリセット", "Look Mode"), GetCurrentLookModeLabel());
                DrawCurrentStateRow(L("移行状態", "Migration"), GetCurrentMigrationSummary(), true);
                DrawCurrentStateRow(L("主な有効機能", "Active Features"), GetActiveFeatureSummary(targetMaterial, 4), true);
                DrawCurrentStateRow(L("Sampler / Pass", "Sampler / Pass"), GetSamplerBudgetSummary(samplerBudget));
            }
        }
        EndBoxedSection(GetFoldout("CurrentState"));
    }

    private void DrawWorkflowControls()
    {
        workflowIsNonToon = NataneToon.Editor.NataneToonShaderTypeSwitcher.DrawShaderTypeDropdown(targetMaterial, materialEditor, out bool shouldReturn);
        workflowShouldReturn = shouldReturn;
        if (workflowShouldReturn)
        {
            return;
        }

        if (workflowIsNonToon)
        {
            EditorGUILayout.HelpBox(
                L("現在の Shader Type は Toon 系ではありません。編集仕様の設定は Toon 系シェーダーで使えます。",
                  "The current shader type is not a toon variant. Editing workflow settings are available on toon shaders."),
                MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        RenderingMode currentMode = GetCurrentRenderingMode();
        RenderingMode newMode = (RenderingMode)EditorGUILayout.Popup(L("描画タイプ", "Rendering Type"), (int)currentMode, RenderingModeLabels);
        if (EditorGUI.EndChangeCheck())
        {
            SetRenderingMode(newMode);
        }

        DrawIntegratedEditingWorkflowControls();
    }

    private void DrawIntegratedEditingWorkflowControls()
    {
        bool isMigratedMaterial = IsLilToonMigratedMaterial(targetMaterial);

        if (!isMigratedMaterial)
        {
            return;
        }

        EditorGUILayout.Space(4);

        // 移行情報 — 1行サマリー + アクション
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string sourceShader = GetLilToonSourceShader(targetMaterial);
        LilToonParityFlags parityFlags = GetLilToonParityFlags(targetMaterial);
        int warningCount = CountLilToonParityFlags(parityFlags);
        string summary = string.IsNullOrEmpty(sourceShader)
            ? L("lilToon から移行済み", "Migrated from lilToon")
            : L($"lilToon ({sourceShader}) から移行済み", $"Migrated from lilToon ({sourceShader})");
        if (warningCount > 0)
        {
            summary += L($"  — 互換警告: {warningCount}件", $"  — {warningCount} parity warning(s)");
        }
        EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(L("Auto Setup で再最適化", "Re-optimize with Auto Setup"), GUILayout.Height(22)))
        {
            quickSetupWizardMode = 3;
            SetFoldout("QuickSetup", true);
        }
        if (GUILayout.Button(L("移行ツール", "Migration Tool"), GUILayout.Height(22)))
        {
            OpenLilToonMigrationTool();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawCurrentStateRow(string label, string value, bool wrapValue = false)
    {
        if (EditorGUIUtility.currentViewWidth < 380f)
        {
            // Narrow view: stack label and value vertically
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(value, wrapValue ? EditorStyles.wordWrappedMiniLabel : EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
        else
        {
            // Normal horizontal layout
            // P-13: Dynamic label width based on view width, with tooltip for full label
            float labelWidth = Mathf.Min(EditorGUIUtility.currentViewWidth * 0.3f, 140f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, label), EditorStyles.miniBoldLabel, GUILayout.Width(labelWidth));
            EditorGUILayout.LabelField(value, wrapValue ? EditorStyles.wordWrappedMiniLabel : EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// Draws a sub-group header with a left accent line and indented bold label
    /// to visually distinguish sub-groups from main section headers.
    /// </summary>
    private void DrawSubGroupHeader(string label)
    {
        EditorGUILayout.Space(2);
        Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        // Left accent line
        float lineX = rect.x + 4;
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(new Rect(lineX, rect.y, 2, rect.height), new Color(0.5f, 0.5f, 0.5f, 0.4f));
        }
        // Indented label
        Rect labelRect = new Rect(rect.x + 12, rect.y, rect.width - 12, rect.height);
        EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
    }

    private string GetCurrentShaderLabel()
    {
        if (targetMaterial == null || targetMaterial.shader == null)
        {
            return L("未設定", "Unassigned");
        }

        return targetMaterial.shader.name;
    }

    private string GetRenderingModeLabel(RenderingMode mode)
    {
        switch (mode)
        {
            case RenderingMode.Cutout:
                return L("カットアウト", "Cutout");
            case RenderingMode.Transparent:
                return L("透明", "Transparent");
            case RenderingMode.Fur:
                return L("ファー", "Fur");
            case RenderingMode.Background:
                return L("背景", "Background");
            default:
                return L("不透明", "Opaque");
        }
    }

    private string GetCurrentEditingModeLabel()
    {
        if (targetMaterial == null)
        {
            return L("未設定", "Unassigned");
        }

        if (IsLilToonMigratedMaterial(targetMaterial))
        {
            return L("Natane仕様 (lilToon移行済み)", "Natane Workflow (migrated from lilToon)");
        }

        return L("Natane仕様", "Natane Workflow");
    }

    private string GetCurrentBaseShadingLabel()
    {
        if (targetMaterial == null)
        {
            return L("未設定", "Unassigned");
        }

        if (targetMaterial.IsKeywordEnabled("_USE_RAMP"))
        {
            return L("ランプ", "Ramp");
        }

        float shadingMode = targetMaterial.HasProperty("_ShadingMode")
            ? targetMaterial.GetFloat("_ShadingMode")
            : 0f;

        if (shadingMode >= PbrLikeShadingModeThreshold)
        {
            return L("PBRライク", "PBR-Like");
        }

        if (shadingMode >= 0.5f)
        {
            return L("グラデーション", "Gradient");
        }

        return L("トゥーン", "Toon");
    }

    private string GetCurrentLookModeLabel()
    {
        if (targetMaterial == null || !targetMaterial.HasProperty("_LookMode"))
        {
            return GetLookModeLabels()[(int)LookMode.Legacy];
        }

        string[] labels = GetLookModeLabels();
        int currentLookMode = Mathf.Clamp(Mathf.RoundToInt(targetMaterial.GetFloat("_LookMode")), 0, labels.Length - 1);
        return labels[currentLookMode];
    }

    private string GetCurrentMigrationSummary()
    {
        if (targetMaterial == null || !IsLilToonMigratedMaterial(targetMaterial))
        {
            return L("Natane標準素材", "Native Natane Material");
        }

        LilToonMigrationMode migrationMode = GetLilToonMigrationMode(targetMaterial);

        LilToonParityFlags parityFlags = GetLilToonParityFlags(targetMaterial);
        int parityCount = CountLilToonParityFlags(parityFlags);
        string review = parityCount > 0
            ? L($" / 要確認: {parityCount}", $" / Review: {parityCount}")
            : string.Empty;

        return $"{GetLilToonMigrationModeLabel(migrationMode)}{review}";
    }

    private string GetSamplerBudgetSummary(NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate samplerBudget)
    {
        string status = samplerBudget.IsOverLimit
            ? L("上限超過", "Over Limit")
            : samplerBudget.IsNearLimit
                ? L("上限近い", "Near Limit")
                : samplerBudget.IsWarning
                    ? L("注意", "Warning")
                    : L("余裕あり", "Comfortable");

        string extraPass = samplerBudget.ExtraPassCount > 0
            ? L($" / 追加Pass +{samplerBudget.ExtraPassCount}", $" / Extra Pass +{samplerBudget.ExtraPassCount}")
            : string.Empty;

        return $"{status} / {samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}{extraPass}";
    }

    private string[][] GetFeatureOverviewEntries()
    {
        return new string[][]
        {
            new[] { "_SPECULAR", L("スペキュラー", "Specular"), "_Specular" },
            new[] { "_HAIR_SPECULAR", L("ヘアハイライト", "Hair Highlight"), "_HairSpecular" },
            new[] { "_RIM_LIGHT", L("リムライト", "Rim Light"), "_RimLight" },
            new[] { "_SSS", "SSS", "_SSS" },
            new[] { "_MATCAP", "MatCap", "_MatCap" },
            new[] { "_GLITTER", L("グリッター", "Glitter"), "_Glitter" },
            new[] { "_WATER_DRIP", L("雫", "Drip"), "_WaterDrip" },
            new[] { "_SMEAR", L("スミア", "Smear"), "_Smear" },
            new[] { "_HOLOGRAM", L("ホログラム", "Hologram"), "_Hologram" },
            new[] { "_DECAL", L("デカール", "Decal"), "_Decal" },
            new[] { "_OUTLINE", L("アウトライン", "Outline"), "_Outline" },
            new[] { "_HALFTONE_SHADOW", L("ハーフトーンシャドウ", "Halftone Shadow"), "_HalftoneShadow" },
            new[] { "_SHADOW_EDGE_NOISE", L("影エッジノイズ", "Shadow Edge Noise"), "_ShadowEdgeNoise" },
            new[] { "_CAST_SHADOW_COLOR", L("キャストシャドウカラー", "Cast Shadow Color"), "_CastShadowColorEnable" },
            new[] { "_LIGHT_SNAP", L("ライトスナップ", "Light Snap"), "_LightSnap" },
            new[] { "_PROCEDURAL_MATCAP", L("プロシージャルMatCap", "Procedural MatCap"), "_ProceduralMatCap" },
            new[] { "_FAKE_REFLECTION", L("フェイクリフレクション", "Fake Reflection"), "_FakeReflection" },
            new[] { "_PERSPECTIVE_FLAT", L("パースフラット", "Perspective Flatten"), "_PerspectiveFlat" },
            new[] { "_DEPTH_COLOR_FADE", L("深度カラーフェード", "Depth Color Fade"), "_DepthColorFade" },
            new[] { "_EMISSION", L("エミッション", "Emission"), "_Emission" },
            new[] { "_AUDIOLINK", "AudioLink", "_AudioLink" },
            new[] { "_REFLECTION", L("リフレクション", "Reflection"), "_Reflection" },
            new[] { "_IRIDESCENCE", L("イリデッセンス", "Iridescence"), "_Iridescence" },
            new[] { "_ENV_RIM", L("環境リム", "Env Rim"), "_EnvRim" },
            new[] { "_REFRACTION", L("屈折", "Refraction"), "_Refraction" },
            new[] { "_NORMALMAP", L("ノーマルマップ", "Normal Map"), "_UseNormalMap" },
            new[] { "_PARALLAX", L("パララックス", "Parallax"), "_Parallax" },
            new[] { "_VERTEX_ANIMATION", L("頂点アニメーション", "Vertex Anim"), "_VertexAnimation" },
            new[] { "_VAT", "VAT", "_VAT" },
            new[] { "_USE_AO", "AO", "_UseAO" },
            new[] { "_USE_DITHERING", L("ディザリング", "Dithering"), "_UseDithering" },
            new[] { "_USE_LIGHT_VOLUME", "Light Volume", "_UseLightVolume" },
            new[] { "_DISTANCE_FADE", L("距離フェード", "Dist Fade"), "_DistanceFade" },
            new[] { "_BACKFACE_TEXTURE", L("裏面", "Backface"), "_BackfaceTexture" },
            new[] { "_COLOR_QUANTIZE", L("色量子化", "Quantize"), "_UseColorQuantize" },
            new[] { "_LUT_3D", "3D LUT", "_UseLUT3D" },
            new[] { "_HATCHING", L("ハッチング", "Hatching"), "_UseHatching" },
            new[] { "_WATERCOLOR", L("水彩", "Watercolor"), "_UseWatercolor" },
            new[] { "_SOFT_FILTER", L("ソフトフィルター", "Soft Filter"), "_UseSoftFilter" },
            new[] { "_KUWAHARA_FILTER", "Kuwahara", "_UseKuwahara" },
            new[] { "_SCREEN_EDGE", L("エッジ検出", "Edge Detect"), "_UseScreenEdge" },
            new[] { "_COLOR_BLEEDING", L("色にじみ", "Bleeding"), "_UseColorBleeding" },
            new[] { "_CHROMATIC_ABERRATION", L("色収差", "Chrom Aber"), "_UseChromaticAberration" },
            new[] { "_OUTLINE_HAND_DRAWN", L("手書き線", "Hand-drawn"), "_UseHandDrawnOutline" },
        };
    }

    private string GetActiveFeatureSummary(Material material, int maxItems)
    {
        if (material == null)
        {
            return L("未設定", "Unassigned");
        }

        string[][] features = GetFeatureOverviewEntries();
        List<string> activeLabels = new List<string>();
        int activeCount = 0;

        for (int i = 0; i < features.Length; i++)
        {
            if (!material.IsKeywordEnabled(features[i][0]))
            {
                continue;
            }

            activeCount++;
            if (activeLabels.Count < maxItems)
            {
                activeLabels.Add(features[i][1]);
            }
        }

        if (activeCount == 0)
        {
            return L("主要な追加機能は未使用", "No major extra features");
        }

        string summary = string.Join(", ", activeLabels.ToArray());
        if (activeCount > activeLabels.Count)
        {
            summary += L($" +{activeCount - activeLabels.Count}件", $" +{activeCount - activeLabels.Count}");
        }

        return $"{summary} ({activeCount})";
    }

    private void DrawPresetsSection()
    {
        SetFoldout("Presets", DrawBoxedSection(L("マテリアルプリセット＆共有", "Material Presets & Sharing"), GetFoldout("Presets"), SectionCategory.Basic));
        if (GetFoldout("Presets"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ プリセット管理で開く", "→ Open Preset Manager",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.PresetManager, 0, targetMaterial));
            }
            NataneToonShaderGUIUtility.DrawMaterialActionsToolbar(targetMaterial, materialEditor);
        }
        EndBoxedSection(GetFoldout("Presets"));
    }

    /// <summary>
    /// Draw Feature Overview panel - shows all feature ON/OFF states in a compact grid
    /// 機能一覧パネル - 全機能のON/OFF状態をコンパクトなグリッドで表示
    /// </summary>
    private void DrawFeatureOverviewSection()
    {
        SetFoldout("FeatureOverview", DrawBoxedSection(L("機能一覧", "Feature Overview"), GetFoldout("FeatureOverview"), SectionCategory.Basic));
        if (GetFoldout("FeatureOverview"))
        {
            // Feature keywords and display names for the overview grid
            string[][] features = GetFeatureOverviewEntries();

            int enabledCount = 0;
            // P-11: Responsive column count based on inspector width
            float viewWidth = EditorGUIUtility.currentViewWidth;
            int columns = viewWidth < 350f ? 2 : (viewWidth < 500f ? 3 : 4);

            for (int i = 0; i < features.Length; i++)
            {
                if (i % columns == 0)
                    EditorGUILayout.BeginHorizontal();

                string keyword = features[i][0];
                bool isEnabled = targetMaterial.IsKeywordEnabled(keyword);
                var toggleEvaluation = GetCachedToggleEvaluation(keyword);
                bool canEnable = isEnabled || toggleEvaluation.CanEnable;
                if (isEnabled) enabledCount++;

                Color badgeColor = isEnabled
                    ? new Color(0.2f, 0.7f, 0.3f, 0.9f)
                    : (canEnable ? new Color(0.4f, 0.4f, 0.4f, 0.4f) : new Color(0.85f, 0.55f, 0.2f, 0.75f));
                Color textColor = isEnabled ? Color.white : (canEnable ? new Color(0.6f, 0.6f, 0.6f) : Color.white);

                string tooltip = !isEnabled && !canEnable
                    ? L(
                        $"Sampler 制限のため有効化できません (+{toggleEvaluation.AddedSamplers}, 推定 {toggleEvaluation.AfterEnable.EstimatedSamplers}/{toggleEvaluation.AfterEnable.Limit})",
                        $"Cannot enable because of the sampler limit (+{toggleEvaluation.AddedSamplers}, estimated {toggleEvaluation.AfterEnable.EstimatedSamplers}/{toggleEvaluation.AfterEnable.Limit})")
                    : string.Empty;
                Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(features[i][1], tooltip), EditorStyles.miniButton, GUILayout.Height(20));

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(btnRect, badgeColor);
                }

                var oldColor = GUI.contentColor;
                GUI.contentColor = textColor;
                GUI.Label(btnRect, new GUIContent(features[i][1], tooltip), new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = isEnabled ? FontStyle.Bold : FontStyle.Normal
                });
                GUI.contentColor = oldColor;

                // クリックでトグル
                if (features[i].Length > 2 && !string.IsNullOrEmpty(features[i][2]))
                {
                    if (canEnable || isEnabled)
                    {
                        EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);
                    }

                    if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
                    {
                        string propName = features[i][2];
                        MaterialProperty prop = FindProperty(propName, properties, false);
                        if (prop != null)
                        {
                            bool newState = !(prop.floatValue > 0.5f);
                            if (!newState || canEnable)
                            {
                                Undo.RecordObject(targetMaterial, "Toggle " + keyword);
                                prop.floatValue = newState ? 1.0f : 0.0f;
                                if (newState)
                                    targetMaterial.EnableKeyword(keyword);
                                else
                                    targetMaterial.DisableKeyword(keyword);
                                EditorUtility.SetDirty(targetMaterial);
                            }
                        }
                        Event.current.Use();
                    }
                }

                if (i % columns == columns - 1 || i == features.Length - 1)
                    EditorGUILayout.EndHorizontal();
            }

            // Fill remaining columns if last row is incomplete
            int remainder = features.Length % columns;
            if (remainder != 0)
            {
                // Row was already ended above
            }

            EditorGUILayout.Space(4);

            // Performance rating bar
            string rating;
            Color ratingColor;
            if (enabledCount <= 3) { rating = "A"; ratingColor = NataneToonColorPalette.PerformanceA; }
            else if (enabledCount <= 6) { rating = "B"; ratingColor = NataneToonColorPalette.PerformanceB; }
            else if (enabledCount <= 9) { rating = "C"; ratingColor = NataneToonColorPalette.PerformanceC; }
            else { rating = "D"; ratingColor = NataneToonColorPalette.PerformanceD; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L($"有効機能: {enabledCount}/{features.Length}", $"Active Features: {enabledCount}/{features.Length}"), GUILayout.Width(120));
            Rect barRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
                float fillWidth = barRect.width * ((float)enabledCount / features.Length);
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), ratingColor);
            }
            EditorGUILayout.LabelField($"Rating: {rating}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ マテリアル検証で開く", "→ Open Material Validator",
                    () => EditorApplication.ExecuteMenuItem(NataneToolMenuPaths.MaterialValidator));
            }
        }
        EndBoxedSection(GetFoldout("FeatureOverview"));
    }

    /// <summary>
    /// Draw performance indicator section
    /// </summary>
    private void DrawPerformanceSection()
    {
        SetFoldout("Performance", DrawBoxedSection(L("パフォーマンス", "Performance"), GetFoldout("Performance"), SectionCategory.Basic));
        if (GetFoldout("Performance"))
        {
            if (targetMaterial != null)
            {
                NataneToonShaderGUIUtility.DrawOpenInStudioButton(
                    "→ マテリアル分析で開く", "→ Open Material Analysis",
                    () => NataneToolBridge.OpenConsolidatedWindow(NataneToolMenuPaths.MaterialAnalysis, 0, targetMaterial));
            }
            NataneToonShaderGUIUtility.DrawPerformanceIndicatorWithSamplerBudget(targetMaterial, GetCurrentSamplerBudgetEstimate());

            DrawHelpToggle("PerformanceHint",
                L("ヒント：使用していない機能を無効化するとパフォーマンスが向上します。\n" +
                "チェックボックスのある機能はオン/オフの切り替えが可能です。",
                "Hint: Disabling unused features improves performance.\n" +
                "Features with checkboxes can be toggled on/off."),
                MessageType.Info);
        }
        EndBoxedSection(GetFoldout("Performance"));
    }

    /// <summary>
    /// Load UI state (selected tab) from EditorPrefs
    /// </summary>
    private void LoadUIState()
    {
        selectedTab = EditorPrefs.GetInt(NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, "SelectedTab"), 0);

        // Feature 1: Inspector mode
        inspectorMode = (InspectorMode)EditorPrefs.GetInt(InspectorModePrefsKey, (int)InspectorMode.Simple);

        // Feature 2: Active Only filter
        showActiveOnly = EditorPrefs.GetBool(ShowActiveOnlyPrefsKey, false);

        // Ensure all expected foldout keys exist with defaults to prevent null reference
        // issues when foldout mappings are modified between versions
        foreach (var kvp in foldoutPrefsKeys)
        {
            if (!foldoutStates.ContainsKey(kvp.Key))
            {
                bool defaultVal = foldoutDefaultTrue.Contains(kvp.Key);
                foldoutStates[kvp.Key] = EditorPrefs.GetBool(
                    NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, kvp.Value), defaultVal);
            }
        }
    }

    /// <summary>
    /// Save UI state (selected tab) to EditorPrefs
    /// </summary>
    private void SaveUIState()
    {
        EditorPrefs.SetInt(NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, "SelectedTab"), selectedTab);
    }

    // LoadFoldoutStates/SaveFoldoutStates removed: Dictionary-based GetFoldout/SetFoldout
    // handles lazy loading from EditorPrefs and immediate persistence on change.

    // ===== UI HELPER METHODS =====

    /// <summary>
    /// Draw expand all / collapse all buttons for a tab
    /// タブ用の全展開/全折畳ボタンを描画
    /// </summary>
    private void DrawExpandCollapseButtons(System.Action<bool> setAllFoldouts)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(L("全展開", "Expand All"), EditorStyles.miniButtonLeft, GUILayout.Width(50)))
        {
            setAllFoldouts(true);
        }
        if (GUILayout.Button(L("全折畳", "Collapse All"), EditorStyles.miniButtonRight, GUILayout.Width(50)))
        {
            setAllFoldouts(false);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// Draw compact header with quick actions
    /// </summary>
    private void DrawCompactHeader()
    {
        // 1行目: タイトル + 言語切替
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("Natane Toon Shader", CachedHeaderTitleStyle, GUILayout.Width(200));

        GUILayout.FlexibleSpace();

        // Language toggle
        string langLabel = IsJapanese ? "JP" : "EN";
        if (GUILayout.Button(langLabel, GUILayout.Width(30), GUILayout.Height(20)))
        {
            NataneToonLocalization.ToggleLanguage();
            materialEditor?.Repaint();
        }

        EditorGUILayout.EndHorizontal();

        // 2行目: アクションボタン（ツールバー風）
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button(new GUIContent(L("更新", "Sync"), L("キーワードとキャッシュを同期", "Synchronize keywords and caches")), EditorStyles.toolbarButton, GUILayout.Width(45)))
        {
            SynchronizeKeywordsAndRefreshInspectorCaches();
        }

        // Feature 1: Simple / Advanced mode toggle
        EditorGUI.BeginChangeCheck();
        bool isSimple = GUILayout.Toggle(inspectorMode == InspectorMode.Simple,
            L("シンプル", "Simple"), EditorStyles.toolbarButton, GUILayout.Width(55));
        bool isAdvanced = GUILayout.Toggle(inspectorMode == InspectorMode.Advanced,
            L("アドバンス", "Advanced"), EditorStyles.toolbarButton, GUILayout.Width(65));
        if (EditorGUI.EndChangeCheck())
        {
            // Only one can be active; whichever was just clicked wins
            if (isSimple && inspectorMode != InspectorMode.Simple)
                inspectorMode = InspectorMode.Simple;
            else if (isAdvanced && inspectorMode != InspectorMode.Advanced)
                inspectorMode = InspectorMode.Advanced;
            EditorPrefs.SetInt(InspectorModePrefsKey, (int)inspectorMode);
            if (materialEditor != null) materialEditor.Repaint();
            GUIUtility.ExitGUI();
        }

        // Feature 2: Active Only filter (visible only in Advanced mode)
        if (inspectorMode == InspectorMode.Advanced)
        {
            EditorGUI.BeginChangeCheck();
            bool newActiveOnly = GUILayout.Toggle(showActiveOnly,
                L("有効のみ", "Active Only"), EditorStyles.toolbarButton, GUILayout.Width(60));
            if (EditorGUI.EndChangeCheck())
            {
                showActiveOnly = newActiveOnly;
                EditorPrefs.SetBool(ShowActiveOnlyPrefsKey, showActiveOnly);
                if (materialEditor != null) materialEditor.Repaint();
                GUIUtility.ExitGUI();
            }
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(L("全展開", "Expand All"), EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExpandAllSections(true);
            if (materialEditor != null) materialEditor.Repaint();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button(L("全折畳", "Collapse All"), EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExpandAllSections(false);
            if (materialEditor != null) materialEditor.Repaint();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(SECTION_SPACING);
    }

    // ===== TAB DRAWING METHODS =====

    /// <summary>
    /// Draw Basic tab content (Main Texture, Surface Finish, Makeup, Shading)
    /// </summary>
    private void DrawBasicTab()
    {
        DrawExpandCollapseButtons((state) => {
            SetFoldout("MainTexture", state); SetFoldout("MakeupTextures", state);
            SetFoldout("ScreenTone", state); SetFoldout("HalftoneShadow", state);
            SetFoldout("ShadowEdgeNoise", state); SetFoldout("GradientBaseColor", state);
            SetFoldout("Shading", state);
        });
        if (ShouldShowSection("QuickSetup"))
        {
            SetFoldout("QuickSetup", DrawBoxedSection(L("クイックセットアップ", "Quick Setup"), GetFoldout("QuickSetup"), SectionCategory.Basic));
            if (GetFoldout("QuickSetup"))
            {
                SafeDrawSection(DrawQuickSetupSection, L("クイックセットアップ", "Quick Setup"));
            }
            EndBoxedSection(GetFoldout("QuickSetup"));
            EditorGUILayout.Space(SECTION_SPACING);
        }

        FilteredDrawSection(DrawMainTextureSection, L("メインテクスチャ", "Main Texture"), "MainTexture");
        if (ShouldShowSection("MainTexture")) { DrawSurfaceFinishSection(); EditorGUILayout.Space(SECTION_SPACING); }
        FilteredDrawSection(DrawMakeupTexturesSection, L("メイクアップテクスチャ", "Makeup Textures"), "MakeupTextures");
        FilteredDrawSection(DrawScreenToneSection, L("スクリーントーン", "Screen Tone"), "ScreenTone");
        FilteredDrawSection(DrawHalftoneShadowSection, L("ハーフトーンシャドウ", "Halftone Shadow"), "HalftoneShadow");
        FilteredDrawSection(DrawShadowEdgeNoiseSection, L("影エッジノイズ", "Shadow Edge Noise"), "ShadowEdgeNoise");
        FilteredDrawSection(DrawGradientBaseColorSection, L("グラデーションベースカラー", "Gradient Base Color"), "GradientBaseColor");
        FilteredDrawSection(DrawShadingSection, L("シェーディング", "Shading"), "Shading");
    }

    private void DrawSharedInspectorSections()
    {
        FilteredDrawSection(DrawPresetsSection, L("プリセット", "Presets"), "Presets");
        FilteredDrawSection(DrawFeatureOverviewSection, L("機能一覧", "Feature Overview"), "FeatureOverview");
    }

    /// <summary>
    /// Draw Lighting tab content
    /// </summary>
    private void DrawLightingTab()
    {
        DrawExpandCollapseButtons((state) => {
            SetFoldout("AdvancedLighting", state); SetFoldout("CastShadowColor", state); SetFoldout("LightSnap", state);
            SetFoldout("AO", state); SetFoldout("Dithering", state);
            SetFoldout("LightVolume", state); SetFoldout("LTCGI", state);
            SetFoldout("BackgroundLightmap", state); SetFoldout("PBR", state);
        });
        // ─── ライティング基本 ───
        NataneToonShaderGUIUtility.DrawCategoryDivider(L("ライティング基本", "Lighting Basics"));
        FilteredDrawSection(DrawAdvancedLightingSection, L("高度なライティング", "Advanced Lighting"), "AdvancedLighting");
        FilteredDrawSection(DrawCastShadowColorSection, L("キャストシャドウカラー", "Cast Shadow Color"), "CastShadowColor");
        FilteredDrawSection(DrawLightSnapSection, L("ライト方向スナップ", "Light Direction Snap"), "LightSnap");
        FilteredDrawSection(DrawAOSection, "AO", "AO");
        FilteredDrawSection(DrawDitheringSection, L("ディザリング", "Dithering"), "Dithering");

        // ─── 外部ライティング ───
        int extLightCount = CountEnabledKeywords("_USE_LIGHT_VOLUME", "_LTCGI");
        NataneToonShaderGUIUtility.DrawCategoryDivider(
            L($"外部ライティング ({extLightCount}/2)", $"External Lighting ({extLightCount}/2)"));
        FilteredDrawSection(DrawLightVolumeSection, "Light Volume", "LightVolume");
        FilteredDrawSection(DrawLTCGISection, "LTCGI", "LTCGI");

        // ─── 背景シェーダー専用 ───
        if (GetCurrentRenderingMode() == RenderingMode.Background)
        {
            NataneToonShaderGUIUtility.DrawCategoryDivider(L("背景シェーダー専用", "Background Shader Only"));
            FilteredDrawSection(DrawBackgroundLightmapSection, L("背景ライトマップ", "Background Lightmap"), "BackgroundLightmap");
            FilteredDrawSection(DrawPBRSection, L("PBR マテリアル", "PBR Material"), "PBR");
        }
    }

    /// <summary>
    /// Draw Effects tab content
    /// </summary>
    private void DrawEffectsTab()
    {
        DrawExpandCollapseButtons((state) => {
            SetFoldout("Specular", state); SetFoldout("HairSpecular", state); SetFoldout("RimLight", state); SetFoldout("SSS", state);
            SetFoldout("MatCap", state); SetFoldout("ProceduralMatCap", state); SetFoldout("Glitter", state); SetFoldout("Drip", state); SetFoldout("Smear", state); SetFoldout("Fur", state); SetFoldout("Decal", state);
            SetFoldout("Hologram", state); SetFoldout("Outline", state); SetFoldout("Emission", state);
            SetFoldout("VirtualExpression", state); SetFoldout("AudioLink", state); SetFoldout("SurfaceCover", state);
        });
        // ─── 光源エフェクト ───
        int lightCount = CountEnabledKeywords("_SPECULAR", "_HAIR_SPECULAR", "_RIM_LIGHT", "_SSS");
        NataneToonShaderGUIUtility.DrawCategoryDivider(
            L($"光源エフェクト ({lightCount}/4)", $"Light Source Effects ({lightCount}/4)"));
        FilteredDrawSection(DrawSpecularSection, L("スペキュラー", "Specular"), "Specular");
        FilteredDrawSection(DrawHairSpecularSection, L("ヘアスペキュラー", "Hair Specular"), "HairSpecular");
        FilteredDrawSection(DrawRimLightSection, L("リムライト", "Rim Light"), "RimLight");
        FilteredDrawSection(DrawSSSSection, "SSS", "SSS");

        // ─── 表面エフェクト ───
        int surfaceCount = CountEnabledKeywords("_MATCAP", "_PROCEDURAL_MATCAP", "_GLITTER", "_WATER_DRIP", "_SMEAR", "_FUR", "_DECAL", "_SURFACE_COVER");
        NataneToonShaderGUIUtility.DrawCategoryDivider(
            L($"表面エフェクト ({surfaceCount}/8)", $"Surface Effects ({surfaceCount}/8)"));
        FilteredDrawSection(DrawMatCapSection, "MatCap", "MatCap");
        FilteredDrawSection(DrawProceduralMatCapSection, L("プロシージャルMatCap", "Procedural MatCap"), "ProceduralMatCap");
        FilteredDrawSection(DrawGlitterSection, L("グリッター", "Glitter"), "Glitter");
        FilteredDrawSection(DrawDripSection, L("雫エフェクト", "Drip Effect"), "Drip");
        FilteredDrawSection(DrawSmearSection, L("スミア", "Smear"), "Smear");
        FilteredDrawSection(DrawFurSection, L("ファー", "Fur"), "Fur");
        FilteredDrawSection(DrawDecalSection, L("デカール", "Decal"), "Decal");
        FilteredDrawSection(DrawSurfaceCoverSection, L("サーフェスカバー", "Surface Cover"), "SurfaceCover");

        // ─── ビジュアルエフェクト ───
        int visualCount = CountEnabledKeywords("_COLOR_QUANTIZE", "_HOLOGRAM", "_OUTLINE", "_EMISSION", "_AUDIOLINK");
        NataneToonShaderGUIUtility.DrawCategoryDivider(
            L($"ビジュアルエフェクト ({visualCount}/5)", $"Visual Effects ({visualCount}/5)"));
        FilteredDrawSection(DrawHologramSection, L("ホログラム＆グリッチ", "Hologram & Glitch"), "Hologram");
        FilteredDrawSection(DrawIllustrationStyleSection, L("イラスト調スタイル", "Illustration Style"), "IllustrationStyle");
        FilteredDrawSection(DrawOutlineSection, L("アウトライン", "Outline"), "Outline");
        FilteredDrawSection(DrawEmissionSection, L("エミッション", "Emission"), "Emission");
        FilteredDrawSection(DrawVirtualExpressionSection, L("バーチャル表現", "Virtual Expression"), "VirtualExpression");
        FilteredDrawSection(DrawAudioLinkSection, "AudioLink", "AudioLink");
    }

    /// <summary>
    /// Draw Environment tab content
    /// </summary>
    private void DrawEnvironmentTab()
    {
        DrawExpandCollapseButtons((state) => {
            SetFoldout("Reflection", state); SetFoldout("FakeReflection", state); SetFoldout("Iridescence", state);
            SetFoldout("EnvironmentalRim", state); SetFoldout("Refraction", state);
            SetFoldout("HeightFog", state);
        });
        FilteredDrawSection(DrawReflectionSection, L("リフレクション", "Reflection"), "Reflection");
        FilteredDrawSection(DrawFakeReflectionSection, L("フェイクリフレクション", "Fake Reflection"), "FakeReflection");
        FilteredDrawSection(DrawIridescenceSection, L("イリデッセンス", "Iridescence"), "Iridescence");
        FilteredDrawSection(DrawEnvironmentalRimSection, L("環境リム", "Environmental Rim"), "EnvironmentalRim");
        FilteredDrawSection(DrawRefractionSection, L("屈折", "Refraction"), "Refraction");
        FilteredDrawSection(DrawHeightFogSection, L("ハイトフォグ", "Height Fog"), "HeightFog");
        FilteredDrawSection(DrawDepthColorFadeSection, L("深度カラーフェード", "Depth Color Fade"), "DepthColorFade");
    }

    /// <summary>
    /// Draw Advanced tab content
    /// </summary>
    private void DrawAdvancedTab()
    {
        DrawExpandCollapseButtons((state) => {
            SetFoldout("NormalMap", state); SetFoldout("Parallax", state); SetFoldout("VertexAnimation", state); SetFoldout("VAT", state);
            SetFoldout("Backface", state); SetFoldout("Video", state); SetFoldout("HeightFade", state); SetFoldout("IntersectionFade", state);
            SetFoldout("DistanceFade", state); SetFoldout("PerspectiveFlat", state); SetFoldout("Rendering", state); SetFoldout("Tessellation", state);
            SetFoldout("DetailMap", state); SetFoldout("Triplanar", state); SetFoldout("MirrorControl", state); SetFoldout("QuestLite", state);
        });
        // ─── マッピング ───
        int mapCount = CountEnabledKeywords("_NORMALMAP", "_PARALLAX");
        NataneToonShaderGUIUtility.DrawCategoryDivider(
            L($"マッピング ({mapCount}/2)", $"Mapping ({mapCount}/2)"));
        FilteredDrawSection(DrawNormalMapSection, L("ノーマルマップ", "Normal Map"), "NormalMap");
        FilteredDrawSection(DrawParallaxSection, L("視差マッピング", "Parallax Mapping"), "Parallax");
        FilteredDrawSection(DrawDetailMapSection, L("ディテールマップ", "Detail Map"), "DetailMap");
        FilteredDrawSection(DrawTriplanarSection, L("トライプレーナー", "Triplanar Mapping"), "Triplanar");

        // ─── アニメーション＆特殊 ───
        NataneToonShaderGUIUtility.DrawCategoryDivider(L("アニメーション＆特殊", "Animation & Special"));
        FilteredDrawSection(DrawVertexAnimationSection, L("頂点アニメーション（風/呼吸/脈動）", "Vertex Animation (Wind/Breath/Pulse)"), "VertexAnimation");
        FilteredDrawSection(DrawVATSection, L("VAT（頂点アニメーション）", "VAT (Vertex Animation Texture)"), "VAT");
        FilteredDrawSection(DrawTessellationSection, L("テッセレーション（曲面スムージング）", "Tessellation (Surface Smoothing)"), "Tessellation");
        FilteredDrawSection(DrawBackfaceSection, L("裏面テクスチャ", "Backface Texture"), "Backface");
        FilteredDrawSection(DrawVideoSection, L("ビデオテクスチャ", "Video Texture"), "Video");
        FilteredDrawSection(DrawHeightFadeSection, L("高さフェード", "Height Fade"), "HeightFade");
        FilteredDrawSection(DrawIntersectionFadeSection, L("オブジェクト交差フェード", "Intersection Fade"), "IntersectionFade");
        FilteredDrawSection(DrawDistanceFadeSection, L("距離フェード", "Distance Fade"), "DistanceFade");
        FilteredDrawSection(DrawPerspectiveFlatSection, L("パースフラット", "Perspective Flatten"), "PerspectiveFlat");

        // ─── VRChat＆パフォーマンス ───
        NataneToonShaderGUIUtility.DrawCategoryDivider(L("VRChat＆パフォーマンス", "VRChat & Performance"));
        FilteredDrawSection(DrawMirrorControlSection, L("ミラー・カメラ制御", "Mirror / Camera Control"), "MirrorControl");
        FilteredDrawSection(DrawQuestLiteSection, L("Quest軽量パス", "Quest Lite"), "QuestLite");

        // ─── レンダリング ───
        NataneToonShaderGUIUtility.DrawCategoryDivider(L("レンダリング", "Rendering"));
        FilteredDrawSection(DrawRenderingSection, L("レンダリング", "Rendering"), "Rendering");
    }

    /// <summary>
    /// Draw search results - shows only sections matching the query
    /// 検索結果を描画 - P-18: スコアベースのランキングで表示
    /// </summary>
    private void DrawSearchResults(string query)
    {
        string lowerQuery = query.ToLowerInvariant();

        // P-18: スコアベースのランキング
        var scored = new List<(int index, int score)>();

        for (int i = 0; i < sectionSearchData.Length; i++)
        {
            string displayName = sectionSearchData[i][1].ToLowerInvariant();
            string keywords = sectionSearchData[i][2].ToLowerInvariant();

            int score = 0;
            // 表示名の前方一致: 最高スコア
            if (displayName.StartsWith(lowerQuery)) score = 3;
            // 表示名の部分一致: 高スコア
            else if (displayName.Contains(lowerQuery)) score = 2;
            // キーワードの一致: 標準スコア
            else if (keywords.Contains(lowerQuery)) score = 1;

            if (score > 0) scored.Add((i, score));
        }

        // スコア降順でソート
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        bool anyMatch = scored.Count > 0;

        // マッチ数を表示
        if (anyMatch)
        {
            EditorGUILayout.LabelField(
                L($"{scored.Count} 件のセクションが見つかりました", $"{scored.Count} section(s) found"),
                EditorStyles.miniLabel);
        }

        foreach (var item in scored)
        {
            string methodKey = sectionSearchData[item.index][0];
            System.Action drawAction = GetSectionDrawAction(methodKey);
            if (drawAction != null)
            {
                SafeDrawSection(drawAction, methodKey);
            }
        }

        if (!anyMatch)
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(
                L($"「{query}」に一致するセクションが見つかりませんでした。",
                  $"No sections found matching \"{query}\"."),
                MessageType.Info);
        }
    }

    /// <summary>
    /// Get the draw action for a section by its method key
    /// </summary>
    private System.Action GetSectionDrawAction(string methodKey)
    {
        switch (methodKey)
        {
            case "CurrentState": return DrawCurrentStateSection;
            case "MainTexture": return DrawMainTextureSection;
            case "MakeupTextures": return DrawMakeupTexturesSection;
            case "ScreenTone": return DrawScreenToneSection;
            case "Shading": return DrawShadingSection;
            case "AdvancedLighting": return DrawAdvancedLightingSection;
            case "AO": return DrawAOSection;
            case "Dithering": return DrawDitheringSection;
            case "LightVolume": return DrawLightVolumeSection;
            case "LTCGI": return DrawLTCGISection;
            case "Specular": return DrawSpecularSection;
            case "HairSpecular": return DrawHairSpecularSection;
            case "RimLight": return DrawRimLightSection;
            case "SSS": return DrawSSSSection;
            case "MatCap": return DrawMatCapSection;
            case "Glitter": return DrawGlitterSection;
            case "Drip": return DrawDripSection;
            case "Smear": return DrawSmearSection;
            case "Fur": return DrawFurSection;
            case "BackgroundLightmap": return DrawBackgroundLightmapSection;
            case "PBR": return DrawPBRSection;
            case "Hologram": return DrawHologramSection;
            case "IllustrationStyle": return DrawIllustrationStyleSection;
            case "Decal": return DrawDecalSection;
            case "Outline": return DrawOutlineSection;
            case "Emission": return DrawEmissionSection;
            case "VirtualExpression": return DrawVirtualExpressionSection;
            case "AudioLink": return DrawAudioLinkSection;
            case "Reflection": return DrawReflectionSection;
            case "Iridescence": return DrawIridescenceSection;
            case "EnvironmentalRim": return DrawEnvironmentalRimSection;
            case "Refraction": return DrawRefractionSection;
            case "NormalMap": return DrawNormalMapSection;
            case "Parallax": return DrawParallaxSection;
            case "VertexAnimation": return DrawVertexAnimationSection;
            case "VAT": return DrawVATSection;
            case "Tessellation": return DrawTessellationSection;
            case "Backface": return DrawBackfaceSection;
            case "Video": return DrawVideoSection;
            case "GradientBaseColor": return DrawGradientBaseColorSection;
            case "HeightFade": return DrawHeightFadeSection;
            case "IntersectionFade": return DrawIntersectionFadeSection;
            case "DistanceFade": return DrawDistanceFadeSection;
            case "Rendering": return DrawRenderingSection;
            case "DetailMap": return DrawDetailMapSection;
            case "Triplanar": return DrawTriplanarSection;
            case "HeightFog": return DrawHeightFogSection;
            case "SurfaceCover": return DrawSurfaceCoverSection;
            case "MirrorControl": return DrawMirrorControlSection;
            case "QuestLite": return DrawQuestLiteSection;
            case "HalftoneShadow": return DrawHalftoneShadowSection;
            case "ShadowEdgeNoise": return DrawShadowEdgeNoiseSection;
            case "CastShadowColor": return DrawCastShadowColorSection;
            case "LightSnap": return DrawLightSnapSection;
            case "ProceduralMatCap": return DrawProceduralMatCapSection;
            case "FakeReflection": return DrawFakeReflectionSection;
            case "PerspectiveFlat": return DrawPerspectiveFlatSection;
            case "DepthColorFade": return DrawDepthColorFadeSection;
            default: return null;
        }
    }

    // ===== P-16: SECTION ICON HELPER =====

    /// <summary>
    /// Get a Unicode icon prefix for a section category
    /// セクションカテゴリに応じたアイコンを返す
    /// </summary>
    private static string GetSectionIcon(SectionCategory category)
    {
        switch (category)
        {
            case SectionCategory.Basic:       return "\ud83c\udfa8"; // palette
            case SectionCategory.Shading:     return "\u2600";       // sun
            case SectionCategory.Lighting:    return "\ud83d\udca1"; // light bulb
            case SectionCategory.Effects:     return "\u2728";       // sparkles
            case SectionCategory.Environment: return "\ud83c\udf0d"; // globe
            case SectionCategory.Advanced:    return "\u2699";       // gear
            default:                          return "";
        }
    }

    // ===== P-14: JUMP MENU =====

    /// <summary>
    /// Show a dropdown menu to jump to a specific section in the current tab.
    /// 現在のタブ内のセクションにジャンプするドロップダウンメニューを表示する。
    /// </summary>
    private void ShowJumpMenu()
    {
        GenericMenu menu = new GenericMenu();
        string[][] sections = GetCurrentTabSections();
        foreach (var section in sections)
        {
            string key = section[0];
            string label = section[1];
            menu.AddItem(new GUIContent(label), GetFoldout(key), () =>
            {
                // Toggle this section open and collapse others
                foreach (var s in sections)
                    SetFoldout(s[0], s[0] == key);
                if (materialEditor != null) materialEditor.Repaint();
            });
        }
        menu.ShowAsContext();
    }

    /// <summary>
    /// Get section key/label pairs for the currently selected tab.
    /// 現在選択中のタブのセクション一覧を返す。
    /// </summary>
    private string[][] GetCurrentTabSections()
    {
        switch (selectedTab)
        {
            case 0: return new[] {
                new[] { "QuickSetup", L("クイックセットアップ", "Quick Setup") },
                new[] { "MainTexture", L("メインテクスチャ", "Main Texture") },
                new[] { "MakeupTextures", L("メイクアップテクスチャ", "Makeup Textures") },
                new[] { "ScreenTone", L("スクリーントーン", "Screen Tone") },
                new[] { "HalftoneShadow", L("ハーフトーンシャドウ", "Halftone Shadow") },
                new[] { "ShadowEdgeNoise", L("影エッジノイズ", "Shadow Edge Noise") },
                new[] { "GradientBaseColor", L("グラデーション", "Gradient Base Color") },
                new[] { "Shading", L("シェーディング", "Shading") }
            };
            case 1: return new[] {
                new[] { "AdvancedLighting", L("高度なライティング", "Advanced Lighting") },
                new[] { "CastShadowColor", L("キャストシャドウ", "Cast Shadow Color") },
                new[] { "LightSnap", L("ライトスナップ", "Light Snap") },
                new[] { "AO", "AO" },
                new[] { "Dithering", L("ディザリング", "Dithering") },
                new[] { "LightVolume", "Light Volume" },
                new[] { "LTCGI", "LTCGI" }
            };
            case 2: return new[] {
                new[] { "Specular", L("スペキュラー", "Specular") },
                new[] { "HairSpecular", L("ヘアスペキュラー", "Hair Specular") },
                new[] { "RimLight", L("リムライト", "Rim Light") },
                new[] { "SSS", "SSS" },
                new[] { "MatCap", "MatCap" },
                new[] { "ProceduralMatCap", L("プロシージャルMatCap", "Procedural MatCap") },
                new[] { "Glitter", L("グリッター", "Glitter") },
                new[] { "Drip", L("雫エフェクト", "Drip Effect") },
                new[] { "Smear", L("スミア", "Smear") },
                new[] { "Fur", L("ファー", "Fur") },
                new[] { "Decal", L("デカール", "Decal") },
                new[] { "SurfaceCover", L("サーフェスカバー", "Surface Cover") },
                new[] { "Hologram", L("ホログラム", "Hologram") },
                new[] { "Outline", L("アウトライン", "Outline") },
                new[] { "Emission", L("エミッション", "Emission") },
                new[] { "VirtualExpression", L("バーチャル表現", "Virtual Expression") },
                new[] { "AudioLink", "AudioLink" }
            };
            case 3: return new[] {
                new[] { "Reflection", L("リフレクション", "Reflection") },
                new[] { "FakeReflection", L("フェイクリフレクション", "Fake Reflection") },
                new[] { "Iridescence", L("イリデッセンス", "Iridescence") },
                new[] { "EnvironmentalRim", L("環境リム", "Environmental Rim") },
                new[] { "Refraction", L("屈折", "Refraction") },
                new[] { "HeightFog", L("ハイトフォグ", "Height Fog") },
                new[] { "DepthColorFade", L("深度カラーフェード", "Depth Color Fade") }
            };
            case 4: return new[] {
                new[] { "NormalMap", L("ノーマルマップ", "Normal Map") },
                new[] { "Parallax", L("パララックス", "Parallax") },
                new[] { "DetailMap", L("ディテールマップ", "Detail Map") },
                new[] { "Triplanar", L("トライプレーナー", "Triplanar") },
                new[] { "VertexAnimation", L("頂点アニメーション", "Vertex Animation") },
                new[] { "VAT", "VAT" },
                new[] { "Tessellation", L("テッセレーション", "Tessellation") },
                new[] { "Backface", L("裏面テクスチャ", "Backface Texture") },
                new[] { "Video", L("ビデオ", "Video") },
                new[] { "HeightFade", L("高さフェード", "Height Fade") },
                new[] { "IntersectionFade", L("交差フェード", "Intersection Fade") },
                new[] { "DistanceFade", L("距離フェード", "Distance Fade") },
                new[] { "PerspectiveFlat", L("パースフラット", "Perspective Flat") },
                new[] { "MirrorControl", L("ミラー・カメラ制御", "Mirror / Camera Control") },
                new[] { "QuestLite", L("Quest軽量", "Quest Lite") },
                new[] { "Rendering", L("レンダリング", "Rendering") }
            };
            default: return new string[0][];
        }
    }

    /// <summary>
    /// 3.2 Quick Setup Section - Quick preset buttons with wizard/legacy mode toggle
    /// P-9: Wizard-style guided setup for beginners, with legacy button layout retained
    /// </summary>
    private void DrawQuickSetupSection()
    {
        // Mode toggle: Auto Setup / Guided / Show All / Templates
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(quickSetupWizardMode == 3, L("自動", "Auto"), EditorStyles.miniButtonLeft))
            quickSetupWizardMode = 3;
        if (GUILayout.Toggle(quickSetupWizardMode == 0, L("ガイド付き", "Guided"), EditorStyles.miniButtonMid))
            quickSetupWizardMode = 0;
        if (GUILayout.Toggle(quickSetupWizardMode == 1, L("全表示", "Show All"), EditorStyles.miniButtonMid))
            quickSetupWizardMode = 1;
        if (GUILayout.Toggle(quickSetupWizardMode == 2, L("用途別", "Templates"), EditorStyles.miniButtonRight))
            quickSetupWizardMode = 2;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        switch (quickSetupWizardMode)
        {
            case 3:
                NataneToon.Editor.NataneAutoSetupHub.DrawAutoSetupPanel(
                    targetMaterial,
                    (mat, look) => ApplyBaseStyleForLook(mat, look));
                break;
            case 0:
                DrawQuickSetupWizard();
                break;
            case 1:
                DrawQuickSetupAllButtons();
                break;
            case 2:
                DrawCategoryPresets();
                break;
        }
    }

    /// <summary>
    /// Feature 3: Draw category preset templates in a 2-column grid.
    /// </summary>
    private void DrawCategoryPresets()
    {
        EditorGUILayout.HelpBox(
            L("用途に合わせたテンプレートを選択すると、必要な機能のみが有効になります。Ctrl+Z で元に戻せます。",
              "Select a template for your use case. Only the needed features will be enabled. Ctrl+Z to undo."),
            MessageType.Info);

        EditorGUILayout.Space(4);

        // 2-column grid
        for (int i = 0; i < categoryPresets.Length; i += 2)
        {
            EditorGUILayout.BeginHorizontal();
            DrawCategoryPresetButton(categoryPresets[i]);
            if (i + 1 < categoryPresets.Length)
                DrawCategoryPresetButton(categoryPresets[i + 1]);
            else
                GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);
    }

    private void DrawCategoryPresetButton(CategoryPreset preset)
    {
        string name = L(preset.nameJP, preset.nameEN);
        string desc = L(preset.descJP, preset.descEN);
        if (GUILayout.Button(new GUIContent(name, desc), GUILayout.Height(36)))
        {
            ApplyCategoryPreset(preset);
        }
    }

    /// <summary>
    /// Feature 3: Apply a category preset — turn off all toggles, then enable only the template's keywords.
    /// </summary>
    private void ApplyCategoryPreset(CategoryPreset preset)
    {
        // Record undo for all target materials
        foreach (Material mat in GetAllTargetMaterials())
        {
            if (mat != null)
                Undo.RecordObject(mat, L("用途別プリセット適用", "Apply Category Preset"));
        }

        // Turn OFF all toggle keywords on all targets
        foreach (var kvp in sectionToggleKeywords)
        {
            string keyword = kvp.Value;
            // Find the property name that maps to this keyword (look up via naming convention)
            foreach (Material mat in GetAllTargetMaterials())
            {
                if (mat == null) continue;
                mat.DisableKeyword(keyword);
            }
        }

        // Turn ON only the preset's specified keywords
        HashSet<string> enableSet = new HashSet<string>(preset.enableKeywords);
        foreach (string keyword in preset.enableKeywords)
        {
            foreach (Material mat in GetAllTargetMaterials())
            {
                if (mat == null) continue;
                mat.EnableKeyword(keyword);
            }

            // Auto-expand the corresponding section
            if (KeywordToSectionKey.TryGetValue(keyword, out string sectionKey))
            {
                SetFoldout(sectionKey, true);
            }
        }

        // Sync float properties with keyword state
        SynchronizeKeywordsAndRefreshInspectorCaches();

        // Mark all targets dirty
        foreach (Material mat in GetAllTargetMaterials())
        {
            if (mat != null) EditorUtility.SetDirty(mat);
        }

        // Switch to Simple mode if preset requests it
        if (preset.switchToSimpleMode && inspectorMode != InspectorMode.Simple)
        {
            inspectorMode = InspectorMode.Simple;
            EditorPrefs.SetInt(InspectorModePrefsKey, (int)inspectorMode);
        }
    }

    /// <summary>
    /// P-9: Legacy quick setup layout (all buttons visible at once)
    /// </summary>
    private void DrawQuickSetupAllButtons()
    {
        EditorGUILayout.HelpBox(
            L(
                "最初は 1. ベーススタイル 2. 表面の質感 の順で決めると迷いにくいです。",
                "Start with 1. base style and 2. surface finish to shape the look quickly."),
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();

        // P-19: Tooltips showing applied parameters
        if (GUILayout.Button(new GUIContent(
            L("シャープなアニメ調", "Sharp Anime Style"),
            L("適用される設定:\n• シェーディングモード: Toon\n• 影段数: 2\n• 影シャープネス: 0.05\n• 影ブレンド: 0\n• ライトブレンド: 0\n• アルベド保持: 0.8",
              "Applied settings:\n• Shading Mode: Toon\n• Shadow Steps: 2\n• Shadow Sharpness: 0.05\n• Shadow Blend: 0\n• Light Blend: 0\n• Albedo Preservation: 0.8")),
            GUILayout.Height(40)))
        {
            Undo.RecordObject(targetMaterial, "Apply Sharp Anime Style");
            ApplySharpAnimeStyle(targetMaterial);
        }

        if (GUILayout.Button(new GUIContent(
            L("柔らかい塗り調", "Soft Painting Style"),
            L("適用される設定:\n• シェーディングモード: Gradient\n• グラデーション幅: 0.3\n• ソフトネス: 0.5\n• 影ブレンド: 0.5\n• ライトブレンド: 0.4\n• アルベド保持: 0.7",
              "Applied settings:\n• Shading Mode: Gradient\n• Gradient Width: 0.3\n• Softness: 0.5\n• Shadow Blend: 0.5\n• Light Blend: 0.4\n• Albedo Preservation: 0.7")),
            GUILayout.Height(40)))
        {
            Undo.RecordObject(targetMaterial, "Apply Soft Painting Style");
            ApplySoftPaintingStyle(targetMaterial);
        }

        EditorGUILayout.EndHorizontal();

        // Visual description for row 1
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            L("→ はっきりした影境界・3段階の明暗・クリーンなセル調",
              "→ Sharp shadow edges, 3-step shading, clean cel look"),
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            L("→ なめらかなグラデーション影・柔らかい光の回り込み・イラスト風",
              "→ Smooth gradient shadows, soft light wrap, illustration style"),
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent(
            L("Toon-PBR ハイブリッド", "Toon-PBR Hybrid"),
            L("適用される設定:\n• シェーディングモード: Toon\n• 影段数: 2\n• 影シャープネス: 0.2\n• 影ブレンド: 0.3\n• 光沢: 0.5\n• マット: 0.3",
              "Applied settings:\n• Shading Mode: Toon\n• Shadow Steps: 2\n• Shadow Sharpness: 0.2\n• Shadow Blend: 0.3\n• Glossiness: 0.5\n• Matte: 0.3")),
            GUILayout.Height(40)))
        {
            Undo.RecordObject(targetMaterial, "Apply Toon-PBR Hybrid");
            ApplyToonPbrHybridStyle(targetMaterial);
        }

        if (GUILayout.Button(new GUIContent(
            L("Near PBR", "Near PBR"),
            L("適用される設定:\n• シェーディングモード: Gradient\n• グラデーション幅: 0.6\n• ソフトネス: 0.8\n• 影ブレンド: 0.7\n• 光沢: 0.8\n• マット: 0",
              "Applied settings:\n• Shading Mode: Gradient\n• Gradient Width: 0.6\n• Softness: 0.8\n• Shadow Blend: 0.7\n• Glossiness: 0.8\n• Matte: 0")),
            GUILayout.Height(40)))
        {
            Undo.RecordObject(targetMaterial, "Apply Near PBR");
            ApplyNearPbrStyle(targetMaterial);
        }

        EditorGUILayout.EndHorizontal();

        // Visual description for row 2
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            L("→ トゥーンの明暗＋PBRの質感・バランス型",
              "→ Toon shading + PBR textures, balanced look"),
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            L("→ リアルな質感・滑らかなライティング・写実的",
              "→ Realistic textures, smooth lighting, photorealistic"),
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Game character style preset (full width)
        if (GUILayout.Button(new GUIContent(
            L("⭐ ゲームキャラクター風", "⭐ Game Character Style"),
            L("適用される設定:\n• シェーディングモード: Toon\n• 影段数: 2\n• リムライト: ON\n• スペキュラー: ON\n• アウトライン: ON\n• テクスチャ連動アウトライン",
              "Applied settings:\n• Shading Mode: Toon\n• Shadow Steps: 2\n• Rim Light: ON\n• Specular: ON\n• Outline: ON\n• Texture-linked outline")),
            GUILayout.Height(40)))
        {
            Undo.RecordObject(targetMaterial, "Apply Game Character Style");
            ApplyGameCharacterStyle(targetMaterial);
            SynchronizeKeywordsAndRefreshInspectorCaches();
        }
        EditorGUILayout.LabelField(
            L("→ 2段影・シャープ境界・リムライト・スペキュラー・テクスチャ連動アウトライン",
              "→ 2-step shadows, sharp edges, rim light, specular, texture-linked outline"),
            EditorStyles.miniLabel);

        DrawQuickSetupPresetHints();

        // Surface finish buttons
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(L("マット", "Matte"), GUILayout.Height(25)))
        {
            Undo.RecordObject(targetMaterial, "Apply Matte Surface");
            targetMaterial.SetFloat("_Glossiness", 0.0f);
            targetMaterial.SetFloat("_MatteEffect", 1.0f);
            EditorUtility.SetDirty(targetMaterial);
        }

        if (GUILayout.Button(L("グロッシー", "Glossy"), GUILayout.Height(25)))
        {
            Undo.RecordObject(targetMaterial, "Apply Glossy Surface");
            targetMaterial.SetFloat("_Glossiness", 1.0f);
            targetMaterial.SetFloat("_MatteEffect", 0.0f);
            EditorUtility.SetDirty(targetMaterial);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            L(
                "マット: 落ち着いたアニメ調。グロッシー: 反射感とツヤを強めたいときにおすすめです。",
                "Matte gives a calm anime look. Glossy is better when you want stronger sheen and reflections."),
            MessageType.None);
        EditorGUILayout.Space(10);
    }

    /// <summary>
    /// P-9: Step-by-step wizard for quick setup
    /// </summary>
    private void DrawQuickSetupWizard()
    {
        // Step progress display
        string[] stepLabels = new[]
        {
            L("1. 用途を選択", "1. Select Purpose"),
            L("2. スタイルを選択", "2. Select Style"),
            L("3. 質感を調整", "3. Adjust Surface")
        };

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < stepLabels.Length; i++)
        {
            GUIStyle stepStyle = (i == quickSetupWizardStep)
                ? EditorStyles.miniBoldLabel
                : EditorStyles.miniLabel;
            Color oldColor = GUI.contentColor;
            if (i < quickSetupWizardStep)
                GUI.contentColor = new Color(0.3f, 0.8f, 0.3f); // Completed step = green
            else if (i == quickSetupWizardStep)
                GUI.contentColor = new Color(0.3f, 0.6f, 1f); // Current step = blue
            EditorGUILayout.LabelField(stepLabels[i], stepStyle);
            GUI.contentColor = oldColor;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        switch (quickSetupWizardStep)
        {
            case 0: DrawWizardStep1_Purpose(); break;
            case 1: DrawWizardStep2_Style(); break;
            case 2: DrawWizardStep3_Surface(); break;
        }

        // Navigation buttons
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(quickSetupWizardStep <= 0))
        {
            if (GUILayout.Button(L("\u2190 戻る", "\u2190 Back"), GUILayout.Height(25)))
                quickSetupWizardStep--;
        }
        GUILayout.FlexibleSpace();
        if (quickSetupWizardStep < 2)
        {
            if (GUILayout.Button(L("次へ \u2192", "Next \u2192"), GUILayout.Height(25)))
                quickSetupWizardStep++;
        }
        else
        {
            if (GUILayout.Button(L("\u2713 完了", "\u2713 Done"), GUILayout.Height(25)))
                quickSetupWizardStep = 0;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWizardStep1_Purpose()
    {
        EditorGUILayout.HelpBox(
            L("このマテリアルの主な用途を選んでください。おすすめのスタイルが次のステップに表示されます。",
              "Choose the main purpose of this material. Recommended styles will appear in the next step."),
            MessageType.Info);

        if (GUILayout.Button(L("キャラクター", "Character"), GUILayout.Height(35)))
        {
            quickSetupWizardStep = 1;
        }
        if (GUILayout.Button(L("背景・小物", "Environment / Props"), GUILayout.Height(35)))
        {
            quickSetupWizardStep = 1;
        }
        if (GUILayout.Button(L("エフェクト", "Effects"), GUILayout.Height(35)))
        {
            quickSetupWizardStep = 1;
        }
    }

    private void DrawWizardStep2_Style()
    {
        EditorGUILayout.HelpBox(
            L("ベースのスタイルを選んでください。クリックするとすぐに適用されます（Ctrl+Zで戻せます）。",
              "Choose a base style. It will be applied immediately (Ctrl+Z to undo)."),
            MessageType.Info);

        // Each button with description
        DrawWizardStyleButton(
            L("シャープなアニメ調", "Sharp Anime Style"),
            L("はっきりした影境界・セル調", "Sharp shadow edges, cel look"),
            () => { Undo.RecordObject(targetMaterial, "Apply Sharp Anime Style"); ApplySharpAnimeStyle(targetMaterial); });

        DrawWizardStyleButton(
            L("柔らかい塗り調", "Soft Painting Style"),
            L("なめらかなグラデーション影・イラスト風", "Smooth gradient shadows, illustration style"),
            () => { Undo.RecordObject(targetMaterial, "Apply Soft Painting Style"); ApplySoftPaintingStyle(targetMaterial); });

        DrawWizardStyleButton(
            L("ゲームキャラクター風", "Game Character Style"),
            L("2段影＋リムライト＋スペキュラー＋アウトライン", "2-step shadow + rim light + specular + outline"),
            () => { Undo.RecordObject(targetMaterial, "Apply Game Character Style"); ApplyGameCharacterStyle(targetMaterial); SynchronizeKeywordsAndRefreshInspectorCaches(); });

        DrawWizardStyleButton(
            L("Toon-PBR ハイブリッド", "Toon-PBR Hybrid"),
            L("トゥーン＋PBRの質感バランス型", "Toon + PBR texture balance"),
            () => { Undo.RecordObject(targetMaterial, "Apply Toon-PBR Hybrid"); ApplyToonPbrHybridStyle(targetMaterial); });

        DrawWizardStyleButton(
            L("Near PBR", "Near PBR"),
            L("リアルな質感・滑らかなライティング", "Realistic textures, smooth lighting"),
            () => { Undo.RecordObject(targetMaterial, "Apply Near PBR"); ApplyNearPbrStyle(targetMaterial); });
    }

    private void DrawWizardStep3_Surface()
    {
        EditorGUILayout.HelpBox(
            L("最後に表面の質感を選んでください。ここまでで基本的なルックが完成します！",
              "Finally, choose the surface finish. This completes the basic look!"),
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        DrawWizardStyleButton(
            L("マット", "Matte"),
            L("落ち着いた質感", "Calm, non-reflective"),
            () => {
                Undo.RecordObject(targetMaterial, "Apply Matte Surface");
                targetMaterial.SetFloat("_Glossiness", 0.0f);
                targetMaterial.SetFloat("_MatteEffect", 1.0f);
                EditorUtility.SetDirty(targetMaterial);
            });
        DrawWizardStyleButton(
            L("グロッシー", "Glossy"),
            L("ツヤと反射感", "Shiny, reflective"),
            () => {
                Undo.RecordObject(targetMaterial, "Apply Glossy Surface");
                targetMaterial.SetFloat("_Glossiness", 1.0f);
                targetMaterial.SetFloat("_MatteEffect", 0.0f);
                EditorUtility.SetDirty(targetMaterial);
            });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            L("セットアップ完了！各タブで細かい調整ができます。",
              "Setup complete! Use the tabs above for fine-tuning."),
            MessageType.None);
    }

    private void DrawWizardStyleButton(string title, string description, System.Action onClick)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (GUILayout.Button(title, GUILayout.Height(30)))
        {
            onClick?.Invoke();
        }
        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    // ===== P-10: Onboarding Panel =====

    private void DrawOnboardingPanel()
    {
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f, 0.3f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = oldBg;

        EditorGUILayout.LabelField(
            L("はじめての方へ", "Getting Started"),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });

        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField(
            L("Natane Toon Shader へようこそ！以下の手順でかんたんにセットアップできます。",
              "Welcome to Natane Toon Shader! Follow these steps for a quick setup."),
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(4);

        // Step indicators
        DrawOnboardingStep("1",
            L("「クイックセットアップ」でスタイルを選ぶ", "Choose a style in Quick Setup"),
            L("色タブの一番上にあります", "Found at the top of the Texture tab"));
        DrawOnboardingStep("2",
            L("各タブで機能を有効化・調整する", "Enable and adjust features in each tab"),
            L("Ctrl+1~5 でタブ切替できます", "Switch tabs with Ctrl+1~5"));
        DrawOnboardingStep("3",
            L("機能一覧で有効状態を確認する", "Check active features in Feature Overview"),
            L("パフォーマンス評価も表示されます", "Performance rating is also shown"));

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(L("OK、はじめる！", "OK, Let's go!"), GUILayout.Width(130), GUILayout.Height(25)))
        {
            _onboardingDismissed = true;
            EditorPrefs.SetBool(OnboardingPrefsKey, true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void DrawOnboardingStep(string number, string title, string hint)
    {
        EditorGUILayout.BeginHorizontal();

        // Step number
        GUIStyle numberStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        numberStyle.normal.textColor = new Color(0.3f, 0.6f, 1f);
        EditorGUILayout.LabelField(number, numberStyle, GUILayout.Width(20), GUILayout.Height(20));

        // Step content
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(hint, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawQuickSetupPresetHints()
    {
        bool narrowLayout = EditorGUIUtility.currentViewWidth < 420f;

        if (narrowLayout)
        {
            DrawQuickSetupHint(
                L("シャープなアニメ調", "Sharp Anime Style"),
                L("2段影とシャープな境界で、セル調をすぐ作れます。", "Fast cel-style setup with harder shadow borders."));
            DrawQuickSetupHint(
                L("柔らかい塗り調", "Soft Painting Style"),
                L("グラデーション寄りのやわらかい陰影に寄せます。", "Moves the look toward softer gradient shading."));
            DrawQuickSetupHint(
                L("Toon-PBR ハイブリッド", "Toon-PBR Hybrid"),
                L("トゥーンの陰影にPBRの質感を加えたバランス型です。", "Balanced blend of toon shading with PBR surface quality."));
            DrawQuickSetupHint(
                L("Near PBR", "Near PBR"),
                L("PBR寄りのリアルなライティングと質感です。", "Near-realistic lighting and surface finish."));
            DrawQuickSetupHint(
                L("ゲームキャラクター風", "Game Character Style"),
                L("2段影＋リムライト＋スペキュラー＋アウトラインの一括設定です。", "One-click setup with 2-step shadow, rim light, specular, and outline."));
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawQuickSetupHint(
            L("シャープなアニメ調", "Sharp Anime Style"),
            L("2段影とシャープな境界で、セル調をすぐ作れます。", "Fast cel-style setup with harder shadow borders."));
        DrawQuickSetupHint(
            L("柔らかい塗り調", "Soft Painting Style"),
            L("グラデーション寄りのやわらかい陰影に寄せます。", "Moves the look toward softer gradient shading."));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawQuickSetupHint(
            L("Toon-PBR ハイブリッド", "Toon-PBR Hybrid"),
            L("トゥーンの陰影にPBRの質感を加えたバランス型です。", "Balanced blend of toon shading with PBR surface quality."));
        DrawQuickSetupHint(
            L("Near PBR", "Near PBR"),
            L("PBR寄りのリアルなライティングと質感です。", "Near-realistic lighting and surface finish."));
        EditorGUILayout.EndHorizontal();

        DrawQuickSetupHint(
            L("ゲームキャラクター風", "Game Character Style"),
            L("2段影＋リムライト＋スペキュラー＋アウトラインの一括設定です。", "One-click setup with 2-step shadow, rim light, specular, and outline."));
    }

    private void DrawQuickSetupHint(string title, string description)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        }
    }

    /// <summary>
    /// Apply Sharp Anime Style preset (3.2 Quick Setup)
    /// </summary>
    internal void ApplySharpAnimeStyle(Material mat)
    {
        mat.SetFloat("_ShadingMode", 0); // Toon
        mat.SetFloat("_ShadowSteps", 2);
        mat.SetFloat("_ShadowSharpness", 0.05f);
        mat.SetFloat("_ShadowBlend", 0);
        mat.SetFloat("_LightBlend", 0);
        mat.SetFloat("_AlbedoPreservation", 0.8f);
        mat.SetFloat("_FinalHighlightBlend", 0.3f);
        // P-20: Group undo operations under a descriptive name
        Undo.SetCurrentGroupName(L("\u30D7\u30EA\u30BB\u30C3\u30C8\u9069\u7528: Sharp Anime", "Apply Preset: Sharp Anime"));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Apply Soft Painting Style preset (3.2 Quick Setup)
    /// </summary>
    internal void ApplySoftPaintingStyle(Material mat)
    {
        mat.SetFloat("_ShadingMode", 1); // Gradient
        mat.SetFloat("_ShadingGradientWidth", 0.3f);
        mat.SetFloat("_LitSoftness", 0.5f);
        mat.SetFloat("_ShadowBlend", 0.5f);
        mat.SetFloat("_LightBlend", 0.4f);
        mat.SetFloat("_AlbedoPreservation", 0.7f);
        // P-20: Group undo operations under a descriptive name
        Undo.SetCurrentGroupName(L("\u30D7\u30EA\u30BB\u30C3\u30C8\u9069\u7528: Soft Painting", "Apply Preset: Soft Painting"));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Apply Toon-PBR Hybrid preset (3.2 Quick Setup)
    /// </summary>
    internal void ApplyToonPbrHybridStyle(Material mat)
    {
        mat.SetFloat("_ShadingMode", 0); // Toon base
        mat.SetFloat("_ShadowSteps", 2);
        mat.SetFloat("_ShadowSharpness", 0.2f);
        mat.SetFloat("_ShadowBlend", 0.3f);
        mat.SetFloat("_LightBlend", 0.3f);
        mat.SetFloat("_AlbedoPreservation", 0.6f);
        mat.SetFloat("_Glossiness", 0.5f);
        mat.SetFloat("_MatteEffect", 0.3f);
        // P-20: Group undo operations under a descriptive name
        Undo.SetCurrentGroupName(L("\u30D7\u30EA\u30BB\u30C3\u30C8\u9069\u7528: Toon-PBR Hybrid", "Apply Preset: Toon-PBR Hybrid"));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Apply Near PBR preset (3.2 Quick Setup)
    /// </summary>
    internal void ApplyNearPbrStyle(Material mat)
    {
        mat.SetFloat("_ShadingMode", 1); // Gradient
        mat.SetFloat("_ShadingGradientWidth", 0.6f);
        mat.SetFloat("_LitSoftness", 0.8f);
        mat.SetFloat("_ShadowBlend", 0.7f);
        mat.SetFloat("_LightBlend", 0.6f);
        mat.SetFloat("_AlbedoPreservation", 0.5f);
        mat.SetFloat("_Glossiness", 0.8f);
        mat.SetFloat("_MatteEffect", 0.0f);
        // P-20: Group undo operations under a descriptive name
        Undo.SetCurrentGroupName(L("\u30D7\u30EA\u30BB\u30C3\u30C8\u9069\u7528: Near PBR", "Apply Preset: Near PBR"));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Apply game-character-style preset
    /// 2-step sharp shadows, warm shadow color, rim light, specular, texture-linked outline
    /// Inspired by modern 3D game character toon rendering techniques
    /// </summary>
    internal void ApplyGameCharacterStyle(Material mat)
    {
        // --- Shading: 2-step sharp toon ---
        mat.SetFloat("_ShadingMode", 0); // Toon
        mat.SetFloat("_ShadowSteps", 2);
        mat.SetFloat("_ShadowSharpness", 0.03f); // Very sharp boundary (near-binary step)
        mat.SetFloat("_ShadowBlend", 0);
        mat.SetFloat("_LitSoftness", 0);
        mat.SetFloat("_WrapAmount", 0);
        mat.SetFloat("_ShadowOffset", 0);

        // --- Shadow color: warm tint (game character warm shadow) ---
        mat.SetColor("_ShadowColor", new Color(0.62f, 0.52f, 0.54f, 1f));
        mat.SetFloat("_ShadowHueShift", 0.02f); // Slight warm hue shift
        mat.SetFloat("_ShadowSaturation", 1.15f); // Slightly boosted saturation

        // --- Albedo & tone ---
        mat.SetFloat("_AlbedoPreservation", 0.85f);
        mat.SetFloat("_FinalHighlightBlend", 0.2f);

        // --- Rim Light (Fresnel-based, game character standard) ---
        mat.SetFloat("_RimLight", 1); // Toggle ON → keyword synced later
        mat.SetColor("_RimColor", new Color(1f, 1f, 1f, 1f));
        mat.SetFloat("_RimPower", 2.5f); // Broad rim
        mat.SetFloat("_RimIntensity", 1.5f);
        mat.SetFloat("_RimSpread", 0f);

        // --- Specular (Blinn-Phong, sharp step threshold) ---
        mat.SetFloat("_Specular", 1); // Toggle ON
        mat.SetColor("_SpecularColor", new Color(1f, 1f, 1f, 1f));
        mat.SetFloat("_SpecularSize", 0.08f); // Small, focused highlight
        mat.SetFloat("_SpecularSoftness", 0.05f); // Sharp edge
        mat.SetFloat("_SpecularIntensity", 1.2f);

        // --- Outline (texture-color linked, moderate width) ---
        mat.SetFloat("_Outline", 1); // Toggle ON
        mat.SetFloat("_OutlineWidth", 0.08f);
        mat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 1f));
        mat.SetFloat("_OutlineTextureColor", 1); // Toggle ON → texture-linked color
        mat.SetFloat("_OutlineTexColorBlend", 0.8f);
        mat.SetFloat("_OutlineTexColorDarken", 0.5f);

        // --- Normal: slight Y-flatten for face softening ---
        mat.SetFloat("_NormalFlattenY", 0.15f);

        // --- Surface: matte-leaning (game characters are mostly matte) ---
        mat.SetFloat("_Glossiness", 0.15f);
        mat.SetFloat("_MatteEffect", 0.6f);

        // P-20: Group undo operations under a descriptive name
        Undo.SetCurrentGroupName(L("\u30D7\u30EA\u30BB\u30C3\u30C8\u9069\u7528: Game Character", "Apply Preset: Game Character"));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Apply the appropriate base style method for a given AutoSetupLook.
    /// Used by NataneAutoSetupHub to invoke the correct style preset.
    /// </summary>
    internal void ApplyBaseStyleForLook(Material mat, NataneToon.Editor.AutoSetupLook look)
    {
        switch (look)
        {
            case NataneToon.Editor.AutoSetupLook.Anime:
                ApplySharpAnimeStyle(mat);
                break;
            case NataneToon.Editor.AutoSetupLook.GameCharacter:
                ApplyGameCharacterStyle(mat);
                break;
            case NataneToon.Editor.AutoSetupLook.SemiRealistic:
                ApplyToonPbrHybridStyle(mat);
                break;
        }
    }

    /// <summary>
    /// 3.3 Draw property with range indicator - Visual feedback for parameter values
    /// </summary>
    private void DrawPropertyWithRangeInfo(string propertyName, string label, string recommendedRange = null)
    {
        MaterialProperty property = FindProperty(propertyName, properties, false);
        if (property == null) return;

        // Draw main property
        materialEditor.ShaderProperty(property, label);

        // Show recommended range
        if (recommendedRange != null)
        {
            GUIStyle miniStyle = new GUIStyle(EditorStyles.miniLabel);
            miniStyle.normal.textColor = new Color(0.5f, 0.8f, 1.0f);
            EditorGUILayout.LabelField(L("推奨: ", "Recommended: ") + recommendedRange, miniStyle);
        }

        // Draw visual bar for Range/Float properties
        if (property.type == MaterialProperty.PropType.Range || property.type == MaterialProperty.PropType.Float)
        {
            float value = property.floatValue;

            // For Range type properties
            if (property.type == MaterialProperty.PropType.Range)
            {
                float min = property.rangeLimits.x;
                float max = property.rangeLimits.y;
                float normalized = Mathf.InverseLerp(min, max, value);

                // Bar color (blue → orange gradient)
                Color barColor = Color.Lerp(
                    new Color(0.3f, 0.6f, 1.0f),
                    new Color(1.0f, 0.6f, 0.3f),
                    normalized
                );

                Rect barRect = EditorGUILayout.GetControlRect(false, 3);
                EditorGUI.DrawRect(
                    new Rect(barRect.x, barRect.y, barRect.width * normalized, barRect.height),
                    barColor
                );
            }
        }

        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// Draw help toggle button and help box
    /// Returns true if help is shown
    /// </summary>
    /// <summary>
    /// Draw help toggle with collapsible preview (3.4 UX Improvement)
    /// </summary>
    private bool DrawHelpToggle(string sectionKey, string helpText, MessageType messageType = MessageType.Info)
    {
        // Get help state for this section (default: collapsed)
        string prefsKey = NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, "Help_" + sectionKey);
        bool showHelp = EditorPrefs.GetBool(prefsKey, false); // Default: false (collapsed)

        // Temporarily reset indent so help buttons are always left-aligned
        int savedIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Help toggle button with icon (right-aligned)
        string icon = showHelp ? "▼" : "▶";
        string buttonLabel = showHelp ? L("ヘルプを非表示", "Hide Help") : L("ヘルプを表示", "Show Help");
        GUIContent helpContent = new GUIContent($"{icon} {buttonLabel}", L("クリックでヘルプを表示/非表示", "Click to show/hide help"));

        if (GUILayout.Button(helpContent, EditorStyles.miniButton, GUILayout.Width(100)))
        {
            showHelp = !showHelp;
            EditorPrefs.SetBool(prefsKey, showHelp);
        }

        EditorGUILayout.EndHorizontal();

        // Show full help box when expanded
        if (showHelp && !string.IsNullOrEmpty(helpText))
        {
            EditorGUILayout.HelpBox(helpText, messageType);
            EditorGUILayout.Space(3);
        }

        // Restore indent
        EditorGUI.indentLevel = savedIndent;

        return showHelp;
    }

    /// <summary>
    /// Draw category header with description
    /// </summary>
    private void DrawCategoryHeader(string title, string description)
    {
        EditorGUILayout.Space(SECTION_SPACING);

        // Background box
        EditorGUILayout.BeginVertical(CachedCategoryBoxStyle);

        // Title with icon
        EditorGUILayout.LabelField("▣ " + title, CachedCategoryTitleStyle);

        // Description
        if (!string.IsNullOrEmpty(description))
        {
            EditorGUILayout.LabelField(description, CachedCategoryDescStyle);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    // ===== FEATURE 4: Multi-material helpers =====

    private Material[] _cachedAllTargetMaterials;

    /// <summary>
    /// Get all target materials from the material editor.
    /// Cached per OnGUI call (invalidated on material change via _lastLoadedMaterialInstanceId).
    /// </summary>
    private Material[] GetAllTargetMaterials()
    {
        if (_cachedAllTargetMaterials != null)
            return _cachedAllTargetMaterials;

        if (materialEditor == null || materialEditor.targets == null || materialEditor.targets.Length == 0)
        {
            _cachedAllTargetMaterials = targetMaterial != null ? new Material[] { targetMaterial } : new Material[0];
        }
        else
        {
            var list = new List<Material>(materialEditor.targets.Length);
            foreach (var t in materialEditor.targets)
            {
                Material mat = t as Material;
                if (mat != null) list.Add(mat);
            }
            _cachedAllTargetMaterials = list.ToArray();
        }
        return _cachedAllTargetMaterials;
    }

    /// <summary>
    /// Feature 4: Propagate a MaterialProperty value to all non-primary target materials.
    /// Unity's built-in multi-material editing only works when all targets share the same shader.
    /// This method bridges the gap for cross-variant editing (Opaque/Cutout/Transparent).
    /// </summary>
    private void PropagatePropertyToOtherTargets(MaterialProperty property)
    {
        Material[] allTargets = GetAllTargetMaterials();
        if (allTargets.Length <= 1) return;

        string propName = property.name;
        foreach (Material mat in allTargets)
        {
            // Skip materials that Unity already handles (same shader as primary)
            if (mat == null || mat == targetMaterial) continue;
            if (mat.shader == targetMaterial.shader) continue; // same shader = already synced by Unity
            if (!mat.HasProperty(propName)) continue;

            Undo.RecordObject(mat, "Cross-variant property sync");
            switch (property.type)
            {
                case MaterialProperty.PropType.Float:
#if UNITY_2021_1_OR_NEWER
                case MaterialProperty.PropType.Int:
#endif
                    mat.SetFloat(propName, property.floatValue);
                    break;
                case MaterialProperty.PropType.Range:
                    mat.SetFloat(propName, property.floatValue);
                    break;
                case MaterialProperty.PropType.Color:
                    mat.SetColor(propName, property.colorValue);
                    break;
                case MaterialProperty.PropType.Vector:
                    mat.SetVector(propName, property.vectorValue);
                    break;
                case MaterialProperty.PropType.Texture:
                    mat.SetTexture(propName, property.textureValue);
                    mat.SetTextureOffset(propName, property.textureScaleAndOffset);
                    break;
            }
            EditorUtility.SetDirty(mat);
        }
    }

    /// <summary>
    /// Feature 4: Propagate a color value to all non-primary target materials.
    /// Used by DrawColorProperty which bypasses MaterialProperty for color field rendering.
    /// </summary>
    private void PropagateColorToOtherTargets(string propertyName, Color color)
    {
        Material[] allTargets = GetAllTargetMaterials();
        if (allTargets.Length <= 1) return;

        foreach (Material mat in allTargets)
        {
            if (mat == null || mat == targetMaterial) continue;
            if (mat.shader == targetMaterial.shader) continue;
            if (!mat.HasProperty(propertyName)) continue;

            Undo.RecordObject(mat, "Cross-variant color sync");
            mat.SetColor(propertyName, color);
            EditorUtility.SetDirty(mat);
        }
    }

    /// <summary>
    /// Check if all target materials use Natane shaders.
    /// </summary>
    private bool AreAllTargetsNataneShaders()
    {
        foreach (Material mat in GetAllTargetMaterials())
        {
            if (mat == null || mat.shader == null) return false;
            if (!NataneToon.Editor.NataneShaderCatalog.IsNataneShader(mat.shader.name)) return false;
        }
        return true;
    }

    /// <summary>
    /// Expand or collapse all sections
    /// </summary>
    private void ExpandAllSections(bool expand)
    {
        foreach (var key in foldoutPrefsKeys.Keys)
        {
            SetFoldout(key, expand);
        }

        // Force repaint
        if (materialEditor != null)
        {
            materialEditor.Repaint();
        }
    }

    /// <summary>
    /// Called when a new shader is assigned to a material
    /// </summary>
    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);

        // Validate keywords after shader assignment
        targetMaterial = material;
        _keywordValidationRequested = true;
        InvalidateInspectorCaches();
        SynchronizeKeywordsAndRefreshInspectorCaches();
    }

    /// <summary>
    /// Validate and fix shader keywords based on property values
    /// This ensures keywords are in sync with material properties even if not toggled manually.
    /// Uses shared keyword mappings from NataneShaderKeywordSynchronizer.
    /// </summary>
    private void ValidateAndFixKeywords()
    {
        if (targetMaterial == null) return;

        bool anyChanges = NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(targetMaterial);

        // Mark material as dirty if changes were made
        if (anyChanges)
        {
            EditorUtility.SetDirty(targetMaterial);
        }
    }

}
