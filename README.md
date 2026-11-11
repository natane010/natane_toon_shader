# Natane Toon Shader

[![Version](https://img.shields.io/badge/version-1.7.0-blue)](https://github.com/natane010/natane_toon_shader/releases)
[![Unity](https://img.shields.io/badge/Unity-2022.3+-black)](https://unity.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Unity Built-in Render Pipeline向けの、セルルック/NPR表現用シェーダーパッケージです。
VRChat運用を意識した機能（AudioLink、VRC Light Volumes、LTCGI、最適化ツール）を同梱しています。

## 特徴

- `Natane/Toon Shader` を中心に、`Cutout` / `Transparent` バリアントを提供
- `Lite` バリアント（GrabPassなし軽量版: Opaque / Cutout / Transparent）
- `Fur` バリアント（シェルベースファーレンダリング）
- `Background` バリアント（背景/ワールド用、ライトマップ・PBR・Metaパス対応）
- 瞳専用 `Natane/Eye`（左右対称UV・マスク運用・表情オーバーレイ対応）
- 特殊表現 `Natane/Toon Shader Wirelight`（Cyber Wireモード、AudioLink連動）
- 画面効果 `Natane/Screen FX Overlay`（GrabPassベースのカスタム画面演出）
- `StandardToon` シェーダータイプ（lilToon互換モード）
- **統合インスペクターUI**: 全シェーダーを1つのインスペクターからドロップダウンで切り替え可能
- **Inspector日英切り替え**: EN/JPボタンでインスペクターUI言語をワンクリック切替
- 主要Editorツールを `Tools/Natane/...` に集約（日本語/英語UI）

## 動作環境

- Unity: `2022.3+`（Built-in Render Pipeline想定）
- パッケージバージョン: `1.7.0`（`package.json`準拠）

## 収録シェーダー

### Natane/Toon Shader

メインのトゥーンシェーダー。146以上のシェーダー機能を搭載。

**テクスチャ・ベース:**
- 5レイヤー合成（2nd-5th Texture + Mask）
- Gradient Base Color（グラデーションベースカラー）
- Hue Shift / Alpha Mask / SDF Map / Shading Grade Map / AO / Shadow Color Texture

**ライティング:**
- Toon/Gradient影、Multi Shadow、Ramp
- Specular / Rim(2層) / SSS / MatCap(最大3層) / Glitter
- Hair Specular（Kajiya-Kay）/ Angel Ring（天使の輪）
- Offset Rim Light / Sheen / Rim Direction Control
- SSS LUT / PCSS Soft Shadow / Soft Lighting Mode
- Cast Shadow Color / Light Direction Snap / Shadow Edge Noise
- Vertex Color Shadow / Procedural AO / Normal Warp
- Specular Anti-Aliasing / Pixel Vertex Lights
- Normal Map / Cubemap Reflection / Environmental Rim

**エフェクト:**
- Emission / Dissolve / Refraction / Parallax / Iridescence
- Screen Tone（網点オーバーレイ）/ Halftone Shadow
- Procedural MatCap / Advanced Glints
- Fake Reflection / Water Drip（雫エフェクト）/ Smear（残像）
- Perspective Flatten / Depth Color Fade
- Eye Parallax / Tessellation
- Height Fade / Intersection Fade
- Hologram / Glitch / Glitch Stretch / Glitch Mask / Glitch Noise Texture
- Distance Fade / Vertex Animation / Decal / Backlight
- AudioLink（Emission/Rim/Hue/Dissolve/Outline/Chronotensity）
- VAT(Houdini) / Main Tex Animation / Backface Texture / Video Texture / LTCGI / Dithering Alpha

**イラスト調スタイル（10機能）:**
- Color Quantize（色量子化）/ 3D LUT
- Hatching（ハッチング）/ Watercolor（水彩）
- Soft Filter / Kuwahara Filter
- Screen Edge Detection / Color Bleeding（色滲み）
- Chromatic Aberration（色収差）/ Hand-drawn Outline（手描き線）

### バリアント

- `Natane/Toon Shader (Cutout)` — Opaque版の軽量バリアント（一部高度機能を省略）
- `Natane/Toon Shader (Transparent)` — Cutout同等 + Refraction対応
- `Natane/Toon Shader (Lite)` — GrabPassなし軽量版
- `Natane/Toon Shader (Cutout Lite)` — Cutout + Lite
- `Natane/Toon Shader (Transparent Lite)` — Transparent + Lite
- `Natane/Toon Shader (Fur)` — シェルベースファーレンダリング
- `Natane/Toon Shader (Fur Lite)` — Fur + Lite
- `Natane/Toon Shader (Background)` — 背景ワールド用（ライトマップ・PBR・Metaパス）

### Natane/Toon Shader (Background) 専用機能

- Detail Map（セカンダリUV対応）
- Triplanar Mapping
- Height Fog（高さベースフォグ）
- Surface Cover（雪/砂カバー）
- Mirror Control
- Quest Lite（Quest最適化モード）
- PBR対応（Metallic/Smoothness）

### Natane/Toon Shader Wirelight

- ジオメトリベースのワイヤー表現
- Cyber Wireモード（Scanline / Chroma / Glitch）
- AudioLink連動（パルス・色・グリッチ）
- Cyber Data Stream（データストリーム表現）

### Natane/Eye

- Eye State: `Normal / Star / Heart / Dead / Nervous`
- Expression Preset: `Normal / Surprised / Crying`
- Expression Overlay: `Spiral / Tearful / Shock Rings`
- 左右分離: Dual Center / Symmetry / Right Eye UV Mirror
- Eye Region Mask（顔と目が同一マテリアルでも運用可能）
- Texture Polish / Iris Caustics / Iris Ring Pulse / AudioLink
- Bubble Effect / Vignette / Transparency Dither / Stencil制御
- Inner Mesh Priority（BlendShape時の内側描画優先）

### Natane/Screen FX Overlay

- Posterize / Edge Darken / Chromatic Aberration / Vignette / Scanline / Grain

## マテリアルプリセットシステム

### Material Preset Browser

`Tools > Natane > プリセット Presets > Material Preset Browser` からアクセスできるビジュアルプリセットブラウザです。

- サムネイル付きプリセットグリッド表示
- カテゴリフィルタリング・検索機能
- ワンクリックでマテリアルにプリセット適用
- プリセット新規作成（現在のマテリアルから）

### デフォルトプリセット

16カテゴリに分類されたプリセットを収録:

- **Character**: Skin / Hair / Clothing / Eyes
- **Props**: Metal / Plastic / Wood / Fabric
- **Environment**: Nature / Architecture
- **Effects**: Transparent / Emission / Special
- **Style**: Toon / NPR
- **Custom**: ユーザー定義

### VTuberプリセット（5種）

`Tools > Natane > ユーティリティ Utility > VTuberプリセット生成` で生成:

1. **キャラクター肌** — ソフトセルシェーディング + SSS
2. **キャラクター髪** — アニメ調ハイライト + MatCap
3. **キャラクター服** — クリーンなセルシェーディング + アウトライン
4. **キャラクター目** — エミッション + 強スペキュラー
5. **ライブパフォーマンス** — 軽量設定（VRChat/配信向け）

### マテリアルパラメータ共有

- **ファイルエクスポート/インポート**: `.ntmaterial` 形式で保存・読み込み
- **クリップボード**: コピー&ペーストでマテリアルパラメータを転送
- エクスポートデータにはユーザー名・日時・メモを含むメタデータ付き

## マスクテクスチャツール

マスクテクスチャの作成・編集を支援する統合ツール群です。

- **ブラシペイント**: Paint / Erase / Smooth / EraseAlpha モード、サイズ・硬さ・不透明度調整
- **3Dプレビュー**: グレースケール / ヒートマップ / チャンネル別表示、回転・ズーム操作
- **チャンネルパッカー**: 最大4枚のグレースケールテクスチャ → 1枚のRGBAテクスチャに合成、逆分解も可能
- **エクスポーター**: 複数圧縮形式対応、品質/パフォーマンスプリセット
- **フィルター**: テクスチャ加工フィルター
- **ジェネレーター**: マスクテクスチャ自動生成
- **レイヤーシステム**: マルチレイヤー編集
- **テンプレート**: よく使うマスクパターンのテンプレート

## 主要ツール（`Tools/Natane`）

- `Dashboard`
- `ヘルプ Help`
- `マテリアル Material`
  - マテリアル検証 / マテリアルエディタ / マテリアルプレビュー / マテリアル比較 / メイクアップレイヤー管理 / ヒエラルキー一括編集
- `プリセット Presets`
  - Material Preset Browser / カラーパレット管理 / デフォルトプリセット生成 / 全プリセット再生成
- `エフェクト Effects`
  - シャドウ調整ウィザード / MatCapレイヤーコンポーザー / ディゾルブパターン生成 / リムライト方向ビジュアライザー / スクリーンエフェクト設定
- `最適化 Optimization`
  - パフォーマンスバジェット / テクスチャ最適化 / アウトライン最適化 / 屈折品質バランサー / アセット参照チェック
- `移行 Migration`
  - lilToon移行 / 一括マテリアル変換 / プレハブバリアント変換
- `メッシュ Mesh`
  - スムース法線ベイク（アウトライン用法線をVertex Colorにベイク）
- `シェーダー Shader`
  - シェーダーバリアント収集 / シェーダープリウォーミング / バリアントストリッピング設定
- `ユーティリティ Utility`
  - UVテクスチャ生成 / パーティクルエフェクトエディタ / VTuberプリセット生成
- `診断 Diagnostics`
  - ツール健全性診断（全ツールのアクセス可否・依存関係を検証）
- `VRChat`
  - VRCライトボリュームヘルパー / VRC Light Volumes 再検出 / LTCGI 再検出

### コンテキストメニュー

- **Assets右クリック** → `Natane/マテリアル検証 Validate Material`（Nataneマテリアル選択時）
- **Assets右クリック** → `Natane/プリセット適用 Apply Preset`（Nataneマテリアル選択時）
- **GameObject右クリック** → `Natane/マテリアルを検証 Validate Materials`（Renderer付きオブジェクト選択時）

## 統合インスペクターUI

全シェーダー（Toon / Eye / Wirelight / Screen FX / StandardToon）のインスペクターが統合されています。

- マテリアルを選択すると、インスペクター最上部に **「シェーダータイプ」ドロップダウン** が表示されます
- ドロップダウンから `Toon` / `Eye` / `Wirelight` / `Screen FX` / `StandardToon (lilToon互換)` を選択するだけでシェーダーが切り替わります
- **EN/JP切り替えボタン**: インスペクター上部のボタンでUI言語を日本語⇔英語にワンクリック切替
- 切り替え時は確認ダイアログが表示され、Undo（Ctrl+Z）にも対応しています
- 各シェーダーに最適化された専用UIが自動的に表示されます
  - **Toon**: 5タブ構成（基本/ライティング/エフェクト/環境/詳細）+ レンダリングモード切替（Opaque/Cutout/Transparent/Lite/Fur/Background）
  - **StandardToon**: lilToon互換モード（`_ShadingMode` による自動検出）
  - **Eye**: 20セクション構成（瞳状態/デュアルセンター/表情オーバーレイ/虹彩エフェクト等）
  - **Wirelight**: 12セクション + 9種プリセットボタン（サイバーパンク/ホログラム/AudioLink等）
  - **Screen FX**: 4セクション構成（ブレンド/トゥーン化/画面歪み/シネマティック）

#### インスペクターUX機能

- **表示密度の切り替え**: ツールバーから シンプル / 標準 / すべて を選択（既定はシンプル）。機能は削らず、シンプルでは使用中のセクションだけを並べ、未使用の機能は「機能を追加」一覧から開く
- **クイックセットアップウィザード**: ガイド付きモード（3ステップ: 用途選択→スタイル選択→質感調整）と全表示モードを切替可能
- **ゲームキャラクター風プリセット**: 2段影＋リムライト＋スペキュラー＋テクスチャ連動アウトラインを一括設定
- **初回オンボーディング**: はじめてインスペクターを開いた際にガイドパネルを表示
- **セクションアイコン**: カテゴリ別アイコン（🎨☀💡✨🌍⚙）でセクションを視覚的に識別
- **ジャンプメニュー**: ▼ボタンから現在タブのセクション一覧へワンクリックジャンプ
- **キーボードショートカット**: Ctrl+1〜 でタブ切替
- **レスポンシブUI**: Feature Overview グリッド（2/3/4列自動切替）、Current State Row の動的幅調整
- **WCAG準拠パフォーマンス表示**: 色+形状の二重エンコーディング（✓A / →B / △C / ✕D）
- **スマート検索**: 全タブ横断検索、前方一致優先のランキング表示
- **プリセットプレビュー**: ボタンホバーで適用パラメータ一覧をツールチップ表示
- **ダークテーマ対応**: ON/OFFバッジ・セクションボックスのコントラスト最適化

## サードパーティライティング統合

### VRC Light Volumes（ボクセル型ライティング）

[VRC Light Volumes](https://github.com/REDSIM/VRCLightVolumes) パッケージがプロジェクトにインストールされていると、**自動的に検出**され、本物の `LightVolumes.cginc` を使用します。

- **自動検出**: `red.sim.lightvolumes` パッケージのインストール/アンインストールを自動検知
- **対応ワールドで自動動作**: マテリアルで「Light Volume有効」をONにするだけで、対応ワールドのボクセルライティングを自動的に受けます
- **フォールバック**: パッケージ未インストール時はUnityライトプローブに自動フォールバック
- **3ブレンドモード**: Add（デフォルト）/ Multiply / Replace
- **スペキュラー対応**: Light Volumeからのカラースペキュラー生成
- **手動再検出**: `Tools > Natane > VRChat > VRC Light Volumes 再検出`

### LTCGI（リアルタイムエリアライト）

[LTCGI](https://github.com/PiMaker/ltcgi) パッケージがプロジェクトにインストールされていると、**自動的に検出**され、本物の `LTCGI.cginc` を使用します。

- **自動検出**: `at.pimaker.ltcgi` パッケージのインストール/アンインストールを自動検知
- **対応ワールドで自動動作**: マテリアルで「LTCGI有効」をONにするだけで、スクリーンやエリアライトからの照明を自動的に受けます
- **アバターモード**: `LTCGI_AVATAR_MODE` を自動設定（ライトマップUV不要）
- **Diffuse + Specular**: エリアライトからの拡散光・反射光を個別に強度調整可能
- **手動再検出**: `Tools > Natane > VRChat > LTCGI 再検出`
- **パッケージ未検出時**: インスペクターUIが自動的に無効化され、インストール案内を表示

### 統合アーキテクチャ

両システムとも同じ自動検出パターンを採用しています：

```
パッケージインストール → エディタ起動時に自動検出 → Config hlsl生成
→ シェーダー再コンパイル → 本物のcginc関数を使用
→ 対応ワールドで自動的にライティングを受ける
```

## クイックスタート

### 1. Toon Shader

1. Materialを作成し、Shaderを `Natane/Toon Shader` に設定（またはインスペクターのシェーダータイプで「Toon」を選択）
2. 必要に応じて `Cutout` / `Transparent` バリアントに切替
3. 「クイックセットアップ」セクションでスタイルプリセットを選択（シャープなアニメ調 / 柔らかい塗り調 / ゲームキャラクター風 / Toon-PBR ハイブリッド / Near PBR）
4. 必要に応じて各タブでエフェクトを追加（Ctrl+1~5でタブ切替可能）

### 2. Eye Shader

1. 目用Materialを作成し、Shaderを `Natane/Eye` に設定（またはインスペクターのシェーダータイプで「Eye」を選択）
2. 目UVが左右対称配置の場合:
   - `Eye Center Mode = Symmetry From Center1`
   - `Symmetry Pivot X` を調整
   - 必要に応じて `Mirror Right Eye UV` をON
3. 顔と目が同一マテリアルの場合:
   - `Use Eye Region Mask` をON
   - `Eye Region Mask` と `Mask Channel` を設定

### 3. Wirelight + Cyber

1. `Natane/Toon Shader Wirelight` を適用（またはインスペクターのシェーダータイプで「Wirelight」を選択）
2. `Wire Style = Cyber Wire` を選択
3. `Enable AudioLink` と `AudioLink Cyber` を必要に応じてON

### 4. Screen FX Overlay

1. `Tools/Natane/エフェクト Effects/スクリーンエフェクト設定 Screen FX Setup` を実行
2. 生成マテリアルを調整して画面演出を追加

### PC版VRChatアップロード前

- アップロード前の確認項目は [Documentation~/VRCHAT_PC_UPLOAD_CHECKLIST.md](Documentation~/VRCHAT_PC_UPLOAD_CHECKLIST.md) を参照してください
- 特に `推定 Sampler 負荷`、`Light Volume / LTCGI` の有効状態、`Material Validator` の結果は先に確認するのが安全です

## 最適化ガイド（要点）

- 不要機能はトグルでOFFにし、キーワード数を抑える
- VR向けでは `Refraction`, `GrabPass系`, 多重エフェクトの同時使用を最小化
- GrabPass不要な場合は `Lite` バリアントを選択
- `最適化ハブ` の **Lite 変換** タブで、GrabPass 系機能とテッセレーションを使っていないマテリアルを選択範囲 / プロジェクト全体から検出して一括変換できる（見た目は変わらず、Undo 可能）
- `Shader Variant Collector` と `Shader Prewarming` を併用して実機負荷を安定化
- `バリアントストリッピング設定` でビルド時の未使用バリアントを除去
- 半透明重なりの破綻対策として、用途に応じて `Cutout` や `Dithering Alpha` を検討

## スタイル指向（参考）

- パラメータ調整により、フラットなアニメ調からリッチなNPR表現まで幅広いセルシェーディングスタイルに対応します
- イラスト調スタイル10機能（Color Quantize / Hatching / Watercolor / Kuwahara等）で手描き風表現も可能
- **ゲームキャラクター風プリセット**: 2段影＋リムライト＋スペキュラー＋テクスチャ連動アウトラインで、3Dゲーム風のトゥーンレンダリングをワンクリック設定
- **セミリアル Toon プリセット**: リアル寄りのトゥーン。グラデーション基調の陰影、影境界の色味、GGX ハイライト、白飛び抑制をワンクリック設定。個別の調整は「シャドウ設定 > セミリアル仕上げ」
- 既定構成は、NPRセル影を軸にした実用寄りのバランスです
- 詳細監査: `Documentation~/SHADER_STYLE_AND_OPTIMIZATION_AUDIT.md`

## ドキュメント

- 変更履歴: `CHANGELOG.md`
- フォルダ構造: `Documentation~/FOLDER_STRUCTURE.md`
- シェーダーバリアント: `Documentation~/SHADER_VARIANTS.md`
- 移行: `Documentation~/MIGRATION_GUIDE.md`
- クイックスタート: `Documentation~/QUICK_START.md`
- 技術資料: `Documentation~/TECHNICAL.md`
- Eye詳細: `Shaders/NataneToon/Eye/NataneToonEye使用方法.md`
- Eyeテクスチャ運用: `Shaders/NataneToon/Eye/NataneToonEye_テクスチャ対応.md`
- Wirelight詳細: `Shaders/NataneToon/NataneToonWirelight_README.md`
- ScreenFX詳細: `Shaders/NataneToon/Effects/NataneScreenFX_README.md`
- ランプテクスチャ: `Documentation~/RAMP_TEXTURE_GUIDE.md`
- パーティクル: `Documentation~/PARTICLE_SYSTEM_GUIDE.md`
- コントリビュート: `Documentation~/CONTRIBUTING.md`

## ライセンス

MIT License。詳細は `LICENSE` を参照してください。
