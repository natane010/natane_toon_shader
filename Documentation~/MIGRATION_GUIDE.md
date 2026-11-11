# 移行ガイド

lilToon から Natane Toon Shader への移行方法を説明します。

## いちばん簡単な方法（ウィンドウを開かずに変換）

ツールウィンドウを開かなくても、今いる場所から 1 操作で変換できます。どれも同じ変換処理を使います。

| やりたいこと | 操作 | 元のマテリアル |
|---|---|---|
| アバター全体を変換 | Hierarchy でアバターを右クリック → `Natane > lilToon から変換 (アバター全体)` | 変更しません（複製を変換してシーン上のアバターに割り当て。Ctrl+Z で戻せます） |
| マテリアル / フォルダを変換 | Project で右クリック → `Natane > lilToon から変換` | 変更しません（隣の `NataneToon` フォルダに変換後を作成） |
| 1 つをその場で変換 | マテリアルのインスペクター右上の ⋮ → `Natane Toon へ変換 (lilToon)` | 書き換えます（`_lilToon_backup` を作成。Ctrl+Z で戻せます） |
| シェーダー欄で切り替え | マテリアルの Shader 欄で `Natane/Toon Shader` を選ぶ | 書き換えます（Ctrl+Z で戻せます） |

- Shader 欄で `Natane/Toon Shader` を選んだ場合は、lilToon の描画モード（不透明 / カットアウト / 半透明）に合うバリアントを自動で選びます。
  特定のバリアント（例: `Natane/Toon Shader Cutout`）を選んだ場合はそれを使います。
- Shader 欄での自動引き継ぎは、移行ツールの「シェーダー欄の切り替えでも引き継ぐ」でオフにできます。
- 変換後に見た目を調整したいときは、インスペクターの「クイックセットアップ」か `Tools > Natane > 効果を試す Try Effects` が近道です。

## 自動移行ツール

Natane Toon Shader には、lilToonから自動的に移行するためのツールが含まれています。

### ツールの種類

#### 1. lilToon Migration Tool
lilToon専用の移行ツールです。プロパティを適切にマッピングして変換します。

**場所**: `Tools > Natane > 移行 Migration > lilToon Migration Tool`

**特徴**:
- lilToonのプロパティを自動検出
- Natane Toon Shaderへの最適なマッピング
- バックアップ作成オプション
- 一括変換対応

#### 2. Batch Material Converter
汎用的なマテリアル変換ツールです。任意のシェーダー間で変換できます。

**場所**: `Tools > Natane > 移行 Migration` 以下の Batch Material Converter

**特徴**:
- 任意のシェーダー間で変換
- シーン/プレハブ内の使用状況を表示
- プロジェクト全体をスキャン
- 選択的に変換可能

## lilToon Migration Tool の使い方

### ステップ1: ツールを開く

1. Unityメニューの `Tools > Natane > 移行 Migration > lilToon Migration Tool` を選択
2. ツールウィンドウが開きます

### ステップ2: オプションを設定

**Create Backup（推奨）**
- 「元マテリアルを直接置換」をオンにしたときだけ使われます（`_lilToon_backup.mat` を作成）
- 通常の変換は元のマテリアルを変更せず、隣の `NataneToon` フォルダに変換後のマテリアルを作るため、バックアップは不要です

**Replace Original（危険）**
- チェック: 元のマテリアルを直接変更（破壊的）
- チェックしない: 新しいマテリアルを作成（`_NataneToon.mat`）

⚠️ **警告**: Replace Original をチェックすると、元のマテリアルが変更されます。必ずプロジェクトのバックアップを取ってください。

### ステップ3: マテリアルをスキャン

1. `Scan for lilToon Materials` ボタンをクリック
2. プロジェクト内のすべてのlilToonマテリアルが検出されます
3. リストに表示されます

### ステップ4: 変換

**個別に変換**:
- リスト内の各マテリアルの `Convert` ボタンをクリック

**一括変換**:
- `Convert All Materials` ボタンをクリック
- 一覧のすべてのマテリアルを順に変換します（進捗バーが表示されます）

### ステップ5: 確認

変換後、以下を確認してください：
- テクスチャが正しく設定されているか
- 色が適切か
- エフェクト（リムライト、アウトラインなど）が有効になっているか

## プロパティマッピング

lilToonとNatane Toon Shaderのプロパティ対応表です。

### メインテクスチャ

| lilToon | Natane Toon Shader | 備考 |
|---------|-------------------|------|
| `_MainTex` or `_lilMainTex` | `_MainTex` | メインテクスチャ |
| `_Color` or `_lilColor` | `_Color` | メインカラー |

### シェーディング

| lilToon | Natane Toon Shader | 変換ロジック |
|---------|-------------------|------------|
| `_lilShadowColor` | `_ShadowColor` | そのまま |
| `_lilShadowBorder` | `_ShadowOffset` | 0.0-1.0 → -0.5-0.5 に変換 |
| `_lilShadowBlur` | `_ShadowSharpness` | 逆数変換（blur が高い = sharpness が低い） |
| `_lilShadowSteps` | `_ShadowSteps` | そのまま（デフォルト: 2） |

### リムライト

| lilToon | Natane Toon Shader | 備考 |
|---------|-------------------|------|
| `_RimColor` | `_RimColor` | そのまま |
| `_RimFresnelPower` | `_RimPower` | クランプ: 0.1-10 |
| - | `_RimIntensity` | デフォルト: 1.0 |

### アウトライン

| lilToon | Natane Toon Shader | 変換ロジック |
|---------|-------------------|------------|
| `_OutlineWidth` | `_OutlineWidth` | × 0.1 で変換（lilToon と Natane Toon の太さの単位差を補正） |
| `_OutlineColor` | `_OutlineColor` | そのまま |

### 発光

| lilToon | Natane Toon Shader | 備考 |
|---------|-------------------|------|
| `_EmissionMap` | `_EmissionMap` | そのまま |
| `_EmissionColor` | `_EmissionColor` | そのまま |

### 法線マップ

| lilToon | Natane Toon Shader | 備考 |
|---------|-------------------|------|
| `_BumpMap` | `_BumpMap` | そのまま |
| `_BumpScale` | `_BumpScale` | そのまま |

### MatCap

| lilToon | Natane Toon Shader | 備考 |
|---------|-------------------|------|
| `_MatCapTex` | `_MatCapTex` | そのまま |
| - | `_MatCapIntensity` | デフォルト: 1.0 |
| - | `_MatCapBlendMode` | デフォルト: Add |

## 手動調整が必要な場合

自動変換後、以下のパラメータを手動で調整することで、より良い結果が得られます：

### 1. Shadow Sharpness（影のシャープさ）
```
推奨値: 0.05 - 0.15
- 低い値: シャープなセルシェーディング
- 高い値: ソフトなグラデーション
```

### 2. Shadow Steps（影の段階）
```
推奨値: 2-3
- 2: 基本的なセルシェーディング
- 3以上: より複雑な陰影
```

### 3. Rim Intensity（リムライトの強さ）
```
推奨値: 0.5 - 2.0
lilToonより弱く感じる場合は値を上げる
```

### 4. Outline Width（アウトライン幅）
```
lilToonの1/100の値に変換されます
太さが足りない場合は調整してください
```

## トラブルシューティング

### 変換後、影が表示されない

**原因**: Shadow Steps が正しく設定されていない

**解決策**:
1. マテリアルを選択
2. Shading セクションを開く
3. Shadow Steps を 2 に設定
4. Shadow Sharpness を 0.1 に設定

### 色が違って見える

**原因**: カラースペースやライティングの違い

**解決策**:
1. Shadow Color を調整
2. Main Color の明度を調整
3. 必要に応じて Rim Light を追加

### アウトラインが太すぎる/細すぎる

**原因**: スケール変換の違い

**解決策**:
1. Outline Width を手動で調整
2. 推奨範囲: 0.005 - 0.02

### テクスチャが設定されていない

**原因**: テクスチャ参照が失われた

**解決策**:
1. 元のマテリアルまたはバックアップを確認
2. 手動でテクスチャを再設定

### リムライトが表示されない

**原因**: Enable Rim Light がオフ

**解決策**:
1. Rim Light セクションを開く
2. `Enable Rim Light` をチェック
3. Rim Color を明るい色に設定

## Batch Material Converter の使い方

より柔軟な変換が必要な場合は、Batch Material Converter を使用してください。

### 使用方法

1. `Tools > Natane > 移行 Migration` から Batch Material Converter を開く
2. Source Shader Contains: `lilToon` と入力
3. Target Shader: `Natane/Toon Shader` を入力
4. オプションを設定:
   - Search in Scenes: シーン内の使用状況を検索
   - Search in Prefabs: プレハブ内の使用状況を検索
5. `Scan Project` をクリック
6. 変換したいマテリアルを選択
7. `Convert Selected Materials` をクリック

### 利点

- プロジェクト全体を一度にスキャン
- マテリアルの使用状況を確認できる
- 選択的に変換可能
- 任意のシェーダー間で使用可能

## ベストプラクティス

### 1. 必ずバックアップを取る

変換前に：
- プロジェクト全体のバックアップ
- または Git でコミット
- Create Backup オプションを有効にする

### 2. 段階的に変換

- まず少数のマテリアルでテスト
- 結果を確認
- 問題がなければ一括変換

### 3. シーンで確認

変換後：
- 実際のシーンでマテリアルを確認
- ライティング条件下でテスト
- 必要に応じて微調整

### 4. プレハブを更新

プレハブを使用している場合：
- プレハブのマテリアルも変換
- プレハブを開いて確認
- 変更を保存

## 変換後のチェックリスト

- [ ] すべてのテクスチャが正しく設定されている
- [ ] メインカラーが適切
- [ ] 影の色と段階が適切
- [ ] リムライトが期待通り（有効な場合）
- [ ] アウトラインが適切（有効な場合）
- [ ] 発光が機能している（有効な場合）
- [ ] 法線マップが適用されている（使用している場合）
- [ ] 複数のライトで正しく表示される
- [ ] 影の受け取り/投影が正常

## よくある質問

### Q: 変換は可逆的ですか？

A: いいえ。Replace Original オプションを使用した場合、変換は不可逆です。必ずバックアップを取ってください。

### Q: 完全に同じ見た目になりますか？

A: 完全に同じにはなりません。シェーダーの実装が異なるため、微調整が必要な場合があります。

### Q: パフォーマンスは向上しますか？

A: 一般的には同等かやや軽量です。不要な機能を無効にすることで、さらに最適化できます。

### Q: lilToonの高度な機能は対応していますか？

A: 基本的な機能は対応していますが、lilToonの全機能が移行されるわけではありません。対応表を確認してください。

### Q: カスタムシェーダーバリアントは？

A: 標準的なlilToonからの移行をサポートしています。大きくカスタマイズされたシェーダーは手動調整が必要です。

## サポート

問題が発生した場合：
1. このガイドのトラブルシューティングを確認
2. GitHub Issues で報告
3. サンプルマテリアルを添付すると解決が早くなります

## 追加リソース

- [README.md](README.md) - 機能の詳細説明
- [TECHNICAL.md](TECHNICAL.md) - 技術仕様
- [QUICK_START.md](QUICK_START.md) - クイックスタート

---

Happy migrating! 🚀
