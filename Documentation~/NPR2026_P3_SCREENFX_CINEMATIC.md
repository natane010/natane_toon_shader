# P3: アニメ撮影模倣 ScreenFX 実装仕様

アニメ撮影（コンポジット）工程で使われる処理を `Natane/Screen FX Overlay` へ追加し、
演出プリセットとして提供する。

- 対象シェーダー: `Shaders/NataneToon/Effects/NataneScreenFXOverlay.shader`
- 対象ツール: `Editor/NataneToon/Tools/ScreenFXSetupTool.cs`
- Stage: C
- **PC限定**（GrabPass ベース。Quest では使用しない）

---

## 1. 動機

『マギアエクセドラ』CEDEC2025 講演で公開された必殺技演出の撮影処理が、
セルルック3DCGにおける事実上の定石になっている。

- Bloom / Gradation / Depth of Field
- **色収差を画面周囲のみに配置して視線誘導**
- モノクロ + 放射ブラーでインパクト強化
- ライト方向をフレーム単位で調整し、フットライト → 逆光 → 順光 で情緒を作る

## 2. 既存実装の調査結果

`NataneScreenFXOverlay.shader` の現行 frag（:125-183）の処理順:

```
ChromaticAberration → Edge darkening → Posterize → Scanline
  → Film grain → Vignette → Contrast → Saturation → Tint
```

| 撮影処理 | 現状 |
|---|---|
| 色収差 | あり（`_ChromaticAberration` / `_AberrationScale`）。ただし**画面全体に一律**（:136-141） |
| モノクロ | `_Saturation = 0` で全画面のみ可能。**部分適用は不可** |
| ビネット | あり（`_Vignette` / `_VignetteSoftness`） |
| グレイン | あり |
| Gradation 合成 | **なし** |
| 放射ブラー | **なし** |
| Bloom | **なし**（GrabPass 1枚では多段ダウンサンプルができないため対象外） |
| DoF | **なし**（深度テクスチャ前提。VRChat アバターからの利用は不安定なため対象外） |

追加するのは **Gradation / 放射ブラー / 周辺限定の色収差 / 部分モノクロ** の4つに絞る。
Bloom と DoF は上記理由で本仕様の対象外とし、ドキュメントにその旨を明記する。

## 3. 仕様

### 3.1 追加プロパティ

| プロパティ | 型 | 既定 | 内容 |
|---|---|---|---|
| `_AberrationEdgeOnly` | Range(0,1) | 0 | 1で画面周辺のみに色収差。中心は無効 |
| `_AberrationEdgeStart` | Range(0,1) | 0.4 | 周辺重みの立ち上がり半径 |
| `_RadialBlurStrength` | Range(0,1) | 0 | 放射ブラー強度 |
| `_RadialBlurCenter` | Vector | (0.5,0.5,0,0) | 放射中心（screen UV） |
| `_RadialBlurSamples` | Range(2,8) | 4 | サンプル数。上限8 |
| `_RadialBlurEdgeOnly` | Range(0,1) | 1 | 中心を残して周辺だけブラー |
| `_GradationTexture` | 2D | "white" | グラデーション素材（縦1pxのランプでも可） |
| `_GradationColorA` | Color | (1,1,1,1) | テクスチャ未指定時の上側の色 |
| `_GradationColorB` | Color | (0,0,0,1) | テクスチャ未指定時の下側の色 |
| `_GradationBlend` | Range(0,1) | 0 | 合成量 |
| `_GradationMode` | Enum | 0 | 0=Multiply / 1=Screen / 2=Overlay / 3=Additive |
| `_GradationAngle` | Range(0,360) | 0 | グラデーション方向 |
| `_MonochromeStrength` | Range(0,1) | 0 | モノクロ化量 |
| `_MonochromeEdgeOnly` | Range(0,1) | 0 | 1で周辺のみモノクロ（中心はカラーを残す） |

`_Saturation` は既存互換のため残し、`_MonochromeStrength` は
「半径重み付きで適用できる別系統」として後段で適用する。

### 3.2 frag への挿入位置

```
SampleScreen
  → 放射ブラー          (追加: 他の処理より前。ブラー後の色を以降の入力にする)
  → 色収差              (変更: edgeWeight を乗算)
  → Edge darkening
  → Posterize
  → Scanline
  → Film grain
  → Vignette
  → Gradation 合成      (追加)
  → Contrast / Saturation / Tint
  → 部分モノクロ        (追加: 最後。撮影で言う「白黒処理」に相当)
```

### 3.3 実装コード

既存の `centeredUV` / `radius` の計算（:169-170）を frag 冒頭へ引き上げて共有する。

```hlsl
                float2 centeredUV = screenUV * 2.0 - 1.0;
                half radius = saturate(length(centeredUV));

                // 周辺重み: 中心 0 → 画面端 1
                half edgeWeight = smoothstep(_AberrationEdgeStart, 1.0h, radius);
```

**放射ブラー**（サンプル数は `#if` ではなくループ上限固定で分岐コストを抑える）:

```hlsl
                // Radial blur — pull samples toward the blur center.
                if (_RadialBlurStrength > 0.001h)
                {
                    half rbWeight = lerp(1.0h, edgeWeight, _RadialBlurEdgeOnly);
                    float2 dir = screenUV - _RadialBlurCenter.xy;
                    half3 acc = fxColor;
                    int count = (int)min(_RadialBlurSamples, 8.0);
                    [loop] for (int s = 1; s <= count; ++s)
                    {
                        half t = (half)s / (half)count;
                        acc += SampleScreen(screenUV - dir * t * _RadialBlurStrength * 0.15);
                    }
                    acc /= (half)(count + 1);
                    fxColor = lerp(fxColor, acc, rbWeight);
                }
```

**周辺限定の色収差**（既存 :136-141 を置き換え）:

```hlsl
                half caAmount = _ChromaticAberration * lerp(1.0h, edgeWeight, _AberrationEdgeOnly);
                float2 aberrationOffset = texel * (caAmount * _AberrationScale * 2.0);
                half3 chromaColor;
                chromaColor.r = SampleScreen(screenUV + aberrationOffset).r;
                chromaColor.g = fxColor.g;
                chromaColor.b = SampleScreen(screenUV - aberrationOffset).b;
                fxColor = lerp(fxColor, chromaColor, saturate(caAmount));
```

`chromaColor.g` は `baseColor.g` ではなく `fxColor.g` を使う（放射ブラー後の色を引き継ぐため）。

**Gradation 合成**:

```hlsl
                if (_GradationBlend > 0.001h)
                {
                    float a = radians(_GradationAngle);
                    float s, c; sincos(a, s, c);
                    half g = saturate(dot(centeredUV, float2(s, c)) * 0.5 + 0.5);
                    half3 grad = lerp(_GradationColorB.rgb, _GradationColorA.rgb, g);
                    grad *= tex2D(_GradationTexture, float2(g, 0.5)).rgb;

                    int mode = (int)(_GradationMode + 0.5);
                    half3 blended =
                        (mode == 0) ? fxColor * grad :
                        (mode == 1) ? 1.0h - (1.0h - fxColor) * (1.0h - grad) :
                        (mode == 2) ? lerp(2.0h * fxColor * grad,
                                           1.0h - 2.0h * (1.0h - fxColor) * (1.0h - grad),
                                           step(0.5h, NataneLuminance(fxColor)))
                                    : fxColor + grad;
                    fxColor = lerp(fxColor, blended, _GradationBlend);
                }
```

**部分モノクロ**（最後）:

```hlsl
                if (_MonochromeStrength > 0.001h)
                {
                    half mWeight = _MonochromeStrength * lerp(1.0h, edgeWeight, _MonochromeEdgeOnly);
                    fxColor = lerp(fxColor, NataneLuminance(fxColor).xxx, mWeight);
                }
```

### 3.4 キーワード方針

ScreenFX は Overlay 1マテリアルにしか使われず変種爆発の懸念が小さいため、
新規 shader_feature は**追加しない**。全て Uniform 分岐（`if` による早期スキップ）とする。
その代わり `_RadialBlurSamples` の上限を8に固定し、最悪コストを抑える。

既存 Refraction が Quest でサンプル数を 9→5 に削減している方針に倣い、
放射ブラーの既定サンプル数は 4 とする。

## 4. プリセット

`ScreenFXSetupTool.cs` へ4種を追加する。

| プリセット | 主な設定 |
|---|---|
| **劇場 / Cinematic** | Vignette 0.35、Grain 0.03、Contrast 1.08、`_AberrationEdgeOnly` 1 / CA 0.3、Gradation Multiply 0.15（上を暖色・下を寒色） |
| **必殺技 / Impact** | RadialBlur 0.6（EdgeOnly 1、Samples 6）、Monochrome 0.5（EdgeOnly 1）、CA 0.8 周辺のみ、Vignette 0.5 |
| **回想 / Flashback** | Monochrome 0.75（全画面）、Grain 0.08、Vignette 0.45、Gradation Screen 0.25（セピア） |
| **シリアス / Serious** | Saturation 0.7、Contrast 1.15、Gradation Multiply 0.3（下方向を暗く）、Vignette 0.3 |

各プリセット適用は `Undo.RecordObject(material, "Apply ScreenFX Preset")` を通す。

## 5. 検証

- 全パラメータ既定値（追加分すべて0）で、現行の見た目と**ピクセル一致**すること
- VR Single Pass Instanced で左右の目にズレが出ないこと
  （`UNITY_SAMPLE_SCREENSPACE_TEXTURE` を経由し続ける。追加サンプルも `SampleScreen` 経由に統一）
- `_RadialBlurSamples = 8` で 2022.3.28f1 / 6000.0.55f1 ともにコンパイル error / warning ゼロ
- Quest ビルド対象から除外されていること（既存の ScreenFX の扱いを踏襲）

## 6. 完了条件

- 追加4機能が Inspector から操作でき、EN/JP 両対応
- 既定値で既存の見た目が変わらない
- プリセット4種が適用でき、Undo が効く
- ドキュメントに PC限定・Bloom / DoF 非対応の理由が明記されている
- シェーダーコンパイルの error / warning がゼロ
