# NPR2026 実装計画

2026年時点の業界動向（CEDEC2026 / SIGGRAPH 2026 / 商用NPRタイトル / VRChat周辺）を調査し、
本パッケージに未実装の表現・自動生成機能を洗い出した実装計画。

- ブランチ: `feature/develop/npr2026`（ベース: `develop` @ `6323794` / v1.6.5）
- 対象スコープ: **Stage A（P2 + P5）／ P1 Shadow Shape Rig ／ P3 撮影模倣ScreenFX**
  + 追補として Stage A'（P6 / P7 / P8）、背景表現 Stage B'（P9〜P12）、ワークフロー整備（W1〜W10）
- 対象外: P4 Toon Standard相互運用（今回は見送り）、ニューラルシェーディング系（後述）

## 個別仕様書

実装レベルの詳細は以下に分割して記載している。

| 項目 | 仕様書 | Stage |
|---|---|---|
| P1 Shadow Shape Rig | [NPR2026_P1_SHADOW_SHAPE_RIG.md](NPR2026_P1_SHADOW_SHAPE_RIG.md) | B |
| P2 顔SDF影マップベイク | [NPR2026_P2_FACE_SDF_BAKE.md](NPR2026_P2_FACE_SDF_BAKE.md) | A |
| P3 撮影模倣 ScreenFX | [NPR2026_P3_SCREENFX_CINEMATIC.md](NPR2026_P3_SCREENFX_CINEMATIC.md) | C |
| P5 ハッチングTAM生成 | [NPR2026_P5_HATCHING_TAM_GENERATOR.md](NPR2026_P5_HATCHING_TAM_GENERATOR.md) | A |
| P8 ディゾルブのオーサリング（+ F2〜F4 修正） | [NPR2026_P8_DISSOLVE_AUTHORING.md](NPR2026_P8_DISSOLVE_AUTHORING.md) | A' |
| 追補: VRChatコミュニティ発の表現（P6 / P7 / F1） | [NPR2026_ADDENDUM_VRC_COMMUNITY.md](NPR2026_ADDENDUM_VRC_COMMUNITY.md) | A' |
| 追補: 背景・ワールド表現（P9〜P12） | [NPR2026_ADDENDUM_BACKGROUND.md](NPR2026_ADDENDUM_BACKGROUND.md) | B' |
| 追補: ワークフロー・自動化の未整備箇所（W1〜W10） | [NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md](NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md) | 先行 |

VRChatコミュニティ追補は、学会・カンファレンス由来ではなく2026年に実際に話題になった表現の調査結果。
Fake Shadow（前髪の落ち影）と See Through Hair 相当のステンシル運用が未対応であることが判明したため、
既存シェーダーへの新キーワード追加を伴わない Stage A' として分離している。

背景追補は背景・ワールド側の調査結果。**実装済みの絵画調機能（水彩・クワハラ・色数削減など）が
Background バリアントにだけ載っていない**という構造的な欠けが中心で、新規実装が要らないものが多い。

ワークフロー追補は表現機能とは独立で、**P1 / P9 / P10 のようにシェーダーファイル構成を触る作業より前に
入れておくほうが安全なもの**（変種間パリティ検査など）を含む。

---

## 1. 調査結果サマリ

### 1.1 すでに実装済みでカバーできているトレンド

| 業界トレンド | 本パッケージの対応 |
|---|---|
| SDF顔影シャドウ（中華NPR由来、UE5/lilToonまで普及） | `_SDF_MAP` / `_FACE_SDF_ROTATION`（`NataneToonLighting.hlsl:537`） |
| VRC Light Volumes（ボクセルライティング） | `_USE_LIGHT_VOLUME` / `_LIGHT_VOLUME_SPECULAR` |
| 手描き感・作画揺れ・コマ打ち | `_LINE_BOIL` / `_OUTLINE_HAND_DRAWN` / `_HATCHING` / `_WATERCOLOR` / `_KUWAHARA_FILTER` |
| 網点・トーン（漫画表現） | `_SCREEN_TONE` / `_HALFTONE_SHADOW` |
| 記号的ハイライト（星・ハート等） | `_SHAPED_HIGHLIGHT`（Custom SDFアトラス含む） |
| スタイライズとPBRのブレンド | `_SOFT_LIGHTING_MODE` / StandardToon / `NataneToonPBR.hlsl` |
| 撮影模倣ポストの一部 | `_CHROMATIC_ABERRATION` / `_COLOR_BLEEDING` / `_LUT_3D` |
| 髪の異方性・天使の輪 | `_HAIR_SPECULAR`（Kajiya-Kay）/ `_ANGEL_RING` |

`Documentation~/PROPOSED_EXPRESSION_FEATURES.md` の8機能案は全て実装完了済み。

### 1.2 対象外にする領域

**ニューラルシェーディング / DLSS 5 系**（NVIDIA DLSS 5 = 2026年秋、CEDEC2026「ニューラルシェーディング入門」）:
VRChatアバターは runtime script / compute が使えず、Quest系にテンソルコアも無い。
エディタ側ベイクへMLを持ち込む費用対効果も現時点では低いため、本計画では扱わない。

---

## 2. 実装項目

### P1. Shadow Shape Rig（影の形のアートディレクション）

詳細: [NPR2026_P1_SHADOW_SHAPE_RIG.md](NPR2026_P1_SHADOW_SHAPE_RIG.md)

**動機**
Shading Rig (ACM TOG) や CEDEC系の「影の形を作画としてディレクションする」潮流に対し、
本パッケージは `_SHAPED_HIGHLIGHT`（ハイライト側）しか持たず、**影側の形状制御が存在しない**。
`ShadowShape` / `ShadingRig` は現状 grep でゼロヒット。

**仕様**
- キーワード: `_SHADOW_SHAPE_RIG`
- 楕円プリミティブ（rig）を4スロット。各スロットが `ndotl - border` を局所的にバイアスし、
  影境界を「膨らませる／へこませる」
- 座標系: UV。`_FACE_ORTHO` 有効時は正射投影空間を優先（顔での安定性確保）
- ライト方向追従: スロットごとの Light Follow と `_ShadowRigFollowScale` で rig 中心を移動（Shading Rig の動的追従に相当）
- **テクスチャ不要・Uniform分岐のみ** → Core収録可、Quest可

**1スロットのパラメータ**

| パラメータ | 内容 |
|---|---|
| Enable Slot N | スロット有効化 |
| Center | rig中心（UV） |
| Size X / Y | 楕円の半径 |
| Rotation | 楕円の回転 |
| Strength | 影境界のバイアス量（負値でへこませる） |
| Falloff | 縁の減衰カーブ |
| Light Follow | ライト方向への追従量 |
| Mask Channel | 適用マスクのチャンネル |

**変更ファイル（`_SHAPED_HIGHLIGHT` を雛形にした登録面）**

1. `Shaders/NataneToon/Include/Effects/NataneToonShadowShapeRig.hlsl`（新規・実装本体）
2. `Include/Lighting/NataneToonLighting.hlsl` — `ApplyShadowShapeRig(uv, ndotl, lightDir)` 追加
3. `Include/Rendering/NataneToonFragment.hlsl` — `ApplySDFShadow`（:719）直後、トーン量子化前に挿入
4. `Include/Core/NataneToonInput.hlsl` — CBUFFERへプロパティ追加
5. `Include/Core/NataneToonBuildSettings.hlsl` — `NATANE_FEATURE_SHADOW_SHAPE_RIG` ガード
6. 本体 + `Variants/*.shader` = **計13ファイル**の `Properties` の `[Toggle(...)]` と
   各パスの `#pragma shader_feature_local`（`_SDF_MAP` は11ファイルに定義あり／同じ範囲を対象）
7. `Editor/NataneToon/Integration/NataneShaderKeywordSynchronizer.cs` — `KeywordMappings` へ
   `("_ShadowShapeRig", "_SHADOW_SHAPE_RIG")`（**単一の真実**。ビルド最適化は全てここ由来）
8. `Editor/NataneToon/GUI/NataneToonShaderGUI.cs` — セクション + トグル + スロットUI
9. `Editor/NataneToon/GUI/NataneToonInspectorSectionRegistry.cs` — 検索キーワード / docSlug
10. `Editor/NataneToon/GUI/NataneToonLocalization.cs` — EN/JP
11. `NataneToonSamplerBudgetEstimator.cs` / `PerformanceBudgetTool.cs` / `MaterialValidator.cs` — コスト反映

**自動生成連携**
Mask Painter で「影を膨らませたい箇所」を塗る → 塗り領域の重心と主軸をPCAで求め、
rigスロット（Center / Size / Rotation）へ自動フィットする「マスクからRig生成」ボタンを追加。

---

### P2. 顔SDF影マップの本格ベイク（Stage A・自動生成）

詳細: [NPR2026_P2_FACE_SDF_BAKE.md](NPR2026_P2_FACE_SDF_BAKE.md)

**動機 — 既存実装の意味的な不整合**
`Editor/NataneToon/GUI/NataneToonSdfAutoGenerator.cs` は
`BuildMask()` → `BuildSignedDistanceField()` の順で、**マスクの符号付き距離場**を生成している。
一方シェーダー側 `_FACE_SDF_ROTATION` は

```hlsl
float threshold = FdotL * 0.5 + 0.5 + _SDFOffset;   // NataneToonLighting.hlsl:564
```

と比較しており、期待している入力は **「その画素が影に入るライト角」を 0–1 で符号化した角度フィールド**。
距離場と角度フィールドは意味が異なるため、回転追従モードでは影の遷移順序が破綻しやすい。

**仕様**
1. 顔メッシュを正面正射投影で UV ベイク（既存 `MapGen_UVBake.compute` を流用）
2. 水平ライト角を 0°→180° まで N ステップ（既定64、最大180）スイープ
3. 各画素が影に入る**最小角** θt を記録 → `s = (1 - cos θt) / 2` として R チャンネルへ
4. 左右対称前提で 0.5 を境にミラー（`_FACE_SDF_ROTATION` の左右反転UVと整合させる）
5. 境界整形に既存 `MapGen_Dilation.compute` / `MapGen_GaussianBlur.compute` を流用
6. `_SDFMap` へ割当 → `_FACE_SDF_ROTATION` を自動ON → `_SDFOffset` / `_SDFSoftness` に推奨値

**変更ファイル**
- `Editor/MapGenerator/Shaders/Compute/MapGen_FaceSdfBake.compute`（新規）
- `Editor/MapGenerator/MapGeneratorEditor.cs` — 「顔SDF影マップ」セクション追加
  （既存の AO / Shadow のレイキャスト実装と同じ progress bar 作法に合わせる）
- `Editor/NataneToon/GUI/NataneToonSdfAutoGenerator.cs` — 既存機能は
  **「Mask SDF（非回転用）」** としてラベルを明確化し、回転用は新ベイクへ誘導

**注意**: `Editor/MapGenerator/` は独立asmdef（`MapGenerator.Editor`）で `NataneToon.Editor` を参照しない。
共有ヘルパー（`NataneEditorCompat` 等）は使えないため、バージョン分岐はインライン `#if UNITY_2022_2_OR_NEWER` で書く。

**シェーダー変更は閾値式1行のみ**（`NataneToonLighting.hlsl:564` の `FdotL` の符号）。
仕様書で導出しているとおり、現行式では正面ライトでほぼ全面が影になる符号反転がある。
新キーワードもバリアント同期も不要なためリスクは小さい。破壊的変更として CHANGELOG に記録する。

---

### P3. アニメ撮影模倣ポストプリセット（ScreenFX拡張）

詳細: [NPR2026_P3_SCREENFX_CINEMATIC.md](NPR2026_P3_SCREENFX_CINEMATIC.md)

**動機**
『マギアエクセドラ』CEDEC2025（Bloom / Gradation / DoF / **色収差を画面周囲のみ** / モノクロ＋放射ブラー）、
『ウマ娘』の映像用コンポジット素材出力といった、アニメ撮影工程の模倣が定石化している。

`Shaders/NataneToon/Effects/NataneScreenFXOverlay.shader` の現プロパティは
Vignette / Grain / Scanline / Posterize / ChromaticAberration / Edge / Tint / Contrast / Saturation まで。

**追加するもの**

| 追加 | 内容 |
|---|---|
| `_AberrationEdgeOnly` | 色収差を画面周辺のみに重み付け（視線誘導。現状は全画面一律） |
| `_RadialBlurStrength` / `_RadialBlurCenter` / `_RadialBlurSamples` | 放射ブラー（インパクト演出） |
| `_GradationTexture` / `_GradationColorA` / `_GradationColorB` / `_GradationBlend` | グラデーション合成（撮影のグラデ） |
| `_Monochrome` | モノクロ化（部分適用対応） |

**プリセット4種**を `Editor/NataneToon/Tools/ScreenFXSetupTool.cs` に追加:
`劇場（Cinematic）` / `必殺技（Impact）` / `回想（Flashback）` / `シリアス（Serious）`

**注意**: ScreenFX は GrabPass ベースのため Quest では非推奨。UI とドキュメントに **PC限定** を明記。
放射ブラーのサンプル数は既存 Refraction の Quest 削減方針（9→5サンプル）に倣い上限を設ける。

---

### P5. ハッチングTAM／水彩素材のプロシージャル生成（Stage A・自動生成）

詳細: [NPR2026_P5_HATCHING_TAM_GENERATOR.md](NPR2026_P5_HATCHING_TAM_GENERATOR.md)

**動機**
`_HATCHING` は `_HatchTex0`（RGBA=L1-4）/ `_HatchTex1`（RG=L5-6）の**6段Tonal Art Map**を要求するが、
どちらも既定値が `"white"` のため、素材を自前で用意しない限り機能をONにしても**何も起きない**。
`_WATERCOLOR` の `_WCGranulationTex` / `_WCPaperTex` も同様。
リポジトリ内の生成器は `DissolvePatternGenerator`（ノイズのみ）だけ。

**仕様** — `Editor/NataneToon/Tools/HatchingToneGenerator.cs`（新規）

| 生成物 | 内容 |
|---|---|
| ハッチング6段TAM | 明るい段の線を暗い段が必ず含む**累積生成**（段境界でのちらつき防止）。ミップ一貫性を明示的に扱う |
| 水彩の粒状感 / 紙目 | 粒子スケール / コントラスト / 繊維方向・異方性。平均0.5へ正規化（ON時に明度が変わらないように） |

**網点（`_SCREEN_TONE` / `_HALFTONE_SHADOW`）はプロシージャル実装で
パターンテクスチャを取らないため対象外**。取るのは適用範囲マスクのみで、それは Mask Painter の担当。

---

### P8. ディゾルブ表現とアニメーションのオーサリング（Stage A'）

詳細: [NPR2026_P8_DISSOLVE_AUTHORING.md](NPR2026_P8_DISSOLVE_AUTHORING.md)

**動機**
`_DISSOLVE` はシェーダー側が18プロパティを持ち、UV/World/Local 座標モードやエッジ発光まで揃っている。
素材生成も `DissolvePatternGenerator`（Perlin / Voronoi / Cellular / Random / Gradient）がある。
足りないのは **「時間変化を作る」部分がまるごと空白**である点。

- `AnimationClip` を生成するツールが**存在しない**（`AnimationUtility` の使用箇所は
  `NataneMigrationService` の binding 追従と `NataneBuildUsageSnapshot` の走査のみ）
- Animator レイヤー / VRC Expression Menu の生成も無い
  （`VRChatIntegrationWindow` のタブは Light Volumes / パッケージ設定 / 自動検出の3つ）
- `_FX_MODULATOR` の Target enum に **Dissolve が無い**（§F2）

結果として「ディゾルブで消えるアニメーション」の実作業がほぼ全部手作業になっている。

**仕様** — `Editor/NataneToon/Tools/DissolveStudioWindow.cs`（新規）

| タブ | 内容 |
|---|---|
| プリセット | 焼失 / 転送 / データ化 / 風化 / 霧散 / 出現。必要なノイズは自動生成 |
| プレビュー | 0→1 のスクラブ再生。閉じたら必ず元の値へ戻す |
| アニメーション | `AnimationClip` 生成。方向・長さ・イージング・コマ打ち・エッジ発光の同時カーブ・複数Renderer一括 |
| VRChat | Animator レイヤー + Expression Parameters / Menu。**Modular Avatar があれば MA で非破壊** |
| 自走モード | F2 前提。FX Modulator で Animator 不使用（Quest 向け） |

クリップ先頭で `_Dissolve` を 1、末尾で 0 に戻すことで常時コストを避ける。
マテリアルスロットが複数ある Renderer では `material[N]._DissolveAmount` バインディングへ自動で切り替える。

**あわせて修正する3件**

| # | 内容 |
|---|---|
| **F2** | `_FXModTarget0` の enum に `DissolveAmount,13` を追加。最も近い `AlphaFade` はエッジ発光を伴わず代替にならない |
| **F3** | `_UseDissolveMask` が**完全に死んでいる**。本体+Variants 計12ファイルの Properties に `[Toggle(_DISSOLVE_MASK)]` として宣言されているが、HLSL・GUI・レジストリのどこからも参照されず `#pragma` も無い。`NataneToonFragment.hlsl:2597-2600` はトグルを無視してマスクを常時サンプルしている |
| **F4** | `_AudioLinkDissolve` も宣言のみで未参照（実装は `_AudioLinkDissolveIntensity` を直接見ている） |

F3 / F4 のような「`Properties` の `[Toggle(KEYWORD)]` が立てるキーワードを、どのパスもコンパイルしていない」
状態は既存の `NataneShaderUpdateAudit` では検出できない（逆方向を見ていない）。
[ワークフロー追補](NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md) の **W3 パリティ検査に逆方向の検出を追加**する。

---

## 3. 段階分けと実行順

| Stage | 内容 | シェーダー変更 | 競合リスク |
|---|---|---|---|
| **A** | P2（顔SDFベイク）→ P5（ハッチングTAM生成） | P2の閾値式1行のみ | 低 |
| **B** | P1（Shadow Shape Rig） | 13ファイル同期 + GUI登録 | 中（GUI / Fragment.hlsl は競合しやすい） |
| **C** | P3（撮影模倣ScreenFX） | ScreenFXのみ | 低 |
| **A'** | P6 / P7（[VRChat追補](NPR2026_ADDENDUM_VRC_COMMUNITY.md)）→ P8（ディゾルブ） | P8のF2〜F4のみ（新キーワードなし） | 低 |
| **B'** | P9 / P10（[背景追補](NPR2026_ADDENDUM_BACKGROUND.md)）→ P11 / P12 | Background への機能展開とバリアント2本追加 | 中（W3 を先に入れる前提） |

Stage A から着手する。新キーワード追加やバリアント同期を伴わず、既存の不整合修正も含むため単体でマージ可能。

---

## 4. 完了条件（各項目共通）

`PROPOSED_EXPRESSION_FEATURES.md` の完了条件を踏襲する。

- 機能OFF時に既存マテリアルの見た目が変わらない（後方互換）
- Inspector から有効化・調整でき、EN/JP 両対応
- Mask で適用範囲を制御できる（P1）
- Sampler Budget にコストが反映される
- MaterialValidator で危険な組み合わせを検出できる
- 対応バリアント外では UI を出さない
- **シェーダーコンパイルの error / warning がゼロ**

### 検証手順

パッケージ参照用ミニUnityプロジェクト（`Packages/manifest.json` に
`"com.natane.toonshader": "file:<repo>"`）で `ShaderUtil.GetShaderMessages` を回すエディタスクリプトを
`-batchmode -quit -executeMethod` で実行する。

- VRC相当: `2022.3.28f1`
- Unity 6: `6000.0.55f1`

error / warning を両方ゼロに保つ。`Scripts have compiler errors` で abort した場合はシェーダー検証が
走らないので、まずログの `error CS` を確認する。

### 作業上の注意

- `Documentation~/` 配下の新規ファイルはグローバル gitignore の `*~` にマッチするため `git add -f` が必須
- 新キーワードは `NataneShaderKeywordSynchronizer.KeywordMappings` が単一の真実。ここを起点に
  FeatureOptimizer / VariantStripper / BuildSettings.hlsl ガード生成が派生する
- OUTLINE / SHADOW_CASTER パスは共有include（`NataneToonOutlinePass.hlsl` /
  `NataneToonShadowCasterPass.hlsl`）。`.shader` へインライン再実装しない

---

## 5. 出典（調査ソース）

- CEDEC2026 タイムテーブル: https://cedec.cesa.or.jp/2026/timetable/
- CEDEC2026 注目講演（シリコンスタジオ）: https://ss-agent.jp/column/special/sp44-cedec2026/
  — 『モンスターハンターストーリーズ3』アニメ調レンダリング(NPR)、『PRAGMATA』髪と表情、
  『Pokémon LEGENDS Z-A』描画技術、ニューラルシェーディング入門(Cygames)
- SIGGRAPH 2026 Papers: https://kesen.realtimerendering.com/sig2026.html
  — See-through: Single-image Layer Decomposition for Anime Characters /
  Lifting Lines and Tone: Image-space Stylization in Path-space
- Shading Rig: Dynamic Art-directable Stylised Shading for 3D Characters (ACM TOG):
  https://dl.acm.org/doi/abs/10.1145/3461696
- 『まどドラ』セルルック調3DCGの必殺技演出【CEDEC2025】:
  https://gamemakers.jp/article/2025_08_05_113664/
- SDFを活用した低負荷なアニメ調シェーディングをUE5で実装:
  https://gamemakers.jp/article/2024_03_01_61881/
- VRC Light Volumes 解説: https://metacul-frontier.com/?p=24259
- lilToon の SDFマップ: https://metacul-frontier.com/?p=20546
