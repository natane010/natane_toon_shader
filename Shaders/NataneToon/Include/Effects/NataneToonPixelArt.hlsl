// NataneToonPixelArt.hlsl
// Pixel Art (ピクセルアート化) — REDUCED SCOPE.
//
// Implements: UV pixelation (snap mainUV before sampling), post-lighting
// color/light posterization, palette LUT remap, and Bayer dither.
//
// DELIBERATE OMISSION: Screen Pixelation and Object-Stable Pixelation modes are
// intentionally NOT implemented. Screen-space snapping is VR-unsafe (per-eye
// pixel seams / stereo mismatch) and object-stable reprojection is high cost;
// both were dropped in the batch-2 scope review. Only the mirror-safe,
// texture-space + color-space subset ships here.
//
// Helpers are pure; the UV snap, palette sampling (NOSAMPLER LUT) and dither
// live in the fragment where the shared sampler and screen position exist.
#ifndef NATANE_TOON_PIXEL_ART_INCLUDED
#define NATANE_TOON_PIXEL_ART_INCLUDED

// Snap a UV to a virtual grid of `res` cells, sampling each cell centre.
float2 NatanePixelSnapUV(float2 uv, float res)
{
    res = max(res, 1.0);
    return (floor(uv * res) + 0.5) / res;
}

// Posterize a colour to `steps` discrete levels per channel (rounded).
half3 NatanePixelPosterize(half3 c, float steps)
{
    steps = max(steps, 1.0);
    return floor(saturate(c) * steps + 0.5) / steps;
}

#endif // NATANE_TOON_PIXEL_ART_INCLUDED
