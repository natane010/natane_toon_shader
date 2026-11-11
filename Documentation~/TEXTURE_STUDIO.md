# テクスチャスタジオ / Texture Studio

Natane Toon のテクスチャ/マスク系ツールを1つのタブ付きウィンドウへ統合したものです。
マスクを **シーンビューで直接ペイント** できる新機能「マスクペインター」を中心に、テクスチャ最適化・各種生成ツールへのクイックアクセスをまとめています。

メニュー: `Tools > Natane > テクスチャ Texture > テクスチャスタジオ Texture Studio`

---

## 1. マスクペイント (Mask Paint)

Natane マテリアルのマスクテクスチャ (例: `_OutlineMask`, `_EmissionMask`, `_FaceOrthoMaskTex`, `_FXModMaskTex`, `_LenticularMask` など) を、UnityのシーンビューでUV空間へ直接塗れます。

### 使い方

1. **レンダラーを選択**: `SkinnedMeshRenderer` または `MeshRenderer` を持つオブジェクトを選び、`レンダラー` フィールドに割り当てます (選択中オブジェクトから自動取得も試みます)。マテリアルスロットが複数ある場合はドロップダウンで選べます。
2. **マスクを選択**: シェーダーのテクスチャプロパティを走査し、名前に `Mask` を含むものを自動で列挙します。ドロップダウンにはGUI表示名とプロパティ名 (例: `Outline Mask (_OutlineMask)`) が出ます。
3. **チャンネルを選択**: `R / G / B / A` から塗るチャンネルを選びます。**指定チャンネルのみ書き込み、他チャンネルは保持** します。マルチチャンネルマスク (後述) で重要です。
4. **マスクの用意**:
   - スロットが空なら `新規マスク作成` (512 / 1024 / 2048、黒または白で初期化) → `SaveFilePanelInProject` でPNG保存し、マテリアルへ自動割り当て。
   - 既にテクスチャがあれば `既存マスクを編集用に読み込み` で編集用コピー (RGBA32) を作成します。
5. **ペイント有効化**: `シーンビューでペイントを有効化` をON。裏で対象メッシュから一時 `MeshCollider` を生成します (Skinned は `BakeMesh` でベイク)。
6. **塗る**: シーンビューでメッシュ表面をドラッグすると、ヒット点のUVへソフト円ブラシを塗ります。編集中は編集用テクスチャをマテリアルへ割り当てているので **リアルタイムでプレビュー** されます。

### ブラシ操作

| 操作 | 内容 |
| --- | --- |
| 左ドラッグ | ペイント |
| Ctrl (⌘) + ドラッグ | 消去 |
| `[` / `]` | ブラシサイズの縮小 / 拡大 |
| Ctrl (⌘) + Z | 直前のストロークを取り消し (最大10回) |

`サイズ` / `硬さ` / `不透明度` スライダーでブラシを調整します。ヒット点には円 (ハンドルディスク) がプレビュー表示されます (消去時は赤)。

### 保存

`保存 (PNG)` を押すと:

1. 現在のチャンネルを数ピクセル外側へ **にじませ (dilate)**、UVアイランド境界のシームを軽減します (`保存時のにじみ (px)` スライダー)。
2. PNGを書き出し (`File.WriteAllBytes`)、再インポート時に **sRGBをオフ** (マスクはリニアデータ) かつ読み書き可能に設定します。
3. インポート済みアセットをマテリアルへ再割り当てします (`Undo` 対応)。

ペイントを未保存のまま終了/無効化すると、マテリアル参照は元のテクスチャへ戻し、非アセットの編集用テクスチャを残しません。

### VRChatでの用途例

- **顔直交投影マスク (`_FaceOrthoMaskTex`) を顔だけに塗る**: 顔メッシュのUVに対し、顔領域を白 (Rチャンネル) で塗ることで、直交投影ベースの表情/影効果を顔部分にのみ適用できます。首や体へのにじみを避けたい場合は保存時のにじみを小さめに。
- **FXモジュレーターのスロットマスク (`_FXModMaskTex`)**: このマスクは **R=スロット0、G=スロット1** のマルチチャンネル構成です。チャンネルを `R` にしてエフェクトAを出す領域を、`G` にしてエフェクトBを出す領域を、別々に塗り分けられます。片方を塗ってももう片方のチャンネルは保持されます。

---

## 2. テクスチャ最適化 (Texture Optimize)

既存の `TextureOptimizer` の機能をそのまま埋め込んでいます。実装は共有ドローワ `TextureOptimizerDrawer` に抽出済みで、旧ウィンドウ (`Tools > Natane > 最適化 Optimization > テクスチャ最適化`) はこのドローワへ委譲するだけの薄いラッパになりました。**旧メニューはそのまま動作します。**

- 最大テクスチャサイズ / 圧縮 / ミップマップ設定
- 選択テクスチャの追加・プロジェクト全体スキャン
- 分析して最適化 (メモリ削減量の表示)

---

## 3. 生成ツール (Generators)

関連する生成ツールへのクイック起動カードです (埋め込みではなくメニューを開くショートカット):

- **ディゾルブパターン生成 / Dissolve Pattern Generator**
- **GPUパーティクルメッシュ生成 / GPU Particle Mesh Generator**
- **スムース法線ベイク / Smooth Normal Baker**
- **マップジェネレーター / Map Generator**

---

## 旧メニュー互換性 / Backward Compatibility

- **マスクペインター単体ウィンドウ**: `Tools > Natane > テクスチャ Texture > マスクペインター Mask Painter` から個別にも開けます。スタジオのタブと同じ埋め込みコア (`NataneMaskPainterCore`) を共有します。
- **テクスチャ最適化**: 旧メニュー `Tools > Natane > 最適化 Optimization > テクスチャ最適化 Texture Optimizer` は削除せず、共有ドローワへ委譲する形で維持しています。
- スタジオ/ペインターとも `NataneToolMenuPaths` と `NataneToolHealthValidator` に登録され、ダッシュボードからも起動できます。

## 実装メモ / Implementation Notes

- `NataneMaskPaintUtil` — GUI非依存のピクセル演算 (`SplatBuffer` / `SplatTexture` / `DilateChannel` / `BrushWeight`)。EditModeテストと `-executeMethod` スモークで直接検証しています。
- `NataneMaskPainterCore` — 埋め込み可能な本体 (`DrawControls` / `OnGUI(Rect)` / `OnSceneGUI`)。ウィンドウとスタジオタブが1つのコアを再利用します。
- `NataneTextureStudioWindow` — `NataneTabbedToolWindow<TextureStudioTab>` を継承したタブUI。
