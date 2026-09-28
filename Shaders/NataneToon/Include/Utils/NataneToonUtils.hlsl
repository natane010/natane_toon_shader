#ifndef NATANE_TOON_UTILS_INCLUDED
#define NATANE_TOON_UTILS_INCLUDED

// ===== NOSAMPLER Function Argument Helpers =====
// Portable type for passing NOSAMPLER textures as function arguments.
// SEPARATE platforms (DX11+): Texture2D, others: sampler2D.
#if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
    #define NATANE_TEX2D_NS_ARG(name) Texture2D name
#else
    #define NATANE_TEX2D_NS_ARG(name) sampler2D name
#endif

// Performance Optimization Macros
#define LUMA_WEIGHTS half3(0.299, 0.587, 0.114)
#define CALC_LUMINANCE(color) dot(color, LUMA_WEIGHTS)

// Common Constants
#define HALF_VALUE 0.5
#define ONE_VALUE 1.0
#define ZERO_VALUE 0.0
#define EPSILON 0.001
#define WHITE_COLOR half3(1, 1, 1)

// Utility Functions

// VRChat Mirror Detection Helpers
// _VRChatMirrorMode: 0=Normal, 1=Mirror(VR), 2=Mirror(Desktop)
// Both 1 and 2 indicate mirror rendering where the view matrix X-axis is flipped.
// Reference: https://creators.vrchat.com/worlds/udon/vrc-graphics/vrchat-shader-globals/
//
// Declared here so that any file including Utils alone (e.g. FurShell)
// gets the VRChat globals without needing the full NataneToonInput.hlsl.
// NataneToonInput.hlsl guards with the same variable names, so no redefinition occurs.
#ifndef NATANE_VRCHAT_GLOBALS_DECLARED
#define NATANE_VRCHAT_GLOBALS_DECLARED
float _VRChatMirrorMode;
float _VRChatCameraMode;
#endif

// Returns true if currently rendering in a VRChat mirror
float NataneIsMirror()
{
    return _VRChatMirrorMode > 0.5;
}

// Returns -1.0 in mirror (X axis flipped), 1.0 in normal view.
// Used to compensate all view-space calculations that use UNITY_MATRIX_V.
float NataneMirrorSign()
{
    return _VRChatMirrorMode > 0.5 ? -1.0 : 1.0;
}

// Returns true if currently rendering for a VRChat camera
// _VRChatCameraMode: 0=Normal, 1=VR handheld camera, 2=Desktop camera, 3=Screenshot
float NataneIsCamera()
{
    return _VRChatCameraMode > 0.5;
}

// Calculate MatCap UV coordinates from world normal
// MatCap uses view-space normals to create sphere-mapped effects
float2 CalculateMatCapUV(float3 worldNormal, float3 viewDir)
{
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    viewNormal.x *= NataneMirrorSign(); // Compensate VRChat mirror X-axis flip
    float2 matcapUV = viewNormal.xy * 0.5 + 0.5;
    return matcapUV;
}

// RGB to HSV conversion
float3 RGBtoHSV(float3 rgb)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

// HSV to RGB conversion
float3 HSVtoRGB(float3 hsv)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
    return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
}

// Apply Hue Shift to RGB color
// shift: 0-1 range (0 = no shift, 1 = full rotation)
float3 ApplyHueShift(float3 rgb, float shift)
{
    float3 hsv = RGBtoHSV(rgb);
    hsv.x = frac(hsv.x + shift); // Wrap hue around [0, 1]
    return HSVtoRGB(hsv);
}

// Apply HSV adjustments to RGB color
// hueShift: -0.5 to 0.5 range (0 = no shift)
// saturation: 0-2 range (1 = no change, 0 = grayscale, 2 = double saturation)
// value: 0-2 range (1 = no change, 0 = black, 2 = double brightness)
// Optimized: Skip conversion if using default values
half3 ApplyHSVAdjustment(half3 rgb, half hueShift, half saturation, half value)
{
    // Optimization: Skip expensive HSV conversion if using default values
    if (abs(hueShift) < 0.001 && abs(saturation - 1.0) < 0.001 && abs(value - 1.0) < 0.001)
    {
        return rgb;
    }

    // Convert to HSV
    half3 hsv = RGBtoHSV(rgb);

    // Apply adjustments
    hsv.x = frac(hsv.x + hueShift); // Hue shift with wrapping
    hsv.y = saturate(hsv.y * saturation); // Saturation multiply
    hsv.z = saturate(hsv.z * value); // Value/brightness multiply

    // Convert back to RGB
    return HSVtoRGB(hsv);
}

// Calculate Dissolve alpha and edge glow
// returns: x = alpha (for clipping), y = edge glow intensity
#if defined(_DISSOLVE)

// Core dissolve calculation from pre-computed noise value
float2 CalculateDissolveFromNoise(float dissolveNoise, float dissolveAmount, float edgeWidth)
{
    float dissolveAlpha = dissolveNoise - dissolveAmount;
    float safeEdgeWidth = max(edgeWidth, EPSILON);
    float edgeFactor = saturate(1.0 - (dissolveAlpha / safeEdgeWidth));
    float inEdgeRange = step(EPSILON, dissolveAlpha) * (1.0 - step(edgeWidth, dissolveAlpha));
    float edgeGlow = edgeFactor * inEdgeRange;
    return float2(dissolveAlpha, edgeGlow);
}

// Original function delegates to new core (backward compatible)
float2 CalculateDissolve(float2 uv, float dissolveAmount, float edgeWidth)
{
    float dissolveNoise = NATANE_SAMPLE_REPEAT(_DissolveTex, uv).r;
    return CalculateDissolveFromNoise(dissolveNoise, dissolveAmount, edgeWidth);
}

#endif // _DISSOLVE

// Parallax Occlusion Mapping
// Creates the illusion of depth by offsetting texture coordinates based on height map
// Returns adjusted UV coordinates
#if defined(_PARALLAX)
float NataneSampleParallaxHeight(float2 uv, float2 uvDx, float2 uvDy)
{
    #if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
        return _ParallaxMap.SampleGrad(sampler_linear_repeat, uv, uvDx, uvDy).r;
    #else
        return tex2Dgrad(_ParallaxMap, uv, uvDx, uvDy).r;
    #endif
}

float2 ParallaxMapping(float2 uv, float3 viewDirTangent)
{
    // Calculate number of layers based on view angle
    // More layers when viewing at steep angles for better quality
    float numLayers = lerp(_ParallaxMaxSamples, _ParallaxMinSamples, abs(dot(float3(0, 0, 1), viewDirTangent)));

    // Calculate the size of each layer
    float layerDepth = 1.0 / numLayers;

    // Depth of current layer
    float currentLayerDepth = 0.0;

    // Calculate UV offset per layer
    float2 deltaUV = viewDirTangent.xy * _ParallaxScale / (max(abs(viewDirTangent.z), 0.001) * numLayers);

    // Initial values
    float2 currentUV = uv;
    float2 uvDx = ddx(uv);
    float2 uvDy = ddy(uv);
    float currentDepthMapValue = NataneSampleParallaxHeight(currentUV, uvDx, uvDy);

    // Parallax Occlusion Mapping loop
    [loop]
    for (int i = 0; i < (int)numLayers && currentLayerDepth < currentDepthMapValue; i++)
    {
        // Shift UV along direction of view
        currentUV -= deltaUV;

        // Get depth value at current UV
        currentDepthMapValue = NataneSampleParallaxHeight(currentUV, uvDx, uvDy);

        // Get depth of next layer
        currentLayerDepth += layerDepth;
    }

    // Interpolation for smoother result (steep parallax mapping)
    float2 prevUV = currentUV + deltaUV;
    float afterDepth = currentDepthMapValue - currentLayerDepth;
    float beforeDepth = NataneSampleParallaxHeight(prevUV, uvDx, uvDy) - currentLayerDepth + layerDepth;

    // Interpolation weight (with zero-division protection)
    float depthDiff = afterDepth - beforeDepth;
    float weight = saturate(afterDepth / max(abs(depthDiff), 0.0001));

    // Final UV coordinates
    float2 finalUV = lerp(currentUV, prevUV, weight);

    return finalUV;
}

// Calculate tangent space view direction for parallax mapping
// Requires world position, world tangent, world binormal, and world normal
float3 CalculateTangentViewDir(float3 worldPos, float3 worldTangent, float3 worldBinormal, float3 worldNormal)
{
    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);

    // Create TBN matrix (tangent, binormal, normal)
    float3x3 TBN = float3x3(worldTangent, worldBinormal, worldNormal);

    // Transform view direction to tangent space
    float3 tangentViewDir = mul(TBN, viewDir);

    return tangentViewDir;
}
#endif // _PARALLAX

// ===== Mask Smoothing Functions =====

// Smooth mask transition for better blending
// Applies feathering to mask edges to create smooth transitions
// featherAmount: 0-1 range, controls the amount of edge softening (default: 0.1)
float SmoothMask(float mask, float featherAmount)
{
    // Apply smoothstep for smoother transitions
    // The feather creates a gradient zone around the mask edges
    float feather = saturate(featherAmount * 0.5); // Scale to reasonable range
    return smoothstep(feather, 1.0 - feather, mask);
}

// Apply mask with automatic edge smoothing
// Creates natural transitions between masked and unmasked areas
float ApplySoftMask(float mask)
{
    // Default feather amount for smooth transitions
    const float DEFAULT_FEATHER = 0.15; // 15% edge softening
    return SmoothMask(mask, DEFAULT_FEATHER);
}

// Blend with soft edges (for effect application)
// Blends base and effect colors with smooth mask transition
float3 BlendWithSoftMask(float3 baseColor, float3 effectColor, float mask)
{
    // Apply soft masking for smooth transitions
    float softMask = ApplySoftMask(mask);
    return lerp(baseColor, effectColor, softMask);
}

// ===== Tone Mapping Functions =====

// Reinhard tone mapping - smooth compression of bright values
// Provides natural rolloff for highlights without hard clipping
half3 ReinhardToneMapping(half3 color, half whitePoint)
{
    // Extended Reinhard with adjustable white point
    half luminance = CALC_LUMINANCE(color);
    half mappedLuminance = luminance * (1.0 + luminance / (whitePoint * whitePoint)) / (1.0 + luminance);

    // Preserve color ratios while adjusting luminance
    half3 result = color * (mappedLuminance / (luminance + 0.01));
    return result;
}

// Filmic tone mapping (ACES approximation)
// Provides cinematic look with natural highlight compression
half3 FilmicToneMapping(half3 color)
{
    // ACES approximation by Krzysztof Narkowicz
    const half a = 2.51;
    const half b = 0.03;
    const half c = 2.43;
    const half d = 0.59;
    const half e = 0.14;

    half3 result = saturate((color * (a * color + b)) / (color * (c * color + d) + e));
    return result;
}

// Smooth shoulder tone mapping
// Custom function for smooth highlight rolloff with configurable shoulder strength
half3 SmoothShoulderToneMapping(half3 color, half shoulderStrength)
{
    if (shoulderStrength < 0.001) return color;

    half luminance = CALC_LUMINANCE(color);

    // Apply smooth shoulder curve to bright areas
    const half shoulderStart = 0.6; // Start compressing at 60% brightness
    if (luminance > shoulderStart)
    {
        half excess = luminance - shoulderStart;
        const half maxExcess = 0.4; // 1.0 - 0.6 = 0.4 (compile-time constant)

        // Smooth compression curve using smoothstep
        half compressionFactor = smoothstep(0.0, maxExcess, excess);
        half compressed = shoulderStart + excess * (1.0 - compressionFactor * shoulderStrength * 0.7);

        // Apply luminance adjustment while preserving color
        color = color * (compressed / (luminance + 0.01));
    }

    return color;
}

// ===== Final Color Blending Functions =====

// Apply final highlight blend (white smoothing)
// Smooths bright areas to prevent harsh white spots
half3 ApplyFinalHighlightBlend(half3 color, half blendAmount, half threshold)
{
    if (blendAmount < 0.001) return color;

    // Calculate luminance (optimized)
    half luminance = CALC_LUMINANCE(color);

    // Only process highlights above threshold
    if (luminance > threshold)
    {
        // Calculate how much above threshold (protected against division by zero)
        // Formula: (luminance - threshold) / (1.0 - threshold)
        // Maps luminance range [threshold, 1.0] to [0.0, 1.0]
        // Example: threshold=0.7, luminance=0.85 → (0.85-0.7)/(1.0-0.7) = 0.5
        // This creates a normalized 0-1 range for highlight strength above the threshold
        half highlightFactor = saturate((luminance - threshold) / max(1.0 - threshold, 0.01));

        // Calculate blend target (slightly desaturated and softened)
        half3 blendTarget = lerp(color, luminance, 0.3); // 30% desaturation

        // Apply smoothing with feathering
        half smoothFactor = smoothstep(0.0, 1.0, highlightFactor) * blendAmount;
        color = lerp(color, blendTarget, smoothFactor);
    }

    return color;
}

// Apply final shadow blend (dark smoothing)
// Smooths dark areas to prevent harsh black spots
half3 ApplyFinalShadowBlend(half3 color, half blendAmount, half threshold)
{
    if (blendAmount < 0.001) return color;

    // Calculate luminance (optimized)
    half luminance = CALC_LUMINANCE(color);

    // Only process shadows below threshold
    if (luminance < threshold)
    {
        // Calculate how much below threshold
        // Formula: (threshold - luminance) / threshold
        // Maps luminance range [0.0, threshold] to [1.0, 0.0] (inverted)
        // Example: threshold=0.3, luminance=0.1 → (0.3-0.1)/0.3 = 0.667
        // Note: This formula is intentionally different from highlight calculation
        // to create asymmetric smoothing behavior (shadows are processed differently than highlights)
        half shadowFactor = saturate((threshold - luminance) / max(threshold, 0.01));

        // Calculate blend target (slightly lifted and softened)
        half3 liftedColor = saturate(color + 0.05); // Lift shadows slightly
        half3 blendTarget = lerp(liftedColor, luminance, 0.2); // 20% towards gray

        // Apply smoothing with feathering
        half smoothFactor = smoothstep(0.0, 1.0, shadowFactor) * blendAmount;
        color = lerp(color, blendTarget, smoothFactor);
    }

    return color;
}

// Apply both highlight and shadow blending with tone mapping
// This is the main function to call for final color processing
// Requires CBUFFER variables from NataneToonInput.hlsl — excluded in standalone passes (e.g. FurShell)
#ifndef NATANE_UTILS_STANDALONE
half3 ApplyFinalColorBlending(half3 color)
{
    // Step 1: Apply smooth shoulder tone mapping for natural highlight compression
    // This prevents harsh white clipping while maintaining color vibrancy
    if (_FinalHighlightBlend > 0.001)
    {
        color = SmoothShoulderToneMapping(color, _FinalHighlightBlend);
    }

    // Step 2: Apply highlight blend for additional smoothing
    // This further softens bright areas above threshold
    color = ApplyFinalHighlightBlend(color, _FinalHighlightBlend * 0.5, _HighlightThreshold);

    // Step 3: Apply shadow blend to prevent black crushing
    color = ApplyFinalShadowBlend(color, _FinalShadowBlend, _ShadowThreshold);

    // Step 4: Gentle clamp to prevent hard cutoff - preserve values slightly above 1.0
    // This allows some headroom for natural brightness while preventing extreme values
    return min(color, 1.05);
}
#endif

// Compress bright diffuse lighting before it turns into flat white.
// This is intentionally conservative so VRChat multi-light worlds keep color separation.
half3 CompressLightingForSafeRange(half3 lightingColor, half shoulderStart, half maxLuminance)
{
    half3 safeColor = max(lightingColor, 0.0);
    half safeLum = CALC_LUMINANCE(safeColor);

    if (safeLum <= shoulderStart)
    {
        return safeColor;
    }

    half overLum = safeLum - shoulderStart;
    half compressedLum = shoulderStart + overLum / (1.0 + overLum * 2.0);
    compressedLum = min(compressedLum, maxLuminance);
    return safeColor * (compressedLum / max(safeLum, 0.0001));
}

// Safe diffuse composition for VRChat worlds with strong SH, Light Volumes, or many point lights.
// Indirect remains visible, but direct/additional light is compressed before it can blow out albedo.
half3 CombineDiffuseLightingSafe(half3 indirectLight, half3 directLight, half3 additionalLight, half3 ambientLight)
{
    half3 safeIndirect = CompressLightingForSafeRange(max(indirectLight, 0.0), 0.75, 0.95);
    half3 safeDirect = max(directLight, 0.0);
    half3 safeAdditional = max(additionalLight, 0.0);
    half3 safeAmbient = max(ambientLight, 0.0);

    half directLum = CALC_LUMINANCE(safeDirect);
    half additionalWeight = lerp(0.4, 1.0, saturate(1.0 - directLum * 0.65));
    half3 combinedDirect = safeDirect + safeAdditional * additionalWeight;
    combinedDirect = CompressLightingForSafeRange(combinedDirect, 0.8, 1.0);

    half3 total = max(safeIndirect, combinedDirect);
    total += safeAmbient * 0.5;
    return CompressLightingForSafeRange(total, 0.95, 1.08);
}

// Safe additive blending - prevents harsh white spots while preserving effect colors
// Balanced compression: allows effect colors to show through on bright surfaces
half3 SafeAdditiveBlend(half3 baseColor, half3 additiveColor, half strength)
{
    // Calculate current luminance (using optimized macro)
    half baseLum = CALC_LUMINANCE(baseColor);

    // Gentle compression: reduce strength as brightness increases
    // but keep enough headroom for effect colors to remain visible
    half compressionFactor = saturate(1.0 - baseLum * 0.5);
    half darknessFactor = smoothstep(0.0, 0.05, baseLum);
    half finalStrength = strength * max(compressionFactor * darknessFactor, 0.08);

    // For dark colored surfaces, lightly tint additive towards base hue
    half3 tintedAdditive = additiveColor;
    if (baseLum < 0.3 && baseLum > 0.01)
    {
        half3 baseDir = normalize(baseColor + 0.01);
        half tintAmount = (0.3 - baseLum) * 1.67; // 1.67 = 1/0.6 optimization
        tintedAdditive = lerp(additiveColor, additiveColor * baseDir * 2.0, tintAmount * 0.2);
    }

    // Apply additive with strength control
    half3 result = baseColor + tintedAdditive * finalStrength;

    // Hue-preserving soft clamp: compress luminance while keeping color direction
    half resultLum = CALC_LUMINANCE(result);
    if (resultLum > 0.95)
    {
        half compression = smoothstep(0.95, 1.2, resultLum);
        half3 resultDir = result / max(resultLum, 0.01);
        half clampedLum = lerp(resultLum, 1.0, compression * 0.65);
        result = resultDir * clampedLum;
    }

    return result;
}

// Fast version of SafeAdditiveBlend (branchless)
// Use for secondary effects (Rim Light 2, Glitter, Iridescence) where full quality is not critical
// Performance: ~60% faster than full version (no branching, no soft clamp)
half3 SafeAdditiveBlendFast(half3 baseColor, half3 additiveColor, half strength)
{
    half baseLum = CALC_LUMINANCE(baseColor);
    half compression = saturate(1.0 - baseLum * 0.5);
    return baseColor + additiveColor * strength * max(compression, 0.08);
}

// ===== Matte Material Quality =====
// Instead of simply suppressing effects, transforms them to look like light on a matte surface.
// Matte surfaces scatter light: effects become desaturated and tinted by the surface color,
// but remain clearly visible — matte changes quality, not visibility.
half3 ApplyMatteQuality(half3 effect, half3 baseColor, half matteAmount)
{
    matteAmount = saturate(matteAmount);

    // 1. Desaturate: matte scattering reduces spectral separation.
    half effectLum = CALC_LUMINANCE(max(effect, 0.0));
    effect = lerp(effect, effectLum.xxx, matteAmount * 0.7);

    // 2. Surface tinting: diffuse-like coloration from the base surface.
    half3 tintBase = saturate(baseColor + 0.35);
    effect = lerp(effect, effect * tintBase, matteAmount * 0.45);

    // 3. Peak compression: reduce shiny spikes while preserving broad response.
    half lumAfter = CALC_LUMINANCE(max(effect, 0.0));
    half peakCompression = rcp(1.0 + lumAfter * matteAmount * 1.25);

    // 4. Keep a minimum presence so effects stay visible even at full matte.
    half mattePresence = lerp(1.0, 0.55, matteAmount);
    half matteSoften = lerp(1.0, 0.9, matteAmount);
    return effect * peakCompression * matteSoften * mattePresence;
}

// ===== Blend Mode Functions for Makeup Textures =====
// Returns blended color based on blend mode

// Overlay blend mode
float3 BlendOverlay(float3 base, float3 blend)
{
    float3 result;
    result.r = base.r < 0.5 ? (2.0 * base.r * blend.r) : (1.0 - 2.0 * (1.0 - base.r) * (1.0 - blend.r));
    result.g = base.g < 0.5 ? (2.0 * base.g * blend.g) : (1.0 - 2.0 * (1.0 - base.g) * (1.0 - blend.g));
    result.b = base.b < 0.5 ? (2.0 * base.b * blend.b) : (1.0 - 2.0 * (1.0 - base.b) * (1.0 - blend.b));
    return result;
}

// Screen blend mode
float3 BlendScreen(float3 base, float3 blend)
{
    return 1.0 - (1.0 - base) * (1.0 - blend);
}

// Apply blend mode to base color with blend texture (with soft masking)
// blendMode: 0=Add, 1=Multiply, 2=Overlay, 3=Screen
// intensity: blend strength (already includes mask value)
float3 ApplyBlendMode(float3 baseColor, float3 blendTexture, float3 blendColor, float intensity, float blendMode)
{
    // Apply soft masking to intensity for smoother transitions
    float softIntensity = ApplySoftMask(intensity);

    float3 blendResult = blendTexture * blendColor;

    // Optimized: Calculate all blend modes and select using lerp (no branching)
    float3 addResult = baseColor + blendResult * softIntensity;
    float3 multiplyResult = baseColor * lerp(WHITE_COLOR, blendResult, softIntensity);
    float3 overlayResult = lerp(baseColor, BlendOverlay(baseColor, blendResult), softIntensity);
    float3 screenResult = lerp(baseColor, BlendScreen(baseColor, blendResult), softIntensity);

    // Select blend mode using step and lerp
    float isMultiply = step(HALF_VALUE, blendMode) * step(blendMode, 1.5);
    float isOverlay = step(1.5, blendMode) * step(blendMode, 2.5);
    float isScreen = step(2.5, blendMode);

    float3 result = lerp(addResult, multiplyResult, isMultiply);
    result = lerp(result, overlayResult, isOverlay);
    result = lerp(result, screenResult, isScreen);

    return result;
}

// Apply makeup/detail texture with HSV adjustment and blend mode
// Consolidates 2nd-5th texture blending logic
half3 ApplyMakeupTexture(
    half3 baseColor,
    half4 texSample,
    float externalMask,
    float hueShift,
    float saturation,
    float value,
    float intensity,
    float blendMode)
{
    float texMask = texSample.a * externalMask;

    // Apply HSV adjustments (skip if default values for performance)
    half3 texAdjusted = ApplyHSVAdjustment(texSample.rgb, hueShift, saturation, value);

    // Apply blend mode with combined mask
    return ApplyBlendMode(baseColor, texAdjusted, WHITE_COLOR, intensity * texMask, blendMode);
}

// ===== Screen-Tone Overlay Functions =====
#if defined(_SCREEN_TONE)

// Calculate screen-tone dot pattern using Bayer dithering
half CalculateScreenTonePattern(float2 screenPos, float scale, float threshold)
{
    float2 scaledPos = screenPos / max(scale, 1.0);

    static const float bayer[16] = {
        0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
        12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
        3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
        15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
    };

    int2 pos = int2(fmod(scaledPos.x, 4), fmod(scaledPos.y, 4));
    float bayerValue = bayer[pos.y * 4 + pos.x];

    return step(bayerValue, threshold);
}

// Apply screen-tone overlay to base color
half3 ApplyScreenTone(half3 baseColor, float2 screenPos, half maskValue)
{
    half pattern = CalculateScreenTonePattern(screenPos, _ScreenToneScale, _ScreenToneThreshold);
    half effectAmount = pattern * maskValue;
    return lerp(baseColor, _ScreenToneColor.rgb, effectAmount);
}
#endif // _SCREEN_TONE

// ===== Effect Blend Post-Processing =====
// 全エフェクトの統一的なブレンド制御
// base: エフェクト適用前の色
// effectResult: エフェクト適用後の色
// blend: 0-1 ブレンド量（0=元のまま、1=エフェクト完全適用）
// blendMode: 0=Normal, 1=Soft(なじませ), 2=Screen, 3=Overlay
half3 ApplyEffectBlendPost(half3 base, half3 effectResult, float blend, float blendMode)
{
    if (blend >= 0.999 && blendMode < 0.5) return effectResult;
    if (blend <= 0.001) return base;

    half3 blended;

    if (blendMode < 0.5) // Normal（通常）
    {
        blended = effectResult;
    }
    else if (blendMode < 1.5) // Soft（なじませ）
    {
        half baseLuma = dot(base, half3(0.299, 0.587, 0.114));
        blended = lerp(base, effectResult, saturate(baseLuma * 2.0));
    }
    else if (blendMode < 2.5) // Screen（スクリーン）
    {
        blended = BlendScreen(base, saturate(effectResult));
    }
    else // Overlay（オーバーレイ）
    {
        blended = BlendOverlay(base, saturate(effectResult));
    }

    return lerp(base, saturate(blended), blend);
}

// Alpha版（透明度エフェクト用）
half ApplyEffectBlendPostAlpha(half baseAlpha, half effectAlpha, float blend)
{
    return lerp(baseAlpha, effectAlpha, blend);
}

// ===== Per-Effect Texture Blur =====
// ddx/ddy ベースの 5点クロスパターンボックスブラー
// blur=0 で早期リターン（追加コストなし）
half4 SampleTex2DBlur(sampler2D tex, float2 uv, float blur)
{
    half4 center = tex2D(tex, uv);
    if (blur <= 0.001) return center;

    // ddx/ddy でスクリーン空間のテクセルサイズを自動取得
    float2 dx = ddx(uv) * blur * 4.0;
    float2 dy = ddy(uv) * blur * 4.0;

    // 5点クロスパターン（中心+上下左右）
    half4 col = center * 0.4;
    col += tex2D(tex, uv + dx) * 0.15;
    col += tex2D(tex, uv - dx) * 0.15;
    col += tex2D(tex, uv + dy) * 0.15;
    col += tex2D(tex, uv - dy) * 0.15;

    return col;
}

// half3 版（RGB のみ）
half3 SampleTex2DBlur3(sampler2D tex, float2 uv, float blur)
{
    return SampleTex2DBlur(tex, uv, blur).rgb;
}

// half 版（単一チャンネル、マスク/AO用）
half SampleTex2DBlur1(sampler2D tex, float2 uv, float blur)
{
    return SampleTex2DBlur(tex, uv, blur).r;
}

// NOSAMPLER overloads for Repeat sampling
// Requires sampler_linear_repeat from NataneToonInput.hlsl — excluded in standalone passes (e.g. FurShell)
#ifndef NATANE_UTILS_STANDALONE

#if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
half4 SampleTex2DBlurRepeat(Texture2D tex, float2 uv, float blur)
{
    half4 center = tex.Sample(sampler_linear_repeat, uv);
    if (blur <= 0.001) return center;
    float2 dx = ddx(uv) * blur * 4.0;
    float2 dy = ddy(uv) * blur * 4.0;
    half4 col = center * 0.4;
    col += tex.Sample(sampler_linear_repeat, uv + dx) * 0.15;
    col += tex.Sample(sampler_linear_repeat, uv - dx) * 0.15;
    col += tex.Sample(sampler_linear_repeat, uv + dy) * 0.15;
    col += tex.Sample(sampler_linear_repeat, uv - dy) * 0.15;
    return col;
}
half3 SampleTex2DBlur3Repeat(Texture2D tex, float2 uv, float blur)
{
    return SampleTex2DBlurRepeat(tex, uv, blur).rgb;
}
#else
#define SampleTex2DBlurRepeat(tex, uv, blur) SampleTex2DBlur(tex, uv, blur)
#define SampleTex2DBlur3Repeat(tex, uv, blur) SampleTex2DBlur3(tex, uv, blur)
#endif

#if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
half4 SampleTex2DBlurShared(Texture2D tex, SamplerState sharedSampler, float2 uv, float blur)
{
    half4 center = tex.Sample(sharedSampler, uv);
    if (blur <= 0.001) return center;

    float2 dx = ddx(uv) * blur * 4.0;
    float2 dy = ddy(uv) * blur * 4.0;

    half4 col = center * 0.4;
    col += tex.Sample(sharedSampler, uv + dx) * 0.15;
    col += tex.Sample(sharedSampler, uv - dx) * 0.15;
    col += tex.Sample(sharedSampler, uv + dy) * 0.15;
    col += tex.Sample(sharedSampler, uv - dy) * 0.15;

    return col;
}
#endif

#if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
    #define NATANE_SAMPLE_SHARED_BLUR(tex, samplerTex, coord, blur) SampleTex2DBlurShared(tex, sampler_linear_repeat, coord, blur)
#else
    #define NATANE_SAMPLE_SHARED_BLUR(tex, samplerTex, coord, blur) SampleTex2DBlur(tex, coord, blur)
#endif

#define NATANE_SAMPLE_SHARED_BLUR_R(tex, samplerTex, coord, blur) NATANE_SAMPLE_SHARED_BLUR(tex, samplerTex, coord, blur).r

#endif // !NATANE_UTILS_STANDALONE

// ===== UV Animation Functions =====

// Animate UV coordinates with scroll and rotation
// Returns transformed UV coordinates
float2 AnimateUV(float2 uv, float2 scrollSpeed, float rotateSpeed)
{
    float2 animatedUV = uv;

    // Apply scrolling
    // NOTE: Branching kept to avoid unnecessary computation when scrollSpeed is zero.
    // Uses squared length to avoid sqrt.
    if (dot(scrollSpeed, scrollSpeed) > (EPSILON * EPSILON))
    {
        animatedUV += scrollSpeed * _Time.y;
    }

    // Apply rotation
    // NOTE: Branching kept to avoid expensive sin/cos computation when rotation is zero
    if (abs(rotateSpeed) > EPSILON)
    {
        // Rotate around UV center (0.5, 0.5)
        float2 centerUV = animatedUV - float2(0.5, 0.5);
        float angle = rotateSpeed * _Time.y;
        float s = sin(angle);
        float c = cos(angle);

        // Rotation matrix
        float2 rotatedUV;
        rotatedUV.x = centerUV.x * c - centerUV.y * s;
        rotatedUV.y = centerUV.x * s + centerUV.y * c;

        animatedUV = rotatedUV + float2(0.5, 0.5);
    }

    return animatedUV;
}

// UV アニメーション判定付きヘルパー
// スクロール速度・回転速度が閾値以上の場合のみアニメーションを適用
float2 AnimateUVIfNeeded(float2 baseUV, float2 scrollSpeed, float rotateSpeed)
{
    if (dot(scrollSpeed, scrollSpeed) > (EPSILON * EPSILON) || abs(rotateSpeed) > EPSILON)
        return AnimateUV(baseUV, scrollSpeed, rotateSpeed);
    return baseUV;
}

// ===== GrabPass Texture Declaration =====
// Shared by: _REFRACTION, _SOFT_FILTER, _KUWAHARA_FILTER, _COLOR_BLEEDING, _CHROMATIC_ABERRATION
// Uses "_nataneBackgroundTexture" named GrabPass (shared with lilToon for cache efficiency)
#if defined(_REFRACTION) || defined(_SOFT_FILTER) || defined(_KUWAHARA_FILTER) || defined(_COLOR_BLEEDING) || defined(_CHROMATIC_ABERRATION)

// Stereo-aware GrabPass texture declaration for VR Single Pass Instanced
UNITY_DECLARE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture);
float4 _nataneBackgroundTexture_TexelSize;

#endif // GrabPass texture

// ===== Refraction Functions =====
#if defined(_REFRACTION)

// Apply refraction distortion to screen UV
// Returns distorted UV for sampling GrabTexture
float2 ApplyRefractionDistortion(float2 screenUV, float3 worldNormal, float3 viewDir, float intensity, float refractionIndex, float blur)
{
    // Calculate refraction using Snell's law
    // IOR ratio: from air (1.0) to material (refractionIndex)
    float iorRatio = 1.0 / refractionIndex;

    // Refract the view direction through the surface
    float3 refractDir = refract(-viewDir, worldNormal, iorRatio);

    // If total internal reflection occurs, use reflection instead
    if (dot(refractDir, refractDir) < 0.0001)
    {
        refractDir = reflect(-viewDir, worldNormal);
    }

    // Calculate distortion offset based on refracted direction
    // Project refraction direction to screen space
    float2 distortion = refractDir.xy * intensity * 0.1;

    // Apply distortion to UV
    float2 distortedUV = screenUV + distortion;

    // Keep UV in valid range [0, 1]
    distortedUV = saturate(distortedUV);

    return distortedUV;
}

// Sample GrabTexture with optional blur
// Optimized: 5 samples (center + cross pattern) instead of 9 samples (3x3)
// Performance: 45% reduction in texture samples
half3 SampleGrabTextureWithBlur(float2 uv, float blurAmount)
{
    // NOTE: Early exit optimization - avoid 4 extra texture samples when blur is disabled
    if (blurAmount < EPSILON)
    {
        // No blur, single sample (stereo-aware for VR SPI)
        return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, uv).rgb;
    }

    // Optimized 5-sample cross blur: center + 4 directions
    // Quality/Performance balance for VR
    float blurRadius = blurAmount * 0.01;
    float2 texelSize = blurRadius * _nataneBackgroundTexture_TexelSize.xy;

    // Center sample with higher weight (stereo-aware for VR SPI)
    half3 color = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, uv).rgb * 0.4;

    // Cross pattern (up, down, left, right)
    color += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, uv + float2(texelSize.x, 0)).rgb * 0.15;
    color += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, uv + float2(-texelSize.x, 0)).rgb * 0.15;
    color += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, uv + float2(0, texelSize.y)).rgb * 0.15;
    color += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, uv + float2(0, -texelSize.y)).rgb * 0.15;

    return color;
}
#endif // _REFRACTION

// ===== AudioLink Functions =====

// Sample AudioLink audio reactive data
// band: 0=Bass, 1=Low Mid, 2=High Mid, 3=Treble
// Returns 0-1 value representing audio intensity
float SampleAudioLink(int band)
{
    #ifdef _AUDIOLINK
        // AudioLink texture layout (128x64): ALPASS_AUDIOLINK = uint2(0, 0), size 128x4.
        // Rows 0..3 are the four bands (Bass, Low Mid, High Mid, Treble);
        // the X axis is the history buffer (x = 0 is the current sample).
        // Sampling texel (0, band) matches AudioLinkData(ALPASS_AUDIOLINK + int2(0, band)).
        float2 audioUV = float2(0.5 / 128.0, (float(clamp(band, 0, 3)) + 0.5) / 64.0);

        // Sample AudioLink texture (mip 0, no filtering across rows)
        float audioValue = tex2Dlod(_AudioTexture, float4(audioUV, 0, 0)).r;
        return saturate(audioValue);
    #else
        return 0.0;
    #endif
}

// Sample AudioLink Chronotensity (time-based intensity)
// band: 0=Bass, 1=Low Mid, 2=High Mid, 3=Treble
// mode: 0=MotionIncrease, 1=MotionSpeed, 2=SelfIntensity, 3=ColorChord
float SampleAudioLinkChronotensity(int band, int mode)
{
    #ifdef _AUDIOLINK_CHRONOTENSITY
        // AudioLink Chronotensity data layout (standard 128x64 texture):
        // ALPASS_CHRONOTENSITY = uint2(16, 28), size 8x4.
        // Columns 16..23 are the chronotensity modes, rows 28..31 are the four bands.
        float2 chronoUV = float2(
            (16.0 + float(clamp(mode, 0, 7)) + 0.5) / 128.0,
            (28.0 + float(clamp(band, 0, 3)) + 0.5) / 64.0
        );
        return saturate(tex2Dlod(_AudioTexture, float4(chronoUV, 0, 0)).r);
    #else
        return 0.0;
    #endif
}

// Convenience overload: sample chronotensity for a single band (MotionIncrease mode)
float SampleAudioLinkChronotensity(int band)
{
    return SampleAudioLinkChronotensity(band, 0);
}

// ===== Distance Fade Functions =====
#if defined(_DISTANCE_FADE)

// Calculate distance fade alpha
// Returns 0-1 fade value based on distance
float CalculateDistanceFade(float3 worldPos, float fadeStart, float fadeEnd)
{
    float distance = length(_WorldSpaceCameraPos - worldPos);
    float fade = saturate((distance - fadeStart) / max(fadeEnd - fadeStart, 0.01));
    return 1.0 - fade; // Invert so 1 = visible, 0 = faded
}

#endif // _DISTANCE_FADE

// ===== Height Fade Functions =====
#if defined(_HEIGHT_FADE)

float CalculateHeightFade(float3 worldPos, float fadeStart, float fadeEnd, float axis, float space, float invert)
{
    float height;
    if (space < 0.5)
    {
        float3 localPos = mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz;
        height = axis < 0.5 ? localPos.x : (axis < 1.5 ? localPos.y : localPos.z);
    }
    else
    {
        height = axis < 0.5 ? worldPos.x : (axis < 1.5 ? worldPos.y : worldPos.z);
    }
    float fade = saturate((height - fadeStart) / max(fadeEnd - fadeStart, 0.01));
    return invert > 0.5 ? 1.0 - fade : fade;
}

#endif // _HEIGHT_FADE

// ===== Gradient Base Color Functions =====
#if defined(_GRADIENT_BASE_COLOR)

half3 CalculateGradientColor(float3 worldPos, half3 topColor, half3 bottomColor,
    float axis, float space, float gradStart, float gradEnd)
{
    float3 pos = space < 0.5
        ? mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz
        : worldPos;
    float axisVal = axis < 0.5 ? pos.x : (axis < 1.5 ? pos.y : pos.z);
    float t = saturate((axisVal - gradStart) / max(gradEnd - gradStart, 0.01));
    return lerp(bottomColor, topColor, t);
}

#endif // _GRADIENT_BASE_COLOR

// ===== Vertex Animation Functions =====
#if defined(_VERTEX_ANIMATION)

// Calculate vertex offset for animation
// type: 0=Wave, 1=Breath, 2=Wind, 3=Pulse
float3 CalculateVertexAnimation(float3 worldPos, float3 worldNormal, float2 uv, float animType, float speed, float strength, float frequency)
{
    float time = _Time.y * speed;
    float3 offset = float3(0, 0, 0);

    if (animType < 0.5) // Wave
    {
        float wave = sin(worldPos.x * frequency + time) * cos(worldPos.z * frequency + time * 0.5);
        offset = worldNormal * wave * strength;
    }
    else if (animType < 1.5) // Breath
    {
        float breath = sin(time * frequency) * 0.5 + 0.5;
        offset = worldNormal * breath * strength;
    }
    else if (animType < 2.5) // Wind
    {
        float wind = sin(worldPos.x * frequency + time) * (1.0 + sin(time * 0.5));
        wind += sin(worldPos.y * frequency * 1.3 + time * 1.1) * 0.5;
        offset = float3(wind * strength, 0, wind * strength * 0.5);
    }
    else // Pulse
    {
        float sinVal = abs(sin(time * frequency));
        float pulse = sinVal * sinVal;
        offset = worldNormal * pulse * strength;
    }

    return offset;
}
#endif // _VERTEX_ANIMATION

// ===== Hologram Functions (conditionally compiled) =====
#if defined(_HOLOGRAM)

// Calculate multi-layer hologram scanline effect
float CalculateHologramScanline(float2 uv, float speed, float intensity,
                                 float density, float width)
{
    float time = _Time.y * speed;
    // Layer 1: Main scanline
    float scan1 = smoothstep(width, width + 0.1,
                  frac(uv.y * density + time));
    // Layer 2: Fine sub-scanline
    float scan2 = smoothstep(0.3, 0.4,
                  frac(uv.y * density * 3.7 - time * 0.7));
    // Layer 3: Large band (slow movement)
    float scan3 = smoothstep(0.4, 0.6,
                  sin(uv.y * density * 0.3 + time * 0.3));
    float combined = scan1 * 0.6 + scan2 * 0.25 + scan3 * 0.15;
    return lerp(1.0, combined, intensity);
}

// Calculate hologram flicker
float CalculateHologramFlicker(float speed, float amount)
{
    float flicker = sin(_Time.y * speed * 10.0) * 0.5 + 0.5;
    return lerp(1.0, flicker, amount);
}

// Calculate Fresnel edge glow for hologram
half3 CalculateHologramEdgeGlow(float3 worldNormal, float3 viewDir,
                                 half3 holoColor, float power, float intensity)
{
    // Mirror-safe Fresnel
    float3 holoViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    holoViewNormal.x *= NataneMirrorSign();
    float fresnel = 1.0 - saturate(dot(normalize(holoViewNormal), float3(0, 0, 1)));
    fresnel = pow(fresnel, power);
    return holoColor * fresnel * intensity;
}

// Calculate hologram alpha with Fresnel-linked transparency
half CalculateHologramAlpha(float3 worldNormal, float3 viewDir,
                             float baseAlpha, float holoAlpha)
{
    // Mirror-safe Fresnel
    float3 holoAlphaViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    holoAlphaViewNormal.x *= NataneMirrorSign();
    float fresnel = 1.0 - saturate(dot(normalize(holoAlphaViewNormal), float3(0, 0, 1)));
    float edgeKeep = saturate(fresnel * 3.0);
    return lerp(baseAlpha, edgeKeep, holoAlpha);
}

// Apply hologram color with monochrome option
half3 ApplyHologramColor(half3 baseColor, half3 holoColor, float monochrome)
{
    float luma = dot(baseColor, float3(0.299, 0.587, 0.114));
    half3 monoColor = holoColor * luma;
    return lerp(baseColor * holoColor, monoColor, monochrome);
}

// Calculate hologram noise distortion
float2 CalculateHologramNoiseDistortion(float2 uv, float intensity,
                                         float speed)
{
    float time = _Time.y * speed;
    float2 noiseUV = uv * 5.0 + float2(time * 0.3, time * 0.7);
    float noise = frac(sin(dot(noiseUV, float2(12.9898, 78.233))) * 43758.5453);
    return uv + float2(noise - 0.5, 0) * intensity * 0.02;
}

#endif // _HOLOGRAM

// ===== Glitch Functions (conditionally compiled) =====
#if defined(_GLITCH)

// Calculate glitch distortion
float2 CalculateGlitchUV(float2 uv, float intensity, float speed, float blockSize)
{
    float time = floor(_Time.y * speed * 10.0) / 10.0; // Stepped time for glitch blocks
    float block = floor(uv.y / blockSize);
    float random = frac(sin(block * 12.9898 + time) * 43758.5453);

    float2 offset = float2(0, 0);
    if (random > 1.0 - intensity)
    {
        offset.x = (random - 0.5) * intensity * 0.15;
    }

    return uv + offset;
}

// Calculate RGB split for glitch effect
// Uses delta method: computes channel shift from texture, applies to lit color
// This preserves lighting/shading and prevents magenta artifacts
half3 CalculateGlitchRGBSplit(half3 baseColor, float2 uv,
                               sampler2D tex, float intensity)
{
    float offset = intensity * 0.02;
    half3 center = tex2D(tex, uv).rgb;
    half rShifted = tex2D(tex, uv + float2(offset, 0)).r;
    half bShifted = tex2D(tex, uv - float2(offset, 0)).b;

    // Apply relative color shift to preserve lighting
    half rDelta = rShifted - center.r;
    half bDelta = bShifted - center.b;
    return saturate(half3(baseColor.r + rDelta, baseColor.g, baseColor.b + bDelta));
}

// Apply noise texture for glitch variety
// mode: 0=UV Distortion, 1=Color Corruption, 2=Block Noise
half3 ApplyGlitchNoise(half3 color, float2 uv, NATANE_TEX2D_NS_ARG(noiseTex),
                        float4 noiseST, float4 scrollSpeed,
                        float intensity, float mode, sampler2D mainTex)
{
    // Scrolling noise UV (manual calculation, not TRANSFORM_TEX)
    float2 noiseUV = uv * noiseST.xy + noiseST.zw;
    noiseUV += scrollSpeed.xy * _Time.y;
    half4 noise = NATANE_SAMPLE_REPEAT(noiseTex, noiseUV);

    if (mode < 0.5)
    {
        // UV Distortion - offset UVs by noise
        float2 noiseOffset = (noise.rg - 0.5) * intensity * 0.1;
        half3 distorted = tex2D(mainTex, uv + noiseOffset).rgb;
        color = lerp(color, distorted, intensity);
    }
    else if (mode < 1.5)
    {
        // Color Corruption - replace color with noise
        half3 corruptColor = noise.rgb;
        color = lerp(color, corruptColor, intensity * noise.a);
    }
    else
    {
        // Block Noise - blocky color modulation
        float blockVal = step(0.5, noise.r);
        half3 blockColor = color * lerp(1.0, noise.g * 2.0, intensity);
        color = lerp(color, blockColor, blockVal * intensity);
    }
    return color;
}

#endif // _GLITCH

// ===== Glitch Stretch Functions (conditionally compiled) =====
#if defined(_GLITCH_STRETCH)

// Calculate UV stretch for glitch stretch effect
// Only stretches horizontally (U axis), preserving V
float2 CalculateGlitchStretchUV(float2 uv, float intensity, float speed,
                                  float blockSize, float frequency)
{
    float time = floor(_Time.y * speed * 10.0) / 10.0;

    // Block-based triggering
    float block = floor(uv.y / blockSize);
    float blockRand = frac(sin(block * 45.23 + time) * 43758.5453);

    // Frequency gate — only trigger stretch for some blocks
    float trigger = step(1.0 - frequency, blockRand);

    // Stretch amount varies per block
    float stretchAmount = frac(sin(block * 12.9898 + time * 1.7) * 43758.5453);
    stretchAmount = (stretchAmount * 2.0 - 1.0) * intensity;

    // Apply horizontal stretch: scale UV.x around center (0.5)
    float2 stretchedUV = uv;
    stretchedUV.x = lerp(uv.x, 0.5 + (uv.x - 0.5) * (1.0 + stretchAmount * 3.0), trigger);

    return stretchedUV;
}

#endif // _GLITCH_STRETCH

// ===== Decal Functions =====
#if defined(_DECAL)

// Calculate decal UV with position, rotation, and scale
float2 CalculateDecalUV(float2 baseUV, float2 position, float rotation, float scale)
{
    // Center UV
    float2 uv = baseUV - position;

    // Apply rotation
    float rad = rotation * 3.14159265 / 180.0;
    float s = sin(rad);
    float c = cos(rad);
    float2 rotatedUV = float2(
        uv.x * c - uv.y * s,
        uv.x * s + uv.y * c
    );

    // Apply scale
    rotatedUV /= scale;

    // Re-center
    rotatedUV += float2(0.5, 0.5);

    // Check if UV is within bounds
    if (rotatedUV.x < 0.0 || rotatedUV.x > 1.0 || rotatedUV.y < 0.0 || rotatedUV.y > 1.0)
        return float2(-1, -1); // Invalid UV (out of bounds)

    return rotatedUV;
}
#endif // _DECAL

// ===== Water Drip Effect Functions =====
#if defined(_WATER_DRIP)

// PCG-style hash functions for procedural drip generation
float Hash11(float p)
{
    uint n = asuint(p);
    n = n * 747796405u + 2891336453u;
    n = ((n >> ((n >> 28u) + 4u)) ^ n) * 277803737u;
    return float((n >> 22u) ^ n) / 4294967295.0;
}

float2 Hash22(float2 p)
{
    uint2 n = uint2(asuint(p.x), asuint(p.y));
    n = n * uint2(747796405u, 2891336453u) + uint2(2891336453u, 747796405u);
    n = ((n >> ((n >> 28u) + 4u)) ^ n) * 277803737u;
    float2 result = float2((n.x >> 22u) ^ n.x, (n.y >> 22u) ^ n.y) / 4294967295.0;
    return result;
}

// Rain-style procedural water drip effect (world-space gravity)
// Uses world position so drips always fall DOWNWARD (-Y) regardless of UV orientation.
// Each "generation" of drips spawns at a fully randomized position.
// Returns additive drip color contribution
// Performance: ~50-60 ALU instructions (comparable to Glitter effect)
half3 CalculateDripEffectFast(float3 worldPos, float time, half3 dripColor, float speed,
    float density, float size, float trailLength, float intensity, float sharpness)
{
    half3 totalDrip = half3(0, 0, 0);

    // Project world position to 2D drip space:
    // X = horizontal (combine world X and Z for full surface coverage)
    // Y = world Y (gravity axis, drips fall in -Y direction)
    float2 dripSpaceUV = float2(worldPos.x + worldPos.z * 0.7, worldPos.y);

    // Scale by density to control drip spacing
    float2 scaledUV = dripSpaceUV * lerp(3.0, 15.0, density);

    // Three layers for natural rain coverage and depth
    [unroll]
    for (int layer = 0; layer < 3; layer++)
    {
        // Offset each layer with prime-number ratios to avoid alignment
        float2 layerUV = scaledUV + float2(layer * 5.17, layer * 3.31);
        float layerSpeed = speed * (0.8 + layer * 0.25);
        float layerIntensity = (layer == 0) ? 1.0 : (layer == 1) ? 0.7 : 0.5;

        // Tile into cells
        float2 cellID = floor(layerUV);
        float2 cellUV = frac(layerUV);

        // Per-cell base random (stable per cell, used for cycle offset)
        float2 cellRand = Hash22(cellID + float2(layer * 137.0, layer * 59.0));

        // Time-based generation cycling: each cell cycles through drip "generations"
        // Each generation spawns at a completely different random position
        float cycleTime = time * layerSpeed * 0.4 + cellRand.y * 6.28;
        float generation = floor(cycleTime);
        float cyclePhase = frac(cycleTime); // 0 to 1 within this generation

        // Random position for THIS generation (changes every cycle)
        float2 genRand = Hash22(cellID + float2(generation * 7.13, generation * 11.37 + layer * 53.0));
        float2 genRand2 = Hash22(cellID + float2(generation * 13.71 + 100.0, generation * 3.77 + layer * 97.0));

        // Random start position within cell (fully randomized per generation)
        float startX = genRand.x * 0.7 + 0.15;
        float startY = genRand.y * 0.3 + 0.65; // Start near TOP of cell (high Y)

        // Drip falls DOWNWARD: Y decreases over time (gravity direction)
        float fallSpeed = 0.5 + genRand2.x * 0.5;
        float dripY = startY - cyclePhase * fallSpeed;

        // Distance from drip center
        float dx = (cellUV.x - startX) / max(size, 0.01);
        float dy = cellUV.y - dripY;

        // Drip head (round shape)
        float headDist = dx * dx + dy * dy;
        float headSize = size * size;
        float headShape = saturate(1.0 - headDist / max(headSize, 0.0001));
        headShape = pow(headShape, sharpness);

        // Trail behind the drip head (extends UPWARD = above the falling head)
        float trailDy = -(dy - trailLength * size); // Trail above drip (positive Y direction)
        float trailMask = saturate(trailDy / max(trailLength * size, 0.001));
        float trailWidth = size * lerp(1.0, 0.2, trailMask);
        float trailDist = dx * dx / max(trailWidth * trailWidth, 0.0001);
        float trailShape = saturate(1.0 - trailDist) * trailMask;
        trailShape = pow(trailShape, sharpness * 0.5) * 0.4;

        // Combine head and trail
        float dripShape = max(headShape, trailShape);

        // Lifecycle fade: appear quickly, fall, then fade out
        float fadeIn = smoothstep(0.0, 0.08, cyclePhase);
        float fadeOut = smoothstep(1.0, 0.7, cyclePhase);
        float lifecycle = fadeIn * fadeOut;

        // Random size variation per generation for organic look
        float sizeVar = 0.7 + genRand2.y * 0.6;

        totalDrip += dripColor * dripShape * lifecycle * layerIntensity * intensity * sizeVar;
    }

    return totalDrip;
}
#endif // _WATER_DRIP

// ===== Dither Coordinate Stabilization =====
// Object pivot をスクリーン投影してオフセットすることで
// オブジェクト移動時のディザパターンスライドを抑制する。
// Note: HLSL の fmod() は負の値を返すため、Bayer配列の範囲外アクセスを防ぐ必要がある。
// 大きな正のオフセットを加えて座標を常に正に保つ。
// Requires _DitherStabilize from CBUFFER — excluded in standalone passes
#ifndef NATANE_UTILS_STANDALONE
float2 StabilizeDitherCoord(float2 screenPixelPos)
{
    float4 pivotClip = mul(UNITY_MATRIX_VP, float4(unity_ObjectToWorld._m03_m13_m23, 1.0));
    float2 pivotNDC = pivotClip.xy / pivotClip.w;
    float2 pivotScreen = (pivotNDC * 0.5 + 0.5) * _ScreenParams.xy;
    // pivotScreen を引くと負座標が生じ、fmod() で負のインデックスになる。
    // 十分大きな正のオフセットを加えて常に正を保証する。
    float2 stablePos = screenPixelPos - pivotScreen + 100000.0;
    return lerp(screenPixelPos, stablePos, saturate(_DitherStabilize));
}
#endif

// ===== Shared Dithering / Alpha Functions =====
static const float NataneBayer4x4[16] = {
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
};

float NataneInterleavedGradientNoise(float2 pixelPos)
{
    return frac(52.9829189 * frac(dot(pixelPos, float2(0.06711056, 0.00583715))));
}

float NataneBayerThreshold4x4(float2 screenPos, float scale)
{
    float2 scaledPos = screenPos * max(scale, 0.0001);
    int2 coord = int2(fmod(scaledPos, 4.0));
    return NataneBayer4x4[coord.y * 4 + coord.x];
}

#if defined(_BLUE_NOISE_DITHER)
float NataneBlueNoiseThreshold(float2 screenPos, float scale)
{
    float2 scaledPos = screenPos * max(scale, 0.0001);
    float frame = floor(_Time.y * max(_BlueNoiseTemporal, 0.0));
    float2 temporalJitter = float2(frame * 0.754877666, frame * 0.569840296) * 64.0;
    float2 p = scaledPos + temporalJitter;

    // Approximation of spatiotemporal blue-noise style distribution using decorrelated IG noise taps.
    float n0 = NataneInterleavedGradientNoise(p);
    float n1 = NataneInterleavedGradientNoise(p.yx + 19.19);
    float n2 = NataneInterleavedGradientNoise(p * 0.5 + 7.7);
    return frac(n0 + n1 * 0.5 + n2 * 0.25);
}
#endif

float NataneGetDitherThreshold(float2 screenPos, float scale)
{
    float bayer = NataneBayerThreshold4x4(screenPos, scale);
    #if defined(_BLUE_NOISE_DITHER)
        float blue = NataneBlueNoiseThreshold(screenPos, scale);
        return lerp(bayer, blue, saturate(_BlueNoiseAmount));
    #else
        return bayer;
    #endif
}

#if defined(_DITHERING_ALPHA)
float BayerMatrix4x4(float2 screenPos)
{
    return NataneBayerThreshold4x4(screenPos, 1.0);
}

// Apply dithering to alpha channel
float ApplyDitheringAlpha(float alpha, float2 screenPos, float scale)
{
    float threshold = NataneGetDitherThreshold(screenPos, scale);
    return alpha - threshold;
}
#endif // _DITHERING_ALPHA

#if defined(_HASHED_ALPHA)
float HashedAlphaThreshold(float2 screenPos, float2 worldPosXZ, float scale)
{
    float2 pixelPos = floor(screenPos * max(scale, 0.5));
    float hashScreen = NataneInterleavedGradientNoise(pixelPos);
    float hashWorld = frac(sin(dot(worldPosXZ, float2(12.9898, 78.233))) * 43758.5453);
    return frac(hashScreen + hashWorld * 0.6180339887);
}

float ApplyHashedAlpha(float alpha, float2 screenPos, float2 worldPosXZ, float scale)
{
    return alpha - HashedAlphaThreshold(screenPos, worldPosXZ, scale);
}
#endif // _HASHED_ALPHA

// ===== Directional Light Fallback (for non-directional environments) =====

// ForwardBase の vertex light 配列から最も明るいライトの方向と色を取得
// ポイントライト/スポットライトのみの環境で機能する
int GetBrightestVertexLightIndex(float3 worldPos, out half3 outDir, out half3 outColor)
{
    half maxLum = 0;
    int outIndex = -1;
    outDir = half3(0, 1, 0);
    outColor = half3(0, 0, 0);

    UNITY_UNROLL
    for (int idx = 0; idx < 4; idx++)
    {
        float3 lightPos = float3(unity_4LightPosX0[idx], unity_4LightPosY0[idx], unity_4LightPosZ0[idx]);
        float3 toLight = lightPos - worldPos;
        float distSq = max(dot(toLight, toLight), 0.000001);
        float atten = 1.0 / (1.0 + distSq * unity_4LightAtten0[idx]);
        half3 color = unity_LightColor[idx].rgb * atten;
        half lum = CALC_LUMINANCE(color);

        if (lum > maxLum)
        {
            maxLum = lum;
            outIndex = idx;
            outDir = toLight * rsqrt(distSq);
            outColor = color;
        }
    }

    return outIndex;
}

void GetBrightestVertexLight(float3 worldPos, out half3 outDir, out half3 outColor)
{
    GetBrightestVertexLightIndex(worldPos, outDir, outColor);
}

// SH L1 帯域から優勢光源方向を抽出（Light Probe ベース）
half3 GetSHDominantLightDirection()
{
    half3 lumCoeff = half3(0.299, 0.587, 0.114);
    half3 shDir = half3(
        dot(half3(unity_SHAr.x, unity_SHAg.x, unity_SHAb.x), lumCoeff),
        dot(half3(unity_SHAr.y, unity_SHAg.y, unity_SHAb.y), lumCoeff),
        dot(half3(unity_SHAr.z, unity_SHAg.z, unity_SHAb.z), lumCoeff)
    );
    half len = length(shDir);
    return (len > 0.001) ? (shDir / len) : half3(0, 1, 0);
}

// SH L0 から平均環境光色を取得（フォールバックライトカラー）
half3 GetSHFallbackLightColor()
{
    half3 shAvg = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
    return max(half3(0.05, 0.05, 0.05), shAvg);
}

// ===== Smear Trail (UV-based multi-sample afterimage) =====
half3 CalculateSmearTrail(float2 uv, half3 baseColor, float3 smearDir, float3 worldNormal, float stretchFactor, float trailLength, float trailFade)
{
    // Project smear direction to UV-ish space using world normal's tangent plane
    float2 uvOffset = smearDir.xy * trailLength;

    half3 trail = half3(0, 0, 0);
    float totalWeight = 0.0;

    // Multi-sample along the smear direction (4 samples)
    [unroll]
    for (int i = 1; i <= 4; i++)
    {
        float t = (float)i / 4.0;
        float2 sampleUV = uv - uvOffset * t;
        float weight = (1.0 - t) * trailFade;
        trail += baseColor * weight; // Use base color for trail (no re-sample needed for toon)
        totalWeight += weight;
    }

    if (totalWeight > 0.001)
        trail /= totalWeight;

    return trail * stretchFactor;
}

// ===== Smear Glow (Fresnel + directional mask) =====
half3 CalculateSmearGlow(float3 worldNormal, float3 viewDir, float3 smearDir, float stretchFactor,
                          half4 glowColor, float glowIntensity, float glowPower)
{
    // Fresnel term (mirror-safe)
    float3 smearViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    smearViewNormal.x *= NataneMirrorSign();
    float NdotV = saturate(dot(normalize(smearViewNormal), float3(0, 0, 1)));
    float fresnel = pow(1.0 - NdotV, glowPower);

    // Directional mask - glow stronger on edges facing the smear direction
    float dirMask = saturate(dot(worldNormal, normalize(smearDir)));
    float edgeMask = saturate(dirMask + (1.0 - dirMask) * 0.3); // Partial wrap to keep some glow everywhere

    half3 glow = glowColor.rgb * fresnel * edgeMask * glowIntensity * stretchFactor;
    return glow;
}

// ===== Triplanar Mapping Functions =====
#if defined(_TRIPLANAR)

// Sample texture using triplanar projection (world-space, no UV required)
half4 TriplanarSample(sampler2D tex, float3 worldPos, float3 worldNormal, float scale, float sharpness)
{
    float3 blend = pow(abs(worldNormal), sharpness);
    blend /= (blend.x + blend.y + blend.z + 0.001);

    float3 scaledPos = worldPos * scale + float3(_TriplanarOffsetX, _TriplanarOffsetY, _TriplanarOffsetZ);
    half4 xProj = tex2D(tex, scaledPos.yz);
    half4 yProj = tex2D(tex, scaledPos.xz);
    half4 zProj = tex2D(tex, scaledPos.xy);
    return xProj * blend.x + yProj * blend.y + zProj * blend.z;
}

#endif // _TRIPLANAR

// =============================================================================
// ===== Illustration Style Functions ==========================================
// =============================================================================
// Artistic post-processing effects for illustration/painterly rendering.
// Each function is guarded by its own shader_feature keyword.

// ---------- 1. Color Quantization (色量子化) ----------
#ifdef _COLOR_QUANTIZE

// Bayer 4x4 dithering matrix for ordered dithering in quantization
// NOTE: _DITHERING_ALPHA section also defines a BayerMatrix4x4 *function*.
//       These are separate: the array here is used inline for quantization,
//       while the function is for alpha dithering.
static const float IllustBayerMatrix4x4[16] = {
     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
};

// Quantize colour in RGB space with optional Bayer dithering.
// levels:       number of discrete levels per channel (e.g. 8)
// ditherAmount: dithering noise strength (0 = no dither)
// screenPos:    pixel position in screen space (e.g. i.screenPos.xy * _ScreenParams.xy)
half3 QuantizeColorRGB(half3 color, float levels, float ditherAmount, float2 screenPos)
{
    int2 ditherCoord = int2(fmod(screenPos, 4.0));
    float dither = IllustBayerMatrix4x4[ditherCoord.y * 4 + ditherCoord.x] - 0.5;
    color += dither * ditherAmount / levels;
    return floor(color * levels + 0.5) / levels;
}

// Quantize colour in HSV space (independent levels per channel).
// Reuses RGBtoHSV / HSVtoRGB defined earlier in this file.
half3 QuantizeColorHSV(half3 color, float hueLevels, float satLevels, float valLevels,
                       float ditherAmount, float2 screenPos)
{
    half3 hsv = RGBtoHSV(color);
    int2 ditherCoord = int2(fmod(screenPos, 4.0));
    float dither = IllustBayerMatrix4x4[ditherCoord.y * 4 + ditherCoord.x] - 0.5;
    hsv.x = floor((hsv.x + dither * ditherAmount / hueLevels) * hueLevels + 0.5) / hueLevels;
    hsv.y = floor((hsv.y + dither * ditherAmount / satLevels) * satLevels + 0.5) / satLevels;
    hsv.z = floor((hsv.z + dither * ditherAmount / valLevels) * valLevels + 0.5) / valLevels;
    return HSVtoRGB(hsv);
}

#endif // _COLOR_QUANTIZE

// ---------- 2. 3D LUT (Look-Up Table) ----------
#ifdef _LUT_3D

// Apply a strip-layout 3D LUT (e.g. 32x32x32 packed into a 1024x32 texture).
// color:   input linear RGB (should be [0,1])
// lutTex:  the LUT texture sampler
// lutSize: number of cells per axis (typically 32)
half3 ApplyLUT3D(half3 color, NATANE_TEX2D_NS_ARG(lutTex), float lutSize)
{
    float blue = color.b * (lutSize - 1.0);
    float blueFloor = floor(blue);
    float blueFrac = blue - blueFloor;

    float invSize = 1.0 / lutSize;
    float halfTexel = 0.5 * invSize;

    // UV for the lower blue slice
    float2 uv1;
    uv1.x = (blueFloor * invSize + color.r * invSize * (1.0 - invSize)) + halfTexel * invSize;
    uv1.y = color.g * (1.0 - invSize) + halfTexel;

    // UV for the upper blue slice
    float2 uv2;
    uv2.x = (min(blueFloor + 1.0, lutSize - 1.0) * invSize + color.r * invSize * (1.0 - invSize)) + halfTexel * invSize;
    uv2.y = uv1.y;

    half3 lut1 = NATANE_SAMPLE_CLAMP(lutTex, uv1).rgb;
    half3 lut2 = NATANE_SAMPLE_CLAMP(lutTex, uv2).rgb;

    return lerp(lut1, lut2, blueFrac);
}

#endif // _LUT_3D

// ---------- 3. Hatching (TAM 6-level cross-hatching) ----------
#ifdef _HATCHING

// Apply 6-level TAM hatching.
// hatchTex0 (RGBA) = density levels 1-4, hatchTex1 (RG) = levels 5-6.
// shadingValue: 0 (full shadow) .. 1 (full light)
// maskValue:    hatching mask (0 = no hatching)
// tiling:       UV tiling multiplier for hatching pattern
// hatchColor:   tint colour for hatching strokes
// blend:        overall blend strength
half3 ApplyHatching(half3 baseColor, float2 uv, half shadingValue, half maskValue,
    NATANE_TEX2D_NS_ARG(hatchTex0), NATANE_TEX2D_NS_ARG(hatchTex1), float tiling, half4 hatchColor, float blend)
{
    float2 hatchUV = uv * tiling;
    half4 h0 = NATANE_SAMPLE_REPEAT(hatchTex0, hatchUV); // RGBA = levels 1-4
    half2 h1 = NATANE_SAMPLE_REPEAT(hatchTex1, hatchUV).rg; // RG = levels 5-6

    // Map shading value to 6 weight slots
    half lum = shadingValue * 6.0;
    half w0 = saturate(lum - 5.0);
    half w1 = saturate(lum - 4.0) - w0;
    half w2 = saturate(lum - 3.0) - w0 - w1;
    half w3 = saturate(lum - 2.0) - w0 - w1 - w2;
    half w4 = saturate(lum - 1.0) - w0 - w1 - w2 - w3;
    half w5 = 1.0 - w0 - w1 - w2 - w3 - w4;

    half hatchValue = w1 * h0.r + w2 * h0.g + w3 * h0.b + w4 * h0.a
                    + w5 * h1.r + max(0, 1.0 - lum) * h1.g;

    return lerp(baseColor, baseColor * hatchColor.rgb, hatchValue * maskValue * blend);
}

#endif // _HATCHING

// ---------- 4. Watercolor Simulation (水彩シミュレーション) ----------
#ifdef _WATERCOLOR

// Simulates watercolor painting with edge darkening, wet edge, granulation, and paper texture.
// All texture-space effects (no GrabPass required).
// granTex / granTex_ST: granulation (pigment particle) texture + tiling/offset
// paperTex / paperTex_ST: paper surface texture + tiling/offset (sampled in screen space)
half3 ApplyWatercolor(half3 baseColor, float2 uv, float2 screenUV, half shadingValue,
    half maskValue, NATANE_TEX2D_NS_ARG(granTex), float4 granTex_ST, NATANE_TEX2D_NS_ARG(paperTex), float4 paperTex_ST,
    float edgeDarkening, float wetEdge, float granulation, float paperIntensity, float paperTiling, float blend)
{
    // 1. Edge Darkening — ddx/ddy gradient magnitude drives darkening
    half3 dx = ddx(baseColor);
    half3 dy = ddy(baseColor);
    half edgeStrength = saturate(length(dx) + length(dy));
    half3 darkened = baseColor * (1.0 - edgeStrength * edgeDarkening);

    // 2. Wet Edge — colours concentrate at edges (increase saturation locally)
    half wetFactor = smoothstep(0.0, wetEdge, edgeStrength);
    half3 saturatedColor = darkened;
    half lum = CALC_LUMINANCE(darkened);
    saturatedColor = lerp(half3(lum, lum, lum), darkened, 1.0 + wetFactor * 0.5);

    // 3. Granulation — pigment particles settle in texture valleys
    float2 granUV = uv * granTex_ST.xy + granTex_ST.zw;
    half granTex_val = NATANE_SAMPLE_REPEAT(granTex, granUV).r;
    half granEffect = lerp(1.0, granTex_val, granulation * (1.0 - shadingValue));
    half3 granulated = saturatedColor * granEffect;

    // 4. Paper Texture — screen-space paper grain overlay
    float2 paperUV = screenUV * paperTiling;
    half paperVal = NATANE_SAMPLE_REPEAT(paperTex, paperUV).r;
    half paperEffect = paperVal * 2.0 - 1.0; // remap [0,1] → [-1,1]
    half3 papered = granulated + granulated * paperEffect * paperIntensity;

    return lerp(baseColor, saturate(papered), maskValue * blend);
}

#endif // _WATERCOLOR

// ---------- 5. Gaussian Blur / Soft Filter (13-tap separated) ----------
// Requires GrabPass (_nataneBackgroundTexture). The texture & texel size are declared in the
// _REFRACTION section above. If _REFRACTION is not active, the caller must ensure
// _nataneBackgroundTexture is still available (e.g. shared GrabPass declaration in the shader).
#ifdef _SOFT_FILTER

static const int ILLUST_GAUSS_SAMPLES = 13;
static const float IllustGaussWeights[13] = {
    0.0044, 0.0115, 0.0257, 0.0488, 0.0799, 0.1133, 0.1389,
    0.1133, 0.0799, 0.0488, 0.0257, 0.0115, 0.0044
};
static const float IllustGaussOffsets[13] = {
    -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6
};

// Single-axis 13-tap Gaussian blur on GrabPass.
// direction: blur axis (e.g. float2(1,0) for horizontal)
// blurRadius: pixel radius multiplier
half3 GaussianBlurGrabPass(float2 screenUV, float2 direction, float blurRadius)
{
    half3 result = 0;
    float2 texelSize = _nataneBackgroundTexture_TexelSize.xy * blurRadius;
    [unroll]
    for (int i = 0; i < ILLUST_GAUSS_SAMPLES; i++)
    {
        float2 offset = direction * IllustGaussOffsets[i] * texelSize;
        result += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, screenUV + offset).rgb * IllustGaussWeights[i];
    }
    return result;
}

// Two-pass (H + V averaged) soft filter with optional selective bloom mode.
// mode: 0 = full blur, >0.5 = selective bloom (only bright areas are blurred)
// threshold: luminance threshold for selective bloom mode
half3 ApplySoftFilter(half3 baseColor, float2 grabUV, float radius, float blend,
                      float threshold, float mode)
{
    half3 blurredH = GaussianBlurGrabPass(grabUV, float2(1, 0), radius);
    half3 blurredV = GaussianBlurGrabPass(grabUV, float2(0, 1), radius);
    half3 blurred = (blurredH + blurredV) * 0.5;

    if (mode > 0.5)
    {
        // Selective Bloom mode — only blend where luminance exceeds threshold
        half baseLum = CALC_LUMINANCE(baseColor);
        half bloomMask = smoothstep(threshold, threshold + 0.2, baseLum);
        return lerp(baseColor, lerp(baseColor, blurred, bloomMask), blend);
    }
    else
    {
        // Full blur mode
        return lerp(baseColor, blurred, blend);
    }
}

#endif // _SOFT_FILTER

// ---------- 6. Kuwahara Filter (油絵風フィルタ) ----------
// Requires GrabPass (_nataneBackgroundTexture).
#ifdef _KUWAHARA_FILTER

// Kuwahara filter: selects the mean colour of the quadrant with minimum variance.
// Produces a painterly, oil-painting look.
// radius: kernel radius per quadrant (small values 2-4 recommended for performance)
half3 ApplyKuwaharaFilter(float2 grabUV, int radius, float blend, half3 baseColor)
{
    half3 meanColors[4] = { half3(0,0,0), half3(0,0,0), half3(0,0,0), half3(0,0,0) };
    half variances[4] = { 0, 0, 0, 0 };
    float2 texelSize = _nataneBackgroundTexture_TexelSize.xy;
    float count = (float)(radius * radius);

    // Iterate over 4 quadrants: top-left, top-right, bottom-left, bottom-right
    [loop]
    for (int qx = 0; qx < 2; qx++)
    {
        [loop]
        for (int qy = 0; qy < 2; qy++)
        {
            int quadIdx = qx * 2 + qy;
            half3 sum = 0;
            half3 sumSq = 0;

            [loop]
            for (int ix = 0; ix < radius; ix++)
            {
                [loop]
                for (int iy = 0; iy < radius; iy++)
                {
                    float2 offset = float2(ix - radius * (1 - qx), iy - radius * (1 - qy)) * texelSize;
                    half3 s = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, grabUV + offset).rgb;
                    sum += s;
                    sumSq += s * s;
                }
            }

            meanColors[quadIdx] = sum / count;
            half3 var = sumSq / count - meanColors[quadIdx] * meanColors[quadIdx];
            variances[quadIdx] = dot(var, LUMA_WEIGHTS);
        }
    }

    // Select quadrant with minimum variance (most uniform region)
    int minIdx = 0;
    half minVar = variances[0];
    [unroll]
    for (int i = 1; i < 4; i++)
    {
        if (variances[i] < minVar)
        {
            minVar = variances[i];
            minIdx = i;
        }
    }

    return lerp(baseColor, meanColors[minIdx], blend);
}

#endif // _KUWAHARA_FILTER

// ---------- 7. Sobel Edge Detection (スクリーンスペース輪郭検出) ----------
// Requires _CameraDepthTexture and _CameraDepthNormalsTexture.
// DecodeViewNormalStereo is provided by UnityCG.cginc (already included).
#ifdef _SCREEN_EDGE

float2 GetScreenEdgeTexelSize()
{
    return 1.0 / max(_ScreenParams.xy, float2(1.0, 1.0));
}

// Sobel edge detection on depth buffer.
// Returns 0..1 edge strength (1 = strong edge).
half SobelEdgeDepth(float2 screenUV, float sensitivity)
{
    float2 texel = GetScreenEdgeTexelSize();

    float d00 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(-texel.x, -texel.y)));
    float d10 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(0, -texel.y)));
    float d20 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(texel.x, -texel.y)));
    float d01 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(-texel.x, 0)));
    float d21 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(texel.x, 0)));
    float d02 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(-texel.x, texel.y)));
    float d12 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(0, texel.y)));
    float d22 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV + float2(texel.x, texel.y)));

    float sobelX = -d00 - 2.0*d01 - d02 + d20 + 2.0*d21 + d22;
    float sobelY = -d00 - 2.0*d10 - d20 + d02 + 2.0*d12 + d22;

    return saturate(sqrt(sobelX * sobelX + sobelY * sobelY) * sensitivity);
}

// Sobel edge detection on camera normals buffer.
// Returns 0..1 edge strength (1 = strong normal discontinuity).
half SobelEdgeNormal(float2 screenUV, float sensitivity)
{
    float2 texel = GetScreenEdgeTexelSize();

    half3 n00 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(-texel.x, -texel.y)));
    half3 n10 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(0, -texel.y)));
    half3 n20 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(texel.x, -texel.y)));
    half3 n01 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(-texel.x, 0)));
    half3 n21 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(texel.x, 0)));
    half3 n02 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(-texel.x, texel.y)));
    half3 n12 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(0, texel.y)));
    half3 n22 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenUV + float2(texel.x, texel.y)));

    half3 sobelX = -n00 - 2.0*n01 - n02 + n20 + 2.0*n21 + n22;
    half3 sobelY = -n00 - 2.0*n10 - n20 + n02 + 2.0*n12 + n22;

    return saturate((length(sobelX) + length(sobelY)) * sensitivity);
}

// Combined depth + normal edge detection.
// Returns max(depth edge, normal edge) as final edge strength.
half ApplyScreenEdge(float2 screenUV, float depthSens, float normalSens, float edgeWidth)
{
    // edgeWidth is pre-baked into sensitivity values by the caller
    half depthEdge = SobelEdgeDepth(screenUV, depthSens);
    half normalEdge = SobelEdgeNormal(screenUV, normalSens);

    return saturate(max(depthEdge, normalEdge));
}

#endif // _SCREEN_EDGE

// ---------- 8. Color Bleeding (色にじみ) ----------
// Requires GrabPass (_nataneBackgroundTexture).
#ifdef _COLOR_BLEEDING

// Simulates colour bleeding / pigment diffusion.
// Samples 8 directions around the pixel; brighter neighbours bleed INTO darker areas.
half3 ApplyColorBleeding(half3 baseColor, float2 grabUV, float radius, float blend)
{
    float2 texelSize = _nataneBackgroundTexture_TexelSize.xy * radius;
    half3 sum = 0;

    // 8-direction sampling (cardinal + diagonal)
    static const float2 bleedDirs[8] = {
        float2( 1,  0), float2(-1,  0), float2( 0,  1), float2( 0, -1),
        float2( 0.707,  0.707), float2(-0.707,  0.707),
        float2( 0.707, -0.707), float2(-0.707, -0.707)
    };

    half selfLum = CALC_LUMINANCE(baseColor);

    [unroll]
    for (int i = 0; i < 8; i++)
    {
        half3 s = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, grabUV + bleedDirs[i] * texelSize).rgb;
        // Selective mixing: blend only if neighbour is brighter (colour flows light → dark)
        half sampleLum = CALC_LUMINANCE(s);
        half mixFactor = saturate(sampleLum - selfLum);
        sum += lerp(baseColor, s, mixFactor);
    }
    sum /= 8.0;

    return lerp(baseColor, sum, blend);
}

#endif // _COLOR_BLEEDING

// ---------- 9. Chromatic Aberration (色収差) ----------
// Requires GrabPass (_nataneBackgroundTexture).
#ifdef _CHROMATIC_ABERRATION

// Radial chromatic aberration — R/G/B channels are shifted outward from screen centre.
// intensity: pixel offset strength
// blend: mix factor with original colour
half3 ApplyChromaticAberration(float2 grabUV, float intensity, float blend, half3 baseColor)
{
    float2 dir = grabUV - 0.5;
    float2 offset = dir * intensity * _nataneBackgroundTexture_TexelSize.xy;

    half r = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, grabUV + offset).r;
    half g = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, grabUV).g;
    half b = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_nataneBackgroundTexture, grabUV - offset).b;

    half3 caColor = half3(r, g, b);
    return lerp(baseColor, caColor, blend);
}

#endif // _CHROMATIC_ABERRATION

// =============================================================================
// ===== End of Illustration Style Functions ===================================
// =============================================================================

#endif // NATANE_TOON_UTILS_INCLUDED
