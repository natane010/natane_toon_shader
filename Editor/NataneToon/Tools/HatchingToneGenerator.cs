using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Hatching TAM / watercolor material generator.
    ///
    /// <c>_HATCHING</c> wants a 6-level Tonal Art Map packed as
    /// <c>_HatchTex0</c> (RGBA = levels 1-4) and <c>_HatchTex1</c> (RG = levels 5-6),
    /// and <c>_WATERCOLOR</c> wants granulation / paper textures. All four default to
    /// flat <c>"white"</c> or <c>"gray"</c>, so turning the features on without supplying
    /// artwork does nothing at all. Nothing in the package could produce that artwork —
    /// the only generator was DissolvePatternGenerator, which makes plain noise.
    ///
    /// The TAM levels are generated <b>cumulatively</b>: every level contains all the
    /// strokes of the lighter levels plus new ones. That is the defining requirement of a
    /// Tonal Art Map — if the strokes were independent per level, the lines would swap out
    /// at each level boundary and the surface would visibly flicker as lighting changes.
    ///
    /// Screen tone (<c>_SCREEN_TONE</c> / <c>_HALFTONE_SHADOW</c>) is deliberately out of
    /// scope: those are procedural and take no pattern texture, only a coverage mask, and
    /// masks are NataneMaskPainter's job.
    /// </summary>
    public class HatchingToneGenerator : EditorWindow
    {
        private enum Mode
        {
            HatchingTAM,
            Watercolor
        }

        private enum HatchPreset
        {
            Custom,
            Pencil,
            Crosshatch,
            Manga,
            Etching
        }

        private const int TamLevels = 6;

        private Mode _mode = Mode.HatchingTAM;
        private Material _targetMaterial;
        private Vector2 _scroll;

        // ---- Hatching parameters ----
        private HatchPreset _preset = HatchPreset.Crosshatch;
        private int _resolution = 512;
        private float _strokeAngle = 45f;
        private float _crossHatchAngle = 90f;
        private int _crossHatchFromLevel = 4;
        private float _lineWidth = 1.5f;
        private float _lineSoftness = 0.5f;
        private int _level1Density = 4;
        private float _densityCurve = 1.6f;
        private float _jitter = 0.15f;
        private float _taper = 0.3f;
        private int _seed;
        private bool _mipConsistency;

        // ---- Watercolor parameters ----
        private float _granulationScale = 64f;
        private float _granulationContrast = 0.5f;
        private float _paperFiberScale = 24f;
        private float _paperFiberDirection;
        private float _paperFiberAnisotropy = 0.6f;
        private float _paperContrast = 0.4f;

        // ---- Preview ----
        private Texture2D[] _previewLevels;
        private Texture2D _previewGradient;
        private Texture2D _previewGranulation;
        private Texture2D _previewPaper;

        [MenuItem(NataneToolMenuPaths.HatchingToneGenerator, false, 44)]
        public static void ShowWindow()
        {
            var window = GetWindow<HatchingToneGenerator>(
                L("ハッチング/水彩素材生成", "Hatching & Watercolor Generator"));
            window.minSize = new Vector2(540, 640);
            window.Show();
        }

        private void OnDisable()
        {
            DisposePreview();
        }

        // ================= Programmatic API =================

        /// <summary>
        /// Preset names usable from <see cref="GeneratePresetTam"/>. Kept as strings so
        /// callers outside this file do not need the private enum.
        /// </summary>
        public static readonly string[] PresetNames = { "Pencil", "Crosshatch", "Manga", "Etching" };

        /// <summary>
        /// Generate a preset TAM pair without opening the window.
        ///
        /// The debug showcase needs real hatching textures to show anything at all —
        /// <c>_HatchTex0</c> / <c>_HatchTex1</c> default to flat white, so a showcase sphere
        /// with <c>_HATCHING</c> enabled but no artwork would look identical to the baseline
        /// and silently "verify" nothing.
        /// </summary>
        /// <returns>False if the preset name is unknown.</returns>
        public static bool GeneratePresetTam(string presetName, int resolution, int seed,
                                             out Texture2D hatchTex0, out Texture2D hatchTex1)
        {
            hatchTex0 = null;
            hatchTex1 = null;

            if (!Enum.TryParse(presetName, out HatchPreset preset) || preset == HatchPreset.Custom)
            {
                return false;
            }

            // EditorWindow をインスタンス化せずに済むよう、パラメータ保持用の裸のインスタンスを作る。
            // ScriptableObject.CreateInstance は OnEnable を走らせるだけで表示はしない。
            var generator = CreateInstance<HatchingToneGenerator>();
            try
            {
                generator._preset = preset;
                generator.ApplyPreset(preset);
                generator._resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(resolution), 64, 1024);
                generator._seed = seed;

                List<Stroke> strokes = generator.BuildCumulativeStrokes(out int[] levelCounts);
                float[][] levels = generator.RasterizeAllLevels(strokes, levelCounts, generator._resolution);

                hatchTex0 = PackLevels(generator._resolution, levels[0], levels[1], levels[2], levels[3]);
                hatchTex1 = PackLevels(generator._resolution, levels[4], levels[5], null, null);
                return true;
            }
            finally
            {
                DestroyImmediate(generator);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("ハッチング/水彩素材ジェネレーター", "Hatching & Watercolor Generator"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("_HATCHING の6段TAMと、_WATERCOLOR の粒状感/紙目を生成します",
                  "Generate the 6-level TAM for _HATCHING and the granulation / paper textures for _WATERCOLOR"),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            _targetMaterial = (Material)EditorGUILayout.ObjectField(
                L("ターゲットマテリアル", "Target Material"), _targetMaterial, typeof(Material), false);

            _mode = (Mode)EditorGUILayout.EnumPopup(L("モード", "Mode"), _mode);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_mode == Mode.HatchingTAM)
            {
                DrawHatchingSettings();
                EditorGUILayout.Space(10);
                DrawHatchingPreview();
            }
            else
            {
                DrawWatercolorSettings();
                EditorGUILayout.Space(10);
                DrawWatercolorPreview();
            }

            EditorGUILayout.Space(10);
            DrawScopeHelp();

            EditorGUILayout.EndScrollView();
        }

        // ================= Hatching =================

        private void DrawHatchingSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("ハッチング TAM", "Hatching TAM"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _preset = (HatchPreset)EditorGUILayout.EnumPopup(L("プリセット", "Preset"), _preset);
            if (EditorGUI.EndChangeCheck() && _preset != HatchPreset.Custom)
            {
                ApplyPreset(_preset);
            }

            if (_preset == HatchPreset.Pencil)
            {
                EditorGUILayout.HelpBox(
                    L("鉛筆プリセットは Line Boil (_LINE_BOIL) の「ハッチングに適用」との併用を推奨します。線が揺れて手描き感が出ます。",
                      "The Pencil preset pairs well with Line Boil (_LINE_BOIL) \"affect hatching\" — the strokes wobble and read as hand-drawn."),
                    MessageType.Info);
            }

            EditorGUILayout.Space(4);

            _resolution = EditorGUILayout.IntPopup(L("解像度", "Resolution"), _resolution,
                new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
            _strokeAngle = EditorGUILayout.Slider(L("線の角度", "Stroke Angle"), _strokeAngle, 0f, 180f);
            _crossHatchAngle = EditorGUILayout.Slider(L("交差角度", "Cross Hatch Angle"), _crossHatchAngle, 0f, 180f);
            _crossHatchFromLevel = EditorGUILayout.IntSlider(
                L("交差を始める段", "Cross Hatch From Level"), _crossHatchFromLevel, 1, TamLevels);
            _lineWidth = EditorGUILayout.Slider(L("線の太さ (px)", "Line Width (px)"), _lineWidth, 0.5f, 6f);
            _lineSoftness = EditorGUILayout.Slider(L("線のぼけ", "Line Softness"), _lineSoftness, 0f, 2f);
            _level1Density = EditorGUILayout.IntSlider(L("最も明るい段の線数", "Level 1 Density"), _level1Density, 1, 32);
            _densityCurve = EditorGUILayout.Slider(L("密度カーブ", "Density Curve"), _densityCurve, 1.05f, 3f);
            _jitter = EditorGUILayout.Slider(L("揺らぎ", "Jitter"), _jitter, 0f, 1f);
            _taper = EditorGUILayout.Slider(L("線端の細り", "Taper"), _taper, 0f, 1f);
            _seed = EditorGUILayout.IntField(L("シード", "Seed"), _seed);

            _mipConsistency = EditorGUILayout.Toggle(L("ミップ一貫性", "Mip Consistency"), _mipConsistency);
            if (_mipConsistency)
            {
                EditorGUILayout.HelpBox(
                    L("PNG では手動ミップを保持できないため .asset (Texture2D) として保存します。\n" +
                      "各ミップを個別に描き、線幅をピクセル単位で一定に保つので、カメラを引いても" +
                      "ハッチングが消えません。",
                      "PNG cannot carry manual mips, so the result is saved as a .asset (Texture2D).\n" +
                      "Each mip is rasterized separately with a constant pixel-space line width, so the " +
                      "hatching does not fade out as the camera pulls back."),
                    MessageType.Info);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button(L("プレビューを更新", "Refresh Preview"), GUILayout.Height(24)))
            {
                BuildHatchingPreview();
            }

            if (GUILayout.Button(L("TAM を生成して保存", "Generate & Save TAM"), GUILayout.Height(30)))
            {
                GenerateAndSaveHatching();
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(HatchPreset preset)
        {
            switch (preset)
            {
                case HatchPreset.Pencil:
                    _strokeAngle = 55f; _crossHatchAngle = 0f; _crossHatchFromLevel = TamLevels;
                    _jitter = 0.25f; _taper = 0.5f; _level1Density = 3; _densityCurve = 1.8f;
                    break;
                case HatchPreset.Crosshatch:
                    _strokeAngle = 45f; _crossHatchAngle = 90f; _crossHatchFromLevel = 3;
                    _jitter = 0.1f; _taper = 0.3f; _level1Density = 4; _densityCurve = 1.5f;
                    break;
                case HatchPreset.Manga:
                    // 均一な平行線。揺らぎもテーパーも入れないのが漫画のカケアミらしさ。
                    _strokeAngle = 75f; _crossHatchAngle = 0f; _crossHatchFromLevel = TamLevels;
                    _jitter = 0.03f; _taper = 0f; _level1Density = 6; _densityCurve = 1.4f;
                    break;
                case HatchPreset.Etching:
                    _strokeAngle = 30f; _crossHatchAngle = 60f; _crossHatchFromLevel = 2;
                    _jitter = 0.05f; _taper = 0.3f; _level1Density = 8; _densityCurve = 1.3f;
                    break;
            }
        }

        private void DrawHatchingPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("プレビュー（6段）", "Preview (6 levels)"), EditorStyles.boldLabel);

            if (_previewLevels == null)
            {
                EditorGUILayout.LabelField(
                    L("「プレビューを更新」を押してください。", "Press \"Refresh Preview\"."),
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _previewLevels.Length; i++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(72));
                Rect r = GUILayoutUtility.GetRect(72, 72);
                if (_previewLevels[i] != null)
                {
                    EditorGUI.DrawPreviewTexture(r, _previewLevels[i], null, ScaleMode.ScaleToFit);
                }
                EditorGUILayout.LabelField("L" + (i + 1), EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                L("段を跨いだ連続性（左=明 → 右=暗）", "Continuity across levels (left = light, right = dark)"),
                EditorStyles.miniLabel);

            if (_previewGradient != null)
            {
                Rect gr = GUILayoutUtility.GetRect(0, 64, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(gr, _previewGradient, null, ScaleMode.StretchToFill);
                EditorGUILayout.LabelField(
                    L("段の境界で線が入れ替わる（＝ちらつく）と、この帯に縦の継ぎ目が出ます。",
                      "If strokes swapped at a level boundary (i.e. it would flicker), a vertical seam appears here."),
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Build the cumulative stroke list. Level N contains every stroke of level N-1,
        /// which is what stops the pattern from flickering at level boundaries.
        /// </summary>
        private List<Stroke> BuildCumulativeStrokes(out int[] levelCounts)
        {
            var rng = new System.Random(_seed);
            var strokes = new List<Stroke>();
            levelCounts = new int[TamLevels];

            for (int level = 1; level <= TamLevels; level++)
            {
                int target = Mathf.Max(1, Mathf.RoundToInt(_level1Density * Mathf.Pow(_densityCurve, level - 1)));

                while (strokes.Count < target)
                {
                    float angle = _strokeAngle + ((float)rng.NextDouble() * 2f - 1f) * _jitter * 30f;

                    // 交差線は「その段以降」かつ追加本数が偶数番目のときだけ。
                    // 全部を交差させると格子になってしまい、濃さの階調が作れない。
                    if (level >= _crossHatchFromLevel && _crossHatchAngle > 0.01f && (strokes.Count % 2) == 1)
                    {
                        angle += _crossHatchAngle;
                    }

                    float offset = PickBestCandidateOffset(strokes, angle, rng);
                    // Taper phase per stroke. Without it every stroke thins out at the
                    // same position along its own axis, and the thin points line up into
                    // a regular grid of gaps that reads as a pattern rather than as
                    // hand-drawn line ends.
                    float taperPhase = (float)rng.NextDouble();
                    strokes.Add(new Stroke { AngleDeg = angle, Offset = offset, TaperPhase = taperPhase });
                }

                levelCounts[level - 1] = strokes.Count;
            }

            return strokes;
        }

        /// <summary>
        /// Mitchell's best-candidate: pick the offset furthest from the strokes that already
        /// share this orientation. Purely random offsets clump into visible bands; a regular
        /// grid reads as a machine screen. This lands between the two.
        /// </summary>
        private static float PickBestCandidateOffset(List<Stroke> existing, float angleDeg, System.Random rng)
        {
            const int Candidates = 8;
            float best = 0f;
            float bestDistance = -1f;

            for (int c = 0; c < Candidates; c++)
            {
                float candidate = (float)rng.NextDouble();
                float nearest = float.MaxValue;

                foreach (Stroke s in existing)
                {
                    // 向きが大きく違う線とは競合しないので距離計算から外す。
                    if (Mathf.Abs(Mathf.DeltaAngle(s.AngleDeg, angleDeg)) > 20f) continue;

                    float d = Mathf.Abs(s.Offset - candidate);
                    d = Mathf.Min(d, 1f - d);   // 平行移動はトーラス上で巡回する
                    nearest = Mathf.Min(nearest, d);
                }

                if (nearest == float.MaxValue) return candidate;   // 同じ向きの線がまだ無い
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }

            return best;
        }

        private struct Stroke
        {
            public float AngleDeg;
            public float Offset;

            /// <summary>0-1. Where along the stroke the taper thins out.</summary>
            public float TaperPhase;
        }

        /// <summary>
        /// Rasterize all six cumulative levels in one pass.
        ///
        /// The levels are nested, so strokes are drawn incrementally into a running buffer
        /// and each level is snapshotted when its stroke count is reached. Rasterizing each
        /// level from scratch would redraw the early strokes six times — at 1024px that is
        /// several hundred million extra texel evaluations for no benefit.
        /// </summary>
        private float[][] RasterizeAllLevels(List<Stroke> strokes, int[] levelCounts, int resolution)
        {
            var levels = new float[TamLevels][];
            var running = new float[resolution * resolution];
            int drawn = 0;

            for (int level = 0; level < TamLevels; level++)
            {
                int target = Mathf.Min(levelCounts[level], strokes.Count);
                if (target > drawn)
                {
                    DrawStrokes(running, strokes, drawn, target, resolution);
                    drawn = target;
                }

                levels[level] = (float[])running.Clone();
            }

            return levels;
        }

        /// <summary>
        /// Draw strokes [<paramref name="from"/>, <paramref name="to"/>) into the buffer.
        /// Coverage is in [0,1] where 1 means "fully hatched" — the shader lerps toward the
        /// hatch color by this value, so a larger value really is darker.
        /// </summary>
        private void DrawStrokes(float[] buffer, List<Stroke> strokes, int from, int to, int resolution)
        {
            float halfWidth = _lineWidth * 0.5f;
            float softness = Mathf.Max(_lineSoftness, 0.001f);

            for (int i = from; i < to && i < strokes.Count; i++)
            {
                Stroke s = strokes[i];
                float rad = s.AngleDeg * Mathf.Deg2Rad;
                float dirX = Mathf.Cos(rad);
                float dirY = Mathf.Sin(rad);
                // 線の法線。テクセルから線までの符号付き距離をこれで測る。
                float nx = -dirY;
                float ny = dirX;

                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float u = (x + 0.5f) / resolution;
                        float v = (y + 0.5f) / resolution;

                        // 法線方向の座標を [0,1) に巻き取ることでタイル境界を跨いでも線が繋がる。
                        float proj = u * nx + v * ny;
                        float d = Mathf.Abs(Mathf.Repeat(proj - s.Offset + 0.5f, 1f) - 0.5f) * resolution;

                        // Taper: 線に沿った位置で幅を細らせる。両端が細くなり手描きらしくなる。
                        // ストロークごとに位相をずらしているので、細る位置が揃って
                        // 規則的な隙間の並びにならない。
                        float along = Mathf.Repeat(u * dirX + v * dirY + s.TaperPhase, 1f);
                        float taperFactor = Mathf.Lerp(1f, Mathf.Sin(along * Mathf.PI), _taper);
                        float w = halfWidth * Mathf.Max(taperFactor, 0.05f);

                        float coverage = 1f - Mathf.SmoothStep(w - softness, w + softness, d);
                        if (coverage <= 0f) continue;

                        int idx = y * resolution + x;
                        // 線が重なった箇所は max で合成する。加算だと重なりだけ真っ黒になる。
                        if (coverage > buffer[idx]) buffer[idx] = coverage;
                    }
                }
            }
        }

        private void BuildHatchingPreview()
        {
            DisposePreview();

            const int PreviewRes = 128;
            List<Stroke> strokes = BuildCumulativeStrokes(out int[] levelCounts);
            float[][] levels = RasterizeAllLevels(strokes, levelCounts, PreviewRes);

            _previewLevels = new Texture2D[TamLevels];

            for (int i = 0; i < TamLevels; i++)
            {
                var tex = new Texture2D(PreviewRes, PreviewRes, TextureFormat.RGBA32, false, true);
                var colors = new Color[PreviewRes * PreviewRes];
                for (int p = 0; p < colors.Length; p++)
                {
                    // 表示は「濃いほど暗い」ほうが直感的なので反転して見せる。
                    float shade = 1f - levels[i][p];
                    colors[p] = new Color(shade, shade, shade, 1f);
                }
                tex.SetPixels(colors);
                tex.Apply(false, false);
                _previewLevels[i] = tex;
            }

            // 段を連続的に跨ぐ帯。TAM が累積になっていれば継ぎ目が出ない。
            const int BandW = 384;
            const int BandH = 64;
            _previewGradient = new Texture2D(BandW, BandH, TextureFormat.RGBA32, false, true);
            var band = new Color[BandW * BandH];
            for (int x = 0; x < BandW; x++)
            {
                float t = x / (float)(BandW - 1);
                float lum = t * TamLevels;
                int lo = Mathf.Clamp(Mathf.FloorToInt(lum), 0, TamLevels - 1);
                int hi = Mathf.Clamp(lo + 1, 0, TamLevels - 1);
                float frac = Mathf.Clamp01(lum - lo);

                for (int y = 0; y < BandH; y++)
                {
                    int px = (int)(x / (float)BandW * PreviewRes);
                    int py = (int)(y / (float)BandH * PreviewRes);
                    int idx = py * PreviewRes + px;
                    float value = Mathf.Lerp(levels[lo][idx], levels[hi][idx], frac);
                    float shade = 1f - value;
                    band[y * BandW + x] = new Color(shade, shade, shade, 1f);
                }
            }
            _previewGradient.SetPixels(band);
            _previewGradient.Apply(false, false);
        }

        private void GenerateAndSaveHatching()
        {
            string folder = EditorUtility.SaveFolderPanel(
                L("TAM の保存先を選択", "Select TAM output folder"), "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;

            string assetFolder = ToAssetPath(folder);
            if (assetFolder == null)
            {
                EditorUtility.DisplayDialog(
                    L("保存できません", "Cannot Save"),
                    L("プロジェクトの Assets 配下を選んでください。", "Choose a folder inside the project's Assets."),
                    "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hatching TAM", "Building strokes...", 0.1f);
                List<Stroke> strokes = BuildCumulativeStrokes(out int[] levelCounts);

                // TAM のパッキング: Tex0 の RGBA = L1..L4、Tex1 の RG = L5..L6。
                EditorUtility.DisplayProgressBar("Hatching TAM", "Rasterizing levels...", 0.3f);
                float[][] levels = RasterizeAllLevels(strokes, levelCounts, _resolution);

                EditorUtility.DisplayProgressBar("Hatching TAM", "Writing textures...", 0.85f);

                Texture2D tex0 = PackLevels(_resolution, levels[0], levels[1], levels[2], levels[3]);
                Texture2D tex1 = PackLevels(_resolution, levels[4], levels[5], null, null);

                if (_mipConsistency)
                {
                    BuildManualMips(tex0, strokes, levelCounts, 0);
                    BuildManualMips(tex1, strokes, levelCounts, 4);
                }

                string baseName = "Hatching_" + _preset;
                string path0 = SaveTexture(tex0, assetFolder, baseName + "_HatchTex0", _mipConsistency);
                string path1 = SaveTexture(tex1, assetFolder, baseName + "_HatchTex1", _mipConsistency);

                AssetDatabase.Refresh();

                var saved0 = AssetDatabase.LoadAssetAtPath<Texture2D>(path0);
                var saved1 = AssetDatabase.LoadAssetAtPath<Texture2D>(path1);
                AssignHatchingToMaterial(saved0, saved1);

                EditorUtility.DisplayDialog(
                    L("生成完了", "Generation Complete"),
                    L($"ハッチング TAM を生成しました。\n{path0}\n{path1}",
                      $"Generated the hatching TAM.\n{path0}\n{path1}"),
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Texture2D PackLevels(int resolution, float[] r, float[] g, float[] b, float[] a)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true);
            var colors = new Color[resolution * resolution];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(
                    r != null ? r[i] : 0f,
                    g != null ? g[i] : 0f,
                    b != null ? b[i] : 0f,
                    a != null ? a[i] : 0f);
            }
            tex.SetPixels(colors);
            // updateMipmaps: true. The texture is created with a mip chain, and mip levels
            // 1+ are uninitialized until something fills them. Applying with `false` left
            // them as garbage — and because a TAM value of 1 means "fully hatched", garbage
            // mips render as solid hatch colour, i.e. the surface goes black as soon as the
            // sampler drops below mip 0.
            //
            // BuildManualMips overwrites these afterwards when mip consistency is on; it
            // applies with `false` so it does not undo its own work.
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>
        /// Rasterize each mip separately instead of box-filtering the base level.
        /// A downsampled TAM loses stroke contrast until the hatching disappears at
        /// distance; re-rasterizing at a constant pixel width keeps every mip readable.
        /// </summary>
        private void BuildManualMips(Texture2D tex, List<Stroke> strokes, int[] levelCounts, int levelOffset)
        {
            int mipCount = tex.mipmapCount;
            for (int mip = 1; mip < mipCount; mip++)
            {
                int res = Mathf.Max(1, _resolution >> mip);
                float[][] mipLevels = RasterizeAllLevels(strokes, levelCounts, res);

                var channels = new float[4][];
                for (int c = 0; c < 4; c++)
                {
                    int level = levelOffset + c;
                    channels[c] = level < TamLevels ? mipLevels[level] : null;
                }

                var colors = new Color[res * res];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(
                        channels[0] != null ? channels[0][i] : 0f,
                        channels[1] != null ? channels[1][i] : 0f,
                        channels[2] != null ? channels[2][i] : 0f,
                        channels[3] != null ? channels[3][i] : 0f);
                }
                tex.SetPixels(colors, mip);
            }
            tex.Apply(false, false);
        }

        private void AssignHatchingToMaterial(Texture2D tex0, Texture2D tex1)
        {
            if (_targetMaterial == null || tex0 == null) return;
            if (!_targetMaterial.HasProperty("_HatchTex0")) return;   // 対象シェーダーでない

            Undo.RecordObject(_targetMaterial, "Assign Hatching Textures");

            _targetMaterial.SetTexture("_HatchTex0", tex0);
            if (tex1 != null && _targetMaterial.HasProperty("_HatchTex1"))
            {
                _targetMaterial.SetTexture("_HatchTex1", tex1);
            }

            if (_targetMaterial.HasProperty("_UseHatching"))
            {
                _targetMaterial.SetFloat("_UseHatching", 1f);
            }
            _targetMaterial.EnableKeyword("_HATCHING");

            // タイリングの推奨値: 解像度が上がるほど 1 タイルあたりの線が細くなるので、
            // 見かけの線密度が揃うようにスケールする。512 を基準にした。
            if (_targetMaterial.HasProperty("_HatchingTiling"))
            {
                float recommended = Mathf.Max(1f, _resolution / 512f * 4f);
                _targetMaterial.SetFloat("_HatchingTiling", recommended);
            }

            EditorUtility.SetDirty(_targetMaterial);
        }

        // ================= Watercolor =================

        private void DrawWatercolorSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("水彩素材", "Watercolor Materials"), EditorStyles.boldLabel);

            _resolution = EditorGUILayout.IntPopup(L("解像度", "Resolution"), _resolution,
                new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("粒状感", "Granulation"), EditorStyles.miniBoldLabel);
            _granulationScale = EditorGUILayout.Slider(L("粒の細かさ", "Granulation Scale"), _granulationScale, 8f, 256f);
            _granulationContrast = EditorGUILayout.Slider(L("コントラスト", "Contrast"), _granulationContrast, 0f, 1f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("紙目", "Paper"), EditorStyles.miniBoldLabel);
            _paperFiberScale = EditorGUILayout.Slider(L("繊維の粗さ", "Fiber Scale"), _paperFiberScale, 4f, 128f);
            _paperFiberDirection = EditorGUILayout.Slider(L("繊維の方向", "Fiber Direction"), _paperFiberDirection, 0f, 180f);
            _paperFiberAnisotropy = EditorGUILayout.Slider(L("方向性の強さ", "Anisotropy"), _paperFiberAnisotropy, 0f, 1f);
            _paperContrast = EditorGUILayout.Slider(L("紙目の強さ", "Paper Contrast"), _paperContrast, 0f, 1f);
            _seed = EditorGUILayout.IntField(L("シード", "Seed"), _seed);

            EditorGUILayout.HelpBox(
                L("どちらも平均 0.5 に正規化して出力します。_WCGranulationTex の既定が \"gray\" なので、" +
                  "正規化しないと割り当てた瞬間に全体の明度が変わってしまいます。\n" +
                  "紙目は加算的に使われるため、Paper Intensity は 0.3 前後を推奨します。",
                  "Both are normalized to a mean of 0.5. _WCGranulationTex defaults to \"gray\", so without " +
                  "normalization assigning the texture would shift overall brightness.\n" +
                  "The paper texture is used additively — a Paper Intensity around 0.3 is a good start."),
                MessageType.Info);

            EditorGUILayout.Space(8);
            if (GUILayout.Button(L("プレビューを更新", "Refresh Preview"), GUILayout.Height(24)))
            {
                BuildWatercolorPreview();
            }

            if (GUILayout.Button(L("水彩素材を生成して保存", "Generate & Save Watercolor"), GUILayout.Height(30)))
            {
                GenerateAndSaveWatercolor();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWatercolorPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("プレビュー", "Preview"), EditorStyles.boldLabel);

            if (_previewGranulation == null && _previewPaper == null)
            {
                EditorGUILayout.LabelField(
                    L("「プレビューを更新」を押してください。", "Press \"Refresh Preview\"."),
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawLabeledPreview(_previewGranulation, L("粒状感", "Granulation"));
            DrawLabeledPreview(_previewPaper, L("紙目", "Paper"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static void DrawLabeledPreview(Texture2D tex, string label)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(160));
            Rect r = GUILayoutUtility.GetRect(160, 160);
            if (tex != null) EditorGUI.DrawPreviewTexture(r, tex, null, ScaleMode.ScaleToFit);
            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private float[] BuildGranulation(int resolution)
        {
            var values = new float[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (float)resolution;
                    float v = y / (float)resolution;
                    // 2〜3 オクターブ。細かい粒が主で、大きなムラを少し重ねる。
                    float n = TiledValueNoise(u, v, _granulationScale, _seed) * 0.6f
                            + TiledValueNoise(u, v, _granulationScale * 0.37f, _seed + 101) * 0.4f;
                    values[y * resolution + x] = n;
                }
            }

            NormalizeToHalf(values, _granulationContrast);
            return values;
        }

        private float[] BuildPaper(int resolution)
        {
            var values = new float[resolution * resolution];
            float rad = _paperFiberDirection * Mathf.Deg2Rad;
            float dirX = Mathf.Cos(rad);
            float dirY = Mathf.Sin(rad);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (float)resolution;
                    float v = y / (float)resolution;

                    // 繊維方向へノイズ座標を圧縮すると、その向きに伸びた筋になる。
                    float stretch = Mathf.Lerp(1f, 0.15f, _paperFiberAnisotropy);
                    float along = (u * dirX + v * dirY);
                    float across = (-u * dirY + v * dirX);
                    float fu = along * stretch;
                    float fv = across;

                    float n = TiledValueNoise(fu, fv, _paperFiberScale, _seed + 7) * 0.65f
                            + TiledValueNoise(u, v, _paperFiberScale * 2.3f, _seed + 13) * 0.35f;
                    values[y * resolution + x] = n;
                }
            }

            NormalizeToHalf(values, _paperContrast);
            return values;
        }

        /// <summary>
        /// Rescale so the mean lands exactly on 0.5 and the spread matches the requested
        /// contrast. Both watercolor slots are neutral at 0.5, so an off-center mean would
        /// visibly shift brightness the moment the texture is assigned.
        /// </summary>
        private static void NormalizeToHalf(float[] values, float contrast)
        {
            double sum = 0;
            for (int i = 0; i < values.Length; i++) sum += values[i];
            float mean = (float)(sum / values.Length);

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Mathf.Clamp01(0.5f + (values[i] - mean) * contrast);
            }
        }

        private void GenerateAndSaveWatercolor()
        {
            string folder = EditorUtility.SaveFolderPanel(
                L("水彩素材の保存先を選択", "Select watercolor output folder"), "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;

            string assetFolder = ToAssetPath(folder);
            if (assetFolder == null)
            {
                EditorUtility.DisplayDialog(
                    L("保存できません", "Cannot Save"),
                    L("プロジェクトの Assets 配下を選んでください。", "Choose a folder inside the project's Assets."),
                    "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Watercolor", "Generating granulation...", 0.2f);
                Texture2D granulation = ToGrayscaleTexture(BuildGranulation(_resolution), _resolution);

                EditorUtility.DisplayProgressBar("Watercolor", "Generating paper...", 0.6f);
                Texture2D paper = ToGrayscaleTexture(BuildPaper(_resolution), _resolution);

                string granPath = SaveTexture(granulation, assetFolder, "Watercolor_Granulation", false);
                string paperPath = SaveTexture(paper, assetFolder, "Watercolor_Paper", false);

                AssetDatabase.Refresh();

                var savedGran = AssetDatabase.LoadAssetAtPath<Texture2D>(granPath);
                var savedPaper = AssetDatabase.LoadAssetAtPath<Texture2D>(paperPath);
                AssignWatercolorToMaterial(savedGran, savedPaper);

                EditorUtility.DisplayDialog(
                    L("生成完了", "Generation Complete"),
                    L($"水彩素材を生成しました。\n{granPath}\n{paperPath}",
                      $"Generated the watercolor textures.\n{granPath}\n{paperPath}"),
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void AssignWatercolorToMaterial(Texture2D granulation, Texture2D paper)
        {
            if (_targetMaterial == null) return;
            if (!_targetMaterial.HasProperty("_WCGranulationTex")) return;

            Undo.RecordObject(_targetMaterial, "Assign Watercolor Textures");

            if (granulation != null) _targetMaterial.SetTexture("_WCGranulationTex", granulation);
            if (paper != null && _targetMaterial.HasProperty("_WCPaperTex"))
            {
                _targetMaterial.SetTexture("_WCPaperTex", paper);
            }

            if (_targetMaterial.HasProperty("_UseWatercolor"))
            {
                _targetMaterial.SetFloat("_UseWatercolor", 1f);
            }
            _targetMaterial.EnableKeyword("_WATERCOLOR");

            // 紙目は加算的に効くので、既定の強さは控えめにしておく。
            if (_targetMaterial.HasProperty("_WCPaperIntensity"))
            {
                _targetMaterial.SetFloat("_WCPaperIntensity", 0.3f);
            }

            EditorUtility.SetDirty(_targetMaterial);
        }

        private void BuildWatercolorPreview()
        {
            DisposePreview();
            const int PreviewRes = 160;
            _previewGranulation = ToGrayscaleTexture(BuildGranulationAt(PreviewRes), PreviewRes);
            _previewPaper = ToGrayscaleTexture(BuildPaperAt(PreviewRes), PreviewRes);
        }

        private float[] BuildGranulationAt(int resolution)
        {
            int saved = _resolution;
            _resolution = resolution;
            try { return BuildGranulation(resolution); }
            finally { _resolution = saved; }
        }

        private float[] BuildPaperAt(int resolution)
        {
            int saved = _resolution;
            _resolution = resolution;
            try { return BuildPaper(resolution); }
            finally { _resolution = saved; }
        }

        // ================= Shared helpers =================

        /// <summary>
        /// Value noise on a torus so the result tiles seamlessly. Both hatching and
        /// watercolor textures are sampled with Repeat wrapping, and a visible tile seam
        /// on a face is immediately obvious.
        /// </summary>
        private static float TiledValueNoise(float u, float v, float scale, int seed)
        {
            int period = Mathf.Max(2, Mathf.RoundToInt(scale));
            float x = u * period;
            float y = v * period;

            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(Mod(xi, period), Mod(yi, period), seed);
            float b = Hash(Mod(xi + 1, period), Mod(yi, period), seed);
            float c = Hash(Mod(xi, period), Mod(yi + 1, period), seed);
            float d = Hash(Mod(xi + 1, period), Mod(yi + 1, period), seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Mod(int value, int period)
        {
            int r = value % period;
            return r < 0 ? r + period : r;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFF) / (float)0x7FFFFFF;
            }
        }

        private static Texture2D ToGrayscaleTexture(float[] values, int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true);
            var colors = new Color[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                colors[i] = new Color(values[i], values[i], values[i], 1f);
            }
            tex.SetPixels(colors);
            // ミップを生成する。生成しないと 1 段目以降が未初期化のまま残る。
            tex.Apply(true, false);
            return tex;
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.Ordinal)) return null;
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        /// <summary>
        /// Save as PNG by default. Manual mips cannot survive a PNG round-trip, so when
        /// mip consistency is requested the texture is stored as a .asset instead.
        /// </summary>
        private static string SaveTexture(Texture2D tex, string assetFolder, string name, bool asAsset)
        {
            if (asAsset)
            {
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{assetFolder}/{name}.asset");
                AssetDatabase.CreateAsset(tex, assetPath);
                AssetDatabase.SaveAssets();
                return assetPath;
            }

            string pngPath = AssetDatabase.GenerateUniqueAssetPath($"{assetFolder}/{name}.png");
            byte[] bytes = tex.EncodeToPNG();

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return null;

            string absolute = Path.Combine(projectRoot, pngPath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            try
            {
                File.WriteAllBytes(absolute, bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Natane Toon] Failed to write texture: {pngPath}\n{e.Message}");
                return null;
            }

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(pngPath);
            return pngPath;
        }

        private static void ConfigureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            // Linear: これらは色ではなく係数として読まれるため sRGB 変換をかけてはいけない。
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private void DrawScopeHelp()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("このツールの対象範囲", "What this tool covers"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("網点（_SCREEN_TONE / _HALFTONE_SHADOW）は対象外です。これらはプロシージャル実装で、\n" +
                  "パターンテクスチャを取りません（受け取るのは適用範囲マスクのみ）。\n" +
                  "適用範囲マスクの作成は Mask Painter の担当です。",
                  "Screen tone (_SCREEN_TONE / _HALFTONE_SHADOW) is out of scope. Those are procedural and\n" +
                  "take no pattern texture — only a coverage mask, which is Mask Painter's job."),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DisposePreview()
        {
            if (_previewLevels != null)
            {
                foreach (Texture2D tex in _previewLevels)
                {
                    if (tex != null) DestroyImmediate(tex);
                }
                _previewLevels = null;
            }

            if (_previewGradient != null) { DestroyImmediate(_previewGradient); _previewGradient = null; }
            if (_previewGranulation != null) { DestroyImmediate(_previewGranulation); _previewGranulation = null; }
            if (_previewPaper != null) { DestroyImmediate(_previewPaper); _previewPaper = null; }
        }
    }
}
