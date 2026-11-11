using System;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    public enum NataneInspectorTab
    {
        Setup,
        Lighting,
        Surface,
        Effects,
        Output
    }

    /// <summary>
    /// 機能の難易度。Shader初心者がどこから触れば良いか一目で分かるようにする。
    /// Basic  … 最初に触ってよい、見た目の土台になる機能
    /// Intermediate … 見た目を盛るための応用機能
    /// Advanced … 仕組みの理解や負荷への配慮が必要な機能
    /// </summary>
    public enum NataneInspectorDifficulty
    {
        Basic,
        Intermediate,
        Advanced
    }

    public sealed class NataneInspectorSectionDescriptor
    {
        public string Key { get; }
        public NataneInspectorTab Tab { get; }
        public string GroupJP { get; }
        public string GroupEN { get; }
        public string LabelJP { get; }
        public string LabelEN { get; }
        public string ToggleKeyword { get; }
        public string ToggleProperty { get; }
        public string SearchTerms { get; }
        public int Order { get; }

        /// <summary>初心者向けの一行説明（見た目の結果を平易な言葉で）。</summary>
        public string DescriptionJP { get; }
        public string DescriptionEN { get; }
        public NataneInspectorDifficulty Difficulty { get; }

        /// <summary>
        /// 公式ドキュメントサイト(natanetoon.com)の該当ページへの相対パス。
        /// 例: "lighting/rim-direction" → JP: params/lighting/rim-direction.html
        ///                              → EN: en/params/lighting/rim-direction.html
        /// 対応ページが無い/断定できない場合はnullのままにする（推測でリンクを貼らない）。
        /// </summary>
        public string DocSlug { get; }

        public string Label => NataneToonLocalization.IsJapanese ? LabelJP : LabelEN;
        public string Group => NataneToonLocalization.IsJapanese ? GroupJP : GroupEN;
        public string Description => NataneToonLocalization.IsJapanese ? DescriptionJP : DescriptionEN;

        public NataneInspectorSectionDescriptor(
            string key,
            NataneInspectorTab tab,
            string groupJP,
            string groupEN,
            string labelJP,
            string labelEN,
            int order,
            string descriptionJP,
            string descriptionEN,
            NataneInspectorDifficulty difficulty,
            string toggleKeyword = null,
            string toggleProperty = null,
            string searchTerms = null,
            string docSlug = null)
        {
            Key = key;
            Tab = tab;
            GroupJP = groupJP;
            GroupEN = groupEN;
            LabelJP = labelJP;
            LabelEN = labelEN;
            Order = order;
            DescriptionJP = descriptionJP ?? string.Empty;
            DescriptionEN = descriptionEN ?? string.Empty;
            Difficulty = difficulty;
            ToggleKeyword = toggleKeyword;
            ToggleProperty = toggleProperty;
            SearchTerms = searchTerms ?? string.Empty;
            DocSlug = docSlug;
        }

        public int GetSearchScore(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return 0;

            string normalized = query.Trim().ToLowerInvariant();
            string labelJP = LabelJP.ToLowerInvariant();
            string labelEN = LabelEN.ToLowerInvariant();
            string key = Key.ToLowerInvariant();
            string terms = SearchTerms.ToLowerInvariant();
            string keyword = (ToggleKeyword ?? string.Empty).ToLowerInvariant();
            string property = (ToggleProperty ?? string.Empty).ToLowerInvariant();

            if (labelJP == normalized || labelEN == normalized || key == normalized) return 100;
            if (labelJP.StartsWith(normalized) || labelEN.StartsWith(normalized)) return 80;
            if (labelJP.Contains(normalized) || labelEN.Contains(normalized)) return 60;
            if (property.Contains(normalized) || keyword.Contains(normalized)) return 50;
            return terms.Contains(normalized) ? 30 : 0;
        }
    }

    /// <summary>
    /// インスペクターのナビゲーション、検索、ON/OFF操作に使うメタデータ。
    /// 一元管理することで、検索やジャンプメニューからのセクション漏れを防ぐ。
    /// 各セクションには初心者向けの一行説明と難易度を持たせ、
    /// 開かなくても「何が起きるか」がわかるようにしている。
    /// </summary>
    public static class NataneToonInspectorSectionRegistry
    {
        private static NataneInspectorSectionDescriptor D(
            string key, NataneInspectorTab tab, string groupJP, string groupEN,
            string labelJP, string labelEN, int order,
            string descJP, string descEN, NataneInspectorDifficulty difficulty,
            string keyword = null, string property = null, string terms = null, string docSlug = null)
        {
            return new NataneInspectorSectionDescriptor(
                key, tab, groupJP, groupEN, labelJP, labelEN, order,
                descJP, descEN, difficulty, keyword, property, terms, docSlug);
        }

        private static readonly IReadOnlyList<NataneInspectorSectionDescriptor> Sections =
            new List<NataneInspectorSectionDescriptor>
            {
                D("QuickSetup", NataneInspectorTab.Setup, "スタート", "Start", "クイックセットアップ", "Quick Setup", 0,
                    "用途を選ぶだけで見た目の土台を自動でセットします。", "Pick a use-case and it sets up the base look for you.", NataneInspectorDifficulty.Basic,
                    terms: "auto guided preset role look quality 自動 用途 見た目 品質"),
                D("Presets", NataneInspectorTab.Setup, "スタート", "Start", "プリセットと共有", "Presets & Sharing", 1,
                    "作った見た目をまるごと保存して他のマテリアルに配れます。", "Save a whole look and share it with other materials.", NataneInspectorDifficulty.Basic,
                    terms: "material preset copy paste share プリセット 共有"),
                D("MainTexture", NataneInspectorTab.Setup, "基本ルック", "Base Look", "メインテクスチャ", "Main Texture", 10,
                    "モデルの基本の色と模様を決めます。", "Sets the base color and pattern of the model.", NataneInspectorDifficulty.Basic,
                    terms: "albedo base color texture アルベド 色 テクスチャ", docSlug: "basic/main-texture"),
                D("MakeupTextures", NataneInspectorTab.Setup, "基本ルック", "Base Look", "追加テクスチャ", "Additional Textures", 11,
                    "追加の模様やメイクを上に重ねられます。", "Layers extra patterns or makeup on top.", NataneInspectorDifficulty.Intermediate,
                    terms: "makeup layer second third fourth fifth メイク レイヤー", docSlug: "basic/makeup"),
                D("GradientBaseColor", NataneInspectorTab.Setup, "基本ルック", "Base Look", "グラデーションベースカラー", "Gradient Base Color", 12,
                    "上から下へ色がグラデーションで変わります。", "The color fades from top to bottom like a gradient.", NataneInspectorDifficulty.Intermediate,
                    "_GRADIENT_BASE_COLOR", "_GradientBaseColor", "gradient tint position グラデーション 色", docSlug: "advanced/gradient-base-color"),
                D("ScreenTone", NataneInspectorTab.Setup, "基本ルック", "Base Look", "スクリーントーン", "Screen Tone", 13,
                    "漫画の網点トーンを表面に重ねます。", "Overlays comic-style halftone dots on the surface.", NataneInspectorDifficulty.Intermediate,
                    "_SCREEN_TONE", "_ScreenTone", "halftone dot overlay ハーフトーン 網点", docSlug: "effects/screen-tone"),
                D("Shading", NataneInspectorTab.Setup, "陰影", "Shading", "トゥーンシェーディング", "Toon Shading", 20,
                    "光の当たり方でアニメ調の陰影を付けます。", "Adds anime-style shading based on how light hits.", NataneInspectorDifficulty.Basic,
                    terms: "shadow shade toon gradient ramp 影 陰影 ランプ", docSlug: "basic/shading"),
                D("HalftoneShadow", NataneInspectorTab.Setup, "陰影", "Shading", "ハーフトーンシャドウ", "Halftone Shadow", 21,
                    "影の部分を漫画の網点で表現します。", "Renders shadows as comic halftone dots.", NataneInspectorDifficulty.Intermediate,
                    "_HALFTONE_SHADOW", "_HalftoneShadow", "shadow dots comic 影 網点 漫画"),
                D("ShadowEdgeNoise", NataneInspectorTab.Setup, "陰影", "Shading", "影エッジノイズ", "Shadow Edge Noise", 22,
                    "影の境目を手描きのように揺らします。", "Makes shadow edges wobble like hand-drawn lines.", NataneInspectorDifficulty.Intermediate,
                    "_SHADOW_EDGE_NOISE", "_ShadowEdgeNoise", "hand drawn analog edge noise 手描き 揺らぎ"),

                D("AdvancedLighting", NataneInspectorTab.Lighting, "基本ライティング", "Lighting Basics", "ライティング調整", "Lighting Controls", 100,
                    "全体の明るさや光の受け方を細かく調整します。", "Fine-tunes overall brightness and how light is received.", NataneInspectorDifficulty.Basic,
                    terms: "light minimum maximum blend gi lighting 光 ライト", docSlug: "lighting/lighting-general"),
                D("CastShadowColor", NataneInspectorTab.Lighting, "基本ライティング", "Lighting Basics", "キャストシャドウカラー", "Cast Shadow Color", 101,
                    "落ちる影の色を好きな色に変えます。", "Tints the cast shadow to any color you like.", NataneInspectorDifficulty.Intermediate,
                    "_CAST_SHADOW_COLOR", "_CastShadowColorEnable", "cast shadow tint 落ち影 色", docSlug: "lighting/cast-shadow-color"),
                D("LightSnap", NataneInspectorTab.Lighting, "基本ライティング", "Lighting Basics", "ライト方向スナップ", "Light Direction Snap", 102,
                    "光の向きを固定してちらつきを抑えます。", "Locks the light direction to stop flicker.", NataneInspectorDifficulty.Intermediate,
                    "_LIGHT_SNAP", "_LightSnap", "direction stabilize flicker 方向 固定 ちらつき", docSlug: "lighting/light-snap"),
                D("AO", NataneInspectorTab.Lighting, "基本ライティング", "Lighting Basics", "アンビエントオクルージョン", "Ambient Occlusion", 103,
                    "くぼみや隙間を暗くして立体感を出します。", "Darkens crevices and gaps for more depth.", NataneInspectorDifficulty.Intermediate,
                    "_USE_AO", "_UseAO", "ao occlusion cavity ambient 遮蔽", docSlug: "lighting/ao"),
                D("Dithering", NataneInspectorTab.Lighting, "基本ライティング", "Lighting Basics", "ディザリング", "Dithering", 104,
                    "影を細かい点々でざらっと表現します。", "Renders shadows with a grainy dotted pattern.", NataneInspectorDifficulty.Intermediate,
                    "_USE_DITHERING", "_UseDithering", "screen tone dither pattern ディザ 網点", docSlug: "lighting/dithering"),
                D("ShadowShapeRig", NataneInspectorTab.Lighting, "基本ライティング", "Lighting Basics", "影シェイプリグ", "Shadow Shape Rig", 105,
                    "影の形を4つの楕円で狙った形に整えます。", "Shapes the shadow with up to four ellipses you place yourself.", NataneInspectorDifficulty.Advanced,
                    "_SHADOW_SHAPE_RIG", "_ShadowShapeRig", "shadow shape rig shading rig 影 形 リグ 影の形 アートディレクション 楕円", docSlug: "lighting/shadow-shape-rig"),
                D("Specular", NataneInspectorTab.Lighting, "表面の光", "Surface Lighting", "スペキュラー", "Specular", 110,
                    "つやのある光沢ハイライトを足します。", "Adds glossy shiny highlights.", NataneInspectorDifficulty.Intermediate,
                    "_SPECULAR", "_Specular", "highlight gloss roughness 光沢 ハイライト", docSlug: "effects/specular"),
                D("HairSpecular", NataneInspectorTab.Lighting, "表面の光", "Surface Lighting", "ヘアハイライト", "Hair Highlight", 111,
                    "髪に天使の輪のような光を出します。", "Creates an angel-ring highlight on hair.", NataneInspectorDifficulty.Intermediate,
                    "_HAIR_SPECULAR", "_HairSpecular", "hair kajiya highlight flow 髪 天使の輪", docSlug: "effects/hair-specular"),
                D("RimLight", NataneInspectorTab.Lighting, "表面の光", "Surface Lighting", "リムライト", "Rim Light", 112,
                    "輪郭にふちどりの光を足します。", "Adds a glowing outline of light around the edges.", NataneInspectorDifficulty.Basic,
                    "_RIM_LIGHT", "_RimLight", "edge light fresnel outline 輪郭光 縁", docSlug: "effects/rimlight1"),
                D("SSS", NataneInspectorTab.Lighting, "表面の光", "Surface Lighting", "半透明表現", "Subsurface Scattering", 113,
                    "光にかざすと肌が薄く透けて見えます。", "Skin looks faintly translucent when backlit.", NataneInspectorDifficulty.Advanced,
                    "_SSS", "_SSS", "subsurface skin translucent thickness 肌 透過", docSlug: "effects/sss"),
                D("LightVolume", NataneInspectorTab.Lighting, "外部ライティング", "External Lighting", "VRC Light Volumes", "VRC Light Volumes", 120,
                    "ワールドの光を拾って自然になじませます。", "Picks up world lighting so it blends in naturally.", NataneInspectorDifficulty.Advanced,
                    "_USE_LIGHT_VOLUME", "_UseLightVolume", "vrchat voxel world light volume", docSlug: "lighting/light-volumes"),
                D("LTCGI", NataneInspectorTab.Lighting, "外部ライティング", "External Lighting", "LTCGI", "LTCGI", 121,
                    "エリアライトの光をリアルタイムで受けます。", "Receives real-time area-light glow.", NataneInspectorDifficulty.Advanced,
                    "_LTCGI", "_LTCGI", "area light realtime gi エリアライト", docSlug: "lighting/ltcgi"),

                D("NormalMap", NataneInspectorTab.Surface, "マッピング", "Mapping", "ノーマルマップ", "Normal Map", 200,
                    "表面に細かい凹凸があるように見せます。", "Fakes fine bumps and dents on the surface.", NataneInspectorDifficulty.Basic,
                    "_NORMALMAP", "_UseNormalMap", "normal bump detail 法線 凹凸", docSlug: "advanced/normal-map"),
                D("DetailMap", NataneInspectorTab.Surface, "マッピング", "Mapping", "ディテールマップ", "Detail Map", 201,
                    "近づいたときの細かいディテールを足します。", "Adds fine detail visible up close.", NataneInspectorDifficulty.Intermediate,
                    "_DETAIL_MAP", "_DetailMap", "secondary uv detail closeup 細部", docSlug: "advanced/detail-map"),
                D("Parallax", NataneInspectorTab.Surface, "マッピング", "Mapping", "視差マッピング", "Parallax Mapping", 202,
                    "見る角度で表面に奥行きが出ます。", "Surface gains depth as the view angle changes.", NataneInspectorDifficulty.Advanced,
                    "_PARALLAX", "_Parallax", "height depth pom parallax 視差 高さ", docSlug: "advanced/parallax"),
                D("Triplanar", NataneInspectorTab.Surface, "マッピング", "Mapping", "トライプレーナー", "Triplanar Mapping", 203,
                    "UVなしでも模様をきれいに貼れます。", "Maps patterns cleanly even without UVs.", NataneInspectorDifficulty.Advanced,
                    "_TRIPLANAR", "_Triplanar", "projection no uv terrain rock 投影 地形", docSlug: "advanced/triplanar"),
                D("MatCap", NataneInspectorTab.Surface, "質感", "Surface Finish", "MatCap", "MatCap", 210,
                    "球状の画像で金属や光沢の質感を付けます。", "Uses a sphere image to fake metal or glossy looks.", NataneInspectorDifficulty.Intermediate,
                    "_MATCAP", "_MatCap", "sphere map material capture 質感", docSlug: "effects/matcap1"),
                D("ProceduralMatCap", NataneInspectorTab.Surface, "質感", "Surface Finish", "プロシージャルMatCap", "Procedural MatCap", 211,
                    "画像なしで数式から質感を作ります。", "Builds a material look from math, no image needed.", NataneInspectorDifficulty.Intermediate,
                    "_PROCEDURAL_MATCAP", "_ProceduralMatCap", "procedural mathematical texture free 数式", docSlug: "effects/procedural-matcap"),
                D("Reflection", NataneInspectorTab.Surface, "質感", "Surface Finish", "リフレクション", "Reflection", 212,
                    "周囲の景色を表面に映り込ませます。", "Reflects the surroundings on the surface.", NataneInspectorDifficulty.Basic,
                    "_REFLECTION", "_Reflection", "cubemap probe environment reflection 反射", docSlug: "environment/reflection"),
                D("FakeReflection", NataneInspectorTab.Surface, "質感", "Surface Finish", "フェイクリフレクション", "Fake Reflection", 213,
                    "空と地面だけの軽い映り込みを付けます。", "Adds a lightweight sky-and-ground reflection.", NataneInspectorDifficulty.Intermediate,
                    "_FAKE_REFLECTION", "_FakeReflection", "fake sky ground lightweight 擬似反射", docSlug: "effects/fake-reflection"),
                D("Iridescence", NataneInspectorTab.Surface, "質感", "Surface Finish", "イリデッセンス", "Iridescence", 214,
                    "見る角度で色が虹のように変わります。", "Colors shift like a rainbow as you change angle.", NataneInspectorDifficulty.Intermediate,
                    "_IRIDESCENCE", "_Iridescence", "rainbow thin film 玉虫色 虹", docSlug: "environment/iridescence"),
                D("EnvironmentalRim", NataneInspectorTab.Surface, "質感", "Surface Finish", "環境リム", "Environmental Rim", 215,
                    "周囲の明るさに応じてふちが光ります。", "Edges glow based on the surrounding brightness.", NataneInspectorDifficulty.Intermediate,
                    "_ENV_RIM", "_EnvRim", "environment rim sky ground 環境 縁", docSlug: "environment/env-rim"),
                D("Outline", NataneInspectorTab.Surface, "形状表現", "Shape", "アウトライン", "Outline", 220,
                    "モデルの周りに輪郭線を描きます。", "Draws a contour line around the model.", NataneInspectorDifficulty.Basic,
                    "_OUTLINE", "_Outline", "contour line smooth normal 輪郭線 線", docSlug: "effects/outline"),
                D("Fur", NataneInspectorTab.Surface, "形状表現", "Shape", "ファー", "Fur", 221,
                    "表面にふさふさの毛を生やします。", "Grows fluffy fur over the surface.", NataneInspectorDifficulty.Advanced,
                    "_FUR", "_Fur", "shell fur hair pelt 毛皮 毛", docSlug: "effects/fur"),
                D("SurfaceCover", NataneInspectorTab.Surface, "形状表現", "Shape", "サーフェスカバー", "Surface Cover", 222,
                    "上から雪や砂が積もったように見せます。", "Looks like snow or sand piled on top.", NataneInspectorDifficulty.Intermediate,
                    "_SURFACE_COVER", "_SurfaceCover", "snow sand accumulation 雪 砂 堆積", docSlug: "advanced/surface-cover"),
                D("PBR", NataneInspectorTab.Surface, "質感", "Surface Finish", "PBRマテリアル", "PBR Material", 223,
                    "金属や粗さをリアルに再現します。", "Reproduces metal and roughness realistically.", NataneInspectorDifficulty.Advanced,
                    "_PBR", "_EnablePBR", "metallic smoothness pbr 金属 粗さ"),

                D("Emission", NataneInspectorTab.Effects, "発光とスタイル", "Glow & Style", "エミッション", "Emission", 300,
                    "指定した部分を自分から光らせます。", "Makes chosen parts glow on their own.", NataneInspectorDifficulty.Basic,
                    "_EMISSION", "_Emission", "glow bloom emissive 発光 ブルーム", docSlug: "effects/emission"),
                D("Hologram", NataneInspectorTab.Effects, "発光とスタイル", "Glow & Style", "ホログラムとグリッチ", "Hologram & Glitch", 301,
                    "走査線やグリッチでデジタル風に見せます。", "Adds scanlines and glitches for a digital look.", NataneInspectorDifficulty.Intermediate,
                    "_HOLOGRAM", "_Hologram", "scanline glitch digital hologram ノイズ", docSlug: "effects/hologram"),
                D("IllustrationStyle", NataneInspectorTab.Effects, "発光とスタイル", "Glow & Style", "イラスト調スタイル", "Illustration Style", 302,
                    "水彩やハッチングでイラスト風に加工します。", "Restyles into watercolor or hatching illustration.", NataneInspectorDifficulty.Intermediate,
                    "_COLOR_QUANTIZE", "_UseColorQuantize", "watercolor kuwahara hatching lut quantize イラスト 水彩", docSlug: "effects/color-quantize"),
                D("PixelArt", NataneInspectorTab.Effects, "発光とスタイル", "Glow & Style", "ピクセルアート化", "Pixel Art", 303,
                    "3Dモデルをドット絵・レトロゲーム風にします。", "Turns the 3D model into pixel art / retro-game style.", NataneInspectorDifficulty.Basic,
                    "_PIXEL_ART", "_PixelArt", "pixel art dot retro game palette dither low res ドット絵 レトロ ピクセル 減色 パレット", docSlug: "effects/pixel-art"),
                D("LineBoil", NataneInspectorTab.Effects, "作画表現", "Line Art FX", "ラインボイル", "Line Boil", 305,
                    "線が手描きアニメのようにパチパチ揺れます。", "Lines jitter frame-to-frame like hand-drawn animation.", NataneInspectorDifficulty.Intermediate,
                    "_LINE_BOIL", "_LineBoil", "boil jitter wobble hand drawn frame line ボイル 揺れ 手描き 線 作画", docSlug: "effects/line-boil"),
                D("ShapedHighlight", NataneInspectorTab.Effects, "作画表現", "Line Art FX", "シェイプハイライト", "Shaped Highlight", 306,
                    "目や宝石にハートや星型のハイライトを入れます。", "Puts heart or star shaped highlights on eyes or gems.", NataneInspectorDifficulty.Intermediate,
                    "_SHAPED_HIGHLIGHT", "_ShapedHighlight", "shaped highlight star heart circle eye catchlight gem 目 ハイライト 星 ハート キャッチライト", docSlug: "effects/shaped-highlight"),
                D("Topographic", NataneInspectorTab.Effects, "作画表現", "Line Art FX", "トポグラフィック", "Topographic", 307,
                    "等高線のような模様が表面に走ります。", "Contour-line patterns run across the surface.", NataneInspectorDifficulty.Advanced,
                    "_TOPOGRAPHIC", "_Topographic", "topographic contour lines bands rings elevation 等高線 地形 縞 模様", docSlug: "effects/topographic"),
                D("Glitter", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "グリッター", "Glitter", 310,
                    "表面にキラキラ光るラメを散らします。", "Scatters sparkling glitter across the surface.", NataneInspectorDifficulty.Intermediate,
                    "_GLITTER", "_Glitter", "sparkle glint glitter ラメ 輝き", docSlug: "effects/glitter"),
                D("Drip", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "雫エフェクト", "Drip Effect", 311,
                    "水滴が表面を流れるように見せます。", "Makes water droplets appear to run down.", NataneInspectorDifficulty.Intermediate,
                    "_WATER_DRIP", "_WaterDrip", "water drop rain 雫 水滴", docSlug: "effects/waterdrip"),
                D("Smear", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "スミア", "Smear", 312,
                    "動きに合わせて残像が伸びます。", "Stretches an afterimage along the motion.", NataneInspectorDifficulty.Intermediate,
                    "_SMEAR", "_Smear", "afterimage stretch trail 残像 引き伸ばし", docSlug: "effects/smear"),
                D("Decal", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "デカール", "Decal", 313,
                    "シールのように模様を貼り付けます。", "Sticks a pattern on like a decal.", NataneInspectorDifficulty.Intermediate,
                    "_DECAL", "_Decal", "sticker overlay 貼り付け 模様", docSlug: "effects/decal"),
                D("Caustics", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "サーフェス・コースティクス", "Surface Caustics", 314,
                    "水中の揺れる光や魔法の光模様が表面を流れます。", "Shimmering underwater light or magical light patterns flow across the surface.", NataneInspectorDifficulty.Intermediate,
                    "_CAUSTICS", "_Caustics", "caustics water light underwater ripple magic 水中 コースティクス 光 揺らぎ 魔法", docSlug: "effects/caustics"),
                D("ShadowBokeh", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "影の玉ボケ（木漏れ日）", "Shadow Bokeh (Dappled Light)", 316,
                    "影の中に木漏れ日のような円形の光斑を落とします。", "Drops dappled circles of light into shadowed areas, like sunlight through leaves.", NataneInspectorDifficulty.Intermediate,
                    "_SHADOW_BOKEH", "_ShadowBokeh", "bokeh dappled komorebi sunlight leaves shadow circle 木漏れ日 玉ボケ 光斑 葉 影 丸", docSlug: "effects/shadow-bokeh"),
                D("Lenticular", NataneInspectorTab.Effects, "サーフェスFX", "Surface FX", "レンチキュラー", "Lenticular", 315,
                    "見る角度によって絵柄や表情が切り替わります(ホログラムカード風)。", "Artwork or expression switches depending on the viewing angle (like a hologram card).", NataneInspectorDifficulty.Intermediate,
                    "_LENTICULAR", "_Lenticular", "lenticular hologram card view angle flip frame atlas parallax レンチキュラー ホログラム 角度 表情 切り替え", docSlug: "effects/lenticular"),
                D("VirtualExpression", NataneInspectorTab.Effects, "演出", "Presentation", "バーチャル表現", "Virtual Expression", 320,
                    "溶けて消える演出などを付けます。", "Adds dissolve-and-vanish style effects.", NataneInspectorDifficulty.Advanced,
                    terms: "dissolve virtual expression ディゾルブ 消滅", docSlug: "effects/dissolve"),
                D("AudioLink", NataneInspectorTab.Effects, "演出", "Presentation", "AudioLink", "AudioLink", 321,
                    "音楽に合わせて光や色が動きます。", "Light and color react to the music.", NataneInspectorDifficulty.Advanced,
                    "_AUDIOLINK", "_AudioLink", "music reactive vrchat 音楽 連動", docSlug: "effects/audiolink"),
                D("VertexAnimation", NataneInspectorTab.Effects, "モーション", "Motion", "頂点アニメーション", "Vertex Animation", 330,
                    "風や呼吸のように形が揺れ動きます。", "The shape sways like wind or breathing.", NataneInspectorDifficulty.Advanced,
                    "_VERTEX_ANIMATION", "_VertexAnimation", "wind breath pulse motion 風 呼吸 脈動", docSlug: "advanced/vertex-animation"),
                D("VAT", NataneInspectorTab.Effects, "モーション", "Motion", "VAT", "VAT", 331,
                    "焼き込んだ頂点アニメを再生します。", "Plays back baked vertex animation.", NataneInspectorDifficulty.Advanced,
                    "_VAT", "_VAT", "vertex animation texture houdini 頂点アニメ", docSlug: "advanced/vat"),
                D("Tessellation", NataneInspectorTab.Effects, "モーション", "Motion", "テッセレーション", "Tessellation", 332,
                    "メッシュを細分化して滑らかにします。", "Subdivides the mesh for smoother curves.", NataneInspectorDifficulty.Advanced,
                    "_TESSELLATION", "_Tessellation", "phong smooth subdivision 分割 スムーズ", docSlug: "advanced/tessellation"),
                D("Refraction", NataneInspectorTab.Effects, "空間FX", "Spatial FX", "屈折", "Refraction", 340,
                    "背景がガラス越しのように歪みます。", "The background warps as if seen through glass.", NataneInspectorDifficulty.Advanced,
                    "_REFRACTION", "_Refraction", "grabpass ior glass 屈折 ガラス", docSlug: "environment/refraction"),
                D("HeightFog", NataneInspectorTab.Effects, "空間FX", "Spatial FX", "ハイトフォグ", "Height Fog", 341,
                    "足元にたまる霧を出します。", "Adds fog that pools around the base.", NataneInspectorDifficulty.Intermediate,
                    "_HEIGHT_FOG", "_HeightFog", "fog mist atmosphere 霧 高さ", docSlug: "environment/height-fog"),
                D("DepthColorFade", NataneInspectorTab.Effects, "空間FX", "Spatial FX", "深度カラーフェード", "Depth Color Fade", 342,
                    "遠くほど色が薄れて空気感が出ます。", "Distant parts fade in color for an aerial feel.", NataneInspectorDifficulty.Intermediate,
                    "_DEPTH_COLOR_FADE", "_DepthColorFade", "distance atmosphere depth color 遠景 空気遠近"),
                D("FXModulator", NataneInspectorTab.Effects, "制御", "Control", "FXモジュレーター", "FX Modulator", 350,
                    "時間や音に合わせて他の効果を自動で動かします。", "Automatically animates other effects over time or sound.", NataneInspectorDifficulty.Advanced,
                    "_FX_MODULATOR", "_FXModulator", "modulator lfo automate audiolink time sine saw drive control 制御 自動 変調 うねり", docSlug: "effects/fx-modulator"),

                D("MirrorControl", NataneInspectorTab.Output, "VRChat", "VRChat", "ミラー・カメラ制御", "Mirror / Camera Control", 400,
                    "鏡やカメラでの表示・非表示を切り替えます。", "Shows or hides in mirrors and cameras.", NataneInspectorDifficulty.Intermediate,
                    "_MIRROR_CONTROL", "_MirrorControl", "mirror camera photo visibility 鏡 カメラ 写真", docSlug: "advanced/mirror-control"),
                D("MirrorTexture", NataneInspectorTab.Output, "VRChat", "VRChat", "鏡・カメラ写り分け", "Mirror / Camera Alt Texture", 401,
                    "鏡やカメラの中だけ別の見た目に変わります。", "Shows a different look only inside mirrors or cameras.", NataneInspectorDifficulty.Intermediate,
                    "_MIRROR_TEXTURE", "_MirrorTexture", "mirror camera alternate secret texture 鏡 写り分け", docSlug: "advanced/mirror-alt-texture"),
                D("Backface", NataneInspectorTab.Output, "表示制御", "Visibility", "裏面テクスチャ", "Backface Texture", 410,
                    "裏面に別のテクスチャを表示します。", "Shows a separate texture on the back faces.", NataneInspectorDifficulty.Intermediate,
                    "_BACKFACE_TEXTURE", "_BackfaceTexture", "backface cull double sided 裏面 両面", docSlug: "advanced/backface"),
                D("Video", NataneInspectorTab.Output, "表示制御", "Visibility", "ビデオテクスチャ", "Video Texture", 411,
                    "動画やレンダーテクスチャを表示します。", "Displays video or a render texture.", NataneInspectorDifficulty.Intermediate,
                    "_VIDEO_TEXTURE", "_VideoTexture", "video render texture movie 動画", docSlug: "advanced/video-texture"),
                D("HeightFade", NataneInspectorTab.Output, "表示制御", "Visibility", "高さフェード", "Height Fade", 412,
                    "高さに応じて上か下が消えていきます。", "Fades out from the top or bottom by height.", NataneInspectorDifficulty.Intermediate,
                    "_HEIGHT_FADE", "_HeightFade", "height alpha fade feet 高さ 足元 消す", docSlug: "advanced/height-fade"),
                D("IntersectionFade", NataneInspectorTab.Output, "表示制御", "Visibility", "交差フェード", "Intersection Fade", 413,
                    "他の物と接した所が光ったり消えたりします。", "Contact points with other objects glow or fade.", NataneInspectorDifficulty.Advanced,
                    "_INTERSECTION_FADE", "_IntersectionFade", "depth contact intersection 交差 接触", docSlug: "advanced/intersection-fade"),
                D("DistanceFade", NataneInspectorTab.Output, "表示制御", "Visibility", "距離フェード", "Distance Fade", 414,
                    "離れると徐々に消えていきます。", "Gradually disappears as you move away.", NataneInspectorDifficulty.Intermediate,
                    "_DISTANCE_FADE", "_DistanceFade", "distance lod camera fade 距離 消える", docSlug: "advanced/distance-fade"),
                D("PerspectiveFlat", NataneInspectorTab.Output, "投影と変形", "Projection", "パースフラット", "Perspective Flatten", 420,
                    "遠近感を抑えて平面イラスト風にします。", "Flattens perspective for a 2D illustration look.", NataneInspectorDifficulty.Advanced,
                    "_PERSPECTIVE_FLAT", "_PerspectiveFlat", "2d depth compression perspective 平面 パース", docSlug: "advanced/perspective-flat"),
                D("FaceOrtho", NataneInspectorTab.Output, "投影と変形", "Projection", "顔直交投影", "Face Ortho Projection", 421,
                    "顔のパースの崩れを抑えます。", "Reduces perspective distortion on the face.", NataneInspectorDifficulty.Advanced,
                    "_FACE_ORTHO", "_FaceOrtho", "face fov orthographic vr 顔 直交", docSlug: "advanced/face-ortho"),
                D("Ghost", NataneInspectorTab.Output, "バリアント設定", "Variant", "ゴースト", "Ghost", 430,
                    "幽霊のように半透明で光る姿になります。", "Turns semi-transparent and glowing like a ghost.", NataneInspectorDifficulty.Advanced,
                    terms: "ghost transparent spectral fresnel 幽霊 透明", docSlug: "effects/ghost"),
                D("XRay", NataneInspectorTab.Output, "バリアント設定", "Variant", "X-Ray", "X-Ray", 433,
                    "壁や物に隠れた部分だけ輪郭やパターンで表示します。", "Shows only the parts hidden behind walls or objects, as an outline or pattern.", NataneInspectorDifficulty.Advanced,
                    terms: "xray x-ray occluded hidden behind wall outline scanline fresnel pulse see through 透視 レントゲン 隠れた 輪郭 スキャンライン", docSlug: "effects/xray"),
                D("QuestLite", NataneInspectorTab.Output, "バリアント設定", "Variant", "Quest軽量パス", "Quest Lite", 431,
                    "Quest向けに重いエフェクトを省いて軽くします。", "Drops heavy effects to run light on Quest.", NataneInspectorDifficulty.Advanced,
                    "_QUEST_LITE", "_QuestLite", "quest mobile optimization 軽量 モバイル", docSlug: "advanced/quest-lite"),
                D("BackgroundLightmap", NataneInspectorTab.Output, "バリアント設定", "Variant", "背景ライトマップ", "Background Lightmap", 432,
                    "背景用にベイクした光を反映します。", "Applies baked lighting for backgrounds.", NataneInspectorDifficulty.Advanced,
                    terms: "background lightmap bake gi 背景 ベイク"),
                D("Rendering", NataneInspectorTab.Output, "出力", "Output", "レンダリング設定", "Rendering Settings", 440,
                    "透明・不透明や描画順などを決めます。", "Sets transparency, opacity and draw order.", NataneInspectorDifficulty.Basic,
                    terms: "render queue blend zwrite stencil cull 描画 ステンシル", docSlug: "advanced/rendering"),
                D("FeatureOverview", NataneInspectorTab.Output, "診断", "Diagnostics", "有効機能", "Active Features", 450,
                    "今どの機能がオンかを一覧で確認できます。", "Lists which features are currently on.", NataneInspectorDifficulty.Basic,
                    terms: "feature overview active toggle 機能 一覧"),
                D("Performance", NataneInspectorTab.Output, "診断", "Diagnostics", "パフォーマンス", "Performance", 451,
                    "負荷の目安をチェックできます。", "Check an estimate of the rendering cost.", NataneInspectorDifficulty.Basic,
                    terms: "sampler pass cost rating optimize 性能 最適化"),
            };

        public static IReadOnlyList<NataneInspectorSectionDescriptor> All => Sections;

        public static IEnumerable<NataneInspectorSectionDescriptor> ForTab(NataneInspectorTab tab)
        {
            return Sections.Where(section => section.Tab == tab).OrderBy(section => section.Order);
        }

        public static NataneInspectorSectionDescriptor Find(string key)
        {
            return Sections.FirstOrDefault(section => string.Equals(section.Key, key, StringComparison.Ordinal));
        }

        public static NataneInspectorSectionDescriptor FindByKeyword(string keyword)
        {
            return Sections.FirstOrDefault(section =>
                string.Equals(section.ToggleKeyword, keyword, StringComparison.Ordinal));
        }

        public static IEnumerable<NataneInspectorSectionDescriptor> Search(string query)
        {
            return Sections
                .Select(section => new { Section = section, Score = section.GetSearchScore(query) })
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Section.Order)
                .Select(result => result.Section);
        }
    }
}
