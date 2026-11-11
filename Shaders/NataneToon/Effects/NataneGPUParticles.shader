// =============================================================================
//  Natane/Effects/GPU Particles (Stateless)
// -----------------------------------------------------------------------------
//  Vertex-animated, fully stateless GPU particle shader for a static
//  "quad-cloud" mesh. Every particle position is a pure function of
//  (_Time.y * _Speed + seed * _CyclePeriod), so there is NO simulation, NO
//  CustomRenderTexture, NO camera, NO GrabPass and NO runtime script. This is
//  100% VRChat avatar-safe and loops seamlessly via frac().
//
//  ---------------------------------------------------------------------------
//  MESH CONVENTION (generate with Tools/Natane/メッシュ Mesh/GPUパーティクルメッシュ生成):
//    - Each particle is a single quad (4 verts, 2 tris).
//    - POSITION : all 4 verts of a particle share the SAME position = the
//                 particle's emission-box CENTER (object space). The camera
//                 facing billboard corner offset is rebuilt in the vertex
//                 shader from UV0 + _ParticleSize.
//    - UV0 (TEXCOORD0) : quad corner, each component 0 or 1
//                 (0,0)=bottom-left  (1,1)=top-right. Doubles as the sprite UV.
//    - UV1.x (TEXCOORD1.x) : per-particle random seed in [0,1).
//    - UV1.y (TEXCOORD1.y) : particle index normalized (i / count) in [0,1).
//    - COLOR  : per-particle tint (multiplied over _Color and the texture).
//
//  Motion modes (_MotionMode):
//    0 Rise  : 火の粉 / 蛍   - upward drift + sine sway.
//    1 Fall  : 桜吹雪 / 雪    - downward + horizontal drift + billboard spin.
//    2 Orbit : キラキラ       - orbit around the object origin.
//    3 Burst : シード周期で中心から放射 - radial burst from center, loops.
//
//  AudioLink (optional, [Toggle(_AUDIOLINK)]): when the global _AudioTexture
//  exists it scales size/emission from a band level. Falls back neutrally
//  (factor 1) when AudioLink is absent because the global samples black.
// =============================================================================
Shader "Natane/Effects/GPU Particles (Stateless)"
{
    Properties
    {
        [Header(Appearance)]
        _MainTex        ("Particle Sprite (white = soft circle)", 2D) = "white" {}
        [HDR] _Color    ("Tint (HDR)", Color) = (1, 1, 1, 1)
        _ParticleSize   ("Particle Size", Float) = 0.1
        _SoftEdge       ("Soft Circle Edge", Range(0.001, 0.5)) = 0.25

        [Header(Motion)]
        [Enum(Rise,0,Fall,1,Orbit,2,Burst,3)] _MotionMode ("Motion Mode", Float) = 0
        _Speed          ("Speed", Float) = 1.0
        _CyclePeriod    ("Cycle Period (loop seconds)", Float) = 4.0
        _Spread         ("Emission Volume (XYZ)", Vector) = (2, 2, 2)
        _SwayAmount     ("Sway / Drift Amount", Float) = 0.3
        _SwayFrequency  ("Sway / Spin Frequency", Float) = 2.0

        [Header(Life)]
        _FadeIn         ("Alpha Fade In", Range(0.001, 0.5)) = 0.15
        _FadeOut        ("Alpha Fade Out", Range(0.001, 0.5)) = 0.25
        _SizeOverLife   ("Size Grow/Shrink Over Life", Range(0.0, 0.5)) = 0.2

        [Header(AudioLink)]
        [Toggle(_AUDIOLINK)] _AudioLink ("Enable AudioLink", Float) = 0
        _AudioLinkBand      ("AudioLink Band (0..3)", Range(0, 3)) = 0
        _AudioLinkSize      ("AudioLink -> Size", Range(0, 4)) = 1
        _AudioLinkEmission  ("AudioLink -> Emission", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "RenderType"       = "Transparent"
            "IgnoreProjector"  = "True"
            "PreviewType"      = "Plane"
            "DisableBatching"  = "True"
            "VRCFallback"      = "Particle"
        }

        Blend One One          // Additive
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _AUDIOLINK

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float  _ParticleSize;
            float  _SoftEdge;

            float  _MotionMode;
            float  _Speed;
            float  _CyclePeriod;
            float4 _Spread;
            float  _SwayAmount;
            float  _SwayFrequency;

            float  _FadeIn;
            float  _FadeOut;
            float  _SizeOverLife;

            float  _AudioLinkBand;
            float  _AudioLinkSize;
            float  _AudioLinkEmission;

            #ifdef _AUDIOLINK
                // AudioLink global data texture (provided by the AudioLink prefab).
                // Absent -> samples black -> neutral fallback via max() below.
                sampler2D _AudioTexture;
                float4 _AudioTexture_TexelSize;
            #endif

            struct appdata
            {
                float4 vertex : POSITION;   // particle center (shared by 4 verts)
                float2 uv     : TEXCOORD0;   // quad corner 0/1 (also sprite uv)
                float2 uv1    : TEXCOORD1;   // x = seed [0,1), y = index norm
                float4 color  : COLOR;       // per-particle tint
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : TEXCOORD1;    // rgb tint, a = life alpha
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // --- Stateless hash helpers (pure functions of the seed) ---------
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }
            float3 hash31(float p)
            {
                float3 p3 = frac(float3(p, p + 0.1, p + 0.2) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xxy + p3.yzz) * p3.zyx);
            }

            // Returns object-space particle center for the current motion mode.
            // life is the frac-based [0,1) loop phase (offset per particle by seed).
            float3 ComputeParticlePosition(float seed, float life, out float spin)
            {
                float3 rnd = hash31(seed + 1.0);          // stable per-particle randoms
                float3 dir = normalize(rnd * 2.0 - 1.0 + 1e-4);
                float3 half3Spread = _Spread.xyz * 0.5;
                float t = _Time.y * _Speed + seed * 6.2831853;
                spin = 0.0;

                float3 p = (rnd - 0.5) * _Spread.xyz;      // scattered base position

                int mode = (int)(_MotionMode + 0.5);
                if (mode == 0) // Rise
                {
                    p.y = (life - 0.5) * _Spread.y;
                    p.x += sin(t * _SwayFrequency) * _SwayAmount;
                    p.z += cos(t * _SwayFrequency * 0.85) * _SwayAmount;
                }
                else if (mode == 1) // Fall
                {
                    p.y = (0.5 - life) * _Spread.y;
                    p.x += sin(t * _SwayFrequency * 0.5 + seed) * _SwayAmount;
                    p.z += cos(t * _SwayFrequency * 0.4 + seed) * _SwayAmount;
                    spin = t * _SwayFrequency + seed * 6.2831853;   // billboard rotation
                }
                else if (mode == 2) // Orbit
                {
                    float radius = lerp(half3Spread.x * 0.3, half3Spread.x, rnd.x);
                    float angle  = t + seed * 6.2831853;
                    p.x = cos(angle) * radius;
                    p.z = sin(angle) * radius;
                    p.y = (rnd.y - 0.5) * _Spread.y + sin(t * _SwayFrequency) * _SwayAmount;
                }
                else // 3 Burst
                {
                    float maxR = length(half3Spread);
                    p = dir * life * maxR;
                }
                return p;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float seed = v.uv1.x;

                // Per-particle looping life phase, seamless via frac().
                float life = frac(_Time.y * _Speed / max(_CyclePeriod, 0.0001) + seed);

                // AudioLink modulation (neutral when AudioLink is absent).
                float audio = 0.0;
                #ifdef _AUDIOLINK
                    // Bands are rows 0-3 of the 128x64 AudioLink texture (x = 0 is "now").
                    // "band / 4" addressed rows 0 / 16 / 32 / 48, so only Bass worked.
                    float2 alUV = float2(0.5 / 128.0, (clamp(round(_AudioLinkBand), 0.0, 3.0) + 0.5) / 64.0);
                    if (_AudioTexture_TexelSize.z > 16.0)
                        audio = max(tex2Dlod(_AudioTexture, float4(alUV, 0, 0)).r, 0.0);
                #endif

                float spin;
                float3 centerOS = ComputeParticlePosition(seed, life, spin);

                // Life-driven size and alpha (smoothstep in/out approximates a curve).
                float sizeLife = smoothstep(0.0, _SizeOverLife, life) *
                                 smoothstep(0.0, _SizeOverLife, 1.0 - life);
                float alphaLife = smoothstep(0.0, _FadeIn, life) *
                                  smoothstep(0.0, _FadeOut, 1.0 - life);

                float size = _ParticleSize * lerp(0.4, 1.0, sizeLife);
                size *= (1.0 + audio * _AudioLinkSize);

                // Camera-facing billboard using view right/up vectors (world space).
                float3 worldCenter = mul(unity_ObjectToWorld, float4(centerOS, 1.0)).xyz;
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 camUp    = normalize(UNITY_MATRIX_V[1].xyz);

                float2 corner = (v.uv - 0.5) * 2.0;        // [-1,1] from UV0 0/1
                float s = sin(spin), c = cos(spin);
                float2 rc = float2(corner.x * c - corner.y * s,
                                   corner.x * s + corner.y * c);

                float3 worldPos = worldCenter + (camRight * rc.x + camUp * rc.y) * (size * 0.5);

                o.pos = UnityWorldToClipPos(worldPos);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);

                float emission = 1.0 + audio * _AudioLinkEmission;
                o.color.rgb = _Color.rgb * v.color.rgb * emission;
                o.color.a   = _Color.a * v.color.a * alphaLife;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed4 tex = tex2D(_MainTex, i.uv);

                // Soft circle mask from the quad UV (works with default white tex).
                float d = length(i.uv - 0.5) * 2.0;         // 0 center .. ~1.41 corner
                float circle = smoothstep(1.0, 1.0 - _SoftEdge * 2.0, d);

                fixed4 col;
                col.rgb = tex.rgb * i.color.rgb;
                col.a   = tex.a * circle * i.color.a;

                // Premultiply into additive output so alpha controls brightness.
                col.rgb *= col.a;

                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }

    CustomEditor "NataneToonShaderGUI"
    FallBack Off
}
