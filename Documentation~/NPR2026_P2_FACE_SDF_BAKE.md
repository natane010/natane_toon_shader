# P2: 顔SDF影マップの本格ベイク 実装仕様

`_FACE_SDF_ROTATION` が本来要求する「角度フィールド」を、メッシュから実際にベイクして生成する。
既存の近似生成器が抱える意味的な不整合の修正を兼ねる。

- 生成物: `_SDFMap`（R チャンネル）
- Stage: A（**シェーダーのロジック修正1行を含む**。詳細は §2.3）

---

## 1. 既存実装の調査結果

### 1.1 現状の生成器

`Editor/NataneToon/GUI/NataneToonSdfAutoGenerator.cs`

```
TryResolveSourceTexture()   … Shadow Receive Mask > Main Texture の優先順で元テクスチャを決定
  → BuildMask()             … 明暗またはalphaで二値マスク化
  → BuildSignedDistanceField()  … マスク境界からの符号付き距離場
  → BuildSdfTexture()       … グレースケール化して保存
  → material.SetTexture("_SDFMap", ...) / EnableKeyword("_SDF_MAP")
```

出力は **マスク形状の符号付き距離場**。ライト角の情報はどこにも入っていない。

### 1.2 シェーダー側が要求しているもの

`Shaders/NataneToon/Include/Lighting/NataneToonLighting.hlsl:540-571`

```hlsl
float FdotL = dot(faceForward, lightDirFlat);
float RdotL = dot(faceRight,   lightDirFlat);

float2 sdfUV = uv;
sdfUV.x = (RdotL < 0) ? (1.0 - sdfUV.x) : sdfUV.x;   // 左右ミラー

float sdfValue  = NATANE_SAMPLE_REPEAT(_SDFMap, sdfUV).r;
float threshold = FdotL * 0.5 + 0.5 + _SDFOffset;
float sdfShadow = smoothstep(threshold - shadowEdge, threshold + shadowEdge, sdfValue);
return lerp(ndotl, sdfShadow, _SDFIntensity);       // sdfShadow = 1 が「明るい」
```

つまり `_SDFMap.r` は **「その画素がライト角に対して明暗反転する閾値」** でなければならない。
距離場とは別物なので、現在の組み合わせでは回転追従が正しく動作しない。

### 1.3 閾値式の符号に関する指摘

`lightDir` は `UnityWorldSpaceLightDir()`（`NataneToonFragment.hlsl:499,525`）なので
**サーフェスからライトへ向かうベクトル**。したがって:

- ライトが顔の正面 → `FdotL = +1` → `threshold = 1.0`
- ライトが顔の背面 → `FdotL = -1` → `threshold = 0.0`

`lit ⟺ sdfValue > threshold` なので、**正面ライトでほぼ全面が影、背面ライトで全面が明るく**なる。
期待と逆である。

導出: 画素が「azimuth θ（0 = 正面）まで明るく、それを超えると影」という遷移角 θt を持つとする。

```
明るい ⟺ θ < θt
        ⟺ cos θ > cos θt
        ⟺ (1 - cos θ)/2 < (1 - cos θt)/2
```

`s(θ) = (1 - cos θ)/2` は θ について単調増加で [0,1] に収まる。
`sdfValue = s(θt)` と定義すれば `明るい ⟺ s(θ) < sdfValue`、すなわち

```
threshold = (1.0 - FdotL) * 0.5 + _SDFOffset
```

が正しい。現行は `FdotL * 0.5 + 0.5` で符号が反転している。

**対応**: `NataneToonLighting.hlsl:564` を上式へ修正する。

```diff
-            float threshold = FdotL * 0.5 + 0.5 + _SDFOffset;
+            // s(theta) = (1 - cos theta) / 2 : 0 = front lit, 1 = fully rotated to back.
+            // The baked map stores s(theta_transition), so lit <=> s(theta) < sdfValue.
+            float threshold = (1.0 - FdotL) * 0.5 + _SDFOffset;
```

後方互換について: 現行の `_SDF_MAP` + `_FACE_SDF_ROTATION` の組み合わせは
上記のとおり正しい絵を出せていないため、既存マテリアルの「意図した見た目」を壊す変更にはならない。
ただし挙動は変わるので CHANGELOG に破壊的変更として明記し、
`_FACE_SDF_ROTATION` を使っていない（`_SDF_MAP` 単体）マテリアルには影響しないことを確認する
（`#else` 側は無変更）。

---

## 2. ベイク仕様

### 2.1 出力フォーマット

| 項目 | 値 |
|---|---|
| 解像度 | 512 / 1024 / 2048（既定 1024） |
| チャンネル | R のみ使用。`s(θt) = (1 - cos θt) / 2` |
| 色空間 | Linear（sRGB オフ） |
| ミップ | 生成しない（境界が滑って階調が崩れる） |
| 圧縮 | 未圧縮または BC4。DXT1 は帯状のアーティファクトが出るため使わない |
| 対称性 | 顔の**右半分（`RdotL >= 0`）のみ**をベイク。シェーダー側が `1.0 - uv.x` でミラーする |

### 2.2 アルゴリズム

シャドウマップ方式。UV空間のG-bufferを作り、ライト角ごとに深度テストする。

```
1. UVベイク:  顔メッシュを UV を頂点位置として描画し、
              RT_pos(world position) と RT_nrm(world normal) を得る
              → 既存 MapGen_UVBake.compute / Shaders/Reduction/BakeNormal.shader を流用

2. 角度スイープ (i = 0 .. N-1):
   θ_i = 180° * i / (N-1)
   lightDir_i = faceForward * cos θ_i + faceRight * sin θ_i      // 右半面のみ
   a. 正射投影カメラを lightDir_i に置き、Shaders/Reduction/BakeDepth.shader で
      深度を RT_depth(i) へ描画
   b. compute: 各テクセルについて
        ndotl    = dot(RT_nrm, lightDir_i)
        occluded = RT_pos を lightDir_i の投影空間へ変換し RT_depth(i) と比較
        lit_i    = (ndotl > 0) && !occluded

3. 遷移角の確定: 各テクセルについて lit が 1 → 0 に落ちる最初の i を θt とする
   （ノイズ耐性のため「2ステップ連続で影」を条件にする）
   一度も影にならないテクセル → s = 1.0（常に明るい）
   一度も明るくならないテクセル → s = 0.0（常に影）

4. s = (1 - cos θt) / 2 を R へ書き込む

5. 後処理: MapGen_Dilation.compute で UV シームを埋め、
   MapGen_GaussianBlur.compute で 1〜2px の平滑化（_SDFSoftness と二重にぼかさないよう控えめに）
```

`N` の既定は 64。UI で 32 / 64 / 128 / 180 を選択可能にする。
描画は N 回だが RT は使い回すため、1024² / N=64 で数秒程度に収まる想定。

### 2.3 既存のレイキャスト方式を使わない理由

`MapGeneratorEditor.cs` の AO / Shadow Map は **頂点単位の `Physics.Raycast`**（:378, :509）。
顔SDFで必要なのは鼻・まつげが落とす**画素単位の影境界**なので、頂点解像度では鼻影が出ない。
1024² × 64角度 = 6,700万レイとなり CPU レイキャストでは非現実的。
そのため深度描画＋compute方式を採用する。

---

## 3. 実装詳細

### 3.1 新規: `Editor/MapGenerator/Shaders/Compute/MapGen_FaceSdfBake.compute`

```hlsl
#pragma kernel CSAccumulate      // 1角度分を評価し、遷移状態を更新
#pragma kernel CSFinalize        // 遷移角 -> s へ変換

Texture2D<float4>   _PosMap;      // xyz = world position, w = coverage(0/1)
Texture2D<float4>   _NrmMap;      // xyz = world normal
Texture2D<float>    _LightDepth;  // 現在の角度の深度
RWTexture2D<float4> _State;       // x = 前ステップのlit, y = 連続shadowカウント,
                                  // z = 確定した遷移index(-1 = 未確定), w = 予備
RWTexture2D<float>  _Result;

float4x4 _LightVP;
float3   _LightDir;
float    _AngleIndex;
float    _AngleCount;
float    _DepthBias;
```

- `CSAccumulate` を N 回ディスパッチ（角度ごとに `_LightDepth` と `_LightVP` を差し替え）
- `_State.z` が未確定のテクセルだけを更新する
- `CSFinalize` で `θt = 180 * z / (N-1)`、`s = (1 - cos(radians(θt))) / 2` を `_Result` へ

**CPUフォールバック**: 既存の他 compute と同様、`FindComputeShader` が null を返した場合は
CPU パスへ落ちる作りにする（`MapGeneratorEditor.cs:1754` の警告と同じ扱い）。
CPUフォールバックは頂点単位の粗い近似とし、警告を出す。

### 3.2 `Editor/MapGenerator/MapGeneratorEditor.cs`

既存のマップと同じ作法でセクションを追加する。

- `MapGenSettings` へ:
  `generateFaceSdf` / `faceSdfResolution` / `faceSdfAngleSteps` /
  `faceSdfForward`(Vector3, 既定 `(0,0,1)`) / `faceSdfRight`(Vector3, 既定 `(1,0,0)`) /
  `faceSdfDepthBias` / `faceSdfDilation` / `faceSdfBlur`
- `GenerateFaceSdfInternal(Mesh, Renderer, MapGenSettings)` を追加
- `DrawMapSection(ref _foldFaceSdf, MapGenL.L("顔SDF影マップ", "Face SDF Shadow Map"), ...)`
- 「★ 全マップ生成」からは**除外**（顔専用で、体のメッシュに対して意味がないため）
- `AssignMap(mat, saved, "_SDFMap", "_UseSDFMap", "_SDF_MAP")` に加えて
  `_FACE_SDF_ROTATION` を有効化し、`_FaceForwardDirection` / `_FaceRightDirection` を
  ベイク時に指定した軸で上書きする（ベイクとシェーダーの座標系を必ず一致させる）
- 推奨値の適用: `_SDFIntensity = 1.0`、`_SDFSoftness = 0.05`、`_SDFOffset = 0`

**asmdef の制約**: `Editor/MapGenerator/` は独立アセンブリ `MapGenerator.Editor` で
`NataneToon.Editor` を参照しない。`NataneEditorCompat` などの共有ヘルパーは使えないため、
バージョン分岐は各ファイル内で `#if UNITY_2022_2_OR_NEWER` を直書きする。

### 3.3 `Editor/NataneToon/GUI/NataneToonSdfAutoGenerator.cs`

既存機能は削除せず、役割を明確化する。

- UI 表記を **「Mask SDF を生成（回転追従なし）」** に変更
- `_FACE_SDF_ROTATION` が有効なマテリアルに対して実行された場合、
  「回転追従には Map Generator の顔SDF影マップベイクを使ってください」と警告を出し、
  Map Generator ウィンドウを開くボタンを添える
- 生成後に `_FACE_SDF_ROTATION` を**有効化しない**（現状も有効化していないので変更なし）

---

## 4. 検証

1. **合成テスト**: 球にベイク → 全テクセルが「正面で明るく、90°で影」になり、
   `s` が概ね `(1 - cos(90°))/2 = 0.5` 付近へ集まること
2. **顔メッシュ**: ライトを水平に一周させ、鼻影・まつげ影が
   正面 → 斜め → 横 の順に自然に伸びること。逆順・突然の反転がないこと
3. **左右ミラー**: `RdotL` の符号が切り替わる真正面付近で継ぎ目が出ないこと
4. `_SDF_MAP` 単体（`_FACE_SDF_ROTATION` 無効）のマテリアルの見た目が変わらないこと
5. 2022.3.28f1 / 6000.0.55f1 の batchmode で error / warning ゼロ

## 5. 完了条件

- Map Generator から顔SDF影マップを生成し、マテリアルへ自動割当できる
- ベイク軸とシェーダーの `_FaceForwardDirection` / `_FaceRightDirection` が必ず一致する
- 既存の Mask SDF 生成器が用途を誤解されないUIになっている
- `threshold` の符号修正が CHANGELOG に破壊的変更として記録されている
- EN/JP 両対応、Undo 対応
- シェーダーコンパイルの error / warning がゼロ
