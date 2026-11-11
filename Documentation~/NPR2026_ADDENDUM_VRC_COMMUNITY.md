# NPR2026 追補: VRChatコミュニティで話題の表現

学会・カンファレンス由来（[NPR2026_PLAN.md](NPR2026_PLAN.md)）とは別に、
2026年にVRChatコミュニティ（X / BOOTH / 解説記事）で実際に話題になった表現を調査した結果の追補。

結論として **追加2件（P6 / P7）と修正1件（F1）** が見つかった。
いずれも学術トレンドではなく「ユーザーが今まさに欲しがっている実務機能」であり、
アバター改変での採用障壁に直結する。

---

## P6. Fake Shadow（前髪の落ち影）

### 動機

前髪が顔に落とす影を、ライティングに依存せず板ポリ／複製メッシュで擬似的に描く手法。
VRChatでは「2Dライクなルック」を作る定番として定着しており、
lilToon には専用の FakeShadow シェーダーが用意されている。

**本パッケージには相当機能が存在しない。** リポジトリ内の `FakeShadow` 参照は
`Editor/NataneToon/Migration/LilToonMigrationTool.cs:1860-1879` の1箇所だけで、

```csharp
bool isFakeShadowShader = sourceShader.Contains("fakeshadow");
...
report.infos.Add("FakeShadow shader detected. Minimal migration applied (color only).");
```

つまり **lilToon から移行してきたユーザーは前髪の落ち影を失う**。

### 仕様

新バリアント `Natane/Toon Shader FakeShadow` を追加する。
影を落とすためだけの極小シェーダーで、既存の146機能は載せない。

| プロパティ | 内容 |
|---|---|
| `_ShadowColor` | 影色。既定 `(0.55, 0.5, 0.6, 1)` |
| `_ShadowAlpha` | 不透明度。Range(0,1) 既定 0.5 |
| `_LightColorFollow` | ライト色への追従量。Range(0,1) 既定 0.3（0で完全にライト非依存） |
| `_ShadowTex` | 影の形テクスチャ（アルファ使用）。既定 `"white"` |
| `_FadeByViewAngle` | 正面から見たとき薄くする量。Range(0,1) 既定 0 |
| Stencil 一式 | `_StencilRef` / `_StencilComp` / `_StencilOp` など。既存本体（:1094-1100）と同じ命名で揃える |

レンダリング設定:

```
Tags { "Queue" = "AlphaTest+50" "RenderType" = "Transparent" "VRCFallback" = "ToonTransparent" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
ZTest LEqual
Cull Back
```

- 顔マテリアルの**直後**に描くため `AlphaTest+50`
- ステンシルで「顔が書いた領域のみ」に制限し、顔からはみ出さないようにする
- `ZWrite Off` で他の半透明の並びを壊さない
- ライト非依存を既定にするのは、暗いワールドで落ち影だけ浮くのを避けるため
  （`_LightColorFollow` で必要なら追従させる）

### 実装ファイル

| ファイル | 変更 |
|---|---|
| `Shaders/NataneToon/Variants/NataneToonShader_FakeShadow.shader` | 新規。単一 Pass。`NataneToonCore.hlsl` は**include しない**（依存を持たせない） |
| `Editor/NataneToon/Tools/FakeShadowSetupTool.cs` | 新規。§下記のセットアップ |
| `Editor/NataneToon/Integration/NataneShaderCatalog.cs` | シェーダー登録。**文字列アンカーは行全体で取る**（シェーダー名は他行の部分文字列になりうる） |
| `Editor/NataneToon/GUI/NataneToonShaderGUI.cs` | FakeShadow 用の簡易インスペクタ（統合UIのドロップダウンへ追加） |
| `Editor/NataneToon/Migration/LilToonMigrationTool.cs` | §F1 |
| `Documentation~/FAKE_SHADOW.md` | 使い方（既存の `GHOST.md` / `XRAY.md` と同じ粒度） |

### セットアップツール

`FakeShadowSetupTool` は2方式を選べるようにする。

1. **板ポリ方式** — 顔の前に1枚のQuadを生成し、前髪のシルエットを模した `_ShadowTex` を割当。
   軽量だが形の自由度が低い。頭ボーンへ追従させる。
2. **メッシュ複製方式** — 前髪メッシュを複製し、法線方向へわずかに押し出したものを影として使う。
   形は正確だがポリゴン数が増える。`SkinnedMeshRenderer` のボーン参照を引き継ぐ。

どちらも:
- 非破壊（元のRendererを変更せず、子GameObjectとして追加）
- `Undo.RegisterCreatedObjectUndo` を通す
- 生成したマテリアルは `Assets/` 配下のプロジェクト側へ保存（パッケージ内に書かない）

---

## P7. ステンシル・プリセット（眉毛・目の透過 / See Through Hair 互換）

### 動機

前髪より前に眉・目・まつげを描き、2Dイラストのような「髪から目が見える」ルックを作る手法。
2026年3月末に公開された **See Through Hair**（goorm / BOOTH）が
Modular Avatar 対応・アップロード時自動更新で話題になったが、
**対応シェーダーは Poiyomi Toon と lilToon のみ**で、本パッケージは対象外。

本パッケージ側の状況:
- `_StencilRef` / `_StencilComp` / `_StencilOp` / `_StencilFail` / `_StencilZFail` /
  `_StencilReadMask` / `_StencilWriteMask` は**生のプロパティとして既に存在**（`NataneToonShader.shader:1094-1100`）
- しかし Ref番号・Comp・Op の組み合わせをユーザーが自力で組む必要があり、
  プリセットもドキュメントも無い

つまり**機構はあるがワークフローが無い**状態。これが採用障壁になっている。

### 仕様

`Editor/NataneToon/Tools/StencilPresetTool.cs`（新規）で、Renderer / マテリアルに
3つのロールを割り当てる。

| ロール | 対象 | Stencil設定 | Queue |
|---|---|---|---|
| **Writer** | 眉・目・まつげ | `Ref = N` / `Comp = Always(8)` / `Pass = Replace(2)` | 髪より前（`Geometry-10`） |
| **Cutter（完全透過）** | 前髪 | `Ref = N` / `Comp = NotEqual(6)` / `Pass = Keep(0)` | 既定のまま |
| **Cutter（半透明）** | 前髪 | 上と同じ + Transparentバリアントで `_Alpha` を下げた2枚目マテリアルを重ねる | `Transparent` |

「完全透過」と「半透明」を分けられるようにするのは、
眉は完全に透かし、前髪は薄く残す、といった使い分けが定番だから。

### 実装上の注意

- **Ref番号の衝突**: VRChat では他アバターのステンシルと衝突しうる。
  ツール側で 1〜255 のうち使用中の値を検出し、未使用値を提案する。
  さらに「衝突すると他アバターの見た目を壊す可能性がある」旨を警告として出す
- **Writer の Queue を前へ出すと不透明ソートが変わる**ため、
  適用前後のドローオーダーを差分表示して確認させる
- `MaterialValidator` へ「Writer が居るのに Cutter が居ない（またはその逆）」の検出を追加
- P6 の Fake Shadow と併用する場合、Fake Shadow は Writer が書いた領域を**避ける**必要がある
  （眉の上に落ち影が乗ると破綻する）。プリセットで両者の Ref を揃えて自動設定する
- 非破壊。マテリアルを直接書き換える場合は必ず `Undo.RecordObject`

### 期待効果

See Through Hair 相当のセットアップが本パッケージ単体で完結し、
「lilToon / Poiyomi でしかできない」状態を解消できる。

---

## F1. lilToon FakeShadow 移行の修正

`Editor/NataneToon/Migration/LilToonMigrationTool.cs:1860-1879`

現状は FakeShadow シェーダーを検出しても色のみの最小移行で、
`report.infos` に "Minimal migration applied (color only)" を残すだけ。

P6 の実装後、以下へ差し替える。

- 変換先を `Natane/Toon Shader FakeShadow` にする
- lilToon 側の対応プロパティをマッピング（`_Color` → `_ShadowColor`、
  アルファ → `_ShadowAlpha`、影テクスチャ → `_ShadowTex`）
- Stencil 設定は値をそのまま引き継ぐ（同じ命名で揃えてあるため素通し可能）
- `report.infos` のメッセージを、実際に移行したプロパティを列挙する内容へ更新する

P6 未実装の間は現状維持とし、`report.infos` のメッセージに
「FakeShadow バリアントは未実装。P6 実装後に自動移行対応予定」と補足を入れておく。

---

## 調査したが対象外（すでに実装済み）

再調査を防ぐための記録。VRChatコミュニティで名前が挙がる機能のうち、既にカバー済みのもの。

| コミュニティでの呼称 | 本パッケージの対応 |
|---|---|
| 白飛び防止 | `_FinalHighlightBlend`（"Highlight Compression Prevent White Blowout"） |
| 明るさ下限・上限（暗いワールド対策） | `_LightColorMin` / `_LightColorMax` / `_LightMinInfluence` / `_LightMaxInfluence` |
| 色トレス線（線に周囲の色を乗せる） | `_OUTLINE_TEXTURE_COLOR` + `_OutlineTexColorHueShift` / `_OutlineTexColorSaturation` |
| 毛先・薄布の透過 | `_DITHERING_ALPHA` / `_HASHED_ALPHA` / `_BLUE_NOISE_DITHER` |
| 影に環境色を混ぜる | `_ShadowEnvStrength` |
| モノクロライト（色被り防止） | `_MonochromeLighting` |
| 顔に影を落とさない／影の受け方を部位で変える | `_ShadowReceiveMask` / `_ShadowReceive` |
| MMDワールド等でのライト方向暴れ対策 | `_LIGHT_SNAP`（`_LightSnapAngle` / `_LightSnapSmooth`） |
| 「顔が半分影でも頬だけ明るく」する法線操作 | `_SMOOTH_NORMAL` / `_NORMAL_WARP` |
| 前髪の影を落とす（ライティングベース） | `_PCSS` / `_CAST_SHADOW_COLOR`（ただし擬似落ち影は P6 が別途必要） |
| 鏡だけ／鏡以外での表示切替 | `_MIRROR_CONTROL` / `_MIRROR_TEXTURE` |
| テクスチャ統合による軽量化 | `Documentation~/TEXTURE_CONSOLIDATION.md` |

---

## 優先度と段階

| 項目 | 内容 | シェーダー変更 | Stage |
|---|---|---|---|
| **P6** | Fake Shadow バリアント + セットアップツール | 新規バリアント1本（既存13ファイルへの影響なし） | A' |
| **P7** | ステンシル・プリセット | なし（既存プロパティの設定のみ） | A' |
| **F1** | lilToon FakeShadow 移行の修正 | なし | P6の後 |

P6 / P7 は既存シェーダーへ新キーワードを追加しないため、
[NPR2026_PLAN.md](NPR2026_PLAN.md) の Stage B（P1）と**並行して進めても競合しない**。
ユーザー需要の即時性を考えると、Stage A（P2 / P5）の次はここを優先してよい。

---

## 出典

- See Through Hair（goorm / BOOTH）: https://booth.pm/ja/items/6610175
- 前髪を自然に透かしてFakeShadowも導入できる「See Through Hair」: https://kohavrog.com/see-through-hair/
- See Through Hairを使って髪から目が見えるようにしよう: https://note.com/luminories/n/n1d1f57992e12
- VRChatアバターで2Dライクなルックを目指す②【眉毛の透過と前髪の落ち影】: https://note.com/famous_oxalis656/n/n6e4f2b906980
- lilToonの「Fake Shadow」で前髪の影を付けよう: https://vrnavi.jp/fake-shadow/
- 【2026年版】lilToon完全ガイド: https://avatarnotekurofox.com/?p=69
- 『崩壊3rd』開発者が語るアニメ風レンダリングの極意: https://learning.unity3d.jp/570/
