Shader "Natane/Eye"
{
    Properties
    {
        [Header(___________________________________________________)]
        [Header(Eye State Settings)]
        [Space(5)]
        [KeywordEnum(Normal, Star, Heart, Dead, Nervous)]_EyeState ("Eye State", float) = 0

        [Header(___________________________________________________)]
        [Header(Main Eye Settings)]
        [Space(5)]
        _MainTex ("Base Texture", 2D) = "white" {}
        [Toggle(_USE_TEXTURE)] _UseTexture ("Use Texture", Float) = 0
        _TextureBlend ("Texture Blend", Range(0, 1)) = 1
        [Enum(Lerp,0,Multiply,1,Add,2,Screen,3,Overlay,4)] _BlendMode ("Blend Mode", Float) = 0
        _MainParallax("Parallax Strength", Range(0, 1)) = 1
        [HDR]_MainColor ("Glare Color", color) = (1,1,1,1)
        [HDR]_BackgroundColor ("Background Color", color) = (1,1,1,1)
        _PupilSize ("Pupil Size", Range(0, 1)) = .4
        _PupilAspect ("Pupil Aspect XY (1,1=Circle)", Vector) = (1,1,0,0)
        [Toggle] _UseRealisticEye ("Enable Realistic Eye", Float) = 0
        _IrisDepth ("Iris Depth", Range(-1, 1)) = -0.15
        _IrisDepthRadius ("Iris Depth Radius", Range(0.1, 1.5)) = 0.82
        [HDR]_LimbalRingColor ("Limbal Ring Color", Color) = (0.2, 0.28, 0.38, 1)
        _LimbalRingWidth ("Limbal Ring Width", Range(0.01, 0.4)) = 0.08
        _LimbalRingIntensity ("Limbal Ring Intensity", Range(0, 3)) = 0
        [HDR]_ScleraTint ("Sclera Tint", Color) = (1, 1, 1, 1)
        _ScleraShadowStrength ("Sclera Shadow Strength", Range(0, 1)) = 0
        [HDR]_CorneaSpecColor ("Cornea Spec Color", Color) = (1, 1, 1, 1)
        _CorneaSpecIntensity ("Cornea Spec Intensity", Range(0, 3)) = 0
        _CorneaSpecSmoothness ("Cornea Spec Smoothness", Range(0, 1)) = 0.92
        _CorneaFresnelPower ("Cornea Fresnel Power", Range(0.5, 8)) = 4

        [Header(___________________________________________________)]
        [Header(Dual Eye Centers)]
        [Space(5)]
        _EyeCenter1 ("Eye Center 1 (UV)", Vector) = (0.25, 0.5, 0, 0)
        _EyeCenter2 ("Eye Center 2 (UV)", Vector) = (0.75, 0.5, 0, 0)
        _EyeSeparationX ("Eye Separation X Threshold", Range(0, 1)) = 0.5
        [Enum(Split X Threshold,0,Nearest Center,1)] _EyeSelectMode ("Eye Select Mode", Float) = 0
        [Enum(Manual Dual Centers,0,Symmetry From Center1,1)] _EyeCenterMode ("Eye Center Mode", Float) = 0
        _EyeSymmetryPivotX ("Symmetry Pivot X", Range(0, 1)) = 0.5
        [Toggle] _MirrorRightEyeUV ("Mirror Right Eye UV", Float) = 0

        [Header(___________________________________________________)]
        [Header(Eye Region Mask)]
        [Space(5)]
        [Toggle] _UseEyeRegionMask ("Use Eye Region Mask", Float) = 0
        _EyeRegionMask ("Eye Region Mask", 2D) = "white" {}
        [Enum(R,0,G,1,B,2,A,3)] _EyeMaskChannel ("Mask Channel", Float) = 3
        _EyeMaskThreshold ("Mask Threshold", Range(0, 1)) = 0.5
        _EyeMaskSoftness ("Mask Softness", Range(0.001, 0.5)) = 0.05
        _EyeMaskInvert ("Mask Invert", Range(0, 1)) = 0

        [Header(___________________________________________________)]
        [Header(Eye State Normal)]
        [Space(5)]
        _NormalStateTex ("Normal State Texture", 2D) = "white" {}
        [HDR]_NormalPupilColor ("Pupil Color", Color) = (0,0,0,0)
        _DetailBrightness ("Detail Brightness", Range(0, 1)) = .2

        [Header(___________________________________________________)]
        [Header(Eye State Star)]
        [Space(5)]
        _StarStateTex ("Star State Texture", 2D) = "white" {}
        [HDR]_StarColor ("Star Color", color) = (1,1,1,1)
        _StarRockSharpness ("Rocking Sharpness", Range(0, 1)) = .4
        _StarRockAngle ("Rocking Angle", Range(0, 90)) = 12
        _StarRockSpeed ("Rocking Speed", Range(0, 1)) = .5

        [Header(___________________________________________________)]
        [Header(Eye State Heart)]
        [Space(5)]
        _HeartStateTex ("Heart State Texture", 2D) = "white" {}
        [HDR]_HeartPupilColor ("Heart Color", Color) = (1,1,1,1)
        _HeartPulseSpeed ("Pulse Speed", Range(0, 1)) = .5
        _HeartPulsePower ("Pulse Power", Range(0, 1)) = .3

        [Header(___________________________________________________)]
        [Header(Eye State Dead)]
        [Space(5)]
        _DeadStateTex ("Dead State Texture", 2D) = "white" {}
        _DeadGradientOffset ("Gradient Offset", Range(0, 1)) = 0
        _DeadTopColor ("Top Color", Color) = (0,0,0,0)
        _DeadBottomColor ("Bottom Color", Color) = (1,1,1,1)

        [Header(___________________________________________________)]
        [Header(Eye State Nervous)]
        [Space(5)]
        _NervousStateTex ("Nervous State Texture", 2D) = "white" {}
        [HDR]_NervousBackgroundColor ("Background Color", Color) = (1,1,1,1)
        [HDR]_NervousLinesColor ("Lines Color", Color) = (0,0,0,0)
        _NervousLinesRandSeed ("Random Seed", float) = 0
        _NervousLinesRandOffs ("Lines Offset", Range(0, 1)) = .1
        _NervousLinesSize ("Lines Size", Range(0, 1)) = .8
        _NervousLinesThickness ("Lines Thickness", Range(0, 1)) = .1
        _NervousCenterFill ("Center Fill", Range(0, 1)) = 0

        [Header(___________________________________________________)]
        [Header(Hue and Color Settings)]
        [Space(5)]
        _Contrast ("Contrast", Range(0, 1)) = .5
        _MainSaturation("Saturation", Range(0, 1)) = 0
        _MainHueShift("Hue Offset", Range(0, 1)) = 0
        _MainHueSpeed("Hue Speed", Range(0, 2)) = 0

        [Header(___________________________________________________)]
        [Header(Bubble Effect)]
        [Space(5)]
        _BubbleSize ("Bubble Size", Range(0, 1)) = .08
        _BubbleBrightness ("Bubble Brightness", Range(0, 1)) = .1
        _BubbleWobbleSpeed ("Wobble Speed", Range(0, 1)) = .5
        _BubbleWobbleStrength ("Wobble Strength", Range(0, 1)) = .1

        [Header(___________________________________________________)]
        [Header(Iris Caustics)]
        [Space(5)]
        [Toggle] _UseIrisCaustics ("Enable Iris Caustics", Float) = 0
        [HDR]_IrisCausticsColor ("Caustics Color", Color) = (0.4, 0.9, 1.2, 1)
        _IrisCausticsIntensity ("Caustics Intensity", Range(0, 3)) = 0.8
        _IrisCausticsScale ("Caustics Scale", Range(1, 64)) = 18
        _IrisCausticsSpeed ("Caustics Speed", Range(0, 4)) = 1
        _IrisCausticsParallax ("Caustics Parallax", Range(-1, 1)) = -0.18
        _IrisCausticsTwist ("Caustics Twist", Range(0, 8)) = 2.5
        [Enum(Add,2,Screen,3,Overlay,4)] _IrisCausticsBlendMode ("Caustics Blend Mode", Float) = 3

        [Header(___________________________________________________)]
        [Header(Iris Ring Pulse)]
        [Space(5)]
        [Toggle] _UseIrisRingPulse ("Enable Iris Ring Pulse", Float) = 0
        [HDR]_IrisRingColor ("Ring Color", Color) = (0.9, 1.2, 1.8, 1)
        _IrisRingRadius ("Ring Radius", Range(0.1, 1.5)) = 0.78
        _IrisRingWidth ("Ring Width", Range(0.01, 0.5)) = 0.08
        _IrisRingPulseSpeed ("Ring Pulse Speed", Range(0, 6)) = 1.2
        _IrisRingPulseAmount ("Ring Pulse Amount", Range(0, 0.4)) = 0.08
        _IrisRingParallax ("Ring Parallax", Range(-1, 1)) = -0.12
        _IrisRingIntensity ("Ring Intensity", Range(0, 3)) = 1
        [Enum(Add,2,Screen,3,Overlay,4)] _IrisRingBlendMode ("Ring Blend Mode", Float) = 2

        [Header(___________________________________________________)]
        [Header(Texture Composite Polish)]
        [Space(5)]
        [Toggle] _UseTexturePolish ("Enable Texture Polish", Float) = 0
        [Enum(Lerp,0,Add,2,Screen,3,Overlay,4)] _TexturePolishBlendMode ("Polish Blend Mode", Float) = 3
        _TexturePolishStrength ("Polish Strength", Range(0, 1)) = 0.65
        _TexturePolishContrast ("Polish Contrast", Range(-1, 1)) = 0.15
        _TexturePolishSaturation ("Polish Saturation", Range(0, 2)) = 1.15
        _TexturePolishIrisFocus ("Polish Iris Focus", Range(0, 1)) = 0.7
        [HDR]_TexturePolishTint ("Polish Tint", Color) = (1, 1, 1, 1)

        [Header(___________________________________________________)]
        [Header(Expression Overlays)]
        [Space(5)]
        [Enum(Custom,0,Normal,1,Surprised,2,Crying,3)] _ExpressionPreset ("Expression Preset", Float) = 0
        [Enum(Off,0,Spiral,1,Tearful,2,Shock Rings,3)] _ExpressionMode ("Expression Mode", Float) = 0
        [HDR]_ExpressionColor ("Expression Color", Color) = (1, 1, 1, 1)
        _ExpressionIntensity ("Expression Intensity", Range(0, 3)) = 1
        _ExpressionScale ("Expression Scale", Range(1, 32)) = 10
        _ExpressionSpeed ("Expression Speed", Range(0, 6)) = 1.2
        _ExpressionParallax ("Expression Parallax", Range(-1, 1)) = -0.2
        _ExpressionDetail ("Expression Detail", Range(0.1, 8)) = 2
        _SpiralTightness ("Spiral Tightness", Range(1, 24)) = 10
        _SpiralLineWidth ("Spiral Line Width", Range(0.05, 1)) = 0.55
        _TearFlow ("Tear Flow", Range(0, 4)) = 1.2
        _TearRim ("Tear Rim Highlight", Range(0, 1)) = 0.6
        _ShockRingCount ("Shock Ring Count", Range(1, 8)) = 3
        _ShockRingWidth ("Shock Ring Width", Range(0.01, 0.5)) = 0.08
        [Enum(Add,2,Screen,3,Overlay,4)] _ExpressionBlendMode ("Expression Blend Mode", Float) = 3

        [Header(___________________________________________________)]
        [Header(Inner Mesh Priority (BlendShape))]
        [Space(5)]
        [Toggle] _UseInnerMeshPriority ("Use Inner Mesh Priority", Float) = 0
        _InnerMeshPriorityMask ("Inner Mesh Priority Mask", 2D) = "black" {}
        [Enum(R,0,G,1,B,2,A,3)] _InnerMeshPriorityChannel ("Priority Mask Channel", Float) = 0
        _InnerMeshPriorityThreshold ("Priority Mask Threshold", Range(0, 1)) = 0.5
        _InnerMeshPrioritySoftness ("Priority Mask Softness", Range(0.001, 0.5)) = 0.05
        _InnerMeshPriorityInvert ("Priority Mask Invert", Range(0, 1)) = 0
        [Enum(Blend To Base,0,Soft Alpha Fade,1,Cutout,2)] _InnerMeshPriorityMode ("Priority Mode", Float) = 2
        _InnerMeshPriorityStrength ("Priority Strength", Range(0, 1)) = 1

        [Header(___________________________________________________)]
        [Header(Performance)]
        [Space(5)]
        [Enum(Quality,0,Balanced,1,Lite,2)] _PerformanceTier ("Performance Tier", Float) = 0

        [Header(___________________________________________________)]
        [Header(Vignette Effect)]
        [Space(5)]
        [Toggle]_VignetteTransparency ("Enable Transparency", float) = 0
        _VignetteDitherScale ("Dither Scale", Range(0, 1)) = .5
        _VignetteThickness("Thickness", Range(0, 1)) = .4
        _VignetteFalloff("Falloff", Range(0, 1)) = 0.15

        [Header(___________________________________________________)]
        [Header(Audio Link)]
        [Space(5)]
        [Enum(Lows,0,Mids,2,Highs,3,Average,4)] _BandSelection("Audio Band", Float) = 0
        _Intensity("Intensity", Range(0, 10)) = 1.0
        _MinValue("Minimum Brightness", Range(0, 1)) = 1

        [Header(___________________________________________________)]
        [Header(Rendering Settings)]
        [Space(5)]
        [IntRange]_StencilRef("Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("Compare", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)]_StencilPass("Pass", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)]_StencilFail("Fail", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)]_StencilZFail("ZFail", Float) = 0
        [IntRange]_StencilReadMask("Read Mask", Range(0, 255)) = 255
        [IntRange]_StencilWriteMask("Write Mask", Range(0, 255)) = 255

        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.BlendOp)]_BlendOp("Blend Operation", Float) = 0
        [Enum(Standard,0,Dithered Stable,1)] _TransparencyResolveMode ("Transparency Resolve Mode", Float) = 0
        _TransparencyDitherScale ("Transparency Dither Scale", Range(0.25, 4)) = 1
        _GlobalOpacity ("Global Opacity", Range(0, 1)) = 1

        [Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("ZTest", Float) = 4
        [Toggle]_ZWrite("ZWrite", Float) = 1
        _OffsetFactor("Offset Factor", Float) = 0
        _OffsetUnits("Offset Units", Float) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0

        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Culling Mode", Float) = 2
        [Enum(RGBA,15, RGB,14, A,1, 0,0)]_ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "PreviewType" = "Plane" }
        LOD 100

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass [_StencilPass]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Blend [_SrcBlend] [_DstBlend]
        BlendOp [_BlendOp]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Offset [_OffsetFactor], [_OffsetUnits]
        Cull [_Cull]
        ColorMask [_ColorMask]
        AlphaTest Greater [_AlphaClipThreshold]
        AlphaToMask True

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // AudioLink integration (optional dependency)
            #if defined(AUDIOLINK)
                #include "Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc"
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float2 viewDir : TEXCOORD2;
                float3 rawViewDir : TEXCOORD5;
                float4 screenPos : TEXCOORD3;
                float2 screenRatio : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _NormalStateTex;
            sampler2D _StarStateTex;
            sampler2D _HeartStateTex;
            sampler2D _DeadStateTex;
            sampler2D _NervousStateTex;
            sampler2D _EyeRegionMask;
            sampler2D _InnerMeshPriorityMask;

            float _UseTexture, _TextureBlend;
            int _BlendMode;

            float4 _MainColor, _BackgroundColor, _StarColor, _HeartPupilColor, _NormalPupilColor;
            float4 _DeadTopColor, _DeadBottomColor, _NervousBackgroundColor, _NervousLinesColor;

            float _MainParallax, _PupilSize;
            float4 _PupilAspect;
            float _UseRealisticEye, _IrisDepth, _IrisDepthRadius, _LimbalRingWidth, _LimbalRingIntensity;
            float _ScleraShadowStrength, _CorneaSpecIntensity, _CorneaSpecSmoothness, _CorneaFresnelPower;
            float4 _LimbalRingColor, _ScleraTint, _CorneaSpecColor;
            int _EyeState;

            float _DetailBrightness;
            float _StarRockSharpness, _StarRockAngle, _StarRockSpeed;
            float _HeartPulseSpeed, _HeartPulsePower;
            float _DeadGradientOffset;
            float _NervousLinesSize, _NervousLinesThickness, _NervousLinesRandOffs, _NervousLinesRandSeed, _NervousCenterFill;

            float _Contrast, _MainSaturation, _MainHueShift, _MainHueSpeed;
            float _BubbleSize, _BubbleBrightness, _BubbleWobbleSpeed, _BubbleWobbleStrength;
            float _UseIrisCaustics, _IrisCausticsIntensity, _IrisCausticsScale, _IrisCausticsSpeed, _IrisCausticsParallax, _IrisCausticsTwist;
            float _UseIrisRingPulse, _IrisRingRadius, _IrisRingWidth, _IrisRingPulseSpeed, _IrisRingPulseAmount, _IrisRingParallax, _IrisRingIntensity;
            float _UseTexturePolish, _TexturePolishStrength, _TexturePolishContrast, _TexturePolishSaturation, _TexturePolishIrisFocus;
            float _ExpressionPreset, _ExpressionMode, _ExpressionIntensity, _ExpressionScale, _ExpressionSpeed, _ExpressionParallax, _ExpressionDetail;
            float _SpiralTightness, _SpiralLineWidth, _TearFlow, _TearRim, _ShockRingCount, _ShockRingWidth;
            float _UseInnerMeshPriority, _InnerMeshPriorityChannel, _InnerMeshPriorityThreshold, _InnerMeshPrioritySoftness, _InnerMeshPriorityInvert;
            float _InnerMeshPriorityMode, _InnerMeshPriorityStrength;
            int _IrisCausticsBlendMode, _IrisRingBlendMode, _TexturePolishBlendMode, _ExpressionBlendMode;
            float4 _IrisCausticsColor, _IrisRingColor, _TexturePolishTint, _ExpressionColor;
            float _PerformanceTier, _TransparencyResolveMode, _TransparencyDitherScale, _GlobalOpacity;
            float _VignetteDitherScale, _VignetteTransparency, _VignetteFalloff, _VignetteThickness;
            float _BandSelection, _Intensity, _MinValue;

            float4 _EyeCenter1, _EyeCenter2;
            float _EyeSeparationX;
            float _EyeSelectMode;
            float _EyeCenterMode, _EyeSymmetryPivotX, _MirrorRightEyeUV;
            float _UseEyeRegionMask, _EyeMaskChannel, _EyeMaskThreshold, _EyeMaskSoftness, _EyeMaskInvert;

            // NTEye = NataneToon Eye shader prefix to avoid naming conflicts

            float2 NTEye_GetRightEyeCenter(float2 leftCenter)
            {
                return float2(2.0 * _EyeSymmetryPivotX - leftCenter.x, leftCenter.y);
            }

            float NTEye_GetEyeSplitX()
            {
                return (_EyeCenterMode > 0.5) ? _EyeSymmetryPivotX : _EyeSeparationX;
            }

            float2 NTEye_GetEyeCenter1()
            {
                return _EyeCenter1.xy;
            }

            float2 NTEye_GetEyeCenter2()
            {
                return (_EyeCenterMode > 0.5) ? NTEye_GetRightEyeCenter(_EyeCenter1.xy) : _EyeCenter2.xy;
            }

            float NTEye_DistanceSq(float2 p, float2 center)
            {
                float2 delta = p - center;
                return dot(delta, delta);
            }

            // Get the appropriate eye center based on UV position
            float2 NTEye_GetEyeCenter(float2 uv)
            {
                float2 center1 = NTEye_GetEyeCenter1();
                float2 center2 = NTEye_GetEyeCenter2();

                if (_EyeSelectMode > 0.5)
                {
                    return (NTEye_DistanceSq(uv, center1) <= NTEye_DistanceSq(uv, center2)) ? center1 : center2;
                }

                float splitX = NTEye_GetEyeSplitX();
                return uv.x < splitX ? center1 : center2;
            }

            float NTEye_IsRightEye(float2 uv)
            {
                float2 center1 = NTEye_GetEyeCenter1();
                float2 center2 = NTEye_GetEyeCenter2();

                if (_EyeSelectMode > 0.5)
                {
                    return step(NTEye_DistanceSq(uv, center2), NTEye_DistanceSq(uv, center1));
                }

                return step(NTEye_GetEyeSplitX(), uv.x);
            }

            float2 NTEye_GetPupilLocal(float2 uv, float2 center)
            {
                float2 aspect = max(_PupilAspect.xy, float2(0.001, 0.001));
                return ((uv - center) / max(_PupilSize, 0.0001)) / aspect;
            }

            float NTEye_GetPupilMask(float2 uv, float2 center)
            {
                float radial = length(NTEye_GetPupilLocal(uv, center));
                return saturate(0.025 / max(saturate(radial - 1.0), 1e-5));
            }

            void NTEye_PrepareEyeData(
                float2 inputUV,
                float2 inputViewDir,
                out float2 eyeCenter,
                out float2 sampleUV,
                out float2 sampleViewDir
            )
            {
                eyeCenter = NTEye_GetEyeCenter(inputUV);
                sampleUV = inputUV;
                sampleViewDir = inputViewDir;

                float isRightEye = NTEye_IsRightEye(inputUV);
                if (_MirrorRightEyeUV > 0.5 && isRightEye > 0.5)
                {
                    sampleUV.x = (2.0 * eyeCenter.x) - sampleUV.x;
                    sampleViewDir.x = -sampleViewDir.x;
                }
            }

            float NTEye_SelectMaskChannel(float4 maskSample, float channel)
            {
                if (channel < 0.5) return maskSample.r;
                if (channel < 1.5) return maskSample.g;
                if (channel < 2.5) return maskSample.b;
                return maskSample.a;
            }

            float NTEye_GetEyeRegionMask(float2 uv)
            {
                if (_UseEyeRegionMask < 0.5)
                {
                    return 1.0;
                }

                float4 maskSample = tex2D(_EyeRegionMask, uv);
                float maskValue = NTEye_SelectMaskChannel(maskSample, _EyeMaskChannel);

                if (_EyeMaskInvert > 0.5)
                {
                    maskValue = 1.0 - maskValue;
                }

                float softness = max(_EyeMaskSoftness, 0.0001);
                return smoothstep(_EyeMaskThreshold - softness, _EyeMaskThreshold + softness, maskValue);
            }

            float NTEye_GetInnerMeshPriorityMask(float2 uv)
            {
                if (_UseInnerMeshPriority < 0.5)
                {
                    return 0.0;
                }

                float4 maskSample = tex2D(_InnerMeshPriorityMask, uv);
                float maskValue = NTEye_SelectMaskChannel(maskSample, _InnerMeshPriorityChannel);

                if (_InnerMeshPriorityInvert > 0.5)
                {
                    maskValue = 1.0 - maskValue;
                }

                float softness = max(_InnerMeshPrioritySoftness, 0.0001);
                return smoothstep(_InnerMeshPriorityThreshold - softness, _InnerMeshPriorityThreshold + softness, maskValue);
            }

            float NTEye_GetPerformanceWeight()
            {
                if (_PerformanceTier > 1.5) return 0.45;
                if (_PerformanceTier > 0.5) return 0.7;
                return 1.0;
            }

            float NTEye_GetDitherNoise(float2 screenUV)
            {
                float2 pixel = floor(screenUV * _ScreenParams.xy / max(_TransparencyDitherScale, 0.001));
                return frac(sin(dot(pixel, float2(12.9898, 78.233))) * 43758.5453);
            }

            float2x2 NTEye_Rot(float angle)
            {
                float s = sin(radians(angle));
                float c = cos(radians(angle));
                return float2x2(c, -s, s, c);
            }

            float NTEye_dot2(in float2 v) { return dot(v, v); }

            float NTEye_smin(float a, float b, float k)
            {
                float h = clamp(0.5 + 0.5*(a - b) / k, 0.0, 1.0);
                return lerp(a, b, h) - k * h*(1.0 - h);
            }

            float NTEye_Pulse(float x, float s)
            {
                return 1. / (pow(s, s * sin(x + (UNITY_PI / 2.))) + 1) * max(pow(sin(x), 2) * (.2 * sin(x) + .1 * sin(x * 20)), 0) * 4.3;
            }

            float2 NTEye_ApplyPulse(float2 uv, float pulse, float2 center)
            {
                return float2((uv.x - center.x) / (1 - pulse) + center.x, (uv.y - center.y) * (1 - pulse) + center.y);
            }

            void NTEye_DoHueShift(inout float3 col, float hueAdjust)
            {
                hueAdjust *= 2 * 3.141592653589793;
                const float3 k = float3(0.57735, 0.57735, 0.57735);
                half cosAngle = cos(hueAdjust);
                col = col * cosAngle + cross(k, col) * sin(hueAdjust) + k * dot(k, col) * (1.0 - cosAngle);
            }

            float2 NTEye_GenerateParallaxUV(float2 UVs, float2 viewDirection, float parallaxScale)
            {
                float2 plane = viewDirection.xy;
                UVs += plane * parallaxScale * _MainParallax;
                return UVs;
            }

            float NTEye_GetIrisMask(float2 uv, float2 center, float radius)
            {
                float radial = length(NTEye_GetPupilLocal(uv, center));
                float outer = smoothstep(radius + 0.10, radius - 0.08, radial);
                float inner = smoothstep(0.03, 0.18, radial);
                return outer * inner;
            }

            float2 NTEye_ApplyIrisDepth(float2 uv, float2 viewDirection, float2 center)
            {
                if (_UseRealisticEye <= 0.5)
                {
                    return uv;
                }

                float irisMask = NTEye_GetIrisMask(uv, center, _IrisDepthRadius);
                float2 irisDepthUV = NTEye_GenerateParallaxUV(uv, viewDirection, _IrisDepth);
                return lerp(uv, irisDepthUV, irisMask);
            }

            float3 NTEye_BlendColors(float3 base, float3 blend, int mode, float blendAmount)
            {
                float3 result;

                // 0: Lerp (Linear interpolation)
                if (mode == 0)
                {
                    result = lerp(base, blend, blendAmount);
                }
                // 1: Multiply
                else if (mode == 1)
                {
                    result = lerp(base, base * blend, blendAmount);
                }
                // 2: Add
                else if (mode == 2)
                {
                    result = lerp(base, base + blend, blendAmount);
                }
                // 3: Screen
                else if (mode == 3)
                {
                    float3 screen = 1.0 - (1.0 - base) * (1.0 - blend);
                    result = lerp(base, screen, blendAmount);
                }
                // 4: Overlay
                else if (mode == 4)
                {
                    float3 overlay;
                    overlay.r = base.r < 0.5 ? (2.0 * base.r * blend.r) : (1.0 - 2.0 * (1.0 - base.r) * (1.0 - blend.r));
                    overlay.g = base.g < 0.5 ? (2.0 * base.g * blend.g) : (1.0 - 2.0 * (1.0 - base.g) * (1.0 - blend.g));
                    overlay.b = base.b < 0.5 ? (2.0 * base.b * blend.b) : (1.0 - 2.0 * (1.0 - base.b) * (1.0 - blend.b));
                    result = lerp(base, overlay, blendAmount);
                }
                else
                {
                    result = base;
                }

                return result;
            }

            float3 NTEye_AdjustColor(float3 color, float contrast, float saturation)
            {
                float luma = dot(color, float3(0.299, 0.587, 0.114));
                color = lerp(luma.xxx, color, saturation);
                color = (color - 0.5) * (1.0 + contrast) + 0.5;
                return saturate(color);
            }

            void NTEye_ApplyTexturePolish(inout fixed4 col, float4 texSample, float2 workingUV, float2 eyeCenter)
            {
                if (_UseTexture <= 0.5 || _UseTexturePolish <= 0.5)
                {
                    return;
                }

                float irisMask = smoothstep(1.1, 0.15, length(NTEye_GetPupilLocal(workingUV, eyeCenter)));
                float focusMask = lerp(1.0, irisMask, _TexturePolishIrisFocus);
                float3 polished = texSample.rgb * _TexturePolishTint.rgb;
                float perf = NTEye_GetPerformanceWeight();
                if (_PerformanceTier < 1.5)
                {
                    polished = NTEye_AdjustColor(polished, _TexturePolishContrast, _TexturePolishSaturation);
                }
                else
                {
                    polished = lerp(polished, dot(polished, float3(0.299, 0.587, 0.114)).xxx, saturate(1.0 - _TexturePolishSaturation));
                }

                float blendAmount = saturate(_TexturePolishStrength * focusMask * _TexturePolishTint.a * perf);
                col.rgb = NTEye_BlendColors(col.rgb, polished, _TexturePolishBlendMode, blendAmount);
            }

            void NTEye_ApplyRealisticEye(inout fixed4 col, float2 workingUV, float2 glareUV, float2 workingViewDir, float2 eyeCenter)
            {
                if (_UseRealisticEye <= 0.5)
                {
                    return;
                }

                float2 localUV = NTEye_GetPupilLocal(workingUV, eyeCenter);
                float radial = length(localUV);
                float irisMask = NTEye_GetIrisMask(workingUV, eyeCenter, _IrisDepthRadius);
                float scleraMask = saturate(1.0 - irisMask);

                float limbalMask = saturate(1.0 - abs(radial - _IrisDepthRadius) / max(_LimbalRingWidth, 0.001));
                limbalMask *= limbalMask * _LimbalRingIntensity;
                col.rgb += _LimbalRingColor.rgb * limbalMask * _LimbalRingColor.a;

                float scleraShadow = smoothstep(-0.2, 0.75, -localUV.y + radial * 0.25) * _ScleraShadowStrength;
                col.rgb = lerp(col.rgb, col.rgb * _ScleraTint.rgb, saturate(scleraMask * _ScleraTint.a));
                col.rgb *= 1.0 - scleraMask * scleraShadow * 0.35;

                float corneaRadius = lerp(0.18, 0.03, saturate(_CorneaSpecSmoothness));
                float corneaSpark = saturate(0.015 / max(length(glareUV - (eyeCenter + float2(0.05, -0.24))) - corneaRadius, 1e-5));
                float corneaFresnel = pow(saturate(length(workingViewDir)), max(0.5, _CorneaFresnelPower));
                float corneaMask = saturate((corneaSpark + corneaFresnel * 0.35) * _CorneaSpecIntensity);
                col.rgb += _CorneaSpecColor.rgb * corneaMask * _CorneaSpecColor.a;
            }

            void NTEye_ApplyIrisCaustics(inout fixed4 col, float2 workingUV, float2 workingViewDir, float2 eyeCenter)
            {
                if (_UseIrisCaustics <= 0.5)
                {
                    return;
                }

                float2 causticUV = NTEye_GenerateParallaxUV(workingUV, workingViewDir, _IrisCausticsParallax);
                float2 localUV = NTEye_GetPupilLocal(causticUV, eyeCenter);
                float radial = length(localUV);
                float angle = (dot(localUV, localUV) > 1e-10) ? atan2(localUV.y, localUV.x) : 0.0;

                float animTime = _Time.y * _IrisCausticsSpeed;
                float perf = NTEye_GetPerformanceWeight();
                float spokes;
                if (_PerformanceTier > 1.5)
                {
                    spokes = sin(angle * (_IrisCausticsScale * 0.7) + animTime * 6.2831853);
                }
                else
                {
                    spokes = sin(angle * _IrisCausticsScale + animTime * 6.2831853 + sin(radial * _IrisCausticsScale * 0.75 - animTime * 3.1415926) * _IrisCausticsTwist);
                }
                spokes = pow(saturate(spokes * 0.5 + 0.5), 2.0);

                float irisBand = smoothstep(1.1, 0.15, radial) * smoothstep(0.02, 0.18, radial);
                float causticMask = spokes * irisBand * _IrisCausticsIntensity * perf;
                float3 causticColor = _IrisCausticsColor.rgb * causticMask;

                col.rgb = NTEye_BlendColors(col.rgb, col.rgb + causticColor, _IrisCausticsBlendMode, saturate(causticMask * _IrisCausticsColor.a));
            }

            void NTEye_ApplyIrisRing(inout fixed4 col, float2 workingUV, float2 workingViewDir, float2 eyeCenter)
            {
                if (_UseIrisRingPulse <= 0.5)
                {
                    return;
                }

                float2 ringUV = NTEye_GenerateParallaxUV(workingUV, workingViewDir, _IrisRingParallax);
                float radial = length(NTEye_GetPupilLocal(ringUV, eyeCenter));
                float pulse = sin(_Time.y * _IrisRingPulseSpeed * 6.2831853) * _IrisRingPulseAmount;
                float targetRadius = _IrisRingRadius + pulse;
                float width = max(_IrisRingWidth, 0.0001);
                float ringMask = saturate(1.0 - abs(radial - targetRadius) / width);
                ringMask = ringMask * ringMask;
                ringMask *= smoothstep(0.03, 0.2, radial);
                ringMask *= _IrisRingIntensity * NTEye_GetPerformanceWeight();

                float3 ringColor = _IrisRingColor.rgb * ringMask;
                col.rgb = NTEye_BlendColors(col.rgb, col.rgb + ringColor, _IrisRingBlendMode, saturate(ringMask * _IrisRingColor.a));
            }

            void NTEye_ApplyExpressionOverlay(inout fixed4 col, float2 workingUV, float2 workingViewDir, float2 eyeCenter)
            {
                float exprMode = _ExpressionMode;
                float exprIntensity = _ExpressionIntensity;
                float exprScale = _ExpressionScale;
                float exprSpeed = _ExpressionSpeed;
                float exprDetail = _ExpressionDetail;
                float spiralTightness = _SpiralTightness;
                float spiralLineWidth = _SpiralLineWidth;
                float tearFlow = _TearFlow;
                float tearRim = _TearRim;
                float shockRingCount = _ShockRingCount;
                float shockRingWidth = _ShockRingWidth;
                float4 exprColor = _ExpressionColor;

                if (_ExpressionPreset > 0.5 && _ExpressionPreset < 1.5)
                {
                    return;
                }
                if (_ExpressionPreset > 1.5 && _ExpressionPreset < 2.5)
                {
                    exprMode = 3.0;
                    exprIntensity = max(exprIntensity, 1.1);
                    exprScale = max(exprScale, 12.0);
                    exprSpeed = max(exprSpeed, 1.8);
                    shockRingCount = max(shockRingCount, 4.0);
                    exprColor.rgb = lerp(exprColor.rgb, float3(1.10, 1.00, 0.90), 0.35);
                }
                else if (_ExpressionPreset > 2.5)
                {
                    exprMode = 2.0;
                    exprIntensity = max(exprIntensity, 1.0);
                    exprScale = max(exprScale, 8.0);
                    exprSpeed = max(exprSpeed, 0.9);
                    tearFlow = max(tearFlow, 1.8);
                    tearRim = max(tearRim, 0.8);
                    exprColor.rgb = lerp(exprColor.rgb, float3(0.75, 0.90, 1.20), 0.45);
                }

                if (exprMode < 0.5)
                {
                    return;
                }

                float2 exprUV = NTEye_GenerateParallaxUV(workingUV, workingViewDir, _ExpressionParallax);
                float2 localUV = NTEye_GetPupilLocal(exprUV, eyeCenter);
                float radial = length(localUV);
                float angle = (dot(localUV, localUV) > 1e-10) ? atan2(localUV.y, localUV.x) : 0.0;
                float irisMask = smoothstep(1.08, 0.12, radial);
                float animTime = _Time.y * exprSpeed * 6.2831853;
                float effectMask = 0.0;
                float perf = NTEye_GetPerformanceWeight();

                if (exprMode < 1.5)
                {
                    float spiralPhase = angle * max(spiralTightness, 1.0) + radial * exprScale * 6.2831853 - animTime;
                    float spiralWave = sin(spiralPhase);
                    float lineMask = smoothstep(max(spiralLineWidth, 0.05), 1.0, saturate(spiralWave * 0.5 + 0.5));
                    float centerFade = smoothstep(0.05, 0.2, radial);
                    effectMask = lineMask * centerFade * irisMask;
                }
                else if (exprMode < 2.5)
                {
                    tearFlow = max(tearFlow, 0.0);
                    float flowWave = sin(localUV.y * (exprScale * 1.2) + sin(localUV.x * (exprDetail * 10.0) + animTime) * exprDetail - animTime * tearFlow);
                    float wateryNoise = saturate(flowWave * 0.5 + 0.5);
                    if (_PerformanceTier < 1.5)
                    {
                        float microWave = sin(localUV.x * 22.0 + animTime * 1.4) * sin(localUV.y * 17.0 - animTime * 1.1);
                        wateryNoise = wateryNoise * 0.65 + saturate(microWave * 0.5 + 0.5) * 0.35;
                    }
                    float rimHighlight = smoothstep(0.95, 0.45, radial) * smoothstep(0.5, -0.25, localUV.y) * tearRim;
                    effectMask = saturate(wateryNoise + rimHighlight) * irisMask;
                }
                else
                {
                    float rings = abs(frac((radial * exprScale - animTime * 0.25) * max(shockRingCount, 1.0)) - 0.5) * 2.0;
                    float lineMask = saturate(1.0 - rings / max(shockRingWidth, 0.001));
                    float centerFade = smoothstep(0.08, 0.2, radial);
                    effectMask = lineMask * centerFade * irisMask;
                }

                effectMask *= exprIntensity * perf;
                float blendAmount = saturate(effectMask * exprColor.a);
                float3 overlayColor = exprColor.rgb * effectMask;
                col.rgb = NTEye_BlendColors(col.rgb, col.rgb + overlayColor, _ExpressionBlendMode, blendAmount);
            }

            float NTEye_HeartSDF(float2 p)
            {
                p.y += .6;
                p.x = abs(p.x);

                if (p.y + p.x > 1.0)
                    return sqrt(NTEye_dot2(p - float2(0.25, 0.75))) - sqrt(2.0) / 4.0;
                return sqrt(min(NTEye_dot2(p - float2(0.00, 1.00)),
                    NTEye_dot2(p - 0.5*max(p.x + p.y, 0.0)))) * sign(p.x - p.y);
            }

            float NTEye_EyeDetail1(float2 uv, float size, float2 center)
            {
                uv -= center;
                uv *= size;

                const float a = length(uv) - .1;
                const float b = length(uv * float2(1.1, .8)) - .105;

                return max(-b, a);
            }

            float NTEye_rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            float NTEye_GetAudioLinkBands(int band)
            {
                #if defined(AUDIOLINK)
                    return AudioLinkData( ALPASS_AUDIOLINK + int2( 0, band ) ).rrrr;
                #else
                    return 0.0;
                #endif
            }

            float NTEye_AverageOfAudioLinkBands()
            {
                #if defined(AUDIOLINK)
                    float final = 0;
                    [unroll]
                    for (int i = 0; i < 4; i++)
                    {
                        final += NTEye_GetAudioLinkBands(i);
                    }
                    return final / 4.;
                #else
                    return 0.0;
                #endif
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                fixed3 worldNormal = UnityObjectToWorldNormal(v.normal);
                fixed3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                fixed3 worldBinormal = cross(worldNormal, worldTangent) * v.tangent.w;

                float3x3 worldToTangent = float3x3(worldTangent, worldBinormal, worldNormal);

                float3 rawViewDir = mul(worldToTangent, WorldSpaceViewDir(v.vertex));
                o.viewDir = normalize(rawViewDir);
                o.rawViewDir = rawViewDir;

                o.screenPos = ComputeScreenPos(o.vertex);
                o.screenRatio = float2(_ScreenParams.x / _ScreenParams.y, 1);

                return o;
            }

            float vignetteDitherMask;

            void DoVignette(inout fixed4 col, v2f i)
            {
                float2 eyeCenter = NTEye_GetEyeCenter(i.uv);
                float vignetteMask = smoothstep(_VignetteFalloff + .001 + _VignetteThickness - .03, -_VignetteFalloff + _VignetteThickness - .03, length(i.uv - eyeCenter));
                vignetteDitherMask = smoothstep(_VignetteFalloff + .001 + _VignetteThickness, -_VignetteFalloff + _VignetteThickness, length(i.uv - eyeCenter));
                col *= vignetteMask;
            }

            void DoAudioLink(inout fixed4 col)
            {
                #if defined(AUDIOLINK)
                    col.rgb *= (_BandSelection == 4 ? NTEye_AverageOfAudioLinkBands() : AudioLinkData( ALPASS_AUDIOLINK + int2( 0, (int)_BandSelection ) ).rrrr) * _Intensity + _MinValue;
                #else
                    col.rgb *= _MinValue;
                #endif
            }

            void DoHue(inout fixed4 col)
            {
                col.rgb = pow(col.rgb, lerp(0, 4.4, _Contrast));
                col.rgb = lerp(col.rgb, max(col.r, max(col.g, col.b)), _MainSaturation);
                NTEye_DoHueShift(col.rgb, _MainHueShift + _Time.y * _MainHueSpeed);
            }

            void DoAlpha(inout fixed4 col, v2f i)
            {
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                col.a = lerp(1, length(frac(screenUV*i.screenRatio*lerp(0, 300, _VignetteDitherScale))-.5) < vignetteDitherMask, _VignetteTransparency);
            }

            fixed4 DoColor(v2f i)
            {
                fixed4 col = 0;
                fixed4 proceduralCol = 0;
                fixed4 texCol = 0;

                float2 eyeCenter;
                float2 workingUV;
                float2 workingViewDir;
                NTEye_PrepareEyeData(i.uv, i.viewDir, eyeCenter, workingUV, workingViewDir);
                float2 irisWorkingUV = NTEye_ApplyIrisDepth(workingUV, workingViewDir, eyeCenter);

                float2 glareUV = NTEye_GenerateParallaxUV(irisWorkingUV, workingViewDir, -.2 * _MainParallax);
                float2 pupilUV = NTEye_GenerateParallaxUV(irisWorkingUV, workingViewDir, -.25 * _MainParallax);

                if (_EyeState == 3)
                {
                    float deadPupilMask = NTEye_GetPupilMask(pupilUV, eyeCenter);
                    proceduralCol = lerp(_DeadBottomColor, _DeadTopColor, saturate(pupilUV.y - _DeadGradientOffset)) * saturate(1 - deadPupilMask + .9);

                    texCol = tex2D(_DeadStateTex, pupilUV);
                    if (_UseTexture > 0.5)
                    {
                        proceduralCol = lerp(proceduralCol, texCol * _BackgroundColor, _TextureBlend);
                    }

                    NTEye_ApplyExpressionOverlay(proceduralCol, irisWorkingUV, workingViewDir, eyeCenter);
                    NTEye_ApplyIrisCaustics(proceduralCol, irisWorkingUV, workingViewDir, eyeCenter);
                    NTEye_ApplyIrisRing(proceduralCol, irisWorkingUV, workingViewDir, eyeCenter);
                    NTEye_ApplyTexturePolish(proceduralCol, texCol, irisWorkingUV, eyeCenter);
                    NTEye_ApplyRealisticEye(proceduralCol, irisWorkingUV, glareUV, workingViewDir, eyeCenter);
                    col = proceduralCol;
                }
                else if (_EyeState == 4)
                {
                    float nervousThickness = _NervousLinesThickness * .1;
                    proceduralCol.rgb = _NervousBackgroundColor;
                    float seed = floor(_Time.y * 10) + _NervousLinesRandSeed;
                    float perf = NTEye_GetPerformanceWeight();

                    // Calculate number of rings based on center fill
                    // At _NervousCenterFill = 0, only outer ring (1 layer)
                    // At _NervousCenterFill = 1, fill to center (up to 6 layers)
                    int ringCount = max(1, 1 + (int)(_NervousCenterFill * 5 * perf));
                    int segmentCount = (_PerformanceTier > 1.5) ? 5 : ((_PerformanceTier > 0.5) ? 6 : 8);
                    float safeLinesSize = max(_NervousLinesSize, 0.02);
                    float sizeStep = safeLinesSize / max(ringCount, 1);

                    [loop]
                    for (int ring = 0; ring < 6; ring++)
                    {
                        if (ring >= ringCount) break;
                        float currentSize = _NervousLinesSize - ring * sizeStep;
                        if (currentSize < 0.02) break;

                        [loop]
                        for (int e = 0; e < 8; e++)
                        {
                            if (e >= segmentCount) break;
                            float2 randOffs = float2(2 * (NTEye_rand(pow(e, 2) + seed + ring * 10) - .5), 2 * (NTEye_rand(pow(e, 2) + 2 + seed + ring * 10)-.5)) * _NervousLinesRandOffs;
                            float circleMask = abs(length(NTEye_GenerateParallaxUV(workingUV - eyeCenter + randOffs, workingViewDir, -.2)) - currentSize);
                            float front = circleMask < nervousThickness;
                            float shadow = 1 - saturate(nervousThickness * .6 / max(circleMask, 1e-5));
                            proceduralCol *= min(1 - front, shadow);
                            proceduralCol += max(front, 1 - shadow) * _NervousLinesColor;
                        }
                    }

                    texCol = tex2D(_NervousStateTex, pupilUV);
                    if (_UseTexture > 0.5)
                    {
                        proceduralCol = lerp(proceduralCol, texCol * _NervousBackgroundColor, _TextureBlend);
                    }

                    NTEye_ApplyExpressionOverlay(proceduralCol, irisWorkingUV, workingViewDir, eyeCenter);
                    NTEye_ApplyIrisCaustics(proceduralCol, irisWorkingUV, workingViewDir, eyeCenter);
                    NTEye_ApplyIrisRing(proceduralCol, irisWorkingUV, workingViewDir, eyeCenter);
                    NTEye_ApplyTexturePolish(proceduralCol, texCol, irisWorkingUV, eyeCenter);
                    NTEye_ApplyRealisticEye(proceduralCol, irisWorkingUV, glareUV, workingViewDir, eyeCenter);
                    col = proceduralCol;
                }
                else
                {

                if (_UseTexture > 0.5)
                {
                    if (_EyeState == 0)
                        texCol = tex2D(_NormalStateTex, pupilUV);
                    else if (_EyeState == 1)
                        texCol = tex2D(_StarStateTex, pupilUV);
                    else if (_EyeState == 2)
                        texCol = tex2D(_HeartStateTex, pupilUV);

                    col.rgb = texCol.rgb * _BackgroundColor.rgb;
                }
                else
                {
                    col.rgb = _BackgroundColor.rgb;
                }

                float pupilMask = NTEye_GetPupilMask(pupilUV, eyeCenter);

                if (_EyeState == 2)
                {
                    float pulseAnim = (NTEye_Pulse(_Time.y * (_HeartPulseSpeed * 2), 12) + NTEye_Pulse(_Time.y * (_HeartPulseSpeed * 2) + UNITY_PI, 12)) * .15;
                    float2 heartLocal = (NTEye_ApplyPulse(pupilUV, pulseAnim * _HeartPulsePower * 3, eyeCenter) - eyeCenter) * .5;
                    heartLocal /= max(_PupilSize, 0.0001);
                    heartLocal /= max(_PupilAspect.xy, float2(0.001, 0.001));
                    pupilMask = saturate(0.025 / max(saturate(NTEye_HeartSDF(heartLocal)), 1e-5));
                }

                if (_UseTexture > 0.5)
                {
                    proceduralCol = col * (1 - pupilMask);
                }
                else
                {
                    col *= 1 - pupilMask;
                }

                float2 glareOffset = eyeCenter + float2(0, -0.37); // Glare position relative to eye center
                float mainFocus = saturate(0.025 / max(saturate(length(glareUV-glareOffset)-.4), 1e-5));
                float centerFocus = saturate(0.025 / max(saturate(length(glareUV-glareOffset)-.15), 1e-5)) * 2;

                col += lerp(0, mainFocus * _MainColor, _MainColor.a);
                col += lerp(0, centerFocus * _MainColor, _MainColor.a);

                col *= 1 - pupilMask * .85;

                // Apply pupil color based on state
                float4 pupilColor = lerp(_NormalPupilColor, _HeartPupilColor, _EyeState == 2);

                if (_UseTexture > 0.5 && _EyeState == 2)
                {
                    // For heart state with texture, blend the heart color with the texture
                    float3 heartShape = pupilColor.rgb * pupilMask;
                    col.rgb = NTEye_BlendColors(col.rgb, col.rgb + heartShape, _BlendMode, pupilMask * _TextureBlend);
                }
                else
                {
                    col += pupilMask * pupilColor;
                }

                if (_EyeState == 1)
                {
                    float starTime = sin(_Time.y * _StarRockSpeed * 10);
                    float2 starLocal = NTEye_GetPupilLocal(pupilUV, eyeCenter);
                    float2 starUV = abs(mul(NTEye_Rot(pow(abs(starTime), 1 - _StarRockSharpness) * sign(starTime) * _StarRockAngle), starLocal)) * .25;
                    float starOffset = 1;
                    float starMask = saturate(0.01 / max(saturate(NTEye_smin(length(abs(starUV * float2(1, .5)) + starUV.x * starOffset), length(abs(starUV * float2(.5, 1)) + starUV.y * starOffset), .6)-.2), 1e-5));

                    if (_UseTexture > 0.5)
                    {
                        // Blend procedural star with texture
                        float3 starShape = _StarColor.rgb * starMask;
                        col.rgb = NTEye_BlendColors(col.rgb, col.rgb + starShape, _BlendMode, starMask * _TextureBlend);
                    }
                    else
                    {
                        // Original procedural behavior
                        col.rgb = col.rgb * (1 - starMask) + starMask * _StarColor;
                    }
                }

                float bubbleMap = smoothstep(.8, 0, length(workingUV-eyeCenter));
                float2 bubbleUV = NTEye_GenerateParallaxUV((workingUV - eyeCenter) * lerp(1, length(workingUV - eyeCenter), .7), workingViewDir, (bubbleMap*.2 - .1) * _MainParallax);
                float2 wobbleWave = float2(sin(_Time.y * 3 * (_BubbleWobbleSpeed * 2)), cos(_Time.y * 3 * (_BubbleWobbleSpeed * 2)))* _BubbleWobbleStrength * .1 * (pow(sin(_Time.y * 1), 5) + .2);

                bubbleUV += wobbleWave;

                float bubbleSize = _BubbleSize * 2;
                float glare = saturate(0.001 / max(saturate(length(bubbleUV-float2(.1, .2)) - .06 * bubbleSize), 1e-5));
                if (_PerformanceTier < 1.5)
                {
                    glare += saturate(0.001 / max(saturate(length(bubbleUV-float2(.06, .12)) - .01 * bubbleSize), 1e-5));
                }
                if (_PerformanceTier < 0.5)
                {
                    glare += saturate(0.001 / max(saturate(length(bubbleUV-float2(-.1, -.1)) - .005 * bubbleSize), 1e-5));
                }

                col.rgb += glare * _BubbleBrightness;

                float2 shadowOffset = eyeCenter + float2(0, -0.15); // Shadow position relative to eye center
                col.rgb *= saturate(smoothstep(.5, .4, length(glareUV - shadowOffset)) + .7);

                if (_PerformanceTier < 1.5)
                {
                    col += saturate(0.001 / max(NTEye_EyeDetail1(glareUV, .25, eyeCenter), 1e-5)) * _DetailBrightness;
                }

                NTEye_ApplyExpressionOverlay(col, irisWorkingUV, workingViewDir, eyeCenter);
                NTEye_ApplyIrisCaustics(col, irisWorkingUV, workingViewDir, eyeCenter);
                NTEye_ApplyIrisRing(col, irisWorkingUV, workingViewDir, eyeCenter);
                NTEye_ApplyTexturePolish(col, texCol, irisWorkingUV, eyeCenter);
                NTEye_ApplyRealisticEye(col, irisWorkingUV, glareUV, workingViewDir, eyeCenter);

                } // else (_EyeState 0/1/2)

                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 col = DoColor(i);

                DoVignette(col, i);
                DoAudioLink(col);
                DoHue(col);
                DoAlpha(col, i);

                float eyeRegionMask = NTEye_GetEyeRegionMask(i.uv);
                col = lerp(baseCol, col, eyeRegionMask);

                float innerPriorityMask = NTEye_GetInnerMeshPriorityMask(i.uv) * _InnerMeshPriorityStrength;
                if (innerPriorityMask > 0.0001)
                {
                    if (_InnerMeshPriorityMode < 0.5)
                    {
                        col.rgb = lerp(col.rgb, baseCol.rgb, innerPriorityMask);
                    }
                    else if (_InnerMeshPriorityMode < 1.5)
                    {
                        col.a = saturate(col.a * (1.0 - innerPriorityMask * 0.6));
                    }
                    else
                    {
                        col.a = saturate(col.a * (1.0 - innerPriorityMask));
                    }
                }

                col.a = saturate(col.a * _GlobalOpacity);
                if (_TransparencyResolveMode > 0.5)
                {
                    float2 resolvedScreenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                    float dither = NTEye_GetDitherNoise(resolvedScreenUV);
                    clip(col.a - dither);
                    col.a = 1.0;
                }

                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
    CustomEditor "NataneToonShaderGUI"
}
