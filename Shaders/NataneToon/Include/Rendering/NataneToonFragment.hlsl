#ifndef NATANE_TOON_FRAGMENT_INCLUDED
#define NATANE_TOON_FRAGMENT_INCLUDED

// ===== Optional extras (_EXTRAS_OFF) =====
// The expression / light-shaping extras below are driven by a strength value and skipped with
// a runtime branch when it is 0. Measured with RenderDoc, the ~25 tests and the code kept alive
// for them still cost about 40% of the forward pass on a material that uses none of them.
// A material that declares it uses none (_EXTRAS_OFF) compiles them out entirely.
// Wrap the strength test of such a block in NATANE_FX(); never use it for anything else.
#if defined(_EXTRAS_OFF)
    #define NATANE_FX(c) (false)
#else
    #define NATANE_FX(c) (c)
#endif

// ===== Fragment Shader Constants =====
#define DIST_FADE_BLUR_SCALE     0.5   // 距離フェードぼかしスケール
#define PCF_TEXEL_SCALE          3.0   // PCF シャドウマップテクセルスケール
#define PCF_SAMPLE_COUNT         9.0   // PCF サンプル合計数
#define SMOOTH_WIDTH_SCALE       0.3   // シャドウスムーズ幅スケール
#define BACKLIGHT_MIN_POWER      0.5   // バックライト最小パワー
#define BACKLIGHT_SCALE          4.0   // バックライト基本スケール
#define BACKLIGHT_BLUR_INFLUENCE 0.8   // バックライトブラー影響度
#define AO_INDIRECT_STRENGTH     0.5   // AO 間接光強度
#define SH_INDIRECT_BLEND        0.85  // SH 間接光ブレンド率

inline half3 NataneResolveLookWeights()
{
    half explicitLookMixer = step(0.5, _LookMode);
    half legacyPbr = step(2.5, _ShadingMode);

    half toonWeight = lerp(1.0h - legacyPbr, saturate(_ToonWeight), explicitLookMixer);
    half nprWeight = lerp(1.0h, saturate(_NprWeight), explicitLookMixer);
    half pbrWeight = lerp(legacyPbr, saturate(_PbrWeight), explicitLookMixer);

    return half3(toonWeight, nprWeight, pbrWeight);
}

inline half NataneResolveBasePbrWeight(half3 lookWeights)
{
    half baseTotal = lookWeights.x + lookWeights.z;
    return baseTotal > 0.0001h ? saturate(lookWeights.z / baseTotal) : 0.0h;
}

inline half NataneResolveNprWeight(half3 lookWeights)
{
    return saturate(lookWeights.y);
}

// Fragment Shader
// Main pixel/fragment rendering function
// Optimized: half precision for better performance, cached luminance calculations
half4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    // ===== Mirror / Camera Control (VRChat) =====
    // VRChat globals: _VRChatMirrorMode (0=Normal, 1=Mirror VR, 2=Mirror Desktop)
    //                 _VRChatCameraMode (0=Normal, 1=VR Camera, 2=Desktop Camera, 3=Screenshot)
    #ifdef _MIRROR_CONTROL
    if (_MirrorControl >= 0.5)
    {
        // _MirrorMode (user setting): 0=Both, 1=Mirror Only, 2=Non-Mirror Only
        if (_MirrorMode > 0.5 && _MirrorMode < 1.5 && !NataneIsMirror())
            discard; // Mirror-only mode: discard in normal view
        if (_MirrorMode > 1.5 && NataneIsMirror())
            discard; // Non-mirror mode: discard in mirror view

        // _CameraMode (user setting): 0=Both, 1=Camera Only, 2=Non-Camera Only
        if (_CameraMode > 0.5 && _CameraMode < 1.5 && !NataneIsCamera())
            discard; // Camera-only mode: discard when not in VRChat camera
        if (_CameraMode > 1.5 && NataneIsCamera())
            discard; // Non-camera mode: discard when in VRChat camera
    }
    #endif

    // ===== Parallax Mapping (UV Adjustment) =====
    // このセクションの処理:
    // 視差マッピングによりテクスチャ座標を視線方向に基づいてオフセットし、
    // ハイトマップから擬似的な凹凸の奥行き表現を生成する。
    // 全後続テクスチャサンプリングの基準UVとなる。
    float2 uv = i.uv;

    // ===== FX Modulator state (computed once, used at each target site) =====
    #if defined(_FX_MODULATOR)
    float3 nataneFXViewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
    half2 nataneFXMask = half2(1.0, 1.0);
    {
        half4 fxm = NATANE_SAMPLE_SHARED(_FXModMaskTex, _MainTex, i.uv);
        nataneFXMask = half2(fxm.r, fxm.g);
    }
    float3 nataneFXObjPos = mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
    NataneFXModState nataneFXState = NataneFXModCompute(i.worldPos, i.worldNormal, nataneFXViewDir,
                                                        i.uv, nataneFXObjPos,
                                                        nataneFXMask.x, nataneFXMask.y);
    #endif

    // ===== Line Boil precompute (time-quantized hand-drawn UV jitter) =====
    #if defined(_LINE_BOIL)
    float nataneBoilStrength = 1.0;
    #if defined(_FX_MODULATOR)
        nataneBoilStrength *= NataneFXModMul(nataneFXState, NATANE_FXT_LINEBOIL);
    #endif
    half nataneBoilMask = NATANE_SAMPLE_SHARED_R(_LineBoilMaskTex, _MainTex, i.uv);
    float2 nataneBoilUV = NataneLineBoilOffset(i.worldPos, NataneLineBoilPhase(), 1.0).xy
                          * _LineBoilUVJitter * nataneBoilMask * nataneBoilStrength;
    #endif

    #if defined(_EYE_PARALLAX) && !defined(_PARALLAX)
    {
        // 軽量アイパララックス: ビュー方向のXY成分でUVオフセット
        float3 eyeViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
        float3 eyeViewTS = mul((float3x3)UNITY_MATRIX_V, eyeViewDir);
        eyeViewTS.x *= NataneMirrorSign(); // Compensate VRChat mirror X-axis flip
        float2 eyeOffset = eyeViewTS.xy * _EyeParallaxDepth;
        uv += eyeOffset;
    }
    #endif
    #ifdef _PARALLAX
        // Property value check: prevents parallax from activating on materials
        // where _Parallax=0 even if the shader keyword is incorrectly enabled
        // (e.g. due to VRChat SDK build variant stripping fallback).
        if (_Parallax >= 0.5)
        {
            float3 tangentViewDir = CalculateTangentViewDir(i.worldPos, i.worldTangent, i.worldBinormal, i.worldNormal);
            uv = ParallaxMapping(i.uv, tangentViewDir);
        }
    #endif

    // ===== UV Animation =====
    float2 mainUV = uv;
    #ifdef _MAIN_TEX_ANIMATION
        if (_MainTexAnimation >= 0.5)
            mainUV = AnimateUV(uv, _MainTexScrollSpeed.xy, _MainTexRotateSpeed);
    #endif

    // ===== Pixel Art: UV pixelation (snap mainUV before main texture sampling) =====
    // Applied to mainUV only so other UV-driven features stay at native res.
    // Screen / Object-Stable snapping intentionally omitted (see NataneToonPixelArt.hlsl).
    #if defined(_PIXEL_ART)
    if (_PixelArt >= 0.5)
    {
        half natanedPxMask = NATANE_SAMPLE_SHARED_R(_PixelArtMask, _MainTex, uv);
        if (natanedPxMask > 0.001)
            mainUV = lerp(mainUV, NatanePixelSnapUV(mainUV, _PixelArtSize), natanedPxMask);
    }
    #endif

    // ===== Glitch Stretch (UV modification before main texture sampling) =====
    #if defined(_GLITCH_STRETCH) && defined(UNITY_PASS_FORWARDBASE)
    if (_GlitchStretch >= 0.5)
    {
        half stretchMaskVal = NATANE_SAMPLE_REPEAT(_GlitchStretchMask, TRANSFORM_TEX(mainUV, _GlitchStretchMask)).r;
        // Mask Scale: マスク値を増幅（1.0=等倍、5.0=5倍ブースト）
        stretchMaskVal = saturate(stretchMaskVal * _GlitchStretchMaskScale);
        half stretchTrigger = step(1.0 - _GlitchStretchFrequency,
            frac(sin(_Time.y * _GlitchStretchSpeed) * 43758.5453));
        if (stretchTrigger > 0.5 && stretchMaskVal > 0.001)
        {
            float2 stretchedUV = CalculateGlitchStretchUV(mainUV,
                _GlitchStretchIntensity * stretchMaskVal, _GlitchStretchSpeed,
                _GlitchStretchBlockSize, _GlitchStretchFrequency);
            mainUV = stretchedUV;
        }
    }
    #endif

    // ===== Texture Sampling =====
    #ifdef _TRIPLANAR
        half4 mainTex = (_Triplanar >= 0.5)
            ? TriplanarSample(_MainTex, i.worldPos, i.worldNormal, _TriplanarScale, _TriplanarBlendSharpness)
            : tex2D(_MainTex, mainUV);
    #else
        half4 mainTex = tex2D(_MainTex, mainUV);
    #endif
    half4 col = mainTex * _Color;

    // ===== Mirror / Camera Alternate Texture (鏡・カメラ写り分けテクスチャ) =====
    // Swaps the base color when rendered by a VRChat mirror and/or VRChat
    // camera, so the reflected/photographed appearance can differ from the
    // directly-viewed one. Runs before makeup/lighting so the whole shading
    // stack applies to the alternate look as well.
    #ifdef _MIRROR_TEXTURE
    {
        half altBlend = 0.0;
        if (_MirrorTexApplyMirror >= 0.5) altBlend = max(altBlend, NataneIsMirror());
        if (_MirrorTexApplyCamera >= 0.5) altBlend = max(altBlend, NataneIsCamera());
        altBlend *= _MirrorTexBlend;
        if (altBlend > 0.001)
        {
            half4 altTex = NATANE_SAMPLE_REPEAT(_MirrorAltTex, mainUV) * _MirrorAltColor;
            col = lerp(col, altTex, altBlend);
        }
    }
    #endif

    // ===== Gradient Base Color =====
    #ifdef _GRADIENT_BASE_COLOR
    if (_GradientBaseColor >= 0.5)
    {
        half3 gradColor = CalculateGradientColor(i.worldPos,
            _GradientTopColor.rgb, _GradientBottomColor.rgb,
            _GradientAxis, _GradientSpace, _GradientStart, _GradientEnd);
        half3 preGradient = col.rgb;
        col.rgb *= gradColor;
        col.rgb = ApplyEffectBlendPost(preGradient, col.rgb, _GradientBlend, _GradientBlendMode);
    }
    #endif

    // ===== Makeup/Detail Textures Blending =====
    // このセクションの処理:
    // メインテクスチャの上に2nd〜5thテクスチャを順番に重ね合わせる。
    // 各テクスチャはUVアニメーション、HSV調整、マスク、ブレンドモード
    // （Add/Multiply/Overlay/Screen）を個別に持ち、メイクアップ表現を実現する。
    #ifdef _2ND_TEXTURE
    if (_Use2ndTexture >= 0.5)
    {
        float2 _2ndAnimUV = AnimateUVIfNeeded(uv, _2ndTexScrollSpeed.xy, _2ndTexRotateSpeed);
        half4 secondTexSample = NATANE_SAMPLE_REPEAT(_2ndTex, _2ndAnimUV);
        float secondMask = NATANE_SAMPLE_SHARED_R(_2ndTexMask, _2ndTex, uv);
        col.rgb = ApplyMakeupTexture(col.rgb, secondTexSample, secondMask,
            _2ndTexHueShift, _2ndTexSaturation, _2ndTexValue,
            _2ndTexIntensity, _2ndTexBlendMode
        );
    }
    #endif

    #ifdef _3RD_TEXTURE
    if (_Use3rdTexture >= 0.5)
    {
        float2 _3rdAnimUV = AnimateUVIfNeeded(uv, _3rdTexScrollSpeed.xy, _3rdTexRotateSpeed);
        half4 thirdTexSample = NATANE_SAMPLE_REPEAT(_3rdTex, _3rdAnimUV);
        float thirdMask = NATANE_SAMPLE_SHARED_R(_3rdTexMask, _3rdTex, uv);
        col.rgb = ApplyMakeupTexture(col.rgb, thirdTexSample, thirdMask,
            _3rdTexHueShift, _3rdTexSaturation, _3rdTexValue,
            _3rdTexIntensity, _3rdTexBlendMode
        );
    }
    #endif

    #ifdef _4TH_TEXTURE
    if (_Use4thTexture >= 0.5)
    {
        float2 _4thAnimUV = AnimateUVIfNeeded(uv, _4thTexScrollSpeed.xy, _4thTexRotateSpeed);
        half4 fourthTexSample = NATANE_SAMPLE_REPEAT(_4thTex, _4thAnimUV);
        float fourthMask = NATANE_SAMPLE_SHARED_R(_4thTexMask, _4thTex, uv);
        col.rgb = ApplyMakeupTexture(col.rgb, fourthTexSample, fourthMask,
            _4thTexHueShift, _4thTexSaturation, _4thTexValue,
            _4thTexIntensity, _4thTexBlendMode
        );
    }
    #endif

    #ifdef _5TH_TEXTURE
    if (_Use5thTexture >= 0.5)
    {
        float2 _5thAnimUV = AnimateUVIfNeeded(uv, _5thTexScrollSpeed.xy, _5thTexRotateSpeed);
        half4 fifthTexSample = NATANE_SAMPLE_REPEAT(_5thTex, _5thAnimUV);
        float fifthMask = NATANE_SAMPLE_SHARED_R(_5thTexMask, _5thTex, uv);
        col.rgb = ApplyMakeupTexture(col.rgb, fifthTexSample, fifthMask,
            _5thTexHueShift, _5thTexSaturation, _5thTexValue,
            _5thTexIntensity, _5thTexBlendMode
        );
    }
    #endif

    // ===== Lenticular / view-angle dependent atlas (base-color modifier) =====
    #if defined(_LENTICULAR)
    if (_Lenticular >= 0.5)
    {
        half lentMask = NATANE_SAMPLE_SHARED_R(_LenticularMask, _MainTex, uv);
        lentMask = ApplySoftMask(lentMask);
        if (lentMask > 0.001)
        {
            float frames = max(_LenticularFrames, 1.0);

            // Object-space view direction (stable across eyes when StereoCenter).
            float3 lentCamPos = NataneLenticularCameraPos(_LenticularStereoMode);
            float3 lentVdW = normalize(lentCamPos - i.worldPos);
            float3 lentVdObj = normalize(mul((float3x3)unity_WorldToObject, lentVdW));
            float lentAngle = NataneLenticularAngle(lentVdObj, _LenticularViewAxis);

            // Angle -> normalized frame position [0,1] (front-centered).
            float lentT = lentAngle / max(_LenticularAngleRange, 0.0001) + 0.5;

            // Optional normal skew of the selection. Uses the interpolated
            // geometric normal (i.worldNormal): this hook runs before the
            // normal-mapped `worldNormal` is resolved. Kept simple by design.
            if (_LenticularNormalInfluence > 0.001)
            {
                float3 lentNObj = normalize(mul((float3x3)unity_WorldToObject, i.worldNormal));
                float lentNSkew = (_LenticularViewAxis < 0.5) ? lentNObj.x : lentNObj.y;
                lentT += lentNSkew * _LenticularNormalInfluence;
            }
            lentT = saturate(lentT);

            float lentF = lentT * (frames - 1.0) + _LenticularFrameOffset;
            int lentMode = (int)(_LenticularMode + 0.5);

            float lentBaseIdx = floor(lentF);
            float lentBlend = frac(lentF);
            // Transition softness sharpens/loosens the crossfade.
            lentBlend = smoothstep(0.5 - max(_LenticularSoftness, 0.0001) * 0.5,
                                   0.5 + max(_LenticularSoftness, 0.0001) * 0.5, lentBlend);

            float lentIdxA = lentBaseIdx;
            float lentIdxB = min(lentBaseIdx + 1.0, frames - 1.0);
            float lentMix = lentBlend;   // A->B crossfade weight
            half lentReveal = 1.0;       // reveal weight over the base color
            half lentEmit = 0.0;         // emission boost weight (revealed frame)

            if (lentMode == 1) // HardStep
            {
                lentIdxA = round(lentF);
                lentIdxB = lentIdxA;
                lentMix = 0.0;
            }
            else if (lentMode == 2) // ScanBlend (thin UV stripes mix adjacent frames)
            {
                float lentStripe = step(0.5, frac(uv.x * max(_LenticularScanScale, 1.0)));
                lentMix = lentStripe;
            }
            else if (lentMode == 3) // FrontReveal (alt frame near front only)
            {
                lentIdxA = _LenticularFrameOffset;                 // base frame
                lentIdxB = frames - 1.0;                           // reveal frame
                lentReveal = 1.0 - smoothstep(0.0, max(_LenticularAngleRange, 0.0001) * 0.5, abs(lentAngle));
                lentMix = lentReveal;
                lentEmit = lentReveal;
            }
            else if (lentMode == 4) // SideReveal (alt frame at sides only)
            {
                lentIdxA = _LenticularFrameOffset;
                lentIdxB = frames - 1.0;
                lentReveal = smoothstep(0.0, max(_LenticularAngleRange, 0.0001) * 0.5, abs(lentAngle));
                lentMix = lentReveal;
                lentEmit = lentReveal;
            }
            else if (lentMode == 5) // Flip (A/B by view side)
            {
                float lentSide = smoothstep(-max(_LenticularSoftness, 0.0001),
                                            max(_LenticularSoftness, 0.0001), lentAngle);
                lentIdxA = _LenticularFrameOffset;
                lentIdxB = min(_LenticularFrameOffset + 1.0, frames - 1.0);
                lentMix = lentSide;
            }

            float2 lentUVA = NataneLenticularFrameUV(uv, lentIdxA, frames, _LenticularDirection);
            float2 lentUVB = NataneLenticularFrameUV(uv, lentIdxB, frames, _LenticularDirection);
            half4 lentTexA = NATANE_SAMPLE_SHARED(_LenticularAtlas, _MainTex, lentUVA);
            half4 lentTexB = NATANE_SAMPLE_SHARED(_LenticularAtlas, _MainTex, lentUVB);
            half3 lentCol = lerp(lentTexA.rgb, lentTexB.rgb, saturate(lentMix));

            // Emission boost brightens the revealed frame (HDR overshoot).
            lentCol *= (1.0 + _LenticularEmission * saturate(lentEmit));

            half lentBlendMod = _LenticularBlend;
            #if defined(_FX_MODULATOR)
                lentBlendMod *= NataneFXModMul(nataneFXState, NATANE_FXT_LENTICULAR);
            #endif
            col.rgb = lerp(col.rgb, lentCol, saturate(lentBlendMod * lentMask));
        }
    }
    #endif

    // ===== Surface Cover (Snow/Sand Accumulation) =====
    #ifdef _SURFACE_COVER
    if (_SurfaceCover >= 0.5)
    {
        float3 safeCoverDirA = normalize(_CoverDirection.xyz + float3(0, 0.0001, 0));
        float coverDot = dot(i.worldNormal, safeCoverDirA);
        float coverFactor = saturate((coverDot - _CoverThreshold) * _CoverBlendSharpness) * _CoverAmount;
        half4 coverSample = NATANE_SAMPLE_REPEAT(_CoverTex, i.worldPos.xz * _CoverTiling) * _CoverColor;
        col.rgb = lerp(col.rgb, coverSample.rgb, coverFactor);
    }
    #endif

    // ===== Screen-Tone Overlay =====
    #ifdef _SCREEN_TONE
    if (_ScreenTone >= 0.5)
    {
        float2 screenToneUV = uv;
        float2 screenTonePatternPos = i.pos.xy;
        #if defined(_LINE_BOIL)
        if (_LineBoilAffectHatching >= 0.5)
        {
            screenToneUV += nataneBoilUV;
            screenTonePatternPos += nataneBoilUV * _ScreenParams.xy;
        }
        #endif
        half screenToneMask = NATANE_SAMPLE_SHARED_BLUR_R(_ScreenToneMask, _MainTex, screenToneUV, _ScreenToneBlur);
        screenToneMask = ApplySoftMask(screenToneMask);
        half3 preScreenTone = col.rgb;
        col.rgb = ApplyScreenTone(col.rgb, screenTonePatternPos, screenToneMask);
        col.rgb = ApplyEffectBlendPost(preScreenTone, col.rgb, _ScreenToneBlend, _ScreenToneBlendMode);
    }
    #endif

    // ===== Normal Mapping =====
    half3x3 tangentToWorld = half3x3(i.worldTangent, i.worldBinormal, i.worldNormal);

    // Optimization: Skip normalization if no normal mapping (already normalized in vertex shader)
    #ifdef _NORMALMAP
        half3 worldNormal;
        if (_UseNormalMap >= 0.5)
        {
            float2 bumpUV = uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
            bumpUV = AnimateUVIfNeeded(bumpUV, _BumpMapScrollSpeed.xy, _BumpMapRotateSpeed);
            half3 normalMapSample = UnpackScaleNormal(NATANE_SAMPLE_REPEAT(_BumpMap, bumpUV), _BumpScale);
            worldNormal = normalize(mul(normalMapSample, tangentToWorld));
        }
        else
        {
            worldNormal = i.worldNormal;
        }
    #else
        half3 worldNormal = i.worldNormal; // Already normalized in vertex shader
    #endif

    // Geometric specular AA kernel for the GGX lobes (see NataneToonPBR.hlsl).
    // Derivatives are taken here, outside any branch; strength 0 (default) keeps it at 0.
    nataneGSAAKernel = NATANE_FX(true) ? NataneComputeGSAAKernel(normalize(i.worldNormal)) * saturate(_SpecularAAGeometric) : 0.0;

    // ===== Detail Map (Secondary UV) =====
    #ifdef _DETAIL_MAP
    if (_DetailMap >= 0.5)
    {
        float2 detailUV = (_DetailUVSet > 0.5) ? i.uv1 : uv;
        detailUV *= _DetailTiling;
        half4 detailAlbedo = NATANE_SAMPLE_REPEAT(_DetailAlbedoMap, detailUV);
        col.rgb = lerp(col.rgb, col.rgb * detailAlbedo.rgb * 2.0, _DetailAlbedoScale * detailAlbedo.a);
        #ifdef _NORMALMAP
            half3 detailNormalTS = UnpackScaleNormal(NATANE_SAMPLE_REPEAT(_DetailNormalMap, detailUV), _DetailNormalScale);
            // Transform detail normal from tangent space to world space using TBN matrix
            half3 detailNormalWS = normalize(mul(detailNormalTS, tangentToWorld));
            worldNormal = normalize(lerp(worldNormal, detailNormalWS, _DetailAlbedoScale));
        #endif
    }
    #endif

    // ===== Micro Normal Detail =====
    if (NATANE_FX(_MicroNormalStrength > 0.001))
    {
        float2 microNormalUV = uv * max(_MicroNormalTiling, 1.0);
        half3 microNormalTS = UnpackScaleNormal(
            NATANE_SAMPLE_REPEAT(_MicroNormalMap, microNormalUV),
            _MicroNormalScale);
        half3 microNormalWS = normalize(mul(microNormalTS, tangentToWorld));
        worldNormal = normalize(lerp(worldNormal, microNormalWS, saturate(_MicroNormalStrength)));
    }

    // ===== Surface Cover Normal Blending =====
    #ifdef _SURFACE_COVER
    #ifdef _NORMALMAP
    if (_SurfaceCover >= 0.5 && _UseNormalMap >= 0.5)
    {
        float3 safeCoverDir = normalize(_CoverDirection.xyz + float3(0, 0.0001, 0));
        float coverDotN = dot(worldNormal, safeCoverDir);
        float coverFactorN = saturate((coverDotN - _CoverThreshold) * _CoverBlendSharpness) * _CoverAmount;
        half3 coverNormTS = UnpackNormal(NATANE_SAMPLE_REPEAT(_CoverNormalMap, i.worldPos.xz * _CoverTiling));
        // Remap from tangent space (xz projection: tangent=X, bitangent=Z, normal=Y)
        half3 coverNormWS = half3(coverNormTS.x, coverNormTS.z, coverNormTS.y);
        worldNormal = normalize(lerp(worldNormal, coverNormWS, coverFactorN));
    }
    #endif
    #endif

    // ===== Procedural Normal Warping =====
    #ifdef _NORMAL_WARP
    if (_NormalWarp >= 0.5)
    {
        // 1. 球状法線: オブジェクト空間位置を正規化
        float3 sphereNormal = normalize(i.objectPos);
        // ワールド空間に変換
        float3 sphereNormalWS = normalize(mul((float3x3)unity_ObjectToWorld, sphereNormal));

        // 2. 球状法線ブレンド
        worldNormal = normalize(lerp(worldNormal, sphereNormalWS, _NormalRoundness));

        // 3. Y成分フラッテン（ワールド空間で適用）
        if (_NormalFlattenY > 0.001)
        {
            worldNormal.y *= (1.0 - _NormalFlattenY);
            worldNormal = normalize(worldNormal + float3(0, 0, 0.0001));
        }
    }
    #endif

    // ===== Shadow Receive Mask Setup =====
    // Sample shadow mask once and use it for all shadow-related calculations
    half shadowReceiveMask = 0.0; // Default: fully receive shadows (black = receive shadows)
    #ifdef _SHADOW_RECEIVE_MASK
        if (_UseShadowReceiveMask >= 0.5)
        {
            shadowReceiveMask = NATANE_SAMPLE_SHARED_R(_ShadowReceiveMask, _MainTex, uv);
            shadowReceiveMask = ApplySoftMask(shadowReceiveMask); // Smooth mask transitions
        }
    #endif

    // ===== Lighting Setup =====
    // このセクションの処理:
    // ライト方向と有効ライトカラーを決定するフォールバックチェーン。
    // 1) ディレクショナルライト → 2) 頂点ライト（ポイント/スポット）
    // → 3) SH Light Probe の順に最適な光源を選択する。
    half3 lightDir;
    half3 effectiveLightColor;
    int primaryVertexLightIndex = -1;

    // Light Volume SH is sampled once, here, so the fallback chain below can take its
    // key light from it; the indirect-light step further down reuses the same sample.
    #if defined(UNITY_PASS_FORWARDBASE) && defined(_USE_LIGHT_VOLUME) && !(defined(_BACKGROUND_MODE) && defined(LIGHTMAP_ON))
        #define NATANE_LV_SAMPLED 1
        float3 lvL0, lvL1r, lvL1g, lvL1b;
        LightVolumeSH(i.worldPos, lvL0, lvL1r, lvL1g, lvL1b);
        half3 lvKeyDir, lvKeyColor, lvFillColor;
        NataneSHL1KeyLight(lvL0, lvL1r, lvL1g, lvL1b, lvKeyDir, lvKeyColor, lvFillColor);
        half lvKeyAmount = saturate(_LightVolumeKeyLight);
        // 1 while the Light Volume key light is the main light (no realtime light found)
        half lvKeyIsMain = 0.0;
    #endif

    #ifdef UNITY_PASS_FORWARDBASE
    {
        half dirLightLum = CALC_LUMINANCE(_LightColor0.rgb);
        if (dirLightLum > 0.01)
        {
            // 1. ディレクショナルライトあり: 通常処理
            lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
            effectiveLightColor = _LightColor0.rgb;
        }
        else
        {
            // ディレクショナルライトなし: フォールバックチェーン
            // 2. ポイント/スポットライトから方向を取得（ForwardBase vertex light 配列）
            half3 vlDir, vlColor;
            primaryVertexLightIndex = GetBrightestVertexLightIndex(i.worldPos, vlDir, vlColor);
            half vlLum = CALC_LUMINANCE(vlColor);

            if (vlLum > 0.01)
            {
                lightDir = vlDir;
                effectiveLightColor = vlColor;
            }
            else
            {
                // 3. SH Light Probe から方向を取得
                lightDir = GetSHDominantLightDirection();
                effectiveLightColor = GetSHFallbackLightColor();
                #ifdef NATANE_LV_SAMPLED
                // 3'. Light Volume key light: per-pixel direction and colour from the
                // volume itself (the object-level probe above ignores Light Volumes and
                // point light volumes entirely). Off by default.
                if (lvKeyAmount > 0.001)
                {
                    lightDir = normalize(lerp(lightDir, lvKeyDir, lvKeyAmount));
                    effectiveLightColor = lerp(effectiveLightColor,
                        max(half3(0.05, 0.05, 0.05), lvKeyColor * _LightVolumeIntensity), lvKeyAmount);
                    lvKeyIsMain = 1.0;
                }
                #endif
            }
        }
    }
    #else
        // ForwardAdd: 常に実ライトを使用
        lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
        effectiveLightColor = _LightColor0.rgb;
    #endif

    // ===== Light Direction Snapping (shadow stabilization) =====
    #ifdef _LIGHT_SNAP
    if (_LightSnap >= 0.5)
    {
        // Quantize light direction to discrete angles to prevent shadow flickering
        float snapRad = max(radians(_LightSnapAngle), 0.0001);
        float phi = atan2(lightDir.z, lightDir.x);
        float theta = acos(clamp(lightDir.y, -1.0, 1.0));
        float snappedPhi = round(phi / snapRad) * snapRad;
        float snappedTheta = round(theta / snapRad) * snapRad;
        float3 snappedDir = float3(
            sin(snappedTheta) * cos(snappedPhi),
            cos(snappedTheta),
            sin(snappedTheta) * sin(snappedPhi)
        );
        lightDir = normalize(lerp(lightDir, snappedDir, 1.0 - _LightSnapSmooth));
    }
    #endif

    // ===== Light Color Correction (lilToon互換: LightMinLimit/LightMaxLimit相当) =====
    // ライトカラーの値域をクランプし、過度な明暗を防止する。
    // _LightColorMin (default=0): 暗いワールドでもキャラが見える最低保証
    // _LightColorMax (default=1): 強いライトでもテクスチャが白飛びしない上限
    // _MonochromeLighting (default=0): ライトカラーの色味を除去（グレースケール化）
    // The _LightColorMin floor is a ForwardBase-only guarantee ("stay visible in
    // dark worlds"). Applying it in ForwardAdd would give every point/spot light
    // a distance-independent floor and over-brighten many-light worlds.
    #ifdef UNITY_PASS_FORWARDBASE
        effectiveLightColor = clamp(effectiveLightColor, _LightColorMin, _LightColorMax);
    #else
        effectiveLightColor = min(effectiveLightColor, _LightColorMax);
    #endif
    half lightGray = CALC_LUMINANCE(effectiveLightColor);
    effectiveLightColor = lerp(effectiveLightColor, half3(lightGray, lightGray, lightGray), _MonochromeLighting);
    UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

    // ===== Per-Effect Distance Fade (early calculation) =====
    #ifdef _DISTANCE_FADE
        half distanceFade = 1.0; // Default: fully visible (no fade)
        if (_DistanceFade >= 0.5)
        {
        float distFadeBlurRange = _DistFadeBlur * (_DistanceFadeEnd - _DistanceFadeStart) * DIST_FADE_BLUR_SCALE;
        distanceFade = CalculateDistanceFade(i.worldPos, _DistanceFadeStart - distFadeBlurRange, _DistanceFadeEnd + distFadeBlurRange);

        // Near fade: camera too close → transparent
        if (_NearFadeEnd > _NearFadeStart + 0.001)
        {
            float cameraDist = NataneViewerDistance(i.worldPos);   // eye-centred, mirror-aware
            float nearFadeBlurRange = _DistFadeBlur * (_NearFadeEnd - _NearFadeStart) * DIST_FADE_BLUR_SCALE;
            half nearFade = saturate(
                (cameraDist - (_NearFadeStart - nearFadeBlurRange)) /
                max((_NearFadeEnd + nearFadeBlurRange) - (_NearFadeStart - nearFadeBlurRange), 0.01)
            );
            distanceFade *= nearFade;
        }
        } // if (_DistanceFade >= 0.5)
    #endif

    // ===== PCSS / Shadow Map Smoothing (PCF + Adaptive) =====
    // PCSS (Percentage Closer Soft Shadows) — スクリーンスペース近似:
    //   遮蔽物に近い影はシャープ、遠い影はソフトになるコンタクトハードニング効果。
    //   ブロッカー探索 → ペナンブラ推定 → 可変幅PCF の3フェーズ。
    // Shadow Map Smoothing (従来):
    //   ディレクショナルライト: PCF 3x3 (9-tap) フィルタ。
    //   ポイント/スポットライト: fwidth ベースの適応型 smoothstep。
    #if defined(UNITY_PASS_FORWARDBASE) && defined(SHADOWS_SCREEN) && !defined(UNITY_NO_SCREENSPACE_SHADOWS)
        #ifdef _PCSS
        // Mirrors: the blocker search linearises _CameraDepthTexture, which is either the
        // main view's texture or oblique-encoded there. Fall back to the regular shadow term.
        if (_UsePCSS >= 0.5 && !NataneIsMirror())
        {
            // === PCSS: Percentage Closer Soft Shadows (Screen-Space Approximation) ===
            half pcssOriginalAtten = atten; // 元の atten を保存（Blend 用）
            float2 pcssShadowUV = i._ShadowCoord.xy / max(i._ShadowCoord.w, 0.0001);
            float pcssReceiverDepth = LinearEyeDepth(
                UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, pcssShadowUV).r);

            // Blur: ブロッカー探索半径も拡張して、よりぼかす
            float pcssBlurScale = 1.0 + _PCSSBlur * 4.0;

            // --- Phase 1: Blocker Search ---
            float2 pcssSearchTexelSize = _PCSSBlockerSearchRadius * pcssBlurScale / _ScreenParams.xy;
            float pcssAvgBlockerDepth = 0.0;
            float pcssBlockerCount = 0.0;
            int pcssSamples = clamp((int)_PCSSSampleCount, 8, 32);

            [loop]
            for (int bi = 0; bi < pcssSamples; bi++)
            {
                float2 bOffset = PoissonDisk32[bi] * pcssSearchTexelSize;
                float bShadowVal = UNITY_SAMPLE_SCREENSPACE_TEXTURE(
                    _ShadowMapTexture, pcssShadowUV + bOffset).r;

                if (bShadowVal < 0.5) // In shadow
                {
                    float bSampleDepth = LinearEyeDepth(
                        UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, pcssShadowUV + bOffset).r);
                    pcssAvgBlockerDepth += bSampleDepth;
                    pcssBlockerCount += 1.0;
                }
            }

            if (pcssBlockerCount > 0.0)
            {
                pcssAvgBlockerDepth /= pcssBlockerCount;

                // --- Phase 2: Penumbra Estimation ---
                float pcssDepthDiff = max(pcssReceiverDepth - pcssAvgBlockerDepth, 0.001);
                float pcssPenumbra = pcssDepthDiff * _PCSSLightSize / max(pcssAvgBlockerDepth, 0.01);
                float pcssFilterRadius = clamp(
                    pcssPenumbra * _PCSSSoftness,
                    _PCSSMinFilterRadius,
                    _PCSSMaxFilterRadius);
                // Blur: フィルタ半径にもブラースケールを適用
                float2 pcssFilterTexelSize = pcssFilterRadius * pcssBlurScale / _ScreenParams.xy;

                // --- Phase 3: Variable-Width Poisson Disk PCF ---
                half pcssSum = 0.0;
                [loop]
                for (int fi = 0; fi < pcssSamples; fi++)
                {
                    float2 fOffset = PoissonDisk32[fi] * pcssFilterTexelSize;
                    pcssSum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(
                        _ShadowMapTexture, pcssShadowUV + fOffset).r;
                }
                atten = pcssSum / (half)pcssSamples;
            }
            else
            {
                // No blockers found — fully lit or fully shadowed
                atten = UNITY_SAMPLE_SCREENSPACE_TEXTURE(
                    _ShadowMapTexture, pcssShadowUV).r;
            }

            // --- Blend + BlendMode: PCSS結果を元のattenとブレンド ---
            // ApplyEffectBlendPost (half3版) を使い、atten をグレースケール色として処理
            half3 pcssPreColor = half3(pcssOriginalAtten, pcssOriginalAtten, pcssOriginalAtten);
            half3 pcssPostColor = half3(atten, atten, atten);
            atten = ApplyEffectBlendPost(pcssPreColor, pcssPostColor, _PCSSBlend, _PCSSBlendMode).r;
        }
        #else
        if (_ShadowSmoothing > 0.001)
        {
            // --- Directional Light: PCF 9-tap on screen-space shadow map ---
            float2 shadowUV = i._ShadowCoord.xy / i._ShadowCoord.w;
            float2 texelSize = _ShadowSmoothing * PCF_TEXEL_SCALE / _ScreenParams.xy;

            half pcfShadow = 0;
            [unroll]
            for (int sx = -1; sx <= 1; sx++)
            {
                [unroll]
                for (int sy = -1; sy <= 1; sy++)
                {
                    pcfShadow += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_ShadowMapTexture, shadowUV + float2(sx, sy) * texelSize).r;
                }
            }
            atten = pcfShadow / PCF_SAMPLE_COUNT;
        }
        #endif
    #else
        if (_ShadowSmoothing > 0.001)
        {
            // --- Point/Spot Light: shadow edge only ---
            // "atten" here is distance falloff x cookie x shadow. Remapping the product
            // turned the falloff itself into bands (the old adaptive centre) or a hard disc
            // (any fixed centre), so only the shadow term is remapped and the falloff and
            // cookie are carried through unchanged.
            half shadowOnly = UNITY_SHADOW_ATTENUATION(i, i.worldPos);
            half smoothWidth = min(max(fwidth(shadowOnly), _ShadowSmoothing * SMOOTH_WIDTH_SCALE), 0.5);
            half shadowSmoothed = smoothstep(0.5 - smoothWidth, 0.5 + smoothWidth, shadowOnly);
            atten *= shadowSmoothed / max(shadowOnly, 1e-4);
        }
    #endif

    // ===== Analytic sphere occluder =====
    // A sphere placed in object space (e.g. the head, set on the body material) casts a
    // soft shadow along the light. On a skinned mesh "object space" is the renderer's
    // root, like _FaceForwardDirection: it follows the avatar, not an individual bone.
    if (NATANE_FX(_SphereOccluderStrength > 0.001))
    {
        float3 sphCenterWS = mul(unity_ObjectToWorld, float4(_SphereOccluder.xyz, 1.0)).xyz;
        float sphScale = length(mul((float3x3)unity_ObjectToWorld, float3(0.0, 1.0, 0.0)));
        half sphShadow = NataneSphereSoftShadow(i.worldPos, lightDir, sphCenterWS,
                                                max(_SphereOccluder.w * sphScale, 1e-4), _SphereOccluderSoftness);
        atten *= lerp(1.0, sphShadow, saturate(_SphereOccluderStrength));
    }

    // ===== Depth hair shadow =====
    // Compares the scene depth a short step towards the light with this pixel: where
    // something nearer (the fringe) is found, the face is shaded. Needs the world to
    // provide _CameraDepthTexture; skipped in mirrors and for orthographic cameras,
    // where that texture is not usable. Anything in front casts it, not only hair.
    #if defined(UNITY_PASS_FORWARDBASE) && !defined(_QUEST_LITE)
    if (NATANE_FX(_DepthHairShadow > 0.001) && !NataneIsMirror() && !NataneIsOrthoCamera())
    {
        float4 dhsClip = UnityWorldToClipPos(i.worldPos + lightDir * _DepthHairShadowOffset);
        float4 dhsScreen = ComputeScreenPos(dhsClip);
        float2 dhsUV = dhsScreen.xy / max(dhsScreen.w, 0.0001);
        float dhsScene = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, dhsUV).r);
        float dhsDiff = dhsClip.w - dhsScene;
        // Occluders further than 0.5 m in front are ignored (other avatars, props).
        half dhsShadow = saturate((dhsDiff - _DepthHairShadowBias) * 100.0) * step(dhsDiff, 0.5);
        atten *= 1.0 - dhsShadow * saturate(_DepthHairShadow);
    }
    #endif

    // Apply shadow receive strength (allows controlling how much shadows affect this material)
    // マスクの判定を反転: 白（1.0）= 影を受けない、黒（0.0）= 影を受ける
    half shadowStrength = (1.0 - shadowReceiveMask) * _ShadowReceive;
    atten = lerp(1.0, atten, shadowStrength);

    half3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

    // ===== Expression extras: albedo-level terms =====
    half exprNdotV = saturate(dot(worldNormal, viewDir));
    half3 exprFaceForwardWS = normalize(mul((float3x3)unity_ObjectToWorld, _FaceForwardDirection.xyz) + float3(0, 0, 0.0001));
    half3 exprUpWS = normalize(mul((float3x3)unity_ObjectToWorld, float3(0.0, 1.0, 0.0)));

    // Nose line that fades in profile, where the silhouette already draws the nose.
    if (NATANE_FX(_NoseLineStrength > 0.001))
    {
        half noseMask = NATANE_SAMPLE_SHARED_R(_NoseLineMask, _MainTex, uv);
        half noseFront = saturate(dot(viewDir, exprFaceForwardWS));
        half noseFadeStart = lerp(0.0, 0.9, saturate(_NoseLineFade));
        half noseVisible = smoothstep(noseFadeStart, min(noseFadeStart + 0.15, 1.0), noseFront);
        col.rgb = lerp(col.rgb, col.rgb * _NoseLineColor.rgb, noseMask * noseVisible * saturate(_NoseLineStrength));
    }

    // Sheer stockings: the cloth colour builds up towards grazing angles, where the
    // view passes through more fibre.
    if (NATANE_FX(_Stocking > 0.001))
    {
        half stockingMask = NATANE_SAMPLE_SHARED_R(_StockingMask, _MainTex, uv);
        half stockingDensity = pow(max(1.0 - exprNdotV, 1e-4), max(_StockingPower, 0.5));
        stockingDensity = saturate(stockingDensity + 0.15) * saturate(_Stocking) * stockingMask;
        col.rgb = lerp(col.rgb, _StockingColor.rgb, stockingDensity);
    }

    // Wetness (after Lagarde, "Water drop 3b", 2013): the diffuse albedo darkens, and a
    // sharp water lobe is added with the direct light further down.
    half exprWet = 0.0;
    if (NATANE_FX(_Wetness > 0.001))
    {
        exprWet = saturate(_Wetness) * NATANE_SAMPLE_SHARED_R(_WetnessMask, _MainTex, uv);
        col.rgb *= lerp(1.0, 0.75, exprWet);
    }

    // Baked hair self-occlusion in vertex colour G (roots dark, tips bright).
    #ifdef _VERTEX_COLOR_SHADOW
    if (_HairVertexOcclusion > 0.001)
    {
        col.rgb *= lerp(1.0, saturate(i.color.g), saturate(_HairVertexOcclusion));
    }
    #endif

    // Anisotropic GGX: consumed by every NatanePBRSpecular lobe.
    nataneAniso = clamp(_Anisotropy, -0.95, 0.95);
    nataneAnisoT = normalize(i.worldTangent - worldNormal * dot(i.worldTangent, worldNormal) + float3(0, 1e-5, 0));
    nataneAnisoB = cross(worldNormal, nataneAnisoT);

    // Bent normal: the least occluded direction, used for ambient / SH lookups only.
    half3 ambientNormal = worldNormal;
    if (NATANE_FX(_BentNormalStrength > 0.001))
    {
        half3 bentTS = UnpackNormal(NATANE_SAMPLE_REPEAT(_BentNormalMap, uv));
        half3 bentWS = normalize(i.worldTangent * bentTS.x + i.worldBinormal * bentTS.y + i.worldNormal * bentTS.z);
        ambientNormal = normalize(lerp(worldNormal, bentWS, saturate(_BentNormalStrength)));
    }

    // 2D ramp: second axis of the ramp texture (facing ratio or viewer distance).
    #if defined(_USE_RAMP)
    if (_RampVAxis > 0.5)
    {
        half rampDist = saturate((NataneViewerDistance(i.worldPos) - _RampDistanceRange.x)
                                 / max(_RampDistanceRange.y - _RampDistanceRange.x, 0.001));
        nataneRampV = (_RampVAxis > 1.5) ? rampDist : exprNdotV;
    }
    #endif

    // ===== Smooth Normal Shading Blend =====
    // Blend world normal with smooth normal for softer shadow boundaries on hard-edge models
    half3 shadingNormal = worldNormal;
    #ifdef _SMOOTH_NORMAL
        if (_SmoothNormalShadingBlend > 0.001)
        {
            half3 smoothN = normalize(i.smoothWorldNormal);
            shadingNormal = normalize(lerp(worldNormal, smoothN, _SmoothNormalShadingBlend));
        }
    #endif
    // ===== Face shading flatten =====
    // Pulls the shading normal towards the face-forward axis, so cheeks, nose and brow
    // stop producing mesh-dependent blotches and the face switches between lit and shade
    // more as one plane (the "reduce diffuse shading on the face" practice of anime
    // character shaders). Normal-map detail, specular and rim keep the real normal.
    // Proxy normal: shade the face as a sphere (or, with the cylinder blend, a vertical
    // cylinder) around an object-space centre, the classic clean-terminator trick.
    if (NATANE_FX(_FaceProxyNormal > 0.001))
    {
        float3 proxyDir = i.worldPos - mul(unity_ObjectToWorld, float4(_FaceProxyCenter.xyz, 1.0)).xyz;
        proxyDir -= exprUpWS * (dot(proxyDir, exprUpWS) * saturate(_FaceProxyCylinder));
        shadingNormal = normalize(lerp(shadingNormal, normalize(proxyDir + float3(0, 0, 1e-5)), saturate(_FaceProxyNormal)));
    }
    if (NATANE_FX(_FaceFlatten > 0.001))
    {
        half faceFlattenAmount = saturate(_FaceFlatten) * NATANE_SAMPLE_SHARED_R(_FaceFlattenMask, _MainTex, uv);
        shadingNormal = normalize(lerp(shadingNormal, exprFaceForwardWS, faceFlattenAmount * 0.98) + float3(0, 0.0001, 0));
    }
    // Vertical light limit: removes the up / down part of the light direction (relative
    // to the avatar's up axis) so light from below or straight above cannot distort the
    // face. Cast shadows still come from the real light, so the two can disagree.
    if (NATANE_FX(_FaceLightVerticalLimit > 0.001))
    {
        lightDir = normalize(lightDir - exprUpWS * (dot(lightDir, exprUpWS) * saturate(_FaceLightVerticalLimit) * 0.95)
                             + exprFaceForwardWS * 1e-4);
    }
    half ndotl = dot(shadingNormal, lightDir);

    // Light source size: an area (sphere) light keeps lighting past the geometric
    // horizon, which widens the terminator. Quadratic horizon fit; own approximation.
    if (NATANE_FX(_LightSourceSize > 0.001))
    {
        half lightSinA = saturate(_LightSourceSize) * 0.5;
        if (ndotl < lightSinA)
        {
            half lightHorizon = lightSinA + ndotl;
            ndotl = (lightHorizon > 0.0) ? (lightHorizon * lightHorizon) / (4.0 * lightSinA) : lightHorizon;
        }
    }

    // Directional shadow offset map (after Todo et al., "Locally Controllable Stylized
    // Shading", 2007): painted offsets for the terminator, one channel per light side.
    // R = light from the character's right, G = left, B = front, A = back; 0.5 = none.
    if (NATANE_FX(_ShadowOffsetMapStrength > 0.001))
    {
        half4 offsetTex = NATANE_SAMPLE_SHARED(_ShadowOffsetMap, _MainTex, uv);
        half3 offsetRightWS = normalize(mul((float3x3)unity_ObjectToWorld, _FaceRightDirection.xyz) + float3(1e-4, 0, 0));
        half offsetLR = dot(lightDir, offsetRightWS);
        half offsetFB = dot(lightDir, exprFaceForwardWS);
        half4 offsetW = half4(saturate(offsetLR), saturate(-offsetLR), saturate(offsetFB), saturate(-offsetFB));
        half offsetValue = dot(offsetTex, offsetW) / max(dot(offsetW, half4(1.0, 1.0, 1.0, 1.0)), 1e-3);
        ndotl += (offsetValue - 0.5) * 2.0 * saturate(_ShadowOffsetMapStrength);
    }
    half exprRawNdotL = ndotl;

    // ===== Bump terminator softening =====
    // Only the lit side is scaled, so the shade never gets darker than before.
    #ifdef _NORMALMAP
    if (_BumpTerminatorStrength > 0.001)
    {
        half bumpShadow = NataneBumpTerminatorShadow(normalize(i.worldNormal), worldNormal, lightDir);
        ndotl = ndotl > 0.0 ? ndotl * lerp(1.0, bumpShadow, saturate(_BumpTerminatorStrength)) : ndotl;
    }
    #endif

    // ===== Matte diffuse (energy-preserving Oren-Nayar) =====
    // Reshapes N.L before the toon ramp, so the terminator itself moves the way a rough
    // surface's does (it works with steps, gradient and ramp alike), and keeps the
    // albedo-dependent part as a tint for the lit colour. Placed before the SDF / shape
    // rig so art-directed face shadows still have the last word.
    half3 matteDiffuseTint = half3(1.0, 1.0, 1.0);
    if (NATANE_FX(_MatteDiffuse > 0.001) && ndotl > 0.0)
    {
        half matteNdotV = max(dot(shadingNormal, viewDir), 1e-3);
        half3 matteRatio = NataneEONDiffuseRatio(ndotl, matteNdotV, dot(lightDir, viewDir),
                                                 saturate(_MatteDiffuseRoughness), saturate(col.rgb));
        half matteRatioLum = max(CALC_LUMINANCE(matteRatio), 1e-3);
        half matteAmount = saturate(_MatteDiffuse);
        ndotl *= lerp(1.0, matteRatioLum, matteAmount);
        matteDiffuseTint = lerp(half3(1.0, 1.0, 1.0), matteRatio / matteRatioLum, matteAmount);
    }

    // ===== SDF Shadow Map =====
    // Apply SDF shadow to ndotl before lighting calculations
    ndotl = ApplySDFShadow(uv, ndotl, lightDir, i.worldPos);

    // ===== Shadow Shape Rig =====
    // Art-directed local shaping of the shading boundary.
    //
    // Placed here deliberately:
    //   - after the SDF so the rigs refine the face shadow the map already decided,
    //   - before vertex-color shadow / wrapped diffuse so it does not double up with
    //     those threshold adjustments,
    //   - before tone quantization (_USE_MULTI_SHADOW / ramp) so the step boundaries
    //     themselves move rather than the shading inside a band.
    ndotl = ApplyShadowShapeRig(uv, ndotl, lightDir);

    // ===== Vertex Color Shadow Threshold =====
    #ifdef _VERTEX_COLOR_SHADOW
    if (_VertexColorShadow >= 0.5)
    {
        // 頂点カラーR値で影閾値をオフセット（R=0.5がデフォルト）
        half vcOffset = (i.color.r - _VCShadowThreshold) + _VCShadowPush;
        ndotl = saturate(ndotl + vcOffset);
    }
    #endif

    // ===== Wrapped Diffuse =====
    // (NdotL + wrap) / (1 + wrap) — 0=Lambert, 0.5=Half-Lambert, 1=Uniform
    // 実効値は _ShadowNaturalness で下限が押し上げられる（単一入口）。
    half effectiveWrap = NataneShadowNaturalWrap();
    if (effectiveWrap > 0.001)
    {
        ndotl = (ndotl + effectiveWrap) / (1.0 + effectiveWrap);
    }

    // ===== Backlight Calculation =====
    // Calculate light coming from behind the object (rim-like effect)
    half backlight = 0.0;
    {
        half backlightDot = max(0.0, dot(worldNormal, -lightDir));
        float backlightPowerBlurred = max(BACKLIGHT_MIN_POWER, BACKLIGHT_SCALE * (1.0 - _BacklightBlur * BACKLIGHT_BLUR_INFLUENCE));
        backlight = pow(backlightDot, backlightPowerBlurred) * _BacklightIntensity;
    }

    // ===== Toon/Ramp Shading =====
    // このセクションの処理:
    // NdotL を基にトゥーンシェーディング（階段状）またはランプテクスチャで
    // 陰影の明暗を計算し、AO・ディザリング・シャドウアッテネーションを適用して
    // 最終的な shadingValue（0=影、1=明るい）と shadowColor を決定する。
    // Calculate base light term
    half lightTerm = ndotl;

    // Apply shadow receive mask to light term
    // 白いマスク部分（shadowReceiveMask = 1.0）では常に明るく保つ
    // これにより、ndotlの影響を受けずにシェーディングを無効化できる
    lightTerm = lerp(lightTerm, 1.0, shadowReceiveMask);

    // Apply lit area softness - Optimized: removed branching
    // Softness calculation always executes (branch removal for better GPU performance)
    half smoothedLight = smoothstep(0.0, 1.0, lightTerm);
    lightTerm = lerp(lightTerm, smoothedLight, _LitSoftness);

    // Apply dithering to soften shadow boundaries
    #ifdef _USE_DITHERING
        half ditherPattern = DitheringPattern(i.pos.xy, _DitheringScale);
        // Apply dithering to shadow boundary area (around 0.4-0.6 range)
        half ditherRange = saturate(1.0 - abs(lightTerm - 0.5) * 2.0);
        float ditherStrengthBlurred = saturate(_DitheringStrength + _DitheringBlur * 0.5);
        half ditherEffect = (ditherPattern - 0.5) * ditherStrengthBlurred * ditherRange;
        half preDitherLightTerm = lightTerm;
        lightTerm = saturate(lightTerm + ditherEffect);
        lightTerm = lerp(preDitherLightTerm, lightTerm, _DitheringBlend);
    #endif

    // Apply unified lighting softness controls (Light Blend / Highlight Softness).
    lightTerm = ApplyLightBlend(lightTerm);

    // ===== Toon/Ramp Shading =====
    half3 lighting;
    half shadingValue;
    half3 shadowColor;

    // 量子化前の連続的な陰影。網点のように「面の丸みを濃度で見せる」表現は、
    // トゥーンで段に落とした後の値を使うと濃度がその段数しか取れない
    // （_ShadowSteps = 2 なら網点も 2 段階しか出ず、トーンを貼り分けたというより
    // 白と黒が切り替わるだけになる）。PBR 相当の連続値を別に持っておき、
    // そちらを濃度の基準に選べるようにする。PBR モードでなくても使える。
    //
    // 落ち影（atten）は<b>掛けない</b>。掛けてしまうと、落ち影の有無と
    // N·L の陰影が 1 つの値に混ざり、「落ち影が無い場所にも落ち影ぶんのトーンが乗る」
    // のを分離できなくなる。落ち影は使う側で別枠に足す。
    half continuousShadingLit = 1.0;

    half aoForIndirect = 1.0;

    // ===== AO (pre-calculate before shading branch) =====
    half aoEffect = 1.0;
    // 1 = reflections untouched. Set in the ForwardBase indirect step.
    half exposureOcclusion = 1.0;
    #ifdef _USE_AO
        half ao = NATANE_SAMPLE_SHARED_BLUR_R(_AOMap, _MainTex, uv, _AOBlur);
        ao = ApplySoftMask(ao);
        aoEffect = lerp(1.0, ao, _AOIntensity);
        aoForIndirect = lerp(1.0, ao, _AOIntensity * AO_INDIRECT_STRENGTH);

        // Shadow proneness: occluded texels drop into shade earlier and stay there, so
        // creases and the under-chin read as stable painted shadows (the AO-like
        // "how easily this area goes into shade" weight of Guilty Gear Xrd's ILM map).
        // Unlike the micro shadow below it does not depend on the light angle.
        lightTerm -= (1.0 - aoEffect) * saturate(_AOShadowProne);

        // Micro-shadowing (Chan, "Material Advances in Call of Duty: WWII", SIGGRAPH 2018;
        // form used by Filament). The AO value is read as the opening of a cone above the
        // surface, and the main light is cut off once it drops below that cone, so creases,
        // weave and collar folds get small directional shadows without a shadow map.
        // Applied to the light term BEFORE the toon ramp so it moves the step instead of
        // adding a smooth gradient on top of it.
        if (_AOMicroShadow > 0.001)
        {
            half microAperture = rsqrt(1.0 - min(aoEffect, 0.9999));
            half microShadow = saturate(max(lightTerm, 0.0) * microAperture);
            lightTerm = lerp(lightTerm, min(lightTerm, lightTerm * microShadow * microShadow), saturate(_AOMicroShadow));
        }
    #endif

    // ===== Procedural AO (Height-based) =====
    #ifdef _PROCEDURAL_AO
    if (_ProceduralAO >= 0.5)
    {
        // オブジェクト空間Y座標からAOグラデーション生成
        float proceduralAO = smoothstep(0.0, max(_ProceduralAOSoftness, 0.01),
                                         i.objectPos.y + _ProceduralAOHeightOffset);
        proceduralAO = lerp(1.0, proceduralAO, _ProceduralAOIntensity);
        // 既存のaoEffectに乗算で合成
        aoEffect *= proceduralAO;
    }
    #endif

    half cavityEffect = 1.0;
    if (NATANE_FX(_CavityStrength > 0.001))
    {
        half cavity = NATANE_SAMPLE_SHARED_R(_CavityMap, _MainTex, uv);
        cavity = ApplySoftMask(cavity);
        cavityEffect = lerp(1.0, cavity, _CavityStrength);
    }

    half specularVisibility = saturate(aoForIndirect * cavityEffect);
    // Mirror-safe NdotV for specular occlusion
    float3 soViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    soViewNormal.x *= NataneMirrorSign();
    half soNdotV = saturate(dot(normalize(soViewNormal), float3(0, 0, 1)));
    half specularOcclusion = NataneSpecularOcclusion(
        specularVisibility,
        soNdotV,
        saturate(1.0 - _Smoothness));
    specularOcclusion = lerp(1.0, specularOcclusion, _SpecularOcclusionStrength);
    half3 lookWeights = NataneResolveLookWeights();
    half basePbrWeight = NataneResolveBasePbrWeight(lookWeights);
    half nprWeight = NataneResolveNprWeight(lookWeights);

    #ifdef _USE_RAMP
        half rampInput = lightTerm;
        #ifdef _USE_AO
            rampInput *= aoEffect;
        #endif
        // 落ち影を掛ける前の連続値。Ramp モードでは rampInput 自体が連続なので
        // そのまま濃度の基準に使える。
        continuousShadingLit = saturate(rampInput + clamp(_ShadowOffset, -1.0, 1.0));
        // Shadow attenuation (separated from NdotL for clean toon boundaries)
        rampInput *= atten;
        // Use ramp texture for custom shadow gradients
        lighting = RampShading(rampInput);
        shadingValue = saturate(rampInput + clamp(_ShadowOffset, -1.0, 1.0));
        shadowColor = lighting;
    #else
        // Toon or Gradient shading. _ShadingMode is a material constant, so only the mode in
        // use is evaluated (measured with RenderDoc: computing both and blending cost about
        // 50 instructions per pixel for nothing). The screen-space width is taken out here
        // because derivatives cannot live inside a branch.
        half pbrLikeValue = saturate(lightTerm + clamp(_ShadowOffset, -1.0, 1.0));
        half shadingAA = fwidth(pbrLikeValue);
        half stylizedMode = step(HALF_VALUE, _ShadingMode) * (1.0 - step(1.5, _ShadingMode));
        half stylizedShadingValue;
        UNITY_BRANCH
        if (stylizedMode > 0.5)
        {
            stylizedShadingValue = GradientShadingAA(pbrLikeValue, shadingAA, _ShadingGradientWidth);
        }
        else
        {
            half toonValue = ToonShadingAA(pbrLikeValue, shadingAA * abs(_ShadowSteps), _ShadowSteps, _ShadowSharpness);

            // ===== Shadow Step Smoothing =====
            // 多段階トゥーンシェーディングの階調をなじませる
            // 量子化されたステップを連続的なライティングに向けてブレンド
            half effectiveSmoothing = NataneShadowNaturalSmoothing();
            if (effectiveSmoothing > 0.001)
            {
                toonValue = lerp(toonValue, pbrLikeValue, effectiveSmoothing);
            }
            stylizedShadingValue = toonValue;
        }

        // Blend between stylized base shading and PBR-like response.
        shadingValue = lerp(stylizedShadingValue, pbrLikeValue, basePbrWeight);

        // 量子化を通していない側。PBR モードかどうかに関係なく常に連続。
        continuousShadingLit = pbrLikeValue;

        // Apply Shading Grade Map before final lighting
        shadingValue = ApplyShadingGradeMap(uv, shadingValue);
        continuousShadingLit = ApplyShadingGradeMap(uv, continuousShadingLit);

        // ===== Shadow Edge Noise (hand-drawn shadow boundaries) =====
        #ifdef _SHADOW_EDGE_NOISE
        if (_ShadowEdgeNoise >= 0.5)
        {
            // Only apply noise near shadow boundaries (shadingValue 0.2-0.8)
            float edgeMask = 1.0 - saturate(abs(shadingValue - 0.5) * 4.0);
            // Value noise from world position
            float2 noiseUV = i.worldPos.xz * _ShadowNoiseScale * 0.01;
            noiseUV += _Time.y * _ShadowNoiseSpeed * 0.1;
            #if defined(_LINE_BOIL)
            if (_LineBoilAffectHatching >= 0.5)
                noiseUV += nataneBoilUV;
            #endif
            float noise = frac(sin(dot(noiseUV, float2(12.9898, 78.233))) * 43758.5453);
            noise = noise * 2.0 - 1.0; // remap to [-1, 1]
            shadingValue += noise * _ShadowNoiseIntensity * edgeMask;
            shadingValue = saturate(shadingValue);
        }
        #endif

        // ===== AO (apply cached AO to shading stage) =====
        #ifdef _USE_AO
            half preShadingAO = shadingValue;
            shadingValue *= aoEffect;
            // Apply AO blend (how much AO affects the shading)
            shadingValue = lerp(preShadingAO, shadingValue, _AOBlend);

            half preContinuousAO = continuousShadingLit;
            continuousShadingLit = lerp(preContinuousAO, continuousShadingLit * aoEffect, _AOBlend);
        #endif

        // ===== Shadow Attenuation (separated from NdotL for clean toon boundaries) =====
        // continuousShadingLit には atten を掛けない（落ち影は使う側で別枠に足す）。
        shadingValue *= atten;

        // Vibrant shadow color mixing for anime look
        half3 litColor = 1.0;

        // Use multi-shadow tone settings (or fallback to single shadow color)
        shadowColor = MultiToneShadowColor(shadingValue, half3(1.0, 1.0, 1.0));

        // Optional texture-driven shadow tinting for richer NPR shadow control
        half shadowColorTexStrength = saturate(_ShadowColorTexStrength);
        if (shadowColorTexStrength > 0.001)
        {
            half3 shadowColorTex = NATANE_SAMPLE_REPEAT(_ShadowColorTex, uv).rgb;
            half3 texturedShadowColor = shadowColor * shadowColorTex;
            shadowColor = lerp(shadowColor, texturedShadowColor, shadowColorTexStrength);
        }

        // ===== Cast Shadow Color Control =====
        #ifdef _CAST_SHADOW_COLOR
        if (_CastShadowColorEnable >= 0.5)
        {
            // Detect cast shadow: low attenuation but surface facing light
            float castShadowMask = saturate((1.0 - atten) * saturate(ndotl + 0.5));
            shadowColor = lerp(shadowColor, shadowColor * _CastShadowTint.rgb, castShadowMask * _CastShadowIntensity);
        }
        #endif

        // Preserve hue and saturation better in shadows
        lighting = lerp(shadowColor, litColor, shadingValue);
    #endif

    // ===== Automatic shadow colour (OKLab) =====
    // Derives the shade from the albedo in a perceptual space: darker, more saturated
    // and hue-rotated, instead of a hand-picked multiply colour. Not used by Ramp mode.
    if (NATANE_FX(_ShadowAutoColor > 0.001))
    {
        half3 autoShadow = NataneOKLabShadowMultiplier(col.rgb, _ShadowAutoHue, _ShadowAutoChroma, _ShadowAutoLightness);
        shadowColor = lerp(shadowColor, autoShadow, saturate(_ShadowAutoColor));
    }

    // ===== Shadow Color HSV Shift =====
    if (NATANE_FX(abs(_ShadowHueShift) > 0.001 || abs(_ShadowSaturation - 1.0) > 0.001))
    {
        float3 shadowHSV = RGBtoHSV(shadowColor);
        shadowHSV.x = frac(shadowHSV.x + _ShadowHueShift);
        shadowHSV.y = saturate(shadowHSV.y * _ShadowSaturation);
        shadowColor = HSVtoRGB(shadowHSV);
    }

    // Apply shadow max darkness limit (prevents shadows from being too black)
    shadowColor = max(shadowColor, saturate(_ShadowMaxDarkness));

    // ===== Backlight Calculation (used in ForwardBase Step 4) =====
    // (backlight was already calculated above at line ~128)

    // ===== ForwardBase: Natural Lighting Pipeline =====
    // このセクションの処理:
    // 5ステップのライティング合成パイプライン:
    // STEP 1: 間接光（Light Volume / SH Light Probe）
    // STEP 2: 影の環境色（LV使用時のみ） → STEP 3: 直接光（ライトカラー適用）
    // STEP 4: 追加光（頂点ライト・バックライト・LTCGI）
    // STEP 5: 最終合成（LVブレンドモード選択 or max合成）
    #ifdef UNITY_PASS_FORWARDBASE
        // ========== STEP 1: Indirect Light ==========
        half3 indirectResult = half3(0, 0, 0);
        float3 ambient = float3(0, 0, 0);

        #if defined(_BACKGROUND_MODE) && defined(LIGHTMAP_ON)
            // Background: ライトマップから間接光を取得
            half3 lmColor = SampleNataneLightmap(i.lightmapUV, worldNormal) * _LightmapIntensity;
            indirectResult = lmColor;
            ambient = float3(0, 0, 0);
            // ライトマップ輝度でトゥーンシェーディング結果を調整
            half lmLum = saturate(CALC_LUMINANCE(lmColor));
            shadingValue = lerp(shadingValue, lmLum, _LightmapToonInfluence);
            // Recompute lighting with updated shadingValue
            lighting = lerp(shadowColor, half3(1, 1, 1), shadingValue);
        #elif defined(_USE_LIGHT_VOLUME)
            half3 preLightVolume = lighting;
            // Sample Light Volume SH coefficients
            // (sampled once in the lighting setup above)
            float3 L0 = lvL0, L1r = lvL1r, L1g = lvL1g, L1b = lvL1b;

            // Evaluate direct and indirect lighting from Light Volume
            // _LightVolumeSHMode 0 (default) is the plain linear evaluation, as before.
            float3 directLightLV = NataneLightVolumeEvaluateMode(ambientNormal, L0, L1r, L1g, L1b, _LightVolumeSHMode);
            float3 indirectLightLV = NataneLightVolumeEvaluateMode(-ambientNormal, L0, L1r, L1g, L1b, _LightVolumeSHMode);

            // Gentle clamp to prevent over-brightening
            directLightLV = min(directLightLV, float3(1.1, 1.1, 1.1));
            indirectLightLV = min(indirectLightLV, float3(1.1, 1.1, 1.1));

            // Apply intensity controls
            directLightLV *= _LightVolumeIntensity;
            indirectLightLV *= _LightVolumeIntensity;

            // Light Volume key light: the ambient floor becomes the light that really
            // reaches the side facing away from the volume's dominant direction, instead
            // of the opposite-normal irradiance (which lights the shade with the key
            // light's own energy and washes the toon boundary out).
            half3 lvFillLight = min(lvFillColor, float3(1.1, 1.1, 1.1)) * _LightVolumeIntensity;
            indirectLightLV = lerp(indirectLightLV, lvFillLight, lvKeyAmount);

            // Apply Shadow Receive Mask to indirect Light Volume
            float lvInfluence = (1.0 - shadowReceiveMask);
            indirectLightLV *= lvInfluence;

            indirectResult = indirectLightLV;

            // LV使用時はUnityのAmbient Colorを使用しない（LVが環境光を提供するため）
            ambient = float3(0, 0, 0);
        #else
            // Fallback: Unity Light Probes (LPPV-aware via NataneShadeSH).
            // When a Light Probe Proxy Volume drives this renderer the SH terms
            // are sampled per-pixel from the probe volume; otherwise this is
            // identical to the classic ShadeSH9() path.
            // L0 (uniform ambient) for indirect
            float3 shAverage = NataneShadeSH(half3(0, 0, 0), i.worldPos);
            indirectResult = max(0, shAverage);

            // Directional SH for ambient contribution
            float3 shDirect = NataneShadeSH(ambientNormal, i.worldPos);
            float3 shIndirect = NataneShadeSH(-ambientNormal, i.worldPos);
            ambient = lerp(shIndirect, shDirect, SH_INDIRECT_BLEND);
            ambient *= _IndirectLightIntensity * _GIIntensity;
        #endif

        // Exposure occlusion (as in Filamented): probe reflections are faded where the
        // light actually reaching the surface is dark, so a bright reflection probe does not
        // make skin, eyes and metal glow inside a dark room.
        if (NATANE_FX(_ReflectionExposureOcclusion > 0.001))
        {
            exposureOcclusion = saturate(length(indirectResult + effectiveLightColor) / _ReflectionExposureOcclusion);
        }

        // Indirect light minimum color (prevents completely dark characters in dark worlds)
        indirectResult = max(indirectResult, _IndirectLightMinColor.rgb);
        indirectResult *= _IndirectLightIntensity * _GIIntensity;
        // AO on indirect light (50% strength to preserve ambient visibility)
        #ifdef _USE_AO
        if (_AOMultiBounce > 0.001)
        {
            // Multi-bounce AO (Jimenez et al., "Practical Real-Time Strategies for Accurate
            // Indirect Occlusion", SIGGRAPH 2016). Light bouncing inside a crease picks up
            // the surface colour, so occluded bright / saturated areas (skin folds, white
            // cloth) stay warm instead of going grey-black.
            half3 mbAlbedo = saturate(col.rgb);
            half3 mbA = 2.0404 * mbAlbedo - 0.3324;
            half3 mbB = -4.7951 * mbAlbedo + 0.6417;
            half3 mbC = 2.7552 * mbAlbedo + 0.6903;
            half3 mbVisibility = max(aoForIndirect, ((aoForIndirect * mbA + mbB) * aoForIndirect + mbC) * aoForIndirect);
            indirectResult *= lerp(half3(aoForIndirect, aoForIndirect, aoForIndirect), mbVisibility, saturate(_AOMultiBounce));
        }
        else
        #endif
        indirectResult *= aoForIndirect;

        // ========== STEP 2: Shadow Environment Color (environment-aware shadow tinting) ==========
        #ifdef _USE_LIGHT_VOLUME
            shadowColor = lerp(shadowColor, half3(1, 1, 1),
                              saturate(indirectResult * _ShadowEnvStrength));
        #endif

        // ========== STEP 3: Direct Light ==========
        #ifdef _USE_RAMP
            half3 directResult = lighting; // Ramp already provides colored shadow-to-lit
        #else
            half3 directResult = lerp(shadowColor, half3(1, 1, 1), shadingValue);
        #endif

        // ===== Shadow border tint (terminator colour) =====
        // A saturated band along the lit / shadow boundary, the cue that makes skin and
        // cloth read as lit volumes rather than two flat tones. "borderEdge" follows
        // whatever transition the shading mode produced (toon step, gradient, ramp, cast
        // shadow edge); "borderHalo" widens it around the N.L terminator by _ShadowBorderWidth.
        if (NATANE_FX(_ShadowBorderStrength > 0.001))
        {
            half borderEdge = 4.0 * shadingValue * (1.0 - shadingValue);
            half borderHalo = saturate(1.0 - abs(continuousShadingLit * atten - 0.5) / max(_ShadowBorderWidth * 0.5, 0.001));
            borderHalo *= step(0.001, _ShadowBorderWidth);
            half borderMask = saturate(max(borderEdge, borderHalo)) * saturate(_ShadowBorderStrength);
            borderMask *= (1.0 - shadowReceiveMask);
            directResult *= lerp(half3(1, 1, 1), _ShadowBorderColor.rgb, borderMask);
        }

        // Matte diffuse: albedo-dependent multiple-scattering tint (1 when the feature is off)
        directResult *= matteDiffuseTint;

        // ===== Expression extras: diffuse response =====
        // Lit-sphere in light space (after Todo, Anjyo & Yokoyama, "Lit-Sphere Extension",
        // 2013): a painted sphere whose "up" is the light, so the painted shading turns
        // with the light instead of the camera.
        if (NATANE_FX(_LitSphereStrength > 0.001))
        {
            half3 lsRef = (abs(lightDir.y) > 0.95) ? half3(0.0, 0.0, 1.0) : half3(0.0, 1.0, 0.0);
            half3 lsRight = normalize(cross(lsRef, lightDir));
            // U = across the light, V = towards the light (top of the texture = lit pole).
            float2 lsUV = float2(dot(shadingNormal, lsRight), dot(shadingNormal, lightDir)) * 0.5 + 0.5;
            half3 litSphere = NATANE_SAMPLE_CLAMP_LOD(_LitSphereTex, lsUV, 0).rgb;
            directResult = lerp(directResult, litSphere * lerp(shadowColor, half3(1.0, 1.0, 1.0), atten), saturate(_LitSphereStrength));
        }
        // Skin: red bleed around the terminator (the look of pre-integrated skin without
        // its LUT; a band fit, not the published curve).
        if (NATANE_FX(_SkinTerminatorScatter > 0.001))
        {
            half scatterBand = saturate(1.0 - abs(exprRawNdotL) / max(_SkinScatterWidth, 0.02));
            scatterBand *= scatterBand;
            directResult += _SkinScatterColor.rgb * (scatterBand * saturate(_SkinTerminatorScatter) * 0.5 * lerp(1.0, atten, 0.5));
        }
        // Skin: per-channel diffuse normals (Hable, Uncharted 2). Red follows the smooth
        // geometric normal, blue the detailed one, so pores stay crisp without grey speckles.
        #ifdef _NORMALMAP
        if (_SkinDiffuseNormalBlur > 0.001)
        {
            half normalBlurDelta = saturate(dot(normalize(i.worldNormal), lightDir)) - saturate(dot(worldNormal, lightDir));
            directResult = max(half3(0, 0, 0), directResult + half3(1.0, 0.45, 0.1) * (normalBlurDelta * saturate(_SkinDiffuseNormalBlur) * 0.6));
        }
        #endif
        // Subsurface flatten (the Hanrahan-Krueger term of the Disney BRDF): lifts grazing
        // angles and flattens the centre, for a soft waxy volume.
        if (NATANE_FX(_SubsurfaceFlatten > 0.001))
        {
            half ssNL = saturate(exprRawNdotL);
            half3 ssH = normalize(lightDir + viewDir);
            half ssLH = saturate(dot(lightDir, ssH));
            half ssFss90 = ssLH * ssLH * saturate(1.0 - _Smoothness);
            half ssFL = pow(1.0 - ssNL, 5.0);
            half ssFV = pow(1.0 - exprNdotV, 5.0);
            half ssFss = lerp(1.0, ssFss90, ssFL) * lerp(1.0, ssFss90, ssFV);
            half ssTerm = 1.25 * (ssFss * (1.0 / max(ssNL + exprNdotV, 0.1) - 0.5) + 0.5);
            directResult *= lerp(1.0, clamp(ssTerm * 1.6, 0.8, 1.25), saturate(_SubsurfaceFlatten));
        }
        // Cloth subsurface colour (Filament cloth model): the shade of thick cloth takes
        // the colour of the light scattered inside it.
        if (NATANE_FX(_ClothSubsurface > 0.001))
        {
            half clothWrap = saturate((exprRawNdotL + 0.5) / 1.5);
            half3 clothLit = saturate(directResult + _ClothSubsurfaceColor.rgb * clothWrap * (1.0 - saturate(directResult)));
            directResult = lerp(directResult, clothLit, saturate(_ClothSubsurface));
        }

        // Light color application (with LightColorInfluence preservation)
        half lightColorLum = CALC_LUMINANCE(effectiveLightColor);
        half3 colorMultiplied = directResult * saturate(effectiveLightColor);
        half3 luminanceOnly = directResult * lightColorLum;
        half3 lightColorInfluenced = lerp(luminanceOnly, colorMultiplied, _LightColorInfluence);
        directResult = lightColorInfluenced * max(0.0, _LightIntensity);

        // Light influence clamping: rescale by luminance ratio to preserve hue.
        // (normalize() here would darken neutral light by ~1/sqrt(3).)
        half directLum = CALC_LUMINANCE(directResult);
        half directLumClamped = clamp(directLum, _LightMinInfluence, _LightMaxInfluence);
        // Soft knee: approach the upper limit along a curve instead of a hard clip.
        if (NATANE_FX(_LightMaxSoftKnee > 0.001))
        {
            half kneeRange = max(_LightMaxInfluence * saturate(_LightMaxSoftKnee) * 0.9, 1e-3);
            half kneeStart = _LightMaxInfluence - kneeRange;
            if (directLum > kneeStart)
            {
                directLumClamped = max(_LightMinInfluence, kneeStart + kneeRange * (1.0 - exp(-(directLum - kneeStart) / kneeRange)));
            }
        }
        directResult = (directLum > 1e-4)
            ? directResult * (directLumClamped / directLum)
            : half3(1, 1, 1) * directLumClamped; // black input: neutral floor at _LightMinInfluence

        // ========== STEP 4: Additional Light ==========
        half3 additionalResult = half3(0, 0, 0);

        // Vertex Lights
        #if defined(_PIXEL_VERTEX_LIGHTS)
            additionalResult += CalculateVertexLightsPixelPrecision(
                i.worldPos, shadingNormal, _ShadowSteps, _ShadowSharpness, _ShadingMode, _ShadingGradientWidth, primaryVertexLightIndex);
        #elif defined(VERTEXLIGHT_ON)
            additionalResult += i.vertexLightColor;
        #endif
        additionalResult *= _AdditionalLightIntensity;

        // Backlight (apply blend amount)
        half backlightBlendFaded = _BacklightBlend;
        #ifdef _DISTANCE_FADE
            backlightBlendFaded *= lerp(1.0, distanceFade, _BacklightDistFade);
        #endif
        additionalResult += backlight * _BacklightColor.rgb * effectiveLightColor * backlightBlendFaded;

        // Ambient rim (Mitchell et al., "Illustrative Rendering in Team Fortress 2", 2007):
        // an upward-facing fresnel rim lit by the ambient light, so the silhouette
        // separates from the background even without a realtime light.
        if (NATANE_FX(_AmbientRimIntensity > 0.001))
        {
            half ambientRim = pow(max(1.0 - exprNdotV, 1e-4), max(_AmbientRimPower, 0.5)) * saturate(dot(worldNormal, float3(0.0, 1.0, 0.0)));
            additionalResult += max(indirectResult, ambient) * (ambientRim * _AmbientRimIntensity);
        }
        // Wetness: water film highlight (F0 0.02), independent of the material's own gloss.
        if (exprWet > 0.001)
        {
            // A water film is isotropic whatever the cloth or hair underneath is.
            half wetSavedAniso = nataneAniso;
            nataneAniso = 0.0;
            half3 wetSpec = NatanePBRSpecular(worldNormal, viewDir, lightDir, max(_WetnessRoughness, 0.02),
                                              half3(0.04, 0.04, 0.04), effectiveLightColor, atten);
            nataneAniso = wetSavedAniso;
            additionalResult += min(wetSpec * 8.0, 2.0) * (exprWet * specularOcclusion);
        }

        // LTCGI (diffuse → additional, specular → applied after base lighting)
        #if defined(_LTCGI)
            float3 ltcgiDiffuse = 0;
            float3 ltcgiSpecular = 0;
            NataneLTCGIContribution(i.worldPos, worldNormal, viewDir,
                1.0 - _Smoothness, i.uv1,
                ltcgiDiffuse, ltcgiSpecular);
            additionalResult += ltcgiDiffuse * _LTCGIIntensity * _LTCGIBlend;
        #endif

        // ========== PBR Direct + Indirect Specular (Background only) ==========
        #ifdef _PBR
        {
            half2 metallicGloss = NATANE_SAMPLE_REPEAT(_PBR_MetallicGlossMap, uv).ra;
            half metallic = metallicGloss.x * _PBR_Metallic;
            half smoothness = metallicGloss.y * _PBR_Smoothness;
            half roughness = max(0.04, 1.0 - smoothness);

            // F0: non-metal=0.04, metal=albedo color
            half3 F0 = lerp(half3(0.04, 0.04, 0.04), col.rgb, metallic);

            // Energy conservation: metals reduce diffuse
            directResult *= (1.0 - metallic * basePbrWeight);

            // GGX direct specular
            half3 pbrSpec = NatanePBRSpecular(worldNormal, viewDir, lightDir,
                roughness, F0, effectiveLightColor, atten);
            additionalResult += pbrSpec * basePbrWeight;

            // Indirect specular (reflection probes)
            half3 indirectSpec = NatanePBRIndirectSpecular(worldNormal, viewDir, i.worldPos,
                roughness, F0);
            half pbrOcclusion = NATANE_SAMPLE_SHARED_R(_PBR_OcclusionMap, _PBR_MetallicGlossMap, uv);
            pbrOcclusion = lerp(1.0, pbrOcclusion, _PBR_OcclusionStrength);
            indirectSpec *= pbrOcclusion * _PBR_ReflectionIntensity * basePbrWeight * exposureOcclusion;
            additionalResult += indirectSpec;
        }
        #endif

        #if defined(_PBR_LIKE) && !defined(_PBR)
        {
            half metallic = saturate(_Metallic);
            half smoothness = saturate(_Smoothness);
            half roughness = max(0.04, 1.0 - smoothness);
            half3 F0 = lerp(half3(0.04, 0.04, 0.04), col.rgb, metallic);
            half skinSurface = step(0.5, _SurfaceModel) * (1.0 - step(1.5, _SurfaceModel));
            half skinPrimaryStrength = lerp(1.0, max(0.0, _SkinSpecPrimaryStrength), skinSurface);

            directResult *= (1.0 - metallic * 0.5 * basePbrWeight);

            half3 pbrLikeSpec = NatanePBRSpecular(worldNormal, viewDir, lightDir,
                roughness, F0, effectiveLightColor, atten);
            pbrLikeSpec *= _Glossiness * specularOcclusion * skinPrimaryStrength * basePbrWeight;
            pbrLikeSpec = ApplyMatteQuality(pbrLikeSpec, col.rgb, _MatteEffect);
            additionalResult += pbrLikeSpec;

            #ifdef UNITY_PASS_FORWARDBASE
                half pbrLikeReflectionIntensity = 1.0;
                #if defined(_REFLECTION)
                    pbrLikeReflectionIntensity = _ReflectionIntensity;
                #endif

                half3 pbrLikeIndirectSpec = NatanePBRIndirectSpecular(worldNormal, viewDir, i.worldPos,
                    roughness, F0);
                pbrLikeIndirectSpec *= pbrLikeReflectionIntensity * _Glossiness * specularOcclusion * skinPrimaryStrength * basePbrWeight * exposureOcclusion;
                pbrLikeIndirectSpec = ApplyMatteQuality(pbrLikeIndirectSpec, col.rgb, _MatteEffect);
                additionalResult += pbrLikeIndirectSpec;
            #endif

            if (skinSurface > 0.5 && _SkinSpecSecondaryStrength > 0.001 && basePbrWeight > 0.001)
            {
                half skinSpecMask = NATANE_SAMPLE_SHARED_R(_SkinSpecMask, _MainTex, uv);
                skinSpecMask = ApplySoftMask(skinSpecMask);
                half secondaryStrength = max(0.0, _SkinSpecSecondaryStrength * skinSpecMask * basePbrWeight);

                if (secondaryStrength > 0.001)
                {
                    half secondaryRoughness = max(0.02, 1.0 - saturate(_SkinSpecSecondarySmoothness));
                    half3 secondaryF0 = half3(0.04, 0.04, 0.04);
                    // Mirror-safe Fresnel for skin specular
                    float3 skinViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
                    skinViewNormal.x *= NataneMirrorSign();
                    half skinNdotV = saturate(dot(normalize(skinViewNormal), float3(0, 0, 1)));
                    half skinGrazing = pow(1.0 - skinNdotV, max(0.1, _SkinSpecFresnelPower));
                    half skinFresnelFactor = lerp(0.35, 1.0, saturate(skinGrazing));

                    half3 skinSecondarySpec = NatanePBRSpecular(worldNormal, viewDir, lightDir,
                        secondaryRoughness, secondaryF0, effectiveLightColor, atten);
                    skinSecondarySpec *= _SkinSpecSecondaryColor.rgb * _Glossiness * specularOcclusion
                        * secondaryStrength * skinFresnelFactor;
                    skinSecondarySpec = ApplyMatteQuality(skinSecondarySpec, col.rgb, _MatteEffect);
                    additionalResult += skinSecondarySpec;

                    #ifdef UNITY_PASS_FORWARDBASE
                        half skinSecondaryReflectionIntensity = 1.0;
                        #if defined(_REFLECTION)
                            skinSecondaryReflectionIntensity = _ReflectionIntensity;
                        #endif

                        half3 skinSecondaryIndirectSpec = NatanePBRIndirectSpecular(worldNormal, viewDir, i.worldPos,
                            secondaryRoughness, secondaryF0);
                        skinSecondaryIndirectSpec *= skinSecondaryReflectionIntensity * _SkinSpecSecondaryColor.rgb
                            * _Glossiness * specularOcclusion * secondaryStrength * skinFresnelFactor * exposureOcclusion;
                        skinSecondaryIndirectSpec = ApplyMatteQuality(skinSecondaryIndirectSpec, col.rgb, _MatteEffect);
                        additionalResult += skinSecondaryIndirectSpec;
                    #endif
                }
            }
        }
        #endif

        // ========== STEP 5: Final Composition ==========
        #ifdef _USE_LIGHT_VOLUME
            if (_LightVolumeBlendMode < 0.5) // Add (Legacy)
            {
                lighting = directResult + additionalResult;
                float3 lvAddition = saturate(directLightLV - lighting);
                lighting += lvAddition;
                // Indirect as subtle rim (mirror-safe)
                float3 lvRimViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
                lvRimViewNormal.x *= NataneMirrorSign();
                float rimFactor = 1.0 - saturate(dot(normalize(lvRimViewNormal), float3(0, 0, 1)));
                rimFactor = rimFactor * rimFactor * rimFactor;
                float3 indirectAddition = saturate(indirectLightLV - lighting);
                lighting += indirectAddition * rimFactor;
                lighting += indirectResult;
                lighting = CompressLightingForSafeRange(lighting, 0.95, 1.08);
            }
            else if (_LightVolumeBlendMode < 1.5) // Multiply
            {
                lighting = directResult + additionalResult;
                lighting *= lerp(float3(1, 1, 1), directLightLV, 1.0);
                lighting += indirectResult;
                lighting = CompressLightingForSafeRange(lighting, 0.95, 1.08);
            }
            else if (_LightVolumeBlendMode < 2.5) // Replace
            {
                lighting = lerp(directResult + additionalResult, directLightLV, 1.0);
                lighting += indirectResult;
                lighting = CompressLightingForSafeRange(lighting, 0.95, 1.08);
            }
            else // Natural (>= 2.5) — DEFAULT
            {
                // Treat Light Volume as environment fill first, then add only a modest direct contribution.
                half3 lvIndirectNatural = CompressLightingForSafeRange(indirectLightLV, 0.75, 0.95);
                half3 lvDirectNatural = CompressLightingForSafeRange(directLightLV, 0.8, 0.9) * 0.35;
                if (lvKeyAmount > 0.001)
                {
                    // Key light mode: the soft 35% irradiance fill is replaced by a toon
                    // term along the volume's dominant direction. When the volume already
                    // is the main light it went through the full shading path above, so
                    // nothing is added twice. With a realtime light it becomes a second,
                    // softer toon light carrying only the directional part (E(d) - E(-d)).
                    half lvKeyBorder = max(0.06, NataneShadowNaturalStepBorder());
                    half lvKeyShade = smoothstep(-lvKeyBorder, lvKeyBorder, dot(worldNormal, lvKeyDir));
                    half3 lvKeyDirectional = max(0.0, lvKeyColor - lvFillColor) * _LightVolumeIntensity;
                    // Half strength: it sits on top of a realtime light that already
                    // carries the scene, so it should model, not double the exposure.
                    half3 lvKeyToon = CompressLightingForSafeRange(lvKeyDirectional * lvKeyShade, 0.8, 0.9)
                                      * (0.5 * (1.0 - lvKeyIsMain) * (1.0 - shadowReceiveMask));
                    lvDirectNatural = lerp(lvDirectNatural, lvKeyToon, lvKeyAmount);
                }
                half3 totalIndirect = max(indirectResult, lvIndirectNatural);
                lighting = CombineDiffuseLightingSafe(totalIndirect, directResult + lvDirectNatural, additionalResult, half3(0, 0, 0));
            }

            // Light Volume Specular (additive on albedo)
            #ifdef _LIGHT_VOLUME_SPECULAR
                float3 lvSpecular = LightVolumeSpecular(col.rgb, _Smoothness, _Metallic,
                    worldNormal, viewDir, L0, L1r, L1g, L1b);
                lvSpecular *= _LightVolumeIntensity * _GIIntensity * specularOcclusion;
                lvSpecular *= _Glossiness;
                lvSpecular = ApplyMatteQuality(lvSpecular, col.rgb, _MatteEffect);
                float lvSpecStrength = saturate(length(lvSpecular) * 0.5);
                col.rgb = SafeAdditiveBlend(col.rgb, lvSpecular, lvSpecStrength);
            #endif

            lighting = lerp(preLightVolume, lighting, _LightVolumeBlend);
        #else
            // Non-LV: keep indirect visibility while compressing direct/additional blowout.
            lighting = CombineDiffuseLightingSafe(indirectResult, directResult, additionalResult, ambient);
        #endif

    #else
        // ===== ForwardAdd: Additional Light Contribution =====
        // ForwardAdd は各追加ライトの寄与のみを出力する（Blend One One で加算合成）。
        // 影色（shadowColor）は ForwardBase で既に処理済みのため、ここでは使用しない。
        // 影色をそのまま使うと、影側でも非ゼロの寄与が加算され、
        // 複数の色付きポイントライトで全身が混色する問題（紫ウォッシュ）が発生する。
        lighting = half3(shadingValue, shadingValue, shadingValue) * matteDiffuseTint;
        half lightColorLum_add = CALC_LUMINANCE(_LightColor0.rgb);
        half3 colorMul_add = lighting * _LightColor0.rgb;
        half3 lumOnly_add = lighting * lightColorLum_add;
        lighting = lerp(lumOnly_add, colorMul_add, _LightColorInfluence);
        lighting *= max(0.0, _LightIntensity);
        lighting *= max(0.0, _AdditionalLightIntensity);
        lighting = CompressLightingForSafeRange(lighting, 0.75, 0.9);
    #endif

    // Store original texture color before lighting application
    half3 originalAlbedo = col.rgb;

    #ifndef UNITY_PASS_FORWARDBASE
        // ForwardAdd pass should output only additional light contribution.
        // Keep this path minimal to avoid over-brightening and reduce per-light cost.
        col.rgb = originalAlbedo * lighting * _Brightness;

        // For transparent variants using Blend SrcAlpha One, the alpha channel
        // controls how much additional light is added. Without this, shadows
        // from additional lights appear opaque on transparent surfaces.
        #ifdef TRANSPARENT_VARIANT
            col.a *= atten;
        #endif

        // Backlight contribution in ForwardAdd
        {
            half backlightBlendFaded_add = _BacklightBlend;
            #ifdef _DISTANCE_FADE
                backlightBlendFaded_add *= lerp(1.0, distanceFade, _BacklightDistFade);
            #endif
            col.rgb += originalAlbedo * backlight * _BacklightColor.rgb * _LightColor0.rgb * atten * _AdditionalLightIntensity * backlightBlendFaded_add;
        }
        col.rgb = CompressLightingForSafeRange(col.rgb, 0.75, 0.9);
    #else
        // Optimized: Cache original luminance (used multiple times)
        half originalLum = CALC_LUMINANCE(originalAlbedo);

            // ===== Improved Color Preservation Lighting =====
            // Instead of directly multiplying, preserve color hue and saturation
            // while applying lighting brightness

            // Cache lighting luminance (already calculated as lightLum above - reuse if possible)
            half lightingLum = CALC_LUMINANCE(lighting);

            // Method 1: Preserve color by applying only luminance change
            // Extract color direction (hue/saturation) from original albedo
            half3 albedoDir = originalAlbedo / max(originalLum, 0.01);

            // Apply lighting luminance to color direction
            // This keeps the original color while adjusting brightness
            half3 preservedLitColor = albedoDir * originalLum * lightingLum;

            // Method 2: Traditional lighting (for blending)
            half3 traditionalLitColor = originalAlbedo * lighting;

            // Blend between preserved color and traditional lighting based on AlbedoPreservation
            // When AlbedoPreservation = 1.0, use fully preserved color (no white-washing)
            // When AlbedoPreservation = 0.0, use traditional lighting
            col.rgb = lerp(traditionalLitColor, preservedLitColor, _AlbedoPreservation);

            // Additional color preservation: prevent color shift in dark areas - Optimized: no branching
            // Dark colors (like black) should stay dark, not become gray
            half darkColorFactor = step(originalLum, 0.1) * step(HALF_VALUE, _AlbedoPreservation);
            half3 darkPreservedColor = min(col.rgb, originalAlbedo * (lightingLum * 1.2));
            col.rgb = lerp(col.rgb, darkPreservedColor, darkColorFactor);

            // 2. Saturation Adjustment - Optimized: removed branching
            // Enhance or reduce color saturation (lerp handles _Saturation=1.0 case efficiently)
            half gray = CALC_LUMINANCE(col.rgb);
            col.rgb = lerp(gray, col.rgb, _Saturation);

            // 3. Overall Brightness Adjustment
            // Final brightness control (applied before effects so they show properly)
            col.rgb *= _Brightness;
    #endif

    // LTCGI Specular: applied after lighting composition to avoid double-application
    // in ForwardAdd, and uses SafeAdditiveBlend + MatteQuality for overbright prevention.
    #if defined(_LTCGI) && defined(UNITY_PASS_FORWARDBASE)
    {
        half3 ltcgiSpecularContrib = ltcgiSpecular * _LTCGISpecular * _LTCGIIntensity;
        ltcgiSpecularContrib *= specularOcclusion;
        ltcgiSpecularContrib = ApplyMatteQuality(ltcgiSpecularContrib, col.rgb, _MatteEffect);
        half ltcgiSpecularStrength = saturate(length(ltcgiSpecularContrib) * 0.8);
        half3 preLTCGIColor = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, ltcgiSpecularContrib, ltcgiSpecularStrength);
        col.rgb = ApplyEffectBlendPost(preLTCGIColor, col.rgb, _LTCGIBlend, _LTCGIBlendMode);
    }
    #endif

    // ===== Halftone Shadow (ForwardBase only) =====
    #if defined(_HALFTONE_SHADOW) && defined(UNITY_PASS_FORWARDBASE)
    if (_HalftoneShadow >= 0.5)
    {
        // 濃度の基準。既定は量子化前の連続値。
        //
        // 量子化後の shadingValue を使うと、濃度が _ShadowSteps の段数しか取れない。
        // 既定の 2 段だと網点も 2 段階しか出ず、「トーンを貼り分けた」ではなく
        // 「白と黒が切り替わる」だけになる。漫画のトーンは面の丸みに沿って
        // 号数を選ぶものなので、基準は滑らかな陰影であるべき。
        // PBR モードを使っていなくても連続値は常に計算してある。
        // 濃度の基準。落ち影を含まない側を使う。
        half htSource = (_HalftoneShadowDensitySource > 0.5) ? continuousShadingLit : shadingValue;

        // shadowArea: 0=lit, 1=shadow
        //
        // 陰影（N·L）由来のトーンと、落ち影由来のトーンを別々に求めて max で合成する。
        // 掛け算で 1 本にまとめると、落ち影が無い（atten = 1）場所でも
        // 陰影の分だけトーンが出て、「影が落ちていないのに網点が乗る」状態になる。
        // 落ち影は atten < 1 の領域にしか存在しないので、そこだけに効かせる。
        float shadowAreaLit = smoothstep(_HalftoneShadowThreshold + _HalftoneShadowSoftness,
                                          _HalftoneShadowThreshold - _HalftoneShadowSoftness,
                                          htSource);

        float shadowArea = shadowAreaLit;

        // 量子化後を基準にしている場合は shadingValue に既に atten が畳み込まれているので、
        // 二重に効かせない。連続基準のときだけ落ち影を別枠で足す。
        if (_HalftoneShadowDensitySource > 0.5)
        {
            float castArea = saturate(1.0 - atten) * _HalftoneShadowCastShadow;
            shadowArea = max(shadowArea, castArea);
        }

        // 影の濃さを「トーンの号数」に量子化する。連続的に太らせるより漫画らしい。
        float htTone = NataneHalftoneQuantizeTone(shadowArea, _HalftoneShadowLevels);

        // 網点グリッドの座標。スクリーン空間だとカメラを動かしたとき模様が滑るため、
        // 面に貼り付けたい場合のためにワールド/UV/オブジェクトも選べる。
        // ワールドは Triplanar-lite なので垂直面でも潰れない。
        float3 htObjPos = mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
        float2 htPos = NataneHalftoneCoord(
            _HalftoneShadowSpace, _HalftoneShadowScale, _HalftoneShadowSurfaceDensity,
            _HalftoneShadowScreenLock, _HalftoneShadowScreenAnchor,
            uv, htObjPos, i.worldPos, worldNormal, i.pos.xy);

        htPos = NataneHalftoneRotate(htPos, _HalftoneShadowAngle);

        float htInk = NataneHalftonePattern(
            htPos, htTone, _HalftoneShadowPattern,
            _HalftoneShadowDotMin, _HalftoneShadowDotMax, _HalftoneShadowAA);

        // tone が 0 の領域には一切載せない（量子化の最下段でも点が残らないように）。
        //
        // ここは step だった。量子化の最下段は tone = 0.5/levels なので、
        // 影に入った瞬間その大きさの網点が一斉に出現し、明部との境が硬い線として見えていた。
        // 網点自体が消える幅（1/levels の半分）で立ち上げると、
        // 「トーンの縁」として自然に見える。levels=1（連続）のときも同じ式で成立する。
        half htFadeIn = 0.5 / max(_HalftoneShadowLevels, 1.0);
        htInk *= smoothstep(0.0, htFadeIn, shadowArea);

        col.rgb = lerp(col.rgb, _HalftoneShadowColor.rgb * col.rgb,
                       htInk * _HalftoneShadowIntensity * _HalftoneShadowBlend);
    }
    #endif

    // ================================================================
    // ===== STAGE A: Illustration Style — Color Transform =====
    // ================================================================

    #if defined(_COLOR_QUANTIZE) && defined(UNITY_PASS_FORWARDBASE)
    if (_UseColorQuantize >= 0.5)
    {
        half qMask = NATANE_SAMPLE_SHARED_R(_QuantizeMask, _MainTex, uv);
        half3 quantized;
        if (_QuantizeMode > 0.5)
        {
            // HSV quantization
            quantized = QuantizeColorHSV(col.rgb, _QuantizeHueLevels, _QuantizeSatLevels,
                _QuantizeValLevels, _QuantizeDither, StabilizeDitherCoord(i.pos.xy));
        }
        else
        {
            // RGB quantization
            quantized = QuantizeColorRGB(col.rgb, _QuantizeLevels, _QuantizeDither, StabilizeDitherCoord(i.pos.xy));
        }
        col.rgb = lerp(col.rgb, quantized, _QuantizeBlend * qMask * nprWeight);
    }
    #endif

    #if defined(_LUT_3D) && defined(UNITY_PASS_FORWARDBASE)
    if (_UseLUT3D >= 0.5)
    {
        half3 lutColor = ApplyLUT3D(saturate(col.rgb), _LUT3DTex, _LUT3DSize);
        col.rgb = lerp(col.rgb, lutColor, _LUT3DIntensity * nprWeight);
    }
    #endif

    #if defined(_HATCHING) && defined(UNITY_PASS_FORWARDBASE)
    if (_UseHatching >= 0.5)
    {
        float2 hatchUV = uv;
        #if defined(_LINE_BOIL)
        if (_LineBoilAffectHatching >= 0.5)
            hatchUV += nataneBoilUV;
        #endif
        half hMask = NATANE_SAMPLE_SHARED_R(_HatchingMask, _MainTex, hatchUV);
        // 階調はライティング（shadingValue: 0=影 / 1=明部）で決める。
        // 以前はここまでの色の輝度を使っていたため、暗いアルベド（黒い服・髪など）が
        // 光の当たった場所でも「影」と判定され、常に最も濃い線が乗っていた。
        col.rgb = ApplyHatching(col.rgb, hatchUV, shadingValue, hMask,
            _HatchTex0, _HatchTex1, _HatchingTiling, _HatchingColor, _HatchingBlend * nprWeight,
            (int)(_HatchingComposite + 0.5));
    }
    #endif

    // ===== Post-Lighting Effects =====
    // このセクションの処理:
    // ライティング適用後に各種視覚エフェクトを順番に加算合成する。
    // Specular → Hair Specular → SSS → Rim Light (1/2) → Offset Rim → Env Rim
    // → MatCap (1/2/3) → Reflection → Refraction → Emission → Hue Shift
    // → AudioLink → Glitter → Iridescence → Drip → Hologram → Glitch → Decal → Dissolve
    // 各エフェクトは SafeAdditiveBlend で白飛びを防ぎ、距離フェードにも対応する。

    #if defined(UNITY_PASS_FORWARDBASE) && (defined(_MATCAP) || defined(_MATCAP_2) || defined(_MATCAP_3) || defined(_PROCEDURAL_MATCAP))
        float2 sharedMatCapUV = CalculateMatCapUV(worldNormal, viewDir);
    #endif

    // ===== Specular Highlight =====
    #ifdef _SPECULAR
    if (_Specular >= 0.5)
    {
        float specSoftnessBlurred = _SpecularSoftness + _SpecularBlur * 0.3;
        half spec = SpecularHighlight(worldNormal, viewDir, lightDir, _SpecularSize, specSoftnessBlurred);

        // Boundary Dithering: apply dither pattern at specular highlight edges
        #if defined(_SPECULAR_DITHER)
        {
            // Pixel coordinates straight from SV_POSITION: identical in mono, and not
            // stretched 2x in double-wide stereo like "screenUV * _ScreenParams.xy" was.
            float2 specDitherScreenPos = i.pos.xy;
            float specDitherThreshold = NataneGetDitherThreshold(StabilizeDitherCoord(specDitherScreenPos), _SpecularDitherScale);
            float ditheredSpec = step(specDitherThreshold, spec);
            spec = lerp(spec, ditheredSpec, _SpecularDitherStrength);
        }
        #endif

        half specIntensityMod = _SpecularIntensity;
        #if defined(_FX_MODULATOR)
            specIntensityMod *= NataneFXModMul(nataneFXState, NATANE_FXT_SPECULAR);
        #endif
        half3 specContrib = spec * _SpecularColor.rgb * specIntensityMod * effectiveLightColor * atten;

        // Apply mask texture with soft blending
        float2 specMaskUV = AnimateUVIfNeeded(uv, _SpecularMaskScrollSpeed.xy, _SpecularMaskRotateSpeed);
        half specMask = NATANE_SAMPLE_SHARED_R(_SpecularMask, _MainTex, specMaskUV);
        specMask = ApplySoftMask(specMask); // Smooth mask transitions
        specContrib *= specMask;

        // Apply additional light intensity scaling in ForwardAdd pass
        #ifndef UNITY_PASS_FORWARDBASE
            specContrib *= _AdditionalLightIntensity;
        #endif

        // Fade the highlight inside the shade (0 = unchanged). Cast shadows are already
        // handled by atten above; this follows the toon shading itself.
        specContrib *= lerp(1.0, shadingValue, saturate(_SpecularShadowMask));

        // Apply glossiness and matte material quality
        specContrib *= _Glossiness * specularOcclusion;
        specContrib = ApplyMatteQuality(specContrib, col.rgb, _MatteEffect);

        // Use safe additive blending to prevent white-out
        half3 preSpec = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, specContrib, saturate(length(specContrib) * 0.8));
        half specBlendFaded = _SpecularBlend;
        #ifdef _DISTANCE_FADE
            specBlendFaded *= lerp(1.0, distanceFade, _SpecularDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preSpec, col.rgb, specBlendFaded, _SpecularBlendMode);
    } // if (_Specular >= 0.5)
    #endif

    // ===== Shaped Toon Highlight (procedural SDF specular) =====
    #ifdef _SHAPED_HIGHLIGHT
    if (_ShapedHighlight >= 0.5)
    {
        // Peak-centered 2D coordinate: deviation of the half-vector from the
        // surface normal, projected into the view plane (mirror-safe).
        half3 shpH = normalize(lightDir + viewDir);
        float3 shpVN = mul((float3x3)UNITY_MATRIX_V, worldNormal);
        float3 shpVH = mul((float3x3)UNITY_MATRIX_V, shpH);
        float3 shpVL = mul((float3x3)UNITY_MATRIX_V, lightDir);
        shpVN.x *= NataneMirrorSign();
        shpVH.x *= NataneMirrorSign();
        shpVL.x *= NataneMirrorSign();
        float2 shpCoord = (shpVH.xy - shpVN.xy);

        // Orientation: base rotation + light-follow + camera-follow terms.
        float shpAng = radians(_ShapedHLRotation);
        shpAng += _ShapedHLLightFollow * atan2(shpVL.y, shpVL.x);
        shpAng += _ShapedHLCameraFollow * atan2(UNITY_MATRIX_V[1].x, UNITY_MATRIX_V[0].x);
        float shpS = sin(shpAng);
        float shpC = cos(shpAng);
        shpCoord = float2(shpCoord.x * shpC - shpCoord.y * shpS,
                          shpCoord.x * shpS + shpCoord.y * shpC);

        // Scale by size and per-axis stretch.
        shpCoord /= max(_ShapedHLSize, 0.001);
        shpCoord /= max(_ShapedHLStretch.xy, float2(0.001, 0.001));

        // Sparkle pulse (breathing intensity).
        half shpPulse = 1.0;
        if (_ShapedHLSparkleSpeed > 0.001)
            shpPulse = 0.5 + 0.5 * sin(_Time.y * _ShapedHLSparkleSpeed);

        // Primary coverage: procedural SDF or custom SDF texture (shape 8).
        half shpCov;
        if (_ShapedHLShape > 7.5)
        {
            float2 shpTexUV = shpCoord * 0.5 + 0.5;
            shpCov = NATANE_SAMPLE_SHARED_R(_ShapedHLTex, _MainTex, saturate(shpTexUV));
        }
        else
        {
            half shpSDF = NataneShapeSDF(shpCoord, _ShapedHLShape);
            shpCov = NataneShapeCoverage(shpSDF, _ShapedHLSoftness);
        }

        // Optional secondary procedural shape (additive coverage).
        if (_ShapedHLIntensity2 > 0.001)
        {
            float2 shpCoord2 = shpCoord * (_ShapedHLSize / max(_ShapedHLSize2, 0.001));
            half shpSDF2 = NataneShapeSDF(shpCoord2, _ShapedHLShape2);
            half shpCov2 = NataneShapeCoverage(shpSDF2, _ShapedHLSoftness)
                           * (_ShapedHLIntensity2 / max(_ShapedHLIntensity, 0.0001));
            shpCov = max(shpCov, shpCov2);
        }

        half shpMask = NATANE_SAMPLE_SHARED_R(_ShapedHLMask, _MainTex, uv);
        shpMask = ApplySoftMask(shpMask);

        half shpIntensityMod = _ShapedHLIntensity;
        #if defined(_FX_MODULATOR)
            shpIntensityMod *= NataneFXModMul(nataneFXState, NATANE_FXT_SHAPED_HL);
        #endif
        half3 shaped = shpCov * shpPulse * _ShapedHLColor.rgb * shpIntensityMod
                       * effectiveLightColor * atten * shpMask;

        #ifndef UNITY_PASS_FORWARDBASE
            shaped *= _AdditionalLightIntensity;
        #endif
        shaped *= _Glossiness * specularOcclusion;
        shaped = ApplyMatteQuality(shaped, col.rgb, _MatteEffect);

        half3 preShaped = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, shaped, saturate(length(shaped) * 0.8));
        #ifdef _DISTANCE_FADE
            col.rgb = lerp(preShaped, col.rgb, distanceFade);
        #endif
    } // if (_ShapedHighlight >= 0.5)
    #endif

    // ===== Hair Specular (Kajiya-Kay) =====
    #ifdef _HAIR_SPECULAR
    if (_HairSpecular >= 0.5)
    {
        half3 hairSpec = HairSpecularHighlight(worldNormal, i.worldTangent, i.worldBinormal,
                                                viewDir, lightDir, uv);
        half3 hairTransmission = HairTransmissionHighlight(worldNormal, i.worldTangent, i.worldBinormal,
                                                           viewDir, lightDir, uv);

        // Boundary Dithering: apply dither pattern at hair specular highlight edges
        #if defined(_SPECULAR_DITHER)
        {
            // Pixel coordinates straight from SV_POSITION: identical in mono, and not
            // stretched 2x in double-wide stereo like "screenUV * _ScreenParams.xy" was.
            float2 hairDitherScreenPos = i.pos.xy;
            float hairDitherThreshold = NataneGetDitherThreshold(StabilizeDitherCoord(hairDitherScreenPos), _SpecularDitherScale);
            float hairSpecIntensity = max(max(hairSpec.r, hairSpec.g), hairSpec.b);
            float hairDitherMask = lerp(1.0, step(hairDitherThreshold, hairSpecIntensity), _SpecularDitherStrength);
            hairSpec *= hairDitherMask;
        }
        #endif

        hairSpec *= effectiveLightColor * atten;
        hairTransmission *= effectiveLightColor * atten;

        // Absorption tint: light that crossed the fibres takes the hair's own hue.
        if (_HairTransmissionAbsorption > 0.001)
        {
            half3 hairHue = saturate(originalAlbedo) / max(CALC_LUMINANCE(saturate(originalAlbedo)), 0.05);
            hairTransmission *= lerp(half3(1.0, 1.0, 1.0), min(hairHue, 2.0), saturate(_HairTransmissionAbsorption));
        }
        // Fake multiple scattering (Karis, "Physically Based Hair Shading in Unreal", 2016):
        // a wrapped diffuse around the strand whose tint saturates inside the shadow.
        if (_HairMultiScatter > 0.001)
        {
            half3 msAlbedo = max(saturate(originalAlbedo), 0.02);
            half3 msStrand = normalize(i.worldBinormal);
            half msKajiya = 1.0 - abs(dot(msStrand, lightDir));
            half3 msFakeN = normalize(viewDir - msStrand * dot(viewDir, msStrand) + float3(0, 1e-4, 0));
            half msNoL = saturate((dot(msFakeN, lightDir) + 1.0) / 4.0);
            half msScatter = lerp(msNoL, msKajiya, 0.33) / UNITY_PI;
            half3 msTint = pow(msAlbedo / max(CALC_LUMINANCE(msAlbedo), 0.02), 1.0 - atten);
            hairTransmission += sqrt(msAlbedo) * msTint * effectiveLightColor * (msScatter * _HairMultiScatter);
        }

        // Rim transmission (the scatter term of Unity HDRP's Kajiya-Kay hair): light from
        // behind glows through the silhouette of the hair. Uses the geometric normal so a
        // normal map cannot break up the halo. In ForwardBase the cast shadow only half
        // applies: back-lit hair is always inside its own shadow, which would erase it.
        if (_HairRimTransmission > 0.001)
        {
            half hairRimNdotV = saturate(dot(normalize(i.worldNormal), viewDir));
            half hairRim = pow(saturate(-dot(lightDir, viewDir)), 9.0)
                         * pow(saturate(1.0 - hairRimNdotV * hairRimNdotV), 12.0);
            #ifdef UNITY_PASS_FORWARDBASE
                half hairRimAtten = lerp(1.0, atten, 0.5);
            #else
                half hairRimAtten = atten;
            #endif
            hairTransmission += _HairTransmissionColor.rgb * effectiveLightColor * (hairRim * _HairRimTransmission * hairRimAtten);
        }

        // Apply additional light intensity scaling in ForwardAdd pass
        #ifndef UNITY_PASS_FORWARDBASE
            hairSpec *= _AdditionalLightIntensity;
            hairTransmission *= _AdditionalLightIntensity;
        #endif

        // Apply glossiness and matte material quality
        hairSpec *= _Glossiness * specularOcclusion;
        hairTransmission *= specularOcclusion;
        hairSpec += hairTransmission;
        hairSpec = ApplyMatteQuality(hairSpec, col.rgb, _MatteEffect);

        // Use safe additive blending to prevent white-out
        half hairSpecStrength = saturate(length(hairSpec) * 0.8);
        half3 preHairSpec = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, hairSpec, hairSpecStrength);
        half hairSpecBlendFaded = _HairSpecBlend;
        #ifdef _DISTANCE_FADE
            hairSpecBlendFaded *= lerp(1.0, distanceFade, _HairSpecDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preHairSpec, col.rgb, hairSpecBlendFaded, _HairSpecBlendMode);
    } // if (_HairSpecular >= 0.5)
    #endif

    // ===== Angel Ring (天使の輪, ForwardBase only) =====
    #if defined(_ANGEL_RING) && defined(UNITY_PASS_FORWARDBASE)
    if (_AngelRing >= 0.5)
    {
        // MatCapベースUV: ビュー空間法線のY成分でリング位置を決定
        // X軸はミラーで反転するため NataneMirrorSign() で補正
        float2 angelUV = float2(
            dot(normalize(UNITY_MATRIX_V[0].xyz), worldNormal) * NataneMirrorSign() * 0.5 + 0.5,
            dot(normalize(UNITY_MATRIX_V[1].xyz), worldNormal) * 0.5 + 0.5
        );
        // Y方向にオフセット（リングの位置調整）
        angelUV.y += _AngelRingOffset;
        // リングパターン: Y座標のガウシアンで幅を制御
        float ringFactor = exp(-pow((angelUV.y - 0.5) / max(_AngelRingWidth, 0.01), 2.0));
        // テクスチャサンプリング
        half4 angelTex = NATANE_SAMPLE_CLAMP(_AngelRingTex, TRANSFORM_TEX(angelUV, _AngelRingTex));
        half3 angelColor = angelTex.rgb * _AngelRingColor.rgb * ringFactor * _AngelRingIntensity;
        // ブレンド
        half3 preAngel = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, angelColor, saturate(ringFactor * angelTex.a * _AngelRingColor.a));
        half angelBlendFaded = _AngelRingBlend;
        #ifdef _DISTANCE_FADE
            angelBlendFaded *= distanceFade;
        #endif
        col.rgb = ApplyEffectBlendPost(preAngel, col.rgb, angelBlendFaded, _AngelRingBlendMode);
    }
    #endif

    // ===== Subsurface Scattering =====
    #if defined(_SSS) && defined(UNITY_PASS_FORWARDBASE)
    if (_SSS >= 0.5)
    {
        half thickness = NATANE_SAMPLE_SHARED_R(_ThicknessMap, _MainTex, uv) * _ThicknessScale;
        half transmissionMask = NATANE_SAMPLE_SHARED_R(_TransmissionMask, _MainTex, uv);
        transmissionMask = ApplySoftMask(transmissionMask);
        half transmissionStrength = lerp(1.0, transmissionMask, _TransmissionStrength);

        #if defined(_SSS_LUT)
            half3 sss = SubsurfaceScatteringLUT(ndotl, worldNormal, i.worldPos, thickness);
        #else
            float sssPowerBlurred = max(0.1, _SSSPower * (1.0 - _SSSBlur * 0.8));
            // With a directional light this is _LightColor0 exactly as before; without one
            // it follows the fallback light the direction was taken from.
            half3 sssLightColor = (CALC_LUMINANCE(_LightColor0.rgb) > 0.01) ? _LightColor0.rgb : effectiveLightColor;
            half3 sss = SubsurfaceScattering(worldNormal, lightDir, viewDir, thickness, atten, sssPowerBlurred, sssLightColor);
        #endif

        // Apply mask texture with soft blending
        half sssMask = NATANE_SAMPLE_SHARED_R(_SSSMask, _MainTex, uv);
        sssMask = ApplySoftMask(sssMask); // Smooth mask transitions
        sss *= sssMask * transmissionStrength;
        // Thickness-driven skin profile: thin parts stay white-ish, thick parts go red.
        if (_TransmissionSkinProfile > 0.001)
        {
            sss *= lerp(half3(1.0, 1.0, 1.0), NataneSkinTransmittance(thickness * 10.0) * 2.0, saturate(_TransmissionSkinProfile));
        }

        // Use safe additive blending to prevent white-out
        half sssStrength = saturate(length(sss) * 0.5);
        half3 preSSS = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, sss, sssStrength);
        half sssBlendFaded = _SSSBlend;
        #ifdef _DISTANCE_FADE
            sssBlendFaded *= lerp(1.0, distanceFade, _SSSDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preSSS, col.rgb, sssBlendFaded, _SSSBlendMode);
    } // if (_SSS >= 0.5)
    #endif

    // ===== Rim Light Direction (pre-calculate for both Rim Light 1 & 2) =====
    #if defined(UNITY_PASS_FORWARDBASE) && (defined(_RIM_LIGHT) || defined(_RIM_LIGHT_2))
        half3 rimDirNormalized = normalize(_RimLightDirection.xyz + float3(0, 0, 0.0001));
    #endif

    // ===== Rim Light =====
    #if defined(_RIM_LIGHT) && defined(UNITY_PASS_FORWARDBASE)
    if (_RimLight >= 0.5)
        {
            float rimPowerBlurred = max(0.1, _RimPower * (1.0 - _RimBlur * 0.8));
            half rimSpreadPower = lerp(rimPowerBlurred, max(0.5, rimPowerBlurred * 0.3), _RimSpread);

            // Fresnel rim (same for both passes: camera-based edge detection)
            half3 rim = RimLighting(worldNormal, viewDir, rimPowerBlurred, _RimIntensity);
            half3 rimGlow = RimLighting(worldNormal, viewDir, rimSpreadPower, _RimIntensity * _RimSpread * 0.5);
            rim += rimGlow * step(0.001, _RimSpread);

            // Apply mask texture with soft blending
            float2 rimMaskUV = AnimateUVIfNeeded(uv, _RimMaskScrollSpeed.xy, _RimMaskRotateSpeed);
            half rimMask = NATANE_SAMPLE_SHARED_R(_RimMask, _MainTex, rimMaskUV);
            rimMask = ApplySoftMask(rimMask); // Smooth mask transitions
            rim *= rimMask;

            if (_RimDirectionRange > 0.001)
            {
                half rimDirectionMask1 = smoothstep(-_RimDirectionRange, _RimDirectionRange, dot(worldNormal, rimDirNormalized));
                rim *= rimDirectionMask1;
            }

            // Apply glossiness and matte material quality
            rim *= _Glossiness;
            rim = ApplyMatteQuality(rim, col.rgb, _MatteEffect);

            // Light direction-linked rim masking (lilToon-style Half-Lambert)
            // Half-Lambert maps NdotL from [-1,1] to [0,1] — rim follows actual light direction
            {
                half rimHalfLambert = dot(worldNormal, lightDir) * 0.5 + 0.5;
                rim *= lerp(1.0, rimHalfLambert, _RimDirStrength);
            }
            // Shadow-based rim suppression (independent of direction)
            rim *= lerp(1.0, shadingValue, _RimShadowMask);

            // FX Modulator: RimIntensity target
            #if defined(_FX_MODULATOR)
                rim *= NataneFXModMul(nataneFXState, NATANE_FXT_RIM);
            #endif

            // Use safe additive blending to prevent white-out
            half rimStrength = saturate(length(rim) * 0.5);
            half3 preRim = col.rgb;
            col.rgb = SafeAdditiveBlend(col.rgb, rim, rimStrength);
            half rimBlendFaded = _RimBlend;
            #ifdef _DISTANCE_FADE
                rimBlendFaded *= lerp(1.0, distanceFade, _RimDistFade);
            #endif
            col.rgb = ApplyEffectBlendPost(preRim, col.rgb, rimBlendFaded, _RimBlendMode);
        }
    #endif

    // ===== Rim Light 2 =====
    #if defined(_RIM_LIGHT_2) && defined(UNITY_PASS_FORWARDBASE)
    if (_RimLight2 >= 0.5)
    {
        float rim2PowerBlurred = max(0.1, _RimPower2 * (1.0 - _Rim2Blur * 0.8));
        half rim2SpreadPower = lerp(rim2PowerBlurred, max(0.5, rim2PowerBlurred * 0.3), _RimSpread2);

        // Fresnel rim — mirror-safe via view-space dot product
        float3 rim2ViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
        rim2ViewNormal.x *= NataneMirrorSign();
        half rim2NdotV = saturate(dot(normalize(rim2ViewNormal), float3(0, 0, 1)));
        half rim2Factor = 1.0 - rim2NdotV;
        rim2Factor = pow(rim2Factor, rim2PowerBlurred) * _RimIntensity2;
        half3 rim2 = rim2Factor * _RimColor2.rgb;
        half rim2SpreadFactor = 1.0 - rim2NdotV;
        rim2SpreadFactor = pow(rim2SpreadFactor, rim2SpreadPower) * _RimIntensity2 * _RimSpread2 * 0.5;
        rim2 += rim2SpreadFactor * _RimColor2.rgb * step(0.001, _RimSpread2);

        // Apply mask texture with soft blending
        float2 rimMask2UV = AnimateUVIfNeeded(uv, _RimMask2ScrollSpeed.xy, _RimMask2RotateSpeed);
        half rimMask2 = NATANE_SAMPLE_SHARED_R(_RimMask2, _MainTex, rimMask2UV);
        rimMask2 = ApplySoftMask(rimMask2); // Smooth mask transitions
        rim2 *= rimMask2;

        if (_RimDirectionRange > 0.001)
        {
            half rimDirectionMask2 = smoothstep(-_RimDirectionRange, _RimDirectionRange, dot(worldNormal, rimDirNormalized));
            rim2 *= rimDirectionMask2;
        }

        // Apply glossiness and matte material quality
        rim2 *= _Glossiness;
        rim2 = ApplyMatteQuality(rim2, col.rgb, _MatteEffect);

        // Light direction-linked rim masking (lilToon-style Half-Lambert)
        {
            half rim2HalfLambert = dot(worldNormal, lightDir) * 0.5 + 0.5;
            rim2 *= lerp(1.0, rim2HalfLambert, _RimDirStrength);
        }
        // Shadow-based rim suppression
        rim2 *= lerp(1.0, shadingValue, _RimShadowMask);

        // FX Modulator: RimIntensity target
        #if defined(_FX_MODULATOR)
            rim2 *= NataneFXModMul(nataneFXState, NATANE_FXT_RIM);
        #endif

        // Use fast additive blending (secondary effect)
        half rim2Strength = saturate(length(rim2) * 0.5);
        half3 preRim2 = col.rgb;
        col.rgb = SafeAdditiveBlendFast(col.rgb, rim2, rim2Strength);
        half rim2BlendFaded = _RimBlend2;
        #ifdef _DISTANCE_FADE
            rim2BlendFaded *= lerp(1.0, distanceFade, _Rim2DistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preRim2, col.rgb, rim2BlendFaded, _RimBlendMode2);
    } // if (_RimLight2 >= 0.5)
    #endif

    // ===== Offset Rim Light =====
    #if defined(_OFFSET_RIM_LIGHT) && defined(UNITY_PASS_FORWARDBASE)
    if (_OffsetRimLight >= 0.5)
    {
        float offsetRimPowerBlurred = max(0.1, _OffsetRimPower * (1.0 - _OffsetRimBlur * 0.8));

        // Offset rim uses lightDir for light direction linking
        half3 offsetRim = OffsetRimLighting(worldNormal, viewDir, lightDir, offsetRimPowerBlurred, _OffsetRimIntensity);

        // Apply mask texture
        half offsetRimMask = NATANE_SAMPLE_SHARED_R(_OffsetRimMask, _MainTex, uv);
        offsetRimMask = ApplySoftMask(offsetRimMask);
        offsetRim *= offsetRimMask;

        // Apply shadow mask (suppress rim in shadowed areas)
        offsetRim *= lerp(1.0, shadingValue, _OffsetRimShadowMask);

        // Apply glossiness and matte material quality
        offsetRim *= _Glossiness;
        offsetRim = ApplyMatteQuality(offsetRim, col.rgb, _MatteEffect);

        // Safe additive blend
        half offsetRimStrength = saturate(length(offsetRim) * 0.5);
        half3 preOffsetRim = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, offsetRim, offsetRimStrength);
        half offsetRimBlendFaded = _OffsetRimBlend;
        #ifdef _DISTANCE_FADE
            offsetRimBlendFaded *= lerp(1.0, distanceFade, _OffsetRimDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preOffsetRim, col.rgb, offsetRimBlendFaded, _OffsetRimBlendMode);
    } // if (_OffsetRimLight >= 0.5)
    #endif

    // ===== Sheen (Fabric Luster, ForwardBase only) =====
    #if defined(_SHEEN) && defined(UNITY_PASS_FORWARDBASE)
    if (_Sheen >= 0.5)
    {
        half3 sheen = SheenHighlight(worldNormal, viewDir, lightDir);
        half sheenMask = NATANE_SAMPLE_CLAMP(_SheenMask, TRANSFORM_TEX(uv, _SheenMask)).r;
        sheen *= sheenMask;
        sheen = ApplyMatteQuality(sheen, col.rgb, _MatteEffect);
        half3 preSheen = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, sheen, saturate(length(sheen) * 0.5));
        half sheenBlendFaded = _SheenBlend;
        #ifdef _DISTANCE_FADE
            sheenBlendFaded *= distanceFade;
        #endif
        col.rgb = ApplyEffectBlendPost(preSheen, col.rgb, sheenBlendFaded, _SheenBlendMode);
    }
    #endif

    // ===== Environmental Rim =====
    #if defined(_ENV_RIM) && defined(UNITY_PASS_FORWARDBASE)
    if (_EnvRim >= 0.5)
    {
        float envRimPowerBlurred = max(0.1, _EnvRimPower * (1.0 - _EnvRimBlur * 0.8));

        // Fresnel + cubemap rim (same for both passes)
        half3 envRim = EnvironmentalRim(worldNormal, viewDir, envRimPowerBlurred);

        // Apply mask texture with soft blending
        half envRimMask = NATANE_SAMPLE_SHARED_R(_EnvRimMask, _MainTex, uv);
        envRimMask = ApplySoftMask(envRimMask); // Smooth mask transitions
        envRim *= envRimMask;

        // Apply glossiness and matte material quality
        envRim *= _Glossiness;
        envRim = ApplyMatteQuality(envRim, col.rgb, _MatteEffect);

        // Light direction-linked rim masking (lilToon-style Half-Lambert)
        {
            half envRimHalfLambert = dot(worldNormal, lightDir) * 0.5 + 0.5;
            envRim *= lerp(1.0, envRimHalfLambert, _RimDirStrength);
        }
        // Shadow-based rim suppression
        envRim *= lerp(1.0, shadingValue, _RimShadowMask);

        // Use safe additive blending to prevent white-out
        half envRimStrength = saturate(length(envRim) * 0.5);
        half3 preEnvRim = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, envRim, envRimStrength);
        half envRimBlendFaded = _EnvRimBlend;
        #ifdef _DISTANCE_FADE
            envRimBlendFaded *= lerp(1.0, distanceFade, _EnvRimDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preEnvRim, col.rgb, envRimBlendFaded, _EnvRimBlendMode);
    } // if (_EnvRim >= 0.5)
    #endif

    // ===== MatCap (ForwardBase only) =====
    #if defined(_MATCAP) && defined(UNITY_PASS_FORWARDBASE)
    if (_MatCap >= 0.5)
    {
        half matCapIntMod = _MatCapIntensity;
        #if defined(_FX_MODULATOR)
            matCapIntMod *= NataneFXModMul(nataneFXState, NATANE_FXT_MATCAP);
        #endif
        half3 matcap = SampleTex2DBlur3Repeat(_MatCapTex, sharedMatCapUV, _MatCapBlur) * matCapIntMod;

        // Apply mask texture with soft blending
        half matcapMask = NATANE_SAMPLE_SHARED_R(_MatCapMask, _MatCapTex, uv);
        matcapMask = ApplySoftMask(matcapMask); // Smooth mask transitions
        matcap *= matcapMask;

        // Apply glossiness and matte material quality
        matcap *= _Glossiness;
        matcap = ApplyMatteQuality(matcap, col.rgb, _MatteEffect);

        // Blend modes: 0=Add (safe), 1=Multiply, 2=Replace - Optimized: no branching
        half3 preMatCap = col.rgb;
        half matcapStrength = saturate(matCapIntMod * matcapMask);
        half3 addResult = SafeAdditiveBlend(col.rgb, matcap, matcapStrength);
        half3 multiplyResult = BlendWithSoftMask(col.rgb, col.rgb * matcap, saturate(matCapIntMod * matcapMask));
        half3 replaceResult = BlendWithSoftMask(col.rgb, matcap, saturate(matCapIntMod * matcapMask));

        // Select blend mode using lerp
        half isMultiply = step(HALF_VALUE, _MatCapBlendMode) * step(_MatCapBlendMode, 1.5);
        half isReplace = step(1.5, _MatCapBlendMode);
        col.rgb = lerp(addResult, multiplyResult, isMultiply);
        col.rgb = lerp(col.rgb, replaceResult, isReplace);
        half matCapBlendFaded = _MatCapBlend;
        #ifdef _DISTANCE_FADE
            matCapBlendFaded *= lerp(1.0, distanceFade, _MatCapDistFade);
        #endif
        col.rgb = lerp(preMatCap, col.rgb, matCapBlendFaded);
    } // if (_MatCap >= 0.5)
    #endif

    // ===== MatCap 2 (ForwardBase only) =====
    #ifndef _QUEST_LITE
    #if defined(_MATCAP_2) && defined(UNITY_PASS_FORWARDBASE)
    if (_MatCap2 >= 0.5)
    {
        half3 matcap2 = SampleTex2DBlur3Repeat(_MatCapTex2, sharedMatCapUV, _MatCap2Blur) * _MatCapIntensity2;

        half matcapMask2 = NATANE_SAMPLE_SHARED_R(_MatCapMask2, _MatCapTex2, uv);
        matcapMask2 = ApplySoftMask(matcapMask2);
        matcap2 *= matcapMask2;
        matcap2 *= _Glossiness;
        matcap2 = ApplyMatteQuality(matcap2, col.rgb, _MatteEffect);

        half3 preMatCap2 = col.rgb;
        half matcapStrength2 = saturate(_MatCapIntensity2 * matcapMask2);
        half3 addResult2 = SafeAdditiveBlend(col.rgb, matcap2, matcapStrength2);
        half3 multiplyResult2 = BlendWithSoftMask(col.rgb, col.rgb * matcap2, saturate(_MatCapIntensity2 * matcapMask2));
        half3 replaceResult2 = BlendWithSoftMask(col.rgb, matcap2, saturate(_MatCapIntensity2 * matcapMask2));

        half isMultiply2 = step(HALF_VALUE, _MatCapBlendMode2) * step(_MatCapBlendMode2, 1.5);
        half isReplace2 = step(1.5, _MatCapBlendMode2);
        col.rgb = lerp(addResult2, multiplyResult2, isMultiply2);
        col.rgb = lerp(col.rgb, replaceResult2, isReplace2);
        half matCap2BlendFaded = _MatCapBlend2;
        #ifdef _DISTANCE_FADE
            matCap2BlendFaded *= lerp(1.0, distanceFade, _MatCap2DistFade);
        #endif
        col.rgb = lerp(preMatCap2, col.rgb, matCap2BlendFaded);
    } // if (_MatCap2 >= 0.5)
    #endif
    #endif // !_QUEST_LITE

    // ===== MatCap 3 (ForwardBase only) =====
    #ifndef _QUEST_LITE
    #if defined(_MATCAP_3) && defined(UNITY_PASS_FORWARDBASE)
    if (_MatCap3 >= 0.5)
    {
        half3 matcap3 = SampleTex2DBlur3Repeat(_MatCapTex3, sharedMatCapUV, _MatCap3Blur) * _MatCapIntensity3;

        half matcapMask3 = NATANE_SAMPLE_SHARED_R(_MatCapMask3, _MatCapTex3, uv);
        matcapMask3 = ApplySoftMask(matcapMask3);
        matcap3 *= matcapMask3;
        matcap3 *= _Glossiness;
        matcap3 = ApplyMatteQuality(matcap3, col.rgb, _MatteEffect);

        half3 preMatCap3 = col.rgb;
        half matcapStrength3 = saturate(_MatCapIntensity3 * matcapMask3);
        half3 addResult3 = SafeAdditiveBlend(col.rgb, matcap3, matcapStrength3);
        half3 multiplyResult3 = BlendWithSoftMask(col.rgb, col.rgb * matcap3, saturate(_MatCapIntensity3 * matcapMask3));
        half3 replaceResult3 = BlendWithSoftMask(col.rgb, matcap3, saturate(_MatCapIntensity3 * matcapMask3));

        half isMultiply3 = step(HALF_VALUE, _MatCapBlendMode3) * step(_MatCapBlendMode3, 1.5);
        half isReplace3 = step(1.5, _MatCapBlendMode3);
        col.rgb = lerp(addResult3, multiplyResult3, isMultiply3);
        col.rgb = lerp(col.rgb, replaceResult3, isReplace3);
        half matCap3BlendFaded = _MatCapBlend3;
        #ifdef _DISTANCE_FADE
            matCap3BlendFaded *= lerp(1.0, distanceFade, _MatCap3DistFade);
        #endif
        col.rgb = lerp(preMatCap3, col.rgb, matCap3BlendFaded);
    } // if (_MatCap3 >= 0.5)
    #endif
    #endif // !_QUEST_LITE

    // ===== Procedural MatCap (ForwardBase only) =====
    #if defined(_PROCEDURAL_MATCAP) && defined(UNITY_PASS_FORWARDBASE)
    if (_ProceduralMatCap >= 0.5)
    {
        // Spherical gradient from view-space normal
        half gradient = pow(saturate(1.0 - length(sharedMatCapUV - 0.5) * 2.0), 1.0 / max(_ProcMatCapPower, 0.001));
        // Fresnel rim enhancement (mirror-safe)
        float3 procViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
        procViewNormal.x *= NataneMirrorSign();
        half procNdotV = saturate(dot(normalize(procViewNormal), float3(0, 0, 1)));
        half procFresnel = pow(1.0 - procNdotV, _ProcMatCapFresnelPower);
        half3 procMatCap = _ProcMatCapColor.rgb * (gradient + procFresnel) * _ProcMatCapIntensity;

        half3 preProcMatCap = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, procMatCap, saturate(_ProcMatCapIntensity * 0.5));
        half procMatCapBlendFaded = _ProcMatCapBlend;
        #ifdef _DISTANCE_FADE
            procMatCapBlendFaded *= lerp(1.0, distanceFade, 0.5);
        #endif
        col.rgb = ApplyEffectBlendPost(preProcMatCap, col.rgb, procMatCapBlendFaded, _ProcMatCapBlendMode);
    }
    #endif

    // ===== Cubemap Reflection (ForwardBase only) =====
    #if defined(_REFLECTION) && defined(UNITY_PASS_FORWARDBASE)
    if (_Reflection >= 0.5)
    {
        half3 reflection = CubemapReflection(worldNormal, viewDir, _Smoothness, _Metallic);

        // Apply mask texture with soft blending
        half reflectionMask = NATANE_SAMPLE_SHARED_R(_ReflectionMask, _MainTex, uv);
        reflectionMask = ApplySoftMask(reflectionMask); // Smooth mask transitions
        reflection *= reflectionMask;

        // Apply glossiness and matte material quality
        reflection *= _Glossiness * specularOcclusion * exposureOcclusion;
        reflection = ApplyMatteQuality(reflection, col.rgb, _MatteEffect);

        // Use safe additive blending to prevent white-out
        half reflectionStrength = saturate(length(reflection) * reflectionMask * 0.5);
        half3 preReflection = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, reflection, reflectionStrength);
        half reflectionBlendFaded = _ReflectionBlend;
        #ifdef _DISTANCE_FADE
            reflectionBlendFaded *= lerp(1.0, distanceFade, _ReflectionDistFade);
        #endif
        col.rgb = lerp(preReflection, col.rgb, reflectionBlendFaded);
    } // if (_Reflection >= 0.5)
    #endif

    // ===== Clear Coat / Wetness Layer (ForwardBase only) =====
    #if defined(UNITY_PASS_FORWARDBASE)
    if (NATANE_FX(_ClearCoatIntensity > 0.001))
    {
        half coatMask = NATANE_SAMPLE_SHARED_R(_ClearCoatMask, _MainTex, uv);
        coatMask = ApplySoftMask(coatMask);
        half coatStrength = saturate(_ClearCoatIntensity * coatMask);

        if (coatStrength > 0.001)
        {
            half3 coatNormalTS = UnpackScaleNormal(
                NATANE_SAMPLE_REPEAT(_ClearCoatNormalMap, uv),
                _ClearCoatNormalScale);
            half3 coatNormalWS = normalize(mul(coatNormalTS, tangentToWorld));

            half coatRoughness = max(0.02, 1.0 - saturate(_ClearCoatSmoothness));
            half3 coatF0 = half3(0.04, 0.04, 0.04);
            half coatGrazing = pow(1.0 - saturate(dot(coatNormalWS, viewDir)), _ClearCoatFresnelPower);
            half coatFresnelFactor = lerp(0.25, 1.0, saturate(coatGrazing));

            half3 clearCoat = NatanePBRSpecular(coatNormalWS, viewDir, lightDir,
                coatRoughness, coatF0, effectiveLightColor, atten);
            clearCoat += NatanePBRIndirectSpecular(coatNormalWS, viewDir, i.worldPos,
                coatRoughness, coatF0) * _GIIntensity * coatFresnelFactor * exposureOcclusion;

            clearCoat *= coatStrength * specularOcclusion;
            clearCoat = ApplyMatteQuality(clearCoat, col.rgb, _MatteEffect);

            half clearCoatBlend = saturate(length(clearCoat) * 0.35 + coatStrength * 0.35);
            half3 preClearCoat = col.rgb;
            col.rgb = SafeAdditiveBlend(col.rgb, clearCoat, clearCoatBlend);
            col.rgb = lerp(preClearCoat, col.rgb, coatStrength);
        }
    }
    #endif

    // ===== Fake Environment Reflection (Cubemap-free, ForwardBase only) =====
    #if defined(_FAKE_REFLECTION) && defined(UNITY_PASS_FORWARDBASE)
    if (_FakeReflection >= 0.5)
    {
        // Reflect view direction around surface normal — mirror-safe
        float3 reflectDir = reflect(-viewDir, worldNormal);
        // World-space reflection vector; valid in mirrors without compensation.
        // Sky-ground gradient based on reflection Y component
        float skyFactor = saturate(reflectDir.y * _FakeReflSmoothness * 5.0 + 0.5);
        half3 fakeRefl = lerp(_FakeReflGroundColor.rgb, _FakeReflSkyColor.rgb, skyFactor);
        // Fresnel: stronger reflection at grazing angles — mirror-safe via view-space
        float3 fakeReflViewN = mul((float3x3)UNITY_MATRIX_V, worldNormal);
        fakeReflViewN.x *= NataneMirrorSign();
        half fresnel = pow(1.0 - saturate(dot(normalize(fakeReflViewN), float3(0, 0, 1))), _FakeReflFresnelPower);
        fakeRefl *= fresnel * _FakeReflIntensity;

        half3 preFakeRefl = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, fakeRefl, saturate(fresnel * _FakeReflIntensity * 0.5));
        half fakeReflBlendFaded = _FakeReflBlend;
        #ifdef _DISTANCE_FADE
            fakeReflBlendFaded *= lerp(1.0, distanceFade, 0.5);
        #endif
        col.rgb = ApplyEffectBlendPost(preFakeRefl, col.rgb, fakeReflBlendFaded, _FakeReflBlendMode);
    }
    #endif

    // ===== Refraction (ForwardBase only) =====
    #if defined(_REFRACTION) && defined(UNITY_PASS_FORWARDBASE)
    if (_Refraction >= 0.5)
    {
        // Calculate screen UV from screen position
        float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);

        // Apply refraction mask
        float refractionMask = NATANE_SAMPLE_SHARED_R(_RefractionMask, _MainTex, uv);
        refractionMask = ApplySoftMask(refractionMask);

        // Calculate distorted UV based on surface normal and refraction settings
        float2 distortedUV = ApplyRefractionDistortion(
            screenUV,
            worldNormal,
            viewDir,
            _RefractionIntensity * refractionMask,
            _RefractionIndex,
            _RefractionBlur
        );

        // Sample background with optional blur
        float3 refractedColor = SampleGrabTextureWithBlur(distortedUV, _RefractionBlur);

        // Blend refracted color with current color based on alpha and refraction intensity
        // Higher intensity = more refraction visible
        float refractionBlend = _RefractionIntensity * refractionMask * (1.0 - col.a);
        half3 preRefraction = col.rgb;
        col.rgb = lerp(col.rgb, refractedColor, saturate(refractionBlend));
        half refractionBlendFaded = _RefractionBlend;
        #ifdef _DISTANCE_FADE
            refractionBlendFaded *= lerp(1.0, distanceFade, _RefractionDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preRefraction, col.rgb, refractionBlendFaded, _RefractionBlendMode);
    } // if (_Refraction >= 0.5)
    #endif

    // ===== Highlight compression (ForwardBase only, before emission) =====
    // Rolls off over-bright lit colour without clipping a channel or shifting the hue.
    // Placed before emission / glitter so their HDR output still drives world bloom.
    #ifdef UNITY_PASS_FORWARDBASE
    if (NATANE_FX(_HighlightCompress > 0.001))
    {
        col.rgb = lerp(col.rgb, NataneNeutralHighlightCompress(max(col.rgb, 0.0)), saturate(_HighlightCompress));
    }
    #endif

    // ===== Emission (ForwardBase only) =====
    #if defined(_EMISSION) && defined(UNITY_PASS_FORWARDBASE)
    half3 nataneHdrEmission = half3(0, 0, 0); // HDR overshoot re-added after the final LDR clamp (keeps bloom alive)
    if (_Emission >= 0.5)
    {
        float2 emissionUV = uv;

        // Apply UV animation (scroll XY + rotation)
        if (abs(_EmissionScrollSpeed) > 0.001 || abs(_EmissionScrollSpeedY) > 0.001 || abs(_EmissionRotateSpeed) > 0.001)
        {
            emissionUV = AnimateUV(uv, float2(_EmissionScrollSpeed, _EmissionScrollSpeedY), _EmissionRotateSpeed);
        }

        half3 emission = SampleTex2DBlur3Repeat(_EmissionMap, emissionUV, _EmissionBlur) * _EmissionColor.rgb;

        // FX Modulator: EmissionIntensity target
        #if defined(_FX_MODULATOR)
            emission *= NataneFXModMul(nataneFXState, NATANE_FXT_EMISSION);
        #endif

        // Apply pulse animation
        if (_EmissionPulseSpeed > 0.001)
        {
            half pulse = sin(_Time.y * _EmissionPulseSpeed) * 0.5 + 0.5;
            pulse = lerp(1.0 - _EmissionPulseAmplitude, 1.0, pulse);
            emission *= pulse;
        }

        // Apply mask texture with soft blending
        float2 emMaskUV = AnimateUVIfNeeded(uv, _EmissionMaskScrollSpeed.xy, _EmissionMaskRotateSpeed);
        half emissionMask = NATANE_SAMPLE_SHARED_R(_EmissionMask, _EmissionMap, emMaskUV);
        emissionMask = ApplySoftMask(emissionMask); // Smooth mask transitions
        emission *= emissionMask;

        // Mirror emission multiplier (VRChat)
        #ifdef _MIRROR_CONTROL
            emission *= lerp(1.0, _MirrorEmissionMultiplier, NataneIsMirror());
        #endif

        // Apply Glow/Bloom effect - Optimized: removed branching, use cached luminance
        half emissionLum = CALC_LUMINANCE(emission);
        half3 glow = emission * emissionLum * _EmissionGlow * 2.0;
        emission += glow * step(0.001, _EmissionGlow); // Conditional add without branch

        // Use safe additive blending to prevent white-out
        half emissionStrength = saturate(length(emission) * emissionMask * 0.6);
        half3 preEmission = col.rgb;
        col.rgb = SafeAdditiveBlend(col.rgb, emission, emissionStrength);
        half emissionBlendFaded = _EmissionBlend;
        #ifdef _DISTANCE_FADE
            emissionBlendFaded *= lerp(1.0, distanceFade, _EmissionDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preEmission, col.rgb, emissionBlendFaded, _EmissionBlendMode);
        // Preserve the HDR portion (>1.0) of [HDR] _EmissionColor so it can
        // drive bloom; SafeAdditiveBlend and the final clamp are LDR-bound.
        nataneHdrEmission = max(emission - half3(1, 1, 1), half3(0, 0, 0)) * saturate(emissionBlendFaded);
    } // if (_Emission >= 0.5)
    #endif

    // ===== Topographic / Fault-Slice overlay (ForwardBase only) =====
    #if defined(_TOPOGRAPHIC) && defined(UNITY_PASS_FORWARDBASE)
    if (_Topographic >= 0.5)
    {
        // Build the slicing coordinate from the selected space + axis.
        float3 topoObj  = mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
        float3 topoView = mul(UNITY_MATRIX_V, float4(i.worldPos, 1.0)).xyz;
        float3 topoSrc  = (_TopoSpace < 0.5) ? topoObj : ((_TopoSpace < 1.5) ? i.worldPos : topoView);

        float3 topoDir;
        if (_TopoAxis < 0.5)      topoDir = float3(1.0, 0.0, 0.0);
        else if (_TopoAxis < 1.5) topoDir = float3(0.0, 1.0, 0.0);
        else if (_TopoAxis < 2.5) topoDir = float3(0.0, 0.0, 1.0);
        else                      topoDir = normalize(_TopoCustomDir.xyz + float3(0.0, 0.0001, 0.0));

        float topoCoord  = dot(topoSrc, topoDir);
        float topoOffset = _TopoOffset;
        #if defined(_FX_MODULATOR)
            topoOffset += NataneFXModAdd(nataneFXState, NATANE_FXT_TOPO_OFFSET);
        #endif
        float topoPhase = (topoCoord + topoOffset + _Time.y * _TopoSpeed) / max(_TopoSpacing, 0.0001);

        // Optional noise distortion of the band coordinate.
        if (_TopoNoiseStrength > 0.001)
        {
            float topoN = NataneTopo_Noise(topoSrc.xy * _TopoNoiseScale + topoSrc.zz * _TopoNoiseScale);
            topoPhase += (topoN - 0.5) * _TopoNoiseStrength;
        }

        float2 topoBand = NataneTopoBand(topoPhase, _TopoMode, _TopoLineWidth, _Time.y * _TopoSpeed);

        half3 topoColor = lerp(_TopoColor.rgb, _TopoColor2.rgb, topoBand.y);
        half  topoAlpha = topoBand.x * _TopoBlend;

        half topoMask = NATANE_SAMPLE_SHARED_R(_TopoMask, _MainTex, uv);
        topoMask = ApplySoftMask(topoMask);
        topoAlpha *= topoMask;

        half3 topoContrib = topoColor * _TopoEmission;
        half3 preTopo = col.rgb;

        // 合成方法。Caustics / 影の玉ボケと同じ列挙・同じ意味に揃えてある。
        // 以前は常に加算合成で陰影と無関係だったため、「影の中にだけ等高線を出す」
        // といった使い方ができなかった。
        int topoComp = (int)(_TopoComposite + 0.5);
        half topoWeight = saturate(topoAlpha);

        if (topoComp == 1)      // BaseColor multiply-brighten
        {
            col.rgb *= (half3(1, 1, 1) + topoContrib * topoWeight);
        }
        else if (topoComp == 2) // LitOnly
        {
            half w = topoWeight * shadingValue;
            col.rgb = SafeAdditiveBlend(col.rgb, topoContrib * shadingValue, saturate(w));
        }
        else if (topoComp == 3) // ShadowOnly
        {
            half shadowAmount = 1.0 - shadingValue;
            half w = topoWeight * shadowAmount;
            col.rgb = SafeAdditiveBlend(col.rgb, topoContrib * shadowAmount, saturate(w));
        }
        else                    // Emission-add
        {
            col.rgb = SafeAdditiveBlend(col.rgb, topoContrib, topoWeight);
        }

        #ifdef _DISTANCE_FADE
            col.rgb = lerp(preTopo, col.rgb, distanceFade);
        #endif
        // Preserve HDR overshoot so emission-strength contour lines drive bloom.
        #if defined(_EMISSION)
            if (topoComp == 0)
                nataneHdrEmission += max(topoContrib - half3(1, 1, 1), half3(0, 0, 0)) * topoWeight;
        #endif
    } // if (_Topographic >= 0.5)
    #endif

    // ===== Surface Caustics (ForwardBase only) =====
    #if defined(_CAUSTICS) && defined(UNITY_PASS_FORWARDBASE)
    if (_Caustics >= 0.5)
    {
        float3 causObj = mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
        float2 causCoord = NataneCausticsCoord(_CausticsSpace, uv, causObj, i.worldPos, worldNormal);
        causCoord = causCoord * _CausticsScale + _CausticsDirection.xy * (_Time.y * _CausticsSpeed);

        float causTime = _Time.y * _CausticsSpeed;
        float causPat;
        if (_CausticsPatternMode > 0.5) // Texture: two scrolling samples multiplied
        {
            float2 causUV2 = causCoord * 0.73 - _CausticsDirection.xy * (_Time.y * _CausticsSpeed * 0.6);
            half causA = NATANE_SAMPLE_SHARED_R(_CausticsTex, _MainTex, causCoord);
            half causB = NATANE_SAMPLE_SHARED_R(_CausticsTex, _MainTex, causUV2);
            causPat = causA * causB;
        }
        else
        {
            #ifdef _QUEST_LITE
                causPat = NataneCausticsPatternLite(causCoord, causTime, _CausticsDistortion);
            #else
                causPat = NataneCausticsPattern(causCoord, causTime, _CausticsDistortion);
            #endif
        }
        causPat = pow(saturate(causPat), max(_CausticsContrast, 0.0001));

        half causMask = NATANE_SAMPLE_SHARED_R(_CausticsMask, _MainTex, uv);
        causMask = ApplySoftMask(causMask);

        half causIntensityMod = _CausticsIntensity;
        #if defined(_FX_MODULATOR)
            causIntensityMod *= NataneFXModMul(nataneFXState, NATANE_FXT_CAUSTICS);
        #endif
        half3 causContrib = _CausticsColor.rgb * (causPat * causIntensityMod * causMask);
        int causComp = (int)(_CausticsComposite + 0.5);
        half3 preCaus = col.rgb;
        if (causComp == 1)      // BaseColor multiply-brighten
            col.rgb *= (half3(1, 1, 1) + causContrib);
        else if (causComp == 2) // LitOnly (scale by shading value)
            col.rgb = SafeAdditiveBlend(col.rgb, causContrib * shadingValue, saturate(length(causContrib) * shadingValue));
        else if (causComp == 3) // ShadowOnly (inverse shading)
            col.rgb = SafeAdditiveBlend(col.rgb, causContrib * (1.0 - shadingValue), saturate(length(causContrib) * (1.0 - shadingValue)));
        else                    // Emission-add
            col.rgb = SafeAdditiveBlend(col.rgb, causContrib, saturate(length(causContrib) * 0.7));

        #ifdef _DISTANCE_FADE
            col.rgb = lerp(preCaus, col.rgb, distanceFade);
        #endif
        // Preserve HDR overshoot so emission-composite caustics can drive bloom.
        #if defined(_EMISSION)
            if (causComp == 0)
                nataneHdrEmission += max(causContrib - half3(1, 1, 1), half3(0, 0, 0));
        #endif
    } // if (_Caustics >= 0.5)
    #endif

    // ===== Shadow Bokeh / 影の玉ボケ（ForwardBase only） =====
    #if defined(_SHADOW_BOKEH) && defined(UNITY_PASS_FORWARDBASE)
    if (_ShadowBokeh >= 0.5)
    {
        // 光源方向に垂直な平面へ投影する。ワールド座標を使うので、
        // カメラを動かしても光斑は面に貼り付いたままになる。
        float2 bokehCoord = NataneLightPlaneCoord(i.worldPos, lightDir);
        bokehCoord = bokehCoord * _ShadowBokehScale
                   + _ShadowBokehDirection.xy * (_Time.y * _ShadowBokehSpeed);

        float bokehPat;
        #ifdef _QUEST_LITE
            bokehPat = NataneShadowBokehPatternLite(
                bokehCoord, _ShadowBokehSize, _ShadowBokehSoftness, _ShadowBokehDensity);
        #else
            bokehPat = NataneShadowBokehPattern(
                bokehCoord, _ShadowBokehSize, _ShadowBokehSoftness,
                _ShadowBokehBlades, _ShadowBokehRimGain, _ShadowBokehDensity);
        #endif

        half bokehMask = NATANE_SAMPLE_SHARED_R(_ShadowBokehMask, _MainTex, uv);
        bokehMask = ApplySoftMask(bokehMask);

        // どれだけ影に入ったら出すか。半影に薄く出て輪郭が濁るのを防ぐ。
        float shadowAmount = 1.0 - shadingValue;
        float shadowGate = smoothstep(
            _ShadowBokehShadowMin, min(_ShadowBokehShadowMin + 0.25, 1.0), shadowAmount);

        half3 bokehContrib = _ShadowBokehColor.rgb
                           * (bokehPat * _ShadowBokehIntensity * bokehMask * _ShadowBokehBlend);

        int bokehComp = (int)(_ShadowBokehComposite + 0.5);
        half3 preBokeh = col.rgb;

        if (bokehComp == 1)
        {
            // LitOnly: 光の中に葉影を落とす。暗くする側。
            half leaf = saturate(bokehPat * _ShadowBokehIntensity * bokehMask * _ShadowBokehBlend);
            col.rgb = lerp(col.rgb, col.rgb * _ShadowBokehColor.rgb, leaf * shadingValue);
        }
        else if (bokehComp == 2)
        {
            // All: 影の内外に関係なく加算。
            col.rgb = SafeAdditiveBlend(col.rgb, bokehContrib, saturate(length(bokehContrib)));
        }
        else
        {
            // ShadowOnly（既定）: 影の中にだけ光斑を落とす＝木漏れ日。
            half w = shadowGate;
            col.rgb = SafeAdditiveBlend(col.rgb, bokehContrib * w, saturate(length(bokehContrib) * w));
        }

        #ifdef _DISTANCE_FADE
            col.rgb = lerp(preBokeh, col.rgb, distanceFade);
        #endif

        // HDR のはみ出しを残して Bloom を焚けるようにする（Caustics と同じ扱い）。
        #if defined(_EMISSION)
            if (bokehComp != 1)
                nataneHdrEmission += max(bokehContrib - half3(1, 1, 1), half3(0, 0, 0));
        #endif
    } // if (_ShadowBokeh >= 0.5)
    #endif

    // ===== Virtual Expression - Hue Shift =====
    // Optimized: removed branching (ApplyHueShift handles _HueShift=0 efficiently)
    #if defined(_HUE_SHIFT) && defined(UNITY_PASS_FORWARDBASE)
    if (_HueShiftEnable >= 0.5)
    {
        half3 preHue = col.rgb;
        float hueShiftBlurred = _HueShift * (1.0 - _HueShiftBlur * 0.7);
        // FX Modulator: HueShift target
        #if defined(_FX_MODULATOR)
            hueShiftBlurred += NataneFXModAdd(nataneFXState, NATANE_FXT_HUESHIFT);
        #endif
        col.rgb = ApplyHueShift(col.rgb, hueShiftBlurred);
        col.rgb = lerp(preHue, col.rgb, _HueShiftBlend);
    }
    #endif

    // ===== AudioLink Integration (ForwardBase only) =====
    #if defined(_AUDIOLINK) && defined(UNITY_PASS_FORWARDBASE)
    if (_AudioLink >= 0.5)
    {
        half3 preAL = col.rgb;
        // AudioLink Emission - modulate emission brightness with audio
        if (_AudioLinkEmissionIntensity > 0.001)
        {
            half alEmission = SampleAudioLink(_AudioLinkEmissionBand);
            #if defined(_EMISSION)
                col.rgb = SafeAdditiveBlendFast(col.rgb, _EmissionColor.rgb * alEmission * _AudioLinkEmissionIntensity, 1.0);
            #endif
        }

        // AudioLink Rim - audio-reactive rim light (requires Rim Light feature)
        #if defined(_RIM_LIGHT)
        if (_AudioLinkRimIntensity > 0.001)
        {
            half alRim = SampleAudioLink(_AudioLinkRimBand);
            // Mirror-safe AudioLink rim
            float3 alRimViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
            alRimViewNormal.x *= NataneMirrorSign();
            half alRimFactor = 1.0 - saturate(dot(normalize(alRimViewNormal), float3(0, 0, 1)));
            alRimFactor = alRimFactor * alRimFactor * alRimFactor;
            col.rgb = SafeAdditiveBlendFast(col.rgb, _RimColor.rgb * alRim * _AudioLinkRimIntensity * alRimFactor, 1.0);
        }
        #endif

        // AudioLink Hue Shift - audio-reactive color shift
        if (_AudioLinkHueShiftIntensity > 0.001)
        {
            half alHue = SampleAudioLink(_AudioLinkHueBand);
            col.rgb = ApplyHueShift(col.rgb, alHue * _AudioLinkHueShiftIntensity);
        }

        // AudioLink Dissolve - audio-reactive dissolve (requires Dissolve feature)
        // The toggle is read here so the inspector checkbox actually gates the
        // effect. Before this it was declared but referenced nowhere (NPR2026 F4).
        #if defined(_DISSOLVE)
        if (_AudioLinkDissolve >= 0.5 && _AudioLinkDissolveIntensity > 0.001)
        {
            half alDissolve = SampleAudioLink(_AudioLinkDissolveBand);
            float2 alDissolveUV = AnimateUVIfNeeded(uv, _DissolveTexScrollSpeed.xy, _DissolveTexRotateSpeed);
            float2 alDissolveResult = CalculateDissolve(alDissolveUV, alDissolve * _AudioLinkDissolveIntensity, 0.1);
            half3 alDissolveGlow = _DissolveEdgeColor.rgb * alDissolveResult.y * 2.0;
            col.rgb = SafeAdditiveBlendFast(col.rgb, alDissolveGlow, saturate(alDissolveResult.y));
        }
        #endif
        half audioLinkBlendFaded = _AudioLinkBlend;
        #ifdef _DISTANCE_FADE
            audioLinkBlendFaded *= lerp(1.0, distanceFade, _AudioLinkDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preAL, col.rgb, audioLinkBlendFaded, _AudioLinkBlendMode);
    }
    #endif

    // ===== Glitter Effect =====
    #ifndef _QUEST_LITE
    #if defined(_GLITTER) && defined(UNITY_PASS_FORWARDBASE)
    if (_Glitter >= 0.5)
    {
        float2 glitterMaskUV = AnimateUVIfNeeded(uv, _GlitterMaskScrollSpeed.xy, _GlitterMaskRotateSpeed);
        half3 glitter = GlitterEffect(glitterMaskUV, i.worldPos, viewDir, worldNormal, lightDir, _GlitterBlur);
        glitter = ApplyMatteQuality(glitter, col.rgb, _MatteEffect);
        half3 preGlitter = col.rgb;
        col.rgb = SafeAdditiveBlendFast(col.rgb, glitter, 1.0);
        half glitterBlendFaded = _GlitterBlend;
        #ifdef _DISTANCE_FADE
            glitterBlendFaded *= lerp(1.0, distanceFade, _GlitterDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preGlitter, col.rgb, glitterBlendFaded, _GlitterBlendMode);
    } // if (_Glitter >= 0.5)
    #endif
    #endif // !_QUEST_LITE

    // ===== Iridescence Effect =====
    #if defined(_IRIDESCENCE) && defined(UNITY_PASS_FORWARDBASE)
    if (_Iridescence >= 0.5)
    {
        float iridSizeBlurred = lerp(_IridescenceSize, _IridescenceSize * 3.0, _IridescenceBlur);
        half3 iridescence = IridescenceEffect(worldNormal, viewDir, uv, iridSizeBlurred);
        iridescence = ApplyMatteQuality(iridescence, col.rgb, _MatteEffect);
        half3 preIridescence = col.rgb;
        col.rgb = SafeAdditiveBlendFast(col.rgb, iridescence, 1.0);
        half iridescenceBlendFaded = _IridescenceBlend;
        #ifdef _DISTANCE_FADE
            iridescenceBlendFaded *= lerp(1.0, distanceFade, _IridescenceDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preIridescence, col.rgb, iridescenceBlendFaded, _IridescenceBlendMode);
    } // if (_Iridescence >= 0.5)
    #endif

    // ===== SMEAR EFFECT (スミア / 残像エフェクト) =====
    #ifdef _SMEAR
    if (_Smear >= 0.5)
    {
        float smearStretch = i.smearStretchFactor;

        if (smearStretch > 0.001)
        {
            float3 smearDir;
            if (_SmearAutoMagnitude > 0.5)
            {
                float speed = length(_SmearDirection.xyz);
                smearDir = (speed > 0.001) ? _SmearDirection.xyz / speed : float3(0, 0, 1);
            }
            else
            {
                smearDir = normalize(_SmearDirection.xyz + float3(0.0001, 0.0001, 0.0001));
            }

            // Apply mask
            float2 smearMaskUV = AnimateUVIfNeeded(uv, _SmearMaskScrollSpeed.xy, _SmearMaskRotateSpeed);
            half smearMask = NATANE_SAMPLE_REPEAT(_SmearMask, smearMaskUV).r;
            smearMask = ApplySoftMask(smearMask);

            // Trail
            half3 smearTrail = CalculateSmearTrail(uv, col.rgb, smearDir, worldNormal, smearStretch, _SmearTrailLength, _SmearTrailFade);

            // Glow
            half3 smearGlow = CalculateSmearGlow(worldNormal, viewDir, smearDir, smearStretch,
                                                  _SmearGlowColor, _SmearGlowIntensity, _SmearGlowPower);

            // Emission
            half3 smearEmission = _SmearEmissionColor.rgb * _SmearEmission * smearStretch;

            // Combine
            half3 smearEffect = (smearTrail + smearGlow + smearEmission) * smearMask;

            // Store pre-smear color for blending
            half3 preSmearColor = col.rgb;

            // Apply blend
            half3 smearBlended = SafeAdditiveBlend(col.rgb, smearEffect, _SmearBlur);
            col.rgb = ApplyEffectBlendPost(preSmearColor, smearBlended, _SmearBlend, _SmearBlendMode);

            // Distance fade
            #ifdef _DISTANCE_FADE
                // distanceFade is 1 near and 0 far: keep the smear near, drop it with distance.
                // (It was inverted and removed the effect up close.)
                col.rgb = lerp(preSmearColor, col.rgb, lerp(1.0, distanceFade, _SmearDistFade));
            #endif
        }
    }
    #endif

    // ===== Water Drip Effect (ForwardBase only) =====
    #ifndef _QUEST_LITE
    #if defined(_WATER_DRIP) && defined(UNITY_PASS_FORWARDBASE)
    if (_WaterDrip >= 0.5)
    {
        float2 dripMaskUV = AnimateUVIfNeeded(uv, _DripMaskScrollSpeed.xy, _DripMaskRotateSpeed);
        half dripMaskValue = NATANE_SAMPLE_REPEAT(_DripMask, dripMaskUV).r;
        dripMaskValue = ApplySoftMask(dripMaskValue);

        float dripSharpnessBlurred = max(0.1, _DripSharpness * (1.0 - _DripBlur * 0.8));
        half3 drip = CalculateDripEffectFast(
            i.worldPos, _Time.y, _DripColor.rgb, _DripSpeed,
            _DripDensity, _DripSize, _DripTrailLength,
            _DripIntensity, dripSharpnessBlurred
        );
        drip *= dripMaskValue;
        half3 preDrip = col.rgb;
        col.rgb = SafeAdditiveBlendFast(col.rgb, drip, 1.0);
        half dripBlendFaded = _DripBlend;
        #ifdef _DISTANCE_FADE
            dripBlendFaded *= lerp(1.0, distanceFade, _DripDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preDrip, col.rgb, dripBlendFaded, _DripBlendMode);
    }
    #endif
    #endif // !_QUEST_LITE

    // ===== Hologram Effect (ForwardBase only) =====
    #ifndef _QUEST_LITE
    #if defined(_HOLOGRAM) && defined(UNITY_PASS_FORWARDBASE)
    if (_Hologram >= 0.5)
    {
        half3 preHolo = col.rgb;
        half preHoloAlpha = col.a;
        float2 holoUV = uv;

        // Noise distortion
        #if defined(_HOLOGRAM_NOISE)
            holoUV = CalculateHologramNoiseDistortion(holoUV,
                _HologramNoiseIntensity, _HologramNoiseSpeed);
        #endif

        // Hologram mask
        float2 holoMaskUV = AnimateUVIfNeeded(uv, _HologramMaskScrollSpeed.xy, _HologramMaskRotateSpeed);
        half holoMask = NATANE_SAMPLE_CLAMP(_HologramMask, holoMaskUV).r;

        // Multi-layer scanline (blur widens scanline width for softer effect)
        float holoWidthBlurred = _HologramScanlineWidth + _HologramBlur * 0.3;
        half scanline = CalculateHologramScanline(holoUV,
            _HologramScanlineSpeed, _HologramScanlineIntensity,
            _HologramScanlineDensity, holoWidthBlurred);
        col.rgb *= lerp(1.0, scanline, holoMask);

        // Fresnel edge glow
        half3 edgeGlow = CalculateHologramEdgeGlow(
            worldNormal, viewDir, _HologramColor.rgb,
            _HologramEdgeGlowPower, _HologramEdgeGlowIntensity);
        col.rgb += edgeGlow * holoMask;

        // Hologram color / monochrome
        col.rgb = lerp(col.rgb,
            ApplyHologramColor(col.rgb, _HologramColor.rgb, _HologramMonochrome),
            holoMask);

        // Flicker
        half flicker = CalculateHologramFlicker(
            _HologramFlickerSpeed, _HologramFlickerAmount);
        col.a *= lerp(1.0, flicker, holoMask);

        // Hologram alpha (Fresnel-linked transparency)
        col.a = lerp(col.a,
            CalculateHologramAlpha(worldNormal, viewDir, col.a, _HologramAlpha),
            holoMask);
        half hologramBlendFaded = _HologramBlend;
        #ifdef _DISTANCE_FADE
            hologramBlendFaded *= lerp(1.0, distanceFade, _HologramDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preHolo, col.rgb, hologramBlendFaded, _HologramBlendMode);
        col.a = ApplyEffectBlendPostAlpha(preHoloAlpha, col.a, hologramBlendFaded);
    }
    #endif
    #endif // !_QUEST_LITE

    // ===== Glitch Effect (ForwardBase only) =====
    #ifndef _QUEST_LITE
    #if defined(_GLITCH) && defined(UNITY_PASS_FORWARDBASE)
    if (_Glitch >= 0.5)
    {
        half3 preGlitch = col.rgb;

        // Glitch mask sampling with scale amplification
        half glitchMask = NATANE_SAMPLE_REPEAT(_GlitchMask, TRANSFORM_TEX(uv, _GlitchMask)).r;
        // Mask Scale: マスク値を増幅して「白い部分をさらに強く」できる
        // Scale=1: 等倍（従来通り）、Scale=5: マスク0.2→1.0に増幅
        glitchMask = saturate(glitchMask * _GlitchMaskScale);

        // Glitch trigger (random occurrence)
        // Mask Affects Frequency: マスク値が頻度にも影響（白い部分ほど頻繁にグリッチ発生）
        half effectiveFrequency = lerp(_GlitchFrequency, _GlitchFrequency * glitchMask, _GlitchMaskAffectsFrequency);
        half glitchTrigger = step(1.0 - effectiveFrequency,
            frac(sin(_Time.y * _GlitchSpeed) * 43758.5453));

        // Apply mask to intensity (amplified by MaskScale)
        half maskedGlitchIntensity = _GlitchIntensity * glitchMask;

        if (glitchTrigger > 0.5 && maskedGlitchIntensity > 0.001)
        {
            // UV distortion
            float2 glitchUV = CalculateGlitchUV(uv,
                maskedGlitchIntensity, _GlitchSpeed, _GlitchBlockSize);

            // RGB split — Mask Affects RGB Split: マスクでRGBスプリットも制御
            half maskedRGBSplit = lerp(_GlitchRGBSplitIntensity,
                _GlitchRGBSplitIntensity * glitchMask, _GlitchMaskAffectsRGBSplit);
            half3 splitColor = CalculateGlitchRGBSplit(
                col.rgb, glitchUV, _MainTex, maskedRGBSplit);
            col.rgb = lerp(col.rgb, splitColor, maskedGlitchIntensity);
        }
        // Apply noise texture for additional glitch variety
        col.rgb = ApplyGlitchNoise(col.rgb, uv, _GlitchNoiseTex,
            _GlitchNoiseTex_ST, _GlitchNoiseScrollSpeed,
            _GlitchNoiseIntensity * glitchMask, _GlitchNoiseMode, _MainTex);
        // Blur dampens glitch distortion by blending back towards original
        col.rgb = lerp(col.rgb, preGlitch, _GlitchBlur * 0.5);
        half glitchBlendFaded = _GlitchBlend;
        #ifdef _DISTANCE_FADE
            glitchBlendFaded *= lerp(1.0, distanceFade, _GlitchDistFade);
        #endif
        col.rgb = ApplyEffectBlendPost(preGlitch, col.rgb, glitchBlendFaded, _GlitchBlendMode);
    }
    #endif
    #endif // !_QUEST_LITE

    // ===== Decal System (ForwardBase only) =====
    #if defined(_DECAL) && defined(UNITY_PASS_FORWARDBASE)
    if (_Decal >= 0.5)
    {
        float2 decalUV = CalculateDecalUV(uv, _DecalPosition.xy, _DecalRotation, _DecalScale);
        // Only apply within decal bounds (CalculateDecalUV returns -1,-1 if out of bounds)
        half decalInBounds = step(0.0, decalUV.x) * step(decalUV.x, 1.0) * step(0.0, decalUV.y) * step(decalUV.y, 1.0);
        if (decalInBounds > 0.5)
        {
            half3 preDecal = col.rgb;
            half4 decalSample = SampleTex2DBlurRepeat(_DecalTex, decalUV, _DecalBlur) * _DecalColor;
            half decalAlpha = decalSample.a;
            // Blend modes: 0=Add, 1=Multiply, 2=Overlay, 3=Replace
            half3 decalAdd = SafeAdditiveBlendFast(col.rgb, decalSample.rgb * decalAlpha, 1.0);
            half3 decalMul = lerp(col.rgb, col.rgb * decalSample.rgb, decalAlpha);
            half3 decalReplace = lerp(col.rgb, decalSample.rgb, decalAlpha);

            half isDecalMul = step(0.5, _DecalBlendMode) * step(_DecalBlendMode, 1.5);
            half isDecalReplace = step(2.5, _DecalBlendMode);
            col.rgb = lerp(decalAdd, decalMul, isDecalMul);
            col.rgb = lerp(col.rgb, decalReplace, isDecalReplace);
            half decalBlendFaded = _DecalBlend;
            #ifdef _DISTANCE_FADE
                decalBlendFaded *= lerp(1.0, distanceFade, _DecalDistFade);
            #endif
            col.rgb = lerp(preDecal, col.rgb, decalBlendFaded);
        }
    } // if (_Decal >= 0.5)
    #endif

    // ===== Virtual Expression - Dissolve =====
    #ifdef _DISSOLVE
    if (_Dissolve >= 0.5)
    {
        // FX Modulator can drive the dissolve amount, so an Animator layer is not
        // required for a self-running dissolve. Evaluated before the early-out so
        // that a material sitting at _DissolveAmount = 0 can still be modulated.
        float dissolveAmountEffective = _DissolveAmount;
        #if defined(_FX_MODULATOR)
            dissolveAmountEffective = saturate(dissolveAmountEffective
                + NataneFXModAdd(nataneFXState, NATANE_FXT_DISSOLVE));
        #endif

        // Early exit if dissolve amount is 0 (no effect)
        if (dissolveAmountEffective > 0.0)
        {
            // Mask sampling is gated by the toggle so materials that do not use a
            // mask do not pay the fetch. Default is 0 and an unset mask is "white",
            // so existing materials render identically either way.
            half dissolveMaskValue = 1.0;
            if (_UseDissolveMask >= 0.5)
            {
                dissolveMaskValue = NATANE_SAMPLE_SHARED_R(_DissolveMask, _MainTex, uv);
            }

            float dissolveEdgeBlurred = _DissolveEdgeWidth + _DissolveBlur * 0.15;
            float2 dissolveUV = AnimateUVIfNeeded(uv, _DissolveTexScrollSpeed.xy, _DissolveTexRotateSpeed);

            // Compute dissolve noise based on coordinate mode
            float dissolveNoise;
            if (_DissolveCoordMode > 0.5)
            {
                float3 dissolveCoordPos = _DissolveCoordMode < 1.5
                    ? i.worldPos
                    : mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
                float axisVal = _DissolveWorldAxis < 0.5 ? dissolveCoordPos.x
                    : (_DissolveWorldAxis < 1.5 ? dissolveCoordPos.y : dissolveCoordPos.z);
                float posNoise = saturate((axisVal - _DissolveWorldMin) / max(_DissolveWorldMax - _DissolveWorldMin, 0.01));
                float texNoise = NATANE_SAMPLE_REPEAT(_DissolveTex, dissolveUV).r;
                dissolveNoise = lerp(posNoise, posNoise * texNoise, _DissolveNoiseBlend);
            }
            else
            {
                dissolveNoise = NATANE_SAMPLE_REPEAT(_DissolveTex, dissolveUV).r;
            }
            float2 dissolveResult = CalculateDissolveFromNoise(dissolveNoise, dissolveAmountEffective, dissolveEdgeBlurred);
            half dissolveAlpha = dissolveResult.x;
            half edgeGlow = dissolveResult.y;

            // Apply mask to edge glow and dissolve effect
            edgeGlow *= dissolveMaskValue;

            // Apply edge glow with safe additive blending (branchless).
            half3 dissolveGlow = _DissolveEdgeColor.rgb * edgeGlow * _DissolveEdgeIntensity;
            half dissolveStrength = saturate(edgeGlow * _DissolveEdgeIntensity * 0.5);
            half3 preDissolve = col.rgb;
            col.rgb = SafeAdditiveBlend(col.rgb, dissolveGlow, dissolveStrength);
            col.rgb = ApplyEffectBlendPost(preDissolve, col.rgb, _DissolveBlend, _DissolveBlendMode);

            // Clip pixels based on dissolve amount and mask
            clip(dissolveAlpha + (1.0 - dissolveMaskValue));
        }
    } // if (_Dissolve >= 0.5)
    #endif

    // ================================================================
    // ===== STAGE B: Illustration Style — Screen Space Effects =====
    // ================================================================

    #if defined(UNITY_PASS_FORWARDBASE) && (defined(_WATERCOLOR) || defined(_SOFT_FILTER) || defined(_KUWAHARA_FILTER) || defined(_SCREEN_EDGE) || defined(_COLOR_BLEEDING) || defined(_CHROMATIC_ABERRATION))
    {
        float2 illustGrabUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
        float2 illustScreenUV = i.pos.xy / _ScreenParams.xy;
        // Depth / depth-normals lookups need the ComputeScreenPos convention
        // (stereo- and flip-aware); i.pos.xy / _ScreenParams is only right in mono.
        float2 illustDepthUV = illustGrabUV;

        #ifdef _WATERCOLOR
        if (_UseWatercolor >= 0.5)
        {
            float2 wcUV = uv;
            #if defined(_LINE_BOIL)
            if (_LineBoilAffectWatercolor >= 0.5)
                wcUV += nataneBoilUV;
            #endif
            half wcMask = NATANE_SAMPLE_REPEAT(_WCMask, TRANSFORM_TEX(wcUV, _WCMask)).r;
            half wcShading = dot(col.rgb, half3(0.299, 0.587, 0.114));
            col.rgb = ApplyWatercolor(col.rgb, wcUV, illustScreenUV, wcShading, wcMask,
                _WCGranulationTex, _WCGranulationTex_ST, _WCPaperTex, _WCPaperTex_ST,
                _WCEdgeDarkening, _WCWetEdge, _WCGranulation, _WCPaperIntensity, _WCPaperTiling, _WCBlend * nprWeight);
        }
        #endif

        #ifdef _SOFT_FILTER
        if (_UseSoftFilter >= 0.5)
        {
            col.rgb = ApplySoftFilter(col.rgb, illustGrabUV, _SoftFilterRadius,
                _SoftFilterBlend * nprWeight, _SoftFilterThreshold, _SoftFilterMode);
        }
        #endif

        #ifdef _KUWAHARA_FILTER
        if (_UseKuwahara >= 0.5)
        {
            col.rgb = ApplyKuwaharaFilter(illustGrabUV, (int)_KuwaharaRadius, _KuwaharaBlend * nprWeight, col.rgb);
        }
        #endif

        #ifdef _SCREEN_EDGE
        // Skipped in mirrors: the depth / depth-normals textures belong to the main view there.
        if (_UseScreenEdge >= 0.5 && !NataneIsMirror())
        {
            half edgeValue = ApplyScreenEdge(illustDepthUV, _EdgeDepthSensitivity, _EdgeNormalSensitivity, _EdgeWidth);
            col.rgb = lerp(col.rgb, _EdgeColor.rgb, edgeValue * _EdgeBlend * nprWeight);
        }
        #endif

        #ifdef _COLOR_BLEEDING
        if (_UseColorBleeding >= 0.5)
        {
            col.rgb = ApplyColorBleeding(col.rgb, illustGrabUV, _BleedingRadius, _BleedingBlend * nprWeight);
        }
        #endif

        #ifdef _CHROMATIC_ABERRATION
        if (_UseChromaticAberration >= 0.5)
        {
            col.rgb = ApplyChromaticAberration(illustGrabUV, _CAIntensity, _CABlend * nprWeight, col.rgb);
        }
        #endif
    }
    #endif

    // ===== Alpha Mask =====
    // Apply alpha mask for partial transparency control
    #ifdef _ALPHA_MASK
    if (_UseAlphaMask >= 0.5)
    {
        half alphaMask = NATANE_SAMPLE_SHARED_R(_AlphaMask, _MainTex, uv);
        col.a *= alphaMask;
    }
    #endif

    // ===== FX Modulator: AlphaFade target =====
    // Multiplies col.a. Only visible on a transparent-capable variant
    // (Transparent / Fade / X-Ray); opaque output ignores alpha.
    #if defined(_FX_MODULATOR)
        col.a *= NataneFXModMul(nataneFXState, NATANE_FXT_ALPHA_FADE);
    #endif

    // ===== Height Fade (Local Height-Based Transparency) =====
    #ifdef _HEIGHT_FADE
    if (_HeightFade >= 0.5)
    {
        half heightFade = CalculateHeightFade(i.worldPos, _HeightFadeStart, _HeightFadeEnd,
            _HeightFadeAxis, _HeightFadeSpace, _HeightFadeInvert);

        // Edge glow at fade boundary
        if (_HeightFadeEdgeWidth > 0.001)
        {
            float edgeLower = smoothstep(0.0, _HeightFadeEdgeWidth, heightFade);
            float edgeUpper = smoothstep(_HeightFadeEdgeWidth, _HeightFadeEdgeWidth * 2.0, heightFade);
            float edge = edgeLower * (1.0 - edgeUpper);
            col.rgb = lerp(col.rgb, _HeightFadeEdgeColor.rgb, edge * _HeightFadeEdgeColor.a);
        }

        if (_HeightFadeMode < 0.5)
        {
            half preHeightAlpha = col.a;
            col.a *= heightFade;
            col.a = ApplyEffectBlendPostAlpha(preHeightAlpha, col.a, _HeightFadeBlend);
        }
        else if (_HeightFadeMode < 1.5)
        {
            clip(heightFade - 0.001);
        }
        else
        {
            float ditherThreshold = DitheringPattern(i.pos.xy, max(_HeightFadeDitherScale, 1.0));
            clip(heightFade - ditherThreshold);
        }
    }
    #endif

    // ===== Intersection Fade (Object Intersection Transparency) =====
    #ifndef _QUEST_LITE
    #ifdef _INTERSECTION_FADE
    // Skipped in mirrors (no valid depth texture: clip mode could remove the whole
    // object) and for orthographic cameras (screenPos.w is not the view depth there).
    if (_IntersectionFade >= 0.5 && !NataneIsMirror() && !NataneIsOrthoCamera())
    {
        float2 intersectScreenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
        float sceneDepth = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, intersectScreenUV).r);
        float fragDepth = i.screenPos.w;
        float depthDiff = sceneDepth - fragDepth;
        half intersectionFade = saturate(depthDiff / max(_IntersectionFadeDistance, 0.001));

        // Edge highlight at intersection
        if (_IntersectionFadeEdgeWidth > 0.001)
        {
            float edge = 1.0 - smoothstep(0.0, _IntersectionFadeEdgeWidth, depthDiff);
            col.rgb = lerp(col.rgb, _IntersectionFadeEdgeColor.rgb, edge * _IntersectionFadeEdgeColor.a);
        }

        if (_IntersectionFadeMode < 0.5)
        {
            half preIntersectAlpha = col.a;
            col.a *= intersectionFade;
            col.a = ApplyEffectBlendPostAlpha(preIntersectAlpha, col.a, _IntersectionFadeBlend);
        }
        else if (_IntersectionFadeMode < 1.5)
        {
            clip(intersectionFade - 0.001);
        }
        else
        {
            float ditherThreshold = DitheringPattern(i.pos.xy, max(_IntersectionFadeDitherScale, 1.0));
            clip(intersectionFade - ditherThreshold);
        }
    }
    #endif
    #endif // !_QUEST_LITE

    // ===== Distance Fade (Global Alpha) =====
    // このセクションの処理:
    // カメラからの距離に基づいてオブジェクト全体の透明度を制御する。
    // Alpha モード: 距離に応じて滑らかにフェードアウト。
    // Simplify モード: 一定距離でハードカリング（オーバードロー削減）。
    // Dithering モード: Bayer パターンのディザリングでオペーク向けフェード。
    // Note: distanceFade value was already computed early for per-effect fading
    #ifdef _DISTANCE_FADE
        if (_DistanceFadeMode < 0.5)
        {
            // Alpha mode: smooth fade-out with distance.
            half preDistAlpha = col.a;
            col.a *= distanceFade;
            col.a = ApplyEffectBlendPostAlpha(preDistAlpha, col.a, _DistanceFadeBlend);
        }
        else if (_DistanceFadeMode < 1.5)
        {
            // Simplify mode: hard cull at distance for stronger overdraw reduction.
            clip(distanceFade - 0.001);
        }
        else
        {
            // Dithering mode: ordered dithering clip for opaque-friendly fade.
            float ditherThreshold = DitheringPattern(i.pos.xy, max(_DistFadeDitherScale, 1.0));
            clip(distanceFade - ditherThreshold);
        }
    #endif

    // ===== Height Fog (Material-Based Fog) =====
    #ifdef _HEIGHT_FOG
    if (_HeightFog >= 0.5)
    {
        float worldY = i.worldPos.y;
        float heightFactor = saturate((worldY - _HeightFogStart) / (_HeightFogEnd - _HeightFogStart + 0.001));
        if (_HeightFogMode > 0.5)
            heightFactor = 1.0 - exp(-heightFactor * 3.0);
        float fogAmount = (1.0 - heightFactor) * _HeightFogDensity;
        col.rgb = lerp(col.rgb, _HeightFogColor.rgb, fogAmount);
    }
    #endif

    // ===== Depth-based Color Fade (Aerial Perspective per Material) =====
    #if defined(_DEPTH_COLOR_FADE)
    if (_DepthColorFade >= 0.5)
    {
        float camDist = NataneViewerDistance(i.worldPos);   // eye-centred, mirror-aware
        float depthFactor = saturate((camDist - _DepthFadeStart) / max(_DepthFadeEnd - _DepthFadeStart, 0.001));
        // Exponential falloff for more natural aerial perspective
        depthFactor = 1.0 - exp(-depthFactor * 3.0);
        // Desaturate
        half lum = dot(col.rgb, half3(0.299, 0.587, 0.114));
        col.rgb = lerp(col.rgb, half3(lum, lum, lum), depthFactor * _DepthFadeDesaturation);
        // Blend toward atmosphere color
        col.rgb = lerp(col.rgb, _DepthFadeColor.rgb, depthFactor * _DepthFadeIntensity);
    }
    #endif

    // ===== Final Color Blending (Highlight & Shadow Smoothing) =====
    // Apply final smoothing to prevent harsh white/black spots
    // This is applied at the very end before fog for the most natural result
    #ifdef UNITY_PASS_FORWARDBASE
        col.rgb = ApplyFinalColorBlending(col.rgb);
        #if defined(_EMISSION)
            // Re-add the HDR emission overshoot after the LDR clamp so bloom works.
            col.rgb += nataneHdrEmission;
        #endif
    #endif

    // ===== Pixel Art: light/color posterize + palette + dither (post-lighting) =====
    #if defined(_PIXEL_ART)
    if (_PixelArt >= 0.5)
    {
        half natanePxMask = NATANE_SAMPLE_SHARED_R(_PixelArtMask, _MainTex, uv);
        if (natanePxMask > 0.001)
        {
            half3 pxSrc = col.rgb;
            half3 pxWork = col.rgb;
            // Bayer dither breaks the posterization banding before the floor.
            if (_PixelDither > 0.001)
            {
                half pxDith = (NataneBayerThreshold4x4(i.pos.xy, 1.0) - 0.5)
                              / max(_PixelLightSteps, 1.0) * _PixelDither;
                pxWork = saturate(pxWork + pxDith);
            }
            half3 pxQuant = NatanePixelPosterize(pxWork, _PixelLightSteps);
            // Optional palette LUT remap by luminance (ForwardBase only — additive
            // passes must not replace the accumulated base look with palette colors).
            #ifdef UNITY_PASS_FORWARDBASE
            if (_PixelPalette > 0.5)
            {
                half pxLum = dot(pxQuant, half3(0.299, 0.587, 0.114));
                pxQuant = NATANE_SAMPLE_SHARED(_PixelPaletteTex, _MainTex, float2(saturate(pxLum), 0.5)).rgb;
            }
            #endif
            col.rgb = lerp(pxSrc, pxQuant, natanePxMask);
        }
    }
    #endif

    // ===== Ghost appearance (GHOST variant only) =====
    // Fresnel-shaped alpha: the silhouette edge stays visible while the
    // center fades out, plus an additive spectral rim tint.
    #ifdef GHOST_VARIANT
    {
        half ghostNdotV = saturate(dot(normalize(worldNormal), normalize(viewDir)));
        half ghostFresnel = pow(1.0 - ghostNdotV, max(_GhostFresnelPower, 0.001));
        col.a *= lerp(1.0, ghostFresnel, _GhostFresnelAlpha);
        col.rgb += _GhostRimColor.rgb * ghostFresnel * _GhostRimStrength * col.a;
    }
    #endif

    // ===== Hashed / Dithering Alpha =====
    #if defined(_HASHED_ALPHA)
        if (_HashedAlpha >= 0.5)
        {
            // Pixel coordinates straight from SV_POSITION: identical in mono, and not
            // stretched 2x in double-wide stereo like "screenUV * _ScreenParams.xy" was.
            float2 hashedScreenPos = i.pos.xy;
            clip(ApplyHashedAlpha(col.a, StabilizeDitherCoord(hashedScreenPos), i.worldPos.xz, _HashedAlphaScale));
        }
    #elif defined(_DITHERING_ALPHA)
        if (_DitheringAlpha >= 0.5)
        {
            // Pixel coordinates straight from SV_POSITION: identical in mono, and not
            // stretched 2x in double-wide stereo like "screenUV * _ScreenParams.xy" was.
            float2 ditherScreenPos = i.pos.xy;
            clip(ApplyDitheringAlpha(col.a, StabilizeDitherCoord(ditherScreenPos), max(_DitheringAlphaScale, 1.0)));
        }
    #endif

    // ===== Fog =====
    UNITY_APPLY_FOG(i.fogCoord, col);

    return col;
}

#endif // NATANE_TOON_FRAGMENT_INCLUDED
