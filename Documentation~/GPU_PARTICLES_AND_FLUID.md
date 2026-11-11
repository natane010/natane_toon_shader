# GPU パーティクル & フェイクフルイド (GPU Particles & Fake Fluid)

VRChat アバターセーフな、**完全ステートレス**な2種類の演出用シェーダーです。
シミュレーション・CustomRenderTexture・カメラ・GrabPass・実行時スクリプトを一切
使用しません。すべてのアニメーションは `_Time` と頂点シード (メッシュに焼き込んだ
乱数) から計算されるため、アバターにそのまま乗せても安全です。

---

## 1. Natane/Effects/GPU Particles (Stateless)

`Shaders/NataneToon/Effects/NataneGPUParticles.shader`

静的な「クアッドクラウド」メッシュを頂点シェーダーでアニメーションさせる
パーティクルシェーダーです。各パーティクルの位置は
`_Time.y * _Speed + seed * _CyclePeriod` の純粋関数で、`frac()` によりシームレスに
ループします。ビルボードはビュー空間の right/up ベクトルで常にカメラを向きます。

### 使い方

1. `Tools > Natane > メッシュ Mesh > GPUパーティクルメッシュ生成 GPU Particle Mesh`
   でクアッドクラウドメッシュを生成・保存する (下記参照)。
2. 生成したメッシュを空の GameObject の MeshFilter に割り当て、MeshRenderer を付ける。
3. マテリアルに `Natane/Effects/GPU Particles (Stateless)` を割り当てる。

### モーションモード (_MotionMode)

| 値 | モード | 用途 |
|----|--------|------|
| 0 | Rise  | 火の粉 / 蛍 — 上昇 + サイン揺らぎ |
| 1 | Fall  | 桜吹雪 / 雪 — 下降 + 横ドリフト + ビルボード回転 |
| 2 | Orbit | キラキラ — オブジェクト原点周りを周回 |
| 3 | Burst | シード周期で中心から放射 |

### 主なプロパティ

- `_ParticleSize` パーティクルサイズ / `_Speed` 速度 / `_CyclePeriod` ループ秒数
- `_Spread` エミッション範囲 (XYZ ボックス)
- `_Color` (HDR) × 頂点カラー × `_MainTex` (デフォルト white = ソフト円)
- `_FadeIn` / `_FadeOut` ライフ中のアルファフェードイン/アウト
- `_SizeOverLife` ライフ中のサイズ変化 (smoothstep 近似)
- `_SwayAmount` / `_SwayFrequency` 揺らぎ・回転

### メッシュ規約

- 1パーティクル = 1クアッド (4頂点)。4頂点すべてが同じ中心座標を持ち、角の
  オフセットは UV0 と `_ParticleSize` から頂点シェーダーで復元。
- **UV0**: クアッドの角 (各成分 0 or 1)。スプライト UV も兼ねる。
- **UV1.x**: パーティクル乱数シード [0,1)。
- **UV1.y**: パーティクルインデックス正規化 [0,1]。
- **頂点カラー**: パーティクル毎のティント。

### レンダリング

Additive ブレンド / ZWrite Off / Cull Off / Queue Transparent /
`VRCFallback = Particle` / target 3.5 / instancing・stereo・fog 対応。

---

## 2. Natane/Effects/Fake Fluid

`Shaders/NataneToon/Effects/NataneFakeFluid.shader`

数式のみで「容器の中の液体」を表現するシェーダーです。液面は `_FillAmount` で
決まるクリップ平面で、`worldPos.xz` を使ったサイン波 (2〜3オクターブ) で揺れます。
シミュレーションはありません。

### 使い方

1. 容器メッシュ (コップ・瓶など、閉じた形状が望ましい) にマテリアルを割り当て、
   シェーダーを `Natane/Effects/Fake Fluid` にする。
2. `_FillMin` / `_FillMax` をメッシュのオブジェクト空間 Y 範囲に合わせる。
3. `_FillAmount` (0〜1) で液面の高さをアニメーション/調整する。

### 仕組み

- オブジェクト空間 Y が液面より上のフラグメントを `clip()` で切り取る。
- **Cull Off + VFACE**: 表面 = 液体本体 (2段トゥーン + フレネルリム)、
  裏面 (切り開いた上面から見える) = フラットな `_SurfaceColor` の液面キャップ。
  これが「フェイク液体」の定番テクニック。
- 液面付近には `_FoamColor` / `_FoamWidth` で泡ライン。

### 主なプロパティ

- `_Color` 液体色 / `_FillAmount` / `_FillMin` / `_FillMax`
- `_WobbleStrength` / `_WobbleSpeed` / `_WobbleScale` 揺れ
- `_SurfaceColor` 液面キャップ色
- `_FoamColor` / `_FoamWidth` 泡ライン
- `_ShadowColor` / `_ShadowThreshold` 2段トゥーン
- `_RimColor` / `_RimPower` フレネルリム

### レンダリング

クリップ (カットアウト) 方式 / Queue AlphaTest / ZWrite On / Cull Off /
`VRCFallback = ToonCutout` / target 3.5 / instancing・stereo・fog 対応。

---

## 3. メッシュ生成ツール

`Editor/NataneToon/Tools/GPUParticleMeshGenerator.cs`
メニュー: `Tools > Natane > メッシュ Mesh > GPUパーティクルメッシュ生成 GPU Particle Mesh`

- **パーティクル数** (既定 256、最大 4096)
- **スプレッド** エミッションボックス
- **乱数シード** 再現可能な乱数
- **基本ティント / ばらつき** 頂点カラーへ焼き込み

`SaveFilePanelInProject` で `.asset` として保存します。

---

## アバターセーフティの根拠 (ステートレス設計)

VRChat のアバターでは実行時スクリプトや動的レンダーテクスチャが制限されます。
本シェーダー群はフレーム間の状態を一切保持せず、すべての運動を `_Time` と
頂点に焼き込んだシード値の純関数として計算します。これにより:

- ランタイムスクリプト不要 (Animator / スクリプト同期なしで動作)。
- CustomRenderTexture / カメラ / GrabPass 不要。
- `frac()` によりシームレスループ。フレームレート非依存。

## AudioLink について

GPU Particles シェーダーは `[Toggle(_AUDIOLINK)]` でオプションの AudioLink 連動を
持ちます。有効時はグローバルテクスチャ `_AudioTexture` を UV `(0.008, band/4)` で
サンプリングし、そのバンドレベルでサイズ/エミッションを増幅します。AudioLink が
ワールドに存在しない場合、グローバルは黒 (0) を返すため `max()` により中立
(倍率 1.0) にフォールバックし、正しくレンダリングされます。

---

## English Summary

Two fully **stateless**, VRChat avatar-safe effect shaders (Built-in RP):

- **`Natane/Effects/GPU Particles (Stateless)`** — vertex-animated billboard
  particles on a static quad-cloud mesh. Positions are pure functions of
  `_Time.y * _Speed + seed * _CyclePeriod` (seamless `frac()` loop). Four motion
  modes: Rise, Fall, Orbit, Burst. Mesh convention: 4 verts per particle share
  the center position; UV0 = quad corner (0/1) + sprite UV; UV1.x = seed;
  UV1.y = normalized index; vertex color = tint. Additive, ZWrite Off, Cull Off,
  Queue Transparent, `VRCFallback = Particle`. Optional AudioLink (`_AUDIOLINK`)
  scales size/emission from `_AudioTexture` and falls back neutrally via `max()`.
- **`Natane/Effects/Fake Fluid`** — math-only "liquid in a container". A clip
  plane at `_FillAmount` (remapped over `_FillMin`/`_FillMax`) with 2-3 octaves
  of sine wobble on `worldPos.xz`. Cull Off + VFACE renders back faces as a flat
  `_SurfaceColor` cap (fake-liquid surface). Fresnel rim, 2-step toon shading,
  optional foam line. Clip-based cutout, Queue AlphaTest, `VRCFallback = ToonCutout`.
- **Mesh generator** — `Tools > Natane > メッシュ Mesh > GPUパーティクルメッシュ生成
  GPU Particle Mesh` builds the quad-cloud asset (count, spread, seed, tint).

No simulation, no CustomRenderTexture, no camera, no GrabPass, no runtime script —
everything is derived from `_Time` and baked per-vertex seeds.
