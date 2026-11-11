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

            half _Intensity;
            half4 _TintColor;
            half _Contrast;
            half _Saturation;

            half _PosterizeStrength;
            half _PosterizeSteps;
            half _EdgeStrength;
            half _EdgeThreshold;

            half _ChromaticAberration;
            half _AberrationScale;
            half _AberrationEdgeOnly;
            half _AberrationEdgeStart;

            half _RadialBlurStrength;
            float4 _RadialBlurCenter;
            half _RadialBlurSamples;
            half _RadialBlurEdgeOnly;

            sampler2D _GradationTexture;
            half4 _GradationColorA;
            half4 _GradationColorB;
            half _GradationBlend;
            half _GradationMode;
            half _GradationAngle;

            half _MonochromeStrength;
            half _MonochromeEdgeOnly;

            half _Vignette;
            half _VignetteSoftness;
            half _ScanlineStrength;
            half _ScanlineDensity;
            half _ScanlineSpeed;
            half _GrainStrength;
            half _GrainScale;
            half _GrainSpeed;

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

            inline half NataneLuminance(half3 color)
            {
                return dot(color, half3(0.299h, 0.587h, 0.114h));
            }

            inline half Hash12(float2 p)
            {
                half h = dot(p, float2(127.1, 311.7));
                return frac(sin(h) * 43758.5453);
            }

            inline half3 SampleScreen(float2 screenUV)
            {
                return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_NataneScreenGrab, screenUV).rgb;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 screenUV = i.grabPos.xy / i.grabPos.w;
                float2 texel = _NataneScreenGrab_TexelSize.xy;

                half3 baseColor = SampleScreen(screenUV);
                half3 fxColor = baseColor;

                // Radius / edge weight, shared by aberration, radial blur, vignette and
                // monochrome. Hoisted here so all four agree on what "the edge" means.
                float2 centeredUV = screenUV * 2.0 - 1.0;
                half radius = saturate(length(centeredUV));
                half edgeWeight = smoothstep(_AberrationEdgeStart, 1.0h, radius);

                // Radial blur — pull samples toward the blur center.
                // Runs before everything else so the rest of the chain operates on the
                // blurred color rather than compositing over a sharp image.
                if (_RadialBlurStrength > 0.001h)
                {
                    half rbWeight = lerp(1.0h, edgeWeight, _RadialBlurEdgeOnly);
                    float2 dir = screenUV - _RadialBlurCenter.xy;
                    half3 acc = fxColor;
                    int count = (int)min(_RadialBlurSamples, 8.0h);

                    [loop] for (int s = 1; s <= count; ++s)
                    {
                        half t = (half)s / (half)count;
                        acc += SampleScreen(screenUV - dir * t * _RadialBlurStrength * 0.15);
                    }

                    acc /= (half)(count + 1);
                    fxColor = lerp(fxColor, acc, rbWeight);
                }

                // Chromatic Aberration
                // _AberrationEdgeOnly = 1 confines it to the periphery, which is how
                // anime compositing uses it: to steer the eye toward the centre.
                half caAmount = _ChromaticAberration * lerp(1.0h, edgeWeight, _AberrationEdgeOnly);
                float2 aberrationOffset = texel * (caAmount * _AberrationScale * 2.0);
                half3 chromaColor;
                chromaColor.r = SampleScreen(screenUV + aberrationOffset).r;
                // fxColor.g, not baseColor.g — otherwise the radial blur would be undone
                // on the green channel.
                chromaColor.g = fxColor.g;
                chromaColor.b = SampleScreen(screenUV - aberrationOffset).b;
                fxColor = lerp(fxColor, chromaColor, saturate(caAmount));

                // Edge darkening for toon-like outlines on top of existing render
                half centerLum = NataneLuminance(baseColor);
                half rightLum = NataneLuminance(SampleScreen(screenUV + float2(texel.x, 0.0)));
                half leftLum = NataneLuminance(SampleScreen(screenUV - float2(texel.x, 0.0)));
                half upLum = NataneLuminance(SampleScreen(screenUV + float2(0.0, texel.y)));
                half downLum = NataneLuminance(SampleScreen(screenUV - float2(0.0, texel.y)));
                half edge = abs(centerLum - rightLum) + abs(centerLum - leftLum) + abs(centerLum - upLum) + abs(centerLum - downLum);
                edge = saturate((edge - _EdgeThreshold) * 6.0);
                fxColor *= (1.0h - edge * saturate(_EdgeStrength));

                // Posterize
                half steps = max(_PosterizeSteps, 2.0h);
                half scale = steps - 1.0h;
                half3 posterized = floor(fxColor * scale + 0.5h) / scale;
                fxColor = lerp(fxColor, posterized, saturate(_PosterizeStrength));

                // Scanline
                half scanWave = sin((screenUV.y * _ScanlineDensity + _Time.y * _ScanlineSpeed) * 6.2831853h) * 0.5h + 0.5h;
                fxColor *= (1.0h - _ScanlineStrength * (1.0h - scanWave));

                // Film grain
                float2 grainUV = floor(screenUV * _GrainScale) + _Time.y * _GrainSpeed;
                half grain = Hash12(grainUV) - 0.5h;
                fxColor += grain * _GrainStrength;

                // Vignette
                half vignetteStart = saturate(1.0h - _VignetteSoftness);
                half vignette = smoothstep(vignetteStart, 1.0h, radius);
                fxColor *= (1.0h - vignette * _Vignette);

                // Gradation — the compositing pass anime uses to lay a colour ramp over
                // the frame (warm above, cool below, and so on).
                if (_GradationBlend > 0.001h)
                {
                    float a = radians(_GradationAngle);
                    float s, c;
                    sincos(a, s, c);
                    half g = saturate(dot(centeredUV, float2(s, c)) * 0.5 + 0.5);

                    half3 grad = lerp(_GradationColorB.rgb, _GradationColorA.rgb, g);
                    // テクスチャ未指定なら "white" なので、2色補間がそのまま残る。
                    grad *= tex2D(_GradationTexture, float2(g, 0.5)).rgb;

                    int mode = (int)(_GradationMode + 0.5h);
                    half3 blended =
                        (mode == 0) ? fxColor * grad :
                        (mode == 1) ? 1.0h - (1.0h - fxColor) * (1.0h - grad) :
                        (mode == 2) ? lerp(2.0h * fxColor * grad,
                                           1.0h - 2.0h * (1.0h - fxColor) * (1.0h - grad),
                                           step(0.5h, NataneLuminance(fxColor)))
                                    : fxColor + grad;

                    fxColor = lerp(fxColor, blended, _GradationBlend);
                }

                // Color grading
                fxColor = (fxColor - 0.5h) * _Contrast + 0.5h;
                half gray = NataneLuminance(fxColor);
                fxColor = lerp(gray.xxx, fxColor, _Saturation);
                fxColor *= _TintColor.rgb;

                // Partial monochrome — the "black and white" step of anime compositing.
                // Kept separate from _Saturation so it can be radius-weighted: the centre
                // stays in colour while the periphery drains, which _Saturation cannot do.
                if (_MonochromeStrength > 0.001h)
                {
                    half mWeight = _MonochromeStrength * lerp(1.0h, edgeWeight, _MonochromeEdgeOnly);
                    fxColor = lerp(fxColor, NataneLuminance(fxColor).xxx, mWeight);
                }

                fxColor = saturate(fxColor);

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
