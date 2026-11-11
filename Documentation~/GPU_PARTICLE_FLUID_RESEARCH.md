# GPUパーティクル・流体表現 調査報告 (2026-07)

natane_toon_shaderへのGPUパーティクル/流体シミュレーション同梱可否の調査結果。

## 結論

- **真の流体シミュレーション同梱は非現実的**: (1) 状態保持に必須のCustomRenderTexture自己更新はアバター切替/インスタンス移動でフリーズする未解決バグあり (2) VRCGraphics.Blit/Udonはアバター不可 (3) Compute Shader dispatchはVRChatに公開APIなし
- **同梱に値する方式**: ①状態を持たない頂点アニメGPUパーティクル(時間+頂点シード) ②数式ベースの疑似流体(Perlinノイズ+高さクリップ+Fresnel) ③AudioLink「消費専用」変調 — いずれもアバター/ワールド/Quest全対応、自前実装可能
- **見送り**: CRT ping-pongシミュ、GrabPassレイマーチ式パーティクル(PC専用・不安定・Quest不可)

## 方式別評価

| 方式 | アバター | ワールド | Quest | ランク影響 | 同梱適合 |
|---|---|---|---|---|---|
| stateless頂点アニメ | ○ | ○ | ○ | 軽微(Mesh扱い) | 高 |
| CRT ping-pong | △(初回のみ・切替でフリーズ) | ○ | ×  | 計測外だが重い | 低 |
| AudioLink消費 | ○(サンプルのみ) | ○ | △(専用ドライバ要) | 軽微 | 中〜高 |
| GrabPassレイマーチ | ○(実例あり・MIT) | ○ | × | 重い | 低 |

## 主要根拠

- アバターのコンポーネント制限: https://creators.vrchat.com/avatars/whitelisted-avatar-components/whitelisted-avatar-components/
- Quest制限: https://creators.vrchat.com/platforms/android/quest-content-limitations/
- CRTアバターバグ(未解決): https://vrchat.canny.io/bug-reports/p/custom-render-textures-bugged-on-avatars
- Blitアバター不可: https://feedback.vrchat.com/avatar-30/p/graphicsblit-scripts-for-avatars
- phi16 GPGPU 2023: https://phi16.hatenablog.com/entry/2023/09/25/235421
- 汎用計算解説: https://qiita.com/kajitaj63b3/items/f465164a36403eca7e5e
- 参考OSS: aurycat/GPUParticleBase (MIT), SCRN-VRC/Raymarched-GPU-Particles (MIT), REDSIM/GPUParticleVolumes (要ライセンス確認)

## ライセンス方針

推奨機能は汎用技法のため既存OSSのコピー不要。**自前実装**とし、ライセンス表示義務・曖昧さを回避する。
