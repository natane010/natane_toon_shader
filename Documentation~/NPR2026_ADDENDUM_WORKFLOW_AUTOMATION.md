# NPR2026 追補: ワークフロー・自動化の未整備箇所

表現機能（[NPR2026_PLAN.md](NPR2026_PLAN.md) / [NPR2026_ADDENDUM_VRC_COMMUNITY.md](NPR2026_ADDENDUM_VRC_COMMUNITY.md)）とは別に、
**開発ワークフローとして未整備・未自動化のもの**を調査した結果。

前提として、この規模のパッケージにしては足回りは既に整っている:
`Tests/Editor/` に EditModeテスト14本、`NataneShaderUpdateAudit`（Shader↔レジストリ整合監査）、
`NataneToolHealthValidator`、`NataneShaderFeatureRegistry`、ビルド最適化（FeatureOptimizer /
VariantStripper / HLSLガード）、`.github/workflows/build-listing.yml` による VPM listing 自動生成。

未整備なのは主に **「作ったチェックが自動で走っていない」** 部分と、
**「単一の真実から派生させられるのに手作業になっている」** 部分。

---

## W1. PR時のCIが存在しない（最優先）

### 現状

`.github/workflows/` は `build-listing.yml` の1本だけで、トリガは

```yaml
on:
  push:
    tags: ['v*']
  workflow_dispatch:
```

つまり **リリース専用**（zip作成 → GitHub Release → VPM listing の index.json 更新 → Pages デプロイ）。
`pull_request` / `push`（ブランチ）で動くワークフローは**ゼロ**。

結果として:
- `Tests/Editor/` の14本は**ローカルで手動実行するしかない**
- シェーダーコンパイル検証（`ShaderUtil.GetShaderMessages`）も手動
- レビュー時に「壊れていないこと」を機械的に保証できない

### 提案

`.github/workflows/pr-validate.yml` を追加し、3ジョブに分ける。

| ジョブ | 内容 | Unity必要 |
|---|---|---|
| `static` | §W3 / W6 / W8 / W9 の静的チェック（Python or Node の素のスクリプト） | **不要** |
| `tests` | `game-ci/unity-test-runner` で EditMode テスト（2022.3.28f1） | 必要 |
| `shaders` | §W2 のミニプロジェクトで全シェーダーをコンパイルし error/warning ゼロを確認 | 必要 |

**`static` ジョブを最初に入れる。** Unityライセンス不要で即日入れられ、
このリポジトリで最も事故が起きている「変種間ドリフト」を直接押さえられる。

`tests` / `shaders` は `UNITY_LICENSE` secret（Personalライセンスの `.ulf`）の登録が前提。
ライセンス整備が終わるまでは `continue-on-error: true` で並走させ、
安定してから required check に昇格させる。

---

## W2. シェーダー検証環境がリポジトリに入っていない

### 現状

シェーダーのコンパイル検証手順は「パッケージ参照用のミニUnityプロジェクトを作り、
`Packages/manifest.json` に `"com.natane.toonshader": "file:<repo>"` を書き、
`ShaderUtil.GetShaderMessages` を回すエディタスクリプトを `-batchmode -quit -executeMethod` で実行する」
というものだが、**この一式がリポジトリに存在しない**。毎回手で組み立てている状態。

### 提案

`Tests/CI/` を新設して以下をコミットする。

```
Tests/CI/
├── setup-validation-project.sh    # ミニプロジェクトを生成（manifest.json を file: 参照で書く）
├── ValidateShaders.cs             # 全 .shader を列挙し GetShaderMessages を集計、
│                                  # error/warning が1件でもあれば exit 1
└── README.md                      # ローカル実行手順（2022.3.28f1 / 6000.0.55f1）
```

- 引数で Unity のパスとバージョンを受け取り、**ローカルとCIで同一コマンド**にする
- 出力は `AUDIT_`/`SHADER_` 接頭辞付きの1行サマリにして、CIログから拾いやすくする
  （既存 `NataneShaderUpdateAudit` が `AUDIT_UNKNOWN_COUNT=` を出しているのと同じ作法）
- `error CS` でC#コンパイルが落ちると `executeMethod` 自体が走らないため、
  スクリプト側でログを grep して**その場合を明示的に失敗として区別**する

これは今回のPRで各仕様書に書いた「検証手順」を、実際に実行可能な形にするもの。

---

## W3. 変種間・パス単位の `#pragma` パリティ検査が無い

### 現状

`CLAUDE.md` に明記されているとおり、キーワード追加時は本体+Variants の各シェーダーが
それぞれ `Properties` と各パスの `#pragma shader_feature_local` を持つため、
**ここのドリフトが実際にコンパイルバグを起こしている**。

既存の `NataneShaderUpdateAudit` はかなり手厚く、以下を検出する:

| 検出項目 | 内容 |
|---|---|
| `unknownKeywords` | シェーダーにある `#pragma` がレジストリ未登録 |
| `missingTargetShaders` | レジストリが対象と宣言したシェーダーが存在しない |
| `missingProperties` | キーワードに対応する `Properties` が無い |
| `unparseableItems` | パース不能な `#pragma` 行 |

一方で**カバーされていない**のは:

1. **パス単位の網羅** — パーサは `PassNames` を抽出しているが、キーワードは
   シェーダー単位で集約している。`FORWARD_BASE` にはあるが `FORWARD_ADD` に無い、
   という状態は検出できない（`_SDF_MAP` は両パスに必要）
2. **変種横断の意図的な差分と事故の区別** — 「Lite には入れない」は正しい設計だが、
   「入れるつもりで忘れた」と区別する宣言がレジストリ側に無い
3. **逆方向 — `Properties` の `[Toggle(KEYWORD)]` が立てるキーワードを、どのパスもコンパイルしていない**
   状態。監査は「`#pragma` にあるがレジストリ未登録」の方向しか見ていない。
   [P8](NPR2026_P8_DISSOLVE_AUTHORING.md) の調査で `_DISSOLVE_MASK`（`_UseDissolveMask`）と
   `_AUDIOLINK_DISSOLVE`（`_AudioLinkDissolve`）の2件が**実際にこの状態で見つかっている**。
   死んだキーワードがマテリアルに書き込まれ、インスペクタのトグルが何も変えない状態になる

### 提案

Pure C# のテストとして `Tests/Editor/NataneShaderVariantParityTests.cs` を追加。

- 全 `.shader`（本体 + Variants = 13ファイル）× パス × キーワードの**行列**を構築
- 期待値を `NataneShaderFeatureRegistry` の scope 宣言から導出
- `Properties` の `[Toggle(KEYWORD)]` を抽出し、**そのキーワードを compile するパスが1つも無い**ものを検出
- 差分があれば「どのファイルのどのパスに何が無いか」を列挙して失敗させる
- **意図的な除外はレジストリに明示的に書く**（`ExcludedShaders` のような宣言）ことを強制し、
  暗黙の欠落を許さない

パースはテキスト処理だけなので Unity 不要にでき、§W1 の `static` ジョブでも実行できる。
そのため実装は「テスト」と「CIスクリプト」で同じパーサを共有できる形にしておく。

---

## W4. 既存の監査がゲートになっていない

`NataneShaderUpdateAudit.Run()` の呼び出し口は、メニュー
（`Tools/Natane/ビルド最適化 Build Optimization/Shaderアップデート監査`）と、
`NataneMigrationService` / `NataneBuildUsageSnapshot` / `NataneUnifiedVariantStripper` /
`NataneWorkspaceHubWindow` からの内部利用のみ。

`Tests/Editor/NataneShaderUpdateAuditTests.cs` には
`Audit_HasNoUnknownKeywordsAfterRegistration()` という**まさにゲートにすべきテストが既にある**。
足りないのは実行の自動化だけなので、§W1 の `tests` ジョブに含めれば解決する。

つまり W4 は独立した作業ではなく、**W1 を入れた時点で自動的に解消される**。
本項は「既にある資産が使われていない」ことの記録として残す。

---

## W5. 新機能追加時の登録面が手作業（11箇所）

### 現状

[NPR2026_P1_SHADOW_SHAPE_RIG.md](NPR2026_P1_SHADOW_SHAPE_RIG.md) §3 で列挙したとおり、
キーワード1個を追加するのに触る場所は:

1. `Effects/*.hlsl`（実装）
2. `Lighting/*.hlsl` or `Rendering/*.hlsl`（呼び出し）
3. `Core/NataneToonInput.hlsl`（CBUFFER）
4. `Core/NataneToonBuildSettings.hlsl`（ガード）
5. 本体 + Variants 13ファイルの `Properties`
6. 同 13ファイルの各パスの `#pragma`
7. `NataneShaderKeywordSynchronizer.KeywordMappings`
8. `NataneToonShaderGUI.cs`（セクション + トグル）
9. `NataneToonInspectorSectionRegistry.cs`（検索キーワード / docSlug）
10. `NataneToonLocalization.cs`（EN/JP）
11. `NataneToonSamplerBudgetEstimator.cs` / `PerformanceBudgetTool.cs` / `MaterialValidator.cs`

11箇所のうち **5, 6, 7, 9, 10 は完全に機械的**で、キーワード名・プロパティ定義・対象パスが
決まれば一意に生成できる。

### 提案

`Editor/NataneToon/Tools/FeatureScaffoldTool.cs`（新規）:

- 入力: キーワード名 / トグルプロパティ名 / プロパティ定義リスト（型・既定値・Range）/
  対象シェーダー / 対象パス / セクション名（EN/JP）
- 出力: 5・6・7・9・10 への追記を**プレビュー付きで**適用。3・4 は雛形コメントを挿入
- 既存箇所を壊さないため、**追記位置は行全体でアンカーを取る**
  （シェーダー名やプロパティ名は他行の部分文字列になりうる）
- 適用後に §W3 のパリティ検査を走らせ、生成漏れがないことを自己検証する

段階的に入れるなら **「不足箇所を検出して一括追記する」だけでも価値が大きい**。
新規追加でなく、既存キーワードの取りこぼし修復にも使える。

---

## W6. ドキュメントリンクの双方向カバレッジ検査が無い（実データで欠落を確認）

### 現状

インスペクタの各セクションは `docSlug`（`"category/slug"` 形式）を持ち、
`NataneToonInspectorComponents.DrawDocLink()` が

```csharp
string baseUrl = IsJapanese
    ? "https://natanetoon.com/params/"
    : "https://natanetoon.com/en/params/";
Application.OpenURL(baseUrl + docSlug + ".html");
```

でドキュメントを開く。

**docSlug → ページ方向は健全**だった（`Website/params/` と `Website/en/params/` の
両方について 63/63 実在、リンク切れゼロ）。

しかし **逆方向に29件の欠落**がある。ページは存在するのに、
どのインスペクタセクションからも `docSlug` で参照されていない:

```
advanced/eye-parallax      advanced/normal-warp        advanced/screen-edge
advanced/smooth-normal     basic/color-enhancement     basic/final-blend
basic/main-tex-animation   basic/surface-finish        effects/alpha-mask
effects/color-bleeding     effects/glitch-stretch      effects/glitch
effects/hue-shift          effects/kuwahara-filter     effects/lut-3d
effects/matcap23           effects/offset-rim-light    effects/rim-direction
effects/rimlight2          effects/sss-lut             effects/watercolor
lighting/backlight         lighting/dither-stabilize   lighting/procedural-ao
lighting/sdf-shadow        lighting/shading-grade-map  lighting/shadow-color
lighting/soft-lighting     lighting/specular-intensity
```

書いたドキュメントに製品から到達できていない状態。
`lighting/sdf-shadow` が含まれているのは [P2](NPR2026_P2_FACE_SDF_BAKE.md) と直接関係する。

### 提案

1. `Tests/Editor/NataneDocSlugCoverageTests.cs`（新規）で**双方向**を検査
   - docSlug → `Website/params/<slug>.html` と `Website/en/params/<slug>.html` の実在
   - ページ → いずれかのセクションから参照されていること
   - 意図的に参照しないページは許可リストに明示的に書かせる
2. 上記29件について、対応するセクションへ `docSlug` を付与する（別PR）
3. Unity不要のテキスト処理なので §W1 の `static` ジョブへ入れる

---

## W7. ローカライズ欠落の自動検出

`NataneToonLocalization` には日本語フォールバック辞書（`JapaneseFallbackExact`）や
文字化け検出の正規表現（`SuspiciousLocalizedTextRegex`）まで用意されており、
**実行時の防御は手厚い**。

一方で、新規UIを追加したときに **EN/JP のどちらかを書き忘れた** ことを
コミット前に検出する仕組みは無い。`L("日本語", "English")` 形式なので、

- 第2引数に日本語文字が含まれている（= ENを書き忘れて日本語をコピペした）
- 第1引数と第2引数が完全一致している（= 未翻訳）
- 片方が空文字

は静的に検出できる。`Tests/Editor/NataneLocalizationLintTests.cs` として追加し、
§W1 の `static` ジョブへ入れる。既存の文字化け検出正規表現をそのまま流用できる。

---

## W8. コミット前チェック（.meta / `Documentation~`）

### 現状

- `.meta` の欠落は**現時点でゼロ**（`Runtime` / `Editor` / `Shaders` / `Tests` の
  ファイル・ディレクトリ全件を確認済み）。ただし検査する仕組みは無く、予防されていない
- グローバル gitignore の `*~` が `Documentation~/` にマッチするため、
  ここへの新規ファイルは `git add -f` が必須。`git add -A` では**静かにスキップされる**
  （今回のPRで追加した6本の仕様書もすべて `-f` が必要だった）
- `.git/hooks/` にはサンプル以外が無く、フックは未導入

### 提案

`Tests/CI/precommit-check.sh`（新規、Unity不要）:

1. `.meta` 欠落・孤児 `.meta`（対応ファイルが無い）の検出
2. `Documentation~/` 配下に未追跡ファイルが残っていないかの警告
3. `git config core.hooksPath` で任意導入できる形にし、**強制はしない**
   （リポジトリ共有フックは環境差で事故りやすいため、CI側の `static` ジョブを本線にする）

CIの `static` ジョブでも同じスクリプトを実行し、フック未導入でも漏れないようにする。

---

## W9. バージョン表記の単一化

### 現状

バージョンは5箇所に散っている。**現在は全て `1.6.5` で一致しており不整合は無い**が、
手作業で揃えている。

| 箇所 | 形式 |
|---|---|
| `package.json` | `"version": "1.6.5"` |
| `README.md:3` | shields.io バッジ `version-1.6.5-blue` |
| `README.md:27` | 本文「パッケージバージョン: `1.6.5`」 |
| `CHANGELOG.md` | `## [1.6.5] - 2026-07-18` |
| `Website/index.html` | `<span data-release-version>v1.6.5</span>`（2箇所） |

### 提案

`Tests/CI/bump-version.sh <new-version>`（新規）で `package.json` を単一の真実とし、
残りを機械的に書き換える。あわせて `static` ジョブで**5箇所の一致を検査**する
（不一致ならCI失敗）。CHANGELOG は見出しの存在だけを検査し、本文は手書きのまま残す。

---

## W10. プリセットのサムネイル生成

`CLAUDE.md` のプリセット規約に「Include thumbnail preview images」とあり、
`MaterialPresetBrowser` はサムネイルを表示する前提になっているが、
**サムネイルを生成するツールは無い**（手で用意する運用）。

`MaterialPreviewWindow` / `MaterialComparisonTool` が既にプレビュー描画を持っているので、
そのレンダリング経路を使い回して

- プリセット一覧を走査し、統一されたライティング・カメラ・プリミティブでレンダリング
- 一定サイズのPNGとして `Runtime/Presets/<category>/Thumbnails/` へ保存
- 差分があるものだけ再生成（ハッシュ比較）

とするバッチを `Editor/NataneToon/Presets/PresetThumbnailBaker.cs` として追加する。
プリセットを増やすたびの手作業が消え、見た目の一貫性も担保できる。

---

## 優先順位

| 順 | 項目 | Unity必要 | 効果 |
|---|---|---|---|
| 1 | **W1 `static` ジョブ** + W3 パリティ検査 | 不要 | 既知の事故（変種間ドリフト）を機械的に止める |
| 2 | **W2 検証環境のコミット** | ローカル実行のみ | 各仕様書の「検証手順」が実行可能になる |
| 3 | W1 `tests` / `shaders` ジョブ（`UNITY_LICENSE` 整備後） | 必要 | 既存テスト14本と W4 が自動で効く |
| 4 | W6 docSlug 双方向検査 + 29件の付与 | 不要 | 書いたドキュメントに製品から到達できるようになる |
| 5 | W5 スキャフォールド | 不要 | P1 以降の機能追加コストが下がる |
| 6 | W7 / W8 / W9 | 不要 | 予防。`static` ジョブへ相乗り |
| 7 | W10 サムネイル | 必要 | プリセット運用の手作業削減 |

1・2 は本PRの表現機能（P1〜P7）と**独立して先に入れられる**。
むしろ P1 のような13ファイル同期を伴う作業の前に W3 を入れておくほうが安全。

---

## 検証済みの事実（このドキュメントの根拠）

再調査を省けるよう、実際に確認したコマンドと結果を残す。

| 確認事項 | 結果 |
|---|---|
| `.github/workflows/` の中身 | `build-listing.yml` のみ。`on: push.tags` + `workflow_dispatch` |
| `Tests/Editor/` のテスト数 | 14ファイル（asmdef 1件を除く） |
| `.meta` 欠落（Runtime/Editor/Shaders/Tests 全件） | ファイル 0件 / ディレクトリ 0件 |
| docSlug の総数 | 63件 |
| docSlug → ページ（JP / EN 両方） | 63/63 実在。欠落 0 |
| ページ → docSlug 参照 | **29件が未参照** |
| バージョン表記 | 5箇所すべて `1.6.5` で一致 |
| git hooks | サンプル以外未導入 |
| Shader Update Audit の検出項目 | `unknownKeywords` / `missingTargetShaders` / `missingProperties` / `unparseableItems` |
| 監査のパス単位検査 | 未対応（`PassNames` は抽出するがキーワードはシェーダー単位で集約） |
| `[Toggle(KEYWORD)]` が未コンパイルの実例 | `_DISSOLVE_MASK` / `_AUDIOLINK_DISSOLVE` の2件（[P8](NPR2026_P8_DISSOLVE_AUTHORING.md) 参照） |
