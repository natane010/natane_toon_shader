# Ghost バリアント (Natane/Toon Shader (Ghost))

半透明の「幽霊」表現に特化したシェーダーバリアントです。
通常の半透明シェーダーで起きる **自己重なりの破綻**(体の前後が二重にブレンドされて濃くなる／裏面が透けて見える)が、構造的に発生しないように設計されています。

---

## 技術概要: 深度プリパス (Depth Prepass) 方式

通常の半透明レンダリングは `ZWrite Off` で描画するため、1つのメッシュ内で重なり合った面がすべて順不同にブレンドされます。その結果、

- 腕と胴体が重なった部分だけ色が濃くなる(二重ブレンド／加算)
- 裏側(背面)のポリゴンが表から透けて見える

といった破綻が起こります。

Ghost バリアントはこれを次の 2 段構えで解決します。

1. **GHOST_DEPTH_PREPASS パス** (最初のパス)
   - `ColorMask 0`(色を書かない) + `ZWrite On`(深度だけ書く)
   - 最も手前の 1 層の深度のみをデプスバッファに確定させます。
   - `_GhostDepthCutoff` によりテクスチャのアルファが極端に低い箇所は `clip()` で除外します。
   - 頂点処理は通常の `vert` を共有しているため、VAT / Smear / Perspective Flat などの頂点変形を有効にしても深度が本体の見た目と一致します。

2. **FORWARD_BASE / FORWARD_ADD パス**
   - `ZWrite Off` + **`ZTest Equal`**
   - プリパスで確定した「最前面の深度」と一致するフラグメントだけがシェーディング・ブレンドされます。
   - つまり **各ピクセルで必ず 1 層だけ** が描画され、重なり部分の二重ブレンドも裏面の透けも発生しません。

### 保証されること (No Self-Overlap Guarantee)

- 同一メッシュ内で前後に重なった半透明面が二重にブレンド／加算されない。
- 背面ポリゴンが表側に透けて見えない。
- 透明度を上げても、体のシルエットが常にきれいな 1 枚のガラスのように見える。

---

## 見た目 (Fragment 処理)

`GHOST_VARIANT` が定義されている場合、最終カラー確定後に以下を適用します(`NataneToonFragment.hlsl`)。

- **フレネル状のアルファ**: 正面(視線に正対する面)ほど透明になり、輪郭(シルエット)側ほど不透明に残ります。これにより「中は透けているが縁は見える」霊的な質感になります。
- **加算リムティント**: `_GhostRimColor`(HDR)をフレネルに沿って縁に加算し、スペクトル的な発光を与えます。

---

## パラメータ

| プロパティ | 説明 | 既定値 |
| --- | --- | --- |
| `_GhostRimColor` | 縁の色 (HDR)。Bloom と組み合わせると発光が強調される。 | (0.4, 0.8, 1.2, 1) |
| `_GhostFresnelAlpha` | 中心の透け具合。0 = フレネルによるアルファ変化なし、1 = 中心が最大限に透ける。 | 0.7 |
| `_GhostFresnelPower` | フレネルの鋭さ。大きいほど縁だけが細く残る。 | 2.5 |
| `_GhostRimStrength` | 縁の発光(加算)強度。 | 1.0 |
| `_GhostDepthCutoff` | 深度プリパスで描画する最低アルファ。テクスチャの薄い部分を深度から除外する。 | 0.02 |

- **全体の透明度**は `Color` のアルファ (`_Color.a`) で調整します。フレネルアルファはその上に乗算されます。

---

## Tips: 足元を消す (Height Fade)

幽霊らしく「下半身が空気に溶ける」表現には、**高さフェード (Height Fade)** をアルファモードで併用してください。

- Height Fade を有効化し、フェード軸を Y(上下)に設定。
- モードを「アルファ」にして、足元(下側)のアルファを 0 に向けてフェードさせます。
- Ghost の深度プリパスと組み合わさっても、消えていく部分は深度カットオフと整合するため破綻しません。

## 注意点

- **アウトラインパスはありません。** 単一レイヤーで描画する Ghost モデルの性質上、輪郭線は構造に合わないため削除されています。輪郭的な表現が欲しい場合は `_GhostRimColor` / `_GhostRimStrength` によるリムで代用してください。
- SubShader は `Queue = Transparent` で、`VRCFallback = ToonTransparent` を宣言しています。
- 影は落としません(ShadowCaster なし)。半透明オブジェクトが不透明な影を落とすのを避けるためです。

---

## English Summary

**Natane/Toon Shader (Ghost)** is a translucent "ghost" variant that structurally avoids self-overlap artifacts (double-blend where body parts overlap, and backface bleed-through) common to ordinary transparent shaders.

It uses a **depth prepass**: the first pass writes depth only (`ColorMask 0`, `ZWrite On`), and the forward passes render with `ZWrite Off` + **`ZTest Equal`**, so exactly one (the nearest) surface layer is shaded and blended per pixel. This guarantees no double-blend and no backface bleed-through.

Appearance is a Fresnel-shaped alpha (transparent at the center, opaque at the silhouette) plus an additive spectral rim tint (`_GhostRimColor`).

Key parameters: `_GhostFresnelAlpha` (center fade), `_GhostFresnelPower` (fresnel sharpness), `_GhostRimColor` / `_GhostRimStrength` (edge glow), `_GhostDepthCutoff` (prepass alpha threshold). Overall opacity is controlled by the `Color` alpha.

Tip: to fade the feet, combine with **Height Fade** in alpha mode along the Y axis. Note the outline pass is intentionally removed for this single-layer variant, and it casts no shadows.
