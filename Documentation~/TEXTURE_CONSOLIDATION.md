# テクスチャ統合 / Texture Consolidation

Natane Toon Shader は、ビルド／アップロード時に「同じようなテクスチャ」を検出し、
完全一致の重複を自動統合します。本ドキュメントは、何が自動で行われ、何が提案に
とどまるか、VRAM 削減見積りの注意点、無効化（オプトアウト）方法をまとめます。

> このドキュメントは日本語を主とし、要点に英語を併記しています。

---

## 1. 何を検出するか

`NataneTextureConsolidator.Analyze(...)` は Natane シェーダーを使うマテリアルが
参照するテクスチャを走査し、3 種類の候補を検出します。

| 種別 | 判定 | 既定の扱い |
| --- | --- | --- |
| a. 完全一致 (ExactDuplicate) | 画素内容が byte 単位で同一（別アセット） | **自動統合** |
| b. ほぼ一致 (NearIdentical) | 32x32 に縮小した平均絶対差 < しきい値（既定 2/255）かつ同一アスペクト比 | **提案のみ**（明示確認が必要） |
| c. 過大マスク (OversizedMask) | `*Mask` / `*MaskTex` プロパティに 512px 超のテクスチャ | **提案のみ** |

### 完全一致の判定方法

1. まず幅×高さでバケット化（寸法が違えば byte 一致し得ない）。
2. `TextureImporter.imageContentsHash` が取得できればそれを内容ハッシュに使用。
3. 取得できない場合は、CPU 読み取り可能コピー（読み取り可能ならそのまま、
   非読み取り／圧縮なら RenderTexture blit でコピー）の生データの SHA1 を使用。

---

## 2. 自動 vs 提案

- **自動（完全一致のみ）**
  - VRChat アップロード時に、そのアバターが参照する完全一致の重複テクスチャを
    1 つの正規アセットへ統合します。
  - **非破壊**: マテリアルのインスタンスコピーを作り、コピー上でのみ参照を
    付け替えます。`renderer.sharedMaterials` をコピーへ差し替えるため、
    プロジェクト／シーンの共有マテリアルおよびテクスチャアセットは一切変更されません。
- **手動（エディタ操作）**
  - `ApplyExactDuplicates(result, undo:true)` はプロジェクトのマテリアル参照を
    正規アセットへ付け替えます（Undo 対応・`SaveAssets` でディスク保存）。
    これはツールウィンドウからの明示操作を想定しています。
- **提案のみ**
  - ほぼ一致・過大マスクは自動統合されません。ほぼ一致の統合は
    `Analyze(..., includeNearIdentical:true)` で検出した上で、
    ユーザーがグループ単位で明示確認する必要があります（ビルド時は絶対に行いません）。

---

## 3. いつ動くか

| タイミング | コンポーネント | 動作 |
| --- | --- | --- |
| VRChat アバタービルド前 (`IVRCSDKPreprocessAvatarCallback`, order -40) | `NataneVRChatTextureConsolidation` | キーワード同期 (order -50) の後に、クローン上で完全一致の重複のみ統合 |
| ビルド後 (`IPostprocessBuildWithReport`, order 1000) | `NataneBuildOptimizationReport` | 完全一致の重複と過大マスクを検出しレポート出力（統合はしない） |

近似一致（NearIdentical）はビルド時には検出も統合も行いません。

---

## 4. VRAM 削減見積りの注意点

- 見積りは `Profiler.GetRuntimeMemorySizeLong(texture)` を優先し、取得できない
  場合は `幅 × 高さ × 4 (RGBA32 相当)` で概算します。
- 概算は圧縮（BC7/DXT/ASTC 等）やミップマップを考慮しない **上限寄り** の値です。
  実際の VRAM 削減量とは差が出ることがあります。
- 削減量は「余剰（非正規）テクスチャ分の合計」です。正規アセット 1 つは残ります。
- ビルド時統合は当該アバターがアップロードするアセットからのみ重複を除きます。
  ディスク上のアセット数は変わりません。

---

## 5. 無効化（オプトアウト）

- ビルド時（VRChat アップロード時）のテクスチャ統合は EditorPrefs で制御します。
  - キー: `NataneToon_BuildTextureConsolidation`（**既定 true = 有効**）
  - `false` にするとアップロード時の自動統合を無効化します。
- 例（エディタスクリプト等から）:
  ```csharp
  UnityEditor.EditorPrefs.SetBool("NataneToon_BuildTextureConsolidation", false);
  ```

---

## 6. 公開 API（ツールウィンドウ向け）

`NataneTextureConsolidator`（namespace `NataneToon.Editor`, UI 無し）:

- `ConsolidationResult Analyze(IEnumerable<Material> materials, bool includeNearIdentical = false, int nearIdenticalThreshold = 2)`
  - Natane マテリアルのみを対象に解析し、シリアライズ可能な結果を返す。
- `int ApplyExactDuplicates(ConsolidationResult result, bool undo = true)`
  - 完全一致グループについてマテリアル参照を正規アセットへ付け替える（同一セッション）。
- `int ApplyExactDuplicates(ConsolidationResult result, IEnumerable<Material> materials, bool undo)`
  - 別セッションで復元した結果に対して、対象マテリアルを明示して適用する。
- `Dictionary<string, Texture> BuildExactDuplicateRemap(ConsolidationResult result)`
  - 「非正規パス → 正規 Texture」の対応表。
- `int RetargetMaterialTextures(Material material, IReadOnlyDictionary<string, Texture> remap, bool undo)`
  - 1 マテリアルのテクスチャスロットを対応表で付け替える。
- `long EstimateTextureBytes(Texture tex)` / `string FormatBytes(long bytes)`
  - VRAM 見積りと整形。

結果モデル `ConsolidationResult` は `exactDuplicateGroups` /
`nearIdenticalGroups` / `oversizedMaskGroups`（各 `ConsolidationGroup`）を持ち、
`ExactDuplicateSavingsBytes` などの集計プロパティを提供します。
