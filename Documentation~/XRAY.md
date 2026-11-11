# X-Ray バリアント (Natane/Toon Shader (X-Ray))

壁やほかのオブジェクトに **隠れている部分だけ** を、別の色・輪郭・パターンで透かして表示する専用シェーダーバリアントです。
`PROPOSED_EXPRESSION_FEATURES.md` の「8. 遮蔽シルエット／X-Ray」に対応します。

追加の深度テストパスを必要とするため通常シェーダーには常設せず、**X-Ray 専用バリアント** として提供しています。

---

## 技術概要: X-RAY 専用パス

このバリアントは Cutout バリアントをベースに生成されており、**見えている部分**(遮蔽されていない部分)は
通常の Forward パス (`ZTest LEqual`) でトゥーンシェーダーの全機能を使って普通に描画されます。

これに加えて、**OUTLINE パスの直後・通常 Forward パスの前** に **X-RAY パス** を 1 枚描画します。

- `Tags { "LightMode" = "Always" }`
- `ZTest Greater` … デプスバッファより奥にあるフラグメント = **遮蔽されている部分だけ** を描画
- `ZWrite Off`
- `Blend [_XRaySrcBlend] [_XRayDstBlend]` … 既定は `SrcAlpha OneMinusSrcAlpha` (アルファ合成)。
  加算にしたい場合は `One One` を指定
- `Cull Back`

X-RAY パスは自己完結した CGPROGRAM です。頂点シェーダーは通常の `vert` を共有しているため、
VAT / Smear / Perspective Flatten / Face Ortho などの頂点変形を有効にしても、透視シルエットが本体と一致します。
(有効な頂点キーワード: `_VAT` / `_VAT_NORMAL` / `_SMEAR` / `_PERSPECTIVE_FLAT` / `_FACE_ORTHO` / `_FACE_ORTHO_MASK`)

Stencil は SubShader レベルで宣言されており (`_StencilRef` ほか)、X-RAY パスにもそのまま適用されます。

---

## 表示モード (`_XRayMode`)

| 値 | モード | 内容 |
|---|---|---|
| 0 | OutlineOnly | フレネルエッジのみを残す。`_XRayOutlineWidth`(pow ベースの輪郭幅)で細さを調整 |
| 1 | SolidFill | `_XRayOccludedAlpha` によるフラットな塗りつぶし |
| 2 | DitherFill | Bayer 4x4 ディザによる網点塗り(スクリーン画素基準) |
| 3 | Scanline | スキャンライン。`_XRayScanlineSpace` で スクリーン空間 / ワールドY を選択、`_XRayScanlineScale` / `_XRayScanlineSpeed` で間隔と速度 |
| 4 | Fresnel | リムグラデーション塗り + エッジへの加算発光(既定モード) |
| 5 | Pulse | `_XRayPulseSpeed` による時間サイン波でアルファを点滅 |
| 6 | DepthGradient | 遮蔽物からの奥行きに応じてフェード(視点深度 `_XRayDepthFade` による近似) |

> 仕様案では Wireframe モードも挙げられていますが、追加のジオメトリシェーダー / バリセントリック座標が必要なため
> このバリアントには含めていません(実装コスト回避)。線的な表現は OutlineOnly / Scanline で代替できます。

---

## パラメータ

| プロパティ | 説明 | 既定値 |
|---|---|---|
| `_XRayColor` (HDR) | 遮蔽部分の色。Bloom と組み合わせると発光する | (0.3, 0.8, 1.5, 1) |
| `_XRayOccludedAlpha` | 遮蔽部分の基本不透明度 | 0.6 |
| `_XRayMode` | 表示モード(上表) | 4 (Fresnel) |
| `_XRayOutlineWidth` | OutlineOnly モードのフレネル輪郭幅(pow) | 4 |
| `_XRayDepthFade` | DepthGradient モードの視点深度フェード距離 | 10 |
| `_XRayScanlineScale` | スキャンライン間隔 | 80 |
| `_XRayScanlineSpeed` | スキャンライン移動速度 | 1 |
| `_XRayScanlineSpace` | スキャンライン座標系 (0=スクリーン, 1=ワールドY) | 0 |
| `_XRayFresnelPower` | Fresnel モードの輪郭鋭さ | 2.5 |
| `_XRayPulseSpeed` | Pulse モードの点滅速度 | 3 |
| `_XRaySrcBlend` / `_XRayDstBlend` | X-RAY パスのブレンド係数 | SrcAlpha / OneMinusSrcAlpha |
| Stencil (`_StencilRef` 他) | SubShader レベルの Stencil 制御(全パス共通) | — |

- テクスチャのアルファ (`_MainTex.a × _Color.a`) でマスクされ、小さなクリップ閾値によって
  カットアウト部分(穴)が壁越しに光らないようになっています。

---

## 主な用途

- ワールド演出 / 自己遮蔽の小物: 壁越しのキャラクター輪郭、索敵表示、隠れた演者・アイテムの強調。
- サイバースキャン演出(Topographic Lines と併用してスキャン線 + 透視表示)。
- 幽霊 / 霊体の「隠れると見える」表現。

### VRChat での挙動

- **アバターに使う場合**: 表示されるのは「他プレイヤーのワールド(壁など)を透かして自分が見える」挙動です。
  他プレイヤー自身のメッシュがデプスに書き込む限り、その奥にいる自分の X-Ray シルエットが見えます。
  自分自身のメッシュの奥側(自己遮蔽)も表示されます。
- Safety でブロックされたアバターは `VRCFallback = "ToonCutout"` により通常のトゥーンカットアウトに退避します。

---

## 既知の制限

- **描画順(透明ソート)**: X-RAY パスは `LightMode = "Always"` のため、後から描画される
  他の半透明オブジェクトを透かして常に表示されてしまうことがあります。半透明オブジェクトの
  描画順ソートの外側で描かれるための構造的な限界であり、Ghost / 通常の半透明シェーダーと同様です。
- Wireframe モードは非対応(上記参照)。
- DepthGradient は本来「遮蔽物との距離」で減衰させたいものですが、追加の深度サンプリングを避けるため、
  フラグメント自身の視点深度による近似としています。

---

## English Summary

**Natane/Toon Shader (X-Ray)** is a dedicated variant that shows only the *occluded* part of a mesh through
walls and other geometry. The visible part renders normally through the full toon feature set (`ZTest LEqual`),
while a dedicated **X-RAY pass** — inserted right after OUTLINE and before the forward passes —
draws the see-through silhouette with `Tags{LightMode=Always}`, `ZTest Greater`, `ZWrite Off`,
`Blend [_XRaySrcBlend] [_XRayDstBlend]` (default alpha, use `One One` for additive), `Cull Back`.

It shares the standard `vert` (with the `_VAT`/`_SMEAR`/`_PERSPECTIVE_FLAT`/`_FACE_ORTHO`/`_FACE_ORTHO_MASK`
vertex keywords) so vertex deformation stays consistent, and respects the `_MIRROR_CONTROL` discards.
Cutout regions are masked via `mainTex.a × _Color.a` with a small clip threshold.

Display modes (`_XRayMode`): 0 OutlineOnly, 1 SolidFill, 2 DitherFill (Bayer), 3 Scanline (screen or world-Y),
4 Fresnel (default), 5 Pulse, 6 DepthGradient. Stencil control lives at the SubShader level (`_StencilRef` etc.).

**Limitations**: as a `LightMode=Always` pass it can show through transparent objects drawn later
(transparent draw-order limitation). Wireframe mode is intentionally omitted. `VRCFallback="ToonCutout"`.
