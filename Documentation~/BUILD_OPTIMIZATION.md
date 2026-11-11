# ビルド最適化 / Build Optimization

Natane Toon Shader は、ビルド時（特に VRChat アバターのアップロード時）に
シェーダーとマテリアルを自動的に最適化する仕組みを備えています。本ドキュメントは
その全体像と、各コンポーネントが「いつ・何を」行うかをまとめたものです。

> このドキュメントは日本語を主とし、要点に英語を併記しています。

---

## 1. 全体像（何が動くか）

ビルド時に連携して動作する 4 つの仕組みがあります。

| 実行タイミング | コンポーネント | 役割 |
| --- | --- | --- |
| ビルド前 (`IPreprocessBuildWithReport`, order -90) | `NataneBuildFeatureOptimizer` | 使用機能だけを `#define` した `NataneToonBuildSettings.hlsl` を生成し、未使用キーワードをソースレベルで `#undef` |
| シーン処理時 (`IProcessSceneWithReport`, order -50) | `NataneBuildSceneKeywordSync` | 全マテリアルのキーワードをプロパティ値と同期してディスク保存（VRChat の AssetBundle ビルド対応） |
| シェーダーコンパイル時 (`IPreprocessShaders`, order 100) | `NataneShaderVariantStripper` | どのマテリアルからも使われていないキーワードを含むコンパイル済みバリアントを破棄 |
| ビルド後 (`IPostprocessBuildWithReport`, order 1000) | `NataneBuildOptimizationReport` | 最適化サマリをコンソールとテキストファイルに出力 |
| VRChat アバタービルド前 (`IVRCSDKPreprocessAvatarCallback`, order -50) | `NataneVRChatBuildHook` | キーワード同期 + Lite 変換候補のログ（SDK 導入時のみコンパイル） |

### 従来 (before) との違い

- **before:** `NataneBuildFeatureOptimizer` がソースレベルで未使用機能を `#undef` し、
  `NataneBuildSceneKeywordSync` がマテリアルキーワードを同期していました。
  これによりビルドサイズは削減されますが、Unity のシェーダーバリアント
  コンパイルパイプライン側では、全 `shader_feature_local` 組み合わせが
  コンパイル対象として残り得ました。
- **now:** 上記に加えて `NataneShaderVariantStripper` が
  「プロジェクト内のどのマテリアルからも使われていないキーワードを含むバリアント」を
  コンパイル段階で破棄します。さらに `NataneBuildOptimizationReport` が結果を可視化し、
  `NataneVRChatBuildHook` が VRChat アップロード時に Lite 変換候補を提案します。

---

## 2. シェーダーバリアントストリップ (NataneShaderVariantStripper)

`IPreprocessShaders` を実装し、Natane シェーダーのコンパイル時に呼ばれます。

- **収集:** プロジェクト内の全 Natane マテリアルで有効な**既知キーワード**の和集合を作成します。
  キーワード状態と Toggle プロパティ値 (`>= 0.5`) のどちらかが有効なら「使用中」とみなします。
- **判定:** 各バリアントの `shader_feature_local` キーワードのうち、**既知の Natane キーワード**が
  1 つでも「使用中集合」に含まれない場合、そのバリアントを破棄します。
- **安全性:** 判定対象は既知の Natane キーワードのみです。`multi_compile` 由来の
  ビルトインキーワード（`DIRECTIONAL` / `SHADOWS_SCREEN` / `INSTANCING_ON` など）には
  一切触れません。
- **既知キーワード:** `NataneShaderKeywordSynchronizer.KeywordMappings` と
  追加キーワード（`_EYE_PARALLAX`）の和集合。`NataneBuildFeatureOptimizer` と同一の定義に揃えています。

### オプトアウト (opt-out)

一時的に無効化したい場合は EditorPrefs にフラグを設定します。

```csharp
// 無効化
EditorPrefs.SetBool("NataneToon_DisableVariantStripping", true);
// 再有効化
EditorPrefs.SetBool("NataneToon_DisableVariantStripping", false);
```

キー: `NataneToon_DisableVariantStripping`（`NataneShaderVariantStripper.OptOutPrefKey`）。

破棄前後の数はコンソールにログ出力されます（`before → after`）。

---

## 3. ビルド最適化レポート (NataneBuildOptimizationReport)

`IPostprocessBuildWithReport` を実装し、ビルド完了後に以下をコンソールと
`Logs/NataneBuildOptimizationReport.txt` に出力します。

1. **破棄されたバリアント数** — `NataneShaderVariantStripper` の集計値。
2. **無効化された機能** — 既知キーワードのうち、どのマテリアルでも未使用のもの一覧。
3. **Lite 変換候補** — GrabPass 機能を使っていないフル版マテリアル一覧。
4. **テクスチャ改善提案** — 2048 を超える `maxSize`、未圧縮テクスチャなど（**提案のみ・自動変更なし**）。

> VRChat のアバタービルドでは `IPostprocessBuildWithReport` は呼ばれません。
> そのため VRChat 向けの Lite 提案は `NataneVRChatBuildHook` 側でも出力されます。

---

## 4. Lite（GrabPass 無し）変換の判定

GrabPass は毎フレームの全画面コピーを伴うため高コストです。以下の機能を
**1 つも使っていない**フル版マテリアルは、対応する Lite 版シェーダーへ変換できます。

GrabPass を必要とする機能キーワード:

- `_REFRACTION`
- `_SOFT_FILTER`
- `_KUWAHARA_FILTER`
- `_COLOR_BLEEDING`
- `_CHROMATIC_ABERRATION`

フル版 → Lite 版の対応:

| フル版 | Lite 版 |
| --- | --- |
| `Natane/Toon Shader` | `Natane/Toon Shader (Lite)` |
| `Natane/Toon Shader (Cutout)` | `Natane/Toon Shader (Cutout Lite)` |
| `Natane/Toon Shader (Transparent)` | `Natane/Toon Shader (Transparent Lite)` |
| `Natane/Toon Shader (Fur)` | `Natane/Toon Shader (Fur Lite)` |

これらはあくまで**提案**です。自動変換は行いません。

---

## 5. VRChat 連携 (NataneVRChatBuildHook)

VRChat SDK (`VRC_SDK_VRCSDK3`) が導入されている場合のみコンパイルされます。

- 実装アセンブリ `NataneToon.Editor.VRChat`（`Editor/NataneToon/Integration/VRChat/`）は
  `defineConstraints: ["VRC_SDK_VRCSDK3"]` を持ちます。SDK 非導入時は**アセンブリごと
  コンパイル対象外**となり、SDK への参照も評価されません。よって本体 `NataneToon.Editor` は
  SDK 無しでも問題なくコンパイルできます（ハード参照なし）。
- `IVRCSDKPreprocessAvatarCallback.OnPreprocessAvatar` で、アップロード前に
  (1) 全 Natane マテリアルのキーワード同期、(2) そのアバターに含まれる Lite 変換候補の
  ログ出力を行います。フックが失敗してもアップロードは止めません。

---

## 6. 機能とキーワードの整合性（drift 対策）

`NataneToonBuildSettings.hlsl` の `#undef` ガードブロックは
`NataneBuildFeatureOptimizer` が `NataneShaderKeywordSynchronizer.KeywordMappings`
（＋ `_EYE_PARALLAX`）から**自動生成**します。C# 側にキーワードを追加すれば、
次回ビルド／エディタ起動時にガードも自動で追随します。

- 新しい shader_feature を追加したら、必ず `KeywordMappings` にも
  `(プロパティ名, キーワード)` を追加してください。
- これにより `NataneBuildFeatureOptimizer`・`NataneShaderVariantStripper`・
  `NataneBuildOptimizationReport` の 3 者が同じ「既知キーワード集合」を共有します。
