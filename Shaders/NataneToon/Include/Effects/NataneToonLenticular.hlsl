// NataneToonLenticular.hlsl
// Lenticular / view-angle dependent texture (レンチキュラー / 角度依存テクスチャ).
//
// Selects a frame from a horizontal or grid atlas based on the signed view
// angle around a chosen object-space axis, so the picture changes as the
// avatar is viewed from different sides. Helpers here are pure (arguments +
// well-known Unity globals only); the atlas sampling and the per-mode
// composition run in the fragment because the atlas is a NOSAMPLER texture
// bound to the shared sampler.
//
// VR: object-space angle plus the optional stereo-center camera position keep
// both eyes on the same frame, avoiding per-eye flicker across frame borders.
#ifndef NATANE_TOON_LENTICULAR_INCLUDED
#define NATANE_TOON_LENTICULAR_INCLUDED

// Camera position for angle evaluation. StereoCenter (mode 1, default) averages
// the two eye positions so left/right eyes resolve the same frame. Falls back to
// the mono camera position outside single-pass stereo.
float3 NataneLenticularCameraPos(float stereoMode)
{
    // NataneViewerPos is the stereo centre in VR and the centred mirror camera in
    // VRChat mirrors (rendered by mono cameras), so both eyes resolve the same frame there too.
    if (stereoMode > 0.5)
        return NataneViewerPos();
    return _WorldSpaceCameraPos;
}

// Signed view angle in degrees around the selected object-space axis.
// axis: 0 = X (horizontal head-turn), 1 = Y (vertical). vdObj is the
// object-space, normalized fragment->camera direction.
float NataneLenticularAngle(float3 vdObj, float axis)
{
    return (axis < 0.5) ? degrees(atan2(vdObj.x, vdObj.z))
                        : degrees(atan2(vdObj.y, vdObj.z));
}

// Atlas UV for a given (clamped) frame index.
// dir: 0 = Horizontal strip, 1 = near-square Grid. frac() keeps the sample
// inside a single frame cell so tiling UVs never bleed across frames.
float2 NataneLenticularFrameUV(float2 uv, float frame, float frames, float dir)
{
    frames = max(frames, 1.0);
    frame  = clamp(frame, 0.0, frames - 1.0);
    float2 fuv = frac(uv);
    if (dir < 0.5) // Horizontal
    {
        return float2((fuv.x + frame) / frames, fuv.y);
    }
    // Grid (top-left = frame 0, row-major)
    float cols = max(ceil(sqrt(frames)), 1.0);
    float rows = max(ceil(frames / cols), 1.0);
    float c = fmod(frame, cols);
    float r = floor(frame / cols);
    return float2((fuv.x + c) / cols, (fuv.y + (rows - 1.0 - r)) / rows);
}

#endif // NATANE_TOON_LENTICULAR_INCLUDED
