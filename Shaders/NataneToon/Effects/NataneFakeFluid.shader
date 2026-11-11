// =============================================================================
//  Natane/Effects/Fake Fluid
// -----------------------------------------------------------------------------
//  Math-only "liquid in a container" shader. There is NO fluid simulation, NO
//  CustomRenderTexture, NO camera and NO runtime script - the liquid surface is
//  purely a clip plane driven by _FillAmount plus animated sine wobble. This is
//  fully VRChat avatar-safe and deterministic (all motion from _Time).
//
//  Technique:
//    - The mesh is filled up to an object-space Y surface height derived from
//      _FillAmount, remapped over [_FillMin, _FillMax] (usually the mesh bounds
//      in object Y). Everything above the surface is clipped.
//    - The surface height is offset per-fragment by 2-3 octaves of sine wobble
//      keyed on worldPos.xz (so the liquid "sloshes" without moving vertices).
//    - Cull Off + VFACE: front faces = the glass/liquid body (toon + fresnel),
//      back faces (visible through the clipped-open top) = a flat _SurfaceColor
//      cap that reads as the top of the liquid. This is the standard fake
//      liquid trick.
//    - A foam line is drawn where the fragment is within _FoamWidth of the
//      surface height.
//
//  Rendered as clip-based cutout: Geometry+1 queue / AlphaTest, VRCFallback
//  "ToonCutout".
// =============================================================================
Shader "Natane/Effects/Fake Fluid"
{
    Properties
    {
        [Header(Fill)]
        [HDR] _Color      ("Liquid Color", Color) = (0.2, 0.6, 1.0, 1.0)
        _FillAmount       ("Fill Amount", Range(0, 1)) = 0.6
        _FillMin          ("Fill Min (object Y)", Float) = -0.5
        _FillMax          ("Fill Max (object Y)", Float) = 0.5

        [Header(Wobble)]
        _WobbleStrength   ("Wobble Strength", Range(0, 0.5)) = 0.05
        _WobbleSpeed      ("Wobble Speed", Float) = 2.0
        _WobbleScale      ("Wobble Scale", Float) = 4.0

        [Header(Surface Cap)]
        [HDR] _SurfaceColor ("Surface Cap Color", Color) = (0.35, 0.75, 1.0, 1.0)

        [Header(Foam Line)]
        [HDR] _FoamColor  ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamWidth        ("Foam Width", Range(0, 0.2)) = 0.03

        [Header(Toon Shading)]
        _ShadowColor      ("Shadow Color", Color) = (0.5, 0.6, 0.8, 1.0)
        _ShadowThreshold  ("Shadow Threshold", Range(-1, 1)) = 0.1

        [Header(Fresnel Rim)]
        [HDR] _RimColor   ("Rim Color", Color) = (0.6, 0.9, 1.0, 1.0)
        _RimPower         ("Rim Power", Range(0.5, 8)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "AlphaTest"
            "RenderType"      = "TransparentCutout"
            "IgnoreProjector" = "True"
            "VRCFallback"     = "ToonCutout"
        }

        Cull Off
        ZWrite On

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float4 _Color;
            float  _FillAmount;
            float  _FillMin;
            float  _FillMax;

            float  _WobbleStrength;
            float  _WobbleSpeed;
            float  _WobbleScale;

            float4 _SurfaceColor;
            float4 _FoamColor;
            float  _FoamWidth;

            float4 _ShadowColor;
            float  _ShadowThreshold;

            float4 _RimColor;
            float  _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 objPos   : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNrm : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos      = UnityObjectToClipPos(v.vertex);
                o.objPos   = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // Animated liquid surface height in object space (with sloshing wobble).
            float SurfaceHeight(float3 worldPos)
            {
                float baseY = lerp(_FillMin, _FillMax, saturate(_FillAmount));
                float t = _Time.y * _WobbleSpeed;
                // 2-3 octaves of sine keyed on world XZ -> the surface sloshes.
                float wobble =
                    sin(worldPos.x * _WobbleScale + t) * 0.5 +
                    sin(worldPos.z * _WobbleScale * 1.3 + t * 1.7) * 0.3 +
                    sin((worldPos.x + worldPos.z) * _WobbleScale * 0.7 - t * 0.9) * 0.2;
                return baseY + wobble * _WobbleStrength;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float surfaceY = SurfaceHeight(i.worldPos);

                // Clip everything above the liquid surface (opens the top).
                clip(surfaceY - i.objPos.y);

                // Back faces seen through the open top = flat liquid surface cap.
                if (facing < 0.0)
                {
                    fixed4 cap = _SurfaceColor;
                    UNITY_APPLY_FOG(i.fogCoord, cap);
                    return cap;
                }

                // ---- Front faces: toon-shaded liquid body ----
                float3 N = normalize(i.worldNrm);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 2-step toon shading vs main light.
                float ndl  = dot(N, L);
                float lit  = step(_ShadowThreshold, ndl);
                float3 baseCol = _Color.rgb;
                float3 shaded  = lerp(baseCol * _ShadowColor.rgb, baseCol, lit);
                shaded *= _LightColor0.rgb + unity_AmbientSky.rgb;

                // Fresnel rim.
                float fres = pow(saturate(1.0 - abs(dot(N, V))), _RimPower);
                shaded += _RimColor.rgb * fres;

                // Foam line near the surface height.
                float distToSurface = abs(i.objPos.y - surfaceY);
                float foam = 1.0 - smoothstep(0.0, max(_FoamWidth, 1e-4), distToSurface);
                shaded = lerp(shaded, _FoamColor.rgb, foam * _FoamColor.a);

                fixed4 col = fixed4(shaded, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    CustomEditor "NataneToonShaderGUI"
    FallBack "Diffuse"
}
