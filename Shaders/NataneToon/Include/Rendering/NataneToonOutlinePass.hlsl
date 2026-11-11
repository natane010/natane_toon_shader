// NataneToonOutlinePass.hlsl
// Shared OUTLINE pass implementation for all NataneToon shader variants.
// Feature blocks are guarded by #ifdef, so each variant controls availability
// via its own #pragma shader_feature_local list in the .shader file.
#ifndef NATANE_TOON_OUTLINE_PASS_INCLUDED
#define NATANE_TOON_OUTLINE_PASS_INCLUDED

#include "UnityCG.cginc"
#include "../Utils/NataneToonCamera.hlsl"

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
    #if defined(_HEIGHT_FADE) || defined(_DISTANCE_FADE)
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
float _OutlineWidthMode;   // 0 = Legacy (follows the camera's near clip), 1 = Stable (camera independent)
// _VRChatMirrorMode is declared by NataneToonCamera.hlsl
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
#ifdef _MIRROR_CONTROL
    // Same user settings as the forward pass ("mirror only" / "camera only" ...).
    float _MirrorControl;
    float _MirrorMode;
    float _CameraMode;
#endif
#ifdef _DISTANCE_FADE
    // Mirrors the forward pass so the hull fades / clips together with the body.
    float _DistanceFade;
    float _DistanceFadeStart;
    float _DistanceFadeEnd;
    float _DistanceFadeMode;
    float _DistanceFadeBlend;
    float _DistFadeBlur;
    float _NearFadeStart;
    float _NearFadeEnd;
    float _DistFadeDitherScale;
#endif
#ifdef _FACE_ORTHO
    float _FaceOrthoAmount;
    float _FaceOrthoVRAmount;
    float4 _FaceOrthoPivot;
    #ifdef _FACE_ORTHO_MASK
        sampler2D _FaceOrthoMaskTex;
    #endif
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

// ===== Line Boil (outline position + width jitter) =====
#ifdef _LINE_BOIL
    float _LineBoilFPS;
    float _LineBoilPositionJitter;
    float _LineBoilWidthJitter;
    float _LineBoilHoldFrames;
    float _LineBoilRandomSeed;
    float _LineBoilAffectOutline;
    #include "../Effects/NataneToonLineBoil.hlsl"
#endif

// ===== FX Modulator (OutlineWidth target) =====
#ifdef _FX_MODULATOR
    float _FXModSource0;
    float _FXModTarget0;
    float _FXModAmount0;
    float _FXModOffset0;
    float _FXModSpeed0;
    float _FXModMin0;
    float _FXModMax0;
    float _FXModInvert0;
    float _FXModCurve0;
    float _FXModManual0;
    float _FXModDistMin0;
    float _FXModDistMax0;
    float _FXModNoiseScale0;
    float _FXModNoiseSpace0;
    float _FXModSource1;
    float _FXModTarget1;
    float _FXModAmount1;
    float _FXModOffset1;
    float _FXModSpeed1;
    float _FXModMin1;
    float _FXModMax1;
    float _FXModInvert1;
    float _FXModCurve1;
    float _FXModManual1;
    float _FXModDistMin1;
    float _FXModDistMax1;
    float _FXModNoiseScale1;
    float _FXModNoiseSpace1;
    #include "../Effects/NataneToonFXModulator.hlsl"
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
        float distanceToCamera = NataneViewerDistance(worldPos);   // eye-centred, mirror-aware
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

        // ===== Line Boil + FX Modulator outline modulation =====
        // Object-space position jitter (before projection) + width multiplier.
        float nataneOutlineWidthMod = 1.0;
        #ifdef _LINE_BOIL
        if (_LineBoilAffectOutline >= 0.5)
        {
            float boilPhase = NataneLineBoilPhase();
            v.vertex.xyz += NataneLineBoilOffset(v.vertex.xyz, boilPhase, _LineBoilPositionJitter * 0.001);
            float boilW = NataneLineBoilOffset(v.vertex.xyz, boilPhase + 7.3, 1.0).x;
            nataneOutlineWidthMod *= 1.0 + boilW * _LineBoilWidthJitter;
        }
        #endif
        #ifdef _FX_MODULATOR
        {
            float3 fxWp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 fxWn = UnityObjectToWorldNormal(v.normal);
            float3 fxVd = normalize(_WorldSpaceCameraPos - fxWp);
            NataneFXModState fxOutline = NataneFXModCompute(fxWp, fxWn, fxVd, v.uv, v.vertex.xyz, 1.0, 1.0);
            nataneOutlineWidthMod *= NataneFXModMul(fxOutline, NATANE_FXT_OUTLINE_WIDTH);
        }
        #endif

        if (_OutlineMode < 0.5)
        {
            // Mode 0: Inverted Hull - Extrusion along normals in view space
            // Improved for better consistency at different angles
            // No mirror compensation here: UNITY_MATRIX_IT_MV already yields the true outward
            // view-space normal under a reflected view matrix, and TransformViewToProjection
            // uses the same projection as the position. (An X flip pulled the hull inward on
            // the left/right silhouettes in mirrors; Mode 1 below never had one.)
            float3 norm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, outlineNormal));
            float2 offset = TransformViewToProjection(norm.xy);

            o.pos = UnityObjectToClipPos(v.vertex);

            // Apply distance compensation for consistent outline width
            // Scale down by 0.01 to maintain original scale with new range (0-1)
            float outlineWidth = _OutlineWidth * 0.1 * (1.0 + distanceFactor) * widthMultiplier * nataneOutlineWidthMod;

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

            // Regular camera: identical to the historical "* o.pos.z". Mirror (oblique) and
            // orthographic projections get the value a regular projection would have produced.
            float outlineDepthScale = NataneOutlineDepthScale(o.pos, _OutlineWidthMode);
            o.pos.xy += offset * outlineDepthScale * outlineWidth;

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
            float outlineWidth = _OutlineWidth * 0.1 * (1.0 + distanceFactor * 0.5) * widthMultiplier * nataneOutlineWidthMod;

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
        // Skipped for oblique (mirror) and orthographic projections, where "w * 0.5"
        // is not a mid-depth and the object would sort against the wrong surfaces.
        if (!NataneIsObliqueProjection() && !NataneIsOrthoCamera())
        {
            float flatZ = lerp(o.pos.z, o.pos.w * 0.5, _PerspectiveFlatAmount);
            o.pos.z = flatZ;
        }
        #endif

        // Face Orthographic Projection — must mirror NataneToonVertex.hlsl
        // exactly so the outline hull stays aligned with the flattened face.
        #ifdef _FACE_ORTHO
        {
            float faceOrthoAmount = _FaceOrthoAmount;
            #if defined(USING_STEREO_MATRICES)
                faceOrthoAmount = _FaceOrthoVRAmount;
            #else
                // VR mirrors are rendered by mono cameras: use the VR amount there too.
                if (_VRChatMirrorMode > 0.5 && _VRChatMirrorMode < 1.5)
                    faceOrthoAmount = _FaceOrthoVRAmount;
            #endif
            #ifdef _FACE_ORTHO_MASK
                faceOrthoAmount *= tex2Dlod(_FaceOrthoMaskTex, float4(v.uv, 0, 0)).r;
            #endif

            float4 faceOrthoPivotCS = UnityObjectToClipPos(float4(_FaceOrthoPivot.xyz, 1.0));
            if (faceOrthoAmount > 0.001 && faceOrthoPivotCS.w > 0.01 && o.pos.w > 0.01)
            {
                float2 ndcPersp = o.pos.xy / o.pos.w;
                float2 ndcOrtho = o.pos.xy / faceOrthoPivotCS.w;
                float2 ndcBlend = lerp(ndcPersp, ndcOrtho, saturate(faceOrthoAmount));
                o.pos.xy = ndcBlend * o.pos.w;
            }
        }
        #endif

        #if defined(_HEIGHT_FADE) || defined(_DISTANCE_FADE)
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
    // Mirror / camera visibility: without this a "mirror only" material kept drawing its
    // inverted hull in the normal view as a solid outline-coloured shape.
    #ifdef _MIRROR_CONTROL
    if (_MirrorControl >= 0.5)
    {
        bool inMirror = _VRChatMirrorMode > 0.5;
        bool inCamera = _VRChatCameraMode > 0.5;
        if (_MirrorMode > 0.5 && _MirrorMode < 1.5 && !inMirror) discard;
        if (_MirrorMode > 1.5 && inMirror) discard;
        if (_CameraMode > 0.5 && _CameraMode < 1.5 && !inCamera) discard;
        if (_CameraMode > 1.5 && inCamera) discard;
    }
    #endif
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

        // Distance / near fade: keep the inverted hull in step with the body. Without
        // this the body faded or clipped while the hull stayed and filled the silhouette
        // with the outline colour.
        #ifdef _DISTANCE_FADE
        if (_DistanceFade >= 0.5)
        {
            float outlineViewDist = NataneViewerDistance(i.worldPos);
            float farBlur = _DistFadeBlur * (_DistanceFadeEnd - _DistanceFadeStart) * 0.5;
            float farStart = _DistanceFadeStart - farBlur;
            float farEnd = _DistanceFadeEnd + farBlur;
            float outlineDistFade = 1.0 - saturate((outlineViewDist - farStart) / max(farEnd - farStart, 0.01));

            if (_NearFadeEnd > _NearFadeStart + 0.001)
            {
                float nearBlur = _DistFadeBlur * (_NearFadeEnd - _NearFadeStart) * 0.5;
                float nearStart = _NearFadeStart - nearBlur;
                float nearEnd = _NearFadeEnd + nearBlur;
                outlineDistFade *= saturate((outlineViewDist - nearStart) / max(nearEnd - nearStart, 0.01));
            }

            if (_DistanceFadeMode < 0.5)
            {
                // Alpha mode: only visible where the pass blends (Transparent variants),
                // exactly like the body.
                col.a *= lerp(1.0, outlineDistFade, saturate(_DistanceFadeBlend));
            }
            else if (_DistanceFadeMode < 1.5)
            {
                clip(outlineDistFade - 0.001);
            }
            else
            {
                clip(outlineDistFade - NataneBayerThreshold4x4(i.pos.xy, NataneFadeDitherScale(_DistFadeDitherScale)));
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

#endif // NATANE_TOON_OUTLINE_PASS_INCLUDED
