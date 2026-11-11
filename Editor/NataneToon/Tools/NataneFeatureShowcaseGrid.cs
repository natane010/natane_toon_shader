using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// 表現機能を球体の行列（コンタクトシート）として並べるデバッグ用ツール。
    ///
    /// 1 行 = 1 つの機能、1 列 = そのバリエーション。
    /// 全球体を XY 平面上（Z=0）に並べ、同じ向き・同じ高さでカメラに正対させる。
    /// 奥行きを付けたり床に寝かせたりすると、位置による陰影差が混ざって
    /// マテリアル同士の比較にならないため。
    ///
    /// 球体を使うのは、1 つの中で N·L が明部から影部まで連続して変化するから。
    /// 色のバリエーションや影の表現は、階調ごとの見え方が分からないと比較できない。
    ///
    /// 生成物は Tests/Verification/Showcase 配下（.gitignore 済み）。
    /// </summary>
    internal static class NataneFeatureShowcaseGrid
    {
        private const string ShaderName = "Natane/Toon Shader";
        internal const string OutputFolder = "Assets/NataneToon/Tests/Verification/Showcase";
        private const string RootName = "NataneFeatureShowcase";

        /// <summary>
        /// 自動カバレッジ区画で 1 行に並べる球体の数。
        /// これを超えたら折り返す。
        /// </summary>
        private const int CoverageColumns = 8;

        /// <summary>
        /// 生成アセットの世代。生成アルゴリズムを変えたら上げること。
        ///
        /// 生成物はパス存在チェックでキャッシュしているため、これが無いと
        /// アルゴリズムを直しても古いテクスチャが再利用され、
        /// 「修正したのに直っていない」という誤った検証結果になる。
        /// </summary>
        internal const string GeneratedAssetVersion = "v7";

        // 球の直径は 2。ラベルは球の手前の床に寝かせて置くので、
        // 行間は「直径 + ラベルの高さ + 余白」が要る。
        // 4.6 でもまだラベルが前の行の球に触っていたため、直径の 3 倍以上を確保する。
        private const float ColumnSpacing = 5.0f;
        private const float RowSpacing = 7.0f;

        /// <summary>球の中心からラベルまでの距離。球の半径 1 ＋ 余白。</summary>
        private const float LabelDrop = 2.2f;

        /// <summary>行ラベル（左端）をグリッドからどれだけ離すか。</summary>
        private const float RowLabelMargin = 4.5f;

        private sealed class Cell
        {
            public string Label;
            public Action<Material> Configure;

            public Cell(string label, Action<Material> configure)
            {
                Label = label;
                Configure = configure;
            }
        }

        private sealed class Row
        {
            public string Label;
            public List<Cell> Cells;

            public Row(string label, params Cell[] cells)
            {
                Label = label;
                Cells = new List<Cell>(cells);
            }
        }

        [MenuItem("Tools/Natane/開発 Dev/表現ショーケース（球体グリッド）を生成", false, 2001)]
        public static void Create()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("表現ショーケース",
                    $"シェーダー \"{ShaderName}\" が見つかりませんでした。", "OK");
                return;
            }

            EnsureFolder();
            List<Row> rows = BuildRows();

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Natane Feature Showcase");

            Font font = LoadBuiltinFont();

            int maxColumns = 0;
            foreach (Row r in rows) maxColumns = Mathf.Max(maxColumns, r.Cells.Count);

            float gridWidth = (maxColumns - 1) * ColumnSpacing;
            float gridHeight = (rows.Count - 1) * RowSpacing;

            // 床は最後に作る。カバレッジ区画や補助区画がどこまで伸びるかは
            // 並べ終わるまで分からないため、先に作ると必ず足りなくなる。

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                Row row = rows[rowIndex];

                // 手前（+Z）から奥（-Z）へ。Hierarchy の並びと見た目の並びを一致させる。
                float z = -gridHeight * 0.5f + rowIndex * RowSpacing;

                CreateLabel(root.transform, font, row.Label,
                    new Vector3(-gridWidth * 0.5f - RowLabelMargin, 0.02f, z), 0.45f, TextAnchor.MiddleRight);

                for (int col = 0; col < row.Cells.Count; col++)
                {
                    Cell cell = row.Cells[col];
                    float x = -gridWidth * 0.5f + col * ColumnSpacing;

                    Material material = CreateOrUpdateMaterial(shader,
                        $"Showcase_{rowIndex:00}_{col:00}_{Sanitize(row.Label)}_{Sanitize(cell.Label)}",
                        cell.Configure);

                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = $"{rowIndex:00}-{col:00} {row.Label} / {cell.Label}";
                    sphere.transform.SetParent(root.transform, false);

                    // 全球体を同じ高さに置く。高さを変えると陰影条件が変わり比較にならない。
                    sphere.transform.localPosition = new Vector3(x, 1f, z);
                    sphere.transform.localScale = Vector3.one * 2f;

                    sphere.GetComponent<Renderer>().sharedMaterial = material;
                    Undo.RegisterCreatedObjectUndo(sphere, "Create Natane Feature Showcase");

                    // ラベルは球体の手前の床に寝かせる。真上から見て読める向き。
                    CreateLabel(root.transform, font, cell.Label,
                        new Vector3(x, 0.02f, z - LabelDrop), 0.32f, TextAnchor.UpperCenter);
                }
            }

            // 球体マテリアルでは確認できない機能は、専用のオブジェクトを別区画に置く。
            // グリッドの奥（+Z 側）へ並べるので、上から見たときに混ざらない。
            CreateAuxiliarySection(root.transform, font, gridHeight);

            // 手書き行が触れていない機能を、キーワード管理表から自動で並べる。
            // これがあるので「新機能を足したらショーケースにも追記する」運用が要らない。
            List<CoverageEntry> coverage = CreateCoverageSection(
                root.transform, font, rows, gridWidth, gridHeight);
            string reportPath = WriteCoverageReport(coverage, rows);

            CreateLegend(root.transform, font, gridWidth, gridHeight);

            // 全部並べ終わってから、実際に使った範囲を測って床を敷く。
            CreateFloorToFit(root.transform, shader);

            EnsureDirectionalLight(root.transform);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null) view.FrameSelected();

            int withSetup = coverage.Count(e => !e.Unavailable && e.HasSetup);
            int bare = coverage.Count(e => !e.Unavailable && !e.HasSetup);
            int unavailable = coverage.Count(e => e.Unavailable);

            EditorUtility.DisplayDialog("表現ショーケース",
                $"手書き比較: {rows.Count} 行 × 最大 {maxColumns} 列\n" +
                $"自動カバレッジ: {withSetup + bare} 機能" +
                $"（素材設定済み {withSetup} / 素のトグル {bare}）\n" +
                (unavailable > 0 ? $"配置できなかった機能: {unavailable} 件\n" : string.Empty) +
                "\n" +
                "全球体が同じ高さ・同一ライティングなので、差分はマテリアル由来だけです。\n\n" +
                "「（素）」付きの球体はパラメータ既定値のままなので、\n" +
                "Baseline と見た目が変わらないことがあります。\n" +
                $"内訳は {reportPath} を参照してください。\n\n" +
                "アニメーションする表現は Scene ビューの Always Refresh を有効にしてください。\n\n" +
                "取り消しは Ctrl+Z です。",
                "OK");
        }

        // ---- 行の定義 ----

        private static List<Row> BuildRows()
        {
            return new List<Row>
            {
                // 何も付けない基準。他の行はこれと見比べる。
                new Row("基準（効果なし）",
                    new Cell("Baseline", m => { })),

                new Row("影の自然さ",
                    new Cell("0 (従来)", m => Set(m, "_ShadowNaturalness", 0f)),
                    new Cell("0.25",     m => Set(m, "_ShadowNaturalness", 0.25f)),
                    new Cell("0.5",      m => Set(m, "_ShadowNaturalness", 0.5f)),
                    new Cell("0.75",     m => Set(m, "_ShadowNaturalness", 0.75f)),
                    new Cell("1.0",      m => Set(m, "_ShadowNaturalness", 1f))),

                new Row("シェーディング",
                    new Cell("Toon",         m => Set(m, "_ShadingMode", 0f)),
                    new Cell("Gradient",     m => Set(m, "_ShadingMode", 1f)),
                    new Cell("StandardToon", m => Set(m, "_ShadingMode", 2f)),
                    new Cell("PBRLike",      m => Set(m, "_ShadingMode", 3f))),

                new Row("階調数",
                    new Cell("1段", m => Set(m, "_ShadowSteps", 1f)),
                    new Cell("2段", m => Set(m, "_ShadowSteps", 2f)),
                    new Cell("3段", m => Set(m, "_ShadowSteps", 3f)),
                    new Cell("4段", m => Set(m, "_ShadowSteps", 4f)),
                    new Cell("6段", m => Set(m, "_ShadowSteps", 6f))),

                // 色の違いは階調ごとの出方が分からないと判断できないので、
                // シェーディングが一巡する球体で見るのが要点。
                new Row("影の色",
                    new Cell("既定",   m => { }),
                    new Cell("青寄り", m => SetColor(m, "_ShadowColor", new Color(0.35f, 0.42f, 0.70f, 1f))),
                    new Cell("赤寄り", m => SetColor(m, "_ShadowColor", new Color(0.70f, 0.38f, 0.40f, 1f))),
                    new Cell("彩度高", m => { Set(m, "_ShadowSaturation", 1.8f); Set(m, "_ShadowHueShift", 0.1f); })),

                new Row("漫画網点",
                    new Cell("ドット",   m => { Halftone(m); Set(m, "_HalftoneShadowPattern", 0f); }),
                    new Cell("万線",     m => { Halftone(m); Set(m, "_HalftoneShadowPattern", 1f); }),
                    new Cell("カケアミ", m => { Halftone(m); Set(m, "_HalftoneShadowPattern", 2f); })),

                // 落ち影を網点にするかどうか。上の球が落とす影を受ける板を
                // 補助区画に置いてあるので、そちらで実際の落ち影を確認できる。
                new Row("網点と落ち影",
                    new Cell("落ち影あり", m => { Halftone(m); Set(m, "_HalftoneShadowCastShadow", 1f); }),
                    new Cell("落ち影なし", m => { Halftone(m); Set(m, "_HalftoneShadowCastShadow", 0f); })),

                // 濃度の基準。トゥーン量子化後を基準にすると、階調数ぶんしか濃度が
                // 取れない（既定 2 段なら網点も 2 段階）。連続を基準にすると
                // 面の丸みに沿って号数が変わり、漫画のトーンらしくなる。
                new Row("網点の濃度基準",
                    new Cell("量子化後 2段", m => { Halftone(m); Set(m, "_ShadowSteps", 2f); Set(m, "_HalftoneShadowDensitySource", 0f); }),
                    new Cell("量子化後 4段", m => { Halftone(m); Set(m, "_ShadowSteps", 4f); Set(m, "_HalftoneShadowDensitySource", 0f); }),
                    new Cell("連続 2段",     m => { Halftone(m); Set(m, "_ShadowSteps", 2f); Set(m, "_HalftoneShadowDensitySource", 1f); }),
                    new Cell("連続 4段",     m => { Halftone(m); Set(m, "_ShadowSteps", 4f); Set(m, "_HalftoneShadowDensitySource", 1f); })),

                // 号数（トーンの階調数）の刻み。1 が連続で、上げるほど「トーンを貼った」
                // 段階的な見え方になる。段差が均等に見えるかがここの確認点。
                new Row("網点の号数",
                    new Cell("1 連続", m => { Halftone(m); Set(m, "_HalftoneShadowLevels", 1f); }),
                    new Cell("2",      m => { Halftone(m); Set(m, "_HalftoneShadowLevels", 2f); }),
                    new Cell("3",      m => { Halftone(m); Set(m, "_HalftoneShadowLevels", 3f); }),
                    new Cell("4 既定", m => { Halftone(m); Set(m, "_HalftoneShadowLevels", 4f); }),
                    new Cell("8",      m => { Halftone(m); Set(m, "_HalftoneShadowLevels", 8f); })),

                // 面に貼り付ける空間（World/UV/Object）は、スクリーン空間と
                // スケールの意味が違う（面側は SurfaceDensity × 30/Scale が実効密度）。
                // 同じ Scale のままだと面側だけ極端に粗くなり、比較にならない。
                new Row("網点の座標空間",
                    new Cell("Screen", m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 0f); }),
                    new Cell("World",  m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 1f); }),
                    new Cell("UV",     m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 2f); }),
                    new Cell("Object", m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 3f); })),

                // カメラを近づけたり回したりして比べる行。
                //   Screen 素        … 大きさ一定・面の上を模様が滑る（泳ぐ）
                //   Screen 貼り付け  … 泳がず、近づけば点が大きくなる（既定）
                //   World            … 泳がないが、近づくと点が大きくなる。UV/三平面のシームが出る
                //   World 大きさ保持 … 泳がず大きさも一定。段階的に密度が切り替わる
                new Row("網点とカメラ移動",
                    new Cell("Screen 素（泳ぐ）", m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 0f); Set(m, "_HalftoneShadowScreenAnchor", 0f); }),
                    new Cell("Screen 貼り付け",   m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 0f); Set(m, "_HalftoneShadowScreenAnchor", 1f); }),
                    new Cell("World",             m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 1f); Set(m, "_HalftoneShadowScreenLock", 0f); }),
                    new Cell("World 大きさ保持",  m => { Halftone(m); Set(m, "_HalftoneShadowSpace", 1f); Set(m, "_HalftoneShadowScreenLock", 1f); })),

                new Row("影の玉ボケ",
                    new Cell("木漏れ日", m => { Bokeh(m); Set(m, "_ShadowBokehComposite", 0f); }),
                    new Cell("葉影",     m => { Bokeh(m); Set(m, "_ShadowBokehComposite", 1f); }),
                    new Cell("全体",     m => { Bokeh(m); Set(m, "_ShadowBokehComposite", 2f); }),
                    new Cell("六角形",   m => { Bokeh(m); Set(m, "_ShadowBokehBlades", 6f); }),
                    new Cell("大粒",     m => { Bokeh(m); Set(m, "_ShadowBokehSize", 0.8f); Set(m, "_ShadowBokehScale", 1.5f); })),

                // 密度は「まだら模様になる／分離した玉になる」を分ける最重要パラメータ。
                new Row("玉ボケの密度",
                    new Cell("0.15 疎", m => { Bokeh(m); Set(m, "_ShadowBokehDensity", 0.15f); }),
                    new Cell("0.35 既定", m => { Bokeh(m); Set(m, "_ShadowBokehDensity", 0.35f); }),
                    new Cell("0.6",     m => { Bokeh(m); Set(m, "_ShadowBokehDensity", 0.6f); }),
                    new Cell("1.0 密",  m => { Bokeh(m); Set(m, "_ShadowBokehDensity", 1f); })),

                new Row("スクリーントーン",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_ScreenTone", 1f); m.EnableKeyword("_SCREEN_TONE"); })),

                new Row("ハッチング",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_UseHatching", 1f); m.EnableKeyword("_HATCHING"); })),

                new Row("水彩",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_UseWatercolor", 1f); m.EnableKeyword("_WATERCOLOR"); })),

                new Row("ピクセルアート",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_PixelArt", 1f); m.EnableKeyword("_PIXEL_ART"); })),

                new Row("色の量子化",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_UseColorQuantize", 1f); m.EnableKeyword("_COLOR_QUANTIZE"); })),

                new Row("グラデ base",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_GradientBaseColor", 1f); m.EnableKeyword("_GRADIENT_BASE_COLOR"); })),

                new Row("影エッジノイズ",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_ShadowEdgeNoise", 1f); m.EnableKeyword("_SHADOW_EDGE_NOISE"); })),

                new Row("落ち影の色",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_CastShadowColorEnable", 1f); m.EnableKeyword("_CAST_SHADOW_COLOR"); })),

                new Row("スペキュラ",
                    new Cell("なし", m => { }),
                    new Cell("有効", m => { m.SetFloat("_Specular", 1f); m.EnableKeyword("_SPECULAR"); })),

                new Row("虹彩 / ラメ",
                    new Cell("なし",       m => { }),
                    new Cell("虹彩",       m => { m.SetFloat("_Iridescence", 1f); m.EnableKeyword("_IRIDESCENCE"); }),
                    new Cell("グリッター", m => { m.SetFloat("_Glitter", 1f); m.EnableKeyword("_GLITTER"); })),

                new Row("疑似反射 / MatCap",
                    new Cell("なし",           m => { }),
                    new Cell("疑似反射",       m => { m.SetFloat("_FakeReflection", 1f); m.EnableKeyword("_FAKE_REFLECTION"); }),
                    new Cell("手続きMatCap",   m => { m.SetFloat("_ProceduralMatCap", 1f); m.EnableKeyword("_PROCEDURAL_MATCAP"); })),

                new Row("線の揺れ / 形状ハイライト",
                    new Cell("なし",       m => { }),
                    new Cell("線の揺れ",   m => { m.SetFloat("_LineBoil", 1f); m.EnableKeyword("_LINE_BOIL"); }),
                    new Cell("形状ハイライト", m => { m.SetFloat("_ShapedHighlight", 1f); m.EnableKeyword("_SHAPED_HIGHLIGHT"); })),

                new Row("色相シフト / ディゾルブ",
                    new Cell("なし",       m => { }),
                    new Cell("色相シフト", m => { m.SetFloat("_HueShiftEnable", 1f); m.EnableKeyword("_HUE_SHIFT"); Set(m, "_HueShift", 0.3f); }),
                    new Cell("ディゾルブ", m => { m.SetFloat("_Dissolve", 1f); m.EnableKeyword("_DISSOLVE"); Set(m, "_DissolveAmount", 0.4f); })),

                new Row("コースティクス",
                    new Cell("World",     m => { Caustics(m); Set(m, "_CausticsSpace", 2f); }),
                    new Cell("Triplanar", m => { Caustics(m); Set(m, "_CausticsSpace", 3f); }),
                    new Cell("影のみ",    m => { Caustics(m); Set(m, "_CausticsComposite", 3f); Set(m, "_CausticsIntensity", 3f); })),

                new Row("等高線",
                    new Cell("Lines", m => { Topo(m); Set(m, "_TopoMode", 0f); }),
                    new Cell("Bands", m => { Topo(m); Set(m, "_TopoMode", 1f); }),
                    new Cell("Noise", m => { Topo(m); Set(m, "_TopoMode", 5f); })),

                // 等高線の合成方法。以前は加算のみで陰影と無関係だった。
                new Row("等高線の合成",
                    new Cell("加算",     m => { Topo(m); Set(m, "_TopoComposite", 0f); }),
                    new Cell("ベース乗算", m => { Topo(m); Set(m, "_TopoComposite", 1f); }),
                    new Cell("明部のみ", m => { Topo(m); Set(m, "_TopoComposite", 2f); }),
                    new Cell("影のみ",   m => { Topo(m); Set(m, "_TopoComposite", 3f); })),

                new Row("リムライト",
                    new Cell("なし", m => { }),
                    new Cell("弱",   m => { Rim(m); Set(m, "_RimIntensity", 0.4f); }),
                    new Cell("強",   m => { Rim(m); Set(m, "_RimIntensity", 1.2f); })),

                // ===== ここからカラー系 =====

                new Row("ベースカラー",
                    new Cell("白",     m => SetColor(m, "_Color", Color.white)),
                    new Cell("赤",     m => SetColor(m, "_Color", new Color(0.85f, 0.25f, 0.25f, 1f))),
                    new Cell("緑",     m => SetColor(m, "_Color", new Color(0.25f, 0.75f, 0.35f, 1f))),
                    new Cell("青",     m => SetColor(m, "_Color", new Color(0.25f, 0.4f, 0.9f, 1f))),
                    new Cell("暗いグレー", m => SetColor(m, "_Color", new Color(0.18f, 0.18f, 0.2f, 1f)))),

                new Row("影の色相",
                    new Cell("既定",   m => { }),
                    new Cell("青紫",   m => SetColor(m, "_ShadowColor", new Color(0.32f, 0.30f, 0.62f, 1f))),
                    new Cell("青緑",   m => SetColor(m, "_ShadowColor", new Color(0.28f, 0.55f, 0.52f, 1f))),
                    new Cell("赤茶",   m => SetColor(m, "_ShadowColor", new Color(0.62f, 0.34f, 0.30f, 1f))),
                    new Cell("ほぼ黒", m => SetColor(m, "_ShadowColor", new Color(0.08f, 0.08f, 0.10f, 1f)))),

                new Row("影の彩度・色相回転",
                    new Cell("既定",     m => { }),
                    new Cell("彩度0",    m => Set(m, "_ShadowSaturation", 0f)),
                    new Cell("彩度2",    m => Set(m, "_ShadowSaturation", 2f)),
                    new Cell("色相+0.1", m => Set(m, "_ShadowHueShift", 0.1f)),
                    new Cell("色相+0.3", m => Set(m, "_ShadowHueShift", 0.3f))),

                new Row("彩度・明度",
                    new Cell("既定",     m => { }),
                    new Cell("彩度0",    m => Set(m, "_Saturation", 0f)),
                    new Cell("彩度1.8",  m => Set(m, "_Saturation", 1.8f)),
                    new Cell("明るさ1.5", m => Set(m, "_Brightness", 1.5f)),
                    new Cell("明るさ0.7", m => Set(m, "_Brightness", 0.7f))),

                new Row("発光の色",
                    new Cell("なし",   m => { }),
                    new Cell("暖色",   m => { Emission(m); SetColor(m, "_EmissionColor", new Color(1.6f, 0.9f, 0.4f, 1f)); }),
                    new Cell("寒色",   m => { Emission(m); SetColor(m, "_EmissionColor", new Color(0.4f, 0.9f, 1.6f, 1f)); }),
                    new Cell("マゼンタ", m => { Emission(m); SetColor(m, "_EmissionColor", new Color(1.5f, 0.3f, 1.2f, 1f)); })),

                new Row("リムの色",
                    new Cell("なし",   m => { }),
                    new Cell("白",     m => { Rim(m); SetColor(m, "_RimColor", Color.white); }),
                    new Cell("シアン", m => { Rim(m); SetColor(m, "_RimColor", new Color(0.3f, 1f, 1f, 1f)); }),
                    new Cell("オレンジ", m => { Rim(m); SetColor(m, "_RimColor", new Color(1f, 0.55f, 0.2f, 1f)); })),

                new Row("玉ボケの色",
                    new Cell("暖色", m => { Bokeh(m); SetColor(m, "_ShadowBokehColor", new Color(1f, 0.95f, 0.8f, 1f)); }),
                    new Cell("寒色", m => { Bokeh(m); SetColor(m, "_ShadowBokehColor", new Color(0.7f, 0.9f, 1.3f, 1f)); }),
                    new Cell("緑",   m => { Bokeh(m); SetColor(m, "_ShadowBokehColor", new Color(0.7f, 1.3f, 0.7f, 1f)); })),

                new Row("網点の色",
                    new Cell("黒に近い", m => { Halftone(m); SetColor(m, "_HalftoneShadowColor", new Color(0.10f, 0.10f, 0.14f, 1f)); }),
                    new Cell("青",       m => { Halftone(m); SetColor(m, "_HalftoneShadowColor", new Color(0.20f, 0.30f, 0.70f, 1f)); }),
                    new Cell("赤",       m => { Halftone(m); SetColor(m, "_HalftoneShadowColor", new Color(0.70f, 0.20f, 0.25f, 1f)); })),

                new Row("等高線の色",
                    new Cell("シアン",   m => { Topo(m); SetColor(m, "_TopoColor", new Color(0.2f, 1f, 0.9f, 1f)); }),
                    new Cell("マゼンタ", m => { Topo(m); SetColor(m, "_TopoColor", new Color(1f, 0.3f, 0.8f, 1f)); }),
                    new Cell("黄",       m => { Topo(m); SetColor(m, "_TopoColor", new Color(1f, 0.9f, 0.3f, 1f)); })),

                new Row("コースティクスの色",
                    new Cell("水色",   m => { Caustics(m); SetColor(m, "_CausticsColor", new Color(0.6f, 0.9f, 1f, 1f)); }),
                    new Cell("金",     m => { Caustics(m); SetColor(m, "_CausticsColor", new Color(1.2f, 0.9f, 0.4f, 1f)); }),
                    new Cell("紫",     m => { Caustics(m); SetColor(m, "_CausticsColor", new Color(0.8f, 0.4f, 1.2f, 1f)); })),

                // ===== NPR2026 で追加・修正した機能 =====

                // P2. 閾値の符号修正の確認。角度フィールドは「正面ライトで明るい」ように
                // 球メッシュから解いてあるので、符号が正しければ Baseline に近い陰影になる。
                // 修正前の式だと正面ライトでほぼ全面が影に落ちるため一目で分かる。
                //
                // 「SDF単体」（回転追従オフ）は載せていない。角度フィールドは回転追従専用で、
                // 単体モードは UV 固定の別ロジック（ndotl に掛ける）なので、
                // 同じテクスチャを流し込むと「ライトと無関係に固定の影」になるだけで
                // 比較の意味がない。
                new Row("顔SDF影（回転追従）",
                    new Cell("無効",        m => { }),
                    new Cell("追従 0",      m => SdfMap(m, 0f)),
                    new Cell("追従 -0.2",   m => SdfMap(m, -0.2f)),
                    new Cell("追従 +0.2",   m => SdfMap(m, 0.2f))),

                // F3. マスクのトグルが実際に効いているかの確認。
                // 中央だけ黒いマスクを与えるので、ON なら中央がディゾルブしない。
                new Row("ディゾルブのマスク",
                    new Cell("マスクなし",     m => Dissolve(m, 0.5f)),
                    new Cell("マスクOFF",      m => { Dissolve(m, 0.5f); DissolveMask(m, false); }),
                    new Cell("マスクON",       m => { Dissolve(m, 0.5f); DissolveMask(m, true); })),

                // F2. Animator を使わずにディゾルブが自走することの確認。
                // Scene ビューの Always Refresh を有効にすると時間で動く。
                new Row("ディゾルブ自走(FXMod)",
                    new Cell("静止 0.5",   m => Dissolve(m, 0.5f)),
                    new Cell("Sine 自走",  m => { Dissolve(m, 0f); FxModDissolve(m, 0f, 0.5f); }),
                    new Cell("Pulse 自走", m => { Dissolve(m, 0f); FxModDissolve(m, 3f, 0.2f); })),

                // P1. 影の形のアートディレクション。中央に 1 スロット置き、
                // 強さの符号で「影が膨らむ／へこむ」が入れ替わることを確認する。
                new Row("影シェイプリグ",
                    new Cell("なし",       m => { }),
                    new Cell("影を増やす", m => ShadowRig(m, strength: -0.35f, falloff: 0.5f, lightFollow: 0f)),
                    new Cell("影を減らす", m => ShadowRig(m, strength: 0.35f, falloff: 0.5f, lightFollow: 0f)),
                    new Cell("硬いふち",   m => ShadowRig(m, strength: -0.35f, falloff: 0.05f, lightFollow: 0f)),
                    new Cell("ライト追従", m => ShadowRig(m, strength: -0.35f, falloff: 0.5f, lightFollow: 1f))),

                // P5. TAM を実際に生成して割り当てる。既定値は真っ白なので、
                // 素材を渡さずにキーワードだけ立てても何も出ない（＝検証にならない）。
                new Row("ハッチングTAM",
                    new Cell("なし",   m => { }),
                    new Cell("鉛筆",   m => HatchingPreset(m, "Pencil")),
                    new Cell("交差",   m => HatchingPreset(m, "Crosshatch")),
                    new Cell("漫画",   m => HatchingPreset(m, "Manga")),
                    new Cell("版画",   m => HatchingPreset(m, "Etching"))),
            };
        }

        // ---- 機能ごとの下ごしらえ ----

        private static void Halftone(Material m)
        {
            m.SetFloat("_HalftoneShadow", 1f);
            m.EnableKeyword("_HALFTONE_SHADOW");
            // シェーダー既定と同じ細かさで確認する。
            Set(m, "_HalftoneShadowScale", 6f);
            Set(m, "_HalftoneShadowThreshold", 0.5f);
            Set(m, "_HalftoneShadowSoftness", 0.1f);
            Set(m, "_HalftoneShadowIntensity", 1f);
            Set(m, "_HalftoneShadowBlend", 1f);
            Set(m, "_HalftoneShadowAngle", 45f);
            Set(m, "_HalftoneShadowLevels", 4f);
            Set(m, "_HalftoneShadowSurfaceDensity", 40f);
            // 落ち影も濃度に効かせる（既定）。
            Set(m, "_HalftoneShadowCastShadow", 1f);
            // 既定はオフ。行ごとに明示的に切り替えて比較する。
            Set(m, "_HalftoneShadowScreenLock", 0f);
            // スクリーン空間はオブジェクト貼り付けを既定にする（泳がない側）。
            Set(m, "_HalftoneShadowScreenAnchor", 1f);
            Set(m, "_HalftoneShadowDotMin", 0.05f);
            Set(m, "_HalftoneShadowDotMax", 0.8f);
            Set(m, "_HalftoneShadowAA", 1f);
            // 濃度は量子化前の連続値を基準にする（既定）。
            Set(m, "_HalftoneShadowDensitySource", 1f);
            SetColor(m, "_HalftoneShadowColor", new Color(0.12f, 0.12f, 0.18f, 1f));
        }

        private static void Bokeh(Material m)
        {
            m.SetFloat("_ShadowBokeh", 1f);
            m.EnableKeyword("_SHADOW_BOKEH");
            Set(m, "_ShadowBokehIntensity", 3f);
            Set(m, "_ShadowBokehScale", 4f);
            Set(m, "_ShadowBokehSize", 0.4f);
            Set(m, "_ShadowBokehSoftness", 0.5f);
            Set(m, "_ShadowBokehRimGain", 0.25f);
            Set(m, "_ShadowBokehDensity", 0.35f);
            Set(m, "_ShadowBokehShadowMin", 0.3f);
            Set(m, "_ShadowBokehBlend", 1f);
            SetColor(m, "_ShadowBokehColor", new Color(1f, 0.95f, 0.8f, 1f));
        }

        private static void Caustics(Material m)
        {
            m.SetFloat("_Caustics", 1f);
            m.EnableKeyword("_CAUSTICS");
            Set(m, "_CausticsPatternMode", 0f);
            Set(m, "_CausticsIntensity", 1.5f);
            Set(m, "_CausticsScale", 4f);
            Set(m, "_CausticsSpeed", 0.5f);
            Set(m, "_CausticsContrast", 2f);
            SetColor(m, "_CausticsColor", new Color(0.6f, 0.9f, 1f, 1f));
        }

        private static void Topo(Material m)
        {
            m.SetFloat("_Topographic", 1f);
            m.EnableKeyword("_TOPOGRAPHIC");
            Set(m, "_TopoSpacing", 0.12f);
            Set(m, "_TopoLineWidth", 0.12f);
            Set(m, "_TopoNoiseStrength", 0.4f);
            Set(m, "_TopoEmission", 2f);
            SetColor(m, "_TopoColor", new Color(0.2f, 1f, 0.8f, 1f));
        }

        private static void Rim(Material m)
        {
            m.SetFloat("_RimLight", 1f);
            m.EnableKeyword("_RIM_LIGHT");
        }

        private static void Emission(Material m)
        {
            m.SetFloat("_Emission", 1f);
            m.EnableKeyword("_EMISSION");
            Set(m, "_EmissionIntensity", 1.5f);
        }

        // ---- NPR2026: 顔SDF（角度フィールド）----

        // ショーケースの「顔の正面」。-Z を正面にしているのは、グリッドを手前から見たときに
        // 見えている半球が「顔の表」になるようにするため。+Z にすると、format が表現できる
        // 半球（正面ライトで明るい側）がカメラの反対側になり、手前が常に真っ黒に見える。
        private static readonly Vector3 ShowcaseFaceForward = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 ShowcaseFaceRight = new Vector3(1f, 0f, 0f);

        private static void SdfMap(Material m, float offset)
        {
            Texture2D field = GetOrCreateAngleField();
            if (field == null) return;

            m.SetTexture("_SDFMap", field);
            Set(m, "_UseSDFMap", 1f);
            m.EnableKeyword("_SDF_MAP");
            Set(m, "_SDFIntensity", 1f);
            // 0.05 は境界が鋭すぎ、閾値がライト角で動くたびに段差が目に付く。
            // 実運用でも 0.1 前後から詰めるほうが扱いやすい。
            Set(m, "_SDFSoftness", 0.12f);
            Set(m, "_SDFOffset", offset);

            // 角度フィールドは回転追従専用。回転追従を切ると
            // 「UV 固定で ndotl に掛ける」別モードになり、ライトと無関係の固定影になる。
            Set(m, "_FaceSDFRotation", 1f);
            m.EnableKeyword("_FACE_SDF_ROTATION");

            // ベイク側とシェーダー側の基底を必ず一致させる。
            if (m.HasProperty("_FaceForwardDirection"))
            {
                m.SetVector("_FaceForwardDirection",
                    new Vector4(ShowcaseFaceForward.x, ShowcaseFaceForward.y, ShowcaseFaceForward.z, 0f));
            }
            if (m.HasProperty("_FaceRightDirection"))
            {
                m.SetVector("_FaceRightDirection",
                    new Vector4(ShowcaseFaceRight.x, ShowcaseFaceRight.y, ShowcaseFaceRight.z, 0f));
            }
        }

        /// <summary>
        /// Build the angle field that <c>_FACE_SDF_ROTATION</c> actually consumes, derived
        /// from the real sphere mesh.
        ///
        /// The encoding matches the Map Generator bake: <c>s = (1 - cos theta_t) / 2</c>,
        /// where theta_t is the horizontal light angle at which the texel flips into shadow.
        ///
        /// Two things this deliberately does NOT do:
        ///
        /// 1. It does not assume a UV convention. The first version computed the normal from
        ///    a guessed lat-long mapping of u and v; if the seam or winding of Unity's sphere
        ///    differs at all, the whole field is rotated relative to the mesh and the shadow
        ///    sweeps in a direction that has nothing to do with the geometry. Reading the
        ///    mesh's own normals and UVs removes the guess.
        ///
        /// 2. It does not treat "not lit when the light is dead ahead" as "never lit". With
        ///    forward F and right R, a texel is lit while sin(theta + psi) > 0 where
        ///    psi = atan2(dot(n,F), dot(n,R)). For psi >= 0 the texel starts lit and flips at
        ///    theta_t = pi - psi, giving s = (1 + cos psi) / 2. For psi &lt; 0 the texel starts
        ///    in shadow and would become lit later — a shape this one-transition format
        ///    cannot express, so it is stored as 0 (shadow) which is what the front-lit case
        ///    wants anyway. That is a property of the format, not of this texture.
        ///
        /// Occlusion is not modelled: a convex sphere casts no shadow on itself, so this is
        /// exactly the N-dot-L transition.
        /// </summary>
        private static Texture2D GetOrCreateAngleField()
        {
            // 512。256 だと勾配が粗く、閾値が動くたびに境界がテクセル単位で跳ねて見える。
            const int Resolution = 512;
            string path = $"{OutputFolder}/Showcase_SdfAngleField_{GeneratedAssetVersion}.png";

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Mesh sphere = LoadPrimitiveMesh(PrimitiveType.Sphere);
            if (sphere == null)
            {
                Debug.LogWarning("[NataneShowcase] 球メッシュを取得できず、角度フィールドを生成できませんでした。");
                return null;
            }

            Vector3[] normals = sphere.normals;
            Vector2[] uvs = sphere.uv;
            int[] triangles = sphere.triangles;

            if (normals == null || uvs == null || triangles == null ||
                normals.Length != uvs.Length || normals.Length == 0)
            {
                Debug.LogWarning("[NataneShowcase] 球メッシュに使える法線/UV がありませんでした。");
                return null;
            }

            // 頂点ごとに s を解く。ベイクと同じ基底を使う（ShowcaseFaceForward / Right）。
            //
            //   psi = atan2(n·F, n·R) とすると、テクセルが明るいのは sin(theta + psi) > 0。
            //   psi > 0 なら theta = 0 で明るく、theta_t = pi - psi で影に落ちるので
            //   s = (1 - cos(theta_t)) / 2 = (1 + cos psi) / 2。
            //
            // cos は偶関数なので、この式は psi の符号で場合分けせずにそのまま使える。
            // 前の版は psi < 0 を「常に影」として 0 に落としていたが、psi = 0 の大円を
            // 挟んで値が 1 から 0 へ飛ぶため、そこがテクスチャ上の硬い段差になり、
            // ライトを回すと影がその線を跨ぐたびにパキッと切り替わっていた。
            //
            // cos psi = n·R / |(n·F, n·R)| なので atan2 も不要。
            var vertexValues = new float[normals.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 n = normals[i].normalized;
                float nf = Vector3.Dot(n, ShowcaseFaceForward);
                float nr = Vector3.Dot(n, ShowcaseFaceRight);

                float len = Mathf.Sqrt(nf * nf + nr * nr);
                float cosPsi = len > 1e-5f ? nr / len : 0f;
                vertexValues[i] = (1f + cosPsi) * 0.5f;
            }

            var values = new float[Resolution * Resolution];
            var covered = new bool[Resolution * Resolution];

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                RasterizeTriangle(
                    uvs[triangles[t]], uvs[triangles[t + 1]], uvs[triangles[t + 2]],
                    vertexValues[triangles[t]], vertexValues[triangles[t + 1]], vertexValues[triangles[t + 2]],
                    values, covered, Resolution);
            }

            // UV シームの隙間を埋める。埋めないと縫い目が黒い線として出る。
            DilateCoverage(values, covered, Resolution, 4);

            var pixels = new Color[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float s = Mathf.Clamp01(values[i]);
                pixels[i] = new Color(s, s, s, 1f);
            }

            return WriteGeneratedTexture(path, pixels, Resolution);
        }

        /// <summary>
        /// 組み込みプリミティブのメッシュを取り出す。
        /// Resources.GetBuiltinResource の名前は Unity のバージョンで揺れるため、
        /// 実際にプリミティブを生成して読む方が確実。
        /// </summary>
        private static Mesh LoadPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            try
            {
                var filter = temp.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        /// <summary>UV 空間へ三角形を重心座標で流し込む。</summary>
        private static void RasterizeTriangle(
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            float d0, float d1, float d2,
            float[] values, bool[] covered, int resolution)
        {
            Vector2 p0 = uv0 * resolution;
            Vector2 p1 = uv1 * resolution;
            Vector2 p2 = uv2 * resolution;

            float area = Cross2D(p1 - p0, p2 - p0);
            if (Mathf.Abs(area) < 0.0001f) return;
            float invArea = 1f / area;

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), 0, resolution - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))), 0, resolution - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), 0, resolution - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))), 0, resolution - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross2D(p1 - p0, p - p0) * invArea;
                    float w1 = Cross2D(p2 - p1, p - p1) * invArea;
                    float w2 = 1f - w0 - w1;

                    if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;

                    int idx = y * resolution + x;
                    values[idx] = d0 * w2 + d1 * w0 + d2 * w1;
                    covered[idx] = true;
                }
            }
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void DilateCoverage(float[] values, bool[] covered, int resolution, int iterations)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                bool[] snapshot = (bool[])covered.Clone();
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int idx = y * resolution + x;
                        if (snapshot[idx]) continue;

                        float sum = 0f;
                        int count = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= resolution || ny >= resolution) continue;

                                int nIdx = ny * resolution + nx;
                                if (!snapshot[nIdx]) continue;

                                sum += values[nIdx];
                                count++;
                            }
                        }

                        if (count == 0) continue;
                        values[idx] = sum / count;
                        covered[idx] = true;
                    }
                }
            }
        }

        // ---- NPR2026: 影シェイプリグ ----

        /// <summary>
        /// 中央にひとつだけ楕円リグを置く。残り 3 スロットは半径 0（＝無効）のまま。
        /// 球の陰影が一巡する中央に置くと、境界がどちらへ動いたかが最も分かりやすい。
        /// </summary>
        private static void ShadowRig(Material m, float strength, float falloff, float lightFollow)
        {
            m.SetFloat("_ShadowShapeRig", 1f);
            m.EnableKeyword("_SHADOW_SHAPE_RIG");

            if (m.HasProperty("_ShadowRigParams0"))
            {
                m.SetVector("_ShadowRigParams0", new Vector4(0.5f, 0.5f, 0.22f, 0.16f));
            }
            if (m.HasProperty("_ShadowRigShape0"))
            {
                m.SetVector("_ShadowRigShape0", new Vector4(0f, strength, falloff, lightFollow));
            }

            // 残りのスロットは明示的に無効化する。ショーケースのマテリアルは
            // 使い回されるので、前回の設定が残ると比較にならない。
            for (int slot = 1; slot < 4; slot++)
            {
                if (m.HasProperty("_ShadowRigParams" + slot))
                {
                    m.SetVector("_ShadowRigParams" + slot, new Vector4(0.5f, 0.5f, 0f, 0f));
                }
                if (m.HasProperty("_ShadowRigShape" + slot))
                {
                    m.SetVector("_ShadowRigShape" + slot, new Vector4(0f, 0f, 0.5f, 0f));
                }
            }

            Set(m, "_ShadowRigFollowScale", 0.25f);
            Set(m, "_ShadowRigMaskStrength", 1f);
        }

        // ---- NPR2026: ディゾルブ ----

        private static void Dissolve(Material m, float amount)
        {
            m.SetFloat("_Dissolve", 1f);
            m.EnableKeyword("_DISSOLVE");
            Set(m, "_DissolveAmount", amount);
            Set(m, "_DissolveEdgeWidth", 0.08f);
            Set(m, "_DissolveEdgeIntensity", 4f);
            Set(m, "_DissolveBlend", 1f);
            SetColor(m, "_DissolveEdgeColor", new Color(2.5f, 1.2f, 0.4f, 1f));
            m.SetTexture("_DissolveTex", GetOrCreateNoiseTexture());
        }

        private static void DissolveMask(Material m, bool useMask)
        {
            Set(m, "_UseDissolveMask", useMask ? 1f : 0f);
            m.SetTexture("_DissolveMask", GetOrCreateCenterMask());
        }

        private static void FxModDissolve(Material m, float source, float amount)
        {
            m.SetFloat("_FXModulator", 1f);
            m.EnableKeyword("_FX_MODULATOR");
            Set(m, "_FXModSource0", source);      // 0 = Sine, 3 = Pulse
            Set(m, "_FXModTarget0", 13f);         // 13 = DissolveAmount
            Set(m, "_FXModAmount0", amount);
            Set(m, "_FXModSpeed0", 0.5f);
            Set(m, "_FXModMin0", 0f);
            Set(m, "_FXModMax0", 1f);
            Set(m, "_FXModCurve0", 1f);
        }

        // ---- NPR2026: ハッチング TAM ----

        private static void HatchingPreset(Material m, string presetName)
        {
            string path0 = $"{OutputFolder}/Showcase_Hatch_{presetName}_{GeneratedAssetVersion}_0.asset";
            string path1 = $"{OutputFolder}/Showcase_Hatch_{presetName}_{GeneratedAssetVersion}_1.asset";

            var tex0 = AssetDatabase.LoadAssetAtPath<Texture2D>(path0);
            var tex1 = AssetDatabase.LoadAssetAtPath<Texture2D>(path1);

            if (tex0 == null || tex1 == null)
            {
                if (!HatchingToneGenerator.GeneratePresetTam(presetName, 256, 0, out Texture2D built0, out Texture2D built1))
                {
                    Debug.LogWarning($"[NataneShowcase] 未知のハッチングプリセット: {presetName}");
                    return;
                }

                // .asset で保存する。PNG に落とすとインポート設定次第で線が潰れ、
                // 「TAM が累積になっているか」の確認にならない。
                AssetDatabase.CreateAsset(built0, path0);
                AssetDatabase.CreateAsset(built1, path1);
                tex0 = built0;
                tex1 = built1;
            }

            m.SetTexture("_HatchTex0", tex0);
            m.SetTexture("_HatchTex1", tex1);
            Set(m, "_UseHatching", 1f);
            m.EnableKeyword("_HATCHING");
            Set(m, "_HatchingTiling", 3f);
            Set(m, "_HatchingBlend", 1f);
        }

        // ---- ショーケース用の合成テクスチャ ----

        /// <summary>2 オクターブのタイル可能なノイズ。ディゾルブの模様に使う。</summary>
        private static Texture2D GetOrCreateNoiseTexture()
        {
            return GetOrCreateGeneratedTexture("Showcase_DissolveNoise", 256, (u, v) =>
            {
                float n = TiledNoise(u, v, 8, 1) * 0.65f + TiledNoise(u, v, 19, 2) * 0.35f;
                return Mathf.Clamp01(n);
            });
        }

        /// <summary>
        /// 中央が黒・周辺が白のマスク。ディゾルブのマスクトグルが効いていれば
        /// 中央だけが溶けずに残るので、ON / OFF の差が一目で分かる。
        /// </summary>
        private static Texture2D GetOrCreateCenterMask()
        {
            return GetOrCreateGeneratedTexture("Showcase_DissolveCenterMask", 256, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.34f, d));
            });
        }

        /// <param name="writeToAlpha">
        /// True writes the value into alpha and leaves RGB white, for shape textures that are
        /// sampled through their alpha channel. A grayscale-with-opaque-alpha image would
        /// silently come out as a fully opaque rectangle there.
        /// </param>
        private static Texture2D GetOrCreateGeneratedTexture(string name, int resolution,
                                                             Func<float, float, float> evaluate,
                                                             bool writeToAlpha = false)
        {
            string path = $"{OutputFolder}/{name}_{GeneratedAssetVersion}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;
                    float v = (y + 0.5f) / resolution;
                    float value = Mathf.Clamp01(evaluate(u, v));
                    pixels[y * resolution + x] = writeToAlpha
                        ? new Color(1f, 1f, 1f, value)
                        : new Color(value, value, value, 1f);
                }
            }

            return WriteGeneratedTexture(path, pixels, resolution);
        }

        /// <summary>
        /// PNG として書き出し、係数テクスチャとしてインポートし直す。
        /// </summary>
        private static Texture2D WriteGeneratedTexture(string path, Color[] pixels, int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply(false, false);

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string absolute = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                path.Replace('/', System.IO.Path.DirectorySeparatorChar));
            string dir = System.IO.Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

            try
            {
                System.IO.File.WriteAllBytes(absolute, png);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneShowcase] {path} を書き出せませんでした: {e.Message}");
                return null;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureDataTexture(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// マスク・角度フィールドは色ではなく係数として読まれるため、
        /// sRGB 変換をかけると値が歪む。Linear / Repeat に固定する。
        /// </summary>
        private static void ConfigureDataTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // 形状テクスチャはアルファを読むので、アルファを落とさせない。
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        private static float TiledNoise(float u, float v, int period, int seed)
        {
            float x = u * period;
            float y = v * period;
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = NoiseHash(Mod(xi, period), Mod(yi, period), seed);
            float b = NoiseHash(Mod(xi + 1, period), Mod(yi, period), seed);
            float c = NoiseHash(Mod(xi, period), Mod(yi + 1, period), seed);
            float d = NoiseHash(Mod(xi + 1, period), Mod(yi + 1, period), seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Mod(int value, int period)
        {
            int r = value % period;
            return r < 0 ? r + period : r;
        }

        private static float NoiseHash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFF) / (float)0x7FFFFFF;
            }
        }

        // ---- 生成の下回り ----

        private static Material CreateOrUpdateMaterial(Shader shader, string name, Action<Material> configure)
        {
            string path = $"{OutputFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                Undo.RecordObject(material, "Configure Natane Showcase Material");
                material.shader = shader;
            }

            SetColor(material, "_Color", new Color(0.6f, 0.6f, 0.64f, 1f));
            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 並べ終わった全オブジェクトを覆う床を敷く。
        ///
        /// サイズを先に決め打ちすると、カバレッジ区画（機能数で伸びる）や補助区画が
        /// 床からはみ出す。実際に配置されたものの範囲を測ってから作る。
        /// </summary>
        private static void CreateFloorToFit(Transform parent, Shader shader)
        {
            bool any = false;
            var bounds = new Bounds();

            foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                // TextMesh のレンダラーも含めて測る。ラベルがはみ出すのも困るため。
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!any) return;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);

            // 中心を実際の範囲に合わせる。原点固定だと片側だけはみ出す。
            Vector3 center = parent.InverseTransformPoint(bounds.center);
            floor.transform.localPosition = new Vector3(center.x, 0f, center.z);

            // Unity の Plane は 10x10 単位。周囲に 4m の余白を持たせる。
            const float margin = 4f;
            float width = bounds.size.x + margin * 2f;
            float height = bounds.size.z + margin * 2f;
            floor.transform.localScale = new Vector3(width / 10f, 1f, height / 10f);

            string path = $"{OutputFolder}/Showcase_Floor.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Showcase_Floor" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            SetColor(mat, "_Color", new Color(0.22f, 0.22f, 0.25f, 1f));
            EditorUtility.SetDirty(mat);

            floor.GetComponent<Renderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(floor, "Create Natane Feature Showcase");
        }

        // ---- 全機能カバレッジ（自動生成）----

        /// <summary>1 機能ぶんのカバレッジ結果。生成後のレポートに使う。</summary>
        private sealed class CoverageEntry
        {
            public string Keyword;
            public string Property;
            public string Label;
            public string ShaderName;

            /// <summary>個別セットアップがあり、素材や強度まで与えられているか。</summary>
            public bool HasSetup;

            /// <summary>どのシェーダーにも該当プロパティが無く、球体を置けなかった。</summary>
            public bool Unavailable;
        }

        /// <summary>
        /// 全機能を漏れなく並べる区画。
        ///
        /// 手書きの行はバリエーション比較のために価値があるが、それだけだと
        /// 新機能を足すたびに追記が要り、必ず漏れる。ここは
        /// <see cref="NataneShaderKeywordSynchronizer.KeywordMappings"/>（キーワードと
        /// トグルプロパティの単一の真実）を走査して生成するので、
        /// 機能を足せば自動でショーケースに現れる。
        ///
        /// 手書きの行で既に扱っているキーワードは重複させない。
        /// </summary>
        private static List<CoverageEntry> CreateCoverageSection(
            Transform parent, Font font, List<Row> curatedRows, float gridWidth, float gridHeight)
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (string kw in CollectCuratedKeywords(curatedRows)) covered.Add(kw);

            // ラベル用にインスペクタ登録を引く（あれば日本語名が出る）。
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var section in NataneToonInspectorSectionRegistry.All)
            {
                if (!string.IsNullOrEmpty(section.ToggleKeyword) && !labels.ContainsKey(section.ToggleKeyword))
                {
                    labels[section.ToggleKeyword] = section.LabelJP;
                }
            }

            var entries = new List<CoverageEntry>();
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                if (string.IsNullOrEmpty(mapping.keyword) || string.IsNullOrEmpty(mapping.propertyName)) continue;
                if (covered.Contains(mapping.keyword)) continue;
                if (!covered.Add(mapping.keyword)) continue;   // マッピング内の重複対策

                labels.TryGetValue(mapping.keyword, out string label);
                entries.Add(new CoverageEntry
                {
                    Keyword = mapping.keyword,
                    Property = mapping.propertyName,
                    Label = string.IsNullOrEmpty(label) ? mapping.keyword : label,
                    HasSetup = NataneShowcaseFeatureSetups.Has(mapping.keyword)
                });
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Keyword, b.Keyword));

            // 補助区画（+6）よりさらに奥。区画同士のラベルが重ならないよう間を空ける。
            float startZ = gridHeight * 0.5f + 24f;

            CreateLabel(parent, font,
                "全機能カバレッジ（キーワード管理表から自動生成 / 手書き行と重複しないもの）",
                new Vector3(0f, 0.02f, startZ - 3.0f), 0.45f, TextAnchor.UpperCenter);

            int placed = 0;
            foreach (CoverageEntry entry in entries)
            {
                Shader shader = ResolveShaderFor(entry.Property);
                if (shader == null)
                {
                    // どのシェーダーにも無い＝カタログとマッピングの食い違い。
                    // 黙って飛ばすと「並んでいないこと」に気付けないのでレポートへ回す。
                    entry.Unavailable = true;
                    continue;
                }

                entry.ShaderName = shader.name;

                int col = placed % CoverageColumns;
                int row = placed / CoverageColumns;
                float x = -(CoverageColumns - 1) * ColumnSpacing * 0.5f + col * ColumnSpacing;
                float z = startZ + row * RowSpacing;

                Material material = CreateOrUpdateMaterial(shader,
                    $"Coverage_{Sanitize(entry.Keyword)}",
                    m =>
                    {
                        if (m.HasProperty(entry.Property)) m.SetFloat(entry.Property, 1f);
                        m.EnableKeyword(entry.Keyword);
                        NataneShowcaseFeatureSetups.TryApply(entry.Keyword, m);
                    });

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"COV {entry.Keyword}";
                sphere.transform.SetParent(parent, false);
                sphere.transform.localPosition = new Vector3(x, 1f, z);
                sphere.transform.localScale = Vector3.one * 2f;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                Undo.RegisterCreatedObjectUndo(sphere, "Create Natane Feature Showcase");

                // セットアップの有無をラベルに出す。素のトグルのままのものは
                // 「見えなくて当たり前」なので、見分けが付かないと検証にならない。
                string suffix = entry.HasSetup ? string.Empty : "（素）";
                CreateLabel(parent, font, Shorten(entry.Label, 12) + suffix,
                    new Vector3(x, 0.02f, z - LabelDrop), 0.26f, TextAnchor.UpperCenter);

                placed++;
            }

            return entries;
        }

        /// <summary>手書き行が触れているキーワードを集める（重複配置を避けるため）。</summary>
        private static IEnumerable<string> CollectCuratedKeywords(List<Row> rows)
        {
            // 手書き行は Action の中でキーワードを立てるので、静的には読めない。
            // ダミーマテリアルへ適用して、実際に立ったキーワードを観測する。
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) yield break;

            var probe = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                foreach (Row row in rows)
                {
                    foreach (Cell cell in row.Cells)
                    {
                        foreach (string kw in probe.shaderKeywords) probe.DisableKeyword(kw);

                        try
                        {
                            cell.Configure(probe);
                        }
                        catch (Exception)
                        {
                            // 素材生成に失敗するセルがあっても、カバレッジ判定は続行する。
                            continue;
                        }

                        foreach (string kw in probe.shaderKeywords) yield return kw;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// そのトグルプロパティを持つシェーダーを探す。
        /// Fur / Background / Particle 専用の機能は本体シェーダーに無いため、
        /// 本体で駄目ならカタログの他のシェーダーへ回す。
        /// </summary>
        private static Shader ResolveShaderFor(string property)
        {
            Shader main = Shader.Find(ShaderName);
            if (main != null && ShaderHasProperty(main, property)) return main;

            foreach (string name in NataneShaderCatalog.ShaderNames)
            {
                if (name == ShaderName) continue;
                Shader candidate = Shader.Find(name);
                if (candidate != null && ShaderHasProperty(candidate, property)) return candidate;
            }

            return null;
        }

        private static readonly Dictionary<string, HashSet<string>> _shaderPropertyCache =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private static bool ShaderHasProperty(Shader shader, string property)
        {
            if (!_shaderPropertyCache.TryGetValue(shader.name, out HashSet<string> props))
            {
                props = new HashSet<string>(StringComparer.Ordinal);
                int count = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < count; i++)
                {
                    props.Add(ShaderUtil.GetPropertyName(shader, i));
                }
                _shaderPropertyCache[shader.name] = props;
            }

            return props.Contains(property);
        }

        /// <summary>
        /// カバレッジのレポートを書き出す。
        /// 「並んでいるが素のトグルなので見えないかもしれない」ものと
        /// 「そもそも置けなかった」ものを明示する。
        /// </summary>
        private static string WriteCoverageReport(List<CoverageEntry> entries, List<Row> curatedRows)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# 表現ショーケース カバレッジ");
            sb.AppendLine();
            sb.AppendLine($"- 生成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- キーワード管理表の総数: {NataneShaderKeywordSynchronizer.KeywordMappings.Length}");
            sb.AppendLine($"- 手書き行: {curatedRows.Count} 行（バリエーション比較用）");
            sb.AppendLine();

            var withSetup = entries.Where(e => !e.Unavailable && e.HasSetup).ToList();
            var bare = entries.Where(e => !e.Unavailable && !e.HasSetup).ToList();
            var unavailable = entries.Where(e => e.Unavailable).ToList();

            sb.AppendLine($"## 素材・強度まで設定済み — {withSetup.Count} 件");
            sb.AppendLine();
            sb.AppendLine("そのまま見た目で確認できる。");
            sb.AppendLine();
            foreach (CoverageEntry e in withSetup) sb.AppendLine($"- `{e.Keyword}` — {e.Label}");
            sb.AppendLine();

            sb.AppendLine($"## トグルを立てただけ — {bare.Count} 件");
            sb.AppendLine();
            sb.AppendLine("ラベルに「（素）」が付いている球体。");
            sb.AppendLine("パラメータ既定値のままなので、**見た目が Baseline と変わらない可能性がある**。");
            sb.AppendLine("確認が必要なら NataneShowcaseFeatureSetups へ個別セットアップを足すこと。");
            sb.AppendLine();
            foreach (CoverageEntry e in bare) sb.AppendLine($"- `{e.Keyword}` — {e.Label}（{e.Property}）");
            sb.AppendLine();

            sb.AppendLine($"## 配置できなかった — {unavailable.Count} 件");
            sb.AppendLine();
            if (unavailable.Count == 0)
            {
                sb.AppendLine("なし。");
            }
            else
            {
                sb.AppendLine("カタログ内のどのシェーダーにもトグルプロパティが見つからなかったもの。");
                sb.AppendLine("キーワード管理表とシェーダーの食い違いの可能性がある。");
                sb.AppendLine();
                foreach (CoverageEntry e in unavailable) sb.AppendLine($"- `{e.Keyword}`（{e.Property}）");
            }
            sb.AppendLine();

            string path = $"{OutputFolder}/COVERAGE.md";
            string root = System.IO.Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(root))
            {
                try
                {
                    string absolute = System.IO.Path.Combine(
                        root, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    string dir = System.IO.Path.GetDirectoryName(absolute);
                    if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllText(absolute, sb.ToString(), new System.Text.UTF8Encoding(false));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NataneShowcase] カバレッジレポートを書き出せませんでした: {e.Message}");
                }
            }

            Debug.Log(
                $"[NataneShowcase] カバレッジ: 設定済み {withSetup.Count} / 素のトグル {bare.Count} / " +
                $"配置不可 {unavailable.Count}\nレポート: {path}");

            return path;
        }

        // ---- 球体では確認できない機能の区画 ----

        /// <summary>
        /// Some features cannot be verified on a sphere material at all: cast-shadow halftone
        /// needs an occluder plus a receiving surface, a light shaft needs a cone, a sky dome
        /// needs to be inside-out. Putting them on the grid as another sphere row would show
        /// nothing.
        ///
        /// They get their own strip behind the grid, each with the geometry the feature is
        /// actually meant to be used with.
        /// </summary>
        private static void CreateAuxiliarySection(Transform parent, Font font, float gridHeight)
        {
            float z = gridHeight * 0.5f + 9f;

            CreateLabel(parent, font,
                "球体では確認できない機能（それぞれ想定される形状で配置）",
                new Vector3(0f, 0.02f, z - 5f), 0.42f, TextAnchor.UpperCenter);

            // 1 デモ 1 行。横に並べると、デモごとに必要な幅（落ち影は板 2 枚、
            // ファーは球 3 個）が違うぶん重なる。行を分ければ幅に依らず衝突しない。
            // 各デモは +X 方向へ伸びるので、幅の半分だけ左へ寄せて中央に置く。
            CreateCastShadowHalftoneDemo(parent, font, new Vector3(-2.5f, 0f, z));
            CreateFurMethodDemo(parent, font, new Vector3(-5f, 0f, z + AuxiliaryRowSpacing));
        }

        /// <summary>
        /// 補助区画の行間。デモ本体（半径 2 まで）とその手前のラベル（-3.4 まで）を
        /// 足した高さより広く取る。
        /// </summary>
        private const float AuxiliaryRowSpacing = 8f;

        /// <summary>
        /// シェル法 / フィン法 / 併用の比較。
        ///
        /// カバレッジ区画の `_FUR` は球体 1 個なので方式の違いが出ない。
        /// 3 方式を横に並べ、輪郭（フィンが効く）と正面（シェルが効く）を
        /// 同時に見比べられるようにする。
        /// </summary>
        private static void CreateFurMethodDemo(Transform parent, Font font, Vector3 origin)
        {
            Shader furShader = Shader.Find("Natane/Toon Shader (Fur)");
            if (furShader == null)
            {
                CreateLabel(parent, font, "ファー: シェーダー未検出",
                    origin + new Vector3(0f, 0.02f, -2.6f), 0.32f, TextAnchor.UpperCenter);
                return;
            }

            var group = new GameObject("Fur Method Demo");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = origin;
            Undo.RegisterCreatedObjectUndo(group, "Create Natane Feature Showcase");

            string[] labels = { "シェル法", "フィン法", "シェル＋フィン" };

            for (int method = 0; method < 3; method++)
            {
                float x = method * 5f;

                Material material = CreateOrUpdateMaterial(furShader,
                    $"Showcase_FurMethod_{method}",
                    m =>
                    {
                        if (m.HasProperty("_Fur")) m.SetFloat("_Fur", 1f);
                        m.EnableKeyword("_FUR");
                        NataneShowcaseFeatureSetups.TryApply("_FUR", m);
                        Set(m, "_FurMethod", method);
                    });

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Fur {labels[method]}";
                sphere.transform.SetParent(group.transform, false);
                sphere.transform.localPosition = new Vector3(x, 1f, 0f);
                sphere.transform.localScale = Vector3.one * 2f;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                Undo.RegisterCreatedObjectUndo(sphere, "Create Natane Feature Showcase");

                CreateLabel(parent, font, labels[method],
                    origin + new Vector3(x, 0.02f, -2.2f), 0.3f, TextAnchor.UpperCenter);
            }

            CreateLabel(parent, font,
                "ファーの方式（フィンは PC のみ。輪郭の毛の出かたを比較する）",
                origin + new Vector3(5f, 0.02f, -3.4f), 0.32f, TextAnchor.UpperCenter);
        }

        /// <summary>
        /// 落ち影に網点が乗るかの確認。
        ///
        /// 球体グリッドは球同士が影を落とさないので、落ち影の網点は確認できない。
        /// 影を落とす球と、それを受ける広い板を組にして置く。
        /// 左が「落ち影も網点にする」、右が「落ち影を無視」。
        /// </summary>
        private static void CreateCastShadowHalftoneDemo(Transform parent, Font font, Vector3 origin)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return;

            var group = new GameObject("CastShadow Halftone Demo");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = origin;
            Undo.RegisterCreatedObjectUndo(group, "Create Natane Feature Showcase");

            for (int i = 0; i < 2; i++)
            {
                bool castShadowOn = i == 0;
                float offsetX = i * 5f;

                Material receiverMat = CreateOrUpdateMaterial(shader,
                    "Showcase_CastShadowHalftone_" + (castShadowOn ? "On" : "Off"),
                    m =>
                    {
                        Halftone(m);
                        Set(m, "_HalftoneShadowCastShadow", castShadowOn ? 1f : 0f);
                        SetColor(m, "_Color", new Color(0.82f, 0.82f, 0.86f, 1f));
                    });

                // 受け面。影が落ちる範囲を広く取るため板を使う。
                GameObject receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
                receiver.name = "CastShadow Receiver " + (castShadowOn ? "(on)" : "(off)");
                receiver.transform.SetParent(group.transform, false);
                receiver.transform.localPosition = new Vector3(offsetX, 0.1f, 0f);
                receiver.transform.localScale = new Vector3(4f, 0.2f, 4f);
                receiver.GetComponent<Renderer>().sharedMaterial = receiverMat;
                Undo.RegisterCreatedObjectUndo(receiver, "Create Natane Feature Showcase");

                // 影を落とす側。ライトとの間に置く。
                GameObject caster = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                caster.name = "CastShadow Caster " + (castShadowOn ? "(on)" : "(off)");
                caster.transform.SetParent(group.transform, false);
                caster.transform.localPosition = new Vector3(offsetX, 1.6f, 0f);
                caster.transform.localScale = Vector3.one * 1.6f;

                Material casterMat = CreateOrUpdateMaterial(shader, "Showcase_CastShadowCaster",
                    m => SetColor(m, "_Color", new Color(0.5f, 0.52f, 0.6f, 1f)));
                caster.GetComponent<Renderer>().sharedMaterial = casterMat;
                Undo.RegisterCreatedObjectUndo(caster, "Create Natane Feature Showcase");

                CreateLabel(parent, font,
                    castShadowOn ? "落ち影も網点" : "落ち影は無視",
                    origin + new Vector3(offsetX, 0.02f, -2.6f), 0.3f, TextAnchor.UpperCenter);
            }

            CreateLabel(parent, font,
                "落ち影の網点（左=有効 / 右=無効）",
                origin + new Vector3(2.5f, 0.02f, -3.6f), 0.32f, TextAnchor.UpperCenter);
        }

        private static void CreateLegend(Transform parent, Font font, float width, float height)
        {
            CreateLabel(parent, font,
                "全球体は同じ高さ・同一ライティング。差分はマテリアルのみ",
                new Vector3(0f, 0.02f, -height * 0.5f - 2.6f), 0.42f, TextAnchor.UpperCenter);
        }

        private static void CreateLabel(Transform parent, Font font, string text,
                                        Vector3 localPosition, float size, TextAnchor anchor)
        {
            var go = new GameObject("Label: " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            // 床に寝かせる。真上／斜め上から見て読める向きにする。
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.25f;
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Center;
            mesh.color = Color.white;
            if (font != null)
            {
                mesh.font = font;
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = font.material;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Natane Feature Showcase");
        }

        /// <summary>Unity 2022.2 以降は組み込みフォントが LegacyRuntime.ttf に変わっている。</summary>
        private static Font LoadBuiltinFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (Exception) { }

            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (Exception)
                {
                    Debug.LogWarning("[NataneShowcase] 組み込みフォントを取得できませんでした。");
                }
            }
            return font;
        }

        private static void EnsureDirectionalLight(Transform parent)
        {
            foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
                if (light.type == LightType.Directional && light.enabled) return;

            var go = new GameObject("Directional Light (showcase)");
            go.transform.SetParent(parent, false);
            // 斜め上手前から。球体の中で明部・ターミネータ・影部が一望できる角度。
            // 床置きなので、上から斜めに当てて各球体に明部・境界・影部を作る。
            go.transform.localRotation = Quaternion.Euler(42f, -35f, 0f);

            Light created = go.AddComponent<Light>();
            created.type = LightType.Directional;
            created.intensity = 1f;
            created.shadows = LightShadows.Soft;

            Undo.RegisterCreatedObjectUndo(go, "Create Natane Feature Showcase");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder)) return;

            string[] parts = OutputFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// 長いラベルを詰める。カバレッジ区画は列間隔が固定なので、
        /// 名前の長い機能がそのまま出ると隣のラベルと重なる。
        /// 完全な名前は GameObject 名とカバレッジレポートに残っている。
        /// </summary>
        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return value.Substring(0, maxLength - 1) + "…";
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        private static void Set(Material m, string prop, float value)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, value);
        }

        private static void SetColor(Material m, string prop, Color value)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, value);
        }
    }
}
