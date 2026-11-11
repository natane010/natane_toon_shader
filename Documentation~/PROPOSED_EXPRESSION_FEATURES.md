# 新規表現機能 仕様案

## 概要

Natane Toon Shaderへ追加を検討する、以下8種類の表現機能をまとめた仕様案です。

1. ラインボイル／コマ打ち
2. レンチキュラー／角度依存テクスチャ
3. 汎用FXモジュレーター
4. 形状付きトゥーンハイライト
5. サーフェス・コースティクス
6. 等高線／断層スライス
7. ピクセルアート化
8. 遮蔽シルエット／X-Ray

既存のHand-drawn Outline、Iridescence、Hatching、Watercolor、Color Quantize、AudioLinkなどと役割が重複しないようにしつつ、組み合わせによって新しいルックを作れる構成を目指します。

## 機能一覧

| 機能 | 表現の中心 | 主な用途 | 追加コストの目安 |
|---|---|---|---|
| ラインボイル | 時間で線や模様が揺れる | 手描きアニメ、鉛筆画 | 低 |
| レンチキュラー | 見る角度で絵柄が変わる | 表情変化、ホログラムカード | 中、Texture 1枚 |
| 汎用FXモジュレーター | 時間・音・距離で他機能を動かす | ライブ演出、覚醒、脈動 | 低～中 |
| 形状付きハイライト | 反射光を星やハート形にする | 瞳、髪、宝石、ネイル | 低 |
| サーフェス・コースティクス | 表面を複雑な光が流れる | 水中、魔法、木漏れ日 | 中 |
| 等高線／断層スライス | 表面に周期的な線や帯を出す | SFスキャン、エネルギー線 | 低 |
| ピクセルアート化 | 空間と色をドット単位に丸める | レトロゲーム、2.5D | 中 |
| 遮蔽シルエット／X-Ray | 隠れた部分だけ表示する | 索敵、幽霊、演者表示 | 高、追加Pass |

---

## 1. ラインボイル／コマ打ち

### 表現概要

輪郭線やハッチングが数フレームごとに少しずつ変化し、手描きアニメのように線が震える表現です。

既存のHand-drawn Outlineが作る静的な形状変化に時間方向の変化を加えます。時間を6～24fps程度に量子化し、そのタイミングでノイズSeedを切り替えることで、滑らかなノイズアニメーションではなく、作画した線がコマごとに置き換わる印象を作ります。

### 表現できるもの

- 鉛筆で描いた輪郭線
- アニメーション原画風の線揺れ
- 絵本、クレヨン、木炭画
- ハッチングが微妙に動く漫画表現
- 水彩紙の粒子がコマごとに変わる表現
- AudioLinkに反応して線が大きく暴れる表現

### 適用対象

- Outline位置
- Outline太さ
- Hatching UV
- Watercolor Grain
- Shadow Edge Noise
- Main Textureの微小なUV揺れ

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| Enable Line Boil | 機能の有効化 |
| Animation FPS | ノイズパターンを更新する頻度 |
| Position Jitter | 線の位置変化量 |
| Width Jitter | 線の太さ変化量 |
| UV Jitter | テクスチャやハッチングのUV変化量 |
| Hold Frames | 同じ形状を保持するフレーム数 |
| Random Seed | マテリアルごとの揺れ方 |
| Affect Outline | Outlineへ適用 |
| Affect Hatching | Hatchingへ適用 |
| Affect Watercolor | Watercolorへ適用 |
| Mask | 適用範囲 |

### プリセット案

- `Clean Anime`：12fps、弱い位置揺れ
- `Pencil`：8fps、位置と太さを中程度に変化
- `Rough Sketch`：6fps、大きく不規則に変化
- `Stop Motion`：4～6fps、テクスチャも一緒に揺らす

---

## 2. レンチキュラー／角度依存テクスチャ

### 表現概要

見る角度によって異なる画像や模様へ切り替わる表現です。

Iridescenceが角度によって色を変化させるのに対し、レンチキュラーでは絵柄そのものを切り替えます。複数画像は横並びまたはグリッド状の1枚のアトラスに格納し、視線角度から表示フレームを決定します。

### 表現できるもの

- 左右から見ると表情が変わる
- 正面では通常、横から見ると別の顔が現れる
- ロゴと文字が切り替わる
- カードのイラストが動く
- 衣装の模様が角度で変形する
- 瞳がNormal、Heart、Starへ変わる
- 角度によって傷、魔法紋、発光模様が現れる
- ホログラムカードや特殊印刷風の素材

### 動作モード

| モード | 内容 |
|---|---|
| Smooth Blend | 隣接フレームを滑らかに補間 |
| Hard Step | 角度ごとに瞬時に切り替え |
| Scan Blend | 細い縞を使って複数画像を混在 |
| Front Reveal | 正面だけ別画像を表示 |
| Side Reveal | 斜めから見たときだけ表示 |
| Flip | 左右で画像A／Bを切り替え |

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| Lenticular Atlas | 複数フレームを格納したアトラス |
| Frame Count | フレーム数 |
| View Axis | 角度判定に使用する軸 |
| Angle Range | 切り替えに使用する角度範囲 |
| Transition Softness | フレーム間の補間幅 |
| Frame Offset | 基準フレームのオフセット |
| Direction | Horizontal／Vertical |
| Stereo Mode | VR時の角度計算方法 |
| Emission Strength | 切り替え画像の発光強度 |
| Normal Influence | Normalへの反映量 |
| Mask | 適用範囲 |

### VR向け注意点

左右の目で別フレームが選択されるとちらついて見える可能性があります。ステレオ中央視点を使うモード、またはフレーム境界へ十分なSoftnessを持たせるモードを用意します。

---

## 3. 汎用FXモジュレーター

### 表現概要

時間、音、距離、視線角度などの値を使い、ほかのShader機能を自動的に動かす制御システムです。

この機能自体は色や模様を描画せず、`Source`から取得した値を`Target`へ適用します。2～4スロット程度を用意し、複数の変化を組み合わせられる構成とします。

### Source候補

- Sine Wave
- Saw Wave
- Triangle Wave
- Pulse
- Random Step
- AudioLink Bass
- AudioLink Low Mid
- AudioLink High Mid
- AudioLink Treble
- Chronotensity
- Camera Distance
- View Angle
- Light Direction
- Object Height
- World Height
- Manual Control

### Target候補

- Hue Shift
- Emission
- Rim Intensity
- Outline Width
- Shadow Border
- Dissolve
- Glitch
- Line Boil Strength
- Lenticular Frame
- Caustics Intensity
- Topographic Offset
- Pixel Size
- X-Ray Pulse
- Decal Rotation
- Refraction Strength

### 1スロットの基本パラメータ

| パラメータ | 内容 |
|---|---|
| Source | 入力値の種類 |
| Target | 変化させる機能 |
| Amount | 変化量 |
| Offset | 入力値のオフセット |
| Speed | 時間入力の速度 |
| Min／Max | 出力範囲 |
| Invert | 入力値の反転 |
| Curve | 変化カーブ |
| Mask | 適用範囲 |

### 表現例

- 音に合わせて模様が切り替わる
- 心拍のようにEmissionが脈動する
- カメラが近づくと覚醒する
- 横から見たときだけGlitchする
- 一定時間ごとに瞳の絵柄が切り替わる
- Bassに合わせて等高線が上へ流れる

---

## 4. 形状付きトゥーンハイライト

### 表現概要

通常の丸いSpecularを、星、十字、ハート、線、リングなどの形状に変える機能です。

物理的な反射よりも、アニメやイラストで描き込まれる記号的なハイライトを再現します。Circle、Cross、Star、Ringなどは数式で生成し、複雑な形状のみSDFアトラスを使用します。

### 表現できるもの

- 瞳の星形ハイライト
- 宝石の十字反射
- 髪の細長いハイライト
- ハート型の光
- 金属の鋭い光条
- 液体の丸い反射
- 魔法アイテムの発光記号
- 一瞬だけ現れる漫画的な光

### 形状モード

- Circle
- Ring
- Cross
- Star
- Heart
- Diamond
- Crescent
- Line
- Custom SDF

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| Shape | ハイライト形状 |
| Color | 色 |
| Intensity | 強度 |
| Size | 大きさ |
| Softness | エッジの柔らかさ |
| Stretch X／Y | 縦横の伸縮 |
| Rotation | 回転 |
| Light Follow | ライト方向への追従量 |
| Camera Follow | カメラへの追従量 |
| Secondary Shape | 2つ目の形状 |
| Sparkle Pulse | 点滅、脈動 |
| Mask | 適用範囲 |

---

## 5. サーフェス・コースティクス

### 表現概要

キャラクターや小物の表面を、複雑な光模様が流れていく表現です。

Eye Shaderの虹彩コースティクスを、全身、衣装、小物、背景向けに一般化します。簡易モードはVoronoiやSin波を組み合わせて生成し、高品質モードのみテクスチャを使用します。

### 表現できるもの

- 水中にいるキャラクター
- 水面から反射した揺れる光
- 魔力が皮膚や衣装を流れる表現
- ステンドグラスから差し込む色光
- 木漏れ日
- 炎やオーロラによる光の揺らぎ
- 結晶内部を移動する光
- ライブステージの投影ライト

### 座標モード

- UV
- Object Space
- World Space
- Triplanar
- Light Projection
- Screen Space

### 合成先

- Base Color
- Emission
- Specular
- Normal Distortion
- Shadow Color
- Lit Area Only
- Shadow Area Only

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| Pattern Mode | Procedural／Texture |
| Pattern Texture | 高品質モード用テクスチャ |
| Color | 光模様の色 |
| Intensity | 強度 |
| Scale | 模様の大きさ |
| Speed | 移動速度 |
| Direction | 移動方向 |
| Distortion | 模様の歪み |
| Contrast | 模様のコントラスト |
| Light Direction Influence | ライト方向への追従量 |
| Emission Strength | 発光量 |
| Mask | 適用範囲 |

---

## 6. 等高線／断層スライス

### 表現概要

Object SpaceやWorld Spaceの座標を一定間隔で区切り、表面に周期的な線や帯を表示する機能です。

高さグラデーションとは異なり、複数の線や帯を繰り返し生成できます。テクスチャを使わずに実装でき、AudioLinkやFXモジュレーターとの組み合わせに向いています。

### 表現できるもの

- 地形図の等高線
- SFスキャンライン
- 身体を上昇するエネルギー
- 3Dプリンターの積層線
- 地層や木目
- ホログラムの断層
- キャラクターを切断するような断面表示
- 魔法陣から上昇する光のリング
- 上から下へ解析されるスキャン

### パターンモード

| モード | 内容 |
|---|---|
| Lines | 細い線 |
| Bands | 太い帯 |
| Gradient Bands | 段階的な色 |
| Double Lines | 二重線 |
| Pulse Rings | 移動するリング |
| Noise Distorted | ノイズで歪んだ線 |

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| Coordinate Space | Object／World／View |
| Axis | X／Y／Z／Custom Direction |
| Spacing | 線の間隔 |
| Width | 線の太さ |
| Offset | 線の位置 |
| Speed | 移動速度 |
| Rotation | 軸の回転 |
| Noise Amount | 歪み量 |
| Noise Scale | 歪みの大きさ |
| Primary Color | 主色 |
| Secondary Color | 補助色 |
| Emission | 発光量 |
| Mask | 適用範囲 |

---

## 7. ピクセルアート化

### 表現概要

テクスチャUV、ライティング、影、アウトラインをピクセル単位に丸め、3Dモデルをドット絵風にする機能です。

Color Quantizeが色数だけを減らすのに対し、ピクセルアート化では空間的な解像度も落とします。Object Stableモードでは、カメラ移動時にピクセル模様が表面上で泳がないようにします。

### 表現できるもの

- 3Dキャラクターのドット絵化
- PS1／レトロ3D風
- 2Dゲームのスプライト風
- 低解像度ホラー
- ボクセルゲーム風
- 一部分だけデジタル化する演出
- ダメージ時にピクセル崩壊する表現
- 現実と仮想空間を切り替える表現

### 適用対象

- Main Texture UV
- Normal
- Lighting
- Shadow
- Specular
- Rim
- Screen Position
- Outline Width
- Alpha／Dissolve

### 動作モード

- UV Pixelation
- Screen Pixelation
- Object Stable Pixelation
- Pixel Lighting
- Pixel Shadow
- Pixel Dissolve

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| Pixel Size | ピクセルの大きさ |
| Resolution | 仮想解像度 |
| Coordinate Mode | UV／Screen／Object Stable |
| UV Snap | UVの量子化強度 |
| Lighting Steps | ライティング段階数 |
| Shadow Steps | 影の段階数 |
| Normal Snap | 法線の量子化強度 |
| Pixel Jitter | ピクセル位置の揺れ |
| Palette Texture | 使用色を制限するパレット |
| Dither | 色補間用ディザ |
| Mask | 適用範囲 |

---

## 8. 遮蔽シルエット／X-Ray

### 表現概要

壁やほかのオブジェクトに隠れている部分だけ、別の色や輪郭で表示する機能です。

通常部分は既存のForward Passで描画し、遮蔽部分は`ZTest Greater`などを使用する専用Passで描画します。追加Passが必要になるため、通常Shaderへ常設せず、X-Ray専用バリアントとして扱います。

### 表現できるもの

- 壁越しのキャラクター輪郭
- ゲームの索敵表示
- 味方、敵の識別
- 幽霊や霊体
- ステージ上で隠れた演者を表示
- 回収可能アイテムの強調
- スキャンされた身体内部
- 遮蔽部分だけのホログラム
- 隠れた部分を点線で描く漫画表現

### 表示モード

- Outline Only
- Solid Fill
- Dither Fill
- Scanline
- Fresnel
- Wireframe
- Pulse
- Depth Gradient

### 基本パラメータ

| パラメータ | 内容 |
|---|---|
| X-Ray Color | 遮蔽部分の色 |
| Occluded Alpha | 遮蔽部分の不透明度 |
| Outline Width | 遮蔽輪郭の太さ |
| Depth Fade | 遮蔽物との距離による減衰 |
| Pattern Mode | 表示パターン |
| Scanline Scale | スキャンライン間隔 |
| Scanline Speed | スキャンライン速度 |
| Fresnel Power | 輪郭強度 |
| Pulse Speed | 点滅速度 |
| Stencil Ref | Stencil制御値 |
| Mask | 適用範囲 |

---

## 組み合わせ例

### 手描きアニメ

- Line Boil
- Hatching
- Watercolor
- Hand-drawn Outline
- Color Quantize

輪郭、影、紙目が同じ低fpsで変化する、統一感のある手描きアニメを作ります。

### ホログラムカード

- Lenticular
- Iridescence
- Shaped Highlight
- Caustics
- Emission

見る角度で絵柄が変わり、表面に虹色と星型の光が走る素材を作ります。

### サイバースキャン

- Topographic Lines
- X-Ray
- Glitch
- Hologram
- FX Modulator

スキャン線が身体を上昇し、壁に隠れた部分だけ解析表示される表現を作ります。

### 魔法覚醒

- Caustics
- Topographic Rings
- Shaped Highlight
- Emission
- AudioLink／Chronotensity

魔法模様が身体を流れ、瞳やアクセサリーへ星型の反射が現れる表現を作ります。

### レトロゲーム

- Pixel Art
- Perspective Flatten
- Color Quantize
- Dither
- Pixel Outline

3Dモデルを低解像度の2Dスプライトに近い見た目へ変化させます。

---

## インスペクター構成案

```text
新規表現 New Expressions
├─ アニメーション
│  └─ ラインボイル
├─ 視線・角度
│  └─ レンチキュラー
├─ ライティング
│  ├─ 形状付きハイライト
│  └─ サーフェス・コースティクス
├─ スタイライズ
│  ├─ 等高線／断層スライス
│  └─ ピクセルアート
├─ 制御
│  └─ FXモジュレーター
└─ 高度な描画
   └─ X-Ray
```

各機能は、最初に以下だけを表示する簡易UIとします。

- Enable
- Mode
- Intensity
- ScaleまたはSize
- Speed
- Mask

詳細な座標、ブレンド、ノイズ、Stereo設定などは`Advanced`内へ格納します。

---

## 推奨するShader構成

すべての機能を製品へ収録しつつ、1つのShaderへ過剰なキーワードやPassを集中させない構成とします。

| Shader構成 | 収録する新機能 |
|---|---|
| Toon Core | ラインボイル、モジュレーター、形状ハイライト、等高線 |
| Toon Stylized | レンチキュラー、コースティクス、ピクセルアート |
| Toon X-Ray | 遮蔽シルエット専用Pass |
| Toon Lite | 追加サンプラー不要な機能のみ |

### Coreへ入れる機能

以下は原則として追加テクスチャを使用せず、Uniform分岐で実装します。

- ラインボイル
- 汎用FXモジュレーター
- プロシージャル形状ハイライト
- 等高線／断層スライス
- 簡易コースティクス

### Stylizedへ分離する機能

以下はテクスチャ、画面座標、複数サンプルなどを使用する可能性があるため、必要に応じてStylizedバリアントへ分離します。

- レンチキュラー
- Texture Caustics
- Screen Pixelation
- Custom SDF Highlight

### 専用バリアントにする機能

- X-Ray

X-Rayは追加Passと深度テストを必要とするため、通常Shaderには常設しません。

---

## 実装順序案

1. ラインボイル
2. 等高線／断層スライス
3. 形状付きハイライト
4. 汎用FXモジュレーター
5. レンチキュラー
6. サーフェス・コースティクス
7. ピクセルアート化
8. X-Ray専用バリアント

最初に追加サンプラーを必要としない機能を実装し、共通座標、マスク、モジュレーターの仕組みを固めてから、テクスチャや追加Passを必要とする機能へ進みます。

## 完了条件

各機能について、最低限以下を満たすことを完了条件とします。

- 機能OFF時に既存マテリアルの見た目が変わらない
- Inspectorから機能を有効化、調整できる
- EN／JP表示に対応している
- Maskで適用範囲を制御できる
- Opaqueで意図した表示になる
- 対応対象外のShaderバリアントではUIを表示しない
- Sampler Budgetへコストが反映される
- Material Validatorで危険な組み合わせを検出できる
- プリセットまたはサンプル設定で表現を確認できる
- 対応Unityバージョンと対象Graphics APIでShaderコンパイルエラーがない
