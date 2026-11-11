# NPR2026 追補: 背景・ワールド表現

キャラクター表現（[NPR2026_PLAN.md](NPR2026_PLAN.md)）とは別に、
**背景・ワールド側で追加できる表現**を調査した結果の追補。

結論として **追加4件（P9〜P12）**。うち P9 と P10 は
「既に実装済みの機能が背景用バリアントにだけ載っていない」という構造的な欠けで、
実装コストが小さく効果が大きい。

---

## 1. 既にカバーできている領域

背景側も想像以上に揃っていた。再調査を防ぐため記録する。

| 背景表現 | 本パッケージの対応 |
|---|---|
| 遠景の削減（インポスター的手法） | `Shaders/Reduction/ParallaxBackground.shader`（湾曲面に対する**ランタイムPOM**）+ `Editor/Reduction/BackgroundReductionWindow.cs` + `BakeDepth.shader` / `BakeNormal.shader` |
| 窓の中に部屋が見える（インテリアマッピング） | `Shaders/ParallaxBox/ParallaxBoxInterior.shader` + `DepthCapture.shader` |
| エリアライト風の間接光 | `_LTCGI`（Background の SubShader Tags は `"LTCGI"="ALWAYS"`） |
| ボクセルライティング | `_USE_LIGHT_VOLUME` / `_LIGHT_VOLUME_SPECULAR` |
| ライトマップとトゥーンの共存 | `_LightmapToonInfluence` / `_LightmapIntensity` / Meta パス |
| 地形のブレンド | `_TRIPLANAR` / `_DETAIL_MAP` |
| 雪・汚れ・苔の被り | `_SURFACE_COVER` |
| 風で揺れる草木 | `_VERTEX_ANIMATION`（`Wave` / `Breath` / `Wind` / `Pulse`） |
| 高さフォグ | `_HEIGHT_FOG` |
| 水面の光の揺らぎ | `_CAUSTICS` / `_WATER_DRIP` |
| 水際のフェード | `_DEPTH_COLOR_FADE` / `_INTERSECTION_FADE` |
| 容器の中の液体 | `Shaders/NataneToon/Effects/NataneFakeFluid.shader`（数式のみ。シミュレーション無し） |
| 大量オブジェクトの頂点アニメ | `_VAT` / `_VAT_NORMAL`（CEDEC2026 でも VAT 活用の講演あり） |
| 動画テクスチャ | `_VIDEO_TEXTURE` |

### 対象外にする領域

**3D Gaussian Splatting**：2026年の VRChat ワールドで最も動きがある領域で、
VRCGS は v4 で**最大1億スプラット**の描画に対応し、`.ply` / `.spz` インポータ、
スキャンから GPU ハイトマップでコリジョンを生成する機能まで持つ。
ただしこれは専用のソート・描画パイプラインを持つ独立ツールの領域で、
トゥーンシェーダーパッケージが担うものではない。本計画では扱わない。

**Volumetric Light Beam 系のアセット**：VRChat ワールドでは
カスタムスクリプトを必要とするため動作しない。→ 板ポリ方式で代替する（§P11）。

---

## 2. P9. Background バリアントに絵画調（美術ボード）機能が載っていない

### 現状

`#pragma shader_feature_local` を本体と Background で突き合わせると、
**本体111件・Background 84件**で、本体にしか無いものが32件ある。

そのうち**背景で価値が高いのに載っていない**もの:

| 欠けている機能 | 背景での用途 |
|---|---|
| `_WATERCOLOR` | 背景美術の水彩・絵の具の質感（`_WCGranulationTex` / `_WCPaperTex`） |
| `_KUWAHARA_FILTER` | 油彩・絵画調のフラット化（美術ボード風の筆致） |
| `_HATCHING` | 影に線を入れる漫画・絵本調の背景 |
| `_COLOR_QUANTIZE` | 色数を落として作画に寄せる |
| `_OUTLINE_HAND_DRAWN` | 建物や小物の手描き輪郭 |
| `_LUT_3D` | シーン全体の色設計をLUTで統一 |
| `_SOFT_FILTER` | 遠景の空気遠近（ソフトフォーカス） |
| `_COLOR_BLEEDING` | 隣接色のにじみ（アナログ感） |
| `_CHROMATIC_ABERRATION` | レンズ感の付与 |
| `_PARALLAX` | レンガ・タイル・格子の擬似奥行き |
| `_REFRACTION` | 窓ガラス・水中の歪み |
| `_PCSS` | 柔らかい落ち影（建物の影が硬く出るのを緩和） |
| `_TESSELLATION` / `_TESS_DISPLACEMENT` | 地形の細分化と起伏 |

一方、**キャラクター専用で除外が妥当**なもの（意図的な欠けとして扱う）:
`_SDF_MAP` / `_FACE_SDF_ROTATION` / `_HAIR_SPECULAR` / `_HAIR_SPEC_MASK` /
`_HAIR_SPEC_SHIFT_TEX` / `_SSS` / `_SSS_LUT` / `_SMEAR` / `_HOLOGRAM` /
`_HOLOGRAM_NOISE` / `_GLITCH` / `_GLITCH_STRETCH` / `_SCREEN_EDGE` /
`_BACKFACE_TEXTURE` / `_DITHERING_ALPHA` / `_HASHED_ALPHA`
（後ろ3件は §P10 で植物向けに追加する）

### なぜ問題か

アニメ背景（美術ボード）を3DCGで作るときの定石は
**「水彩・絵画調・色数削減・手描き輪郭」でフラットに寄せること**であり、
本パッケージはその手段を**すべて持っているのに、背景用バリアントからは使えない**。
キャラは絵画調にできるが背景はできないため、同じワールド内でルックが揃わない。

### 提案

上表13件を `NataneToonShader_Background.shader` へ展開する。

- **新規実装は不要**。`Properties` と各パスの `#pragma shader_feature_local` を追加し、
  必要な include が `NataneToonCore.hlsl` 経由で入っていることを確認するだけ
- `_PCSS` / `_TESSELLATION` / `_TESS_DISPLACEMENT` は重いので、
  `MaterialValidator` と `PerformanceBudgetTool` に警告を追加する
- `_REFRACTION` は GrabPass を伴うため Quest 非対応。`_QUEST_LITE` で確実に剥がれることを確認する
- 変種数が増えるため、[NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md](NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md)
  の **W3 パリティ検査を先に入れてから**着手する（意図的除外16件を宣言として書けるようにする）

段階的に入れるなら **絵画調の5件（`_WATERCOLOR` / `_KUWAHARA_FILTER` / `_COLOR_QUANTIZE` /
`_LUT_3D` / `_SOFT_FILTER`）を第一弾**にする。これだけで美術ボード風の背景が作れる。

---

## 3. P10. Background に Cutout / Transparent が無い

### 現状

`NataneToonShader_Background.shader` の SubShader Tags は

```
"RenderType"="Opaque"
"Queue"="Geometry"
"VRCFallback"="Toon"
```

で**不透明専用**。`_Cutoff` プロパティも存在しない（grep 0件）。

一方、背景専用の機能5件（`_TRIPLANAR` / `_DETAIL_MAP` / `_HEIGHT_FOG` /
`_SURFACE_COVER` / `_PBR`）は **Background にしか無い**。

つまり:

| やりたいこと | 現状 |
|---|---|
| 葉・柵・金網（Cutout）を三平面マッピングしたい | **不可**。Cutout に行くと `_TRIPLANAR` が無い |
| 窓ガラス・半透明の水面に高さフォグを乗せたい | **不可**。Transparent に行くと `_HEIGHT_FOG` が無い |
| 苔や雪の被りを半透明オブジェクトに乗せたい | **不可**。`_SURFACE_COVER` は Background 専用 |

**背景専用機能と半透明が排他**になっている。

### 提案

`Variants/NataneToonShader_Background_Cutout.shader` と
`Variants/NataneToonShader_Background_Transparent.shader` を追加する。

- 既存 Cutout / Transparent バリアントの `Tags` / `Blend` / `ZWrite` / `Cull` 設定と、
  Background の機能セットを合わせる
- `VRCFallback` はそれぞれ `"ToonCutout"` / `"ToonTransparent"`
- Cutout 側には**植物向けの3件**を載せる
  - `_DITHERING_ALPHA` / `_HASHED_ALPHA` / `_BLUE_NOISE_DITHER`
    — Cutout の葉がジャギる問題（VRChatのワールド制作でよく指摘される）への対処
  - `_BACKFACE_TEXTURE` — 葉の裏面に別テクスチャ（裏地の色を変える）
- 併せて**葉の透過光（translucency）**を検討する。`_SHEEN` が近いが、
  「裏から光が透ける」表現は別途 `NdotL` の裏側成分が必要。
  ただしこれは新規実装になるため P9 / P10 の第一弾には含めない

シェーダーファイルが 13 → 15 に増えるため、これも **W3 を先に入れてから**着手する。

---

## 4. P11. 光の筋（Light Shaft）専用シェーダー

### 動機

窓から差す光の筋、木漏れ日、ステージのスポット光は背景演出の定番。
VRChat ワールドでは **カスタムスクリプトが使えないため Volumetric Light Beam 系の
アセットは動作せず**、実際には**板ポリや円錐メッシュを置いてシェーダーで見せる**のが定石。

本パッケージには該当シェーダーが無い（`lightshaft` / `godray` / `skybox` は grep ゼロヒット）。
`_HEIGHT_FOG` は霧であって筋ではない。

### 仕様

`Shaders/NataneToon/Effects/NataneLightShaft.shader`（新規）。

| プロパティ | 内容 |
|---|---|
| `_ShaftColor` (HDR) | 光の色 |
| `_ShaftIntensity` | 強度 |
| `_EdgeSoftness` | 法線とビュー方向の角度で端を消す（板の輪郭を見せない） |
| `_LengthFade` | 根元→先端の減衰カーブ |
| `_DepthFade` | 深度差でのフェード（床や壁との交差線を消す）。`_DEPTH_COLOR_FADE` の資産を流用 |
| `_NoiseTex` / `_NoiseScrollSpeed` / `_NoiseStrength` | 埃の揺らぎ |
| `_CameraFade` | カメラが近すぎるときのフェード（板が見えるのを防ぐ） |
| `_ViewAngleFade` | 真横から見たときに消す |
| AudioLink 連動 | `_AUDIOLINK` を載せてライブ演出に使えるようにする |

```
Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "VRCFallback" = "ToonTransparent" }
Blend One One      // 加算
ZWrite Off
ZTest LEqual
Cull Off           // 両面。板ポリを裏から見ても消えないように
```

- 深度フェードは `_CameraDepthTexture` に依存する。VRChat ワールドでは
  深度テクスチャが常に有効とは限らないため、**深度なしでも成立する経路**
  （`_LengthFade` + `_EdgeSoftness` のみ）を既定にし、深度利用はオプトインにする
- Quest でも動くよう、ノイズ1枚以内・GrabPass なしで完結させる

### セットアップツール

`Editor/NataneToon/Tools/LightShaftSetupTool.cs`（新規）:
選択したライトの向きに合わせて円錐または板ポリを生成し、マテリアルを割り当てる。
窓（矩形）から差す形と、スポット（円錐）の2種のプリセットを持つ。

---

## 5. P12. スタイライズ空・雲

### 動機

アニメ背景で最も面積を占めるのが空と雲。本パッケージには空・雲のシェーダーが無い。
この領域は既に定番アセット（RED_SIM の Beautiful Sky Shader など、
ボリュメトリック雲・昼夜サイクル・星図）が存在するが、
**セル調・段階トーンの「アニメの空」**に振ったものは少ない。

### 仕様

`Shaders/NataneToon/Effects/NataneSkyDome.shader`（新規）。

| パラメータ | 内容 |
|---|---|
| `_SkyTopColor` / `_SkyHorizonColor` / `_SkyGradientPower` | 空のグラデーション |
| `_CloudTex` または プロシージャル | 雲の形。2〜3オクターブの value noise でも可 |
| `_CloudSteps` | 雲の階調段数（2〜4）。**セル調の要** |
| `_CloudColorLit` / `_CloudColorShadow` | 雲の明部・暗部の2色 |
| `_CloudOutlineWidth` / `_CloudOutlineColor` | 雲の輪郭線（作画寄せ） |
| `_CloudScrollSpeed` / `_CloudScale` | 流れる速度と大きさ |
| `_HorizonHaze` | 地平線付近の霞 |
| `_SunDirection` / `_SunSize` / `_SunColor` | 太陽の位置と見た目 |
| `_LineBoilAffectCloud` | `_LINE_BOIL` と連動させてコマ打ちにする |

**Skybox ではなくドームメッシュ用にする。** 理由:

- Skybox の差し替えはワールド全体に影響し、ミラーやカメラ越しの見た目の制御が難しい
- ドームなら Queue とレイヤーで制御でき、**ワールドの一部エリアだけ空を変える**ことができる
- Built-in RP のライトマップ・リフレクションプローブとの共存も素直

将来 Skybox 形式（`Shader "Skybox/..."`）を派生させる余地は残すが、第一弾はドームとする。

雲の階調を `_CloudSteps` で段階化し、輪郭線を持たせる点が既存アセットとの差別化になる。
`_LINE_BOIL` 連動で「雲もコマ打ちで揺れる」状態を作れれば、
キャラ・背景・空が同じ低fpsで動く統一感が出る（[NPR2026_PLAN.md](NPR2026_PLAN.md) の
手描きアニメ組み合わせ例と同じ思想）。

---

## 6. 優先順位

| 順 | 項目 | 内容 | 新規実装 |
|---|---|---|---|
| 1 | **P9 第一弾** | Background へ絵画調5件（`_WATERCOLOR` / `_KUWAHARA_FILTER` / `_COLOR_QUANTIZE` / `_LUT_3D` / `_SOFT_FILTER`） | **不要**（展開のみ） |
| 2 | P9 第二弾 | 残る8件（`_PARALLAX` / `_REFRACTION` / `_PCSS` / `_HATCHING` / `_OUTLINE_HAND_DRAWN` / `_COLOR_BLEEDING` / `_CHROMATIC_ABERRATION` / Tessellation 2件） | 不要 |
| 3 | P10 | Background_Cutout / Background_Transparent + 植物向け3件 | 少（設定の組み替え） |
| 4 | P11 | Light Shaft シェーダー + セットアップツール | 中 |
| 5 | P12 | Sky Dome シェーダー | 中 |

**P9 / P10 はどちらもシェーダーファイルの構成を変えるため、
[W3 のパリティ検査](NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md)を先に入れてから着手する。**
意図的な除外（キャラ専用16件）をレジストリに宣言として書けるようにしておかないと、
「載せ忘れ」と「意図的に載せない」の区別がつかなくなる。

## 7. 検証（各項目共通）

- P9 / P10: 機能OFF時に既存の背景マテリアルの見た目が変わらないこと
- P9: `_REFRACTION` が `_QUEST_LITE` で確実に剥がれること
- P10: Cutout の葉でディザ系3件が実際にジャギを軽減すること
- P11: 深度テクスチャが無い環境でも成立すること／板ポリの輪郭が見えないこと
- P12: ミラー内・カメラ越しで破綻しないこと
- 全項目: 2022.3.28f1 / 6000.0.55f1 の batchmode で error / warning ゼロ

---

## 出典

- VRChat Gaussian Splatting v2 公開（CGWORLD）: https://cgworld.jp/flashnews/01-202507-VRChatGaussianSplatting-v2.html
- VRCGS v4 で最大1億スプラットに対応（MoguLive）: https://www.moguravr.com/vrcgs-v4-vrchat-gaussian-splatting/
- 「3D Gaussian Splatting」技術を使ったワールド 注目作5選（MoguLive）: https://www.moguravr.com/vrchat-3d-gaussian-splatting-review/
- VRChatのワールドで植物を少しでも綺麗に見せたい人向けの記事（wata23）: https://note.com/watahumi_mina/n/n574c307818ae
- VRChat ワールドの軽量化 ～描画の仕組みと計測方法～（wata23）: https://note.com/watahumi_mina/n/ned5a874f6c52
- VRChat ワールド軽量化のためのLODについて（Kluele）: https://note.com/kluele_vrc/n/nfb8593fbd775
- VRC Light Volumes / Compatible Shaders: https://github.com/REDSIM/VRCLightVolumes/blob/main/Documentation/CompatibleShaders.md
- Beautiful Sky Shader（RED_SIM）: https://www.patreon.com/posts/beautiful-sky-35667377
- Volumetric Lighting（VRChat Feature Requests）: https://vrchat.canny.io/feature-requests/p/volumetric-lighting
- 自然の景色のワールドを作る方法（sparkly-box）: https://tiny-sparklies.com/20260113-2/
