Shader "Natane/Screen FX Overlay"
{
    Properties
    {
        [Header(Blend)]
        _Intensity ("Effect Blend", Range(0, 1)) = 1
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _Contrast ("Contrast", Range(0.5, 2)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1

        [Header(Toonize)]
        _PosterizeStrength ("Posterize Strength", Range(0, 1)) = 0
        _PosterizeSteps ("Posterize Steps", Range(2, 16)) = 8
        _EdgeStrength ("Edge Darken Strength", Range(0, 2)) = 0
        _EdgeThreshold ("Edge Threshold", Range(0, 1)) = 0.1

        [Header(Screen Distortion)]
        _ChromaticAberration ("Chromatic Aberration", Range(0, 3)) = 0
        _AberrationScale ("Aberration Scale", Range(0, 2)) = 1
        // 色収差を画面周辺だけに寄せると視線誘導になる。アニメ撮影の定石。
        _AberrationEdgeOnly ("Aberration Edge Only", Range(0, 1)) = 0
        _AberrationEdgeStart ("Aberration Edge Start", Range(0, 1)) = 0.4

        [Header(Radial Blur)]
        _RadialBlurStrength ("Radial Blur Strength", Range(0, 1)) = 0
        _RadialBlurCenter ("Radial Blur Center (screen UV)", Vector) = (0.5, 0.5, 0, 0)
        // 上限 8。既存 Refraction の Quest 削減方針（9→5）に倣い最悪コストを固定する。
        _RadialBlurSamples ("Radial Blur Samples", Range(2, 8)) = 4
        _RadialBlurEdgeOnly ("Radial Blur Edge Only", Range(0, 1)) = 1

        [Header(Gradation)]
        _GradationTexture ("Gradation Ramp", 2D) = "white" {}
        _GradationColorA ("Gradation Color A", Color) = (1, 1, 1, 1)
        _GradationColorB ("Gradation Color B", Color) = (0, 0, 0, 1)
        _GradationBlend ("Gradation Blend", Range(0, 1)) = 0
        [Enum(Multiply,0,Screen,1,Overlay,2,Additive,3)] _GradationMode ("Gradation Mode", Float) = 0
        _GradationAngle ("Gradation Angle", Range(0, 360)) = 0

        [Header(Monochrome)]
        _MonochromeStrength ("Monochrome", Range(0, 1)) = 0
        _MonochromeEdgeOnly ("Monochrome Edge Only", Range(0, 1)) = 0

        [Header(Cinematic)]
        _Vignette ("Vignette", Range(0, 1)) = 0
        _VignetteSoftness ("Vignette Softness", Range(0.01, 1)) = 0.35
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0
        _ScanlineDensity ("Scanline Density", Range(100, 2000)) = 900
        _ScanlineSpeed ("Scanline Speed", Range(-5, 5)) = 0
        _GrainStrength ("Film Grain", Range(0, 1)) = 0
        _GrainScale ("Grain Scale", Range(1, 400)) = 120
        _GrainSpeed ("Grain Speed", Range(0, 5)) = 1
    }

    // ===== URP (Unity 6 / URP 17+) : 全画面パス =====
    // URP には GrabPass が無いので、Natane Screen FX Renderer Feature (RenderGraph) が
    // カメラカラーを _BlitTexture として渡し、このパスで 1 回のブリットとして描く。
    // メッシュに貼っても描かれない (LightMode が URP の描画対象外)。Renderer Feature にマテリアルを割り当てて使う。
    // URP の入っていないプロジェクト (Built-in RP / VRChat) では読み飛ばされる。
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "17.0.0"
        }

        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "NataneScreenFX"
            Tags { "LightMode" = "NataneScreenFX" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NataneScreenFXFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GradationTexture);
            SAMPLER(sampler_GradationTexture);

            // LOD 0 固定で読む: 放射ブラーのループ内でも勾配を使わない。
            #define NATANE_SCREENFX_SAMPLE_SCREEN(uv) (SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb)
            #define NATANE_SCREENFX_SAMPLE_GRADATION(uv) (SAMPLE_TEXTURE2D_LOD(_GradationTexture, sampler_GradationTexture, uv, 0).rgb)
            #include "NataneScreenFXCore.hlsl"

            half4 NataneScreenFXFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.texcoord;
                half4 baseColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, screenUV, 0);
                half3 fxColor = NataneScreenFXApply(screenUV, _BlitTexture_TexelSize.xy, baseColor.rgb);

                // BiRP 版はアルファブレンドで重ねる。こちらは全画面を書き直すので自前で混ぜる。
                return half4(lerp(baseColor.rgb, fxColor, saturate(_Intensity)), baseColor.a);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue" = "Overlay+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        GrabPass
        {
            "_NataneScreenGrab"
        }

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // Stereo-aware declaration: in VR Single Pass Instanced the grab
            // target is a texture array — a plain sampler2D reads the wrong eye.
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_NataneScreenGrab);
            float4 _NataneScreenGrab_TexelSize;

            sampler2D _GradationTexture;

            inline half3 SampleScreen(float2 screenUV)
            {
                return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_NataneScreenGrab, screenUV).rgb;
            }

            // 効果の本体は URP の全画面パスと共有している (NataneScreenFXCore.hlsl)。
            #define NATANE_SCREENFX_SAMPLE_SCREEN(uv) SampleScreen(uv)
            #define NATANE_SCREENFX_SAMPLE_GRADATION(uv) tex2D(_GradationTexture, uv).rgb
            #include "NataneScreenFXCore.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 screenUV = i.grabPos.xy / i.grabPos.w;
                float2 texel = _NataneScreenGrab_TexelSize.xy;

                half3 baseColor = SampleScreen(screenUV);
                half3 fxColor = NataneScreenFXApply(screenUV, texel, baseColor);

                return half4(fxColor, saturate(_Intensity));
            }
            ENDCG
        }
    }

    // 本体の NataneToonShaderGUI は 990 プロパティ前提で、ScreenFX が持つ 30 数個に対して
    // ほとんどのセクションが空振りする。撮影模倣プリセットの入口も兼ねた専用 GUI を当てる。
    CustomEditor "NataneToon.Editor.NataneScreenFXShaderGUI"
    FallBack Off
}
