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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // Screen-space texture macros so the GrabPass result also works under
            // VR Single Pass Instanced (Texture2DArray) rendering.
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
                // float: the sine argument exceeds half precision range on mobile GPUs
                float h = dot(p, float2(127.1, 311.7));
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

                // Chromatic Aberration
                float2 aberrationOffset = texel * (_ChromaticAberration * _AberrationScale * 2.0);
                half3 chromaColor;
                chromaColor.r = SampleScreen(screenUV + aberrationOffset).r;
                chromaColor.g = baseColor.g;
                chromaColor.b = SampleScreen(screenUV - aberrationOffset).b;
                fxColor = lerp(fxColor, chromaColor, saturate(_ChromaticAberration));

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
                float2 centeredUV = screenUV * 2.0 - 1.0;
                half radius = saturate(length(centeredUV));
                half vignetteStart = saturate(1.0h - _VignetteSoftness);
                half vignette = smoothstep(vignetteStart, 1.0h, radius);
                fxColor *= (1.0h - vignette * _Vignette);

                // Color grading
                fxColor = (fxColor - 0.5h) * _Contrast + 0.5h;
                half gray = NataneLuminance(fxColor);
                fxColor = lerp(gray.xxx, fxColor, _Saturation);
                fxColor *= _TintColor.rgb;
                fxColor = saturate(fxColor);

                return half4(fxColor, saturate(_Intensity));
            }
            ENDCG
        }
    }

    CustomEditor "NataneToonShaderGUI"
    FallBack Off
}
