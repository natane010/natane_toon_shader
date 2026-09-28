# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Natane Toon Shader is a Unity package providing a comprehensive cel-shading/NPR toon shader for Unity's Built-in Render Pipeline. It is distributed as a VPM/UPM package and includes extensive editor tooling, material presets, and migration utilities.

**Version**: see `package.json` (1.5.x line)
**Unity Compatibility**: 2022.3+ (Built-in RP; needs >= 2021.2 for the local keyword count). Regular variants target SM 3.5; the Tessellation variants target SM 4.6.
**Target Platform**: Built-in Render Pipeline, VRChat PC optimized

## Architecture Overview

### Three-Layer Architecture

1. **Shader Layer** (`Shaders/NataneToon/`): Modular HLSL shader system
   - 13 shader variants sharing one include tree (see below)
   - Modular include files organized by function (Core/Lighting/Rendering/Utils/Config/Wirelight)
   - Side shaders: `Eye/NataneToonEye.shader` (unlit, standalone), `NataneToonWirelight.shader` (geometry shader, SM 5.0), `Effects/NataneScreenFXOverlay.shader`
   - Unrelated helper shaders live in `Shaders/AutoMat`, `Shaders/ParallaxBox`, `Shaders/Reduction`, `Shaders/Compute`

2. **Editor Layer** (`Editor/`): C# editor extensions (~54k lines)
   - `Editor/NataneToon/GUI/` custom ShaderGUI (`NataneToonShaderGUI.cs`, ~9.5k lines) with Japanese/English localization
   - `Editor/NataneToon/Tools/`, `Presets/`, `Migration/`, `Help/`, `Setup/`
   - `Editor/NataneToon/Integration/` keyword synchronizer, asset index, build hooks, VRC Light Volumes / LTCGI auto-detection
   - Sibling tools: `Editor/MapGenerator`, `Editor/ParallaxBox`, `Editor/ParticleSystem`, `Editor/Reduction`

3. **Runtime Layer** (`Runtime/`): ScriptableObject presets, color palettes, particle helpers. Contains a few MonoBehaviours that VRChat strips on upload.

### Shader Variants

All variants live in `Shaders/NataneToon/NataneToonShader.shader` (Opaque) and `Shaders/NataneToon/Variants/`:
Cutout, Cutout Lite, Transparent, Transparent Lite, Lite, Fur, Fur Lite, Background, ScreenEdge Split,
plus the tessellation twins Tessellation, Cutout Tessellation, Transparent Tessellation.
"Lite" variants have no GrabPass and omit `_REFRACTION`, `_SOFT_FILTER`, `_KUWAHARA_FILTER`, `_COLOR_BLEEDING`, `_CHROMATIC_ABERRATION`.
Only the three Tessellation variants declare hull/domain stages (`#pragma target 4.6`); every other variant uses `#pragma vertex vert` with `#pragma target 3.5` and ignores `_TESSELLATION`. The inspector's Tessellation section switches materials between the pairs (mapping in `NataneToonShaderGUI.s_TessellationCounterparts`).

Each variant file inlines its own Properties block and an OUTLINE pass (~350 lines, self-contained, includes only UnityCG). The ShadowCaster pass is shared through `Include/Rendering/NataneToonShadowCaster.hlsl` (Input + Utils + Vertex, never Fragment) and declares the silhouette-affecting keywords. **Any change to the OUTLINE pass, the Properties block or the `#pragma shader_feature_local` lists must be replicated to all applicable variants, including the Tessellation twins.** Diff the variants after editing; drift here has caused real bugs.

### Shader Module System

Include order in `Include/Core/NataneToonCore.hlsl` (strict, do not reorder):

```
NataneToonCore.hlsl
├── Core/NataneToonBuildSettings.hlsl   (build-time feature #undef guard, first)
├── UnityCG.cginc, Lighting.cginc, AutoLight.cginc (SHADOWS_SCREEN undefined for TRANSPARENT_VARIANT)
├── Core/NataneToonInput.hlsl           (CBUFFER properties, NOSAMPLER textures, appdata/v2f)
├── Utils/NataneToonUtils.hlsl          (HSV, blending, dithering, AudioLink, effects helpers)
├── Lighting/NataneToonLighting.hlsl    (toon shading, specular, rim, SSS; includes ThirdPartyLighting + Config/*)
├── Lighting/NataneToonLightmap.hlsl    (Background variant only)
├── Lighting/NataneToonPBR.hlsl         (GGX helpers)
├── Core/NataneToonVertex.hlsl          (vertex shader, VAT, smear, smooth normal)
├── Core/NataneToonTessellation.hlsl    (hull/domain; only used by the Tessellation variants)
└── Rendering/NataneToonFragment.hlsl   (main fragment shader, effect composition)

Rendering/NataneToonShadowCaster.hlsl   (ShadowCaster pass; includes BuildSettings, Unity cgincs, Input, Utils, Vertex)
```

Approximate sizes: Fragment ~2.5k lines, Utils ~1.9k, Input ~1.5k, Lighting ~0.7k.

Textures are declared with `UNITY_DECLARE_TEX2D_NOSAMPLER` and sampled through the `NATANE_SAMPLE_*` macros with two shared sampler states. Functions that take a texture argument must use `NATANE_TEX2D_NS_ARG(name)` and the `NATANE_SAMPLE_*` macros, never `sampler2D` + `tex2D`.

## Development Workflow

### Adding a New Shader Feature

1. **Define properties** in `Include/Core/NataneToonInput.hlsl` (inside the CBUFFER; textures outside it with NOSAMPLER).
2. **Add utility functions** (if needed) in `Include/Utils/NataneToonUtils.hlsl`.
3. **Implement the effect**: lighting in `Lighting/NataneToonLighting.hlsl`, fragment composition in `Rendering/NataneToonFragment.hlsl`, vertex work in `Core/NataneToonVertex.hlsl`. Guard ForwardBase-only effects with `UNITY_PASS_FORWARDBASE` and gate on both the keyword and the toggle float (`#ifdef _X` + `if (_X >= 0.5)`).
4. **Add the property and `#pragma shader_feature_local _NEW_FEATURE`** to every applicable variant's Properties block and FORWARD_BASE pass (FORWARD_ADD only if the effect contributes to additive lights). Add a `NATANE_FEATURE_*` guard in `NataneToonBuildSettings.hlsl`.
5. **Register the keyword** in `Editor/NataneToon/Integration/NataneShaderKeywordSynchronizer.cs` (`KeywordMappings`) and add the UI in `Editor/NataneToon/GUI/NataneToonShaderGUI.cs` via `DrawToggle`.
6. Update `README.md` and `CHANGELOG.md`.

### Adding a New Editor Tool

1. Choose the category folder under `Editor/NataneToon/`.
2. Namespace `NataneToon.Editor`; asmdef graph is Runtime <- Editor <- Editor.Tools <- Editor.Migration (no cycles; cross-assembly access is via reflection).
3. Register the menu path as a constant in `Editor/NataneToon/NataneToolMenuPaths.cs` and reference that constant from `[MenuItem]`.
4. Localize UI strings with `L("日本語", "English")` from `NataneToonLocalization`.
5. Wrap every material modification with `Undo.RecordObject`.

## Key Conventions

- Boolean toggles: `_EnableFeature` / `_Feature` float; shader keywords: `_FEATURE_NAME`; textures: `_FeatureTex` / `_FeatureMap`; colors: `_FeatureColor`.
- Performance rating: A (0-3 features), B (4-6), C (7-9), D (10+).
- `ShaderVariants/NataneToonShaderVariants.shadervariants` and `Tools > Natane > シェーダー Shader` hold the variant collection / prewarming / stripping tools. Stripping is opt-in.
- The `[InitializeOnLoad]` keyword synchronizer keeps toggle floats and keywords consistent for materials indexed under `Assets/`; `Tools/Natane/Fix All Material Keywords` runs it manually.

## Important Files

- `Shaders/NataneToon/Include/Rendering/NataneToonFragment.hlsl` - main rendering logic
- `Shaders/NataneToon/Include/Lighting/NataneToonLighting.hlsl` - lighting calculations
- `Shaders/NataneToon/Include/Core/NataneToonInput.hlsl` - property definitions
- `Editor/NataneToon/GUI/NataneToonShaderGUI.cs` - material inspector
- `Editor/NataneToon/Integration/NataneShaderKeywordSynchronizer.cs` - keyword table and sync
- `Editor/NataneToon/NataneToolMenuPaths.cs` - menu path registry
- `package.json`, `CHANGELOG.md`, `THIRD_PARTY_NOTICES.md`
- `.github/workflows/build-listing.yml` - VPM listing build (runs on `v*` tags)

## VRChat-Specific Considerations

- **VRC Light Volumes**: package version preferred, bundled fallback in `Shaders/NataneToon/ThirdParty/VRCLightVolumes/` (MIT, RED_SIM).
- **LTCGI**: used only when the package is installed (`Config/NataneToonLTCGIConfig.hlsl` is auto-written by the detector).
- **AudioLink**: reads `_AudioTexture` directly; band rows follow `ALPASS_AUDIOLINK`.
- **Mirrors**: `_VRChatMirrorMode` is always declared; view-space XY dependent effects (MatCap, Offset Rim, Angel Ring, Eye Parallax, outline offset) flip X via `NataneMirrorSign()`.
- **Build hooks**: `IPreprocessBuildWithReport` / `IProcessSceneWithReport` do not run inside the VRChat SDK upload pipeline. `Editor/NataneToon/Tools/NataneVRChatBuildCallbacks.cs` (compiled under `VRC_SDK_VRCSDK3`) runs the keyword sync, build preparation and stripper reset from `IVRCSDKBuildRequestedCallback` / `IVRCSDKPreprocessAvatarCallback`; `IPreprocessShaders` (the stripper) runs in both pipelines.
- Regular variants need SM 3.5; the Tessellation variants need SM 4.6. Quest/mobile GPUs are not a supported target of the main shader.

## Documentation

- `README.md`, `CHANGELOG.md`, `BOOTH_DESCRIPTION.md`
- `Documentation~/` (excluded from the VPM zip): TECHNICAL, FOLDER_STRUCTURE, MIGRATION_GUIDE, SHADER_VARIANTS, PARTICLE_SYSTEM_GUIDE, QUICK_START, RAMP_TEXTURE_GUIDE, CONTRIBUTING, VRCHAT_PC_UPLOAD_CHECKLIST, VRCHAT_WHITEOUT_CHECKLIST, SHADER_STYLE_AND_OPTIMIZATION_AUDIT
- `Website/` is the VPM listing site (excluded from packages)

## Critical Warnings

- Do not modify the include order in `NataneToonCore.hlsl`.
- Replicate OUTLINE pass / Properties / pragma changes to all applicable variants and diff them.
- Maintain backward compatibility for existing materials when changing properties or lighting math; look-changing fixes need a compatibility toggle (example: `_DirectLightClampMode`, whose `Legacy` default preserves the old normalize-based direct light clamp).
- Editor scripts must handle null materials/shaders gracefully and always implement Undo.
- `NataneToonBuildSettings.hlsl` is rewritten during builds by `NataneBuildFeatureOptimizer`; do not hand-edit the generated section.
