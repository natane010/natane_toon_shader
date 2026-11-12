# URP / Unity 6 対応 (URP Support)

## 概要

Natane Toon Shader は Built-in RP (VRChat) 用のシェーダーですが、主要な 6 つのシェーダーは **URP (Universal Render Pipeline) でもそのまま描けます**。

- **Built-in RP (VRChat) はこれまで通り** — BiRP 用の SubShader は変更していません。URP 用の SubShader と C# は、URP パッケージの入っていないプロジェクトでは読み込まれません (ShaderLab の `PackageRequirements` と asmdef の `defineConstraints` で判定)。
- マテリアルのプロパティ名・シェーダーキーワードは BiRP 版と共通です。**同じマテリアルが両方のパイプラインで動きます**。
- Unity 6 の URP では、RenderGraph / Forward+ / Deferred+ / Adaptive Probe Volumes / GPU Resident Drawer などに対応した専用の SubShader が使われます。

## 動作要件

| 環境 | 使われる SubShader | 検証 |
|---|---|---|
| Unity 6.1 以降 / URP 17.1+ | Unity 6 用 SubShader | Unity 6000.3.11f1 (URP 17.3.0)、Unity 6000.5.7f1 (URP 17.5.0) でコンパイル検証済み。URP 17.1 / 17.2 は未検証 |
| Unity 2022.3 〜 Unity 6.0 / URP 14 〜 17.0 | 互換 SubShader | Unity 2022.3.33f1 (URP 14.0.11) でコンパイル検証済み。URP 15 〜 17.0 は未検証 |
| Built-in RP (VRChat) | BiRP SubShader | Unity 2022.3 で検証 (従来どおり) |

検証は `Tests/CI/setup-urp-validation-project.sh` による全パス・複数キーワード組み合わせのコンパイル (エラー・警告ゼロ) です。

## URP で使えるシェーダー

| シェーダー | URP |
|---|---|
| `Natane/Toon Shader` / `(Lite)` | ○ |
| `Natane/Toon Shader (Cutout)` / `(Cutout Lite)` | ○ (アルファ抜きは影・深度・モーションベクターにも反映) |
| `Natane/Toon Shader (Transparent)` / `(Transparent Lite)` | ○ (BiRP と同じく影・深度は書かない) |
| `Natane/Screen FX Overlay` | ○ (Unity 6 / URP 17+。Renderer Feature 経由。下記) |
| Fur / Ghost / XRay / Background / ScreenEdgeSplit / Eye / Wirelight / Particle / FakeShadow ほか | × (Built-in RP 専用。URP ではピンク表示) |

シーン内のマテリアルが URP 対応のバリアントかどうかは、`Tools > Natane > URP > シーンの URP 対応を確認` で一覧できます (Unity 6 / URP 17+)。

URP では Lite と通常版の描画内容は同じです (どちらもコア機能)。

## Unity 6 用 SubShader (URP 17.1+)

| Pass | LightMode | 内容 |
|---|---|---|
| ForwardLit | `UniversalForwardOnly` | トゥーンシェーディング本体。Forward / Forward+ / Deferred / Deferred+ のどのレンダリングパスでも前方描画される |
| Outline | `SRPDefaultUnlit` | 反転ハル方式のアウトライン (Renderer Feature 不要) |
| ShadowCaster | `ShadowCaster` | 影 (ディレクショナル / ポイント / スポット) |
| DepthOnly | `DepthOnly` | 深度プリパス / Depth Texture |
| DepthNormalsOnly | `DepthNormalsOnly` | SSAO・Depth Normals Texture・Deferred 時の法線 |
| MotionVectors | `MotionVectors` | オブジェクトモーションベクター (TAA / STP / モーションブラー) |
| XRMotionVectors | `XRMotionVectors` | XR の Application SpaceWarp |

Transparent バリアントは ForwardLit と Outline のみです。

### Unity 6 の機能への対応

| 機能 | 対応 |
|---|---|
| RenderGraph | ○ (シェーダーパスは RenderGraph の描画にそのまま乗る。Screen FX は RenderGraph API で実装) |
| Forward+ / Deferred+ (クラスターライトループ) | ○ `_CLUSTER_LIGHT_LOOP`。追加のディレクショナルライトも処理 |
| Deferred / Deferred+ レンダリングパス | ○ トゥーンは GBuffer に乗らないため、前方描画 (ForwardOnly) で合成 |
| Adaptive Probe Volumes (APV) | ○ L1 / L2、プローブオクルージョン |
| ライトレイヤー (Rendering Layers) | ○ メインライト・追加ライト |
| Rendering Layers テクスチャの書き出し | ○ (デカール等の Rendering Layer マスク用) |
| ライトクッキー | ○ |
| シャドウマスク (プローブオクルージョン) | ○ |
| SH の評価位置 (頂点 / ピクセル / 混合) | ○ URP の設定に従う |
| LOD クロスフェード | ○ 全パス |
| モーションベクター / TAA / STP | ○ |
| XR: Single Pass Instanced / Foveated Rendering / Application SpaceWarp | ○ キーワード対応 (実機での確認は未実施) |
| SRP Batcher | ○ 全パスで `UnityPerMaterial` を共有 |
| GPU Resident Drawer (DOTS Instancing) | ○ 全パス |
| スクリーンスペースの影 / ソフトシャドウ品質 | ○ |
| SSAO の受け取り | × 意図的に無効 (トゥーンの面が汚れるため)。DepthNormals は書くので他のオブジェクトの SSAO には寄与 |
| ライトマップ / Meta パス | × (アバター用途を想定) |
| デカール (DBuffer) の受け取り | × |

GPU Resident Drawer を使うには、URP Asset の **GPU Resident Drawer** を `Instanced Drawing` に設定してください (対応グラフィックス API が必要)。DOTS Instancing でインスタンスごとに変えられるプロパティは `_Color` / `_EmissionColor` / `_OutlineColor` / `_OutlineWidth` です。

## 互換 SubShader (URP 14 〜 17.0)

ForwardLit (`UniversalForward`、Forward / Forward+) / Outline / ShadowCaster / DepthOnly / DepthNormalsOnly。メインライトの影、追加ライト、SH アンビエント、フォグに対応します。APV・ライトレイヤー・ライトクッキー・モーションベクター・LOD クロスフェードは Unity 6 用 SubShader のみです。

URP 17.1 以降では `_FORWARD_PLUS` を宣言するだけで非推奨の警告が出るため、この SubShader は `PackageRequirements` のバージョン範囲 (`[14.0.0,17.1.0)`) で読み飛ばされます。

## 対応機能 (URP でのコア機能スコープ)

URP では BiRP 版の全機能ではなく、**トゥーン表現のコア機能**が動きます。

- メインテクスチャ / カラー (`_MainTex` / `_Color`)、Cutout の `_Cutoff`、Transparent のブレンド設定
- トゥーン段階シェーディング (`_ShadowSteps` / `_ShadowSharpness` / `_ShadowColor` / `_ShadowOffset` / `_StepBorderSmooth` / `_ShadowBlend` / `_LitSoftness`、fwidth ベースのアンチエイリアス付き)
- ランプテクスチャ (`_USE_RAMP` / `_RampTex`)
- ノーマルマップ (`_NORMALMAP`)
- リムライト (`_RIM_LIGHT`)
- MatCap (`_MATCAP`)
- エミッション (`_EMISSION`)
- アウトライン (`_OUTLINE`: 幅 / 色 / モード / 距離補正 / マスク / 幅マップ / テクスチャ連動カラー / マルチカラー)
- スムーズノーマル (`_SMOOTH_NORMAL`: 頂点カラー OS / 頂点カラー TS (lilToon 互換) / ベイク法線テクスチャ)
- ステンシル設定、カリング、ZWrite、Alpha To Coverage

## Screen FX (URP / RenderGraph)

`Natane/Screen FX Overlay` (画面全体の撮影効果: 放射ブラー・色収差・ポスタライズ・走査線・グレイン・ビネット・グラデーション・モノクロなど) は、BiRP では GrabPass を使うクアッドとして描きます。URP に GrabPass は無いので、**Natane Screen FX Renderer Feature** が RenderGraph の全画面パスとして描きます。効果の中身は BiRP 版と同じコード (`Effects/NataneScreenFXCore.hlsl`) です。

### セットアップ

1. `Tools > Natane > URP > Screen FX を Renderer に追加` を実行する。いま使われている URP Renderer に Renderer Feature が追加され、マテリアルが無ければ `Assets/Natane Screen FX (URP).mat` が作られます (既存の Screen FX マテリアルを選択してから実行すると、それが割り当てられます)。
2. マテリアルのインスペクターで効果を調整する。

手動で行う場合は、Renderer のインスペクターで `Add Renderer Feature > Natane Screen FX Renderer Feature` を選び、マテリアルを割り当てます。

| 設定 | 内容 |
|---|---|
| Material | `Natane/Screen FX Overlay` のマテリアル |
| Injection Point | `After Post Processing` (既定。トーンマップ後の色にかける) / `Before Post Processing` (HDR のシーンカラーにかける) |
| Apply To Scene View | シーンビューにもかける |

### 最適化

- カメラカラーを読んで **1 回の全画面パス**で書き出し、単独のカメラではそのテクスチャをカメラカラーとして差し替えます (元のターゲットへの書き戻しコピーなし)。カメラスタック中は安全のため書き戻します。
- マテリアルの `Effect Blend` が 0 のあいだは、パス自体を登録しません。
- 書き込み先は全ピクセル上書きとして宣言しており、タイルベース GPU で不要なロードが発生しません。

### 注意

- RenderGraph が必要です (Unity 6 の既定)。URP の「Compatibility Mode (Render Graph Disabled)」では描かれません。
- マテリアルをメッシュに貼っても URP では描かれません (Renderer Feature 経由でのみ動作)。
- 実機・エディタでの見た目の確認は行っていません (コンパイル検証のみ)。

## 既知の制限事項

- BiRP 版の高度な機能 (GrabPass 系の屈折・フィルター、SDF 影、AudioLink、LTCGI、VRC Light Volumes、視差、テッセレーション、ディゾルブ、5 層メイクテクスチャ等) は URP では動作しません。
- 追加ライトの「頂点ライティング」設定 (`_ADDITIONAL_LIGHTS_VERTEX`) でもピクセル単位で計算されます。
- アウトラインの `_SMEAR` / `_HEIGHT_FADE` / 手描き風 / パース平坦化 / VRChat ミラー補正は URP 版では省略されています。
- URP 12 / 13 (Unity 2021.x) は対象外です。
- マテリアルインスペクターは BiRP 版と共通で、URP で効かない項目も表示されます。

## 開発者向け

- URP の SubShader は `Tests/CI/generate_urp_subshaders.py` が 6 つの `.shader` に生成します (マーカー `<auto-generated:natane-urp-subshaders>` の間)。パス構成を変えるときはスクリプトを直して再生成してください。`--check` で差分を検出できます。
- パスの実装は `Shaders/NataneToon/Include/URP/` (`NataneToonURPInput.hlsl` / `ForwardPass` / `OutlinePass` / `DepthPasses`)。Unity 6 用と互換用の違いは `NATANE_URP_UNITY6` で切り替えています。
- URP のコンパイル検証: `Tests/CI/setup-urp-validation-project.sh --unity <Unity 実行ファイル>` (エディタ同梱の URP を使うので、確認したいエディタごとに実行)。
- C#: `Runtime/URP/` (`NataneToon.Runtime.URP`) と `Editor/NataneToon/Integration/URP/` (`NataneToon.Editor.URP`) は URP 17 以上でのみコンパイルされます。

---

# URP Support (English Summary)

Six shaders (`Natane/Toon Shader`, Cutout, Transparent and their Lite versions) render in URP with the same materials, properties and keywords as Built-in RP. Built-in RP (VRChat) is unchanged: the URP SubShaders are gated by ShaderLab `PackageRequirements` and the URP C# by asmdef `defineConstraints`, so neither loads without the URP package.

- **Unity 6 SubShader (URP 17.1+, compile-verified on URP 17.3 and 17.5)**: `UniversalForwardOnly` forward pass (works with Forward, Forward+, Deferred and Deferred+), outline, ShadowCaster, DepthOnly, DepthNormalsOnly, MotionVectors and XRMotionVectors. Supports the cluster light loop, Adaptive Probe Volumes, light layers, rendering-layers output, light cookies, shadowmask, SH evaluation modes, LOD cross-fade, foveated rendering keywords, SRP Batcher and GPU Resident Drawer.
- **Compatibility SubShader (URP 14 to 17.0, compile-verified on URP 14.0.11)**: forward, outline, shadow and depth passes.
- **Screen FX**: `Natane/Screen FX Overlay` runs in URP 17+ through the RenderGraph-based `Natane Screen FX Renderer Feature` (`Tools > Natane > URP > Add Screen FX Renderer Feature`). One fullscreen pass with a camera-colour swap; skipped entirely while Effect Blend is 0.
- **Not supported in URP**: the other variants (Fur, Ghost, XRay, Background, Eye, Particle, ...), BiRP-only advanced features, lightmaps, decal reception and SSAO reception (disabled on purpose).
- Verification so far is shader/C# compilation only; the look has not been checked in a running URP scene.
