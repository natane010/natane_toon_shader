# Light Probe Proxy Volume (LPPV) サポート

Built-in Render Pipeline 向けに、Unity の Light Probe Proxy Volume (LPPV) を
評価できるようにしました。実装は Unity 2022.3 Built-in の
`ShadeSHPerPixel()`（`UnityStandardUtils.cginc`）を踏襲しています。

## 実装概要

### 共通 SH 評価関数

- 追加ファイル: `Shaders/NataneToon/Include/Lighting/NataneToonSH.hlsl`
- 関数: `half3 NataneShadeSH(half3 worldNormal, float3 worldPos)`
- 挙動:
  - `UNITY_LIGHT_PROBE_PROXY_VOLUME` が有効かつ `unity_ProbeVolumeParams.x == 1`
    のとき、L0/L1 項を 3D プローブボリュームテクスチャ
    (`unity_ProbeVolumeSH`) からピクセル単位でサンプル
    (`SHEvalLinearL0L1_SampleProbeVolume`)。
  - それ以外は従来どおり per-object SH (`SHEvalLinearL0L1`) を使用。
  - L2 項は常に per-object SH (`SHEvalLinearL2`) から取得（Unity の
    `ShadeSHPerPixel` と同じく、L2 はプローブボリュームに格納されないため）。
  - `UNITY_COLORSPACE_GAMMA` 時のガンマ変換も従来 `ShadeSH9` と同一。
- LPPV 非適用時（通常環境）の出力は従来の `ShadeSH9` と**ビット単位で同一**です。
- インクルード経路: `NataneToonUtils.hlsl` から include。Utils は Fragment /
  Fur Shell / Background の全経路で `UnityCG.cginc` の後に読み込まれるため
  依存関係を満たします。Particle シェーダーは独自インクルードツリーのため
  `UnityCG.cginc` 直後に直接 include しています。

### SH 評価箇所の統一

`ShadeSH9` を使用していた「環境光（ambient / indirect）評価」箇所を
すべて `NataneShadeSH` に置き換えました。

| 箇所 | ファイル | 対応 |
| --- | --- | --- |
| Fragment 間接光ブロック（L0平均 / 方向性SH×2） | `Include/Rendering/NataneToonFragment.hlsl` | 統一・LPPV対応（ForwardBase） |
| Fur Shell の ambient | `Include/Rendering/NataneToonFurShell.hlsl` | 統一（Fur Shell は "Always" パスのため LPPV は 0=フォールバック） |
| Particle Toon Lighting の ambient | `Variants/NataneToonShader_Particle.shader` | 統一・LPPV対応（ForwardBase、`worldPos` 補間子を追加） |
| Background 経路 | 上記 Fragment を共有（`NataneToonCore.hlsl` 経由） | 統一・LPPV対応 |

意図的に**置き換えていない**箇所:

- `Include/Utils/NataneToonUtils.hlsl` の `GetSHDominantLightDirection()` /
  `GetSHFallbackLightColor()` — これらは `unity_SHAr/g/b` を直接使う
  「ドミナントライト方向 / 平均色の推定」であり、環境光評価ではないため対象外。
- `Shaders/AutoMat/Include/AutoMat_PBRLighting.hlsl` の `ShadeSH9` — AutoMat は
  Natane Toon とは別系統のシェーダーで、本タスクのスコープ外のため据え置き。

### ForwardBase のみへの適用 / ForwardAdd への非加算

- 間接光（環境光）の合成は Fragment 内の `#ifdef UNITY_PASS_FORWARDBASE`
  ブロック内でのみ行われます。ForwardAdd パスは環境光を評価しません（従来どおり）。
- LPPV の `#pragma multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME` は
  **FORWARD_BASE パスにのみ**追加しました。FORWARD_ADD には追加していません。

### VRC Light Volumes と Unity Light Probes の優先順位

Fragment の間接光分岐は以下の優先順で従来構造を維持しています（SH 評価器のみ差し替え）:

1. `_BACKGROUND_MODE && LIGHTMAP_ON` … ライトマップ
2. `_USE_LIGHT_VOLUME` … **VRC Light Volumes（最優先）**。有効時は SH/LPPV を評価せず、
   環境光の二重加算を回避。
3. それ以外 … Unity Light Probes（`NataneShadeSH` = LPPV対応）

Fur Shell も同様に、`_UseLightVolume > 0.5` のとき Light Volume の結果で ambient を
上書き（`max`）する既存構造を維持しています。

### multi_compile の判断

- 追加した pragma: `#pragma multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME`
- 対象: 全 FORWARD_BASE パス（メイン + 全 Variant、計 11 パス）+ Particle の
  ForwardBase パス（計 12 パス）。
- 理由: `UNITY_LIGHT_PROBE_PROXY_VOLUME` はグラフィックス Tier 設定由来の
  キーワードで、`multi_compile_fwdbase` の展開には含まれません。明示的に
  `multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME` を宣言することで、Tier 設定に
  依存せず LPPV バリアントの生成を保証します。ForwardBase バリアント数は倍増
  しますが、`shader_feature_local` による機能ストリップが効くため実使用の
  バリアント爆発は限定的で、許容範囲と判断しました（`shader_feature` ではなく
  `multi_compile` なのは、LPPV がマテリアル固有ではなくレンダラー単位で
  ランタイムに切り替わるグローバルキーワードのため）。

## サンプラー予算 (VRChat PC 向け)

- LPPV はグローバルサンプラー `unity_ProbeVolumeSH`（フィルタ付き 3D テクスチャ）を
  1 つ追加で消費しますが、消費するのは `UNITY_LIGHT_PROBE_PROXY_VOLUME` の
  ON バリアントのみ、かつレンダラーが実際に LPPV コンポーネントの影響下にある
  ときだけです。
- これはマテリアルの機能トグルではなく Tier 由来のグローバルキーワードのため、
  `NataneToonSamplerBudgetEstimator` の機能→サンプラー表（`FeatureCosts`）の
  パターンには馴染みません。したがって表には追加せず、代わりに
  `LppvProbeVolumeSamplerCost = 1` という説明用定数とコメントを
  `Editor/NataneToon/GUI/NataneToonSamplerBudgetEstimator.cs` に追加しました。
- `BaseSamplerCount = 3`（上限 16）のヘッドルームが最悪ケースの +1 を吸収するため、
  VRChat PC のサンプラー予算（16）を超えることはありません。
- Shader Model 要件: LPPV は SM3.5+（3D 浮動小数点テクスチャ + フィルタリング）で
  利用可能。対象パスは `#pragma target 4.6`（メイン）/ `3.5`（Particle）で要件を満たします。

## 検証項目

Unity 2022.3.28f1（`file:` 依存でワークツリーを参照するスクラッチプロジェクト、
batchmode）で `ShaderUtil.GetShaderMessages` をパッケージ全シェーダーに対して実行。

| # | 検証項目 | 状態 |
| --- | --- | --- |
| 1 | `ShadeSHPerPixel` 相当の共通関数を追加 | ✅ 検証済（`NataneShadeSH`） |
| 2 | `UNITY_LIGHT_PROBE_PROXY_VOLUME` / `unity_ProbeVolumeParams.x` で LPPV 評価、通常時は `ShadeSH9` 維持 | ✅ 検証済 |
| 3 | ForwardBase のみ適用、ForwardAdd へ環境光を重複加算しない | ✅ 検証済（SH ブロックは `UNITY_PASS_FORWARDBASE` 内、pragma も FORWARD_BASE のみ） |
| 4 | VRC Light Volumes を LPPV より優先（二重加算なし） | ✅ 検証済（`_USE_LIGHT_VOLUME` 分岐が SH/LPPV より先） |
| 5 | Fur / Particle / Background の SH 評価を共通関数へ統一 | ✅ 検証済 |
| 6 | LPPV 用 Sampler / Shader Model が VRChat PC のサンプラー予算を超えない | ✅ 検証済（+1 は Base ヘッドルーム内、SM 要件は target で充足） |

### コンパイル検証結果

- パッケージ全シェーダー: **24 本 / エラー 0 / 警告 0**
- C# コンパイル: `grep -cE "error CS|warning CS"` = **0**
- LPPV 分岐（`SHEvalLinearL0L1_SampleProbeVolume` 経路）は、エディタの既定
  グラフィックス Tier が `UNITY_LIGHT_PROBE_PROXY_VOLUME` を有効化するため
  import 時に ON バリアントとしてコンパイルされ、専用のフォースコンパイル用
  テストシェーダーでも 0/0 を確認済み。
