# 表現系エフェクト バッチ1 (v1.6.0)

Natane Toon Shader v1.6.0 で追加された 4 つの表現系機能のシェーダー仕様です。
いずれもキーワード (`shader_feature_local`) で切り替わり、**OFF 時は描画結果に一切変化を与えません** (ゼロコスト)。
Opaque / Cutout / Transparent / Lite / Fur / Background / Ghost / ScreenEdgeSplit の全バリアントに対応しています
(Particle バリアントは対象外)。

対象パス:
- `FORWARD_BASE` / `FORWARD_ADD`: 4 機能すべて
- `OUTLINE`: `_LINE_BOIL` と `_FX_MODULATOR` (アウトライン幅ターゲット用) のみ

マスクはすべて NOSAMPLER 共有サンプラーで追加サンプラーを消費しません。fwidth ベースのトゥーン段差 AA とも共存します。

---

## A. ラインボイル (`_LINE_BOIL`)

手描きアニメの「線のうねり (boil)」を時間量子化ジッターで再現します。
`floor(_Time.y * FPS / HoldFrames)` を位相シードにするため、指定フレーム数だけジッターが固定され、
2 コマ・3 コマ打ちのようなアニメ的な揺れになります。

| プロパティ | キーワード / 型 | 既定 | 説明 |
|---|---|---|---|
| `_LineBoil` | `[Toggle(_LINE_BOIL)]` | 0 | 機能の有効化 |
| `_LineBoilFPS` | Range(1,24) | 8 | ボイルの更新フレームレート |
| `_LineBoilPositionJitter` | Range(0,5) | 1 | アウトライン頂点の位置ジッター量 (×0.001 オブジェクト空間) |
| `_LineBoilWidthJitter` | Range(0,1) | 0.2 | アウトライン幅の乱れ量 |
| `_LineBoilUVJitter` | Range(0,0.05) | 0.005 | フラグメント側 UV ジッター量 |
| `_LineBoilHoldFrames` | Range(1,8) | 1 | 位相を固定するコマ数 (2 = 2コマ打ち) |
| `_LineBoilRandomSeed` | Float | 0 | 位相シードのオフセット |
| `_LineBoilAffectOutline` | `[Toggle]` | 1 | アウトライン (位置/幅) に適用 |
| `_LineBoilAffectHatching` | `[Toggle]` | 1 | ハッチング / スクリーントーン / 影エッジノイズの UV に適用 |
| `_LineBoilAffectWatercolor` | `[Toggle]` | 1 | 水彩グレインの UV に適用 |
| `_LineBoilMaskTex` | 2D (R, NOSAMPLER) | white | R チャンネルでボイル強度をマスク |

**適用箇所**
- アウトラインパス: 投影前のオブジェクト空間位置ジッター + 幅乗算ジッター (`_OUTLINE_HAND_DRAWN` の作法に準拠)
- フラグメント: `_SCREEN_TONE` / `_HATCHING` / `_SHADOW_EDGE_NOISE` は `_LineBoilAffectHatching`、`_WATERCOLOR` は `_LineBoilAffectWatercolor` で個別に UV をオフセット

**ヘルパー** (`Include/Effects/NataneToonLineBoil.hlsl`)
`float3 NataneLineBoilOffset(float3 seedPos, float phase, float amount)` — ハッシュベース (既存の GetHandDrawnJitter と同一規約)

### プリセット値 (ラインボイル)

| プリセット | FPS | HoldFrames | PositionJitter | WidthJitter | UVJitter |
|---|---|---|---|---|---|
| Clean Anime (クリーンアニメ) | 12 | 2 | 0.4 | 0.10 | 0.002 |
| Pencil (鉛筆) | 8 | 2 | 1.0 | 0.25 | 0.006 |
| Rough Sketch (ラフスケッチ) | 6 | 1 | 2.5 | 0.50 | 0.012 |
| Stop Motion (ストップモーション) | 4 | 3 | 1.5 | 0.30 | 0.008 |

---

## B. 形状付きトゥーンハイライト (`_SHAPED_HIGHLIGHT`)

丸いスペキュラの代わりに、円/リング/十字/星/ハート/ダイヤ/三日月/ライン等の
プロシージャル SDF 形状 (またはカスタム SDF テクスチャ) をハイライトとして加算します。
座標はハーフベクトルと法線のビュー空間偏差 (スペキュラピーク中心) から構築し、既存スペキュラと整合します。

| プロパティ | キーワード / 型 | 既定 | 説明 |
|---|---|---|---|
| `_ShapedHighlight` | `[Toggle(_SHAPED_HIGHLIGHT)]` | 0 | 有効化 |
| `_ShapedHLShape` | Enum(0-8) | 3 (Star) | Circle/Ring/Cross/Star/Heart/Diamond/Crescent/Line/Custom |
| `_ShapedHLColor` | `[HDR]` Color | (1,1,1,1) | ハイライト色 (HDR) |
| `_ShapedHLIntensity` | Range(0,10) | 1 | 強度 |
| `_ShapedHLSize` | Range(0.01,2) | 0.5 | サイズ |
| `_ShapedHLSoftness` | Range(0.001,1) | 0.1 | エッジのソフトさ |
| `_ShapedHLStretch` | Vector | (1,1,0,0) | XY 方向の伸縮 |
| `_ShapedHLRotation` | Range(0,360) | 0 | 回転 (度) |
| `_ShapedHLLightFollow` | Range(0,1) | 1 | ライト方向への追従 (回転) |
| `_ShapedHLCameraFollow` | Range(0,1) | 0 | カメラ向きへの追従 (回転) |
| `_ShapedHLShape2` | Enum(0-7) | 0 | セカンダリ形状 |
| `_ShapedHLIntensity2` | Range(0,10) | 0 | セカンダリ強度 (0 で無効) |
| `_ShapedHLSize2` | Range(0.01,2) | 0.3 | セカンダリサイズ |
| `_ShapedHLSparkleSpeed` | Range(0,20) | 0 | スパークル明滅速度 |
| `_ShapedHLTex` | 2D (NOSAMPLER) | black | カスタム SDF テクスチャ (Shape=Custom 時) |
| `_ShapedHLMask` | 2D (R, NOSAMPLER) | white | マスク |

**適用箇所**: `_SPECULAR` ブロック直後に `SafeAdditiveBlend` で加算 (白飛び防止)。`_DISTANCE_FADE` にも対応。
FORWARD_ADD では `_AdditionalLightIntensity` でスケール、`_Glossiness`・`specularOcclusion`・`ApplyMatteQuality` を適用。

**ヘルパー** (`Include/Effects/NataneToonShapedHighlight.hlsl`)
`half NataneShapeSDF(float2 p, float shapeMode)` (iq 系 2D SDF + Eye シェーダーの Star/Heart 手法を流用)

---

## C. 等高線 / 断層スライス (`_TOPOGRAPHIC`)

空間座標を等間隔でスライスし、等高線 / 断層バンドを Emission 対応色で重ねます。ForwardBase のみで動作します
(純粋な加算色のため ForwardAdd への影響はなく、`_EMISSION` と同じガード方針)。

| プロパティ | キーワード / 型 | 既定 | 説明 |
|---|---|---|---|
| `_Topographic` | `[Toggle(_TOPOGRAPHIC)]` | 0 | 有効化 |
| `_TopoSpace` | Enum(Object/World/View) | 1 (World) | 座標空間 |
| `_TopoAxis` | Enum(X/Y/Z/Custom) | 1 (Y) | 軸 |
| `_TopoCustomDir` | Vector | (0,1,0,0) | カスタム方向 (Axis=Custom) |
| `_TopoMode` | Enum(0-5) | 0 | Lines/Bands/GradientBands/DoubleLines/PulseRings/NoiseDistorted |
| `_TopoSpacing` | Range(0.001,2) | 0.1 | バンド間隔 |
| `_TopoOffset` | Float | 0 | オフセット |
| `_TopoSpeed` | Float | 0 | スクロール速度 |
| `_TopoLineWidth` | Range(0.001,0.5) | 0.1 | 等高線の太さ |
| `_TopoColor` | `[HDR]` Color | (1,1,1,1) | プライマリ色 |
| `_TopoColor2` | `[HDR]` Color | (0,0,0,1) | セカンダリ色 |
| `_TopoEmission` | Range(0,10) | 1 | Emission 強度 (HDR 分は Bloom へ) |
| `_TopoNoiseScale` | Range(0,10) | 1 | ノイズスケール |
| `_TopoNoiseStrength` | Range(0,1) | 0.3 | ノイズ歪み量 (NoiseDistorted で顕著) |
| `_TopoBlend` | Range(0,1) | 1 | ブレンド量 |
| `_TopoMask` | 2D (R, NOSAMPLER) | white | マスク |

**適用箇所**: Emission ステージ直後にオーバーレイ (`SafeAdditiveBlend`)。HDR オーバーシュートは `nataneHdrEmission` に加算され Bloom を駆動。

**ヘルパー** (`Include/Effects/NataneToonTopographic.hlsl`)
`float2 NataneTopoBand(float phase, float mode, float lineWidth, float time)` / `float NataneTopo_Noise(float2 p)`

---

## D. 汎用 FX モジュレーター (`_FX_MODULATOR`)

2 スロットの信号ソースを選択し、任意のターゲットパラメータを時間 / オーディオ / 幾何情報で変調します。
スロット値はフラグメント冒頭で一度だけ計算し、各ターゲット地点で乗算 / 加算します。

**ソース (Source)**: 0 Sine / 1 Saw / 2 Triangle / 3 Pulse / 4 RandomStep / 5-8 AudioLink Bass·LowMid·HighMid·Treble / 9 Chronotensity / 10 CameraDistance / 11 ViewAngle / 12 Manual / **13 StaticNoise / 14 DynamicNoise / 15 DynamicNoiseSteps**
(AudioLink 系は `_AUDIOLINK` 併用時のみ、非対応時は中立 0。Source 9 は既存の `SampleAudioLinkChronotensity` を利用。)

| ノイズソース | 内容 |
|---|---|
| 13 StaticNoise | 位置ハッシュのバリューノイズ（時間項なし）。フラグメント / 頂点ごとの空間的まだら。時間で動かない静的ノイズ。 |
| 14 DynamicNoise | 2 オクターブのバリューノイズを `_Time.y × Speed` でスクロール。滑らかにゆらめく動的ノイズ。 |
| 15 DynamicNoiseSteps | 時間を Speed 段でフロア量子化した動的ノイズ。カクついた明滅 / グリッチ感。 |

ノイズは自己完結ヘルパー (`NataneFXMod_ValueNoise`) を使用（OUTLINE パスに Utils が無いため Utils のハッシュは流用せず、`NataneToonFXModulator.hlsl` 内にローカル定義）。座標は Noise Space で選択（0 UV / 1 Object / 2 World）。OUTLINE パスでは頂点単位で `v.uv` と `v.vertex.xyz`（オブジェクト空間）を使用するため OutlineWidth ターゲットでも動作。

**ターゲット (Target)**: 0 None / 1 EmissionIntensity / 2 HueShift / 3 RimIntensity / 4 OutlineWidth / 5 LineBoilStrength / 6 TopographicOffset / **7 ShapedHighlightIntensity / 8 CausticsIntensity / 9 LenticularBlend / 10 SpecularIntensity / 11 MatCapIntensity / 12 AlphaFade**
(強度系は乗算、Hue / Offset 系は加算。OutlineWidth は OUTLINE パスでも動作。7-11 は対応機能キーワードが OFF のとき自動でノーオペ。**12 AlphaFade は `col.a` を乗算するため、Transparent / Fade / X-Ray など透過対応バリアントでのみ視認可能**。)

各スロット (`0`/`1`) のプロパティ:

| プロパティ | 型 | 既定 | 説明 |
|---|---|---|---|
| `_FXModulator` | `[Toggle(_FX_MODULATOR)]` | 0 | 有効化 |
| `_FXModSource{n}` | Enum(0-15) | 0 | 信号ソース |
| `_FXModTarget{n}` | Enum(0-12) | 0 | 適用先 |
| `_FXModAmount{n}` | Float | 0 | 変調量 (0 で中立) |
| `_FXModOffset{n}` | Float | 0 | 位相オフセット |
| `_FXModSpeed{n}` | Float | 1 | 速度 |
| `_FXModMin{n}` | Float | 0 | 出力レンジ最小 |
| `_FXModMax{n}` | Float | 1 | 出力レンジ最大 |
| `_FXModInvert{n}` | `[Toggle]` | 0 | 反転 |
| `_FXModCurve{n}` | Range(0.1,5) | 1 | カーブ (pow) |
| `_FXModManual{n}` | Range(0,1) | 0.5 | Manual ソース値 |
| `_FXModDistMin{n}` / `_FXModDistMax{n}` | Float | 0 / 10 | CameraDistance 正規化範囲 |
| `_FXModNoiseScale{n}` | Float | 5 | ノイズソース(13-15)のスケール |
| `_FXModNoiseSpace{n}` | Enum(0-2) | 0 | ノイズ座標空間 (0 UV / 1 Object / 2 World) |
| `_FXModMaskTex` | 2D (NOSAMPLER) | white | 共有マスク (R=スロット0, G=スロット1) |

**ヘルパー** (`Include/Effects/NataneToonFXModulator.hlsl`)
`half NataneFXMod_Source(...)` / `NataneFXModState NataneFXModCompute(...)` / `half NataneFXModMul(state, target)` / `half NataneFXModAdd(state, target)` / `float NataneFXMod_ValueNoise(float2)`

**レシピ例**:

| ソース | ターゲット | 効果 | 設定の勘所 |
|---|---|---|---|
| 13 StaticNoise | 1 EmissionIntensity | まだら発光（斑点状に光る面） | Noise Space=Object, Noise Scale=8, Amount=1, Min=0/Max=1。時間で動かない斑発光。 |
| 14 DynamicNoise | 3 RimIntensity | ゆらめくオーラ | Noise Space=World, Speed=0.5, Amount=1.5, Curve=2。リム光が有機的に脈動。 |
| 15 DynamicNoiseSteps | 12 AlphaFade | グリッチ明滅（透過が飛び飛びに消える） | Transparent バリアントで使用。Speed=8, Amount=-1, Min=0/Max=1 で `col.a` が段階的に明滅。 |

---

## 完了条件

- OFF 時ゼロ変化 (キーワードガード) / Opaque・Cutout・Transparent 正常
- マスク対応 / 追加サンプラーなし (NOSAMPLER 共有)
- FORWARD_ADD で頂点デシンクなし (ラインボイルはフラグメント UV とアウトラインパスのみ、頂点位置は不変)
- fwidth ベースのトゥーン AA と共存

検証: Unity 2022.3.28f1 バッチモードにて全 11 シェーダーの FORWARD_BASE (4 キーワード + 主要機能併用)・
FORWARD_ADD・OUTLINE ([_OUTLINE,_LINE_BOIL,_FX_MODULATOR]) を強制コンパイルし、エラー 0 / 警告 0 を確認済み。
