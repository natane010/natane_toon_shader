# URP / Unity 6 GPU Resident Drawer 対応 (URP Support)

## 概要

`Natane/Toon Shader` (メインの Opaque シェーダー) に、条件分岐型の **URP (Universal Render Pipeline) SubShader** を追加しました。

- **Built-in RP (VRChat) はこれまで通りデフォルト** — 既存の BiRP SubShader は一切変更されていません。
- ShaderLab の `PackageRequirements` により、**URP パッケージ (`com.unity.render-pipelines.universal` 14.0.0 以上) が導入されているプロジェクトでのみ** URP SubShader が有効になります。URP 未導入のプロジェクト (BiRP / VRChat) では自動的にスキップされ、従来の BiRP SubShader が使われます。
- マテリアルのプロパティ名・シェーダーキーワードは BiRP 版と共通のため、**同じマテリアルが両パイプラインでそのまま動作**します。

## 動作要件

| 項目 | 要件 |
|---|---|
| URP SubShader | Unity 2022.2+ / URP 14.0.0+ (`PackageRequirements` で自動判定) |
| GPU Resident Drawer | Unity 6 (URP 17+) + 対応 API (DX12 / Vulkan / Metal 等) |
| 検証済み環境 | Unity 6000.0.55f1 (URP 17.0.4) / Unity 2022.3.28f1 (BiRP) |

## URP SubShader のパス構成

| Pass | LightMode | 内容 |
|---|---|---|
| ForwardLit | `UniversalForward` | メインのトゥーンシェーディング (Forward / Forward+ 両対応) |
| Outline | `SRPDefaultUnlit` | 反転ハル方式アウトライン (Renderer Feature 不要) |
| ShadowCaster | `ShadowCaster` | シャドウキャスト (URP 標準パスを利用) |
| DepthOnly | `DepthOnly` | デプスプリパス |
| DepthNormalsOnly | `DepthNormalsOnly` | SSAO 等の DepthNormals テクスチャ用 |

Meta (ライトマップベイク) パスと GBuffer (Deferred) パスは未実装です。

## 対応機能 (URP でのコア機能スコープ)

URP SubShader は BiRP 版の全機能ではなく、**トゥーン表現のコア機能のみ**を移植しています。プロパティ名・キーワードは BiRP と同一です。

- メインテクスチャ / カラー (`_MainTex` / `_Color`)
- トゥーン段階シェーディング (`_ShadowSteps` / `_ShadowSharpness` / `_ShadowColor` / `_ShadowOffset` / `_StepBorderSmooth` / `_ShadowBlend` / `_LitSoftness`、fwidth ベースのアンチエイリアス付き)
- ランプテクスチャ (`_USE_RAMP` / `_RampTex`)
- ノーマルマップ (`_NORMALMAP` / `_BumpMap` / `_BumpScale`)
- リムライト (`_RIM_LIGHT` / `_RimColor` / `_RimPower` / `_RimIntensity`)
- MatCap (`_MATCAP` / `_MatCapTex` / `_MatCapIntensity` / `_MatCapBlendMode` / `_MatCapBlend`)
- エミッション (`_EMISSION` / `_EmissionMap` / `_EmissionColor` [HDR])
- アウトライン (`_OUTLINE` / `_OutlineWidth` / `_OutlineColor` / `_OutlineMode` (反転ハル / バックフェイス) / 距離補正 / エッジ幅補正 / マスク / 幅マップ / テクスチャ連動カラー / マルチカラー)
- スムーズノーマル (`_SMOOTH_NORMAL`: 頂点カラー OS / 頂点カラー TS (lilToon 互換) / ベイク法線テクスチャの 3 モード)
- ステンシル設定 (BiRP と共通のステンシルプロパティ)

### URP ライティング

- メインライト: `GetMainLight(shadowCoord)` によるリアルタイムシャドウ対応 (`_MAIN_LIGHT_SHADOWS` / `_MAIN_LIGHT_SHADOWS_CASCADE` / `_MAIN_LIGHT_SHADOWS_SCREEN` / `_SHADOWS_SOFT` 系)
- 追加ライト: ピクセル単位のトゥーンステップ処理付きライトループ (`_ADDITIONAL_LIGHTS` / `_ADDITIONAL_LIGHT_SHADOWS`)
- **Unity 6 Forward+ 対応**: `_FORWARD_PLUS` キーワードと `LIGHT_LOOP_BEGIN` / `LIGHT_LOOP_END` (クラスタードライトループ) を使用
- アンビエント: `SampleSH` による球面調和ライティング
- フォグ: `multi_compile_fog` 対応

## SRP Batcher / GPU Resident Drawer 対応

### SRP Batcher

全パスで共通の include (`Shaders/NataneToon/Include/URP/NataneToonURPInput.hlsl`) を使用し、`CBUFFER_START(UnityPerMaterial)` のレイアウトを完全に一致させています。

### GPU Resident Drawer (Unity 6)

URP の `Lit.shader` と同じパターンで DOTS Instancing (BatchRendererGroup) に対応しています。

- 全パス (Forward / Outline / ShadowCaster / DepthOnly / DepthNormalsOnly) に `#pragma multi_compile _ DOTS_INSTANCING_ON` (URP の `DOTS.hlsl` を `include_with_pragmas` で使用、DOTS 変種は `#pragma target 4.5`)
- `UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)` ブロックで以下をインスタンス化プロパティとして公開:
  - `_Color` (float4)
  - `_EmissionColor` (float4)
  - `_OutlineColor` (float4)
  - `_OutlineWidth` (float)
- URP Lit と同様の static キャッシュ (`UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES`) パターンを採用

GPU Resident Drawer を使うには、URP Asset の **GPU Resident Drawer** を `Instanced Drawing` に設定してください (Unity 6 / 対応グラフィックス API が必要)。

## 既知の制限事項

- **Opaque (メイン) シェーダーのみ**: Cutout / Transparent / Lite / Fur / Background 等のバリアントは現時点で URP SubShader を持ちません (URP プロジェクトではマゼンタ表示になります)。
- BiRP 版の高度な機能 (GrabPass 系の屈折・フィルター、SDF 影、AudioLink、LTCGI、VRC Light Volumes、視差、テッセレーション、ディゾルブ、5 層メイクテクスチャ等) は URP では動作しません。
- ライトマップ / Meta パス未対応 (アバター用途を想定)。Deferred レンダラーの GBuffer パスも未対応です (Forward / Forward+ を使用してください)。
- 追加ライトの「頂点ライティング」品質設定 (`_ADDITIONAL_LIGHTS_VERTEX`) の場合もピクセル単位で計算されます。
- アウトラインの `_SMEAR` / `_HEIGHT_FADE` / 手描き風 / パース平坦化 / VRChat ミラー補正は URP 版では省略されています。
- URP 12/13 (Unity 2021.x) は対象外です (`PackageRequirements` が 14.0.0 以上のため)。

---

# URP Support (English Summary)

The main `Natane/Toon Shader` now contains a conditional URP SubShader gated by ShaderLab `PackageRequirements` (`com.unity.render-pipelines.universal >= 14.0.0`). Built-in RP remains the default and the BiRP SubShader is untouched; projects without URP skip the URP SubShader entirely.

- Passes: UniversalForward (toon core), SRPDefaultUnlit (inverted-hull outline), ShadowCaster, DepthOnly, DepthNormalsOnly.
- Core feature set driven by the same material properties/keywords as BiRP: main tex/color, stepped toon shading with fwidth AA, ramp texture, normal map, rim light, MatCap, emission, outline (incl. smooth-normal modes).
- URP lighting: main light with realtime shadows, per-pixel additional light loop with Unity 6 Forward+ (`_FORWARD_PLUS` cluster light loop), SH ambient, fog.
- SRP Batcher compatible (single shared `UnityPerMaterial` CBUFFER across all passes).
- GPU Resident Drawer (Unity 6) compatible: `DOTS_INSTANCING_ON` in all passes with `_Color`, `_EmissionColor`, `_OutlineColor`, `_OutlineWidth` exposed as DOTS-instanced properties, following URP Lit's pattern.
- Limitations: main (Opaque) shader only; variants, lightmaps/Meta, GBuffer/Deferred, and BiRP-only advanced features are not supported in URP.
