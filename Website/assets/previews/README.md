# Feature preview images

Drop feature screenshots here to replace the styled placeholder blocks on the
home page (and, later, doc pages).

## Naming convention

`assets/previews/<slug>.jpg`

The `<slug>` matches the `preview` key of each feature in `Website/home.js`.
The same `<slug>.jpg` also fills the placeholder block at the top of the matching
`params/effects/<slug>.html` doc page.
Current slugs:

- `face-ortho.jpg`   — Face Ortho Projection
- `ghost.jpg`        — Ghost variant (artifact-free transparency)
- `gpu-particles.jpg`— GPU Particles

### v1.6.x expression features (doc pages + What's New)

- `line-boil.jpg`        — Line Boil (hand-drawn frame-stepped wobble)
- `shaped-highlight.jpg` — Shaped Highlight (star / heart / cross speculars)
- `topographic.jpg`      — Topographic / Slice Lines
- `fx-modulator.jpg`     — FX Modulator
- `lenticular.jpg`       — Lenticular (angle-dependent artwork)
- `caustics.jpg`         — Surface Caustics
- `pixel-art.jpg`        — Pixel Art
- `xray.jpg`             — X-Ray variant (occluded silhouette)

## Recommended

- ~800x480 px, JPG or PNG (keep the extension `.jpg` or update the src in home.js)
- Dark background so it blends with the dark theme
- Show the *visual result* of the feature (before/after is ideal)

If a file is missing, `home.js` automatically falls back to the neon placeholder
block, so it is always safe to ship without every image present.
