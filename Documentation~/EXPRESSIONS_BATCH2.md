# 表現系エフェクト バッチ2 (v1.6.0)

Natane Toon Shader v1.6.0 で追加された 3 つの表現系機能のシェーダー仕様です。
いずれもキーワード (`shader_feature_local`) で切り替わり、**OFF 時は描画結果に一切変化を与えません** (ゼロコスト)。
Opaque / Cutout / Transparent / Lite / Fur / Background / Ghost / ScreenEdgeSplit の全バリアントに対応しています
(Particle バリアントは対象外)。

対象パス:
- `FORWARD_BASE` / `FORWARD_ADD`: 3 機能すべて (いずれもフラグメント専用。OUTLINE / SHADOW_CASTER には追加しません)
- `_CAUSTICS` は `_TOPOGRAPHIC` と同様に ForwardBase のみで描画 (加算色のため ForwardAdd では中立)

マスク・アトラス・パレット・パターンテクスチャはすべて NOSAMPLER 共有サンプラーで、追加サンプラーを消費しません。

---

## A. レンチキュラー (`_LENTICULAR`)

見る角度によって、横並び (Horizontal) またはグリッド (Grid) アトラスの別フレームへ絵柄が切り替わります。
角度判定はオブジェクト空間の視線ベクトルで行うため、VRChat ミラーやカメラ位置に依存しにくく安定します。
選択したフレームを **メイクアップテクスチャ合成の直後 (ベースカラー修飾)** に混合します。

| プロパティ | キーワード / 型 | 既定 | 説明 |
|---|---|---|---|
| `_Lenticular` | `[Toggle(_LENTICULAR)]` | 0 | 有効化 |
| `_LenticularAtlas` | 2D (NOSAMPLER) | white | 複数フレームを格納したアトラス |
| `_LenticularFrames` | Range(1,16) | 4 | フレーム数 |
| `_LenticularDirection` | Enum(Horizontal,0,Grid,1) | 0 | アトラス配置 (Grid は ceil(√n) 列の行優先) |
| `_LenticularMode` | Enum(0-5) | 0 | SmoothBlend/HardStep/ScanBlend/FrontReveal/SideReveal/Flip |
| `_LenticularViewAxis` | Enum(X,0,Y,1) | 0 | 角度判定軸 (X=横振り, Y=縦振り) |
| `_LenticularAngleRange` | Range(1,180) | 90 | フレーム全体をマップする角度幅 (度) |
| `_LenticularFrameOffset` | Float | 0 | 基準フレームのオフセット |
| `_LenticularStereoMode` | Enum(PerEye,0,StereoCenter,1) | 1 | VR 角度計算 (既定は両眼中央=ちらつき防止) |
| `_LenticularEmission` | Range(0,10) | 0 | 出現フレームの発光ブースト (HDR) |
| `_LenticularNormalInfluence` | Range(0,1) | 0 | 法線によるフレーム選択スキュー (簡易・幾何法線ベース) |
| `_LenticularBlend` | Range(0,1) | 1 | ベースカラーへの混合量 |
| `_LenticularScanScale` | Range(1,200) | 40 | ScanBlend の縞スケール |
| `_LenticularSoftness` | Range(0.001,1) | 0.25 | フレーム境界の補間幅 |
| `_LenticularMask` | 2D (R, NOSAMPLER) | white | マスク |

**動作モード**
- SmoothBlend: 隣接フレームを `frac` + Softness で補間
- HardStep: `round` で瞬時に切り替え
- ScanBlend: UV 縞 (`_LenticularScanScale`) で隣接フレームを混在
- FrontReveal / SideReveal: 正面付近 / 斜め付近のみ別フレームを出現 (Emission ブースト連動)
- Flip: 視線の左右で A/B を切り替え (Softness で中央をクロスフェード)

**VR 向け**: StereoCenter (既定) は左右目の視点を平均し、両眼で同一フレームを選択します。
Softness を十分に持たせるとフレーム境界のちらつきをさらに抑えられます。

**ヘルパー** (`Include/Effects/NataneToonLenticular.hlsl`)
`float3 NataneLenticularCameraPos(float stereoMode)` (USING_STEREO_MATRICES 下で両眼平均) /
`float NataneLenticularAngle(float3 vdObj, float axis)` / `float2 NataneLenticularFrameUV(float2 uv, float frame, float frames, float dir)`

---

## B. サーフェス・コースティクス (`_CAUSTICS`)

キャラクターや小物の表面を、複雑な光模様が流れる表現です。ForwardBase のみで描画します。

| プロパティ | キーワード / 型 | 既定 | 説明 |
|---|---|---|---|
| `_Caustics` | `[Toggle(_CAUSTICS)]` | 0 | 有効化 |
| `_CausticsPatternMode` | Enum(Procedural,0,Texture,1) | 0 | 生成方式 |
| `_CausticsTex` | 2D (NOSAMPLER) | black | Texture モード用 (2 枚スクロール乗算) |
| `_CausticsSpace` | Enum(UV,0,Object,1,World,2,TriplanarLite,3) | 2 | 座標空間 (Triplanar-lite は主要軸平面) |
| `_CausticsComposite` | Enum(EmissionAdd,0,BaseMultiply,1,LitOnly,2,ShadowOnly,3) | 0 | 合成方式 |
| `_CausticsColor` | `[HDR]` Color | (0.6,0.9,1,1) | 光模様の色 |
| `_CausticsIntensity` | Range(0,10) | 1 | 強度 |
| `_CausticsScale` | Range(0.1,20) | 4 | 模様のスケール |
| `_CausticsSpeed` | Float | 0.5 | 移動速度 |
| `_CausticsDirection` | Vector | (1,0.5,0,0) | 移動方向 (XY) |
| `_CausticsDistortion` | Range(0,1) | 0.2 | 歪み量 (sin ワープ) |
| `_CausticsContrast` | Range(0.1,8) | 2 | コントラスト (pow) |
| `_CausticsMask` | 2D (R, NOSAMPLER) | white | マスク |

**パターン**: Procedural は単一 3×3 セルの Voronoi F1 + sin ワープ (軽量、多重オクターブなし)。
Texture は 2 枚のスクロールサンプルを乗算。

**合成方式**: EmissionAdd (`SafeAdditiveBlend` + HDR は `nataneHdrEmission` へ) / BaseMultiply (明部ブースト) /
LitOnly (`shadingValue` でスケール) / ShadowOnly (`1 - shadingValue`)。

**Quest**: `_QUEST_LITE` 時は 3×3 セル走査を行わず、交差 sin フィールドの軽量パターン (`NataneCausticsPatternLite`) を強制します。

**ヘルパー** (`Include/Effects/NataneToonCaustics.hlsl`)
`float NataneCausticsPattern(float2 uv, float time, float distortion)` /
`float NataneCausticsPatternLite(...)` / `float2 NataneCausticsCoord(float space, ...)`

---

## C. ピクセルアート化 (`_PIXEL_ART`) — スコープ縮小版

UV のピクセル化、ライティング / 影の量子化、パレット、ディザを実装します。

> **意図的な非対応**: Screen Pixelation と Object-Stable Pixelation はレビュー判断により実装しません。
> スクリーン空間スナップは VR で両眼にピクセル継ぎ目が出て安全でなく、Object-Stable 再投影は高コストのためです。
> ミラー安全なテクスチャ空間 + 色空間のサブセットのみを提供します
> (詳細は `Include/Effects/NataneToonPixelArt.hlsl` のコメント参照)。

| プロパティ | キーワード / 型 | 既定 | 説明 |
|---|---|---|---|
| `_PixelArt` | `[Toggle(_PIXEL_ART)]` | 0 | 有効化 |
| `_PixelArtSize` | Range(4,512) | 64 | 仮想解像度 (UV スナップ) |
| `_PixelLightSteps` | Range(2,32) | 6 | ライティング / 色の量子化段階数 |
| `_PixelPalette` | `[Toggle]` | 0 | パレット LUT を使用 |
| `_PixelPaletteTex` | 2D (NOSAMPLER) | white | 横方向パレット LUT (輝度でサンプル) |
| `_PixelDither` | Range(0,1) | 0 | Bayer ディザ量 |
| `_PixelArtMask` | 2D (R, NOSAMPLER) | white | マスク |

**適用箇所**
- UV ピクセル化: UV アニメーション確定直後に `mainUV` のみをスナップ (`floor(uv*res)+0.5)/res`)。
  他の UV 駆動機能はネイティブ解像度を維持します。
- 色 / ライト量子化: ライティング後 (FinalColorBlending 直後) に `col.rgb` をポスタライズ。
  ディザは floor 前に加算してバンディングを緩和。
  パレット LUT は輝度で置換し、**ForwardBase のみ** (加算パスでベースの見た目をパレット色で置換しないため)。

**ヘルパー** (`Include/Effects/NataneToonPixelArt.hlsl`)
`float2 NatanePixelSnapUV(float2 uv, float res)` / `half3 NatanePixelPosterize(half3 c, float steps)`
(ディザは既存の `NataneBayerThreshold4x4` を利用)

---

## 完了条件

- OFF 時ゼロ変化 (キーワードガード) / Opaque・Cutout・Transparent 正常
- マスクで適用範囲制御 / 追加サンプラーなし (NOSAMPLER 共有)
- FORWARD_ADD で頂点デシンクなし (3 機能ともフラグメント専用、頂点位置は不変)
- fwidth ベースのトゥーン AA と共存
- レンチキュラーは StereoCenter で両眼フレーム一致 (VR ちらつき防止)
- コースティクスは `_QUEST_LITE` で軽量パスへ自動切替

検証: Unity 2022.3.28f1 バッチモードにて `ShaderData.Pass.CompileVariant` (リフレクション) で
メインシェーダー + 全 10 バリアントの FORWARD_BASE (`_LENTICULAR,_CAUSTICS,_PIXEL_ART`)、
ストレス併用 (`+_LINE_BOIL,_TOPOGRAPHIC,_EMISSION,_AUDIOLINK,_QUEST_LITE`)、FORWARD_ADD を強制コンパイルし、
**エラー 0 / 警告 0 / CS 問題 0** を確認済み。
