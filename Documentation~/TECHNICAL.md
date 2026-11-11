# 技術仕様書

Natane Toon Shaderの技術的な詳細とカスタマイズガイドです。

## アーキテクチャ

### ファイル構成

```
Assets/
├── Shaders/
│   ├── NataneToonShader.shader      # メインシェーダーファイル
│   └── Include/
│       └── NataneToonCore.cginc     # コア機能実装
└── Editor/
    └── NataneToonShaderGUI.cs       # カスタムインスペクタ
```

### シェーダーパス構成

#### 1. OUTLINE Pass
- **目的**: アウトライン描画
- **Cull Mode**: Front（前面カリング）
- **手法**: 法線押し出し（Inverted Hull）
- **実行条件**: `_OUTLINE` キーワードが有効な場合

#### 2. FORWARD_BASE Pass
- **目的**: メインライティング、環境光、各種エフェクト
- **Light Mode**: ForwardBase
- **機能**:
  - セルシェーディング/ランプシェーディング
  - スペキュラハイライト
  - リムライト
  - MatCap
  - エミッション
  - 環境光
  - シャドウ受信

#### 3. FORWARD_ADD Pass
- **目的**: 追加ライト処理
- **Light Mode**: ForwardAdd
- **Blend Mode**: One One（加算ブレンド）
- **機能**:
  - セルシェーディング/ランプシェーディング
  - スペキュラハイライト
  - 複数ライト対応

#### 4. SHADOW_CASTER Pass
- **目的**: シャドウキャスティング
- **Light Mode**: ShadowCaster
- **機能**: 他のオブジェクトへの影の投影

## ライティングアルゴリズム

### セルシェーディング

```csharp
float ToonShading(float ndotl, float steps, float sharpness)
{
    // Shadow Offsetを適用
    ndotl = ndotl + _ShadowOffset;

    // ステップ化
    float toon = floor(ndotl * steps) / steps;

    // エッジのスムージング
    float edge = frac(ndotl * steps);
    toon += smoothstep(0.5 - sharpness * 0.5,
                       0.5 + sharpness * 0.5,
                       edge) / steps;

    return saturate(toon);
}
```

**パラメータ**:
- `ndotl`: 法線とライト方向の内積
- `steps`: 影のステップ数（1-10）
- `sharpness`: エッジのシャープネス（0.001-1）

### ランプテクスチャシェーディング

```csharp
float3 RampShading(float ndotl)
{
    float2 rampUV = float2(saturate(ndotl + _ShadowOffset), 0.5);
    return tex2D(_RampTex, rampUV).rgb;
}
```

**テクスチャ仕様**:
- 横方向にグラデーション（左=暗、右=明）
- 推奨サイズ: 256x4 または 512x4
- フォーマット: RGB、Clamp

### スペキュラハイライト

```csharp
float SpecularHighlight(float3 normal, float3 viewDir,
                       float3 lightDir, float size, float softness)
{
    float3 halfVector = normalize(lightDir + viewDir);
    float ndoth = max(0, dot(normal, halfVector));

    // アニメ調のシャープなスペキュラ
    float spec = smoothstep(1.0 - size - softness,
                           1.0 - size + softness,
                           ndoth);
    return spec;
}
```

**特徴**:
- Blinn-Phongベースのハーフベクトル使用
- smoothstepでアニメ調のシャープなエッジ
- サイズとソフトネスで調整可能

### リムライト

```csharp
float3 RimLighting(float3 normal, float3 viewDir,
                  float power, float intensity)
{
    float rim = 1.0 - saturate(dot(normal, viewDir));
    rim = pow(rim, power) * intensity;
    return rim * _RimColor.rgb;
}
```

**特徴**:
- Fresnel効果ベース
- Powerパラメータで範囲制御
- Intensityで強度調整

### MatCap

```csharp
float2 CalculateMatCapUV(float3 worldNormal, float3 viewDir)
{
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    float2 matcapUV = viewNormal.xy * 0.5 + 0.5;
    return matcapUV;
}
```

**ブレンドモード**:
- **Add (0)**: `col.rgb += matcap` - 光沢、ハイライト
- **Multiply (1)**: `col.rgb *= matcap` - 陰影、暗部
- **Replace (2)**: `col.rgb = lerp(col.rgb, matcap, intensity)` - 金属質感

### アウトライン

```csharp
// 頂点シェーダー
float3 norm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
float2 offset = TransformViewToProjection(norm.xy);

o.pos = UnityObjectToClipPos(v.vertex);
o.pos.xy += offset * o.pos.z * _OutlineWidth;
```

**特徴**:
- ビュー空間での法線押し出し
- 距離に応じた太さ調整（`o.pos.z`で乗算）
- 前面カリングで内側のみ描画

## シェーダーキーワード

| キーワード | 機能 | パフォーマンス影響 |
|-----------|------|------------------|
| `_USE_RAMP` | ランプテクスチャ | 小（テクスチャ読み込み1回） |
| `_SPECULAR` | スペキュラハイライト | 小（計算少量） |
| `_RIM_LIGHT` | リムライト | 小（計算少量） |
| `_MATCAP` | MatCap | 中（テクスチャ読み込み+座標計算） |
| `_OUTLINE` | アウトライン | 中（追加パス） |
| `_EMISSION` | 発光 | 小（テクスチャ読み込み1回） |
| `_NORMALMAP` | 法線マップ | 中（テクスチャ+接線空間計算） |

## パフォーマンス最適化

### レベル1: 最軽量（モバイル向け）
```
有効: セルシェーディングのみ
無効: すべてのエフェクト
Shadow Steps: 2
```
**用途**: ローエンドモバイル、多数のキャラクター

### レベル2: 軽量（標準モバイル）
```
有効: セルシェーディング、リムライト、アウトライン
無効: MatCap、法線マップ
Shadow Steps: 2
```
**用途**: スタンダードなモバイルゲーム

### レベル3: 標準（PC/コンソール）
```
有効: すべての基本機能
オプション: MatCap、法線マップ
Shadow Steps: 2-3
```
**用途**: PC、コンソールゲーム

### レベル4: 高品質
```
有効: すべての機能
法線マップ: 使用
Shadow Steps: 3-5
```
**用途**: ハイエンドPC、映像制作

## カスタマイズガイド

### 新しいエフェクトの追加

1. **Propertiesに追加**
```csharp
[Toggle(_NEW_FEATURE)] _NewFeature ("Enable New Feature", Float) = 0
_NewFeatureParam ("Parameter", Range(0, 1)) = 0.5
```

2. **#pragma shader_featureを追加**
```csharp
#pragma shader_feature _NEW_FEATURE
```

3. **変数宣言（NataneToonCore.cginc）**
```csharp
float _NewFeatureParam;
```

4. **実装（frag関数内）**
```csharp
#ifdef _NEW_FEATURE
    // Your code here
#endif
```

5. **ShaderGUIに追加**
```csharp
bool enableFeature = DrawToggle("_NEW_FEATURE", "_NewFeature", "Enable New Feature");
if (enableFeature)
{
    DrawProperty("_NewFeatureParam", "Parameter");
}
```

### カスタムライティングモデルの追加

`NataneToonCore.cginc` の `frag` 関数内でライティング計算をカスタマイズ:

```csharp
// 既存のライティング
#ifdef _USE_RAMP
    lighting = RampShading(ndotl * atten);
#else
    float toon = ToonShading(ndotl * atten, _ShadowSteps, _ShadowSharpness);
    lighting = lerp(_ShadowColor.rgb, float3(1, 1, 1), toon);
#endif

// カスタムライティングを追加
#ifdef _CUSTOM_LIGHTING
    lighting = YourCustomLightingFunction(ndotl, atten);
#endif
```

## デバッグとトラブルシューティング

### デバッグビューの追加

```csharp
// Normal可視化
return fixed4(worldNormal * 0.5 + 0.5, 1);

// NdotL可視化
return fixed4(ndotl.xxx, 1);

// Shadow可視化
return fixed4(atten.xxx, 1);
```

### よくある問題

#### アウトラインがちらつく
**原因**: モデルの法線が不正
**解決**:
- モデルの法線を再計算
- Smoothingを適切に設定
- Outline Widthを調整

#### 影の境界がガタガタ
**原因**: Shadow Sharpnessが高すぎる
**解決**: Sharpnessを0.05-0.1に設定

#### MatCapが回転する
**原因**: 正常動作（ビュー空間ベースのため）
**解決**: ワールド空間MatCapが必要な場合はコード修正

## VRC ライティング連携 互換性・対応メモ

最終確認日: 2026-07-17

確認基準は VRChat Unity 2022.3.22f1、VRC Light Volumes 2.1.3、LTCGI 1.7.1、Built-in Render Pipeline / VRChat PCです。

### VRC Light Volumes

VRC Light Volumes 2.1.3へ対応済みです。

- 同梱している `Shaders/NataneToon/ThirdParty/VRCLightVolumes/LightVolumes.cginc` は、2.1.3の同名ファイルとSHA-256が一致します。
- パッケージ導入時は `Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc` を自動検出して使用します。
- `LightVolumeSH`、`LightVolumeEvaluate`、`LightVolumeSpecular` の現行APIを使用します。
- パッケージがない場合も同梱版を使用し、Light Volumesが無効な環境ではUnity Light Probesへフォールバックします。

VRC Light Volumes 3.0.0-dev系は正式な対応対象に含めません。3.0.0-dev.10時点ではソース互換ですが、正式版公開時にパッケージパス、関数シグネチャ、SH係数、スペキュラーAPI、同梱ファイルとライセンス表記を再確認してください。

関連実装:

- `Shaders/NataneToon/Include/Lighting/NataneToonThirdPartyLighting.hlsl`
- `Shaders/NataneToon/Include/Rendering/NataneToonFragment.hlsl`
- `Editor/NataneToon/Integration/VRCLightVolumesAutoDetector.cs`

### LTCGI

LTCGI 1.7.1とのソース互換をUnityコンパイルレベルで検証済みです(2026-07-17)。VRChat実機確認のみ未実施です。

検証済み(Unity 2022.3.28f1、公式VPMの at.pimaker.ltcgi 1.7.1 / zipSHA256一致確認済み):

- LTCGI導入状態で `LTCGIAutoDetector.ForceRedetect` により `NATANE_LTCGI_AVAILABLE` が生成されること
- Main / Cutout / Transparent / Lite / Fur の5シェーダーで `_LTCGI` ON/OFF 両方ともエラー0・警告0
- LTCGI削除後の再検出でconfigが未定義状態へ正しく復元され、ベースラインもエラー0・警告0
- VRCSDK非導入でもLTCGIのC#はdefineガードによりコンパイル可能(CS0414警告1件はLTCGI側既存)

未検証(VRChat Build & Testが必要): 実機でのDiffuse/Specular/動画色変化の見た目、Light Volumes同時使用時の白飛び・二重加算・Sampler上限、Quest実機、VRCFallback発動時の見た目。

- `Packages/at.pimaker.ltcgi/Shaders/LTCGI.cginc` を自動検出します。
- LTCGI API v2のカスタム入力とDiffuse/Specular callbackを使用します。
- Built-in用SubShaderに `"LTCGI"="ALWAYS"` タグがあります。
- DiffuseとSpecularを分けて受け取り、Natane側の強度・ブレンド設定を適用します。
- パッケージがない場合は寄与を0にします。
- Avatar Modeは、LTCGI Controllerが `LTCGI_config.cginc` の `LTCGI_AVATAR_MODE` をVRChat Avatarプロジェクトに合わせて設定する前提です。

検証手順:

1. VCCからLTCGI 1.7.1を導入する。
2. `Tools > Natane > VRChat > LTCGI 再検出` を実行する。
3. `NataneToonLTCGIConfig.hlsl` に `NATANE_LTCGI_AVAILABLE` が生成されることを確認する。
4. Main、Cutout、Transparent、Lite、Furの代表Shaderで `_LTCGI` ON/OFF両方のコンパイルを確認する。
5. LTCGI Screenの `Affect Avatars` を有効にし、Diffuse、Specular、動画色変化をVRChat Build & Testで確認する。
6. VRC Light Volumesとの同時使用時に、白飛び、二重加算、Sampler上限超過がないことを確認する。

現在のcallbackはlilToon系の見た目に合わせた独自の距離減衰を使用しています。公式API v2サンプルの `output.intensity * output.color` を直接使用する方式とは見た目が異なるため、物理的な照り返し精度を優先する場合は比較検証してください。

関連実装:

- `Shaders/NataneToon/Include/Lighting/NataneToonThirdPartyLighting.hlsl`
- `Shaders/NataneToon/Include/Config/NataneToonLTCGIConfig.hlsl`
- `Editor/NataneToon/Integration/LTCGIAutoDetector.cs`

### Unity Light Probes

通常の `Blend Probes` は対応済みです。`ShadeSH9` によりRendererへ設定された補間済みSH係数を評価し、VRC Light Volumesを使用しない場合の間接光フォールバックにも利用します。

#### 対応済み: Light Probe Proxy Volume (2026-07-17)

LPPV対応を実装しました。共通関数 `NataneShadeSH(worldNormal, worldPos)` (`Shaders/NataneToon/Include/Lighting/NataneToonSH.hlsl`) が `UNITY_LIGHT_PROBE_PROXY_VOLUME` かつ `unity_ProbeVolumeParams.x == 1` のときプローブボリュームをサンプリングし、それ以外では従来の `ShadeSH9` とビット一致の結果を返します。Fragment(Background含む)・FurShell・Particleを同関数へ統一済み。FORWARD_BASEのみに `multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME` を宣言(ForwardAddへの環境光重複なし)、VRC Light Volumes有効時はLight Volumesが優先。Sampler予算 +1 は `NataneToonSamplerBudgetEstimator.LppvProbeVolumeSamplerCost` として計上。詳細と検証チェックリストは `Documentation~/LPPV_SUPPORT.md` を参照。

当初の実装方針(記録):

- Unity 2022.3 Built-inの `ShadeSHPerPixel(worldNormal, ambient, worldPos)` を使用する共通関数を追加する。
- `UNITY_LIGHT_PROBE_PROXY_VOLUME` と `unity_ProbeVolumeParams.x` に応じてLPPVを評価し、通常環境では従来の `ShadeSH9` 結果を維持する。
- ForwardBaseのみで間接光へ適用し、ForwardAddへ環境光を重複加算しない。
- VRC Light VolumesとUnity Light Probesの優先順位を明示する。
- Fur、Particle、Backgroundなど独自のSH評価箇所も同じ共通関数へ統一する。
- LPPV用SamplerとShader Model要件が、VRChat PC向けのSampler予算を超えないことを確認する。

検証項目:

- `Blend Probes` の従来表示が変わらない。
- `Use Proxy Volume` でメッシュ位置に沿って照明色が変化する。
- LPPVなし、ライトプローブなし、AmbientのみのSceneでも破綻しない。
- VRC Light Volumes ON/OFFで意図した優先順位になる。
- GPU Instancing、Skinned Mesh、左右眼カメラ、鏡で不整合がない。
- Unity 2022.3.22f1のWindows/D3D11でShaderコンパイルエラーがない。

### 発光の照り返しに関する制約

- ワールドでベイクされた発光の照り返しは、Light ProbesまたはVRC Light Volumesへベイクされていれば受光できます。
- LTCGI対応ScreenやArea Lightからのリアルタイム照明は、LTCGIパッケージと対応ワールド設定が揃っている場合に受光できます。
- アバター自身のEmissionから、他のアバターやワールドへ本物のGIをShader単体で投射することはできません。
- 自己照り返しが必要な場合は、オブジェクト空間の疑似Emissionライトを別機能として実装し、本物のGIではないことをInspector上で明示してください。

参照先:

- VRChat Current Unity Version: https://creators.vrchat.com/sdk/upgrade/current-unity-version/
- VRC Light Volumes: https://github.com/REDSIM/VRCLightVolumes
- LTCGI Shader Authors: https://ltcgi.dev/Advanced/Shader_Authors
- Unity Light Probe Proxy Volume: https://docs.unity3d.com/2022.3/Documentation/Manual/class-LightProbeProxyVolume.html

## ライセンスとクレジット

このシェーダーは以下を参考に設計されています：
- lilToon (MIT License)
- 一般的なトゥーンシェーディング技法

MITライセンスの下で配布されています。
