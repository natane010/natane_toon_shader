#ifndef NATANE_TOON_INPUT_INCLUDED
#define NATANE_TOON_INPUT_INCLUDED

// Properties and Structures
CBUFFER_START(UnityPerMaterial)

    // ===== SECTION 1: Core Rendering (主表面) =====
    // メインテクスチャ、カラー、アルファ、サーフェス設定
    // シェーダーの基本的な表面描画に必要なパラメータ群

    // Main Texture
    float4 _MainTex_ST;
    half4 _Color;

    // Main Texture Animation
    float4 _MainTexScrollSpeed;
    float _MainTexRotateSpeed;

    // Color Preservation
    float _AlbedoPreservation;
    float _Saturation;
    float _Brightness;

    // Surface Finish
    float _Glossiness;
    float _MatteEffect;

    // Final Color Blending
    float _FinalHighlightBlend;
    float _HighlightThreshold;
    float _FinalShadowBlend;
    float _ShadowThreshold;

    // ===== SECTION 2: Makeup Textures (マルチレイヤー) =====
    // 2nd〜5thテクスチャレイヤー（各8パラメータ）
    // メインテクスチャの上に重ねるメイクアップ/デカール用マルチレイヤーシステム

    // Makeup Textures
    float4 _2ndTex_ST;
    float _2ndTexHueShift;
    float _2ndTexSaturation;
    float _2ndTexValue;
    float _2ndTexIntensity;
    float _2ndTexBlendMode;
    float4 _2ndTexScrollSpeed;
    float _2ndTexRotateSpeed;

    float4 _3rdTex_ST;
    float _3rdTexHueShift;
    float _3rdTexSaturation;
    float _3rdTexValue;
    float _3rdTexIntensity;
    float _3rdTexBlendMode;
    float4 _3rdTexScrollSpeed;
    float _3rdTexRotateSpeed;

    float4 _4thTex_ST;
    float _4thTexHueShift;
    float _4thTexSaturation;
    float _4thTexValue;
    float _4thTexIntensity;
    float _4thTexBlendMode;
    float4 _4thTexScrollSpeed;
    float _4thTexRotateSpeed;

    float4 _5thTex_ST;
    float _5thTexHueShift;
    float _5thTexSaturation;
    float _5thTexValue;
    float _5thTexIntensity;
    float _5thTexBlendMode;
    float4 _5thTexScrollSpeed;
    float _5thTexRotateSpeed;

    // Screen-Tone Overlay
    #if defined(_SCREEN_TONE)
    half4 _ScreenToneColor;
    float _ScreenToneScale;
    float _ScreenToneThreshold;
    float _ScreenToneBlend;
    float _ScreenToneBlendMode;
    float _ScreenToneBlur;
    #endif

    // Halftone Shadow
    #if defined(_HALFTONE_SHADOW)
    half4 _HalftoneShadowColor;
    float _HalftoneShadowScale;
    float _HalftoneShadowThreshold;
    float _HalftoneShadowSoftness;
    float _HalftoneShadowIntensity;
    float _HalftoneShadowBlend;
    #endif

    // Gradient Base Color
    #if defined(_GRADIENT_BASE_COLOR)
    half4 _GradientTopColor;
    half4 _GradientBottomColor;
    float _GradientAxis;
    float _GradientSpace;
    float _GradientStart;
    float _GradientEnd;
    float _GradientBlend;
    float _GradientBlendMode;
    #endif

    // ===== SECTION 3: Lighting & Shading (ライティング基本) =====
    // シェーディングモード、マルチトーン、SDFシャドウマップ、影設定
    // トゥーンシェーディングの核となるライティング計算パラメータ群

    // Shading
    float _ShadingMode;
    float _SurfaceModel;
    float _LookMode;
    float _ToonWeight;
    float _NprWeight;
    float _PbrWeight;
    float _ShadowMainStrength;
    float _Shadow2ndBlur;
    float _Shadow3rdBlur;
    float _ShadingGradientWidth;
    half4 _ShadowColor;
    float _ShadowHueShift;
    float _ShadowSaturation;
    half4 _Shadow2ndColor;
    float _Shadow2ndBorder;
    half4 _Shadow3rdColor;
    float _Shadow3rdBorder;
    float _ShadowSteps;
    float _ShadowSharpness;
    float _StepBorderSmooth;
    float _ShadowOffset;
    float _WrapAmount;
    float _LitSoftness;
    float _ShadowBlend;

    // Vertex Color Shadow Threshold
    #if defined(_VERTEX_COLOR_SHADOW)
    float _VCShadowThreshold;
    float _VCShadowPush;
    #endif

    // SDF Shadow Map
    float _SDFIntensity;
    float _SDFSoftness;
    float _SDFOffset;

    // Face SDF Rotation
    float4 _FaceForwardDirection;
    float4 _FaceRightDirection;

    // Shading Grade Map
    float _ShadingGradeScale;

    // Ambient Occlusion
    float _AOIntensity;
    float _AOBlend;
    float _AOBlendMode;
    float _AOBlur;
    float _CavityStrength;
    float _SpecularOcclusionStrength;
    float _SkinSpecPrimaryStrength;
    float _SkinSpecSecondaryStrength;
    float _SkinSpecSecondarySmoothness;
    half4 _SkinSpecSecondaryColor;
    float _SkinSpecFresnelPower;

    // Procedural AO
    #if defined(_PROCEDURAL_AO)
    float _ProceduralAOHeightOffset;
    float _ProceduralAOIntensity;
    float _ProceduralAOSoftness;
    #endif

    // Normal Warping
    #if defined(_NORMAL_WARP)
    float _NormalFlattenY;
    float _NormalRoundness;
    #endif

    // Dithering
    float _DitheringScale;
    float _DitheringStrength;
    float _DitheringBlend;
    float _DitheringBlur;
    float _DitherStabilize;
    #if defined(_BLUE_NOISE_DITHER)
    float _BlueNoiseTemporal;
    float _BlueNoiseAmount;
    #endif

    // Advanced Lighting Controls
    float _SoftLightingIntensity;
    float _LightIntensity;
    float _IndirectLightIntensity;
    float _GIIntensity; // Environment Reflection (GI/Light Probes) intensity control
    float _LightColorInfluence;
    float _ShadowReceive;
    float _ShadowSmoothing;
    float _ShadowMaxDarkness;
    float _SmoothNormalShadingBlend;
    float _SmoothNormalMode;
    float _LightColorMin;
    float _LightColorMax;
    float _MonochromeLighting;
    float _LightMinInfluence;
    float _LightMaxInfluence;
    float _LightBlend;
    float _HighlightSoftness;
    float _BacklightIntensity;
    half4 _BacklightColor;
    float _BacklightBlend;
    float _BacklightBlendMode;
    float _BacklightBlur;
    float _AdditionalLightIntensity;

    // VRC Light Volumes
    float _LightVolumeIntensity;
    float _LightVolumeBlendMode;
    float _LightVolumeBlend;

    // Indirect Lighting (min color + max composition)
    half4 _IndirectLightMinColor;
    float _ShadowEnvStrength;

    // ===== SECTION 4: Effects - Light Based (光源依存エフェクト) =====
    // スペキュラ、ヘアスペキュラ、リムライト、バックライト
    // ライト方向に依存して変化するエフェクト群

    // Specular
    #if defined(_SPECULAR)
    half4 _SpecularColor;
    float _SpecularSize;
    float _SpecularSoftness;
    float _SpecularIntensity;
    float _SpecularBlend;
    float _SpecularBlendMode;
    float _SpecularBlur;
    float4 _SpecularMaskScrollSpeed;
    float _SpecularMaskRotateSpeed;
    #endif
    #if defined(_SPECULAR_AA)
    float _SpecularAAStrength;
    #endif
    #if defined(_SPECULAR_DITHER)
    float _SpecularDitherScale;
    float _SpecularDitherStrength;
    #endif

    // Hair Specular (Kajiya-Kay)
    #if defined(_HAIR_SPECULAR)
    half4 _HairSpecColor1;
    float _HairSpecShift1;
    float _HairSpecWidth1;
    half4 _HairSpecColor2;
    float _HairSpecShift2;
    float _HairSpecWidth2;
    float _HairSpecIntensity;
    float _HairSpecBlend;
    float _HairSpecBlendMode;
    float _HairStrandDirectionStrength;
    half4 _HairTransmissionColor;
    float _HairTransmissionStrength;
    float _HairTransmissionPower;
    #endif

    #if defined(_ANGEL_RING)
    float4 _AngelRingTex_ST;
    half4 _AngelRingColor;
    float _AngelRingOffset;
    float _AngelRingWidth;
    float _AngelRingIntensity;
    float _AngelRingBlend;
    float _AngelRingBlendMode;
    #endif

    // Rim Light
    #if defined(_RIM_LIGHT)
    half4 _RimColor;
    float _RimPower;
    float _RimIntensity;
    float _RimSpread;
    float _RimBlend;
    float _RimBlendMode;
    float _RimBlur;
    float4 _RimMaskScrollSpeed;
    float _RimMaskRotateSpeed;
    #endif

    // Rim Light 2
    #if defined(_RIM_LIGHT_2)
    half4 _RimColor2;
    float _RimPower2;
    float _RimIntensity2;
    float _RimSpread2;
    float _RimBlend2;
    float _RimBlendMode2;
    float _Rim2Blur;
    float4 _RimMask2ScrollSpeed;
    float _RimMask2RotateSpeed;
    #endif

    // Offset Rim Light
    #if defined(_OFFSET_RIM_LIGHT)
    half4 _OffsetRimColor;
    float _OffsetRimPower;
    float _OffsetRimIntensity;
    float _OffsetRimOffsetX;
    float _OffsetRimOffsetY;
    float _OffsetRimUseLightDir;
    float _OffsetRimLightDirStrength;
    float _OffsetRimSharpness;
    float _OffsetRimShadowMask;
    float _OffsetRimBlend;
    float _OffsetRimBlendMode;
    float _OffsetRimBlur;
    #endif

    // Sheen
    #if defined(_SHEEN)
    float4 _SheenMask_ST;
    half4 _SheenColor;
    float _SheenIntensity;
    float _SheenPower;
    float _SheenBlend;
    float _SheenBlendMode;
    #endif

    // ===== SECTION 5: Effects - View Based (視線依存エフェクト) =====
    // MatCap、リフレクション、環境リム、イリデッセンス
    // カメラ/視線方向に依存して変化するエフェクト群

    // MatCap
    #if defined(_MATCAP)
    float _MatCapIntensity;
    float _MatCapBlendMode;
    float _MatCapBlend;
    float _MatCapBlur;
    #endif

    // ===== SECTION 6: Effects - Emission (発光効果) =====
    // エミッション、グリッター、ホログラム、グリッチ
    // 自己発光・特殊視覚効果系のエフェクト群

    // Glitter
    #if defined(_GLITTER)
    half4 _GlitterColor;
    float _GlitterSize;
    float _GlitterDensity;
    float _GlitterSpeed;
    float _GlitterIntensity;
    float _GlitterBlend;
    float _GlitterBlendMode;
    float _GlitterBlur;
    float4 _GlitterMaskScrollSpeed;
    float _GlitterMaskRotateSpeed;
    #endif
    #if defined(_GLINTS_ADVANCED)
    float _GlintsSharpness;
    float _GlintsTemporal;
    float _GlintsNormalJitter;
    #endif

    // Outline
    #if defined(_OUTLINE)
    half4 _OutlineColor;
    float _OutlineWidth;
    float _OutlineDistCompMax;
    #endif

    // Outline Texture Color HSV
    #if defined(_OUTLINE_TEXTURE_COLOR)
    float _OutlineTexColorHueShift;
    float _OutlineTexColorSaturation;
    #endif

    // Emission
    #if defined(_EMISSION)
    half4 _EmissionColor;
    float _EmissionGlow;
    float _EmissionBlend;
    float _EmissionBlendMode;
    float _EmissionBlur;
    #endif

    // ===== SECTION 7: Surface Modification (表面変形) =====
    // ノーマルマップ、パララックス、リフラクション、ディゾルブ、ドリップ、デカール
    // 表面の見た目や形状を変形・修飾するエフェクト群

    // Normal Map
    float4 _BumpMap_ST;
    float _BumpScale;
    float _MicroNormalScale;
    float _MicroNormalTiling;
    float _MicroNormalStrength;
    // Normal Map UV Animation
    float4 _BumpMapScrollSpeed;
    float _BumpMapRotateSpeed;

    // ===== SECTION 8: Advanced Lighting (高度なライティング) =====
    // SSS（サブサーフェス・スキャタリング）、Light Volume、LTCGI
    // リアルタイム光学シミュレーション系の高度なライティングエフェクト

    // Subsurface Scattering
    #if defined(_SSS)
    half4 _SSSColor;
    float _SSSIntensity;
    float _SSSPower;
    float _SSSDistortion;
    float _ThicknessScale;
    float _SSSBlend;
    float _SSSBlendMode;
    float _SSSBlur;
    float _TransmissionStrength;
    #endif

    // SSS LUT (Pre-integrated Subsurface Scattering)
    #if defined(_SSS_LUT)
    float _SSSLUTScale;
    #endif

    // Virtual Expression - Dissolve
    #if defined(_DISSOLVE)
    float _DissolveAmount;
    float _DissolveEdgeWidth;
    half4 _DissolveEdgeColor;
    float _DissolveEdgeIntensity;
    float _DissolveBlend;
    float _DissolveBlendMode;
    float _DissolveBlur;
    float4 _DissolveTexScrollSpeed;
    float _DissolveTexRotateSpeed;
    float _DissolveCoordMode;
    float _DissolveWorldAxis;
    float _DissolveWorldMin;
    float _DissolveWorldMax;
    float _DissolveNoiseBlend;
    #endif

    // Virtual Expression - Hue Shift
    #if defined(_HUE_SHIFT)
    float _HueShift;
    float _HueShiftBlend;
    float _HueShiftBlur;
    #endif

    // Virtual Expression - Emission Animation
    #if defined(_EMISSION)
    float _EmissionScrollSpeed;
    float _EmissionScrollSpeedY;
    float _EmissionRotateSpeed;
    float4 _EmissionMaskScrollSpeed;
    float _EmissionMaskRotateSpeed;
    float _EmissionPulseSpeed;
    float _EmissionPulseAmplitude;
    #endif

    // Smoothness/Metallic - shared by reflection and realistic-character workflows
    float _Smoothness;
    float _Metallic;

    // Cubemap Reflection (Environment Mapping)
    #if defined(_REFLECTION)
    half4 _ReflectionColor;
    float _ReflectionIntensity;
    float _FresnelPower;
    float _FresnelSoftness;
    float _ReflectionBlendMode;
    float _ReflectionBlend;
    #endif
    float _ClearCoatIntensity;
    float _ClearCoatSmoothness;
    float _ClearCoatNormalScale;
    float _ClearCoatFresnelPower;

    // Iridescence
    #if defined(_IRIDESCENCE)
    half4 _IridescenceColor;
    float _IridescenceIntensity;
    float _IridescenceHueShift;
    float _IridescenceSize;
    float _IridescenceBlend;
    float _IridescenceBlendMode;
    float _IridescenceBlur;
    #endif

    // Environmental Rim
    #if defined(_ENV_RIM)
    half4 _EnvRimColor;
    float _EnvRimPower;
    float _EnvRimIntensity;
    float _EnvRimBlend;
    float _EnvRimBlendMode;
    float _EnvRimBlur;
    #endif

    // Parallax Mapping
    #if defined(_PARALLAX)
    float _Parallax;
    float _ParallaxScale;
    float _ParallaxMinSamples;
    float _ParallaxMaxSamples;
    #endif

    #if defined(_EYE_PARALLAX)
    float _EyeParallaxDepth;
    #endif

    // Refraction
    #if defined(_REFRACTION)
    float _RefractionIndex;
    float _RefractionIntensity;
    float _RefractionBlur;
    float _RefractionBlend;
    float _RefractionBlendMode;
    #endif

    // MatCap 2 & 3
    #if defined(_MATCAP_2)
    float _MatCapIntensity2;
    float _MatCapBlendMode2;
    float _MatCapBlend2;
    float _MatCap2Blur;
    #endif
    #if defined(_MATCAP_3)
    float _MatCapIntensity3;
    float _MatCapBlendMode3;
    float _MatCapBlend3;
    float _MatCap3Blur;
    #endif

    // Rim Direction Control
    float _RimDirectionControl;
    float4 _RimLightDirection;
    float _RimDirectionRange;
    float _RimDirStrength;
    float _RimShadowMask;

    // Shadow Color Texture
    float _ShadowColorTexStrength;

    // Outline Multi-Color
    half4 _OutlineColor2;
    float _OutlineColorMix;

    // AudioLink
    #if defined(_AUDIOLINK)
    float _AudioLinkEmissionBand;
    float _AudioLinkEmissionIntensity;
    float _AudioLinkRimBand;
    float _AudioLinkRimIntensity;
    float _AudioLinkHueBand;
    float _AudioLinkHueShiftIntensity;
    float _AudioLinkDissolveBand;
    float _AudioLinkDissolveIntensity;
    float _AudioLinkOutlineBand;
    float _AudioLinkOutlineIntensity;
    float _AudioLinkBlend;
    float _AudioLinkBlendMode;
    #endif

    // ===== SECTION 9: Distance Fade System (距離フェード) =====
    // グローバルフェード設定 + 各エフェクト個別のフェード制御
    // カメラ距離に応じたLOD/パフォーマンス最適化システム

    // Distance Fade
    #if defined(_DISTANCE_FADE)
    float _DistanceFadeStart;
    float _DistanceFadeEnd;
    float _DistanceFadeMode;
    float _DistanceFadeBlend;
    float _DistFadeBlur;
    float _NearFadeStart;
    float _NearFadeEnd;
    float _DistFadeDitherScale;
    // ===== Per-Effect Distance Fade Blend =====
    // 各エフェクトに個別の距離フェードブレンド (0=フェード無し, 1=完全フェード)
    // Effect List:
    //   Specular / HairSpec / SSS
    //   Rim(1,2) / OffsetRim / EnvRim
    //   MatCap(1,2,3) / Reflection / Refraction
    //   Emission / AudioLink / Glitter / Iridescence
    //   Drip / Hologram / Glitch / Decal / Backlight
    float _SpecularDistFade;
    float _HairSpecDistFade;
    float _SSSDistFade;
    float _RimDistFade;
    float _Rim2DistFade;
    float _OffsetRimDistFade;
    float _EnvRimDistFade;
    float _MatCapDistFade;
    float _MatCap2DistFade;
    float _MatCap3DistFade;
    float _ReflectionDistFade;
    float _RefractionDistFade;
    float _EmissionDistFade;
    float _AudioLinkDistFade;
    float _GlitterDistFade;
    float _IridescenceDistFade;
    float _DripDistFade;
    float _HologramDistFade;
    float _GlitchDistFade;
    float _DecalDistFade;
    float _BacklightDistFade;
    float _SmearDistFade;
    #endif

    // Height Fade
    #if defined(_HEIGHT_FADE)
    float _HeightFadeStart;
    float _HeightFadeEnd;
    float _HeightFadeAxis;
    float _HeightFadeSpace;
    float _HeightFadeInvert;
    float _HeightFadeMode;
    float _HeightFadeBlend;
    float _HeightFadeDitherScale;
    float _HeightFadeEdgeWidth;
    half4 _HeightFadeEdgeColor;
    #endif

    // Intersection Fade
    #if defined(_INTERSECTION_FADE)
    float _IntersectionFadeDistance;
    float _IntersectionFadeMode;
    float _IntersectionFadeBlend;
    float _IntersectionFadeDitherScale;
    float _IntersectionFadeEdgeWidth;
    half4 _IntersectionFadeEdgeColor;
    #endif

    // ===== SECTION 10: Vertex & Special Features =====
    // 頂点アニメーション、VAT、テッセレーション、AudioLink、ディザリング
    // 頂点変形・外部連携・特殊レンダリング機能

    // Vertex Animation
    #if defined(_VERTEX_ANIMATION)
    float _VertexAnimSpeed;
    float _VertexAnimStrength;
    float _VertexAnimFrequency;
    float _VertexAnimType;
    #endif

    // Hologram & Glitch (conditionally compiled for optimization)
    #if defined(_HOLOGRAM)
    float _HologramScanlineSpeed;
    float _HologramScanlineIntensity;
    float _HologramFlickerSpeed;
    float _HologramFlickerAmount;
    half4 _HologramColor;
    float _HologramEdgeGlowPower;
    float _HologramEdgeGlowIntensity;
    float _HologramScanlineDensity;
    float _HologramScanlineWidth;
    float _HologramAlpha;
    float _HologramNoiseIntensity;
    float _HologramNoiseSpeed;
    float _HologramMonochrome;
    float _HologramBlend;
    float _HologramBlendMode;
    float _HologramBlur;
    float4 _HologramMaskScrollSpeed;
    float _HologramMaskRotateSpeed;
    #endif
    #if defined(_GLITCH)
    float _GlitchIntensity;
    float _GlitchSpeed;
    float _GlitchBlockSize;
    float _GlitchRGBSplitIntensity;
    float _GlitchFrequency;
    float _GlitchBlend;
    float _GlitchBlendMode;
    float _GlitchBlur;
    float4 _GlitchMask_ST;
    float _GlitchMaskScale;
    float _GlitchMaskAffectsRGBSplit;
    float _GlitchMaskAffectsFrequency;
    float4 _GlitchNoiseTex_ST;
    float _GlitchNoiseIntensity;
    float4 _GlitchNoiseScrollSpeed;
    float _GlitchNoiseMode;
    #endif

    // ===== Glitch Stretch =====
    #if defined(_GLITCH_STRETCH)
    float _GlitchStretchIntensity;
    float _GlitchStretchSpeed;
    float _GlitchStretchBlockSize;
    float _GlitchStretchFrequency;
    float4 _GlitchStretchMask_ST;
    float _GlitchStretchMaskScale;
    #endif

    // Decal
    #if defined(_DECAL)
    half4 _DecalColor;
    float4 _DecalPosition;
    float _DecalRotation;
    float _DecalScale;
    float _DecalBlendMode;
    float _DecalBlend;
    float _DecalBlur;
    #endif

    // Backface Texture
    #if defined(_BACKFACE_TEXTURE)
    half4 _BackfaceColor;
    float _BackfaceBlend;
    float _BackfaceBlendMode;
    #endif

    // Video Texture
    #if defined(_VIDEO_TEXTURE)
    float _VideoEmission;
    float _VideoBlend;
    float _VideoBlendMode;
    #endif

    // LTCGI
    #if defined(_LTCGI)
    float _LTCGIIntensity;
    float _LTCGISpecular;
    float _LTCGIBlend;
    float _LTCGIBlendMode;
    #endif

    // Dithering Alpha
    #if defined(_DITHERING_ALPHA)
    float _DitheringAlphaScale;
    #endif
    #if defined(_HASHED_ALPHA)
    float _HashedAlphaScale;
    #endif

    // Water Drip Effect
    #if defined(_WATER_DRIP)
    half4 _DripColor;
    float _DripSpeed;
    float _DripDensity;
    float _DripSize;
    float _DripTrailLength;
    float _DripIntensity;
    float _DripSharpness;
    float _DripBlend;
    float _DripBlendMode;
    float _DripBlur;
    float4 _DripMaskScrollSpeed;
    float _DripMaskRotateSpeed;
    #endif

    // Vertex Animation Texture (VAT)
    #if defined(_VAT)
    float4 _VATPositionMap_ST;
    float4 _VATNormalMap_ST;
    float _VATNumOfFrames;
    float _VATSpeed;
    float _VATIntensity;
    float _VATPadding;
    float _VATPositionMin;
    float _VATPositionMax;
    float _VATNormalMin;
    float _VATNormalMax;
    float _VATPackingMode;
    #endif

    // Tessellation
    #if defined(_TESSELLATION)
    float _TessFactor;
    float _TessPhongStrength;
    float _TessNormalSmooth;
    float _TessDistanceMin;
    float _TessDistanceMax;
    float _TessDispStrength;
    float _TessDispOffset;
    #endif

    // Smear Effect (スミア / 残像エフェクト)
    #if defined(_SMEAR)
    float _SmearStretch;
    float4 _SmearDirection;
    float _SmearNoiseScale;
    float _SmearNoiseStrength;
    float _SmearTrailLength;
    float _SmearTrailFade;
    half4 _SmearGlowColor;
    float _SmearGlowIntensity;
    float _SmearGlowPower;
    float _SmearEmission;
    half4 _SmearEmissionColor;
    float _SmearBlend;
    float _SmearBlendMode;
    float _SmearBlur;
    float4 _SmearMaskScrollSpeed;
    float _SmearMaskRotateSpeed;
    float _SmearAutoMagnitude;
    float _SmearMotionSensitivity;
    float _SmearVATVelocity;
    #endif

    // ===== 11. Fur (Shell-Based) =====
    #if defined(_FUR)
    float _FurLength;
    float _FurDensity;
    float _FurAlphaCutoff;
    float _FurGravity;
    half4 _FurRootColor;
    half4 _FurTipColor;
    float _FurColorBlend;
    float _FurAO;
    float _FurShadowStrength;
    float4 _FurWindDirection;
    float _FurWindSpeed;
    float _FurWindStrength;
    float _FurSpecular;
    float _FurRimLight;
    float _FurLODDistance;
    float _FurLODMinLayers;
    float4 _FurNoiseTex_ST;
    #endif

    // ===== 12. Background Mode (背景モード) =====
    #if defined(_BACKGROUND_MODE)
        float _LightmapToonInfluence;
        float _LightmapIntensity;
    #endif

    // ===== 13. PBR Mode (物理ベースレンダリング) =====
    #if defined(_PBR)
        float _PBR_Metallic;
        float _PBR_Smoothness;
        float _PBR_OcclusionStrength;
        float _PBR_ReflectionIntensity;
    #endif

    // ===== 14. Detail Map (ディテールマップ) =====
    #if defined(_DETAIL_MAP)
        float _DetailNormalScale;
        float _DetailAlbedoScale;
        float _DetailUVSet;
        float _DetailTiling;
    #endif

    // ===== 15. Triplanar Mapping (トライプレーナー) =====
    #if defined(_TRIPLANAR)
        float _TriplanarScale;
        float _TriplanarBlendSharpness;
        float _TriplanarOffsetX;
        float _TriplanarOffsetY;
        float _TriplanarOffsetZ;
    #endif

    // ===== 16. Height Fog (ハイトフォグ) =====
    #if defined(_HEIGHT_FOG)
        half4 _HeightFogColor;
        float _HeightFogStart;
        float _HeightFogEnd;
        float _HeightFogDensity;
        float _HeightFogMode;
    #endif

    // ===== 17. Surface Cover (雪/砂堆積) =====
    #if defined(_SURFACE_COVER)
        half4 _CoverColor;
        float _CoverAmount;
        float _CoverThreshold;
        float _CoverBlendSharpness;
        float _CoverTiling;
        float4 _CoverDirection;
    #endif

    // ===== 18. Mirror / Camera Control (ミラー・カメラ制御) =====
    #if defined(_MIRROR_CONTROL)
        float _MirrorMode;
        float _CameraMode;
        float _MirrorEmissionMultiplier;
    #endif

    // ===== 19. Quest Lite (Quest軽量パス) =====
    // No CBUFFER properties needed - keyword only

    // ===== 20. PCSS (Percentage Closer Soft Shadows) =====
    #if defined(_PCSS)
    float _PCSSLightSize;
    float _PCSSSoftness;
    float _PCSSBlockerSearchRadius;
    float _PCSSMinFilterRadius;
    float _PCSSMaxFilterRadius;
    float _PCSSSampleCount;
    float _PCSSBlendMode;
    float _PCSSBlend;
    float _PCSSBlur;
    #endif

    // ===== 21. Illustration Style (イラスト風技法) =====
    #ifdef _COLOR_QUANTIZE
    float _QuantizeMode;
    float _QuantizeLevels;
    float _QuantizeHueLevels;
    float _QuantizeSatLevels;
    float _QuantizeValLevels;
    float _QuantizeDither;
    float _QuantizeBlend;
    #endif
    #ifdef _LUT_3D
    float _LUT3DIntensity;
    float _LUT3DSize;
    #endif
    #ifdef _HATCHING
    float _HatchingTiling;
    float4 _HatchingColor;
    float _HatchingBlend;
    #endif
    #ifdef _WATERCOLOR
    float _WCEdgeDarkening;
    float _WCWetEdge;
    float _WCGranulation;
    float _WCPaperIntensity;
    float _WCPaperTiling;
    float _WCBlend;
    float4 _WCGranulationTex_ST;
    float4 _WCPaperTex_ST;
    float4 _WCMask_ST;
    #endif
    #ifdef _SOFT_FILTER
    float _SoftFilterRadius;
    float _SoftFilterBlend;
    float _SoftFilterThreshold;
    float _SoftFilterMode;
    #endif
    #ifdef _KUWAHARA_FILTER
    float _KuwaharaRadius;
    float _KuwaharaBlend;
    #endif
    #ifdef _SCREEN_EDGE
    float4 _EdgeColor;
    float _EdgeWidth;
    float _EdgeDepthSensitivity;
    float _EdgeNormalSensitivity;
    float _EdgeBlend;
    #endif
    #ifdef _COLOR_BLEEDING
    float _BleedingRadius;
    float _BleedingBlend;
    #endif
    #ifdef _CHROMATIC_ABERRATION
    float _CAIntensity;
    float _CABlend;
    #endif
    #ifdef _OUTLINE_HAND_DRAWN
    float _OutlineNoiseTiling;
    float _OutlineWidthVariation;
    float _OutlineJitterAmount;
    #endif
    #if defined(_PROCEDURAL_MATCAP)
    half4 _ProcMatCapColor;
    float _ProcMatCapPower;
    float _ProcMatCapIntensity;
    float _ProcMatCapFresnelPower;
    float _ProcMatCapBlend;
    float _ProcMatCapBlendMode;
    #endif
    #if defined(_FAKE_REFLECTION)
    half4 _FakeReflSkyColor;
    half4 _FakeReflGroundColor;
    float _FakeReflIntensity;
    float _FakeReflFresnelPower;
    float _FakeReflSmoothness;
    float _FakeReflBlend;
    float _FakeReflBlendMode;
    #endif
    #if defined(_SHADOW_EDGE_NOISE)
    float _ShadowNoiseScale;
    float _ShadowNoiseIntensity;
    float _ShadowNoiseSpeed;
    #endif
    #if defined(_LIGHT_SNAP)
    float _LightSnapAngle;
    float _LightSnapSmooth;
    #endif
    #if defined(_CAST_SHADOW_COLOR)
    half4 _CastShadowTint;
    float _CastShadowIntensity;
    #endif
    #if defined(_PERSPECTIVE_FLAT)
    float _PerspectiveFlatAmount;
    #endif
    #if defined(_DEPTH_COLOR_FADE)
    half4 _DepthFadeColor;
    float _DepthFadeStart;
    float _DepthFadeEnd;
    float _DepthFadeIntensity;
    float _DepthFadeDesaturation;
    #endif

    // ===== Feature Toggle Properties =====
    // [Toggle(_KEYWORD)] properties used for runtime feature guards.
    // These must be in the CBUFFER for the if(_Prop >= 0.5) checks
    // in Fragment.hlsl to compile.
    float _MirrorControl;
    float _MainTexAnimation;
    float _GlitchStretch;
    float _Triplanar;
    float _GradientBaseColor;
    float _Use2ndTexture;
    float _Use3rdTexture;
    float _Use4thTexture;
    float _Use5thTexture;
    float _SurfaceCover;
    float _ScreenTone;
    float _UseNormalMap;
    float _DetailMap;
    float _NormalWarp;
    float _UseShadowReceiveMask;
    float _LightSnap;
    float _DistanceFade;
    float _UsePCSS;
    float _VertexColorShadow;
    float _ProceduralAO;
    float _ShadowEdgeNoise;
    float _CastShadowColorEnable;
    float _HalftoneShadow;
    float _UseColorQuantize;
    float _UseLUT3D;
    float _UseHatching;
    float _AngelRing;
    // _SSS: property name == keyword name (_SSS).
    // Unity auto-generates the uniform from Properties block,
    // so explicit CBUFFER declaration would cause redefinition.
    // When keyword active: _SSS is preprocessor define (1) → if(1 >= 0.5) → true.
    // When keyword inactive: _SSS is the auto-generated float uniform.
    float _RimLight;
    float _RimLight2;
    float _OffsetRimLight;
    float _Sheen;
    float _EnvRim;
    float _Specular;
    float _HairSpecular;
    float _MatCap;
    float _MatCap2;
    float _MatCap3;
    float _ProceduralMatCap;
    float _Reflection;
    float _FakeReflection;
    float _Refraction;
    float _Emission;
    float _HueShiftEnable;
    float _AudioLink;
    float _Glitter;
    float _Iridescence;
    float _Smear;
    float _WaterDrip;
    float _Hologram;
    float _Glitch;
    float _Decal;
    float _Dissolve;
    float _UseAlphaMask;
    float _HeightFade;
    float _IntersectionFade;
    float _HeightFog;
    float _DepthColorFade;
    float _HashedAlpha;
    float _DitheringAlpha;
    float _UseWatercolor;
    float _UseSoftFilter;
    float _UseKuwahara;
    float _UseScreenEdge;
    float _UseColorBleeding;
    float _UseChromaticAberration;

CBUFFER_END

// ===== VRChat Shader Globals (set by VRChat runtime) =====
// These uniform variables are provided by VRChat and available in all worlds.
// Always declared globally so view-dependent effects can compensate for mirror flipping,
// regardless of whether the user has enabled the Mirror/Camera Control feature.
// Reference: https://creators.vrchat.com/worlds/udon/vrc-graphics/vrchat-shader-globals/

// Mirror Detection
// Guarded: may already be declared by NataneToonUtils.hlsl for standalone passes (e.g. FurShell)
#ifndef NATANE_VRCHAT_GLOBALS_DECLARED
#define NATANE_VRCHAT_GLOBALS_DECLARED
float _VRChatMirrorMode;      // 0=Normal, 1=Mirror(VR), 2=Mirror(Desktop)
float _VRChatCameraMode;      // 0=Normal, 1=VR handheld camera, 2=Desktop camera, 3=Screenshot
#endif
float _VRChatFaceMirrorMode;  // Face mirror mode

// Camera Detection (extended - always declared here)
float _VRChatCameraMask;      // Camera layer mask

// Camera Positions & Rotations
float3 _VRChatMirrorCameraPos;    // Mirror camera world position
float3 _VRChatScreenCameraPos;    // Screen camera world position
float4 _VRChatScreenCameraRot;    // Screen camera rotation (quaternion)
float3 _VRChatPhotoCameraPos;     // Photo camera world position
float4 _VRChatPhotoCameraRot;     // Photo camera rotation (quaternion)

// Texture samplers (must be outside CBUFFER per HLSL specification)
// Main
sampler2D _MainTex;
#if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;
#endif

// ===== NOSAMPLER Sampling Macros =====
// Group A (Repeat+Bilinear): share sampler_linear_repeat
// Group B (Clamp+Bilinear):  share sampler_linear_clamp
#if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
#ifndef NATANE_SAMPLE_SHARED
    // Use Unity's inline sampler names so tessellation/vertex programs do not rely on
    // a texture-bound sampler declaration that may not exist for NOSAMPLER textures.
    #define NATANE_SAMPLE_SHARED(tex, samplerTex, coord) tex.Sample(sampler_linear_repeat, coord)
    #define NATANE_SAMPLE_SHARED_R(tex, samplerTex, coord) NATANE_SAMPLE_SHARED(tex, samplerTex, coord).r
#endif

    #define NATANE_SAMPLE_REPEAT(tex, uv)   tex.Sample(sampler_linear_repeat, uv)
    #define NATANE_SAMPLE_REPEAT_R(tex, uv) NATANE_SAMPLE_REPEAT(tex, uv).r
    #define NATANE_SAMPLE_CLAMP(tex, uv)    tex.Sample(sampler_linear_clamp, uv)
    #define NATANE_SAMPLE_CLAMP_R(tex, uv)  NATANE_SAMPLE_CLAMP(tex, uv).r

    // tex2Dlod replacements for vertex/tessellation/fur shell
    #define NATANE_SAMPLE_REPEAT_LOD(tex, uv, lod) tex.SampleLevel(sampler_linear_repeat, uv, lod)
    #define NATANE_SAMPLE_CLAMP_LOD(tex, uv, lod)  tex.SampleLevel(sampler_linear_clamp, uv, lod)
#else
    #ifndef NATANE_SAMPLE_SHARED
    #define NATANE_SAMPLE_SHARED(tex, samplerTex, coord) UNITY_SAMPLE_TEX2D_SAMPLER(tex, samplerTex, coord)
    #define NATANE_SAMPLE_SHARED_R(tex, samplerTex, coord) NATANE_SAMPLE_SHARED(tex, samplerTex, coord).r
    #endif

    #define NATANE_SAMPLE_REPEAT(tex, uv)   UNITY_SAMPLE_TEX2D_SAMPLER(tex, _MainTex, uv)
    #define NATANE_SAMPLE_REPEAT_R(tex, uv) NATANE_SAMPLE_REPEAT(tex, uv).r
    #define NATANE_SAMPLE_CLAMP(tex, uv)    UNITY_SAMPLE_TEX2D_SAMPLER(tex, _linear_clamp, uv)
    #define NATANE_SAMPLE_CLAMP_R(tex, uv)  NATANE_SAMPLE_CLAMP(tex, uv).r

    #define NATANE_SAMPLE_REPEAT_LOD(tex, uv, lod) tex2Dlod(tex, float4(uv, 0, lod))
    #define NATANE_SAMPLE_CLAMP_LOD(tex, uv, lod)  tex2Dlod(tex, float4(uv, 0, lod))
#endif

// Makeup Textures
#if defined(_2ND_TEXTURE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_2ndTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_2ndTexMask);
#endif
#if defined(_3RD_TEXTURE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_3rdTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_3rdTexMask);
#endif
#if defined(_4TH_TEXTURE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_4thTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_4thTexMask);
#endif
#if defined(_5TH_TEXTURE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_5thTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_5thTexMask);
#endif

// Screen-Tone
#if defined(_SCREEN_TONE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_ScreenToneMask);
#endif

// Shading
#if defined(_USE_RAMP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_RampTex);
#endif
#if defined(_SHADOW_RECEIVE_MASK)
UNITY_DECLARE_TEX2D_NOSAMPLER(_ShadowReceiveMask);
#endif
UNITY_DECLARE_TEX2D_NOSAMPLER(_ShadowColorTex);

// SDF & Grade Maps
#if defined(_SDF_MAP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_SDFMap);
#endif
#if defined(_SHADING_GRADE_MAP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_ShadingGradeMap);
#endif

// Ambient Occlusion
#if defined(_USE_AO)
UNITY_DECLARE_TEX2D_NOSAMPLER(_AOMap);
#endif
UNITY_DECLARE_TEX2D_NOSAMPLER(_CavityMap);

// Specular
#if defined(_SPECULAR)
UNITY_DECLARE_TEX2D_NOSAMPLER(_SpecularMask);
#endif
UNITY_DECLARE_TEX2D_NOSAMPLER(_SkinSpecMask);

// Hair Specular
#if defined(_HAIR_SPECULAR)
UNITY_DECLARE_TEX2D_NOSAMPLER(_HairSpecMask);
UNITY_DECLARE_TEX2D_NOSAMPLER(_HairSpecShiftTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_HairStrandDirectionMap);
UNITY_DECLARE_TEX2D_NOSAMPLER(_HairTransmissionMask);
#endif

// Angel Ring
#if defined(_ANGEL_RING)
UNITY_DECLARE_TEX2D_NOSAMPLER(_AngelRingTex);
#endif

// Rim Light
#if defined(_RIM_LIGHT)
UNITY_DECLARE_TEX2D_NOSAMPLER(_RimMask);
#endif
#if defined(_RIM_LIGHT_2)
UNITY_DECLARE_TEX2D_NOSAMPLER(_RimMask2);
#endif
#if defined(_OFFSET_RIM_LIGHT)
UNITY_DECLARE_TEX2D_NOSAMPLER(_OffsetRimMask);
#endif
#if defined(_SHEEN)
UNITY_DECLARE_TEX2D_NOSAMPLER(_SheenMask);
#endif

// MatCap
#if defined(_MATCAP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_MatCapTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_MatCapMask);
#endif
#if defined(_MATCAP_2)
UNITY_DECLARE_TEX2D_NOSAMPLER(_MatCapTex2);
UNITY_DECLARE_TEX2D_NOSAMPLER(_MatCapMask2);
#endif
#if defined(_MATCAP_3)
UNITY_DECLARE_TEX2D_NOSAMPLER(_MatCapTex3);
UNITY_DECLARE_TEX2D_NOSAMPLER(_MatCapMask3);
#endif

// Glitter
#if defined(_GLITTER)
UNITY_DECLARE_TEX2D_NOSAMPLER(_GlitterMask);
#endif

// Outline
#if defined(_OUTLINE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_OutlineMask);
#endif

// Emission
#if defined(_EMISSION)
UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMap);
#endif
#if defined(_EMISSION)
UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMask);
#endif

// Normal Map
#if defined(_NORMALMAP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_BumpMap);
#endif
UNITY_DECLARE_TEX2D_NOSAMPLER(_MicroNormalMap);

// Subsurface Scattering
#if defined(_SSS)
UNITY_DECLARE_TEX2D_NOSAMPLER(_ThicknessMap);
UNITY_DECLARE_TEX2D_NOSAMPLER(_TransmissionMask);
UNITY_DECLARE_TEX2D_NOSAMPLER(_SSSMask);
#endif

// SSS LUT
#if defined(_SSS_LUT)
UNITY_DECLARE_TEX2D_NOSAMPLER(_SSSLUTTex);
#endif

// Dissolve
#if defined(_DISSOLVE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_DissolveTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_DissolveMask);
#endif

// Alpha Mask
#if defined(_ALPHA_MASK)
UNITY_DECLARE_TEX2D_NOSAMPLER(_AlphaMask);
#endif

// Reflection
#if defined(_REFLECTION)
UNITY_DECLARE_TEX2D_NOSAMPLER(_ReflectionMask);
#endif
UNITY_DECLARE_TEX2D_NOSAMPLER(_ClearCoatMask);
UNITY_DECLARE_TEX2D_NOSAMPLER(_ClearCoatNormalMap);

// Iridescence
#if defined(_IRIDESCENCE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_IridescenceMask);
#endif

// Environmental Rim
#if defined(_ENV_RIM)
UNITY_DECLARE_TEX2D_NOSAMPLER(_EnvRimMask);
#endif

// Parallax
#if defined(_PARALLAX)
UNITY_DECLARE_TEX2D_NOSAMPLER(_ParallaxMap);
#endif

// Refraction
#if defined(_REFRACTION)
UNITY_DECLARE_TEX2D_NOSAMPLER(_RefractionMask);
#endif

// Decal
#if defined(_DECAL)
UNITY_DECLARE_TEX2D_NOSAMPLER(_DecalTex);
#endif

// Backface
#if defined(_BACKFACE_TEXTURE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_BackfaceTex);
#endif

// Video
#if defined(_VIDEO_TEXTURE)
sampler2D _VideoTex;
#endif

// Vertex Animation
#if defined(_VERTEX_ANIMATION)
UNITY_DECLARE_TEX2D_NOSAMPLER(_VertexAnimMask);
#endif

// Water Drip
#if defined(_WATER_DRIP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_DripMask);
#endif

// Smear
#if defined(_SMEAR)
UNITY_DECLARE_TEX2D_NOSAMPLER(_SmearMask);
#endif

// Fur
#if defined(_FUR)
UNITY_DECLARE_TEX2D_NOSAMPLER(_FurNoiseTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_FurMask);
#endif

// PBR
#if defined(_PBR)
UNITY_DECLARE_TEX2D_NOSAMPLER(_PBR_MetallicGlossMap);
UNITY_DECLARE_TEX2D_NOSAMPLER(_PBR_OcclusionMap);
#endif

// Smooth Normal Texture (for Mode 2: Baked Normal Texture)
#if defined(_SMOOTH_NORMAL)
UNITY_DECLARE_TEX2D_NOSAMPLER(_SmoothNormalTex);
#endif

// Hologram (conditionally compiled)
#if defined(_HOLOGRAM)
UNITY_DECLARE_TEX2D_NOSAMPLER(_HologramMask);
#endif
#if defined(_HOLOGRAM_NOISE)
UNITY_DECLARE_TEX2D_NOSAMPLER(_HologramNoiseTex);
#endif

// Glitch Mask
#if defined(_GLITCH)
UNITY_DECLARE_TEX2D_NOSAMPLER(_GlitchMask);
UNITY_DECLARE_TEX2D_NOSAMPLER(_GlitchNoiseTex);
#endif

// Glitch Stretch Mask
#if defined(_GLITCH_STRETCH)
UNITY_DECLARE_TEX2D_NOSAMPLER(_GlitchStretchMask);
#endif

// Illustration Style Textures
#ifdef _COLOR_QUANTIZE
UNITY_DECLARE_TEX2D_NOSAMPLER(_QuantizeMask);
#endif
#ifdef _LUT_3D
UNITY_DECLARE_TEX2D_NOSAMPLER(_LUT3DTex);
#endif
#ifdef _HATCHING
UNITY_DECLARE_TEX2D_NOSAMPLER(_HatchTex0);
UNITY_DECLARE_TEX2D_NOSAMPLER(_HatchTex1);
UNITY_DECLARE_TEX2D_NOSAMPLER(_HatchingMask);
#endif
#ifdef _WATERCOLOR
UNITY_DECLARE_TEX2D_NOSAMPLER(_WCGranulationTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_WCPaperTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_WCMask);
#endif
#ifdef _SCREEN_EDGE
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture);
#endif
#ifdef _OUTLINE_HAND_DRAWN
UNITY_DECLARE_TEX2D_NOSAMPLER(_OutlineNoiseTex);
#endif

// VAT
#if defined(_VAT)
UNITY_DECLARE_TEX2D_NOSAMPLER(_VATPositionMap);
UNITY_DECLARE_TEX2D_NOSAMPLER(_VATNormalMap);
#endif

// Tessellation Displacement
#if defined(_TESS_DISPLACEMENT)
UNITY_DECLARE_TEX2D_NOSAMPLER(_TessDispMap);
#endif

// Detail Map
#if defined(_DETAIL_MAP)
UNITY_DECLARE_TEX2D_NOSAMPLER(_DetailAlbedoMap);
UNITY_DECLARE_TEX2D_NOSAMPLER(_DetailNormalMap);
#endif

// Surface Cover
#if defined(_SURFACE_COVER)
UNITY_DECLARE_TEX2D_NOSAMPLER(_CoverTex);
UNITY_DECLARE_TEX2D_NOSAMPLER(_CoverNormalMap);
#endif

// Cubemap samplers (outside CBUFFER)
#if defined(_REFLECTION)
samplerCUBE _ReflectionCube;
#endif
#if defined(_ENV_RIM)
samplerCUBE _EnvRimCube;
#endif

// Intersection Fade / PCSS / Screen Edge — shared _CameraDepthTexture declaration
#if defined(_INTERSECTION_FADE) || defined(_PCSS) || defined(_SCREEN_EDGE)
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
#endif

// PCSS Poisson Disk sampling pattern (32 samples)
#if defined(_PCSS)
static const float2 PoissonDisk32[32] = {
    float2(-0.94201624, -0.39906216), float2( 0.94558609, -0.76890725),
    float2(-0.09418410, -0.92938870), float2( 0.34495938,  0.29387760),
    float2(-0.91588581,  0.45771432), float2(-0.81544232, -0.87912464),
    float2(-0.38277543,  0.27676845), float2( 0.97484398,  0.75648379),
    float2( 0.44323325, -0.97511554), float2( 0.53742981, -0.47373420),
    float2(-0.26496911, -0.41893023), float2( 0.79197514,  0.19090188),
    float2(-0.24188840,  0.99706507), float2(-0.81409955,  0.91437590),
    float2( 0.19984126,  0.78641367), float2( 0.14383161, -0.14100790),
    float2(-0.44451373, -0.69745003), float2( 0.69546413, -0.16150797),
    float2(-0.65731890,  0.68906659), float2( 0.36949191,  0.56157024),
    float2(-0.10214935, -0.18408868), float2( 0.83618390,  0.48918439),
    float2(-0.56318188, -0.29645988), float2( 0.27004808, -0.68117476),
    float2(-0.73403048,  0.09498067), float2( 0.47089072,  0.97014981),
    float2(-0.95723576, -0.09001040), float2( 0.09698980,  0.41944910),
    float2( 0.60395759, -0.74880689), float2(-0.47938831,  0.56906949),
    float2( 0.85680531,  0.91739876), float2(-0.33726816, -0.97149441)
};
#endif

// GrabPass texture for Refraction — declared in NataneToonUtils.hlsl (VR stereo-aware)
// Uses "_nataneBackgroundTexture" named GrabPass (unique name to avoid interference with other shaders)

// AudioLink texture (VRChat)
#if defined(_AUDIOLINK)
sampler2D _AudioTexture;
sampler2D _AudioTexture2D;
#endif

// Vertex Input Structure
struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv : TEXCOORD0;
    #if defined(_BACKGROUND_MODE) || defined(_DETAIL_MAP) || defined(_LTCGI)
        float2 uv1 : TEXCOORD1;  // Lightmap UV / Detail UV / LTCGI UV
    #endif
    #if defined(_SMOOTH_NORMAL) || defined(_VERTEX_COLOR_SHADOW)
        float4 color : COLOR;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Vertex to Fragment Structure
struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    float3 worldTangent : TEXCOORD3;
    float3 worldBinormal : TEXCOORD4;
    UNITY_FOG_COORDS(5)
    SHADOW_COORDS(6)
    #if defined(_REFRACTION) || defined(_PARALLAX) || defined(_DISSOLVE) || defined(_DITHERING_ALPHA) || defined(_HASHED_ALPHA) || defined(_INTERSECTION_FADE) || defined(_SOFT_FILTER) || defined(_KUWAHARA_FILTER) || defined(_COLOR_BLEEDING) || defined(_CHROMATIC_ABERRATION) || defined(_SCREEN_EDGE) || defined(_WATERCOLOR) || defined(_SPECULAR_DITHER)
        float4 screenPos : TEXCOORD7; // For GrabPass / Dithering / Intersection Fade / Illustration Style / Specular Dither
    #endif
    #if defined(VERTEXLIGHT_ON) && !defined(_PIXEL_VERTEX_LIGHTS)
        float3 vertexLightColor : TEXCOORD8;
    #endif
    #ifdef _SMOOTH_NORMAL
        float3 smoothWorldNormal : TEXCOORD9;
    #endif
    #ifdef _SMEAR
        float smearStretchFactor : TEXCOORD10;
    #endif
    #ifdef _BACKGROUND_MODE
        float2 lightmapUV : TEXCOORD11;
    #endif
    #if defined(_DETAIL_MAP) || defined(_LTCGI)
        float2 uv1 : TEXCOORD12;
    #endif
    #ifdef _VERTEX_COLOR_SHADOW
        half4 color : COLOR;
    #endif
    #if defined(_PROCEDURAL_AO) || defined(_NORMAL_WARP)
        float3 objectPos : TEXCOORD13;
    #endif
    #if defined(_NORMAL_WARP)
        float3 objectNormal : TEXCOORD14;
    #endif
    UNITY_VERTEX_OUTPUT_STEREO
};

#endif // NATANE_TOON_INPUT_INCLUDED
