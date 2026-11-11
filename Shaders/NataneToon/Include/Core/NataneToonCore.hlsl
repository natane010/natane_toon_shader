#ifndef NATANE_TOON_CORE_INCLUDED
#define NATANE_TOON_CORE_INCLUDED

// Natane Toon Shader Core
// Modular cel-shading/NPR shader for Unity Built-in Render Pipeline
//
// Architecture:
// - NataneToonInput.hlsl: Properties and data structures
// - NataneToonUtils.hlsl: Utility functions
// - NataneToonLighting.hlsl: Lighting calculation functions
// - NataneToonVertex.hlsl: Vertex shader
// - NataneToonFragment.hlsl: Fragment shader

// Build settings (feature #defines for build-time optimization)
// Must be included before everything else.
#include "NataneToonBuildSettings.hlsl"

// Unity includes
#include "UnityCG.cginc"
#include "Lighting.cginc"

// Transparent variants must NOT use screen-space shadows.
// Screen-space shadows sample the depth buffer, but transparent objects
// (ZWrite Off) are absent from it, so the lookup returns the shadow of
// whatever opaque geometry sits behind — causing shadow "bleed-through".
// Undefining SHADOWS_SCREEN before AutoLight.cginc forces Unity to fall
// back to direct light-space shadow map sampling, which is depth-buffer
// independent and gives correct per-fragment shadow positions.
#ifdef TRANSPARENT_VARIANT
    #ifdef SHADOWS_SCREEN
        #undef SHADOWS_SCREEN
    #endif
#endif

#include "AutoLight.cginc"

// Natane Toon Shader modules
// Organized by functionality for better maintainability
#include "NataneToonInput.hlsl"
#include "../Utils/NataneToonUtils.hlsl"
// Expression effect modules (v1.6.0 batch 1). Included after Utils so the
// FX Modulator can call SampleAudioLink*; all four are #ifdef-guarded internally.
#include "../Effects/NataneToonLineBoil.hlsl"
#include "../Effects/NataneToonShapedHighlight.hlsl"
#include "../Effects/NataneToonTopographic.hlsl"
#include "../Effects/NataneToonFXModulator.hlsl"
// Expression effect modules (v1.6.0 batch 2). All #ifdef-guarded internally.
#include "../Effects/NataneToonLenticular.hlsl"
#include "../Effects/NataneToonCaustics.hlsl"
#include "../Effects/NataneToonShadowBokeh.hlsl"
#include "../Effects/NataneToonHalftone.hlsl"
#include "../Effects/NataneToonPixelArt.hlsl"
// Shadow Shape Rig (NPR2026 P1). Must come before Lighting: ApplyShadowShapeRig
// lives in Lighting and calls NataneRigWeight from here.
#include "../Effects/NataneToonShadowShapeRig.hlsl"
#include "../Lighting/NataneToonLighting.hlsl"
#include "../Lighting/NataneToonLightmap.hlsl"
#include "../Lighting/NataneToonPBR.hlsl"
#include "NataneToonVertex.hlsl"
#include "NataneToonTessellation.hlsl"
#include "../Rendering/NataneToonFragment.hlsl"

#endif // NATANE_TOON_CORE_INCLUDED
