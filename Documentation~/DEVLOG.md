# Whiteboard

## 2026-07-29

### Fur: rebuilt the strand model on lilToon's approach
- The strand model is now a HEIGHT FIELD sliced by the layer height, which is how both
  references build fur: lilToon reads the field from `_FurNoiseMask`
  (`lil_common_frag.hlsl`), hecomi's URP articles from the fur texture's alpha. Hairs are
  carved out of the field, not drawn directly.
- Ported lilToon's curve verbatim:
  `shift = layer - layer * _FurRootOffset + _FurRootOffset`,
  `alpha = saturate(field - shift * |shift|^3 + 0.25)`, and its AO
  (`saturate(1 - field + field * layer)`).
- Added `_FurRootOffset` (lilToon-compatible) and `_FurUseNoiseTex`. With the toggle off,
  an equivalent dot field is generated in the shader (3x3 neighbourhood, per-cell random
  length and lean) so the default material shows fur without an authored texture.
- Confirmed against lilToon that gravity scales with strand length
  (`furVector.y -= _FurGravity * length(furVector)`), which validates the earlier fix here.
- Four earlier attempts at computing the hair cross-section geometrically failed in a
  chain — bald patches, then cactus spines, then welded fleshy tubes. All of them were
  failures to reproduce, by hand, what thresholding a height field does for free. Recorded
  in `Documentation~/FUR.md` so it does not get re-litigated.

### Fur: shell / fin methods
- `_FurMethod` selects Shell / Fin / ShellAndFin. Shells are weakest exactly at the
  silhouette (you look along them); fins are strongest there.
- New `FUR_FIN` pass (geometry shader) on both fur variants: fins on triangle edges,
  `_FurFinJoints` rings so gravity and wind bend them, `_FurFinViewThreshold`,
  `_FurFinNormalBlend`, `_FurFinRandomDir`, two-sided via VFACE.
- PC only: `#pragma require geometry`, so the pass is skipped where unsupported and the
  material falls back to the shell look. Fin-only still issues the 16 shell draw calls
  (Built-in RP cannot switch a pass off from a material value); those collapse to a
  degenerate triangle and cost no fill.
- Both methods read the same field, so combining them does not double up the coat.

### Hatching
- Tone now comes from `shadingValue` instead of the luminance of the colour so far. Dark
  albedo (black clothing, dark hair) used to be hatched at full density in full light.
- Added `_HatchingComposite` (ShadowOnly / LitOnly / All), same enum as `_TopoComposite`
  and `_ShadowBokehComposite`. Default All keeps the previous behaviour.

### Showcase
- Removed the FakeShadow demo (a quad plus a sphere did not communicate the feature).
  The feature itself, its setup tool and `FAKE_SHADOW.md` are untouched.
- Auxiliary strip is one demo per row now; laying demos out side by side made them
  overlap, because each needs a different width.
- Added a fur method comparison (Shell / Fin / ShellAndFin) and a `_FUR` entry in
  `NataneShowcaseFeatureSetups` (the bare toggle showed nothing at default values).

### Verification status
- HLSL is compiled with `fxc` against Unity 2022.3 CGIncludes for every fur entry point
  (`furVert`, `furFrag`, `finVert`, `finGeom`, `finFrag`), in the plain and the
  `STEREO_INSTANCING_ON` configuration. No warnings.
- **Not verified visually.** Nothing in this session has been looked at in the editor;
  the fur appearance in particular has been iterated on blind from user reports.
  Regenerate the showcase and check before trusting any of it.

## 2026-03-25

### High quality map bake
- Added editor-side high quality bake settings to `MapGenSettings`.
- Implemented supersampled bake, normal-map-aware downsampling, and temporary mesh subdivision for map generation.
- Reused prepared meshes in `GenerateAll` so skinned pose baking and subdivision only happen once per batch.
- Exposed the new bake settings in the MapGenerator inspector, window, and material extension UI.
- Updated the normal auto-generate path so ShaderGUI can enable it from mesh-only scene context as well.

### VCC release prep
- Bumped the package version from `1.5.9` to `1.5.10` for the next VCC/VPM release.
- Synced the README version badge/text and added a `1.5.10` changelog entry.
