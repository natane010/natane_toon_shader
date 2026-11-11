# P8: ディゾルブ表現とアニメーションのオーサリング 実装仕様

ディゾルブ（溶解して消える／現れる）表現と、その**アニメーション**を簡単に作れるようにする。

- 新規ツール: `Editor/NataneToon/Tools/DissolveStudioWindow.cs`
- あわせて修正3件（F2 / F3 / F4）
- Stage: A'（既存シェーダーへの新キーワード追加は F2 のみ）

---

## 1. 現状の調査結果

### 1.1 シェーダー側は充実している

`_DISSOLVE` は18プロパティを持ち、表現としては十分。

| 分類 | プロパティ |
|---|---|
| 基本 | `_DissolveAmount` / `_DissolveTex` / `_DissolveBlend` / `_DissolveBlendMode` / `_DissolveBlur` |
| エッジ | `_DissolveEdgeWidth` / `_DissolveEdgeColor`(HDR) / `_DissolveEdgeIntensity` |
| UVアニメ | `_DissolveTexScrollSpeed` / `_DissolveTexRotateSpeed` |
| 座標モード | `_DissolveCoordMode`(UV/World/Local) / `_DissolveWorldAxis` / `_DissolveWorldMin` / `_DissolveWorldMax` / `_DissolveNoiseBlend` |
| マスク | `_DissolveMask` / `_UseDissolveMask`（**死んでいる。§F3**） |
| 音連動 | `_AudioLinkDissolve`（**死んでいる。§F4**）/ `_AudioLinkDissolveIntensity` / `_AudioLinkDissolveBand` |

World/Local 座標モードがあるため「足元から上へ溶ける」「胸から放射状に消える」といった
方向性のある溶解も既に表現可能（`NataneToonFragment.hlsl:2605-2621`）。

### 1.2 素材生成もある

`Editor/NataneToon/Tools/DissolvePatternGenerator.cs`:
Perlin / Voronoi / Cellular / Random / Gradient の5種、256〜1024、Scale / Contrast 調整、
プレビューとマテリアルへの直接割当あり。`EffectStudioWindow` の「ディゾルブ」タブから起動できる。

### 1.3 足りないのは「時間変化を作る」部分

**ここが完全に空白**。

| 必要なもの | 現状 |
|---|---|
| `AnimationClip` を生成するツール | **無い**。`AnimationUtility` の使用箇所は `NataneMigrationService`（既存クリップの binding をプロパティ改名に追従させる rebind）と `NataneBuildUsageSnapshot`（使用状況の走査）だけで、新規オーサリングは1箇所も無い |
| Animator レイヤー / VRC Expression Menu の生成 | **無い**。`VRChatIntegrationWindow` のタブは Light Volumes / パッケージ設定 / 自動検出の3つのみ |
| シェーダー内で自走させる仕組み | `_FX_MODULATOR` があるが、**Target enum に Dissolve が無い**（§F2） |

つまりユーザーは、`_DissolveAmount` を 0→1 に動かすために
Animation ウィンドウでマテリアルプロパティのカーブを手打ちし、
Animator レイヤーと VRChat の Expression Parameter / Menu を自力で組む必要がある。
「ディゾルブで消えるアニメーション」の実作業の**ほぼ全部が手作業**という状態。

---

## 2. 修正項目

### F2. FX Modulator に Dissolve ターゲットを追加

`NataneToonShader.shader:994` の現状:

```
[Enum(None,0,EmissionIntensity,1,HueShift,2,RimIntensity,3,OutlineWidth,4,LineBoilStrength,5,
      TopographicOffset,6,ShapedHighlightIntensity,7,CausticsIntensity,8,LenticularBlend,9,
      SpecularIntensity,10,MatCapIntensity,11,AlphaFade,12)] _FXModTarget0
```

`Documentation~/PROPOSED_EXPRESSION_FEATURES.md` の Target候補には Dissolve が挙がっていたが、
実装時に落ちている。最も近い `AlphaFade`(12) は単純なアルファ減衰で、**エッジ発光を伴わない**ため
ディゾルブの代替にはならない。

**対応**: `DissolveAmount,13` を追加し、`NataneToonFXModulator.hlsl` で
`_DissolveAmount` へ加算するパスを通す。

```hlsl
// 適用は Dissolve の評価より前でなければならない（Fragment.hlsl:2591 の #ifdef _DISSOLVE ブロック直前）
#if defined(_FX_MODULATOR) && defined(_DISSOLVE)
    dissolveAmountEffective = saturate(_DissolveAmount + NataneFXModValue(NATANE_FXMOD_TARGET_DISSOLVE));
#endif
```

これにより **Animator を一切使わずに** 時間・音・カメラ距離・視線角度で自走するディゾルブが作れる。
Quest でも Animator レイヤー追加なしで済むため軽い。

対象は `_FX_MODULATOR` と `_DISSOLVE` の両方を持つバリアント。
新キーワードは増えず、既存 enum への追加なので変種数は増えない。

### F3. `_UseDissolveMask` が完全に死んでいる

`_UseDissolveMask` は本体 + Variants の**計12ファイルの Properties に
`[Toggle(_DISSOLVE_MASK)]` として宣言されている**が、

- HLSL からの参照: **無し**
- GUI からの参照: **無し**
- `NataneShaderKeywordSynchronizer.KeywordMappings` への登録: **無し**
- `NataneShaderFeatureRegistry` への登録: **無し**
- どのパスの `#pragma shader_feature_local`: **無し**

一方 `NataneToonFragment.hlsl:2597-2600` は

```hlsl
half dissolveMaskValue = 1.0;

// Apply mask texture
dissolveMaskValue = NATANE_SAMPLE_SHARED_R(_DissolveMask, _MainTex, uv);
```

と、**トグルを無視してマスクを常時サンプル**している。

結果:
- インスペクタの「Use Dissolve Mask」チェックボックスは**何も変えない**
- `[Toggle]` が `_DISSOLVE_MASK` キーワードをマテリアルに立てるが、誰もコンパイルしないため
  **死んだキーワードがマテリアルに残る**
- マスクを使わない場合でもテクスチャフェッチ1回を常に払っている（既定は `"white"` なので絵は正しい）

**対応**（後方互換を保つ方向で修正する）:

```hlsl
half dissolveMaskValue = 1.0;
if (_UseDissolveMask >= 0.5)
{
    dissolveMaskValue = NATANE_SAMPLE_SHARED_R(_DissolveMask, _MainTex, uv);
}
```

- Uniform 分岐にして、キーワードは増やさない（変種爆発を避ける）
- `[Toggle(_DISSOLVE_MASK)]` → `[Toggle]` へ変更し、死んだキーワードを立てないようにする
- 既定値は `0` なので、**既存マテリアルの見た目は変わらない**
  （マスク未設定＝`"white"`＝乗算1.0 で、サンプルしてもしなくても同じ）
- マスクを実際に設定していたユーザーは、トグルをONにする必要がある。
  移行を助けるため、`MaterialValidator` に
  「`_DissolveMask` が既定以外なのに `_UseDissolveMask` が 0」の検出を追加する

### F4. `_AudioLinkDissolve` も宣言のみで未参照

`NataneToonShader.shader:627` の `[Toggle(_AUDIOLINK_DISSOLVE)] _AudioLinkDissolve` も
どこからも参照されていない。実装（`NataneToonFragment.hlsl:2322`）は
`_AudioLinkDissolveIntensity > 0.001` を直接見ている。

**対応**: F3 と同じ扱い。`[Toggle]` へ変更して死んだキーワードを立てないようにするか、
`_AudioLinkDissolveIntensity` のゲートとして実際に参照する。
UIの意味を保つ後者を推奨する。

### F2〜F4 から見えた横断的な問題

`Properties` の `[Toggle(KEYWORD)]` が宣言するキーワードを、**どのパスもコンパイルしていない**
という状態は、既存の `NataneShaderUpdateAudit` では検出できない
（監査は「`#pragma` にあるがレジストリ未登録」の方向を見ており、逆方向を見ていない）。

[NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md](NPR2026_ADDENDUM_WORKFLOW_AUTOMATION.md) の **W3**
（パリティ検査）に、この逆方向の検出を追加項目として含める。
F3 / F4 はその検査を入れれば自動で見つかった類の不整合である。

---

## 3. P8 仕様: Dissolve Studio

`Editor/NataneToon/Tools/DissolveStudioWindow.cs`（新規）。
既存の `DissolvePatternGenerator` は素材生成に専念させ、こちらは**表現とアニメーションの組み立て**を担う。
`EffectStudioWindow` の「ディゾルブ」タブから起動できるようにする。

### 3.1 タブ構成

```
Dissolve Studio
├─ プリセット      … ルックを選ぶ
├─ プレビュー      … 0→1 をスクラブして確認
├─ アニメーション  … AnimationClip を生成
└─ VRChat         … Animator レイヤー / Expression Menu を生成
```

### 3.2 プリセット

素材（`_DissolveTex`）とパラメータの組を1つのプリセットとして持つ。
テクスチャが必要なものは `DissolvePatternGenerator` を内部呼び出しして自動生成する。

| プリセット | ノイズ | 座標 | エッジ | 用途 |
|---|---|---|---|---|
| **焼失 / Burn** | Perlin (scale 8) | UV | 幅 0.08 / 橙 HDR 強度 4 | 紙や布が焼けて消える |
| **転送 / Teleport** | Gradient | World Y | 幅 0.15 / シアン 強度 6 | 足元から上へ光の帯で消える |
| **データ化 / Digital** | Cellular (scale 16) | UV | 幅 0.02 / 白 強度 8 | 四角いブロックで分解 |
| **風化 / Erode** | Voronoi (scale 5) | Local Y | 幅 0.2 / 灰 強度 1 | 砂のように崩れる |
| **霧散 / Mist** | Perlin (scale 3) + Blur 0.6 | UV | 幅 0.3 / 淡色 強度 2 | ふわっと薄れて消える |
| **出現 / Emerge** | 上記いずれか + 反転 | 任意 | 同上 | 1→0 で現れる方向 |

プリセット適用は `Undo.RecordObject(material, "Apply Dissolve Preset")` を通す。

### 3.3 プレビュー（スクラブ）

- `_DissolveAmount` を 0→1 のスライダで動かし、シーンビューで確認
- ウィンドウを閉じる／リセットで**必ず元の値へ戻す**
  （プレビュー開始時の値を保持し、`Undo` にも積む）
- 「再生」ボタンで `EditorApplication.update` を使って指定秒数で自動スクラブ

### 3.4 アニメーション生成（本題）

| 入力 | 内容 |
|---|---|
| 対象 | Renderer（複数選択可）または GameObject ルート以下を自動収集 |
| 方向 | 消える(0→1) / 現れる(1→0) / 往復 |
| 長さ | 秒。既定 1.0 |
| イージング | Linear / EaseIn / EaseOut / EaseInOut / Step(コマ打ち) |
| ホールド | 消え切ったあとの保持秒数 |
| 同時アニメ | `_DissolveEdgeIntensity` の山型カーブ、`_DissolveEdgeColor` のHDR強度変化 |
| トグル制御 | クリップ先頭で `_Dissolve` を 1、末尾で 0 に戻す（**常時コストを避ける**） |

生成の要点:

- バインディングは `Renderer` 相対パスに対して
  `EditorCurveBinding.PPtrCurve` ではなく **`material._DissolveAmount` 形式のフロートカーブ**
  （`AnimationUtility.SetEditorCurve` / `EditorCurveBinding` の
  `propertyName = "material._DissolveAmount"`）
- 複数マテリアルスロットを持つ Renderer は `material.` ではなくインデックス付きの
  `material[N]._DissolveAmount` を使う必要があるため、スロット数を見て自動で切り替える
- `Step(コマ打ち)` は `AnimationUtility.SetKeyLeftTangentMode` で Constant にする
  （[NPR2026_PLAN.md](NPR2026_PLAN.md) の Line Boil と同じ「低fpsで見せる」思想）
- 生成先はプロジェクト側（`Assets/` 配下）。**パッケージ内には書かない**
- `Undo.RegisterCreatedObjectUndo` を通す

**既存の資産との噛み合わせ**: `NataneMigrationService` は
`AnimationUtility.GetCurveBindings` / `SetEditorCurve` を使って
既存クリップのプロパティ名変更に追従する仕組みを持っている（`NataneMigrationService.cs:542-552`）。
生成したクリップも同じ経路で保護されるため、将来 `_DissolveAmount` を改名しても追従できる。

### 3.5 VRChat 連携

| 生成物 | 内容 |
|---|---|
| AnimatorController レイヤー | `Off` → `DissolveOut` → `Hold` → `DissolveIn` の遷移。Write Defaults オフ |
| パラメータ | bool トグル（消えたまま維持）または float ラジアル（手動スクラブ） |
| VRC Expression Parameters / Menu | パラメータとメニュー項目を追加 |

- **Modular Avatar が入っている場合は MA コンポーネント（`MA Merge Animator` /
  `MA Parameters` / `MA Menu Item`）で非破壊構成にする**
- 入っていない場合は FX レイヤーへ直接書き込むが、**書き込み前にバックアップを取り**、
  その旨をダイアログで明示する
- VRCSDK / Modular Avatar の有無は既存の `NataneDependencyStatus` /
  `NataneDependencyInstaller` で判定できるので、それを使う
- パラメータのメモリコスト（bool 1bit / float 8bit）を表示し、
  VRChat のパラメータ上限に対する残量を出す

### 3.6 自走モード（F2 前提）

Animator を使わず、シェーダー内で完結させる構成をプリセットで提供する。

| プリセット | FX Modulator 設定 |
|---|---|
| 呼吸するように薄れる | Source = Sine / Target = DissolveAmount / Amount 0.3 / Speed 0.5 |
| 音に合わせて分解 | Source = AudioBass / Target = DissolveAmount / Amount 0.6 |
| 近づくと現れる | Source = CameraDistance / Target = DissolveAmount / Invert ON |
| 一定間隔で明滅 | Source = Pulse / Target = DissolveAmount / Speed 0.2 |

Animator レイヤーもパラメータも消費しないため、
**Quest やパラメータ枠が厳しいアバターではこちらを推奨**する旨をUIに書く。

---

## 4. 検証

1. F3 修正後、`_UseDissolveMask = 0`（既定）で既存マテリアルの見た目が変わらないこと
2. F3 修正後、マスクを設定してトグルONで実際にマスクが効くこと
3. F2 追加後、`_FX_MODULATOR` のみ有効／`_DISSOLVE` のみ有効／両方有効の3通りで
   コンパイルが通ること（`#if defined(_FX_MODULATOR) && defined(_DISSOLVE)` のガード確認）
4. 生成した AnimationClip を Animator で再生し、`_DissolveAmount` が意図どおり動くこと
5. マテリアルスロットが2つ以上の Renderer で `material[N].` バインディングが正しいこと
6. Modular Avatar 有／無の両方で VRChat 連携が完結すること
7. 2022.3.28f1 / 6000.0.55f1 の batchmode で error / warning ゼロ

## 5. 完了条件

- プリセットを選ぶだけでディゾルブのルックが決まる（テクスチャも自動生成される）
- 「消える」アニメーションが AnimationClip として1クリックで生成できる
- VRChat のトグル／ラジアルとして動く構成まで自動で組める（MA があれば非破壊）
- Animator を使わない自走モードがプリセットで選べる
- F3 / F4 の死んだトグルが解消され、後方互換が保たれている
- W3 のパリティ検査に「Properties の Toggle キーワードが未コンパイル」の検出が入っている
- EN/JP 両対応、Undo 対応
- シェーダーコンパイルの error / warning がゼロ
