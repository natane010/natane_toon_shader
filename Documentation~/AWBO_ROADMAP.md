# Asset Workspace & Build Optimizer — 残課題と次段階の提案

最終更新: 2026-07-18(Stage A〜I 完了時点 / develop `049137e`)

全ステージ(実装順序1〜18)は実装・検証済み。本メモは「まだ改善できない処理」と「次に着手すべき内容」の記録。

## まだ改善できない処理(現状の制約と理由)

| 項目 | 現状 | 理由 / リスク |
|---|---|---|
| `_PBR_LIKE` のDerived評価 | 常時保持(削減対象外) | 複数Property(_ShadingMode/_LookMode/_PbrWeight)からの派生条件が形式化されておらず、誤削除リスクを避けるため安全側に固定。Registry上は `EvaluationKind=Derived` として登録済み |
| AnimationClip由来Keywordの組合せ展開(Aggressive) | 「実在構成+Animation由来Keywordを加えた構成も保持」の保守的近似 | 厳密な組合せ展開は指数的爆発の恐れ。近似は過剰保持側なので安全だが、削減余地を残している |
| 文字列パス参照の検出(Asset Organizer) | 注意喚起の表示のみ | Resources.Load等の文字列参照の静的検出はC#全走査+誤検出リスクが大きい。現状は移動前警告に留める |
| 実ビルド経路の検証 | 純関数+executeMethodまで | 実プレイヤービルド/VRChatアップロードでの実削除・Strict例外発火・レポートJSON実出力は未走行。既定Safe+全保持フォールバックのため失敗時も「最適化されないだけ」で壊れない設計 |
| メインEditor asmdefの肥大(116ファイル) | 未分割 | 分割は大規模ファイル移動を伴うため意図的に見送り。新機能はディレクトリ単位(AssetManagement/SceneWorkspace/MigrationPreview/Workspace/Integration)で境界整理済み=分割の下準備は完了 |
| VRChatビルドのレポート保存 | 次回BeginSession時に前回分を永続化(1ビルド遅延) | VRChatはビルド後コールバック(IPostprocessBuildWithReport)が発火しないため構造的制約 |

## 次段階の提案(優先度順)

1. **実ビルド計測の実施**(最優先・実装不要)
   - 実プロジェクトで Report Only → Safe の順に有効化し、`Library/NataneToon/Reports/` のレポートで削除候補と実削減を確認
   - Package更新前後の Variant数 / Build時間 / Build size 比較(レポート基盤は`previousComparison`/`stageTimings`として実装済み)
   - VRChatアップロード1回目→2回目でレポートJSONが保存されることの確認

2. **asmdef分割**(再コンパイル範囲の縮小)
   - 推奨境界: Editor.Core(Localization/共通UI部品)/ Editor.AssetManagement / Editor.SceneWorkspace / Editor.BuildOptimization(Integration配下)/ Editor.UI(GUI/)/ 既存の Tools / Migration / VRChat / Tests
   - ファイル移動はGUID維持(`AssetDatabase.MoveAsset`)で。Asset Organizerを自家利用できる

3. **`_PBR_LIKE` Derived評価器**
   - NataneToonShaderGUI の LookMode/ShadingMode 連動条件を `NataneShaderFeatureRegistry` の CustomEvaluator として形式化 → SafeStrippable化で削減対象に昇格

4. **AnimationClip由来の厳密展開(Aggressive強化)**
   - クリップ単位で「同時にONになり得るKeyword集合」を推定し、実在構成×クリップ集合の直積を上限付きで展開(上限超過時は現行近似へフォールバック)

5. **未知Keywordの分類UI**
   - Audit が検出した未知Keywordを、人が Toggle/Enum/Composite/Derived/常時保持/対象外 へ分類してRegistryへ取り込むエディタフロー(自動登録は仕様で禁止のため、分類支援のみ)

6. **Asset Organizerの文字列参照スキャナ(オプトイン)**
   - `Resources.Load("...")` / Addressablesキーの静的grepを「参考情報」として提案画面に表示(ブロックはしない)

7. **Scene Profileの拡張**
   - Build Profile(ビルド対象シーン集合)の実装(SPECで「Scene Profileとは別」と規定済みの未着手部分)

## 参照

- 設定: `Project Settings > Natane Toon`(Optimization Mode / Strict / 常時保持 / SVC / Runtime動的宣言)
- 統合UI: `Tools > Natane > ワークスペース > Asset Workspace & Build Optimizer`
- 比較レポート: `Tools > Natane > ビルド最適化 > HLSL方式との比較レポート`(Safe⊇HLSLの根拠データ)
- 新規導入時の既定: Mode=Safe / Prewarm=OFF / HLSL Guard=Off / Unknown=Keep / Snapshot失敗=Keep All / Aggressive=OFF
