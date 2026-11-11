# NataneToon パーティクルシェーダー / Particle Shader

## 日本語

### 概要

`Natane/Toon Shader (Particle)` は、パーティクル向けの超軽量シェーダーバリアントです。
「背景・キャラ・パーティクル全てに対応できる汎用 Shader」の一角として、
トゥーンファミリーの見た目（2 段トゥーンシェーディング + 影色ティント）を保ちながら、
パーティクルに必要な機能だけを自己完結で実装しています（NataneToonCore の巨大な機能群は含みません）。

- パス数: 1（アウトラインなし、シャドウキャスターなし）
- `Cull Off` / `ZWrite Off` / `Queue = Transparent`
- VRChat フォールバック: `"VRCFallback" = "Particle"`
- `Runtime/ParticleSystem` のパーティクルエフェクトプリセット（`ParticleEffectPreset.particleMaterial`）にそのまま割り当てられます

### 機能一覧

| 機能 | プロパティ / キーワード | 説明 |
|------|------------------------|------|
| 頂点カラー | （常時有効） | テクスチャ × 頂点カラー × `_Color` で合成。Start Color / Color over Lifetime がそのまま反映されます |
| ブレンドモード | `_BlendMode`（GUI が `_SrcBlend` / `_DstBlend` / `_BlendOp` を設定） | Alpha / Additive / Premultiplied / Multiply。デフォルトは Alpha |
| トゥーンライティング | `_PARTICLE_TOON_LIGHTING` | ON でメインディレクショナルライト + SH アンビエントに対する 2 段トゥーン（`_ShadowColor` / `_ShadowThreshold` / `_ShadowSmoothness`）。OFF で Unlit（通常のパーティクル） |
| ソフトパーティクル | `_SOFT_PARTICLES` | 地面などとの交差を `_SoftParticleFadeDistance` で滑らかにフェード。カメラのデプステクスチャが必要 |
| フリップブック補間 | `_FLIPBOOK_BLENDING` | Texture Sheet Animation のフレーム間を滑らかに補間（下記の頂点ストリーム設定が必要） |
| エミッション | `_EMISSION` | `_EmissionColor`（HDR）× `_EmissionMap` |
| カメラフェード | `_CAMERA_FADE` | `_CameraFadeNear`〜`_CameraFadeFar` の距離でカメラ至近をフェードし、ニアクリップでのパッと消える現象を防止 |

### 使い方

1. マテリアルを新規作成し、シェーダーに `Natane/Toon Shader (Particle)` を選択
2. Particle System の Renderer モジュールの Material に割り当て
3. インスペクター（専用 GUI）でブレンドモードと必要な機能を設定

### 必要な頂点ストリーム（Custom Vertex Streams）

Renderer モジュール > Custom Vertex Streams を以下のように設定してください。
**順番が重要です**（Unity が上から順に POSITION / NORMAL / COLOR / TEXCOORD へパックします）。

基本（フリップブック補間なし）:

```
Position   (POSITION)
Normal     (NORMAL)    ← トゥーンライティング使用時に必要
Color      (COLOR)
UV         (TEXCOORD0.xy)
```

フリップブック補間あり（`_FLIPBOOK_BLENDING` ON）:

```
Position   (POSITION)
Normal     (NORMAL)
Color      (COLOR)
UV         (TEXCOORD0.xy)
UV2        (TEXCOORD0.zw)  ← 追加
AnimBlend  (TEXCOORD1.x)   ← 追加
```

さらに Texture Sheet Animation モジュールを有効にしてください。

### 注意事項

- ソフトパーティクルはデプステクスチャが有効な環境でのみ動作します
  （VRChat ではシャドウ付きディレクショナルライトのあるワールドなど）。無効な環境では単に効果が出ないだけで安全です
- VR（シングルパスステレオ / インスタンシング）対応済み。フォグ対応済み（加算は黒へ、乗算は白へフェード）
- パーティクルは総ピクセル負荷（オーバードロー）が支配的です。大きなパーティクルの重ねすぎに注意してください

---

## English

### Overview

`Natane/Toon Shader (Particle)` is an ultra-lightweight shader variant for particle systems.
It keeps the toon-family look (2-step toon shading + shadow color tint) while implementing
only what particles need, as a self-contained shader (it does not include the full
NataneToonCore feature set).

- Single pass (no outline, no shadow caster)
- `Cull Off` / `ZWrite Off` / `Queue = Transparent`
- VRChat fallback: `"VRCFallback" = "Particle"`
- Drop-in material for the particle effect preset system in `Runtime/ParticleSystem`
  (`ParticleEffectPreset.particleMaterial`)

### Features

| Feature | Property / Keyword | Description |
|---------|--------------------|-------------|
| Vertex color | (always on) | Texture x vertex color x `_Color`. Start Color / Color over Lifetime apply directly |
| Blend mode | `_BlendMode` (GUI drives `_SrcBlend` / `_DstBlend` / `_BlendOp`) | Alpha / Additive / Premultiplied / Multiply. Default: Alpha |
| Toon lighting | `_PARTICLE_TOON_LIGHTING` | 2-step toon vs main directional light + SH ambient (`_ShadowColor` / `_ShadowThreshold` / `_ShadowSmoothness`). Off = unlit (classic particle) |
| Soft particles | `_SOFT_PARTICLES` | Fades intersections with geometry over `_SoftParticleFadeDistance`. Requires the camera depth texture |
| Flipbook blending | `_FLIPBOOK_BLENDING` | Smoothly blends Texture Sheet Animation frames (requires the vertex streams below) |
| Emission | `_EMISSION` | `_EmissionColor` (HDR) x `_EmissionMap` |
| Camera fade | `_CAMERA_FADE` | Fades particles between `_CameraFadeNear` and `_CameraFadeFar` to avoid near-plane popping |

### Usage

1. Create a material and select the `Natane/Toon Shader (Particle)` shader
2. Assign it to the Particle System's Renderer module material
3. Configure blend mode and features in the dedicated inspector GUI

### Required Vertex Streams

Set Renderer module > Custom Vertex Streams as follows. **Order matters**
(Unity packs streams top-down into POSITION / NORMAL / COLOR / TEXCOORD semantics).

Basic (no flipbook blending):

```
Position   (POSITION)
Normal     (NORMAL)    <- required for Toon Lighting
Color      (COLOR)
UV         (TEXCOORD0.xy)
```

With flipbook blending (`_FLIPBOOK_BLENDING` on):

```
Position   (POSITION)
Normal     (NORMAL)
Color      (COLOR)
UV         (TEXCOORD0.xy)
UV2        (TEXCOORD0.zw)  <- add
AnimBlend  (TEXCOORD1.x)   <- add
```

Also enable the Texture Sheet Animation module.

### Notes

- Soft particles only work when the camera depth texture is available
  (e.g. VRChat worlds with a shadowed directional light). When unavailable, the effect
  is simply skipped — nothing breaks
- VR-ready (single-pass stereo / instancing) and fog-ready (additive fades to black,
  multiply fades to white)
- Particle cost is dominated by overdraw; avoid stacking many large particles
