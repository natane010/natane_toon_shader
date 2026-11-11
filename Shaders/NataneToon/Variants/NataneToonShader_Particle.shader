// ===== NataneToon Shader - Particle Variant =====
// Render Type: Transparent
// Queue: Transparent
// 特徴: パーティクル向けの超軽量バリアント。頂点カラー、ブレンドモード切替、
//       トゥーンライティング(任意)、ソフトパーティクル、フリップブック補間、
//       エミッション、カメラ距離フェードをサポート。
//
// 意図的に NataneToonCore.hlsl は使用しない（パーティクルは低コスト優先のため、
// 自己完結のミニマル実装。見た目はトゥーンファミリーの 2 段トゥーン + 影色ティントを踏襲）。
//
// ---------------------------------------------------------------------------
// Required Particle System Vertex Streams (Renderer > Custom Vertex Streams)
// 必要な頂点ストリーム（Renderer モジュール > Custom Vertex Streams）:
//
//   Default (デフォルトのまま / no custom streams needed):
//     Position (POSITION)
//     Normal   (NORMAL)    ... required for Toon Lighting (トゥーンライティング使用時に必要)
//     Color    (COLOR)     ... particle tint/alpha (パーティクル色・透明度)
//     UV       (TEXCOORD0.xy)
//
//   Flipbook Blending enabled (フリップブック補間を有効にする場合は下記を追加):
//     Position  (POSITION)
//     Normal    (NORMAL)
//     Color     (COLOR)
//     UV        (TEXCOORD0.xy)
//     UV2       (TEXCOORD0.zw)  <- add "UV2"
//     AnimBlend (TEXCOORD1.x)   <- add "AnimBlend"
//   ※ Texture Sheet Animation モジュールを有効にしてください。
// ---------------------------------------------------------------------------
Shader "Natane/Toon Shader (Particle)"
{
    Properties
    {
        // ===== Main =====
        [Header(Main)]
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        // ===== Blending =====
        // _BlendMode: 0=Alpha, 1=Additive, 2=Premultiplied, 3=Multiply
        // (_SrcBlend/_DstBlend/_BlendOp are driven by NataneToonParticleGUI)
        [HideInInspector] _BlendMode ("Blend Mode", Float) = 0
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", Float) = 0

        // ===== Toon Lighting =====
        [Header(Toon Lighting)]
        [Toggle(_PARTICLE_TOON_LIGHTING)] _ParticleToonLighting ("Enable Toon Lighting", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0.5, 0.5, 0.5, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 1)) = 0.05

        // ===== Soft Particles =====
        [Header(Soft Particles)]
        [Toggle(_SOFT_PARTICLES)] _SoftParticlesEnabled ("Enable Soft Particles", Float) = 0
        _SoftParticleFadeDistance ("Soft Particle Fade Distance", Range(0.01, 5)) = 0.5

        // ===== Flipbook =====
        [Header(Flipbook)]
        [Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending ("Enable Flipbook Blending", Float) = 0

        // ===== Emission =====
        [Header(Emission)]
        [Toggle(_EMISSION)] _EmissionEnabled ("Enable Emission", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}

        // ===== Camera Fade =====
        [Header(Camera Fade)]
        [Toggle(_CAMERA_FADE)] _CameraFadeEnabled ("Enable Camera Fade", Float) = 0
        _CameraFadeNear ("Camera Fade Near", Float) = 0.5
        _CameraFadeFar ("Camera Fade Far", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "PerformanceChecks"="False"
            "VRCFallback"="Particle"
        }

        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]
        BlendOp [_BlendOp]
        ColorMask RGB

        Pass
        {
            Name "PARTICLE_FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #pragma shader_feature_local _PARTICLE_TOON_LIGHTING
            #pragma shader_feature_local _SOFT_PARTICLES
            #pragma shader_feature_local _FLIPBOOK_BLENDING
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _CAMERA_FADE

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // LPPV support (ForwardBase). Doubles this pass's variant count but
            // lets billboard particles pick up Light Probe Proxy Volume ambient.
            #pragma multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "../Include/Lighting/NataneToonSH.hlsl"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _BlendMode;

            #ifdef _PARTICLE_TOON_LIGHTING
                fixed4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
            #endif

            #ifdef _SOFT_PARTICLES
                UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
                float _SoftParticleFadeDistance;
                float _VRChatMirrorMode;   // VRChat global: >0 while rendering a mirror
            #endif

            #ifdef _EMISSION
                sampler2D _EmissionMap;
                fixed4 _EmissionColor;
            #endif

            #ifdef _CAMERA_FADE
                float _CameraFadeNear;
                float _CameraFadeFar;
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                #ifdef _FLIPBOOK_BLENDING
                    float4 uv : TEXCOORD0;        // xy = uv, zw = uv2 (frame N+1)
                    float texcoordBlend : TEXCOORD1; // x = flipbook blend factor
                #else
                    float2 uv : TEXCOORD0;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                #ifdef _FLIPBOOK_BLENDING
                    float4 uv : TEXCOORD0;
                    float blend : TEXCOORD1;
                #else
                    float2 uv : TEXCOORD0;
                #endif
                UNITY_FOG_COORDS(2)
                #if defined(_SOFT_PARTICLES) || defined(_CAMERA_FADE)
                    // xy(w) = screen pos, z = view-space (eye) depth of the particle
                    float4 projPos : TEXCOORD3;
                #endif
                #ifdef _PARTICLE_TOON_LIGHTING
                    float3 worldNormal : TEXCOORD4;
                    float3 worldPos : TEXCOORD5; // for LPPV sampling
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;

                #ifdef _FLIPBOOK_BLENDING
                    o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
                    o.uv.zw = TRANSFORM_TEX(v.uv.zw, _MainTex);
                    o.blend = v.texcoordBlend;
                #else
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                #endif

                #if defined(_SOFT_PARTICLES) || defined(_CAMERA_FADE)
                    o.projPos = ComputeScreenPos(o.pos);
                    COMPUTE_EYEDEPTH(o.projPos.z);
                #endif

                #ifdef _PARTICLE_TOON_LIGHTING
                    o.worldNormal = UnityObjectToWorldNormal(v.normal);
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                #endif

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // ----- Base color: texture x vertex color x tint -----
                fixed4 tex = tex2D(_MainTex, i.uv.xy);
                #ifdef _FLIPBOOK_BLENDING
                    fixed4 tex2 = tex2D(_MainTex, i.uv.zw);
                    tex = lerp(tex, tex2, i.blend);
                #endif
                fixed4 col = tex * _Color * i.color;

                // ----- Toon lighting (2-step vs main directional + SH ambient) -----
                #ifdef _PARTICLE_TOON_LIGHTING
                {
                    float3 normal = normalize(i.worldNormal);
                    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                    // Half-Lambert keeps billboards from going fully black when facing away
                    float ndl = dot(normal, lightDir) * 0.5 + 0.5;
                    float toonShade = smoothstep(
                        _ShadowThreshold - _ShadowSmoothness,
                        _ShadowThreshold + _ShadowSmoothness,
                        ndl);
                    half3 ambient = NataneShadeSH(normal, i.worldPos);
                    half3 litColor = col.rgb * saturate(_LightColor0.rgb + ambient);
                    half3 shadeColor = litColor * _ShadowColor.rgb;
                    col.rgb = lerp(shadeColor, litColor, toonShade);
                }
                #endif

                // ----- Emission -----
                #ifdef _EMISSION
                    col.rgb += tex2D(_EmissionMap, i.uv.xy).rgb * _EmissionColor.rgb;
                #endif

                // ----- Soft particles (stereo-correct depth sampling) -----
                // Skipped in VRChat mirrors: the depth texture there is the main view's.
                #ifdef _SOFT_PARTICLES
                if (_VRChatMirrorMode < 0.5)
                {
                    float sceneZ = LinearEyeDepth(
                        SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                    float partZ = i.projPos.z;
                    float softFade = saturate((sceneZ - partZ) / max(_SoftParticleFadeDistance, 0.0001));
                    col.a *= softFade;
                }
                #endif

                // ----- Camera-distance fade (avoid near-plane popping) -----
                #ifdef _CAMERA_FADE
                {
                    float eyeDepth = i.projPos.z;
                    float camFade = saturate(
                        (eyeDepth - _CameraFadeNear) / max(_CameraFadeFar - _CameraFadeNear, 0.0001));
                    col.a *= camFade;
                }
                #endif

                // ----- Blend-mode dependent color shaping -----
                // Premultiplied (One, OneMinusSrcAlpha): premultiply RGB by alpha
                if (abs(_BlendMode - 2.0) < 0.5)
                {
                    col.rgb *= col.a;
                }
                // Multiply (DstColor, Zero): fade toward white so alpha 0 is a no-op
                if (abs(_BlendMode - 3.0) < 0.5)
                {
                    col.rgb = lerp(half3(1, 1, 1), col.rgb, col.a);
                }

                // ----- Fog (target color depends on blend mode) -----
                fixed4 fogColor = unity_FogColor;
                if (abs(_BlendMode - 1.0) < 0.5) fogColor = fixed4(0, 0, 0, 0); // Additive: fade to nothing
                if (abs(_BlendMode - 3.0) < 0.5) fogColor = fixed4(1, 1, 1, 1); // Multiply: fade to no-op
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fogColor);

                return col;
            }
            ENDCG
        }
    }

    Fallback "Particles/Alpha Blended"
    CustomEditor "NataneToon.Editor.NataneToonParticleGUI"
}
