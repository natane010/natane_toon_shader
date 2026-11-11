# P1: Shadow Shape Rig 実装仕様

影の形をアーティストがディレクションできるようにする機能。
Shading Rig (Todo et al., ACM TOG 40(4)) の考え方を、追加テクスチャ1枚以内・Uniform分岐のみで実装する。

- キーワード: `_SHADOW_SHAPE_RIG`
- プロパティ接頭辞: `_ShadowRig`
- Stage: B

---

## 1. 動機と既存実装の調査結果

| 既存機能 | 対象 | 影側の形状制御 |
|---|---|---|
| `_SHAPED_HIGHLIGHT` | スペキュラ／ハイライト | なし（ハイライト側のみ） |
| `_SDF_MAP` | 顔の影の**遷移順序** | 形状はテクスチャ依存で編集不可 |
| `_SHADOW_EDGE_NOISE` | 影境界のノイズ | ランダム。狙った形は作れない |
| `_USE_MULTI_SHADOW` | 影の段数 | 段数のみ。形状は不可 |

`ShadowShape` / `ShadingRig` は現時点で grep ゼロヒット。
「特定の部位だけ影を膨らませる／へこませる」ディレクションができない状態。

## 2. 仕様

### 2.1 モデル

シェーディング境界を決める `ndotl` に対し、UV上に置いた楕円プリミティブ（rig）が局所的なバイアスを加える。

```
ndotl' = saturate(ndotl + Σ_i ( w_i(uv) * strength_i ))
```

- `w_i(uv)` … スロット i の楕円内側で 1、境界外で 0 になる重み（falloff で減衰）
- `strength_i` … 正で影を減らす（膨らませない＝明るくする）、負で影を増やす
- 4スロット。全スロットOFFなら `ndotl` は不変（後方互換）

### 2.2 座標系

| 条件 | 使用する座標 |
|---|---|
| 既定 | `uv`（MainTex UV） |
| `_FACE_ORTHO` 有効時 | 正射投影空間を優先（顔で首の向きに影響されない） |

### 2.3 ライト追従

Shading Rig の「rigがライトに応じて動く」性質を、faceRight/faceForward への射影で近似する。

```
center' = center + lightFollow * float2(RdotL, FdotL) * _ShadowRigFollowScale
```

`RdotL` / `FdotL` は `ApplySDFShadow` と同じ定義（`NataneToonLighting.hlsl:553-554`）を再利用する。
`_FACE_SDF_ROTATION` が無効な場合は `lightDir` をオブジェクト空間 XY へ射影した値で代替する。

### 2.4 プロパティ

パッキングして CBUFFER 使用量を抑える。1スロット = float4 × 2。

| プロパティ | 型 | 内容 |
|---|---|---|
| `_ShadowShapeRig` | Float (Toggle) | 機能有効化。キーワード `_SHADOW_SHAPE_RIG` |
| `_ShadowRigParams0..3` | Vector | `(centerX, centerY, radiusX, radiusY)` |
| `_ShadowRigShape0..3` | Vector | `(rotationDeg, strength, falloff, lightFollow)` |
| `_ShadowRigFollowScale` | Range(0,1) | ライト追従の移動量スケール。既定 0.25 |
| `_ShadowRigMask` | 2D (R) | 全スロット共通のマスク。既定 `"white"` |
| `_ShadowRigMaskStrength` | Range(0,1) | マスクの効き。既定 1 |

既定値: `_ShadowRigParams0 = (0.5, 0.5, 0, 0)`（radius 0 = 実質無効）、`_ShadowRigShape0 = (0, 0, 0.5, 0)`。
`radiusX * radiusY == 0` のスロットは計算をスキップする。

---

## 3. 実装詳細

### 3.1 新規ファイル: `Shaders/NataneToon/Include/Effects/NataneToonShadowShapeRig.hlsl`

`NataneToonShapedHighlight.hlsl` と同じ「CBUFFER を読まない純関数のみ」方針で書く。

```hlsl
// NataneToonShadowShapeRig.hlsl
// Art-directable shadow shaping (Shading Rig style). Pure math, no CBUFFER reads.
#ifndef NATANE_TOON_SHADOW_SHAPE_RIG_INCLUDED
#define NATANE_TOON_SHADOW_SHAPE_RIG_INCLUDED

// Elliptical rig weight. Returns 1 inside, falling to 0 at the ellipse edge.
//   params = (centerX, centerY, radiusX, radiusY)
//   rotRad  = rotation in radians
//   falloff = 0 (hard) .. 1 (very soft)
half NataneRigWeight(float2 uv, float4 params, float rotRad, half falloff)
{
    float2 radius = params.zw;
    if (radius.x <= 1e-5 || radius.y <= 1e-5) return 0.0h;

    float2 d = uv - params.xy;
    float s, c;
    sincos(rotRad, s, c);
    float2 p = float2(d.x * c + d.y * s, -d.x * s + d.y * c);
    p /= radius;

    half r = (half)length(p);                 // 1.0 == ellipse boundary
    half inner = 1.0h - clamp(falloff, 0.0h, 0.999h);
    return 1.0h - smoothstep(inner, 1.0h, r);
}

#endif // NATANE_TOON_SHADOW_SHAPE_RIG_INCLUDED
```

### 3.2 `Include/Lighting/NataneToonLighting.hlsl`

`ApplySDFShadow`（:537-589）の直後に追加する。`FdotL` / `RdotL` の計算は
`ApplySDFShadow` 内のローカル変数なので、共通のヘルパーへ切り出して両者から呼ぶ。

```hlsl
// 追加: face-relative light basis (ApplySDFShadow からも呼ぶ)
void NataneFaceLightBasis(float3 lightDir, out float FdotL, out float RdotL)
{
    float3 faceForward = normalize(mul((float3x3)unity_ObjectToWorld, _FaceForwardDirection.xyz) + float3(0, 0, 0.0001));
    float3 faceRight   = normalize(mul((float3x3)unity_ObjectToWorld, _FaceRightDirection.xyz) + float3(0.0001, 0, 0));
    float3 faceUp      = cross(faceForward, faceRight);

    float3 raw = lightDir - dot(lightDir, faceUp) * faceUp;
    float len = length(raw);
    float3 flat = (len > 0.001) ? (raw / len) : faceForward;

    FdotL = dot(faceForward, flat);
    RdotL = dot(faceRight,   flat);
}

// Shadow Shape Rig
float ApplyShadowShapeRig(float2 uv, float ndotl, float3 lightDir)
{
#ifdef _SHADOW_SHAPE_RIG
    float FdotL, RdotL;
    NataneFaceLightBasis(lightDir, FdotL, RdotL);
    float2 follow = float2(RdotL, FdotL) * _ShadowRigFollowScale;

    half mask = 1.0h;
    #if defined(_SHADOW_SHAPE_RIG)
        mask = lerp(1.0h, NATANE_SAMPLE_REPEAT(_ShadowRigMask, uv).r, _ShadowRigMaskStrength);
    #endif

    half bias = 0.0h;
    #define NATANE_RIG_SLOT(P, S) \
        { \
            float4 prm = P; \
            prm.xy += follow * (S).w; \
            bias += NataneRigWeight(uv, prm, radians((S).x), (half)(S).z) * (half)(S).y; \
        }

    NATANE_RIG_SLOT(_ShadowRigParams0, _ShadowRigShape0)
    NATANE_RIG_SLOT(_ShadowRigParams1, _ShadowRigShape1)
    NATANE_RIG_SLOT(_ShadowRigParams2, _ShadowRigShape2)
    NATANE_RIG_SLOT(_ShadowRigParams3, _ShadowRigShape3)
    #undef NATANE_RIG_SLOT

    return saturate(ndotl + bias * mask);
#else
    return ndotl;
#endif
}
```

`radius = 0` のスロットは `NataneRigWeight` の早期 return で 0 になり、
`strength = 0` なら加算も 0 になるため、未使用スロットのコストは分岐1回に収まる。

### 3.3 `Include/Rendering/NataneToonFragment.hlsl`

`:719` の直後、`_VERTEX_COLOR_SHADOW`（:722）より前に挿入する。

```hlsl
    // ===== SDF Shadow Map =====
    ndotl = ApplySDFShadow(uv, ndotl, lightDir, i.worldPos);

    // ===== Shadow Shape Rig =====            <-- 追加
    // Art-directed local shaping of the shading boundary.
    ndotl = ApplyShadowShapeRig(uv, ndotl, lightDir);
```

この位置にする理由:
- SDFの後 → SDFで決まった顔影の形をさらに整形できる
- 頂点カラー影・Wrapped Diffuse の前 → 既存の閾値調整と二重適用にならない
- トーン量子化（`_USE_MULTI_SHADOW` / ramp）より前 → 段の境界そのものが動く

### 3.4 `Include/Core/NataneToonInput.hlsl`

CBUFFER 内、`_SHAPED_HIGHLIGHT` ブロック（:1024-1038）と同じ作法で追加する。

```hlsl
    #if defined(_SHADOW_SHAPE_RIG)
    float4 _ShadowRigParams0;
    float4 _ShadowRigParams1;
    float4 _ShadowRigParams2;
    float4 _ShadowRigParams3;
    float4 _ShadowRigShape0;
    float4 _ShadowRigShape1;
    float4 _ShadowRigShape2;
    float4 _ShadowRigShape3;
    float _ShadowRigFollowScale;
    float _ShadowRigMaskStrength;
    #endif
```

マスクテクスチャは `:1538` の「Expression Effects — masks / custom SDF (NOSAMPLER, shared samplers)」ブロックへ。

```hlsl
#ifdef _SHADOW_SHAPE_RIG
UNITY_DECLARE_TEX2D_NOSAMPLER(_ShadowRigMask);
#endif
```

`NataneToonCore.hlsl` の include 群へ `Effects/NataneToonShadowShapeRig.hlsl` を追加する
（Utils の後、Lighting より前。Lighting から `NataneRigWeight` を呼ぶため）。

### 3.5 `.shader` 側（本体 + Variants = 計13ファイル）

Properties へ追加（`NataneToonShader.shader:955` 付近の並びに合わせる）:

```
        // Shadow Shape Rig (影の形のアートディレクション)
        [Toggle(_SHADOW_SHAPE_RIG)] _ShadowShapeRig ("Enable Shadow Shape Rig (影シェイプリグ)", Float) = 0
        _ShadowRigParams0 ("Rig 0 Center XY / Radius XY", Vector) = (0.5,0.5,0,0)
        _ShadowRigShape0  ("Rig 0 Rot / Strength / Falloff / LightFollow", Vector) = (0,0,0.5,0)
        ... (1..3 同様)
        _ShadowRigFollowScale ("Rig Light Follow Scale", Range(0,1)) = 0.25
        [NoScaleOffset] _ShadowRigMask ("Rig Mask (R)", 2D) = "white" {}
        _ShadowRigMaskStrength ("Rig Mask Strength", Range(0,1)) = 1
```

`#pragma shader_feature_local _SHADOW_SHAPE_RIG` は
**`_SDF_MAP` と同じパス**に入れる。本体では `FORWARD_BASE`（:1390付近）と `FORWARD_ADD`（:1532付近）の2箇所。
`_SDF_MAP` が定義されている11ファイルを対象とし、Lite / Background など
`_SDF_MAP` を持たないバリアントには追加しない（機能セットの整合を保つ）。

対象確認コマンド:

```bash
grep -l '_SDF_MAP' Shaders/NataneToon/*.shader Shaders/NataneToon/Variants/*.shader
```

### 3.6 Editor 側

| ファイル | 変更 |
|---|---|
| `Integration/NataneShaderKeywordSynchronizer.cs` | `KeywordMappings` へ `("_ShadowShapeRig", "_SHADOW_SHAPE_RIG")`。**単一の真実**。ビルド最適化・VariantStripper・`NataneToonBuildSettings.hlsl` のガードは全てここから派生する |
| `GUI/NataneToonShaderGUI.cs` | `_SHAPED_HIGHLIGHT`（:7852-7855）と同じ `SetFoldout` + `DrawBoxedSection` + `DrawToggle` の形。スロット4つは折りたたみ内に並べ、`Vector` は個別ラベル付きの2行UI（Center XY / Radius XY）へ分解して描画する |
| `GUI/NataneToonInspectorSectionRegistry.cs` | `"_SHADOW_SHAPE_RIG", "_ShadowShapeRig", "shadow shape rig shading rig 影 形 リグ 影の形 アートディレクション", docSlug: "lighting/shadow-shape-rig"` |
| `GUI/NataneToonLocalization.cs` | EN/JP 文言 |
| `GUI/NataneToonSamplerBudgetEstimator.cs` | マスク1枚分を加算 |
| `Tools/PerformanceBudgetTool.cs` / `Tools/MaterialValidator.cs` | 機能カウントへ反映。`_SHADOW_EDGE_NOISE` との併用は影境界の二重変形になるため注意を出す |

### 3.7 自動生成連携: マスクから Rig 生成

`Tools/NataneMaskPainter.cs` で塗ったマスクから rig パラメータを推定するボタンを追加する。

1. マスクを二値化し、連結成分を4つまで抽出（面積降順）
2. 各成分の重心 → `center`
3. 各成分の二次モーメント行列を固有値分解（2×2なので解析解）→ 長軸・短軸長と傾き → `radius` / `rotation`
4. `strength` は既定 `-0.2`（影を増やす方向）で入れ、ユーザーが調整

配置先: `Editor/NataneToon/Tools/ShadowRigFitter.cs`（新規、`NataneToon.Editor.Tools` asmdef 内）。
`Undo.RecordObject(material, "Fit Shadow Rig")` を必ず通す。

---

## 4. 検証

- OFF時に `ndotl` が変化しないこと（`ApplyShadowShapeRig` が `return ndotl`）
- 全スロット `radius = 0` でも ON にした場合、見た目が変わらないこと
- `_FACE_SDF_ROTATION` との併用でライト回転時に rig が破綻しないこと
- 2022.3.28f1 / 6000.0.55f1 の batchmode で `ShaderUtil.GetShaderMessages` が error / warning ゼロ
- Quest（`_QUEST_LITE`）で追加サンプラーが budget を超えないこと

## 5. 完了条件

- 機能OFF時に既存マテリアルの見た目が変わらない
- Inspector から4スロットを編集でき、EN/JP 両対応
- `_ShadowRigMask` で適用範囲を制御できる
- Sampler Budget / Performance Budget にコストが反映される
- `_SDF_MAP` を持つ11バリアントで有効、それ以外ではUIを出さない
- シェーダーコンパイルの error / warning がゼロ
