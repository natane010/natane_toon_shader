using System;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>Natane Post FX のプリセット (MV でよく使うルックの出発点)。</summary>
    public static class NatanePostFXPresets
    {
        public sealed class Preset
        {
            public readonly string ja;
            public readonly string en;
            public readonly Action<NatanePostFX> apply;

            public Preset(string ja, string en, Action<NatanePostFX> apply)
            {
                this.ja = ja;
                this.en = en;
                this.apply = apply;
            }
        }

        /// <summary>全効果を初期値に戻す。シーン内の参照 (マスク対象・ピント対象・ライト・テクスチャ) は残す。</summary>
        public static void ResetAll(NatanePostFX fx)
        {
            GameObject[] targets = fx.mask.targets;
            Transform focusTarget = fx.focus.focusTarget;
            Light shaftLight = fx.glow.shaftLight;
            Texture2D lut = fx.color.lut;
            Texture overlayTexture = fx.overlay.texture;

            fx.lens = new NPFXLens();
            fx.glitch = new NPFXGlitch();
            fx.style = new NPFXStyle();
            fx.glow = new NPFXLight();
            fx.focus = new NPFXFocus();
            fx.mask = new NPFXMask();
            fx.color = new NPFXColor();
            fx.overlay = new NPFXOverlay();
            fx.retro = new NPFXRetro();
            fx.fog = new NPFXFog();
            fx.time = new NPFXTime();
            fx.beat = new NPFXBeat();

            fx.mask.targets = targets ?? new GameObject[0];
            fx.focus.focusTarget = focusTarget;
            fx.glow.shaftLight = shaftLight;
            fx.color.lut = lut;
            fx.overlay.texture = overlayTexture;
        }

        public static readonly Preset[] All =
        {
            new Preset("すべてオフ (初期値)", "Reset (everything off)", fx => { }),

            new Preset("アニメ撮影 (ディフュージョン＋フレア)", "Anime Compositing", fx =>
            {
                fx.glow.diffusion = 0.45f; fx.glow.diffusionRadius = 0.6f;
                fx.glow.bloom = 0.6f; fx.glow.threshold = 0.85f; fx.glow.scatter = 0.75f;
                fx.color.contrast = 1.06f; fx.color.saturation = 1.08f;
                fx.color.shadowTint = new Color(0.45f, 0.5f, 0.62f); fx.color.shadowTintAmount = 0.25f;
                fx.color.highlightTint = new Color(0.6f, 0.55f, 0.45f); fx.color.highlightTintAmount = 0.3f;
                fx.overlay.gradient = 0.18f; fx.overlay.blend = NPFXBlend.Screen;
                fx.overlay.gradientTop = new Color(0.55f, 0.4f, 0.25f); fx.overlay.gradientBottom = new Color(0.05f, 0.08f, 0.2f);
                fx.overlay.vignette = 0.25f;
            }),

            new Preset("シネマティック (シネスコ＋フィルム)", "Cinematic", fx =>
            {
                fx.overlay.letterbox = 2.39f; fx.overlay.vignette = 0.35f;
                fx.color.tonemap = NPFXTonemap.ACES; fx.color.exposure = 0.3f; fx.color.contrast = 1.08f;
                fx.color.shadowTint = new Color(0.35f, 0.5f, 0.55f); fx.color.shadowTintAmount = 0.35f;
                fx.color.highlightTint = new Color(0.65f, 0.55f, 0.4f); fx.color.highlightTintAmount = 0.35f;
                fx.glow.bloom = 0.5f; fx.glow.streak = 0.4f; fx.glow.streakLength = 0.7f;
                fx.lens.chromaticAberration = 0.12f; fx.lens.distortion = 0.04f;
                fx.retro.grain = 0.22f;
            }),

            new Preset("ポートレート (背景ボケ)", "Portrait Bokeh", fx =>
            {
                fx.focus.depthOfField = true; fx.focus.focusDistance = 2f; fx.focus.focusRange = 1.2f; fx.focus.bokehSize = 14f;
                fx.glow.bloom = 0.35f; fx.glow.diffusion = 0.2f;
                fx.overlay.vignette = 0.3f; fx.color.saturation = 1.05f;
            }),

            new Preset("夢・回想 (ソフトフォーカス)", "Dream / Flashback", fx =>
            {
                fx.glow.diffusion = 0.8f; fx.glow.diffusionRadius = 0.9f; fx.glow.bloom = 1.2f; fx.glow.threshold = 0.6f;
                fx.color.saturation = 0.75f; fx.color.exposure = 0.35f; fx.color.temperature = 0.2f;
                fx.overlay.vignette = 0.45f; fx.overlay.vignetteColor = Color.white; fx.overlay.vignetteSmoothness = 0.8f;
                fx.overlay.lightLeak = 0.35f; fx.lens.chromaticAberration = 0.2f;
            }),

            new Preset("VHS (ビデオテープ)", "VHS", fx =>
            {
                fx.glitch.vhs = 0.6f; fx.glitch.colorDrift = 0.35f; fx.glitch.lineJitter = 0.15f;
                fx.lens.rgbShift = 0.18f; fx.lens.crtCurvature = 0.15f;
                fx.retro.scanlines = 0.35f; fx.retro.scanlineCount = 240f; fx.retro.grain = 0.3f; fx.retro.grainSize = 2.5f;
                fx.retro.staticNoise = 0.04f;
                fx.color.saturation = 0.85f; fx.color.contrast = 0.92f; fx.color.lift = new Color(0.04f, 0.03f, 0.06f);
                fx.focus.blur = 0.25f; fx.focus.blurRadius = 0.2f; fx.overlay.vignette = 0.3f; fx.overlay.letterbox = 1.3333f;
            }),

            new Preset("ブラウン管モニター", "CRT Monitor", fx =>
            {
                fx.lens.crtCurvature = 0.45f; fx.retro.scanlines = 0.55f; fx.retro.scanlineCount = 300f;
                fx.retro.rgbMask = 0.5f; fx.glow.bloom = 0.6f; fx.glow.threshold = 0.6f;
                fx.overlay.vignette = 0.4f; fx.lens.chromaticAberration = 0.15f; fx.retro.flicker = 0.1f;
            }),

            new Preset("8mm フィルム", "8mm Film", fx =>
            {
                fx.retro.grain = 0.55f; fx.retro.grainSize = 2f; fx.retro.scratches = 0.6f; fx.retro.dust = 0.5f; fx.retro.flicker = 0.45f;
                fx.color.sepia = 0.45f; fx.color.contrast = 1.1f; fx.color.saturation = 0.8f;
                fx.overlay.vignette = 0.6f; fx.overlay.lightLeak = 0.5f; fx.overlay.letterbox = 1.3333f;
                fx.lens.shake = 0.06f; fx.lens.shakeSpeed = 14f; fx.time.frameHoldFps = 16f;
            }),

            new Preset("モノクロ映画", "Black & White Film", fx =>
            {
                fx.color.monochrome = 1f; fx.color.contrast = 1.25f; fx.retro.grain = 0.4f;
                fx.overlay.vignette = 0.45f; fx.glow.diffusion = 0.25f; fx.overlay.letterbox = 1.85f;
            }),

            new Preset("グリッチ (デジタルノイズ)", "Glitch", fx =>
            {
                fx.glitch.block = 0.5f; fx.glitch.blockCount = 18f; fx.glitch.lineJitter = 0.5f; fx.glitch.colorDrift = 0.6f;
                fx.glitch.verticalJump = 0.15f; fx.glitch.speed = 14f;
                fx.lens.rgbShift = 0.3f; fx.retro.scanlines = 0.2f; fx.retro.staticNoise = 0.05f;
            }),

            new Preset("サイバー / ネオン", "Cyber Neon", fx =>
            {
                fx.glow.bloom = 1.8f; fx.glow.threshold = 0.7f; fx.glow.bloomTint = new Color(0.8f, 0.9f, 1f);
                fx.glow.streak = 1.2f; fx.glow.streakLength = 0.85f; fx.glow.streakTint = new Color(0.4f, 0.7f, 1f);
                fx.lens.chromaticAberration = 0.35f;
                fx.color.shadowTint = new Color(0.35f, 0.3f, 0.75f); fx.color.shadowTintAmount = 0.5f;
                fx.color.highlightTint = new Color(0.8f, 0.4f, 0.7f); fx.color.highlightTintAmount = 0.4f;
                fx.color.contrast = 1.15f; fx.color.saturation = 1.2f; fx.retro.scanlines = 0.12f; fx.overlay.vignette = 0.35f;
            }),

            new Preset("漫画 (白黒トーン)", "Manga", fx =>
            {
                fx.style.edge = 1f; fx.style.edgeThickness = 1.2f; fx.style.halftone = 1f; fx.style.halftoneSize = 5f;
                fx.style.inkColor = Color.black; fx.style.paperColor = Color.white;
                fx.color.contrast = 1.2f;
            }),

            new Preset("アメコミ (カラー網点)", "Comic (color halftone)", fx =>
            {
                fx.style.posterize = 5f; fx.style.halftone = 0.55f; fx.style.halftoneColor = true; fx.style.halftoneSize = 5f;
                fx.style.edge = 1f; fx.style.edgeThickness = 1.5f;
                fx.color.saturation = 1.3f; fx.color.contrast = 1.1f;
            }),

            new Preset("鉛筆スケッチ", "Pencil Sketch", fx =>
            {
                fx.style.edge = 1f; fx.style.sketch = 0.85f; fx.style.crosshatch = 0.6f; fx.style.halftoneSize = 7f;
                fx.style.inkColor = new Color(0.15f, 0.15f, 0.18f); fx.style.paperColor = new Color(0.97f, 0.95f, 0.9f);
                fx.retro.grain = 0.15f;
            }),

            new Preset("油彩", "Oil Painting", fx =>
            {
                fx.style.kuwahara = 4f; fx.style.sharpen = 0.3f; fx.color.saturation = 1.15f; fx.color.contrast = 1.05f;
                fx.overlay.vignette = 0.2f;
            }),

            new Preset("レトロゲーム (ドット絵)", "Retro Game", fx =>
            {
                fx.lens.pixelate = 5f; fx.style.retroBits = 3f; fx.style.retroPixelSize = 5f;
                fx.color.saturation = 1.2f; fx.color.contrast = 1.1f;
            }),

            new Preset("集中線 (インパクト)", "Impact Lines", fx =>
            {
                fx.overlay.speedLines = 1f; fx.overlay.lineDensity = 140f; fx.overlay.lineInnerRadius = 0.32f;
                fx.lens.radialBlur = 0.25f; fx.color.contrast = 1.15f; fx.overlay.vignette = 0.2f;
            }),

            new Preset("夕焼け・エモ (光漏れ)", "Sunset Emo", fx =>
            {
                fx.color.temperature = 0.45f; fx.color.tint = 0.1f; fx.color.exposure = 0.2f; fx.color.vibrance = 0.3f;
                fx.overlay.lightLeak = 0.7f; fx.overlay.gradient = 0.3f; fx.overlay.blend = NPFXBlend.Screen;
                fx.overlay.gradientTop = new Color(0.7f, 0.35f, 0.2f); fx.overlay.gradientBottom = new Color(0.15f, 0.05f, 0.25f);
                fx.glow.bloom = 0.9f; fx.glow.shafts = 0.8f; fx.glow.diffusion = 0.35f; fx.overlay.vignette = 0.3f; fx.retro.grain = 0.15f;
            }),

            new Preset("キャラクター強調 (背景を落とす)", "Character Focus (needs mask targets)", fx =>
            {
                fx.mask.backgroundDesaturate = 0.8f; fx.mask.backgroundDarken = 0.45f; fx.mask.backgroundBlur = 0.8f;
                fx.mask.outline = 1f; fx.mask.outlineWidth = 6f; fx.mask.outlineColor = Color.white; fx.mask.outlineSoftness = 0.5f;
            }),

            new Preset("シルエット (影絵)", "Silhouette (needs mask targets)", fx =>
            {
                fx.mask.fill = 1f; fx.mask.fillColor = Color.black; fx.mask.outline = 0.8f; fx.mask.outlineWidth = 4f;
                fx.mask.outlineColor = new Color(1f, 0.9f, 0.7f); fx.mask.outlineSoftness = 1f;
                fx.color.gradientMap = 0.6f; fx.glow.bloom = 0.8f;
            }),

            new Preset("クラブ・ライブ (拍で動く)", "Club / Live (beat driven)", fx =>
            {
                fx.beat.bpm = 128f; fx.beat.zoom = 0.3f; fx.beat.flash = 0.12f; fx.beat.chromatic = 0.6f; fx.beat.bloom = 0.6f;
                fx.glow.bloom = 1f; fx.glow.streak = 0.6f; fx.time.afterimage = 0.5f; fx.time.lightTrail = 1f;
                fx.color.saturation = 1.2f; fx.color.contrast = 1.1f; fx.overlay.vignette = 0.3f;
            }),

            new Preset("万華鏡", "Kaleidoscope", fx =>
            {
                fx.lens.kaleidoscope = 8f; fx.glow.bloom = 0.8f; fx.color.saturation = 1.25f; fx.lens.chromaticAberration = 0.2f;
            }),

            new Preset("暗視スコープ", "Night Vision", fx =>
            {
                fx.color.nightVision = 1f; fx.retro.grain = 0.4f; fx.retro.scanlines = 0.25f; fx.overlay.vignette = 0.7f;
                fx.overlay.vignetteRoundness = 1f; fx.lens.distortion = 0.15f; fx.glow.bloom = 0.6f;
            }),

            new Preset("ミニチュア (チルトシフト)", "Miniature (tilt shift)", fx =>
            {
                fx.focus.tiltShift = 1f; fx.focus.tiltCenter = 0.45f; fx.focus.tiltWidth = 0.2f; fx.focus.bokehSize = 14f;
                fx.color.saturation = 1.35f; fx.color.contrast = 1.15f; fx.time.frameHoldFps = 12f;
            }),
        };
    }
}
