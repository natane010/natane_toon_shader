#ifndef NATANE_TOON_CAMERA_INCLUDED
#define NATANE_TOON_CAMERA_INCLUDED

// ===================================================================
// Natane Toon Shader - Camera / viewer helpers
// ===================================================================
// Everything that depends on WHICH camera is rendering lives here, so the
// forward passes, the outline pass and the fur passes all agree:
//
//   * VRChat mirrors render with a reflected view matrix and an OBLIQUE
//     near-clip projection. Clip-space z is then no longer a function of
//     view depth, so anything scaled by "o.pos.z" changes with the distance
//     to the mirror plane.
//   * Mirror cameras usually have no depth texture of their own, so
//     _CameraDepthTexture lookups read the main view's depth.
//   * In VR each eye has its own camera position. Distance-driven fades,
//     clips, dithers and LOD steps evaluated per eye disagree between the
//     eyes (binocular rivalry).
//
// Requires UnityCG.cginc. Safe to include from standalone passes.

// ----- VRChat globals (set by the VRChat runtime; zero elsewhere) -----
#ifndef NATANE_VRCHAT_GLOBALS_DECLARED
#define NATANE_VRCHAT_GLOBALS_DECLARED
float _VRChatMirrorMode;      // 0=Normal, 1=Mirror(VR), 2=Mirror(Desktop)
float _VRChatCameraMode;      // 0=Normal, 1=VR handheld camera, 2=Desktop camera, 3=Screenshot
#endif
#ifndef NATANE_VRCHAT_MIRRORCAMPOS_DECLARED
#define NATANE_VRCHAT_MIRRORCAMPOS_DECLARED
float3 _VRChatMirrorCameraPos;    // Mirror camera world position (eye-centred), zero outside mirrors
#endif

// Reference near-clip distance for the camera-independent outline width mode.
#define NATANE_OUTLINE_REF_NEAR 0.05

bool NataneIsOrthoCamera()
{
    return unity_OrthoParams.w > 0.5;
}

// True when the projection's z row was replaced by an oblique near plane.
// A mirror viewed head-on has zero x/y terms, hence the explicit VRChat flag.
bool NataneIsObliqueProjection()
{
    return _VRChatMirrorMode > 0.5
        || (abs(UNITY_MATRIX_P._m20) + abs(UNITY_MATRIX_P._m21)) > 1e-6;
}

// Eye-independent viewer position for DISTANCE-driven effects only
// (distance / near fade, LOD, density steps, outline distance compensation).
// View DIRECTION (specular, rim, parallax) must keep using the per-eye camera.
float3 NataneViewerPos()
{
    // VR mirrors are rendered by one mono camera per eye; VRChat provides the centred position.
    if (_VRChatMirrorMode > 0.5 && dot(_VRChatMirrorCameraPos, _VRChatMirrorCameraPos) > 1e-8)
        return _VRChatMirrorCameraPos;
#if defined(USING_STEREO_MATRICES)
    return (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
#else
    return _WorldSpaceCameraPos;
#endif
}

float NataneViewerDistance(float3 worldPos)
{
    return length(NataneViewerPos() - worldPos);
}

// Replacement for the historical "offset * o.pos.z * width" outline term.
//
//   Regular perspective camera, legacy mode: returns clip z itself, so the
//   result is bit-identical to previous versions.
//
//   Oblique projection (VRChat mirror): clip z follows the distance to the
//   mirror plane, which made outlines many times thicker and vary while
//   walking. Rebuild the value a regular projection of the same camera
//   would produce from view depth (clip w) and the camera's near/far.
//
//   Orthographic camera: clip z is ~1 there (about 1/near too thick); use
//   the camera's near distance so the world-space thickness matches.
//
//   Stable mode (opt-in, _OutlineWidthMode = 1): a fixed reference near, so
//   the thickness no longer depends on the rendering camera's near clip
//   (editor Game view 0.3, VRChat 0.01-0.05, handheld camera, ...).
float NataneOutlineDepthScale(float4 clipPos, float stableMode)
{
    if (stableMode >= 0.5)
        return NATANE_OUTLINE_REF_NEAR;

    bool isOrtho = NataneIsOrthoCamera();
    if (!isOrtho && !NataneIsObliqueProjection())
        return clipPos.z;

    float nearPlane = _ProjectionParams.y;
    float farPlane = _ProjectionParams.z;
    if (isOrtho)
        return nearPlane;

    float viewDepth = clipPos.w;
#if defined(UNITY_REVERSED_Z)
    return nearPlane * (farPlane - viewDepth) / max(farPlane - nearPlane, 1e-5);
#else
    return (viewDepth * (farPlane + nearPlane) - 2.0 * farPlane * nearPlane) / max(farPlane - nearPlane, 1e-5);
#endif
}

// Clip position -> pixel position in the SV_POSITION (i.pos.xy) convention.
// Use this to anchor screen-space patterns to a projected world point.
float2 NataneClipToPixel(float4 clipPos)
{
    float2 ndc = clipPos.xy / clipPos.w;
#if UNITY_UV_STARTS_AT_TOP
    ndc.y = -ndc.y;   // SV_POSITION runs top-down on these platforms
#endif
    float2 px = (ndc * 0.5 + 0.5) * _ScreenParams.xy;
#if defined(UNITY_SINGLE_PASS_STEREO)
    px.x += unity_StereoEyeIndex * _ScreenParams.x;   // double-wide: right-eye viewport offset
#endif
    return px;
}

// ----- Ordered dithering (shared by the forward and outline passes) -----
static const float NataneBayer4x4[16] = {
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
};

// screenPos is in pixels (SV_POSITION.xy, centres at .5). The coordinate is
// floored before the modulo: without it an even scale only ever reached a
// few of the 16 thresholds (scale 10 -> 4 levels, scale 4 or 8 -> 1 level).
float NataneBayerThreshold4x4(float2 screenPos, float scale)
{
    float2 cell = floor(screenPos * max(scale, 0.0001));
    int2 coord = int2(fmod(cell, 4.0));
    return NataneBayer4x4[coord.y * 4 + coord.x];
}

// The fade / shadow dither sliders use a "x10" convention (default 10 = one
// Bayer cell per pixel, smaller = coarser cells). Values above 10 used to
// skip cells and alias, so they are clamped to one cell per pixel.
float NataneFadeDitherScale(float sliderValue)
{
    return min(max(sliderValue, 1.0) * 0.1, 1.0);
}

#endif // NATANE_TOON_CAMERA_INCLUDED
