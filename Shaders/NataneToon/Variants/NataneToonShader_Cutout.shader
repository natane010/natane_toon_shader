// ===== NataneToon Shader - Cutout Variant =====
// Render Type: TransparentCutout
// Queue: AlphaTest
// 特徴: アルファカットアウト。_Cutoff 閾値でクリッピング。
Shader "Natane/Toon Shader (Cutout)"
{
    Properties
    {
        // ===== Base Settings (基本設定) =====
        [Header(Main Texture)]
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Space(10)]
        [Toggle(_MAIN_TEX_ANIMATION)] _MainTexAnimation ("Main Tex Animation", Float) = 0
        _MainTexScrollSpeed ("Scroll Speed XY", Vector) = (0,0,0,0)
        _MainTexRotateSpeed ("Rotate Speed", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Color Preservation)]
        _AlbedoPreservation ("Texture Color Preservation", Range(0, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Overall Brightness", Range(0.5, 5.0)) = 1

        [Header(Final Color Blending)]
        _FinalHighlightBlend ("Highlight Compression Prevent White Blowout", Range(0, 1)) = 0
        _HighlightThreshold ("Highlight Threshold Start Point", Range(0, 1)) = 0.75
        _FinalShadowBlend ("Shadow Lift Prevent Black Crush", Range(0, 1)) = 0
        _ShadowThreshold ("Shadow Threshold Start Point", Range(0, 1)) = 0.25

        [Header(Surface Finish)]
        _Glossiness ("Glossiness Overall Gloss", Range(0, 1)) = 1
        _MatteEffect ("Matte Effect Reduce Gloss", Range(0, 1)) = 0

        [Header(Makeup Textures)]
        [Toggle(_2ND_TEXTURE)] _Use2ndTexture ("Enable 2nd Texture", Float) = 0
        _2ndTex ("Texture Makeup", 2D) = "white" {}
        _2ndTexHueShift ("Hue Shift", Range(-0.5, 0.5)) = 0
        _2ndTexSaturation ("Saturation", Range(0, 2)) = 1
        _2ndTexValue ("Brightness", Range(0, 2)) = 1
        _2ndTexIntensity ("2nd Tex Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Overlay,2,Screen,3)] _2ndTexBlendMode ("2nd Tex Blend Mode", Float) = 0
        [Toggle(_2ND_TEX_MASK)] _Use2ndTexMask ("Use 2nd Tex Mask", Float) = 0
        _2ndTexMask ("2nd Tex Mask", 2D) = "white" {}
        _2ndTexScrollSpeed ("2nd Tex Scroll Speed XY", Vector) = (0,0,0,0)
        _2ndTexRotateSpeed ("2nd Tex Rotate Speed", Float) = 0
        [Space(10)]
        [Toggle(_3RD_TEXTURE)] _Use3rdTexture ("Enable 3rd Texture", Float) = 0
        _3rdTex ("Texture Makeup", 2D) = "white" {}
        _3rdTexHueShift ("Hue Shift", Range(-0.5, 0.5)) = 0
        _3rdTexSaturation ("Saturation", Range(0, 2)) = 1
        _3rdTexValue ("Brightness", Range(0, 2)) = 1
        _3rdTexIntensity ("3rd Tex Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Overlay,2,Screen,3)] _3rdTexBlendMode ("3rd Tex Blend Mode", Float) = 0
        [Toggle(_3RD_TEX_MASK)] _Use3rdTexMask ("Use 3rd Tex Mask", Float) = 0
        _3rdTexMask ("3rd Tex Mask", 2D) = "white" {}
        _3rdTexScrollSpeed ("3rd Tex Scroll Speed XY", Vector) = (0,0,0,0)
        _3rdTexRotateSpeed ("3rd Tex Rotate Speed", Float) = 0
        [Space(10)]
        [Toggle(_4TH_TEXTURE)] _Use4thTexture ("Enable 4th Texture", Float) = 0
        _4thTex ("Texture Makeup", 2D) = "white" {}
        _4thTexHueShift ("Hue Shift", Range(-0.5, 0.5)) = 0
        _4thTexSaturation ("Saturation", Range(0, 2)) = 1
        _4thTexValue ("Brightness", Range(0, 2)) = 1
        _4thTexIntensity ("4th Tex Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Overlay,2,Screen,3)] _4thTexBlendMode ("4th Tex Blend Mode", Float) = 0
        [Toggle(_4TH_TEX_MASK)] _Use4thTexMask ("Use 4th Tex Mask", Float) = 0
        _4thTexMask ("4th Tex Mask", 2D) = "white" {}
        _4thTexScrollSpeed ("4th Tex Scroll Speed XY", Vector) = (0,0,0,0)
        _4thTexRotateSpeed ("4th Tex Rotate Speed", Float) = 0
        [Space(10)]
        [Toggle(_5TH_TEXTURE)] _Use5thTexture ("Enable 5th Texture", Float) = 0
        _5thTex ("Texture Makeup", 2D) = "white" {}
        _5thTexHueShift ("Hue Shift", Range(-0.5, 0.5)) = 0
        _5thTexSaturation ("Saturation", Range(0, 2)) = 1
        _5thTexValue ("Brightness", Range(0, 2)) = 1
        _5thTexIntensity ("5th Tex Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Overlay,2,Screen,3)] _5thTexBlendMode ("5th Tex Blend Mode", Float) = 0
        [Toggle(_5TH_TEX_MASK)] _Use5thTexMask ("Use 5th Tex Mask", Float) = 0
        _5thTexMask ("5th Tex Mask", 2D) = "white" {}
        _5thTexScrollSpeed ("5th Tex Scroll Speed XY", Vector) = (0,0,0,0)
        _5thTexRotateSpeed ("5th Tex Rotate Speed", Float) = 0

        // ===== Screen-Tone Overlay =====
        [Header(Screen Tone)]
        [Toggle(_SCREEN_TONE)] _ScreenTone ("Enable Screen-Tone", Float) = 0
        _ScreenToneColor ("Screen-Tone Color", Color) = (0,0,0,1)
        _ScreenToneMask ("Screen-Tone Mask", 2D) = "white" {}
        _ScreenToneScale ("Pattern Scale", Range(1, 200)) = 10
        _ScreenToneThreshold ("Dot Density", Range(0, 1)) = 0.5
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _ScreenToneBlendMode ("Blend Mode", Float) = 0
        _ScreenToneBlend ("Blend", Range(0, 1)) = 1
        _ScreenToneBlur ("Mask Blur", Range(0, 1)) = 0

        [Header(Halftone Shadow)]
        [Toggle(_HALFTONE_SHADOW)] _HalftoneShadow ("Enable Halftone Shadow", Float) = 0
        _HalftoneShadowColor ("Halftone Color", Color) = (0, 0, 0, 1)
        _HalftoneShadowScale ("Halftone Scale", Range(1, 200)) = 30
        _HalftoneShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _HalftoneShadowSoftness ("Softness", Range(0, 0.5)) = 0.1
        _HalftoneShadowIntensity ("Intensity", Range(0, 1)) = 0.5
        _HalftoneShadowBlend ("Blend", Range(0, 1)) = 1

        // ===== Gradient Base Color (グラデーションベースカラー) =====
        [Header(Gradient Base Color)]
        [Toggle(_GRADIENT_BASE_COLOR)] _GradientBaseColor ("Enable Gradient Base Color", Float) = 0
        _GradientTopColor ("Top Color", Color) = (1, 1, 1, 1)
        _GradientBottomColor ("Bottom Color", Color) = (0.5, 0.5, 0.5, 1)
        [Enum(X,0,Y,1,Z,2)] _GradientAxis ("Gradient Axis", Float) = 1
        [Enum(Local,0,World,1)] _GradientSpace ("Coordinate Space", Float) = 0
        _GradientStart ("Gradient Start", Float) = 0
        _GradientEnd ("Gradient End", Float) = 1
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _GradientBlendMode ("Blend Mode", Float) = 0
        _GradientBlend ("Blend", Range(0, 1)) = 1

        // ===== Shading (シェーディング) =====
        [Header(Shading)]
        [Enum(Toon,0,Gradient,1,StandardToon,2,PBRLike,3)] _ShadingMode ("Shading Mode", Float) = 0
        [Enum(Default,0,Skin,1,Hair,2,Eye,3,Cloth,4)] _SurfaceModel ("Surface Model", Float) = 0
        [Enum(Legacy,0,Toon,1,NPR,2,PBR,3,Hybrid,4)] _LookMode ("Look Mode", Float) = 0
        _ToonWeight ("Toon Weight", Range(0, 1)) = 1
        _NprWeight ("NPR Weight", Range(0, 1)) = 0
        _PbrWeight ("PBR Weight", Range(0, 1)) = 0
        [HideInInspector] _LilToonMigrated ("LilToon Migrated", Float) = 0
        [HideInInspector] _LilToonMigrationMode ("LilToon Migration Mode", Float) = 0
        [HideInInspector] _LilToonParityFlags ("LilToon Parity Flags", Float) = 0
        [HideInInspector] _ShadowMainStrength ("LilToon Shadow Main Strength", Range(0, 1)) = 0
        [HideInInspector] _Shadow2ndBlur ("LilToon Shadow 2nd Blur", Range(0, 1)) = 0.1
        [HideInInspector] _Shadow3rdBlur ("LilToon Shadow 3rd Blur", Range(0, 1)) = 0.1
        _ShadingGradientWidth ("Gradient Width", Range(0.001, 1)) = 0.5
        [Toggle(_USE_RAMP)] _UseRamp ("Use Ramp Texture", Float) = 0
        _RampTex ("Ramp Texture", 2D) = "white" {}
        _ShadowColor ("Shadow Color 1st", Color) = (0.5, 0.5, 0.5, 1)
        _ShadowHueShift ("Shadow Hue Shift", Range(-0.5, 0.5)) = 0
        _ShadowSaturation ("Shadow Saturation", Range(0, 2)) = 1
        [Toggle(_USE_MULTI_SHADOW)] _UseMultiShadow ("Use Multi-tone Shadow", Float) = 0
        _Shadow2ndColor ("Shadow Color 2nd", Color) = (0.35, 0.35, 0.35, 1)
        _Shadow2ndBorder ("2nd Shadow Border", Range(0, 1)) = 0.3
        _Shadow3rdColor ("Shadow Color 3rd", Color) = (0.2, 0.2, 0.2, 1)
        _Shadow3rdBorder ("3rd Shadow Border", Range(0, 1)) = 0.15
        _ShadowSteps ("Shadow Steps", Range(1, 10)) = 3
        _ShadowSharpness ("Shadow Sharpness", Range(0.001, 1)) = 0.1
        _StepBorderSmooth ("Step Border Smooth", Range(0, 1)) = 0
        _ShadowOffset ("Shadow Offset", Range(-1, 1)) = 0
        _WrapAmount ("Wrap Amount (Light Wraparound)", Range(0, 1)) = 0
        _LitSoftness ("Lit Area Softness Global Smoothstep", Range(0, 1)) = 0.1
        _ShadowBlend ("Shadow Blend Softness", Range(0, 1)) = 0.1
        [Space(10)]
        [Toggle(_VERTEX_COLOR_SHADOW)] _VertexColorShadow ("Vertex Color Shadow Threshold", Float) = 0
        _VCShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _VCShadowPush ("Shadow Push", Range(-1, 1)) = 0
        [Toggle(_SHADOW_RECEIVE_MASK)] _UseShadowReceiveMask ("Use Shadow Receive Mask", Float) = 0
        _ShadowReceiveMask ("Shadow Receive Mask", 2D) = "white" {}
        [Space(10)]
        [Toggle(_SDF_MAP)] _UseSDFMap ("Use SDF Shadow Map", Float) = 0
        _SDFMap ("SDF Shadow Map", 2D) = "white" {}
        _SDFIntensity ("SDF Intensity", Range(0, 1)) = 0.5
        _SDFSoftness ("SDF Softness", Range(0, 1)) = 0.1
        _SDFOffset ("SDF Offset", Range(-1, 1)) = 0
        [Toggle(_FACE_SDF_ROTATION)] _FaceSDFRotation ("Face SDF Rotation", Float) = 0
        _FaceForwardDirection ("Face Forward Direction", Vector) = (0,0,1,0)
        _FaceRightDirection ("Face Right Direction", Vector) = (1,0,0,0)
        [Space(10)]
        [Toggle(_SHADING_GRADE_MAP)] _UseGradeMap ("Use Shading Grade Map", Float) = 0
        _ShadingGradeMap ("Shading Grade Map", 2D) = "white" {}
        _ShadingGradeScale ("Shading Grade Scale", Range(-1, 1)) = 0
        [Toggle(_USE_AO)] _UseAO ("Use Ambient Occlusion", Float) = 0
        _AOMap ("AO Map", 2D) = "white" {}
        _AOIntensity ("AO Intensity", Range(0, 1)) = 1
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _AOBlendMode ("AO Blend Mode", Float) = 0
        _AOBlend ("AO Blend", Range(0, 1)) = 1
        _AOBlur ("AO Blur", Range(0, 1)) = 0
        _CavityMap ("Cavity Map", 2D) = "white" {}
        _CavityStrength ("Cavity Strength", Range(0, 1)) = 0
        _SpecularOcclusionStrength ("Specular Occlusion Strength", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_PROCEDURAL_AO)] _ProceduralAO ("Enable Procedural AO", Float) = 0
        _ProceduralAOHeightOffset ("AO Height Offset", Range(-2, 2)) = 0
        _ProceduralAOIntensity ("AO Intensity", Range(0, 1)) = 0.5
        _ProceduralAOSoftness ("AO Softness", Range(0.01, 2)) = 0.5
        [Toggle(_USE_DITHERING)] _UseDithering ("Use Dithering Shadow Edge Only", Float) = 0
        _DitheringScale ("Dithering Scale Pattern Size", Range(1, 100)) = 10
        _DitheringStrength ("Dithering Strength Boundary Softness", Range(0, 1)) = 0.5
        _DitheringBlend ("Dithering Blend", Range(0, 1)) = 1
        _DitheringBlur ("Dithering Blur", Range(0, 1)) = 0
        _DitherStabilize ("Dither Stabilize (Object-Relative)", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_SHADOW_COLOR_TEX)] _UseShadowColorTex ("Use Shadow Color Texture", Float) = 0
        _ShadowColorTex ("Shadow Color Texture", 2D) = "white" {}
        _ShadowColorTexStrength ("Shadow Color Tex Strength", Range(0, 1)) = 1

        [Header(Shadow Edge Noise)]
        [Toggle(_SHADOW_EDGE_NOISE)] _ShadowEdgeNoise ("Enable Shadow Edge Noise", Float) = 0
        _ShadowNoiseScale ("Noise Scale", Range(1, 100)) = 20
        _ShadowNoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.3
        _ShadowNoiseSpeed ("Noise Animation Speed", Range(0, 5)) = 0

        [Header(Cast Shadow Color)]
        [Toggle(_CAST_SHADOW_COLOR)] _CastShadowColorEnable ("Enable Cast Shadow Color", Float) = 0
        _CastShadowTint ("Cast Shadow Tint", Color) = (0.5, 0.4, 0.6, 1)
        _CastShadowIntensity ("Cast Shadow Intensity", Range(0, 1)) = 0.5

        [Header(Advanced Lighting)]
        [Toggle(_SOFT_LIGHTING_MODE)] _SoftLightingMode ("Soft Lighting Mode Global", Float) = 0
        _SoftLightingIntensity ("Soft Lighting Intensity", Range(0, 1)) = 0.5
        _LightIntensity ("Light Intensity Global", Range(0, 5)) = 1
        _IndirectLightIntensity ("Indirect Light Intensity", Range(0, 2)) = 1
        [Header(Environment Reflection Control)]
        _GIIntensity ("GI Intensity (環境反射強度)", Range(0, 1)) = 0.45
        _LightColorInfluence ("Light Color Influence", Range(0, 1)) = 1
        _ShadowReceive ("Shadow Receive", Range(0, 1)) = 1
        _ShadowSmoothing ("Shadow Map Smoothing", Range(0, 1)) = 0
        [Space(5)]
        [Toggle(_PCSS)] _UsePCSS ("Enable PCSS Soft Shadow", Float) = 0
        _PCSSLightSize ("PCSS Light Size", Range(0.01, 5.0)) = 1.0
        _PCSSSoftness ("PCSS Softness", Range(0.1, 10.0)) = 1.0
        _PCSSBlockerSearchRadius ("PCSS Blocker Search Radius", Range(1, 20)) = 8
        _PCSSMinFilterRadius ("PCSS Min Filter Radius", Range(0.5, 5.0)) = 1.0
        _PCSSMaxFilterRadius ("PCSS Max Filter Radius", Range(1, 30)) = 15.0
        [Enum(Low 8,8,Medium 16,16,High 32,32)] _PCSSSampleCount ("PCSS Sample Quality", Float) = 16
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _PCSSBlendMode ("PCSS Blend Mode", Float) = 0
        _PCSSBlend ("PCSS Blend", Range(0, 1)) = 1
        _PCSSBlur ("PCSS Blur", Range(0, 1)) = 0
        _ShadowMaxDarkness ("Shadow Max Darkness", Range(0, 1)) = 0
        _LightColorMin ("Light Color Min (ライト色下限)", Range(0, 1)) = 0
        _LightColorMax ("Light Color Max (ライト色上限)", Range(0, 10)) = 0.9
        _MonochromeLighting ("Monochrome Lighting (モノクロライト)", Range(0, 1)) = 0
        _LightMinInfluence ("Light Min Influence", Range(0, 1)) = 0
        _LightMaxInfluence ("Light Max Influence", Range(1, 5)) = 2
        [Enum(Legacy,0,HuePreserving,1)] _DirectLightClampMode ("Direct Light Clamp Mode", Float) = 0
        _LightBlend ("Light Blend Softness", Range(0, 1)) = 0
        _HighlightSoftness ("Highlight Softness", Range(0, 1)) = 0
        _BacklightIntensity ("Backlight Intensity", Range(0, 2)) = 0
        _BacklightColor ("Backlight Color", Color) = (1, 1, 1, 1)
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _BacklightBlendMode ("Backlight Blend Mode", Float) = 0
        _BacklightBlend ("Backlight Blend", Range(0, 1)) = 1
        _BacklightBlur ("Backlight Blur", Range(0, 1)) = 0
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0, 1)) = 0.35
        _IndirectLightMinColor ("Indirect Light Min Color (間接光最低色)", Color) = (0.1, 0.1, 0.1, 1)
        _ShadowEnvStrength ("Shadow Env Strength (影への環境色反映)", Range(0, 1)) = 0
        [Toggle(_PIXEL_VERTEX_LIGHTS)] _UsePixelVertexLights("Pixel Vertex Lights", Float) = 0

        [Header(Light Direction Snap)]
        [Toggle(_LIGHT_SNAP)] _LightSnap ("Enable Light Snap", Float) = 0
        _LightSnapAngle ("Snap Angle (degrees)", Range(5, 90)) = 45
        _LightSnapSmooth ("Snap Smoothness", Range(0, 1)) = 0.1

        [Header(VRC Light Volumes)]
        [Toggle(_USE_LIGHT_VOLUME)] _UseLightVolume ("Use Light Volume", Float) = 1
        _LightVolumeIntensity ("Light Volume Intensity", Range(0, 1)) = 1
        [Enum(Add,0,Multiply,1,Replace,2,Natural,3)] _LightVolumeBlendMode ("Light Volume Blend Mode", Float) = 3
        [Toggle(_LIGHT_VOLUME_SPECULAR)] _LightVolumeSpecular ("Light Volume Specular", Float) = 0
        _LightVolumeBlend ("Light Volume Blend", Range(0, 1)) = 1

        // ===== Effects (エフェクト) =====
        [Header(Specular)]
        [Toggle(_SPECULAR)] _Specular ("Enable Specular", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularSize ("Specular Size", Range(0, 1)) = 0.1
        _SpecularSoftness ("Specular Softness", Range(0.001, 1)) = 0.05
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 0.8
        [Toggle(_SPECULAR_MASK)] _UseSpecularMask ("Use Specular Mask", Float) = 0
        _SpecularMask ("Specular Mask", 2D) = "white" {}
        _SpecularMaskScrollSpeed ("Specular Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _SpecularMaskRotateSpeed ("Specular Mask Rotate Speed", Float) = 0
        [Toggle(_SPECULAR_AA)] _SpecularAA ("Enable Specular Anti-Aliasing", Float) = 0
        _SpecularAAStrength ("Specular AA Strength", Range(0, 2)) = 1
        [Toggle(_SPECULAR_DITHER)] _SpecularDither ("Specular Dither", Float) = 0
        _SpecularDitherScale ("Specular Dither Scale", Range(0.5, 8.0)) = 1.0
        _SpecularDitherStrength ("Specular Dither Strength", Range(0, 1)) = 1.0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _SpecularBlendMode ("Specular Blend Mode", Float) = 0
        _SpecularBlend ("Specular Blend", Range(0, 1)) = 1
        _SpecularBlur ("Specular Blur", Range(0, 1)) = 0
        [Header(Skin Dual Lobe Specular)]
        _SkinSpecPrimaryStrength ("Skin Primary Spec Strength", Range(0, 2)) = 1
        _SkinSpecSecondaryStrength ("Skin Secondary Spec Strength", Range(0, 2)) = 0
        _SkinSpecSecondarySmoothness ("Skin Secondary Smoothness", Range(0, 1)) = 0.85
        _SkinSpecSecondaryColor ("Skin Secondary Spec Color", Color) = (1,1,1,1)
        _SkinSpecFresnelPower ("Skin Spec Fresnel Power", Range(0.1, 8)) = 3
        _SkinSpecMask ("Skin Spec Mask", 2D) = "white" {}

        [Header(Hair Specular Kajiya Kay)]
        [Toggle(_HAIR_SPECULAR)] _HairSpecular ("Enable Hair Specular", Float) = 0
        _HairSpecColor1 ("Primary Spec Color", Color) = (1,1,1,1)
        _HairSpecShift1 ("Primary Tangent Shift", Range(-1, 1)) = 0.1
        _HairSpecWidth1 ("Primary Spec Width", Range(1, 256)) = 64
        _HairSpecColor2 ("Secondary Spec Color", Color) = (0.5,0.5,0.5,1)
        _HairSpecShift2 ("Secondary Tangent Shift", Range(-1, 1)) = -0.1
        _HairSpecWidth2 ("Secondary Spec Width", Range(1, 256)) = 32
        _HairSpecIntensity ("Hair Spec Intensity", Range(0, 2)) = 1
        [Toggle(_HAIR_SPEC_MASK)] _UseHairSpecMask ("Use Hair Spec Mask", Float) = 0
        _HairSpecMask ("Hair Spec Mask", 2D) = "white" {}
        [Toggle(_HAIR_SPEC_SHIFT_TEX)] _UseHairSpecShiftTex ("Use Shift Texture", Float) = 0
        _HairSpecShiftTex ("Shift Texture", 2D) = "grey" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _HairSpecBlendMode ("Hair Spec Blend Mode", Float) = 0
        _HairSpecBlend ("Hair Spec Blend", Range(0, 1)) = 1
        [Header(Hair Direction Transmission)]
        _HairStrandDirectionMap ("Hair Strand Direction Map", 2D) = "gray" {}
        _HairStrandDirectionStrength ("Hair Strand Direction Strength", Range(0, 1)) = 0
        _HairTransmissionColor ("Hair Transmission Color", Color) = (1.0, 0.65, 0.45, 1)
        _HairTransmissionStrength ("Hair Transmission Strength", Range(0, 2)) = 0
        _HairTransmissionPower ("Hair Transmission Power", Range(1, 128)) = 24
        _HairTransmissionMask ("Hair Transmission Mask", 2D) = "white" {}

        [Header(Angel Ring)]
        [Toggle(_ANGEL_RING)] _AngelRing ("Enable Angel Ring", Float) = 0
        _AngelRingTex ("Angel Ring Texture", 2D) = "white" {}
        _AngelRingColor ("Angel Ring Color", Color) = (1,1,1,0.5)
        _AngelRingOffset ("Angel Ring Offset", Range(-0.5, 0.5)) = 0.0
        _AngelRingWidth ("Angel Ring Width", Range(0.01, 1.0)) = 0.3
        _AngelRingIntensity ("Angel Ring Intensity", Range(0, 3)) = 1.0
        _AngelRingBlend ("Angel Ring Blend", Range(0, 1)) = 1
        [Enum(Add,0,Multiply,1,Screen,2,Overlay,3)] _AngelRingBlendMode ("Angel Ring Blend Mode", Float) = 0

        [Header(Rim Light)]
        [Toggle(_RIM_LIGHT)] _RimLight ("Enable Rim Light", Float) = 0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.1, 10)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 5)) = 1
        _RimSpread ("Rim Spread (Glow)", Range(0, 1)) = 0
        [Toggle(_RIM_MASK)] _UseRimMask ("Use Rim Mask", Float) = 0
        _RimMask ("Rim Mask", 2D) = "white" {}
        _RimMaskScrollSpeed ("Rim Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _RimMaskRotateSpeed ("Rim Mask Rotate Speed", Float) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _RimBlendMode ("Rim Blend Mode", Float) = 0
        _RimBlend ("Rim Blend", Range(0, 1)) = 1
        _RimBlur ("Rim Blur", Range(0, 1)) = 0
        [Toggle(_RIM_LIGHT_2)] _RimLight2 ("Enable Rim Light 2", Float) = 0
        _RimColor2 ("Rim Color 2", Color) = (0.5,0.8,1,1)
        _RimPower2 ("Rim Power 2", Range(0.1, 10)) = 5
        _RimIntensity2 ("Rim Intensity 2", Range(0, 5)) = 0.5
        _RimSpread2 ("Rim Spread 2 (Glow)", Range(0, 1)) = 0
        [Toggle(_RIM_MASK_2)] _UseRimMask2 ("Use Rim Mask 2", Float) = 0
        _RimMask2 ("Rim Mask 2", 2D) = "white" {}
        _RimMask2ScrollSpeed ("Rim Mask 2 Scroll Speed XY", Vector) = (0,0,0,0)
        _RimMask2RotateSpeed ("Rim Mask 2 Rotate Speed", Float) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _RimBlendMode2 ("Rim 2 Blend Mode", Float) = 0
        _RimBlend2 ("Rim 2 Blend", Range(0, 1)) = 1
        _Rim2Blur ("Rim 2 Blur", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_OFFSET_RIM_LIGHT)] _OffsetRimLight ("Enable Offset Rim Light", Float) = 0
        _OffsetRimColor ("Offset Rim Color", Color) = (0.8,0.9,1,1)
        _OffsetRimPower ("Offset Rim Power", Range(0.01, 10)) = 3
        _OffsetRimIntensity ("Offset Rim Intensity", Range(0, 10)) = 1
        _OffsetRimOffsetX ("Offset Rim X", Range(-5, 5)) = 0.3
        _OffsetRimOffsetY ("Offset Rim Y", Range(-5, 5)) = 0.1
        [Toggle] _OffsetRimUseLightDir ("Use Light Direction", Float) = 0
        _OffsetRimLightDirStrength ("Light Dir Strength", Range(0, 1)) = 0.5
        _OffsetRimSharpness ("Offset Rim Sharpness", Range(0, 1)) = 0.5
        _OffsetRimShadowMask ("Shadow Mask", Range(0, 1)) = 0.5
        _OffsetRimMask ("Offset Rim Mask", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _OffsetRimBlendMode ("Offset Rim Blend Mode", Float) = 0
        _OffsetRimBlend ("Offset Rim Blend", Range(0, 1)) = 1
        _OffsetRimBlur ("Offset Rim Blur", Range(0, 1)) = 0

        [Header(Sheen Fabric Luster)]
        [Toggle(_SHEEN)] _Sheen ("Enable Sheen", Float) = 0
        _SheenColor ("Sheen Color", Color) = (1, 1, 0.9, 1)
        _SheenIntensity ("Sheen Intensity", Range(0, 3)) = 1.0
        _SheenPower ("Sheen Power", Range(0.1, 10)) = 2.0
        _SheenMask ("Sheen Mask", 2D) = "white" {}
        _SheenBlend ("Sheen Blend", Range(0, 1)) = 1
        [Enum(Add,0,Multiply,1,Screen,2,Overlay,3)] _SheenBlendMode ("Sheen Blend Mode", Float) = 2

        [Space(10)]
        [Toggle(_RIM_DIRECTION_CONTROL)] _RimDirectionControl ("Rim Direction Control", Float) = 0
        _RimLightDirection ("Rim Light Direction", Vector) = (0,1,0,0)
        _RimDirectionRange ("Direction Range", Range(0, 1)) = 0.5
        _RimDirStrength ("Light Direction Strength", Range(0, 1)) = 0
        _RimShadowMask ("Shadow Mask", Range(0, 1)) = 0

        [Header(Subsurface Scattering)]
        [Toggle(_SSS)] _SSS ("Enable SSS", Float) = 0
        _SSSColor ("SSS Color", Color) = (1, 0.5, 0.5, 1)
        _SSSIntensity ("SSS Intensity", Range(0, 5)) = 1.5
        _SSSPower ("SSS Power", Range(0.1, 10)) = 3
        _SSSDistortion ("SSS Distortion", Range(0, 1)) = 0.5
        [Toggle(_THICKNESS_MAP)] _UseThicknessMap ("Use Thickness Map", Float) = 0
        _ThicknessMap ("Thickness Map", 2D) = "white" {}
        _ThicknessScale ("Thickness Scale", Range(0, 1)) = 0.2
        _TransmissionMask ("Transmission Mask", 2D) = "white" {}
        _TransmissionStrength ("Transmission Strength", Range(0, 1)) = 0
        [Toggle(_SSS_MASK)] _UseSSS_Mask ("Use SSS Mask", Float) = 0
        _SSSMask ("SSS Mask", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _SSSBlendMode ("SSS Blend Mode", Float) = 0
        _SSSBlend ("SSS Blend", Range(0, 1)) = 1
        _SSSBlur ("SSS Blur", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_SSS_LUT)] _SSSLUT ("Use SSS LUT", Float) = 0
        _SSSLUTTex ("SSS LUT Texture", 2D) = "white" {}
        _SSSLUTScale ("LUT Scale", Range(0, 2)) = 1.0

        [Header(MatCap)]
        [Toggle(_MATCAP)] _MatCap ("Enable MatCap", Float) = 0
        _MatCapTex ("MatCap Texture", 2D) = "black" {}
        _MatCapIntensity ("MatCap Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Replace,2)] _MatCapBlendMode ("MatCap Blend Mode", Float) = 0
        [Toggle(_MATCAP_MASK)] _UseMatCapMask ("Use MatCap Mask", Float) = 0
        _MatCapMask ("MatCap Mask", 2D) = "white" {}
        _MatCapBlend ("MatCap Blend", Range(0, 1)) = 1
        _MatCapBlur ("MatCap Blur", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_MATCAP_2)] _MatCap2 ("Enable MatCap 2", Float) = 0
        _MatCapTex2 ("MatCap Texture 2", 2D) = "black" {}
        _MatCapIntensity2 ("MatCap 2 Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Replace,2)] _MatCapBlendMode2 ("MatCap 2 Blend Mode", Float) = 0
        [Toggle(_MATCAP_MASK_2)] _UseMatCapMask2 ("Use MatCap 2 Mask", Float) = 0
        _MatCapMask2 ("MatCap 2 Mask", 2D) = "white" {}
        _MatCapBlend2 ("MatCap 2 Blend", Range(0, 1)) = 1
        _MatCap2Blur ("MatCap 2 Blur", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_MATCAP_3)] _MatCap3 ("Enable MatCap 3", Float) = 0
        _MatCapTex3 ("MatCap Texture 3", 2D) = "black" {}
        _MatCapIntensity3 ("MatCap 3 Intensity", Range(0, 2)) = 1
        [Enum(Add,0,Multiply,1,Replace,2)] _MatCapBlendMode3 ("MatCap 3 Blend Mode", Float) = 0
        [Toggle(_MATCAP_MASK_3)] _UseMatCapMask3 ("Use MatCap 3 Mask", Float) = 0
        _MatCapMask3 ("MatCap 3 Mask", 2D) = "white" {}
        _MatCapBlend3 ("MatCap 3 Blend", Range(0, 1)) = 1
        _MatCap3Blur ("MatCap 3 Blur", Range(0, 1)) = 0

        [Header(Procedural MatCap)]
        [Toggle(_PROCEDURAL_MATCAP)] _ProceduralMatCap ("Enable Procedural MatCap", Float) = 0
        _ProcMatCapColor ("Procedural MatCap Color", Color) = (0.8, 0.85, 1.0, 1)
        _ProcMatCapPower ("Procedural MatCap Power", Range(0.5, 10)) = 2.0
        _ProcMatCapIntensity ("Procedural MatCap Intensity", Range(0, 3)) = 1.0
        _ProcMatCapFresnelPower ("Procedural MatCap Fresnel", Range(0.1, 10)) = 3.0
        _ProcMatCapBlend ("Procedural MatCap Blend", Range(0, 1)) = 1
        [Enum(Add,0,Multiply,1,Screen,2,Overlay,3)] _ProcMatCapBlendMode ("Procedural MatCap Blend Mode", Float) = 0

        [Header(Glitter)]
        [Toggle(_GLITTER)] _Glitter ("Enable Glitter", Float) = 0
        _GlitterColor ("Glitter Color", Color) = (1,1,1,1)
        _GlitterSize ("Glitter Size", Range(0, 1)) = 0.1
        _GlitterDensity ("Glitter Density", Range(0, 1)) = 0.5
        _GlitterSpeed ("Glitter Speed", Float) = 1
        _GlitterIntensity ("Glitter Intensity", Range(0, 2)) = 1
        [Toggle(_GLITTER_MASK)] _UseGlitterMask ("Use Glitter Mask", Float) = 0
        _GlitterMask ("Glitter Mask", 2D) = "white" {}
        _GlitterMaskScrollSpeed ("Glitter Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _GlitterMaskRotateSpeed ("Glitter Mask Rotate Speed", Float) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _GlitterBlendMode ("Glitter Blend Mode", Float) = 0
        _GlitterBlend ("Glitter Blend", Range(0, 1)) = 1
        _GlitterBlur ("Glitter Blur", Range(0, 1)) = 0
        [Toggle(_GLINTS_ADVANCED)] _GlintsAdvanced ("Enable Advanced Glints", Float) = 0
        _GlintsSharpness ("Glints Sharpness", Range(8, 512)) = 128
        _GlintsTemporal ("Glints Temporal Speed", Range(0, 4)) = 1
        _GlintsNormalJitter ("Glints Normal Jitter", Range(0, 1)) = 0.35
        // ===== Outline (アウトライン) =====
        [Header(Outline)]
        [Toggle(_OUTLINE)] _Outline ("Enable Outline", Float) = 0
        [Enum(Inverted Hull,0,Back Face,1)] _OutlineMode ("Outline Mode", Float) = 0
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        _OutlineDistCompMax ("Outline Distance Compensation Max", Range(0, 10)) = 3.0
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        [Toggle(_OUTLINE_MASK)] _UseOutlineMask ("Use Outline Mask", Float) = 0
        _OutlineMask ("Outline Mask", 2D) = "white" {}
        [Toggle(_OUTLINE_WIDTH_MAP)] _UseOutlineWidthMap ("Use Outline Width Map", Float) = 0
        _OutlineWidthMap ("Outline Width Map", 2D) = "white" {}
        [Space(10)]
        [Toggle(_OUTLINE_MULTI_COLOR)] _OutlineMultiColor ("Multi-Color Outline", Float) = 0
        _OutlineColor2 ("Outline Color 2", Color) = (0.5,0,0,1)
        _OutlineColorMix ("Color Mix", Range(0, 1)) = 0.5
        [Space(10)]
        [Toggle(_OUTLINE_TEXTURE_COLOR)] _OutlineTextureColor ("Texture-linked Color", Float) = 0
        _OutlineTexColorBlend ("Tex Color Blend", Range(0, 1)) = 0.8
        _OutlineTexColorDarken ("Tex Color Darken", Range(0, 1)) = 0.5
        _OutlineTexColorHueShift ("Outline Hue Shift", Range(-0.5, 0.5)) = 0
        _OutlineTexColorSaturation ("Outline Saturation", Range(0, 2)) = 1.0
        [Space(10)]
        [Toggle(_SMOOTH_NORMAL)] _SmoothNormal ("Smooth Normal", Float) = 0
        [Enum(Vertex Color ObjectSpace,0,Vertex Color TangentSpace,1,Baked Normal Texture,2)] _SmoothNormalMode ("Smooth Normal Mode", Float) = 0
        _SmoothNormalTex ("Smooth Normal Texture", 2D) = "bump" {}
        _SmoothNormalShadingBlend ("Smooth Normal Shading Blend", Range(0, 1)) = 0
        [Header(Outline Corner Fix)]
        _OutlineCornerSmooth ("Corner Smooth Fallback", Range(0, 1)) = 0
        _OutlineEdgeCompensation ("Edge Width Compensation", Range(0, 1)) = 0

        [Header(Emission)]
        [Toggle(_EMISSION)] _Emission ("Enable Emission", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionGlow ("Emission Glow (Bloom)", Range(0, 1)) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _EmissionBlendMode ("Emission Blend Mode", Float) = 0
        _EmissionBlend ("Emission Blend", Range(0, 1)) = 1
        _EmissionBlur ("Emission Blur", Range(0, 1)) = 0
        [Toggle(_EMISSION_SCROLL)] _EmissionScroll ("Emission Scroll", Float) = 0
        _EmissionScrollSpeed ("Emission Scroll Speed", Float) = 1
        _EmissionScrollSpeedY ("Emission Scroll Speed Y", Float) = 0
        _EmissionRotateSpeed ("Emission Rotate Speed", Float) = 0
        [Toggle(_EMISSION_PULSE)] _EmissionPulse ("Emission Pulse", Float) = 0
        _EmissionPulseSpeed ("Emission Pulse Speed", Float) = 1
        _EmissionPulseAmplitude ("Emission Pulse Amplitude", Range(0, 1)) = 0.5
        [Toggle(_EMISSION_MASK)] _UseEmissionMask ("Use Emission Mask", Float) = 0
        _EmissionMask ("Emission Mask", 2D) = "white" {}
        _EmissionMaskScrollSpeed ("Emission Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _EmissionMaskRotateSpeed ("Emission Mask Rotate Speed", Float) = 0

        [Header(Virtual Expression)]
        [Toggle(_DISSOLVE)] _Dissolve ("Enable Dissolve", Float) = 0
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveTex ("Dissolve Texture (Noise)", 2D) = "white" {}
        _DissolveTexScrollSpeed ("Dissolve Tex Scroll Speed XY", Vector) = (0,0,0,0)
        _DissolveTexRotateSpeed ("Dissolve Tex Rotate Speed", Float) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.5)) = 0.1
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 0.5, 0, 1)
        _DissolveEdgeIntensity ("Dissolve Edge Intensity", Range(0, 10)) = 2
        [Toggle(_DISSOLVE_MASK)] _UseDissolveMask ("Use Dissolve Mask", Float) = 0
        _DissolveMask ("Dissolve Mask", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _DissolveBlendMode ("Dissolve Blend Mode", Float) = 0
        _DissolveBlend ("Dissolve Blend", Range(0, 1)) = 1
        _DissolveBlur ("Dissolve Blur", Range(0, 1)) = 0
        [Enum(UV,0,World,1,Local,2)] _DissolveCoordMode ("Dissolve Coordinate Mode", Float) = 0
        [Enum(X,0,Y,1,Z,2)] _DissolveWorldAxis ("Dissolve Axis", Float) = 1
        _DissolveWorldMin ("World Min", Float) = 0
        _DissolveWorldMax ("World Max", Float) = 1
        _DissolveNoiseBlend ("Noise Texture Blend", Range(0, 1)) = 0
        [Space(10)]
        [Toggle(_ALPHA_MASK)] _UseAlphaMask ("Use Alpha Mask", Float) = 0
        _AlphaMask ("Alpha Mask", 2D) = "white" {}
        [Space(10)]
        [Toggle(_HUE_SHIFT)] _HueShiftEnable ("Enable Hue Shift", Float) = 0
        _HueShift ("Hue Shift", Range(0, 1)) = 0
        _HueShiftBlend ("Hue Shift Blend", Range(0, 1)) = 1
        _HueShiftBlur ("Hue Shift Blur", Range(0, 1)) = 0

        // ===== Normal Map (法線マップ) =====
        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        _MicroNormalMap ("Micro Normal Map", 2D) = "bump" {}
        _MicroNormalScale ("Micro Normal Scale", Range(0, 2)) = 0.5
        _MicroNormalTiling ("Micro Normal Tiling", Range(1, 64)) = 8
        _MicroNormalStrength ("Micro Normal Strength", Range(0, 1)) = 0
        _BumpMapScrollSpeed ("Normal Map Scroll Speed XY", Vector) = (0,0,0,0)
        _BumpMapRotateSpeed ("Normal Map Rotate Speed", Float) = 0
        [Space(10)]
        [Toggle(_NORMAL_WARP)] _NormalWarp ("Enable Normal Warping", Float) = 0
        _NormalFlattenY ("Normal Flatten Y", Range(0, 1)) = 0
        _NormalRoundness ("Normal Roundness (Spherical Blend)", Range(0, 1)) = 0

        [Header(Cubemap Reflection)]
        [Toggle(_REFLECTION)] _Reflection ("Enable Reflection", Float) = 0
        _ReflectionCube ("Reflection Cubemap", CUBE) = "black" {}
        _ReflectionColor ("Reflection Color", Color) = (1, 1, 1, 1)
        _ReflectionIntensity ("Reflection Intensity", Range(0, 2)) = 1
        _Smoothness ("Smoothness Glossiness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _FresnelPower ("Fresnel Power", Range(0, 10)) = 5
        _FresnelSoftness ("Fresnel Softness", Range(0, 1)) = 0
        _ReflectionBlendMode ("Reflection Blend Mode", Range(0, 1)) = 0
        [Toggle(_REFLECTION_MASK)] _UseReflectionMask ("Use Reflection Mask", Float) = 0
        _ReflectionMask ("Reflection Mask", 2D) = "white" {}
        _ReflectionBlend ("Reflection Blend", Range(0, 1)) = 1
        [Space(10)]
        _ClearCoatIntensity ("Clear Coat Intensity", Range(0, 1)) = 0
        _ClearCoatSmoothness ("Clear Coat Smoothness", Range(0, 1)) = 0.9
        _ClearCoatMask ("Clear Coat Mask", 2D) = "white" {}
        _ClearCoatNormalMap ("Clear Coat Normal Map", 2D) = "bump" {}
        _ClearCoatNormalScale ("Clear Coat Normal Scale", Range(0, 2)) = 1
        _ClearCoatFresnelPower ("Clear Coat Fresnel Power", Range(0.1, 10)) = 5

        [Header(Fake Environment Reflection)]
        [Toggle(_FAKE_REFLECTION)] _FakeReflection ("Enable Fake Reflection", Float) = 0
        _FakeReflSkyColor ("Sky Color", Color) = (0.5, 0.7, 1.0, 1)
        _FakeReflGroundColor ("Ground Color", Color) = (0.3, 0.25, 0.2, 1)
        _FakeReflIntensity ("Intensity", Range(0, 3)) = 1.0
        _FakeReflFresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0
        _FakeReflSmoothness ("Smoothness", Range(0, 1)) = 0.5
        _FakeReflBlend ("Blend", Range(0, 1)) = 1
        [Enum(Add,0,Multiply,1,Screen,2,Overlay,3)] _FakeReflBlendMode ("Blend Mode", Float) = 0

        [Header(Iridescence)]
        [Toggle(_IRIDESCENCE)] _Iridescence ("Enable Iridescence", Float) = 0
        _IridescenceColor ("Iridescence Color", Color) = (1, 1, 1, 1)
        _IridescenceIntensity ("Intensity", Range(0, 2)) = 0.5
        _IridescenceHueShift ("Hue Shift", Range(0, 1)) = 0.5
        _IridescenceSize ("Size (Frequency)", Range(0, 10)) = 1
        [Toggle(_IRIDESCENCE_MASK)] _UseIridescenceMask ("Use Iridescence Mask", Float) = 0
        _IridescenceMask ("Iridescence Mask", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _IridescenceBlendMode ("Iridescence Blend Mode", Float) = 0
        _IridescenceBlend ("Iridescence Blend", Range(0, 1)) = 1
        _IridescenceBlur ("Iridescence Blur", Range(0, 1)) = 0

        [Header(Environmental Rim)]
        [Toggle(_ENV_RIM)] _EnvRim ("Enable Environmental Rim", Float) = 0
        _EnvRimCube ("Environment Cubemap", CUBE) = "black" {}
        _EnvRimColor ("Env Rim Color", Color) = (1, 1, 1, 1)
        _EnvRimPower ("Env Rim Power", Range(0.1, 10)) = 3
        _EnvRimIntensity ("Env Rim Intensity", Range(0, 5)) = 1
        [Toggle(_ENV_RIM_MASK)] _UseEnvRimMask ("Use Env Rim Mask", Float) = 0
        _EnvRimMask ("Env Rim Mask", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _EnvRimBlendMode ("Env Rim Blend Mode", Float) = 0
        _EnvRimBlend ("Env Rim Blend", Range(0, 1)) = 1
        _EnvRimBlur ("Env Rim Blur", Range(0, 1)) = 0

        [Header(Parallax Mapping WARNING Performance Heavy in VR)]
        [Toggle(_PARALLAX)] _Parallax ("Enable Parallax Max 64 Samples", Float) = 0
        _ParallaxMap ("Height Map", 2D) = "grey" {}
        _ParallaxScale ("Parallax Scale Distortion Strength", Range(0, 0.1)) = 0.02
        _ParallaxMinSamples ("Min Samples Flat View", Range(4, 16)) = 4
        _ParallaxMaxSamples ("Max Samples Steep View", Range(16, 64)) = 32

        [Space(10)]
        [Toggle(_EYE_PARALLAX)] _EyeParallax ("Enable Eye Parallax", Float) = 0
        _EyeParallaxDepth ("Eye Depth", Range(0, 0.5)) = 0.1

        [Header(Vertex Animation Texture Houdini VAT)]
        [Toggle(_VAT)] _VAT ("Enable VAT Animation", Float) = 0
        _VATPositionMap ("VAT Position Map", 2D) = "black" {}
        [Toggle(_VAT_NORMAL)] _VATNormal ("Use VAT Normal Map", Float) = 0
        _VATNormalMap ("VAT Normal Map", 2D) = "black" {}
        _VATNumOfFrames ("Number of Frames", Float) = 24
        _VATSpeed ("Animation Speed", Float) = 1
        _VATIntensity ("Animation Intensity", Range(0, 2)) = 1
        _VATPositionMin ("Position Min Value", Float) = -1
        _VATPositionMax ("Position Max Value", Float) = 1
        _VATNormalMin ("Normal Min Value", Float) = -1
        _VATNormalMax ("Normal Max Value", Float) = 1
        _VATPadding ("VAT Padding", Range(0, 1)) = 0
        [Space(10)]
        [Enum(Absolute,0,Offset,1)] _VATPackingMode ("VAT Packing Mode", Float) = 1

        [Header(AudioLink VRChat Club Events)]
        [Toggle(_AUDIOLINK)] _AudioLink ("Enable AudioLink", Float) = 0
        [Toggle(_AUDIOLINK_EMISSION)] _AudioLinkEmission ("AudioLink Emission", Float) = 0
        [Enum(Bass,0,Low Mid,1,High Mid,2,Treble,3)] _AudioLinkEmissionBand ("Emission Band", Float) = 0
        _AudioLinkEmissionIntensity ("Emission Intensity", Range(0, 5)) = 1
        [Toggle(_AUDIOLINK_RIM)] _AudioLinkRim ("AudioLink Rim Light", Float) = 0
        [Enum(Bass,0,Low Mid,1,High Mid,2,Treble,3)] _AudioLinkRimBand ("Rim Band", Float) = 0
        _AudioLinkRimIntensity ("Rim Intensity", Range(0, 5)) = 1
        [Toggle(_AUDIOLINK_HUE_SHIFT)] _AudioLinkHueShift ("AudioLink Hue Shift", Float) = 0
        [Enum(Bass,0,Low Mid,1,High Mid,2,Treble,3)] _AudioLinkHueBand ("Hue Band", Float) = 0
        _AudioLinkHueShiftIntensity ("Hue Shift Intensity", Range(0, 1)) = 0.5
        [Toggle(_AUDIOLINK_DISSOLVE)] _AudioLinkDissolve ("AudioLink Dissolve", Float) = 0
        [Enum(Bass,0,Low Mid,1,High Mid,2,Treble,3)] _AudioLinkDissolveBand ("Dissolve Band", Float) = 0
        _AudioLinkDissolveIntensity ("Dissolve Intensity", Range(0, 1)) = 0.5
        [Toggle(_AUDIOLINK_OUTLINE)] _AudioLinkOutline ("AudioLink Outline", Float) = 0
        [Enum(Bass,0,Low Mid,1,High Mid,2,Treble,3)] _AudioLinkOutlineBand ("Outline Band", Float) = 0
        _AudioLinkOutlineIntensity ("Outline Intensity", Range(0, 1)) = 0.5
        [Toggle(_AUDIOLINK_CHRONOTENSITY)] _AudioLinkChronotensity ("Use Chronotensity", Float) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _AudioLinkBlendMode ("AudioLink Blend Mode", Float) = 0
        _AudioLinkBlend ("AudioLink Blend", Range(0, 1)) = 1

        // ===== Height Fade (高さフェード) =====
        [Header(Height Fade)]
        [Toggle(_HEIGHT_FADE)] _HeightFade ("Enable Height Fade", Float) = 0
        _HeightFadeStart ("Fade Start Height", Float) = 0
        _HeightFadeEnd ("Fade End Height", Float) = 1
        [Enum(X,0,Y,1,Z,2)] _HeightFadeAxis ("Fade Axis", Float) = 1
        [Enum(Local,0,World,1)] _HeightFadeSpace ("Coordinate Space", Float) = 0
        [Toggle] _HeightFadeInvert ("Invert Direction", Float) = 0
        [Enum(Alpha,0,Clip,1,Dithering,2)] _HeightFadeMode ("Fade Mode", Float) = 0
        _HeightFadeBlend ("Height Fade Blend", Range(0, 1)) = 1
        _HeightFadeDitherScale ("Dither Scale", Range(1, 200)) = 4
        _HeightFadeEdgeWidth ("Edge Glow Width", Range(0, 0.5)) = 0
        [HDR] _HeightFadeEdgeColor ("Edge Glow Color", Color) = (1, 0.5, 0, 1)

        // ===== Intersection Fade (オブジェクト交差フェード) =====
        [Header(Intersection Fade)]
        [Toggle(_INTERSECTION_FADE)] _IntersectionFade ("Enable Intersection Fade", Float) = 0
        _IntersectionFadeDistance ("Fade Distance", Float) = 0.5
        [Enum(Alpha,0,Clip,1,Dithering,2)] _IntersectionFadeMode ("Fade Mode", Float) = 0
        _IntersectionFadeBlend ("Blend", Range(0, 1)) = 1
        _IntersectionFadeDitherScale ("Dither Scale", Range(1, 200)) = 4
        _IntersectionFadeEdgeWidth ("Edge Width", Float) = 0
        [HDR] _IntersectionFadeEdgeColor ("Edge Color", Color) = (0, 0.8, 1, 1)

        // ===== Distance Fade (距離フェード) =====
        [Header(Distance Fade VRChat Optimization)]
        [Toggle(_DISTANCE_FADE)] _DistanceFade ("Enable Distance Fade", Float) = 0
        _DistanceFadeStart ("Fade Start Distance", Float) = 10
        _DistanceFadeEnd ("Fade End Distance", Float) = 20
        [Enum(Alpha,0,Simplify,1,Dithering,2)] _DistanceFadeMode ("Fade Mode", Float) = 0
        _DistanceFadeBlend ("Distance Fade Blend", Range(0, 1)) = 1
        _DistFadeBlur ("Distance Fade Blur", Range(0, 1)) = 0
        _NearFadeStart ("Near Fade Start", Float) = 0
        _NearFadeEnd ("Near Fade End", Float) = 0
        _DistFadeDitherScale ("Distance Fade Dither Scale", Range(1, 100)) = 10
        // Per-effect distance fade
        _SpecularDistFade ("Specular Distance Fade", Range(0, 1)) = 0
        _HairSpecDistFade ("Hair Specular Distance Fade", Range(0, 1)) = 0
        _SSSDistFade ("SSS Distance Fade", Range(0, 1)) = 0
        _RimDistFade ("Rim Light Distance Fade", Range(0, 1)) = 0
        _Rim2DistFade ("Rim Light 2 Distance Fade", Range(0, 1)) = 0
        _OffsetRimDistFade ("Offset Rim Distance Fade", Range(0, 1)) = 0
        _EnvRimDistFade ("Env Rim Distance Fade", Range(0, 1)) = 0
        _MatCapDistFade ("MatCap Distance Fade", Range(0, 1)) = 0
        _MatCap2DistFade ("MatCap 2 Distance Fade", Range(0, 1)) = 0
        _MatCap3DistFade ("MatCap 3 Distance Fade", Range(0, 1)) = 0
        _ReflectionDistFade ("Reflection Distance Fade", Range(0, 1)) = 0
        _RefractionDistFade ("Refraction Distance Fade", Range(0, 1)) = 0
        _EmissionDistFade ("Emission Distance Fade", Range(0, 1)) = 0
        _AudioLinkDistFade ("AudioLink Distance Fade", Range(0, 1)) = 0
        _GlitterDistFade ("Glitter Distance Fade", Range(0, 1)) = 0
        _IridescenceDistFade ("Iridescence Distance Fade", Range(0, 1)) = 0
        _DripDistFade ("Water Drip Distance Fade", Range(0, 1)) = 0
        _HologramDistFade ("Hologram Distance Fade", Range(0, 1)) = 0
        _GlitchDistFade ("Glitch Distance Fade", Range(0, 1)) = 0
        _DecalDistFade ("Decal Distance Fade", Range(0, 1)) = 0
        _BacklightDistFade ("Backlight Distance Fade", Range(0, 1)) = 0

        [Header(Vertex Offset Animation Wind Breathing)]
        [Toggle(_VERTEX_ANIMATION)] _VertexAnimation ("Enable Vertex Animation", Float) = 0
        _VertexAnimSpeed ("Animation Speed", Float) = 1
        _VertexAnimStrength ("Animation Strength", Range(0, 1)) = 0.1
        _VertexAnimFrequency ("Animation Frequency", Range(0, 10)) = 1
        [Enum(Wave,0,Breath,1,Wind,2,Pulse,3)] _VertexAnimType ("Animation Type", Float) = 2
        [Toggle(_VERTEX_ANIM_MASK)] _UseVertexAnimMask ("Use Vertex Anim Mask", Float) = 0
        _VertexAnimMask ("Vertex Anim Mask", 2D) = "white" {}

        [Header(Hologram Glitch Effect)]
        [Toggle(_HOLOGRAM)] _Hologram ("Enable Hologram", Float) = 0
        _HologramColor ("Hologram Color", Color) = (0,1,1,1)
        _HologramScanlineSpeed ("Scanline Speed", Float) = 1
        _HologramScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.5
        _HologramScanlineDensity ("Scanline Density", Float) = 100
        _HologramScanlineWidth ("Scanline Width", Range(0, 1)) = 0.5
        _HologramFlickerSpeed ("Flicker Speed", Float) = 5
        _HologramFlickerAmount ("Flicker Amount", Range(0, 1)) = 0.3
        _HologramEdgeGlowPower ("Edge Glow Power", Range(0.1, 10)) = 3
        _HologramEdgeGlowIntensity ("Edge Glow Intensity", Range(0, 5)) = 1
        _HologramAlpha ("Hologram Alpha", Range(0, 1)) = 0
        _HologramMonochrome ("Monochrome", Range(0, 1)) = 0
        _HologramNoiseIntensity ("Noise Intensity", Range(0, 1)) = 0
        _HologramNoiseSpeed ("Noise Speed", Float) = 1
        [Toggle(_HOLOGRAM_MASK)] _UseHologramMask ("Use Hologram Mask", Float) = 0
        _HologramMask ("Hologram Mask", 2D) = "white" {}
        _HologramMaskScrollSpeed ("Hologram Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _HologramMaskRotateSpeed ("Hologram Mask Rotate Speed", Float) = 0
        [Toggle(_HOLOGRAM_NOISE)] _UseHologramNoise ("Use Noise Texture", Float) = 0
        _HologramNoiseTex ("Noise Texture", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _HologramBlendMode ("Hologram Blend Mode", Float) = 0
        _HologramBlend ("Hologram Blend", Range(0, 1)) = 1
        _HologramBlur ("Hologram Blur", Range(0, 1)) = 0
        [Toggle(_GLITCH)] _Glitch ("Enable Glitch", Float) = 0
        _GlitchIntensity ("Glitch Intensity", Range(0, 3)) = 0.5
        _GlitchSpeed ("Glitch Speed", Float) = 1
        _GlitchBlockSize ("Glitch Block Size", Range(0.01, 1)) = 0.1
        _GlitchRGBSplitIntensity ("RGB Split Intensity", Range(0, 3)) = 0.5
        _GlitchFrequency ("Glitch Frequency", Range(0, 1)) = 0.3
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _GlitchBlendMode ("Glitch Blend Mode", Float) = 0
        _GlitchBlend ("Glitch Blend", Range(0, 1)) = 1
        _GlitchBlur ("Glitch Blur", Range(0, 1)) = 0
        _GlitchMask ("Glitch Mask", 2D) = "white" {}
        _GlitchMaskScale ("Glitch Mask Scale", Range(1, 5)) = 1
        _GlitchMaskAffectsRGBSplit ("Mask Affects RGB Split", Range(0, 1)) = 1
        _GlitchMaskAffectsFrequency ("Mask Affects Frequency", Range(0, 1)) = 0
        _GlitchNoiseTex ("Glitch Noise Texture", 2D) = "gray" {}
        _GlitchNoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.5
        _GlitchNoiseScrollSpeed ("Noise Scroll Speed", Vector) = (1, 0.5, 0, 0)
        [Enum(UV Distortion,0,Color Corruption,1,Block Noise,2)] _GlitchNoiseMode ("Noise Mode", Float) = 0

        [Space(5)]
        [Toggle(_GLITCH_STRETCH)] _GlitchStretch ("Enable Stretch Glitch", Float) = 0
        _GlitchStretchIntensity ("Stretch Intensity", Range(0, 5)) = 0.5
        _GlitchStretchSpeed ("Stretch Speed", Float) = 1
        _GlitchStretchBlockSize ("Stretch Block Size", Range(0.01, 1)) = 0.1
        _GlitchStretchFrequency ("Stretch Frequency", Range(0, 1)) = 0.3
        _GlitchStretchMask ("Stretch Glitch Mask", 2D) = "white" {}
        _GlitchStretchMaskScale ("Stretch Mask Scale", Range(1, 5)) = 1

        // ===== Illustration Style (イラスト風技法) =====
        [Header(Illustration Style)]
        [Toggle(_COLOR_QUANTIZE)] _UseColorQuantize ("Enable Color Quantize", Float) = 0
        [Enum(RGB,0,HSV,1)] _QuantizeMode ("Quantize Mode", Float) = 1
        _QuantizeLevels ("Quantize Levels", Range(2, 32)) = 8
        _QuantizeHueLevels ("Hue Levels", Range(2, 36)) = 12
        _QuantizeSatLevels ("Saturation Levels", Range(2, 16)) = 8
        _QuantizeValLevels ("Value Levels", Range(2, 16)) = 8
        _QuantizeDither ("Dither Amount", Range(0, 1)) = 0.5
        _QuantizeBlend ("Quantize Blend", Range(0, 1)) = 1
        _QuantizeMask ("Quantize Mask", 2D) = "white" {}

        [Toggle(_LUT_3D)] _UseLUT3D ("Enable 3D LUT", Float) = 0
        _LUT3DTex ("LUT Texture", 2D) = "white" {}
        _LUT3DIntensity ("LUT Intensity", Range(0, 1)) = 1
        _LUT3DSize ("LUT Grid Size", Float) = 32

        [Toggle(_HATCHING)] _UseHatching ("Enable Hatching", Float) = 0
        _HatchTex0 ("Hatch Texture 0 (RGBA=L1-4)", 2D) = "white" {}
        _HatchTex1 ("Hatch Texture 1 (RG=L5-6)", 2D) = "white" {}
        _HatchingTiling ("Hatching Tiling", Float) = 8
        _HatchingColor ("Hatching Color", Color) = (0.1, 0.1, 0.1, 1)
        _HatchingBlend ("Hatching Blend", Range(0, 1)) = 1
        _HatchingMask ("Hatching Mask", 2D) = "white" {}

        [Toggle(_WATERCOLOR)] _UseWatercolor ("Enable Watercolor", Float) = 0
        _WCEdgeDarkening ("Edge Darkening", Range(0, 2)) = 0.5
        _WCWetEdge ("Wet Edge", Range(0, 1)) = 0.3
        _WCGranulation ("Granulation", Range(0, 1)) = 0.4
        _WCGranulationTex ("Granulation Texture", 2D) = "gray" {}
        _WCPaperTex ("Paper Texture", 2D) = "white" {}
        _WCPaperIntensity ("Paper Intensity", Range(0, 1)) = 0.3
        _WCPaperTiling ("Paper Tiling", Float) = 1
        _WCBlend ("Watercolor Blend", Range(0, 1)) = 1
        _WCMask ("Watercolor Mask", 2D) = "white" {}

        [Toggle(_SOFT_FILTER)] _UseSoftFilter ("Enable Soft Filter", Float) = 0
        _SoftFilterRadius ("Filter Radius", Range(0, 10)) = 2
        _SoftFilterBlend ("Filter Blend", Range(0, 1)) = 0.5
        _SoftFilterThreshold ("Bloom Threshold", Range(0, 1)) = 0.6
        [Enum(Full Blur,0,Selective Bloom,1)] _SoftFilterMode ("Filter Mode", Float) = 1

        [Toggle(_KUWAHARA_FILTER)] _UseKuwahara ("Enable Kuwahara Filter", Float) = 0
        _KuwaharaRadius ("Kuwahara Radius", Range(1, 6)) = 3
        _KuwaharaBlend ("Kuwahara Blend", Range(0, 1)) = 1

        [Toggle(_SCREEN_EDGE)] _UseScreenEdge ("Enable Screen Edge", Float) = 0
        _EdgeColor ("Edge Color", Color) = (0, 0, 0, 1)
        _EdgeWidth ("Edge Width", Range(0.1, 5)) = 1
        _EdgeDepthSensitivity ("Depth Sensitivity", Range(0, 50)) = 10
        _EdgeNormalSensitivity ("Normal Sensitivity", Range(0, 10)) = 2
        _EdgeBlend ("Edge Blend", Range(0, 1)) = 1

        [Toggle(_COLOR_BLEEDING)] _UseColorBleeding ("Enable Color Bleeding", Float) = 0
        _BleedingRadius ("Bleeding Radius", Range(0, 5)) = 1
        _BleedingBlend ("Bleeding Blend", Range(0, 1)) = 0.3

        [Toggle(_CHROMATIC_ABERRATION)] _UseChromaticAberration ("Enable Chromatic Aberration", Float) = 0
        _CAIntensity ("CA Intensity", Range(0, 20)) = 3
        _CABlend ("CA Blend", Range(0, 1)) = 1

        [Toggle(_OUTLINE_HAND_DRAWN)] _UseHandDrawnOutline ("Enable Hand-drawn Outline", Float) = 0
        _OutlineNoiseTex ("Outline Noise Texture", 2D) = "gray" {}
        _OutlineNoiseTiling ("Noise Tiling", Float) = 5
        _OutlineWidthVariation ("Width Variation", Range(0, 0.5)) = 0.2
        _OutlineJitterAmount ("Jitter Amount", Range(0, 1)) = 0.3

        [Header(Decal System Stickers)]
        [Toggle(_DECAL)] _Decal ("Enable Decal", Float) = 0
        _DecalTex ("Decal Texture", 2D) = "white" {}
        _DecalColor ("Decal Color", Color) = (1,1,1,1)
        _DecalPosition ("Decal Position XY", Vector) = (0,0,0,0)
        _DecalRotation ("Decal Rotation", Range(0, 360)) = 0
        _DecalScale ("Decal Scale", Float) = 1
        [Enum(Add,0,Multiply,1,Overlay,2,Replace,3)] _DecalBlendMode ("Decal Blend Mode", Float) = 0
        _DecalBlend ("Decal Blend", Range(0, 1)) = 1
        _DecalBlur ("Decal Blur", Range(0, 1)) = 0

        [Header(Backface Texture Cloth Interior)]
        [Toggle(_BACKFACE_TEXTURE)] _BackfaceTexture ("Enable Backface Texture", Float) = 0
        _BackfaceTex ("Backface Texture", 2D) = "white" {}
        _BackfaceColor ("Backface Color", Color) = (1,1,1,1)
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _BackfaceBlendMode ("Backface Blend Mode", Float) = 0
        _BackfaceBlend ("Backface Blend", Range(0, 1)) = 1

        [Header(Video Render Texture Screen Display)]
        [Toggle(_VIDEO_TEXTURE)] _VideoTexture ("Enable Video Texture", Float) = 0
        _VideoTex ("Video Render Texture", 2D) = "black" {}
        _VideoEmission ("Video Emission", Range(0, 5)) = 1
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _VideoBlendMode ("Video Blend Mode", Float) = 0
        _VideoBlend ("Video Blend", Range(0, 1)) = 1

        [Header(LTCGI Realtime GI Support)]
        [Toggle(_LTCGI)] _LTCGI ("Enable LTCGI", Float) = 0
        _LTCGIIntensity ("LTCGI Intensity", Range(0, 2)) = 1
        _LTCGISpecular ("LTCGI Specular", Range(0, 1)) = 0.5
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _LTCGIBlendMode ("LTCGI Blend Mode", Float) = 0
        _LTCGIBlend ("LTCGI Blend", Range(0, 1)) = 1

        [Header(Water Drip Effect)]
        [Toggle(_WATER_DRIP)] _WaterDrip ("Enable Water Drip", Float) = 0
        _DripColor ("Drip Color", Color) = (0.7, 0.85, 1.0, 1)
        _DripSpeed ("Drip Speed", Range(0.1, 5)) = 1
        _DripDensity ("Drip Density", Range(0, 1)) = 0.5
        _DripSize ("Drip Size", Range(0.01, 0.5)) = 0.15
        _DripTrailLength ("Drip Trail Length", Range(0, 3)) = 1.0
        _DripIntensity ("Drip Intensity", Range(0, 2)) = 1
        _DripSharpness ("Drip Sharpness", Range(0.5, 5)) = 2
        [Toggle(_DRIP_MASK)] _UseDripMask ("Use Drip Mask", Float) = 0
        _DripMask ("Drip Mask", 2D) = "white" {}
        _DripMaskScrollSpeed ("Drip Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _DripMaskRotateSpeed ("Drip Mask Rotate Speed", Float) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _DripBlendMode ("Drip Blend Mode", Float) = 0
        _DripBlend ("Drip Blend", Range(0, 1)) = 1
        _DripBlur ("Drip Blur", Range(0, 1)) = 0

        [Header(Smear Effect)]
        [Toggle(_SMEAR)] _Smear ("Enable Smear", Float) = 0
        _SmearStretch ("Stretch Amount", Range(0, 3)) = 0
        _SmearDirection ("Smear Direction", Vector) = (0,0,0,0)
        _SmearNoiseScale ("Noise Scale", Range(0, 5)) = 1
        _SmearNoiseStrength ("Noise Strength", Range(0, 1)) = 0.3
        [Toggle] _SmearAutoMagnitude ("Auto Magnitude", Float) = 0
        _SmearMotionSensitivity ("Motion Sensitivity", Range(0.1, 10)) = 1
        [Toggle] _SmearVATVelocity ("VAT Velocity Link", Float) = 0
        _SmearTrailLength ("Trail Length", Range(0, 0.5)) = 0.1
        _SmearTrailFade ("Trail Fade", Range(0, 1)) = 0.5
        _SmearGlowColor ("Glow Color", Color) = (1,1,1,1)
        _SmearGlowIntensity ("Glow Intensity", Range(0, 3)) = 1
        _SmearGlowPower ("Glow Power", Range(0.5, 10)) = 3
        _SmearEmission ("Emission Intensity", Range(0, 3)) = 0
        _SmearEmissionColor ("Emission Color", Color) = (1,0.5,0,1)
        _SmearMask ("Smear Mask", 2D) = "white" {}
        _SmearMaskScrollSpeed ("Mask Scroll Speed XY", Vector) = (0,0,0,0)
        _SmearMaskRotateSpeed ("Mask Rotate Speed", Float) = 0
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _SmearBlendMode ("Smear Blend Mode", Float) = 0
        _SmearBlend ("Smear Blend", Range(0, 1)) = 1
        _SmearBlur ("Smear Blur", Range(0, 1)) = 0
        _SmearDistFade ("Smear Distance Fade", Range(0, 1)) = 0

        [Header(Dithering Alpha Transparent Dithering)]
        [Toggle(_DITHERING_ALPHA)] _DitheringAlpha ("Enable Dithering Alpha", Float) = 0
        _DitheringAlphaScale ("Dithering Alpha Scale", Range(1, 100)) = 10
        [Toggle(_HASHED_ALPHA)] _HashedAlpha ("Enable Hashed Alpha", Float) = 0
        _HashedAlphaScale ("Hashed Alpha Scale", Range(0.5, 8)) = 1
        [Toggle(_BLUE_NOISE_DITHER)] _BlueNoiseDither ("Enable Blue Noise Dither", Float) = 0
        _BlueNoiseTemporal ("Blue Noise Temporal Speed", Range(0, 8)) = 1
        _BlueNoiseAmount ("Blue Noise Blend", Range(0, 1)) = 1
        [Header(Refraction)]
        [Toggle(_REFRACTION)] _Refraction ("Enable Refraction", Float) = 0
        _RefractionIndex ("Refraction Index IOR", Range(1, 3)) = 1.5
        _RefractionIntensity ("Refraction Intensity", Range(0, 1)) = 1
        _RefractionBlur ("Refraction Blur", Range(0, 1)) = 0
        [Toggle(_REFRACTION_MASK)] _UseRefractionMask ("Use Refraction Mask", Float) = 0
        _RefractionMask ("Refraction Mask", 2D) = "white" {}
        [Enum(Normal,0,Soft,1,Screen,2,Overlay,3)] _RefractionBlendMode ("Refraction Blend Mode", Float) = 0
        _RefractionBlend ("Refraction Blend", Range(0, 1)) = 1

        [Header(Tessellation Surface Smoothing)]
        [Toggle(_TESSELLATION)] _Tessellation ("Enable Tessellation", Float) = 0
        _TessFactor ("Tessellation Factor", Range(1, 16)) = 4
        _TessPhongStrength ("Phong Smoothing Strength", Range(0, 1)) = 0.5
        _TessNormalSmooth ("Normal Smooth Strength", Range(0, 1)) = 0.5
        _TessDistanceMin ("Min Distance (Full Tess)", Float) = 1
        _TessDistanceMax ("Max Distance (No Tess)", Float) = 20
        [Toggle(_TESS_DISPLACEMENT)] _TessDisplacement ("Enable Displacement", Float) = 0
        _TessDispMap ("Displacement Map", 2D) = "gray" {}
        _TessDispStrength ("Displacement Strength", Range(-1, 1)) = 0
        _TessDispOffset ("Displacement Offset", Range(-0.5, 0.5)) = 0

        [Header(Perspective Flattening)]
        [Toggle(_PERSPECTIVE_FLAT)] _PerspectiveFlat ("Enable Perspective Flatten", Float) = 0
        _PerspectiveFlatAmount ("Flatten Amount", Range(0, 1)) = 0.5

        [Header(Depth Color Fade)]
        [Toggle(_DEPTH_COLOR_FADE)] _DepthColorFade ("Enable Depth Color Fade", Float) = 0
        _DepthFadeColor ("Atmosphere Color", Color) = (0.7, 0.8, 1.0, 1)
        _DepthFadeStart ("Fade Start Distance", Float) = 10
        _DepthFadeEnd ("Fade End Distance", Float) = 100
        _DepthFadeIntensity ("Fade Intensity", Range(0, 1)) = 0.5
        _DepthFadeDesaturation ("Desaturation", Range(0, 1)) = 0.3

        // ===== Mirror / Camera Control (ミラー・カメラ制御) =====
        [Header(Mirror Camera Control VRChat)]
        [Toggle(_MIRROR_CONTROL)] _MirrorControl ("Enable Mirror Camera Control", Float) = 0
        [Enum(Both,0,Mirror Only,1,Non Mirror Only,2)] _MirrorMode ("Mirror Mode", Float) = 0
        [Enum(Both,0,Camera Only,1,Non Camera Only,2)] _CameraMode ("Camera Mode", Float) = 0
        _MirrorEmissionMultiplier ("Mirror Emission Multiplier", Range(0, 2)) = 1

        // ===== Advanced (詳細設定) =====
        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite ("Z Write", Float) = 1
        [Enum(Off,0,On,1)] _AlphaToMask ("Alpha To Coverage (MSAA)", Float) = 0
        // ===== Stencil =====
        [Header(Stencil)]
        _StencilRef ("Stencil Reference", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Pass Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail Operation", Float) = 0
        _StencilReadMask ("Read Mask", Range(0, 255)) = 255
        _StencilWriteMask ("Write Mask", Range(0, 255)) = 255
    }

    SubShader
    {
        Tags
        {
            "LTCGI"="ALWAYS"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
        }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        // NOTE: GrabPass is required for _REFRACTION, _SOFT_FILTER, _KUWAHARA_FILTER,
        // _COLOR_BLEEDING, _CHROMATIC_ABERRATION features.
        // If you don't need these, use NataneToonShader_Cutout_Lite — no GrabPass, better perf.
        GrabPass
        {
            "_nataneBackgroundTexture"
        }

        // Outline Pass
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            ZWrite [_ZWrite]
            AlphaToMask [_AlphaToMask]
CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OUTLINE
            #pragma shader_feature_local _OUTLINE_TEXTURE_COLOR
            #pragma shader_feature_local _OUTLINE_WIDTH_MAP
            #pragma shader_feature_local _OUTLINE_MULTI_COLOR
            #pragma shader_feature_local _OUTLINE_MASK
            #pragma shader_feature_local _SMOOTH_NORMAL
            #pragma shader_feature_local _SMEAR
            #pragma shader_feature_local _HEIGHT_FADE
            #pragma shader_feature_local _OUTLINE_HAND_DRAWN
            #pragma shader_feature_local _PERSPECTIVE_FLAT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "UnityCG.cginc"

            // Inline HSV functions for Outline pass (standalone CGPROGRAM)
            #ifdef _OUTLINE_TEXTURE_COLOR
            float3 RGBtoHSV(float3 rgb)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
                float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            float3 HSVtoRGB(float3 hsv)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
            }
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                #ifdef _HEIGHT_FADE
                float3 worldPos : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _OutlineWidth;
            float _OutlineDistCompMax;
            float4 _OutlineColor;
            float4 _OutlineColor2;
            float _OutlineColorMix;
            float _Outline;
            float _OutlineMode;
            float _OutlineCornerSmooth;
            float _OutlineEdgeCompensation;
            float _VRChatMirrorMode;
            sampler2D _OutlineMask;
            float4 _OutlineMask_ST;
            sampler2D _OutlineWidthMap;
            #ifdef _OUTLINE_TEXTURE_COLOR
                sampler2D _MainTex;
                float4 _MainTex_ST;
                float _OutlineTexColorBlend;
                float _OutlineTexColorDarken;
                float _OutlineTexColorHueShift;
                float _OutlineTexColorSaturation;
            #endif
            #ifdef _SMOOTH_NORMAL
                float _SmoothNormalMode;
                sampler2D _SmoothNormalTex;
            #endif
            #ifdef _SMEAR
                float _SmearStretch;
                float4 _SmearDirection;
                float _SmearNoiseScale;
                float _SmearNoiseStrength;
                float _SmearAutoMagnitude;
                float _SmearMotionSensitivity;
            #endif
            #ifdef _HEIGHT_FADE
                float _HeightFadeStart;
                float _HeightFadeEnd;
                float _HeightFadeAxis;
                float _HeightFadeSpace;
                float _HeightFadeInvert;
                float _HeightFadeMode;
                float _HeightFadeBlend;
                float _HeightFadeDitherScale;
            #endif
            #ifdef _PERSPECTIVE_FLAT
                float _PerspectiveFlatAmount;
            #endif
            #ifdef _OUTLINE_HAND_DRAWN
                sampler2D _OutlineNoiseTex;
                float _OutlineNoiseTiling;
                float _OutlineWidthVariation;
                float _OutlineJitterAmount;

                float GetHandDrawnWidthFactor(float2 uv)
                {
                    float2 noiseUV = uv * _OutlineNoiseTiling;
                    float widthNoise = tex2Dlod(_OutlineNoiseTex, float4(noiseUV, 0, 0)).r;
                    return lerp(1.0 - _OutlineWidthVariation, 1.0 + _OutlineWidthVariation, widthNoise);
                }

                float3 GetHandDrawnJitter(float3 objectPos)
                {
                    float hash1 = frac(sin(dot(objectPos.xy, float2(12.9898, 78.233))) * 43758.5453);
                    float hash2 = frac(sin(dot(objectPos.yz, float2(45.164, 93.177))) * 27183.8241);
                    float hash3 = frac(sin(dot(objectPos.xz, float2(63.419, 17.652))) * 69143.2758);
                    return (float3(hash1, hash2, hash3) * 2.0 - 1.0) * _OutlineJitterAmount * 0.001;
                }
            #endif

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.uv = v.uv;

                #ifdef _SMEAR
                {
                    float3 rawDir = _SmearDirection.xyz;
                    float3 smDir;
                    float smAmt;
                    if (_SmearAutoMagnitude > 0.5)
                    {
                        float sp = length(rawDir);
                        smDir = (sp > 0.001) ? rawDir / sp : float3(0, 0, 1);
                        smAmt = min(sp * _SmearMotionSensitivity, _SmearStretch);
                    }
                    else
                    {
                        smDir = normalize(rawDir + float3(0.0001, 0.0001, 0.0001));
                        smAmt = _SmearStretch;
                    }
                    float3 wn = UnityObjectToWorldNormal(v.normal);
                    float dm = saturate(dot(wn, smDir));
                    float ns = frac(sin(dot(v.vertex.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
                    ns = lerp(1.0, ns, _SmearNoiseStrength * _SmearNoiseScale * 0.2);
                    float3 off = smDir * smAmt * dm * ns;
                    off = mul((float3x3)unity_WorldToObject, off);
                    v.vertex.xyz += off;
                }
                #endif

                #ifdef _OUTLINE
                    // Calculate distance compensation for consistent outline width
                    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    float distanceToCamera = distance(worldPos, _WorldSpaceCameraPos);
                    float distanceFactor = min(distanceToCamera * 0.1, _OutlineDistCompMax);

                    // Get outline width from map if enabled
                    float widthMultiplier = 1.0;
                    #ifdef _OUTLINE_WIDTH_MAP
                        widthMultiplier = tex2Dlod(_OutlineWidthMap, float4(v.uv, 0, 0)).r;
                    #endif
                    #ifdef _OUTLINE_MASK
                        widthMultiplier *= tex2Dlod(_OutlineMask, float4(TRANSFORM_TEX(v.uv, _OutlineMask), 0, 0)).r;
                    #endif

                    // Resolve outline normal (smooth normal or original)
                    float3 outlineNormal = v.normal;
                    #ifdef _SMOOTH_NORMAL
                        if (_SmoothNormalMode < 0.5)
                        {
                            // Mode 0: Vertex Color Object Space
                            // Decode from vertex color RGB: [0,1] -> [-1,1]
                            outlineNormal = v.color.rgb * 2.0 - 1.0;
                        }
                        else if (_SmoothNormalMode < 1.5)
                        {
                            // Mode 1: Vertex Color Tangent Space (lilToon compatible)
                            // Decode from vertex color RGB and transform via TBN matrix
                            float3 smoothTS = v.color.rgb * 2.0 - 1.0;
                            float3 binormal = cross(v.normal, v.tangent.xyz) * v.tangent.w;
                            float3x3 tbnOS = float3x3(v.tangent.xyz, binormal, v.normal);
                            outlineNormal = mul(smoothTS, tbnOS);
                        }
                        else
                        {
                            // Mode 2: Baked Normal Texture
                            // Sample baked normal from texture and transform from tangent space
                            float3 bakedNormal = tex2Dlod(_SmoothNormalTex, float4(v.uv, 0, 0)).rgb * 2.0 - 1.0;
                            float3 binormal = cross(v.normal, v.tangent.xyz) * v.tangent.w;
                            float3x3 tbnOS = float3x3(v.tangent.xyz, binormal, v.normal);
                            outlineNormal = mul(bakedNormal, tbnOS);
                        }
                        outlineNormal = normalize(outlineNormal);
                    #else
                        // Fallback: blend vertex normal toward vertex position direction
                        if (_OutlineCornerSmooth > 0.001)
                        {
                            float3 posNormal = normalize(v.vertex.xyz);
                            outlineNormal = normalize(lerp(v.normal, posNormal, _OutlineCornerSmooth));
                        }
                    #endif

                    if (_OutlineMode < 0.5)
                    {
                        // Mode 0: Inverted Hull - Extrusion along normals in view space
                        // Improved for better consistency at different angles
                        float3 norm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, outlineNormal));
                        // VRChat mirror/camera flips the view matrix, compensate outline normal
                        norm.x *= _VRChatMirrorMode > 0.5 ? -1.0 : 1.0;
                        float2 offset = TransformViewToProjection(norm.xy);

                        o.pos = UnityObjectToClipPos(v.vertex);

                        // Apply distance compensation for consistent outline width
                        // Scale down by 0.01 to maintain original scale with new range (0-1)
                        float outlineWidth = _OutlineWidth * 0.1 * (1.0 + distanceFactor) * widthMultiplier;

                        // Edge width compensation
                        if (_OutlineEdgeCompensation > 0.001)
                        {
                            float normalConsistency = saturate(dot(normalize(v.normal), outlineNormal));
                            float edgeComp = lerp(1.0, lerp(0.3, 1.0, normalConsistency), _OutlineEdgeCompensation);
                            outlineWidth *= edgeComp;
                        }

                        // Hand-drawn outline: width variation
                        #ifdef _OUTLINE_HAND_DRAWN
                            outlineWidth *= GetHandDrawnWidthFactor(v.uv);
                        #endif

                        o.pos.xy += offset * o.pos.z * outlineWidth;

                        // Hand-drawn outline: position jitter
                        #ifdef _OUTLINE_HAND_DRAWN
                            o.pos.xyz += GetHandDrawnJitter(v.vertex.xyz);
                        #endif
                    }
                    else
                    {
                        // Mode 1: Back Face - Scale up vertices along normals in object space
                        // Improved with distance compensation
                        // Scale down by 0.1 to maintain original scale with new range (0-1)
                        float outlineWidth = _OutlineWidth * 0.1 * (1.0 + distanceFactor * 0.5) * widthMultiplier;

                        // Edge width compensation
                        if (_OutlineEdgeCompensation > 0.001)
                        {
                            float normalConsistency = saturate(dot(normalize(v.normal), outlineNormal));
                            float edgeComp = lerp(1.0, lerp(0.3, 1.0, normalConsistency), _OutlineEdgeCompensation);
                            outlineWidth *= edgeComp;
                        }

                        // Hand-drawn outline: width variation
                        #ifdef _OUTLINE_HAND_DRAWN
                            outlineWidth *= GetHandDrawnWidthFactor(v.uv);
                        #endif

                        float3 scaledPos = v.vertex.xyz + normalize(outlineNormal) * outlineWidth;
                        o.pos = UnityObjectToClipPos(float4(scaledPos, 1.0));

                        // Hand-drawn outline: position jitter
                        #ifdef _OUTLINE_HAND_DRAWN
                            o.pos.xyz += GetHandDrawnJitter(v.vertex.xyz);
                        #endif
                    }

                    // Perspective Flattening for outline pass
                    #ifdef _PERSPECTIVE_FLAT
                    {
                        float flatZ = lerp(o.pos.z, o.pos.w * 0.5, _PerspectiveFlatAmount);
                        o.pos.z = flatZ;
                    }
                    #endif

                    #ifdef _HEIGHT_FADE
                    o.worldPos = worldPos;
                    #endif
                #else
                    o.pos = float4(0, 0, 0, 0);
                #endif

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #ifdef _OUTLINE
                    fixed4 col = _OutlineColor;

                    // Apply texture-linked outline color
                    #ifdef _OUTLINE_TEXTURE_COLOR
                        fixed4 texColor = tex2D(_MainTex, TRANSFORM_TEX(i.uv, _MainTex));
                        fixed3 darkenedTexColor = texColor.rgb * (1.0 - _OutlineTexColorDarken);
                        // HSV adjustment
                        float3 outHSV = RGBtoHSV(darkenedTexColor);
                        outHSV.x = frac(outHSV.x + _OutlineTexColorHueShift);
                        outHSV.y = saturate(outHSV.y * _OutlineTexColorSaturation);
                        darkenedTexColor = HSVtoRGB(outHSV);
                        col.rgb = lerp(col.rgb, darkenedTexColor, _OutlineTexColorBlend);
                    #endif

                    // Apply multi-color outline
                    #ifdef _OUTLINE_MULTI_COLOR
                        // Mix between two colors based on UV or other parameter
                        float mixFactor = frac(i.uv.y * 5.0 + _Time.y * 0.5); // Animated gradient
                        col.rgb = lerp(_OutlineColor.rgb, _OutlineColor2.rgb, mixFactor * _OutlineColorMix);
                    #endif

                    // Apply outline mask
                    #ifdef _OUTLINE_MASK
                        float outlineMask = tex2D(_OutlineMask, TRANSFORM_TEX(i.uv, _OutlineMask)).r;
                        // Clip directly by mask value so it works regardless of _OutlineColor.a
                        clip(outlineMask - 0.01);
                        col.a *= outlineMask;
                    #endif

                    // Apply height fade to outline
                    #ifdef _HEIGHT_FADE
                    {
                        float height;
                        if (_HeightFadeSpace < 0.5)
                        {
                            float3 localPos = mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
                            height = _HeightFadeAxis < 0.5 ? localPos.x : (_HeightFadeAxis < 1.5 ? localPos.y : localPos.z);
                        }
                        else
                        {
                            height = _HeightFadeAxis < 0.5 ? i.worldPos.x : (_HeightFadeAxis < 1.5 ? i.worldPos.y : i.worldPos.z);
                        }
                        float heightFade = saturate((height - _HeightFadeStart) / max(_HeightFadeEnd - _HeightFadeStart, 0.001));
                        heightFade = _HeightFadeInvert > 0.5 ? 1.0 - heightFade : heightFade;

                        if (_HeightFadeMode < 0.5)
                        {
                            // Alpha mode
                            col.a *= heightFade;
                            clip(col.a - 0.001);
                        }
                        else if (_HeightFadeMode < 1.5)
                        {
                            // Clip mode
                            clip(heightFade - 0.001);
                        }
                        else
                        {
                            // Dithering mode
                            float2 spos = i.pos.xy * max(_HeightFadeDitherScale, 1.0) * 0.1;
                            float ditherThreshold = frac(dot(floor(spos), float2(0.067, 0.258)) * 43.0);
                            clip(heightFade - ditherThreshold);
                        }
                    }
                    #endif

                    UNITY_APPLY_FOG(i.fogCoord, col);
                    return col;
                #else
                    discard;
                    return fixed4(0, 0, 0, 0);
                #endif
            }
            ENDCG
        }

        // Base Forward Pass
        Pass
        {
            Name "FORWARD_BASE"
            Tags { "LightMode" = "ForwardBase" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            AlphaToMask [_AlphaToMask]
CGPROGRAM
            #pragma target 4.6
            #pragma vertex tessVert
            #pragma hull hull
            #pragma domain domain
            #pragma fragment frag_cutout
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _MAIN_TEX_ANIMATION
            #pragma shader_feature_local _2ND_TEXTURE
            #pragma shader_feature_local _3RD_TEXTURE
            #pragma shader_feature_local _4TH_TEXTURE
            #pragma shader_feature_local _5TH_TEXTURE
            #pragma shader_feature_local _SCREEN_TONE
            #pragma shader_feature_local _HALFTONE_SHADOW
            #pragma shader_feature_local _GRADIENT_BASE_COLOR
            #pragma shader_feature_local _USE_RAMP
            #pragma shader_feature_local _USE_MULTI_SHADOW
            #pragma shader_feature_local _SHADOW_RECEIVE_MASK
            #pragma shader_feature_local _SDF_MAP
            #pragma shader_feature_local _FACE_SDF_ROTATION
            #pragma shader_feature_local _SHADING_GRADE_MAP
            #pragma shader_feature_local _USE_AO
            #pragma shader_feature_local _PROCEDURAL_AO
            #pragma shader_feature_local _NORMAL_WARP
            #pragma shader_feature_local _USE_DITHERING
            #pragma shader_feature_local _SHADOW_EDGE_NOISE
            #pragma shader_feature_local _CAST_SHADOW_COLOR
            #pragma shader_feature_local _LIGHT_SNAP
            #pragma shader_feature_local _BLUE_NOISE_DITHER
#pragma shader_feature_local _SOFT_LIGHTING_MODE
            #pragma shader_feature_local _USE_LIGHT_VOLUME
            #pragma shader_feature_local _LIGHT_VOLUME_SPECULAR
            #pragma shader_feature_local _SPECULAR
            #pragma shader_feature_local _SPECULAR_AA
            #pragma shader_feature_local _SPECULAR_DITHER
            #pragma shader_feature_local _HAIR_SPECULAR
            #pragma shader_feature_local _ANGEL_RING
            #pragma shader_feature_local _RIM_LIGHT
            #pragma shader_feature_local _RIM_LIGHT_2
            #pragma shader_feature_local _OFFSET_RIM_LIGHT
            #pragma shader_feature_local _SHEEN
            #pragma shader_feature_local _SSS
            #pragma shader_feature_local _SSS_LUT
            #pragma shader_feature_local _MATCAP
            #pragma shader_feature_local _GLITTER
            #pragma shader_feature_local _GLINTS_ADVANCED
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local _ALPHA_MASK
            #pragma shader_feature_local _HUE_SHIFT
            #pragma shader_feature_local _REFLECTION
            #pragma shader_feature_local _FAKE_REFLECTION
            #pragma shader_feature_local _IRIDESCENCE
            #pragma shader_feature_local _ENV_RIM
            #pragma shader_feature_local _PARALLAX
            #pragma shader_feature_local _EYE_PARALLAX
            #pragma shader_feature_local _REFRACTION
            #pragma shader_feature_local _MATCAP_2
            #pragma shader_feature_local _MATCAP_3
            #pragma shader_feature_local _PROCEDURAL_MATCAP
            #pragma shader_feature_local _AUDIOLINK
            #pragma shader_feature_local _HEIGHT_FADE
            #pragma shader_feature_local _INTERSECTION_FADE
            #pragma shader_feature_local _DISTANCE_FADE
            #pragma shader_feature_local _VERTEX_ANIMATION
            #pragma shader_feature_local _HOLOGRAM
            #pragma shader_feature_local _GLITCH
            #pragma shader_feature_local _GLITCH_STRETCH
            #pragma shader_feature_local _COLOR_QUANTIZE
            #pragma shader_feature_local _LUT_3D
            #pragma shader_feature_local _HATCHING
            #pragma shader_feature_local _WATERCOLOR
            #pragma shader_feature_local _SOFT_FILTER
            #pragma shader_feature_local _KUWAHARA_FILTER
            #pragma shader_feature_local _SCREEN_EDGE
            #pragma shader_feature_local _COLOR_BLEEDING
            #pragma shader_feature_local _CHROMATIC_ABERRATION
            #pragma shader_feature_local _HOLOGRAM_NOISE
            #pragma shader_feature_local _DECAL
            #pragma shader_feature_local _BACKFACE_TEXTURE
            #pragma shader_feature_local _VIDEO_TEXTURE
            #pragma shader_feature_local _LTCGI
            #pragma shader_feature_local _WATER_DRIP
            #pragma shader_feature_local _SMEAR
            #pragma shader_feature_local _DITHERING_ALPHA
            #pragma shader_feature_local _HASHED_ALPHA
            #pragma shader_feature_local _VAT
            #pragma shader_feature_local _VAT_NORMAL
            #pragma shader_feature_local _PIXEL_VERTEX_LIGHTS
            #pragma shader_feature_local _SMOOTH_NORMAL
            #pragma shader_feature_local _VERTEX_COLOR_SHADOW
            #pragma shader_feature_local _PBR_LIKE
            #pragma shader_feature_local _TESSELLATION
            #pragma shader_feature_local _TESS_DISPLACEMENT
            #pragma shader_feature_local _PCSS
            #pragma shader_feature_local _PERSPECTIVE_FLAT
            #pragma shader_feature_local _DEPTH_COLOR_FADE
            #pragma shader_feature_local _MIRROR_CONTROL
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
            #define CUTOUT_VARIANT

            float _Cutoff;

            #include "../Include/Core/NataneToonCore.hlsl"

            half4 frag_cutout(v2f i) : SV_Target
            {
                half4 col = frag(i);
                #if defined(_HASHED_ALPHA)
                    // Hashed alpha is already clipped inside frag() (NataneToonFragment.hlsl).
                    // Clipping again here with a second hash would square the coverage,
                    // so only fall back to the hard cutoff when the toggle is off.
                    if (_HashedAlpha < 0.5)
                    {
                        clip(col.a - _Cutoff);
                    }
                #else
                    clip(col.a - _Cutoff);
                #endif
                return col;
            }


            ENDCG
        }

        // Additional Forward Pass
        Pass
        {
            Name "FORWARD_ADD"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off
            Cull [_Cull]

            CGPROGRAM
            #pragma target 4.6
            #pragma vertex tessVert
            #pragma hull hull
            #pragma domain domain
            #pragma fragment frag_cutout
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _MAIN_TEX_ANIMATION
            #pragma shader_feature_local _2ND_TEXTURE
            #pragma shader_feature_local _3RD_TEXTURE
            #pragma shader_feature_local _4TH_TEXTURE
            #pragma shader_feature_local _5TH_TEXTURE
            #pragma shader_feature_local _SCREEN_TONE
            #pragma shader_feature_local _GRADIENT_BASE_COLOR
            #pragma shader_feature_local _USE_RAMP
            #pragma shader_feature_local _SHADOW_RECEIVE_MASK
            #pragma shader_feature_local _USE_MULTI_SHADOW
            #pragma shader_feature_local _SOFT_LIGHTING_MODE
            #pragma shader_feature_local _HEIGHT_FADE
            #pragma shader_feature_local _INTERSECTION_FADE
            #pragma shader_feature_local _DISTANCE_FADE
            #pragma shader_feature_local _DITHERING_ALPHA
            #pragma shader_feature_local _HASHED_ALPHA
            #pragma shader_feature_local _SDF_MAP
            #pragma shader_feature_local _SHADING_GRADE_MAP
            #pragma shader_feature_local _USE_AO
            #pragma shader_feature_local _PROCEDURAL_AO
            #pragma shader_feature_local _NORMAL_WARP
            #pragma shader_feature_local _USE_DITHERING
            #pragma shader_feature_local _SHADOW_EDGE_NOISE
            #pragma shader_feature_local _CAST_SHADOW_COLOR
            #pragma shader_feature_local _LIGHT_SNAP
            #pragma shader_feature_local _BLUE_NOISE_DITHER
            #pragma shader_feature_local _SPECULAR
            #pragma shader_feature_local _SPECULAR_AA
            #pragma shader_feature_local _SPECULAR_DITHER
            #pragma shader_feature_local _HAIR_SPECULAR
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local _ALPHA_MASK
            #pragma shader_feature_local _PARALLAX
            #pragma shader_feature_local _SMEAR
            #pragma shader_feature_local _VAT
            #pragma shader_feature_local _VAT_NORMAL
            #pragma shader_feature_local _SMOOTH_NORMAL
            #pragma shader_feature_local _VERTEX_COLOR_SHADOW
            #pragma shader_feature_local _PBR_LIKE
            #pragma shader_feature_local _TESSELLATION
            #pragma shader_feature_local _TESS_DISPLACEMENT
            #pragma shader_feature_local _MIRROR_CONTROL
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
            #define CUTOUT_VARIANT

            float _Cutoff;

            #include "../Include/Core/NataneToonCore.hlsl"

            half4 frag_cutout(v2f i) : SV_Target
            {
                half4 col = frag(i);
                #if defined(_HASHED_ALPHA)
                    // Hashed alpha is already clipped inside frag() (NataneToonFragment.hlsl).
                    // Clipping again here with a second hash would square the coverage,
                    // so only fall back to the hard cutoff when the toggle is off.
                    if (_HashedAlpha < 0.5)
                    {
                        clip(col.a - _Cutoff);
                    }
                #else
                    clip(col.a - _Cutoff);
                #endif
                return col;
            }


            ENDCG
        }

        // Shadow Caster Pass
        Pass
        {
            Name "SHADOW_CASTER"
            Tags { "LightMode" = "ShadowCaster" }

                        CGPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            // Features that change the silhouette must match the forward passes
            #pragma shader_feature_local _MAIN_TEX_ANIMATION
            #pragma shader_feature_local _ALPHA_MASK
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local _DITHERING_ALPHA
            #pragma shader_feature_local _HASHED_ALPHA
            #pragma shader_feature_local _BLUE_NOISE_DITHER
            #pragma shader_feature_local _VAT
            #pragma shader_feature_local _VAT_NORMAL
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
            #define CUTOUT_VARIANT
            #include "../Include/Rendering/NataneToonShadowCaster.hlsl"
            ENDCG
        }
    }

    CustomEditor "NataneToonShaderGUI"
    FallBack "Transparent/Cutout/Diffuse"
}

