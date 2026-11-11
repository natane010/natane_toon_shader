# P5: ハッチングTAM / 水彩素材ジェネレーター 実装仕様

`_HATCHING` と `_WATERCOLOR` が要求するテクスチャを手元で生成できるようにする。

- 新規ツール: `Editor/NataneToon/Tools/HatchingToneGenerator.cs`
- Stage: A（シェーダー変更なし）

---

## 1. 動機と調査結果

### 1.1 テクスチャを必要とする機能の既定値

| プロパティ | 既定値 | 既定のままの挙動 |
|---|---|---|
| `_HatchTex0` ("Hatch Texture 0 (RGBA=L1-4)") | `"white"` | ハッチングが**一切出ない** |
| `_HatchTex1` ("Hatch Texture 1 (RG=L5-6)") | `"white"` | 同上 |
| `_WCGranulationTex` | `"gray"` | 粒状感なし |
| `_WCPaperTex` | `"white"` | 紙目なし |

`_HATCHING` を ON にしても、ユーザーが自前で6段階のTAMを用意しない限り**何も起きない**。
リポジトリ内の生成ツールは `DissolvePatternGenerator`（ノイズのみ）だけで、
ハッチング・紙目・粒状感を作る手段が存在しない。

### 1.2 対象外にするもの

`_SCREEN_TONE` と `_HALFTONE_SHADOW` は**プロシージャル実装**で、
`_ScreenToneScale` / `_ScreenToneThreshold` / `_HalftoneShadowScale` 等のパラメータのみで完結する
（パターンテクスチャを取らない。取るのは `_ScreenToneMask` = 適用範囲マスクのみ）。
したがって網点テクスチャの生成は不要。本ツールの対象は **ハッチングTAM と 水彩素材** に絞る。

適用範囲マスクの作成は既存の `NataneMaskPainter` の担当領域であり、本ツールでは扱わない。

### 1.3 TAM のパッキング仕様（`NataneToonUtils.hlsl:1625-1645`）

```hlsl
half4 h0 = SAMPLE(hatchTex0, uv * tiling);   // RGBA = levels 1-4
half2 h1 = SAMPLE(hatchTex1, uv * tiling).rg; // RG   = levels 5-6

half lum = shadingValue * 6.0;
// w0..w5 で6段を線形補間し、
hatchValue = w1*h0.r + w2*h0.g + w3*h0.b + w4*h0.a + w5*h1.r + max(0, 1-lum)*h1.g;
return lerp(baseColor, baseColor * hatchColor.rgb, hatchValue * maskValue * blend);
```

- **値が大きい = 濃くハッチングが乗る**（`hatchValue` が `hatchColor` への lerp 係数）
- したがって `h0.r`（level 1 = 最も明るい段）が最も**疎**、`h1.g`（最暗）が最も**密**
- 6枚は独立ではなく、**明るい段の線が暗い段にも必ず含まれる**（累積的）必要がある。
  そうでないと段が切り替わる瞬間に線が入れ替わってちらつく（TAM の基本要件）

---

## 2. 仕様

### 2.1 生成物

| 出力 | 内容 | フォーマット |
|---|---|---|
| `<name>_HatchTex0.png` | R=L1, G=L2, B=L3, A=L4 | RGBA32, Linear, sRGBオフ, Repeat |
| `<name>_HatchTex1.png` | R=L5, G=L6, B/A=未使用(0) | RGBA32, Linear, sRGBオフ, Repeat |
| `<name>_Granulation.png` | 粒状ノイズ（グレースケール、平均0.5） | RGBA32 または R8, Linear, Repeat |
| `<name>_Paper.png` | 紙目（グレースケール、平均0.5） | RGBA32 または R8, Linear, Repeat |

解像度は 256 / 512 / 1024（既定 512）。

### 2.2 ハッチングTAM の生成パラメータ

| パラメータ | 既定 | 内容 |
|---|---|---|
| Resolution | 512 | 出力解像度 |
| Stroke Angle | 45° | 線の基準角度 |
| Cross Hatch Angle | 90° | 段が進んだときに追加する交差角（0で交差なし） |
| Cross Hatch From Level | 4 | この段から交差線を追加 |
| Line Width | 1.5 px | 線の太さ |
| Line Softness | 0.5 | 線のエッジのぼけ |
| Level 1 Density | 4 | 最も明るい段の線本数（テクスチャ幅あたり） |
| Density Curve | 1.6 | 段が進むごとの線本数の倍率（累積の伸び方） |
| Jitter | 0.15 | 線位置・角度の揺らぎ（手描き感） |
| Taper | 0.3 | 線端の細り |
| Seed | 0 | 乱数シード |
| Mip Consistency | ON | §2.4 のミップ処理を有効化 |

### 2.3 累積生成アルゴリズム

```
lines = []                       // 全段で共有する線リスト
for level in 1..6:
    target = round(Level1Density * DensityCurve^(level-1))
    while lines.count < target:
        追加する線を生成:
            angle  = StrokeAngle + jitter
            if level >= CrossHatchFromLevel かつ 追加本数が偶数番目:
                angle += CrossHatchAngle
            offset = 既存の線から最も離れた位置（Mitchell's best-candidate で8候補から選択）
        lines.append(line)
    levelBuffer[level] = rasterize(lines)     // ここまでの全線を描く = 累積
```

- 各段は「前の段の線を全部含む」ので、段の切り替えでちらつかない
- 線の配置は best-candidate サンプリングで、規則的な縞にも完全ランダムにもならない中間を狙う
- ラスタライズは線分の距離場 → `smoothstep(w - softness, w + softness, dist)` の反転で
  アンチエイリアス付きに描く。Taper は線分の媒介変数 t に応じて `w` をスケールする

### 2.4 ミップ一貫性

TAM を単純に縮小すると線が薄れて階調が失われ、遠景でハッチングが消える。
Unity の自動ミップに任せず、**各ミップレベルを個別に生成**する。

```
for mip in 0..maxMip:
    res_mip = Resolution >> mip
    // 線の本数は据え置きのまま、線幅を「ピクセル単位で一定」に保つ
    lineWidth_mip = LineWidth            // px 単位なので mip でも同じ太さ
    levelBuffer_mip = rasterize(lines, res_mip, lineWidth_mip)
```

`Texture2D.SetPixels(colors, mipLevel)` で各ミップへ直接書き込み、
`TextureImporter` 側では `mipmapEnabled = true` かつ **自動生成を上書きしない**よう
生成後に `Texture2D` アセットとして保存する（PNG 経由ではミップを持てないため
`.asset` として保存するか、PNG + 実行時 `Texture2D` 生成のどちらかを選ぶ）。

**判断**: PNG での配布互換を優先し、既定は PNG（ミップは Unity 自動生成）とする。
`Mip Consistency` を ON にした場合のみ `.asset`（`Texture2D`）として保存し、
UI に「PNGでは手動ミップを保持できないため .asset で保存します」と明記する。

### 2.5 水彩素材の生成パラメータ

| パラメータ | 既定 | 内容 |
|---|---|---|
| Granulation Scale | 64 | 粒子の細かさ |
| Granulation Contrast | 0.5 | 粒子のコントラスト |
| Paper Fiber Scale | 24 | 紙繊維の粗さ |
| Paper Fiber Direction | 0° | 繊維の流れ方向 |
| Paper Fiber Anisotropy | 0.6 | 方向性の強さ（0で等方） |
| Paper Contrast | 0.4 | 紙目の強さ |

生成は 2〜3 オクターブの value noise。`_WCGranulationTex` は既定 `"gray"` なので
**平均を 0.5 に正規化**して出力する（そうしないと ON にした瞬間に全体の明度が変わる）。
`_WCPaperTex` は既定 `"white"` だが実装側で `paperEffect` として加算に使われるため、
こちらも平均 0.5 を基準にし、ツール側で「Paper Intensity 0.3 推奨」と案内する。

---

## 3. 実装詳細

### 3.1 新規: `Editor/NataneToon/Tools/HatchingToneGenerator.cs`

`DissolvePatternGenerator.cs`（280行）を雛形にする。同じ `NataneToon.Editor.Tools` asmdef 内。

```csharp
namespace NataneToon.Editor
{
    public class HatchingToneGenerator : EditorWindow
    {
        private enum Mode { HatchingTAM, Watercolor }

        [MenuItem(NataneToolMenuPaths.HatchingToneGenerator)]
        public static void Open() { ... }

        private void OnGUI()
        {
            // Mode 切り替え → パラメータ → プレビュー → 生成 → マテリアルへ割当
        }
    }
}
```

- `Editor/NataneToon/NataneToolMenuPaths.cs` へ `HatchingToneGenerator` のパス定数を追加
  （`Tools/Natane/...` 配下。既存ツールの並びに合わせる）
- プレビューは6段を横並びに表示し、その下に「段を跨いだときに線が消えていないか」を
  確認するための連続グラデーション帯を描く
- 生成後に選択中マテリアルへ割当するボタン:
  `_HatchTex0` / `_HatchTex1` を設定し `_HATCHING` を有効化、`_UseHatching = 1`、
  `_HatchingTiling` は解像度から推奨値を計算して設定
- `Undo.RecordObject(material, "Assign Hatching Textures")` を必ず通す
- マテリアルが null / 対象シェーダーでない場合は安全に無視する（防御的実装）

### 3.2 `Editor/NataneToon/Tools/NataneTextureStudioWindow.cs`

「生成ツール」タブ（:145-186）へ `DrawCard` を1枚追加する。

```csharp
                DrawCard(
                    L("ハッチング/水彩素材生成", "Hatching & Watercolor Generator"),
                    L("ハッチング6段TAMと、水彩の粒状感/紙目テクスチャを生成します。",
                      "Generate 6-level hatching TAMs and watercolor granulation / paper textures."),
                    NataneToolMenuPaths.HatchingToneGenerator);
```

### 3.3 プリセット

| プリセット | 設定 |
|---|---|
| **Pencil / 鉛筆** | Angle 55°, CrossHatch 0°, Jitter 0.25, Taper 0.5, Density 3 / Curve 1.8 |
| **Crosshatch / 交差** | Angle 45°, CrossHatch 90°, From Level 3, Jitter 0.1, Density 4 / Curve 1.5 |
| **Manga / 漫画** | Angle 75°, CrossHatch 0°, Jitter 0.03, Taper 0, Density 6 / Curve 1.4（均一な平行線） |
| **Etching / 版画** | Angle 30°, CrossHatch 60°, From Level 2, Jitter 0.05, Density 8 / Curve 1.3 |

`_LINE_BOIL` の `_LineBoilAffectHatching` と組み合わせると線が揺れるため、
Pencil プリセットの説明に「Line Boil 併用推奨」を書く。

---

## 4. 検証

1. 生成した TAM を `_HatchTex0` / `_HatchTex1` へ割当し、
   ライトを回して6段が連続的に切り替わること（段境界で線が入れ替わらない）
2. カメラを引いてハッチングが完全に消えないこと（ミップ一貫性の確認）
3. `_WCGranulationTex` 割当前後で全体の明度が変わらないこと（平均0.5の確認）
4. 生成テクスチャのインポート設定が Linear / sRGBオフ / Repeat になっていること
5. Unity 2022.3.28f1 / 6000.0.55f1 の双方でツールが起動し、C#コンパイルエラーがないこと
   （`error CS` があると batchmode のシェーダー検証自体が走らない）

## 5. 完了条件

- Texture Studio の「生成ツール」タブから起動できる
- ハッチング6段TAM（RGBA + RG のパッキング）を仕様どおり出力できる
- 水彩の粒状感・紙目を出力でき、平均0.5に正規化されている
- 生成物を選択中マテリアルへ自動割当でき、Undo が効く
- プリセット4種が用意されている
- EN/JP 両対応
- `_SCREEN_TONE` / `_HALFTONE_SHADOW` を対象外とした理由がツール内ヘルプに記載されている
