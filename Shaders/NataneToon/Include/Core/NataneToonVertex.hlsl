#ifndef NATANE_TOON_VERTEX_INCLUDED
#define NATANE_TOON_VERTEX_INCLUDED

// VAT Helper Functions
#ifdef _VAT
// Decode position data from VAT texture
float3 DecodeVATPosition(float4 rawData, float minVal, float maxVal)
{
    // Decode from normalized [0,1] range back to original range
    float3 position = rawData.rgb;
    position = position * (maxVal - minVal) + minVal;
    return position;
}

// Decode normal data from VAT texture
float3 DecodeVATNormal(float4 rawData, float minVal, float maxVal)
{
    // Decode from normalized [0,1] range back to original range
    float3 normal = rawData.rgb;
    normal = normal * 2.0 - 1.0; // Convert from [0,1] to [-1,1]
    return normalize(normal + float3(0, 0, 0.0001));
}

// Apply VAT animation to vertex
void ApplyVAT(inout float4 vertex, inout float3 normal, float2 uv)
{
    // Calculate frame to sample based on time
    float frame = frac(_Time.y * _VATSpeed) * _VATNumOfFrames;

    // Calculate V coordinate for the frame
    // V coordinate represents the frame, U coordinate represents the vertex ID
    float frameV = (frame + 0.5) / _VATNumOfFrames;

    // Sample position map
    float2 vatUV = float2(uv.x, frameV);
    float4 positionData = NATANE_SAMPLE_CLAMP_LOD(_VATPositionMap, vatUV, 0);
    float3 vatPosition = DecodeVATPosition(positionData, _VATPositionMin, _VATPositionMax);

    // Apply position offset with intensity control
    vertex.xyz += vatPosition * _VATIntensity;

    // Sample and apply normal map if available
    #ifdef _VAT_NORMAL
        float4 normalData = NATANE_SAMPLE_CLAMP_LOD(_VATNormalMap, vatUV, 0);
        float3 vatNormal = DecodeVATNormal(normalData, _VATNormalMin, _VATNormalMax);
        normal = lerp(normal, vatNormal, _VATIntensity);
    #endif
}
#endif

// ===== Hand-Drawn Outline Helper Functions =====
#ifdef _OUTLINE_HAND_DRAWN
// Modulate outline width using noise texture for hand-drawn variation
float GetHandDrawnWidthFactor(float2 uv)
{
    float2 noiseUV = uv * _OutlineNoiseTiling;
    float widthNoise = NATANE_SAMPLE_REPEAT_LOD(_OutlineNoiseTex, noiseUV, 0).r;
    return lerp(1.0 - _OutlineWidthVariation, 1.0 + _OutlineWidthVariation, widthNoise);
}

// Compute position jitter from vertex hash for hand-drawn wobble
float3 GetHandDrawnJitter(float3 objectPos)
{
    float hash1 = frac(sin(dot(objectPos.xy, float2(12.9898, 78.233))) * 43758.5453);
    float hash2 = frac(sin(dot(objectPos.yz, float2(45.164, 93.177))) * 27183.8241);
    float hash3 = frac(sin(dot(objectPos.xz, float2(63.419, 17.652))) * 69143.2758);
    return (float3(hash1, hash2, hash3) * 2.0 - 1.0) * _OutlineJitterAmount * 0.001;
}
#endif

// Vertex Shader
// Transforms vertices and prepares data for fragment shader
v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    // Apply VAT animation before transforming to clip space
    #ifdef _VAT
        ApplyVAT(v.vertex, v.normal, v.uv);
    #endif

    // Smear vertex stretch
    #ifdef _SMEAR
    {
        float3 rawDir = _SmearDirection.xyz;
        float3 smearDir;
        float smearAmount;

        if (_SmearAutoMagnitude > 0.5)
        {
            // Auto mode: direction vector の大きさ = 速度
            float speed = length(rawDir);
            smearDir = (speed > 0.001) ? rawDir / speed : float3(0, 0, 1);
            smearAmount = speed * _SmearMotionSensitivity;
            smearAmount = min(smearAmount, _SmearStretch); // _SmearStretch を最大値として使用
        }
        else
        {
            // Manual mode: 従来通り
            smearDir = normalize(rawDir + float3(0.0001, 0.0001, 0.0001));
            smearAmount = _SmearStretch;
        }

        // VAT velocity integration: derive smear from VAT animation speed
        #ifdef _VAT
        if (_SmearVATVelocity > 0.5)
        {
            float frame = frac(_Time.y * _VATSpeed) * _VATNumOfFrames;
            float prevFrame = frac((_Time.y - unity_DeltaTime.x) * _VATSpeed) * _VATNumOfFrames;

            float frameV_curr = (frame + 0.5) / _VATNumOfFrames;
            float frameV_prev = (prevFrame + 0.5) / _VATNumOfFrames;

            float4 posCurr = NATANE_SAMPLE_CLAMP_LOD(_VATPositionMap, float2(v.uv.x, frameV_curr), 0);
            float4 posPrev = NATANE_SAMPLE_CLAMP_LOD(_VATPositionMap, float2(v.uv.x, frameV_prev), 0);

            float3 vatVel = (DecodeVATPosition(posCurr, _VATPositionMin, _VATPositionMax)
                           - DecodeVATPosition(posPrev, _VATPositionMin, _VATPositionMax))
                           / max(unity_DeltaTime.x, 0.001);

            float vatSpeed = length(vatVel);
            if (vatSpeed > 0.01)
            {
                smearDir = vatVel / vatSpeed;
                smearAmount += vatSpeed * _SmearMotionSensitivity;
                smearAmount = min(smearAmount, _SmearStretch);
            }
        }
        #endif

        float3 worldNorm = UnityObjectToWorldNormal(v.normal);
        float dirMask = saturate(dot(worldNorm, smearDir));

        // Simple noise using vertex position
        float noise = frac(sin(dot(v.vertex.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
        noise = lerp(1.0, noise, _SmearNoiseStrength * _SmearNoiseScale * 0.2);

        float3 offset = smearDir * smearAmount * dirMask * noise;
        // Transform offset from world to object space
        offset = mul((float3x3)unity_WorldToObject, offset);
        v.vertex.xyz += offset;

        o.smearStretchFactor = dirMask * smearAmount;
    }
    #endif

    // Transform vertex to clip space
    o.pos = UnityObjectToClipPos(v.vertex);

    // ===== Perspective Flattening (3D → 2D depth compression) =====
    #ifdef _PERSPECTIVE_FLAT
    // Skipped for oblique (VRChat mirror) and orthographic projections, where
    // "w * 0.5" is not a mid-depth and the object would sort against the wrong
    // surfaces (or vanish behind the scene). Must match NataneToonOutlinePass.hlsl.
    if (!NataneIsObliqueProjection() && !NataneIsOrthoCamera())
    {
        // Compress Z depth toward center to reduce foreshortening
        // This creates a more 2D/illustration-like appearance
        float flatZ = lerp(o.pos.z, o.pos.w * 0.5, _PerspectiveFlatAmount);
        o.pos.z = flatZ;
    }
    #endif

    // ===== Face Orthographic Projection (顔直交投影) =====
    // Blends the screen-space projection of masked vertices (typically the
    // face) from perspective toward orthographic around a pivot, so the face
    // keeps its ideal illustration-like proportions at any camera distance,
    // FOV, or camera type (desktop / VRChat camera / mirror).
    #ifdef _FACE_ORTHO
    {
        float faceOrthoAmount = _FaceOrthoAmount;

        // Fully orthographic faces fight stereo depth cues, so VR uses its
        // own (default: reduced) amount.
        #if defined(USING_STEREO_MATRICES)
            faceOrthoAmount = _FaceOrthoVRAmount;
        #else
            // VR mirrors are rendered by mono cameras: use the VR amount there too.
            if (_VRChatMirrorMode > 0.5 && _VRChatMirrorMode < 1.5)
                faceOrthoAmount = _FaceOrthoVRAmount;
        #endif

        #ifdef _FACE_ORTHO_MASK
            faceOrthoAmount *= NATANE_SAMPLE_REPEAT_LOD(_FaceOrthoMaskTex, v.uv, 0).r;
        #endif

        float4 faceOrthoPivotCS = UnityObjectToClipPos(float4(_FaceOrthoPivot.xyz, 1.0));
        // Skip when the pivot is behind/at the near plane (projection undefined).
        if (faceOrthoAmount > 0.001 && faceOrthoPivotCS.w > 0.01 && o.pos.w > 0.01)
        {
            // Orthographic look = every face vertex shares the pivot's
            // perspective divisor: ndcOrtho = pivotNDC + (clip - pivotClip)/pivotW,
            // which simplifies to clip.xy / pivotW. Blend in NDC space.
            float2 ndcPersp = o.pos.xy / o.pos.w;
            float2 ndcOrtho = o.pos.xy / faceOrthoPivotCS.w;
            float2 ndcBlend = lerp(ndcPersp, ndcOrtho, saturate(faceOrthoAmount));
            o.pos.xy = ndcBlend * o.pos.w;
        }
    }
    #endif

    // Calculate UV coordinates with tiling and offset
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);

    // Transform position to world space
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

    // Transform normal, tangent, and binormal to world space for lighting calculations
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);

    // Calculate binormal (bitangent) using cross product
    // tangent.w contains handedness information for correct orientation
    o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w * unity_WorldTransformParams.w;

    // Smooth Normal: Decode from vertex color and transform to world space for shading
    #ifdef _SMOOTH_NORMAL
    {
        float3 smoothNormalOS = v.normal; // fallback to original
        if (_SmoothNormalMode < 0.5)
        {
            // Mode 0: Vertex Color Object Space
            smoothNormalOS = v.color.rgb * 2.0 - 1.0;
        }
        else if (_SmoothNormalMode < 1.5)
        {
            // Mode 1: Vertex Color Tangent Space (lilToon compatible)
            float3 smoothTS = v.color.rgb * 2.0 - 1.0;
            float3 binormal = cross(v.normal, v.tangent.xyz) * v.tangent.w;
            float3x3 tbnOS = float3x3(v.tangent.xyz, binormal, v.normal);
            smoothNormalOS = mul(smoothTS, tbnOS);
        }
        else
        {
            // Mode 2: Baked Normal Texture (tangent space, same as outline pass)
            float3 bakedNormal = NATANE_SAMPLE_REPEAT_LOD(_SmoothNormalTex, v.uv, 0).rgb * 2.0 - 1.0;
            float3 binormal = cross(v.normal, v.tangent.xyz) * v.tangent.w;
            float3x3 tbnOS = float3x3(v.tangent.xyz, binormal, v.normal);
            smoothNormalOS = mul(bakedNormal, tbnOS);
        }
        o.smoothWorldNormal = UnityObjectToWorldNormal(normalize(smoothNormalOS));
    }
    #endif

    // Calculate screen position for GrabPass (Refraction / Illustration filters) / Dithering Alpha / Intersection Fade
    #if defined(_REFRACTION) || defined(_PARALLAX) || defined(_DISSOLVE) || defined(_DITHERING_ALPHA) || defined(_HASHED_ALPHA) || defined(_INTERSECTION_FADE) || defined(_SOFT_FILTER) || defined(_KUWAHARA_FILTER) || defined(_COLOR_BLEEDING) || defined(_CHROMATIC_ABERRATION) || defined(_SCREEN_EDGE) || defined(_WATERCOLOR) || defined(_SPECULAR_DITHER)
        o.screenPos = ComputeScreenPos(o.pos);
    #endif

    // Lightmap UV (Background mode only)
    #ifdef _BACKGROUND_MODE
        o.lightmapUV = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
    #endif

    // UV1 pass-through for Detail Map / LTCGI
    #if defined(_DETAIL_MAP) || defined(_LTCGI)
        o.uv1 = v.uv1;
    #endif

    // Vertex Color Shadow: pass vertex color to fragment shader
    #ifdef _VERTEX_COLOR_SHADOW
        o.color = v.color;
    #endif

    // Procedural AO / Normal Warping: pass object-space position
    #if defined(_PROCEDURAL_AO) || defined(_NORMAL_WARP)
        o.objectPos = v.vertex.xyz;
    #endif

    // Normal Warping: pass object-space normal
    #if defined(_NORMAL_WARP)
        o.objectNormal = v.normal;
    #endif

    // Transfer fog and shadow coordinates
    UNITY_TRANSFER_FOG(o, o.pos);
    TRANSFER_SHADOW(o);

    // ForwardBase vertex light calculation (4 point lights)
    // multi_compile_fwdbase already defines VERTEXLIGHT_ON when vertex lights are available
    // Skip when _PIXEL_VERTEX_LIGHTS is enabled (fragment shader handles it instead)
    #if defined(VERTEXLIGHT_ON) && !defined(_PIXEL_VERTEX_LIGHTS)
        o.vertexLightColor = Shade4PointLights(
            unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
            unity_LightColor[0].rgb, unity_LightColor[1].rgb,
            unity_LightColor[2].rgb, unity_LightColor[3].rgb,
            unity_4LightAtten0,
            o.worldPos, o.worldNormal
        );
    #endif

    return o;
}

#endif // NATANE_TOON_VERTEX_INCLUDED
