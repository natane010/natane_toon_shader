# Changelog

All notable changes to Natane Toon Shader will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- **Cutout / Cutout Lite**: `#pragma fragment` がコア側の `frag` を指しており `_Cutoff` によるクリップが実行されていなかった問題を修正。ShadowCaster も `_Color.a` を考慮するように変更。
- **手描きアウトライン**: Lite / Cutout / Cutout Lite / Transparent / Transparent Lite / ScreenEdge Split でヘルパー関数が未定義でコンパイルエラーになる問題を修正。
- **3D LUT**: `_LUT3DTex`（NOSAMPLER 宣言）を `sampler2D` 引数で受けていたため D3D11 でコンパイルエラーになる問題を修正。
- **AudioLink**: 全バンドが Bass の履歴を参照していた座標計算を修正（行 = バンド、列 = 履歴）。Chronotensity の参照位置も `ALPASS_CHRONOTENSITY` に合わせて修正。
- **テッセレーション**: Hull/Domain ステージでインスタンス ID が伝播せず、GPU インスタンシング時に行列が壊れる問題を修正。
- **Fur**: 重力と風をワールド空間で適用するように変更（回転したオブジェクトで横方向に垂れていた）。
- **Screen FX Overlay**: GrabPass テクスチャをスクリーンスペースマクロで参照し、VR Single Pass Instanced で破綻しないように修正。
- **Shader Variant Stripper**: マテリアルの全キーワード集合とパス単位の変種キーを完全一致で比較していたため、OUTLINE / FORWARD_ADD の変種がほぼ全て除去されていた問題を修正。同一セッション内の2回目以降のビルドでホワイトリストが更新されない問題も修正。
- **エディタ**: 一部ツールの `[MenuItem]` が旧英語グループのままで、メニューが二重化し Dashboard から開けなかった問題を修正。
- **エディタ**: Material Validator の自動修正、Shadow Adjustment Wizard のランプ生成、Look Mixer の Legacy 自動移行に Undo を追加。
- **エディタ**: 起動時キーワード同期がプロジェクト全体の `SaveAssets()` を呼んでいた問題を、変更マテリアルのみ保存するように修正。

### Changed
- **FORWARD_ADD パス**: ForwardBase 専用の `_RIM_LIGHT` / `_RIM_LIGHT_2` / `_OFFSET_RIM_LIGHT` / `_ENV_RIM` / `_SSS` を各バリアントの FORWARD_ADD から削除し、変種数を削減。Fur バリアントの FORWARD_BASE / FORWARD_ADD から `_FUR` を削除。
- **package.json**: `unity` を `2022.3` に更新（ローカルキーワード上限の都合で 2021.2 未満では動作しないため）。
- **配布物**: 開発用ファイル（`Whiteboard.md`、`unique_same_l_strings.txt`）、`Tests/`、孤立 `.meta` を VPM zip から除外。
- **ライセンス表記**: `THIRD_PARTY_NOTICES.md` を追加し、Morgan McGuire のノイズ関数と Björn Ottosson の OKLab の帰属を明記。

---

## [1.5.13] - 2026-04-21

### Fixed
- **アウトラインマスク**: トグル UI の欠落により `_OUTLINE_MASK` キーワードが有効化されない問題を修正。シェーダー GUI にアウトラインマスク/幅マップ用のトグルを追加。
- **アウトラインマスクの確実性向上**: 頂点段階で outline width にマスクを乗算して物理的にアウトラインを無効化。clip 処理を `_OutlineColor.a` に依存しない形に変更。全10シェーダーバリアントに反映。

### Added
- **キーワード自動同期**: エディタ起動/スクリプトリロード時に全 Natane マテリアルのキーワードを自動同期。
- **変更検知による自動同期**: マテリアルの float プロパティやキーワードが変更された瞬間に再同期（`Undo.postprocessModifications` 監視）。`Tools/Natane/Fix All Material Keywords` の手動実行が不要に。

---

## [1.5.12] - 2026-04-02

### Fixed
- **ダッシュボードショートカット**: Ctrl+J に変更（Ctrl+DはUnity標準のDuplicateと競合するため）

---

## [1.5.11] - 2026-04-02

### Fixed
- **ダッシュボードショートカット**: D キー単押しから Ctrl+D に変更（押し間違い防止）

---

## [1.5.10] - 2026-03-27

### Added
- **High quality map bake**: Added editor-side supersampling, temporary mesh subdivision, posed renderer mesh baking, and normal-aware downsampling to the Map Generator workflow for cleaner VCC-ready texture outputs.

### Changed
- **VCC/VPM package metadata**: Updated `package.json` to `1.5.10` for the next VCC/VPM release.
- **README version labels**: Synchronized the public version badge and package version text with the new VCC/VPM package version.

## [1.5.0] - 2026-03-13

### Added
- **VRChat Mirror / Camera Control**: New per-material mirror and camera visibility toggle. Supports Mirror Only, Non-Mirror Only, Camera Only, Non-Camera Only modes for all 10 shader variants.
- **VRChat Shader Globals**: Declared all 9 official VRChat shader globals (`_VRChatMirrorMode`, `_VRChatCameraMode`, `_VRChatCameraMask`, `_VRChatFaceMirrorMode`, `_VRChatMirrorCameraPos`, `_VRChatScreenCameraPos`, `_VRChatScreenCameraRot`, `_VRChatPhotoCameraPos`, `_VRChatPhotoCameraRot`).
- **Mirror detection helpers**: `NataneIsMirror()`, `NataneMirrorSign()`, `NataneIsCamera()` utility functions for consistent mirror/camera state checking across all shader modules.

### Fixed
- **Mirror rendering: MatCap UV flip** — MatCap textures no longer appear left-right reversed in VRChat mirrors.
- **Mirror rendering: AngelRing flip** — Angel Ring (hair highlight) no longer shifts to the wrong side in mirrors.
- **Mirror rendering: OffsetRimLight flip** — Offset Rim Light direction and position are now correct in mirrors.
- **Mirror rendering: Eye Parallax flip** — Eye parallax depth offset no longer reverses in mirrors.
- **Mirror rendering: Outline Desktop mirror** — Fixed outline normal X multiplier using `lerp(1,-1,val)` which produced -3.0 instead of -1.0 when `_VRChatMirrorMode=2` (Desktop mirror). All 10 shader variants fixed.
- **Mirror rendering: Global detection** — `_VRChatMirrorMode` is now always declared globally, enabling mirror compensation even when Mirror Control feature is disabled.

## [1.4.8] - 2026-03-13

### Fixed
- **ForwardAdd shadow color leakage**: Eliminated shadow color bleeding in ForwardAdd pass that caused multiple colored point lights to wash the entire model in a mixed color (purple wash). ForwardAdd now outputs zero on the shadow side, matching the vertex light pixel-precision path.
- **Comprehensive UI improvements**: Fixed 25 UI/UX issues across the editor tooling including exception handling in migration card, foldout state persistence, search-time performance visibility, Look Mixer property safety, localization gaps, and disabled-state clarity.
- **Migration tool localization**: Added missing Japanese translations for migration tool buttons and messages.
- **Batch operation safety**: Added confirmation dialog for workflow switches and Undo grouping for batch auto-fix operations.

### Added
- **NataneUIConstants**: Centralized UI constants (button heights, spacing, window sizes) for consistent editor tool layout.
- **Prefab material pagination**: Large prefab material lists now paginate at 20 items per page.
- **Workflow navigation**: Direct "Open Workflow Settings" button in Look Mixer when lilToon migration mode is active.
- **Dependency warning dismiss**: Users can now dismiss the dependency installer status notification.

### Changed
- **Search result ordering**: Reorganized inspector search results by category importance (Core → Effects → Advanced).
- **Foldout state validation**: Added key validation on load to prevent null references from version mismatches.

### Improved
- **Inspector UX Phase 2 (P-1~P-8)**: Quick Setup visual preview descriptions, sampler budget reduction suggestions, Legacy LookMode upgrade button, sub-group header visual distinction, narrow-view tab labels, dark theme ON/OFF badges, responsive Current State Row, concise HelpBox messages.
- **Inspector UX Phase 3 (P-9~P-22)**: Quick Setup wizard mode (guided 3-step / show-all toggle), first-run onboarding panel, responsive Feature Overview grid (2/3/4 columns), cross-tab search label, dynamic Current State Row label width, section jump menu, WCAG-compliant performance rating (shape+color), section header icons, disabled feature hints, ranked search results, preset parameter tooltips, Undo group names, dark theme contrast enhancement, Ctrl+1~5 tab shortcuts.
- **Game Character Style preset**: One-click setup with 2-step shadows, rim light, specular, and texture-linked outline for game-character toon rendering.
- **Toon shading defaults**: Improved default values for better out-of-box visual impact (ShadowSteps 2→3, ShadingGradientWidth 0.2→0.5, ShadowBlend 0→0.1, LitSoftness 0→0.1).
- **Quick Setup foldable**: Quick Setup section now uses DrawBoxedSection pattern for consistent fold behavior.

## [1.4.7] - 2026-03-11

### Fixed
- **Avatar pixel vertex light response**: Allowed the `_PIXEL_VERTEX_LIGHTS` path to run independently of the `VERTEXLIGHT_ON` variant and aligned it with the final shading normal so skinned/avatar materials can react more consistently to point and spot lights.
- **Primary vertex light double counting**: Excluded the fallback primary vertex light from the pixel-precision vertex-light accumulation path to prevent the same point light from washing the whole material twice.

## [1.4.6] - 2026-03-10

### Fixed
- **Pixel-precision vertex light directionality**: Adjusted pixel-precision point/spot light shaping so additional vertex lights respect surface direction more clearly instead of tinting the whole material uniformly.
- **StandardToon compatibility for pixel vertex lights**: Kept the pixel-precision vertex light path aligned with Toon/Gradient mode selection without unintentionally pushing StandardToon materials toward gradient-style response.

## [1.4.5] - 2026-03-10

### Fixed
- **VRChat whiteout reduction**: Reworked diffuse lighting composition to compress over-bright direct/additional light before it washes albedo to white in bright worlds.
- **Light Volume natural blend stability**: Reduced double-counting risk in VRC Light Volume environments by treating Light Volume as softer environment fill instead of a hard brightness floor.
- **ForwardAdd overbright accumulation**: Limited additional-light pass output and disabled repeated rim/SSS-style buildup from extra lights to stabilize multi-light VRChat worlds.
- **Brightest vertex light fallback**: Corrected the fallback vertex-light color to use attenuated light color instead of the unattenuated source color.

### Changed
- **Safer default lighting values**: Lowered default peak-lighting values for GI, light color max, additional light intensity, and specular intensity across the main shader and major packaged variants.
- **VRChat validation support**: Added a dedicated whiteout checklist document for bright-world, Light Volume, LTCGI, and multi-light verification.

## [1.4.4] - 2026-03-10

### Fixed
- **Editor localization consistency**: Normalized Japanese UI labels across batch conversion, migration, preset browser, asset checking, shader tools, and unified material editing windows.
- **Preset browser layout consistency**: Kept the material action area visible in a consistent layout even when no material is selected, using disabled controls instead of switching to a different panel layout.

### Changed
- **Package version**: Prepared the package metadata for the next VCC/VPM release as `1.4.4`.

## [1.4.3] - 2026-03-10

### Added
- **Texture Generator workflow improvements**: Added stroke history, shortcut profile, brush stabilizer, line drawing assist, value picker, and brush-active canvas navigation to improve parity with dedicated paint tools.
- **Asset indexing and build preparation**: Added Natane asset index services, shader catalog support, and build preparation settings/hooks to reduce editor-side asset lookup cost and standardize pre-build checks.
- **Release handoff documents**: Added research, implementation, validation, and publish notes under `handoff/` for repeatable release preparation.

### Fixed
- **Unified Material Editor compile recovery**: Restored missing helper methods and preset type resolution so the editor window can compile again after text cleanup.
- **Editor text readability**: Normalized several mojibake-affected menu labels and tool headers in editor tooling to readable UTF-8 strings.

### Changed
- **Package version**: Prepared the package metadata for the next VCC/VPM release as `1.4.3`.

## [1.4.2] - 2026-03-08

### Optimized
- **サンプラースロット最適化**: 7テクスチャを `UNITY_DECLARE_TEX2D_NOSAMPLER` に移行し、サンプラー使用数を7個削減（DX11上限16個に対するマージン拡大）
  - SSS: `_ThicknessMap`, `_SSSMask` → `_MainTex` サンプラー共有
  - Parallax: `_ParallaxMap` → `_MainTex` サンプラー共有 + `tex2Dgrad` 移行
  - Dissolve: `_DissolveMask` → `_MainTex` サンプラー共有
  - Refraction: `_RefractionMask` → `_MainTex` サンプラー共有
  - PBR: `_PBR_OcclusionMap` → `_PBR_MetallicGlossMap` サンプラー共有
  - Illustration: `_QuantizeMask` → `_MainTex` サンプラー共有
- **Parallax Occlusion Mapping 品質改善**: ループ内の `tex2D` を `tex2Dgrad` に移行し、動的ループ内の gradient 不定問題を解消。遠距離でのmipレベル選択が正確になり、テクスチャちらつきを抑制
- **SampleTex2DBlur 最適化**: 中心テクスチャサンプリングをキャッシュして重複フェッチを削減（blur有効時に呼び出しごとに1テクスチャフェッチ削減）

### Fixed
- **VR Single Pass Instanced (SPI) 対応**: `_CameraDepthNormalsTexture` の宣言を `UNITY_DECLARE_SCREENSPACE_TEXTURE` に修正し、SobelEdgeNormal のサンプリングを `UNITY_SAMPLE_SCREENSPACE_TEXTURE` に統一。VR環境での右目エッジ検出が正確に動作するように

### Added
- **Screen Edge Split Variant**: サンプラー予算超過時にScreen Edgeを分離パスで描画する新シェーダーバリアント `Natane/Toon Shader (ScreenEdge Split)` を追加。エディターGUIにワンクリック切替UIを実装
- **サンプラー予算見積り改善**: SamplerBudgetEstimator のコスト値をNOSAMPLER化に合わせて更新。ScreenSpace Lighting コンボ検出・警告を追加
- **MaterialValidator 拡張**: ScreenSpace Lighting コンボに対する警告バリデーションを追加
- **ShaderVariant ツール拡張**: ShaderVariantCollector/Prewarming/Stripper に ScreenEdge Split バリアントの Normal パス収集を追加

---

## [1.4.1] - 2026-03-07

### Fixed
- **VCC インストール不可修正**: v1.4.0 の VPM zip にディレクトリプレフィックスが付与されており、VCC がパッケージを認識できなかった問題を修正

---

## [1.4.0] - 2026-03-07

### Added
- **UVアイランド クリック選択**: UVマスクタブでキャンバス上のUVアイランドを色分け表示し、クリックで直接選択/解除可能に。HSVカラーホイールによるアイランド色分け、重心座標法によるヒットテスト、ホバーハイライト対応
- **ダッシュボード レスポンシブUI**: ツールカードグリッドがウィンドウ幅に追従する動的列数レイアウトに刷新。GUIStyleキャッシュ化によるGC Alloc削減
- **ダッシュボード 完全ローカライズ**: 全ツール名・説明文・カテゴリ名・検索ラベル・ボタンの日英対応を完了。副名称（日→英 / 英→日）表示を追加

### Fixed
- **VCC ダッシュボード起動不可修正**: 削除済み VTuberPresetGenerator のゴーストエントリが ToolRegistry/Dashboard/MenuPaths に残存し、HealthValidator が Error を返していた問題を修正
- **NatanePackagePathResolver**: VCC (Packages/) とUnityPackage (Assets/) 両方のインストールパスを動的解決するリゾルバーを追加

### Changed
- **ダッシュボード ToolInfo 構造改善**: 日英テキストを分離保持し、言語切替で即時反映される設計に変更
- **検索機能改善**: 日英両方のツール名・説明文を横断検索可能に

---

## [1.3.7] - 2026-03-04

### Added
- **ディザ座標安定化 (`_DitherStabilize`)**: オブジェクトピボット基準のスクリーンオフセットにより、キャラクター移動時のディザパターンスライドを抑制。0=従来スクリーン基準、1=オブジェクト相対（全9バリアント対応）
- **スペキュラー強度パラメータ (`_SpecularIntensity`)**: Range(0,5)でスペキュラー強度を直接制御可能に（全9バリアント+EditorGUI対応）
- **シェーダーバリアントツール全バリアント対応**: ShaderVariantCollector/Stripper/PrewarmingEditorのシェーダー名リストを4種→12種（Lite/Fur/Background/Eye/ScreenFX）に拡張。パスタイプ（ForwardBase/ForwardAdd/ShadowCaster/Meta）をシェーダーごとに正確設定

### Fixed
- **ShadowCasterパス InstanceIDエラー修正**: `v2f`構造体に`UNITY_VERTEX_INPUT_INSTANCE_ID`を追加し、GPU Instancing時のビルドエラーを解消（7バリアント）
- **スペキュラー加算ブレンド緩和**: `SafeAdditiveBlend`の過剰圧縮を緩和（compression 0.4→0.25, min 0.15→0.25）、Fragment側の強度乗算も0.5→0.8に調整
- **fmod負値によるディザ不具合修正**: StabilizeDitherCoordに+100000.0オフセットを追加し、負座標でのBayer配列アクセスエラーを防止
- **バリアントストリッピング デフォルトOFF**: VRChatで機能が反映されない問題を防止するため、ShaderVariantStripperのデフォルトを無効に変更

---

## [1.3.6] - 2026-03-03

### Added
- **ヒエラルキー一括編集 インスペクターUI統合**: プロパティブラウザ+Set/Add/Multiply方式を廃止し、MaterialEditor埋め込みによるNataneToonShaderGUI直接描画に刷新。リアルタイム編集・Unity標準Undo・Mixed Values表示に対応（748行→253行、約500行削減）
- **プリセットブラウザ「全て」カテゴリ**: カテゴリフィルターに「全て」トグルを追加し、全プリセット一覧表示が可能に
- **GUI トグル-キーワード同期追加**: SpecularDither, HairSpecular, GlintsAdvanced, WaterDrip, ScreenTone, GradientBaseColor, BlueNoiseDither, HashedAlpha, HeightFade, IntersectionFade, Tessellation, TessDisplacement

### Changed
- **シェーダーバリアント最適化**: 低使用率キーワード約25個を `shader_feature_local` → `shader_feature` に変更。マテリアル間で共有されないキーワードのビルドサイズ削減
- **プリセットシステム プロパティ名更新**: リネーム済みシェーダープロパティに対応（`_ToonSteps`→`_ShadowSteps`, `_SpecularSharpness`→`_SpecularSoftness`, `_EmissionIntensity`→`_EmissionGlow` 等）
- **リムライト キーワード修正**: `_RIM` → `_RIM_LIGHT` に統一

### Removed
- **VTuberPresetGenerator**: 不要になったプリセット生成器を削除

---

## [1.3.5] - 2026-03-02

### Added
- **PCSS (Percentage Closer Soft Shadows)**: スクリーンスペース近似によるコンタクトハードニングシャドウ
  - 3フェーズアルゴリズム: ブロッカー探索 → ペナンブラ推定 → 可変幅 Poisson Disk PCF
  - サンプル品質選択: Low (8) / Medium (16) / High (32)
  - ブレンドモード (Normal/Soft/Screen/Overlay)、ブレンド強度、ブラー対応
  - `shader_feature_local` によりPCSS無効時ゼロコスト
  - VRChat互換（ランタイムスクリプト不要）
- **グリッチマスクテクスチャ**: 部位ごとにグリッチ強度を制御
  - マスクスケール (1-5倍) で増幅可能
  - RGBスプリット・発生頻度もマスクで部位制御可能
- **ストレッチグリッチ**: テクスチャUVのみ横伸縮する独立グリッチモード
  - 専用マスクテクスチャ（マスクスケール対応）
  - 既存グリッチ `_GLITCH` とは独立したキーワード `_GLITCH_STRETCH`
- **グリッチノイズテクスチャ**: テクスチャベースのノイズでグリッチ表現を拡張
  - 3モード: UV Distortion / Color Corruption / Block Noise
  - スクロール速度対応
- **グリッチ強度上限引き上げ**: Intensity 0-3、RGB Split 0-3、Stretch 0-5

### Changed
- UV歪みスケール 0.1→0.15、RGB分離スケール 0.01→0.02 で高強度時の効果増強

### Fixed
- `SAMPLE_DEPTH_TEXTURE` → `UNITY_SAMPLE_SCREENSPACE_TEXTURE` 統一（VRステレオインスタンシング互換性向上、他シェーダー併用時の安定性改善）

---

## [1.3.4] - 2026-02-28

### Fixed
- **VRステレオインスタンシング修正**: SV_InstanceID/テッセレーションパイプライン非互換修正

---

## [1.3.3] - 2026-02-28

### Fixed
- **テッセレーション予約語エラー修正**: HLSL予約語との衝突を解消
- **VRステレオインスタンシング修正**: ステレオレンダリング時の不具合を修正

---

## [1.3.2] - 2026-02-27

### Added
- **StandardToon ShaderType**: シェーダータイプドロップダウンに「StandardToon (lilToon互換)」を追加
  - インスペクター上部から直接 StandardToon モードを選択可能
  - Toon ↔ StandardToon の相互切替で `_ShadingMode` を自動設定
- **StandardToon v2 ライティングパイプライン**: lilToon 完全再現のシェーディングシステム
  - Half-Lambert ベースの LilToonShading 関数
  - 3段影（Shadow 1st/2nd/3rd）+ Shadow Color Texture 対応
  - lilToon 互換 SH ライティング統合
- **lilToon 移行ツール機能強化**:
  - MatCap マスクテクスチャ移行（`_MatCapBlendMask` → `_MatCapMask`）
  - MatCap 2nd の完全移行（テクスチャ・ブレンドモード・マスク）
  - ライトカラー制限（`_LightColorMin` / `_LightColorMax`）移行
  - モノクロライティング（`_MonochromeLighting`）移行

### Fixed
- **StandardToon 黒レンダリングバグ修正**: MatCap の Multiply ブレンドモードが原因で全身真っ黒になる問題を修正
  - StandardToon では MatCap を常に Add モード（`SafeAdditiveBlend`）に強制
- **StandardToon + Light Volume 黒レンダリング修正**: LV 環境での stLightColor 依存による暗転を修正
- **Shadow Color Texture サンプリング修正**: 未設定時にアルベド情報が失われる問題を修正
- **lilToon 移行 — MatCap 安全移行**: 移行時に MatCap をオフ状態で移行するよう変更
  - テクスチャ・パラメーター・ブレンドモード・マスクは全て保持
  - ユーザーが手動で有効化・調整する安全なアプローチに変更

### Changed
- **lilToon 移行ツール**: MatCap の移行方針を変更
  - MatCap はオフ状態で移行（lilToon と Natane で挙動が大きく異なるため）
  - 移行レポートに警告メッセージを追加

---

## [1.3.1] - 2026-02-27

### Added
- **Background Shader 機能拡張**: 背景シェーダーに12の新機能を追加
  - **Phase 1 - 既存機能の有効化** (6機能):
    - デカール (`_DECAL`): 汚れ・看板・ステッカー
    - グリッター (`_GLITTER`): キラキラ・結晶・水粒
    - 水滴 (`_WATER_DRIP`): 雨・露滴の環境演出
    - インターセクションフェード (`_INTERSECTION_FADE`): オブジェクト交差部の透明化
    - AudioLink (`_AUDIOLINK`): 音楽連動環境エフェクト
    - ビデオテクスチャ (`_VIDEO_TEXTURE`): モニター・スクリーン背景
  - **Phase 2 - 新規機能** (3機能):
    - ディテールマップ (`_DETAIL_MAP`): セカンダリUV対応、近距離テクスチャ解像感向上
    - トライプレーナーマッピング (`_TRIPLANAR`): UV展開不要の3軸テクスチャ投影（岩・洞窟・地形に最適）
    - ハイトフォグ (`_HEIGHT_FOG`): マテリアルベースの高さフォグ（Linear/Exponential）、VRChatポストプロセス不可環境向け
  - **Phase 3 - 高度な追加機能** (3機能):
    - サーフェスカバー (`_SURFACE_COVER`): 雪/砂堆積表現、ワールド空間法線ベースのカバーブレンド、法線マップ対応
    - ミラー対応 (`_MIRROR_CONTROL`): VRChatミラー内の表示制御（両方/ミラーのみ/ミラー以外）、ミラー内エミッション倍率
    - Quest軽量パス (`_QUEST_LITE`): MatCap 2/3・グリッター・水滴・ホログラム・グリッチ・インターセクションフェードを自動スキップ、モバイルGPU 30-50%改善
- **GUI**: 6つの新セクション追加（ディテールマップ・トライプレーナー・ハイトフォグ・サーフェスカバー・ミラー対応・Quest軽量パス）
  - 全セクション日英バイリンガル対応
  - 検索機能対応、タブ配置（Effects/Environment/Advanced）

### Fixed
- **v2f 構造体 `uv1` 欠如修正**: Detail Map 使用時のコンパイルエラーを防止（TEXCOORD12 追加）
- **Surface Cover 法線空間修正**: UnpackNormal のタンジェント空間→ワールド空間変換を修正（xz投影用リマップ）
- **Detail Map 法線空間修正**: tangentToWorld 行列によるワールド空間変換を適用
- **CoverDirection ゼロベクトル安全策**: normalize() の NaN 防止にエプシロン加算
- **MirrorEmissionMultiplier 実装**: エミッションセクションでミラー内エミッション倍率を適用

---

## [1.3.0] - 2026-02-27

### Added
- **Inspector 日英切り替え**: EN/JP ボタンによるインスペクター言語のワンクリック切り替え
  - 全 1,214 箇所のローカライゼーション対応
  - デフォルト言語: 日本語（EditorPrefs で永続化）
- **バイリンガル ドキュメントサイト**: natanetoon.com に英語版を追加
  - /en/ パス以下に全 74+ ページの英語版
  - ヘッダーの言語トグルで即座に切り替え
  - nav.js による動的ナビゲーションの完全日英対応

---

## [1.2.17] - 2026-02-25

### Added
- **シェルベースファー機能**: 高品質な毛皮表現を新規シェーダーバリアント `Natane/Toon Shader (Fur)` として追加
  - 16シェルパスによるリアルなファーレンダリング（デフォルト OFF・高GPU負荷のため手動有効化が必要）
  - ファー長さ・密度・アルファカットオフの基本設定
  - 根元/先端カラーグラデーション・メインテクスチャとの混合比制御
  - 重力（二次関数ドロープ）・風アニメーション（方向・速度・強度）の物理シミュレーション
  - ファーマスクテクスチャ（白=毛あり、黒=毛なし）
  - セルフオクルージョン（AO）・セルフシャドウ・スペキュラ・リムライト
  - 距離ベースLOD（遠距離でシェル数を自動削減しパフォーマンス最適化）
  - `_FUR` キーワードOFF時はdiscardスタブでGPUコストゼロ
  - インスペクターに日本語UIとヘルプテキスト付きの専用セクション
  - 描画タイプ「ファー」を選択して使用

---

## [1.2.16] - 2026-02-25

### Added
- **高さフェード アウトライン対応**: Height Fade 有効時にアウトラインパスにも高さフェードを適用。Alpha/Clip/Dithering 全3モード対応
  - アウトラインパスに `_HEIGHT_FADE` キーワード・変数宣言・worldPos 受け渡しを追加
  - 高さフェードでアルファモード使用時にアウトラインが残る不具合を修正
- **描画タイプドロップダウン追加**: インスペクター上部（シェーダータイプ直下）に描画タイプセレクターを追加
  - 「不透明」「カットアウト」「半透明」の日本語ラベルで直感的に切り替え可能
  - 既存のレンダリング設定セクションのドロップダウンも同一の日本語ラベルに統一

---

## [1.2.15] - 2026-02-25

### Added
- **アウトライン コーナーギャップ修正**: ハードエッジメッシュでアウトラインの角に隙間が出る問題の対策機能を追加
  - `_OutlineCornerSmooth`: スムース法線OFF時の法線フォールバック。頂点法線を頂点位置方向（オブジェクト中心→頂点）にブレンドしてコーナーギャップを軽減（Range 0-1, デフォルト0）
  - `_OutlineEdgeCompensation`: シャープエッジ検出によるアウトライン幅自動縮小。元の法線と使用中の法線の不一致度に応じて幅を0.3〜1.0倍に調整（Range 0-1, デフォルト0）
  - GUI: スムース法線OFF時の警告ヒント・コーナースムージング/エッジ幅補正のヘルプテキスト追加
- デフォルト値0で後方互換性を維持、`shader_feature` 追加なしでバリアント数増加なし

---

## [1.2.14] - 2026-02-25

### Added
- **高さフェード（Height Fade）**: 高さに基づくフェードアウト機能。Alpha/Clip/Dithering の3モード対応、エッジグロー付き
- **ステンシル**: Stencil Fail / ZFail Operation の追加。より柔軟なステンシル制御
- **ワールド座標ディゾルブ**: Dissolve にワールド/ローカル座標モードを追加。軸指定・範囲指定・ノイズブレンド対応
- **オブジェクト交差フェード（Intersection Fade）**: 深度バッファを使用したオブジェクト交差部のフェード
- **グラデーションベースカラー**: 軸指定のグラデーションカラーをベーステクスチャにブレンド

### Fixed
- **アウトライン幅修正**: アウトラインWidthのRange上限を0-1に戻し、乗数0.1で十分な幅を確保
- **半透明ブレンドプリセット**: 半透明バリアントのブレンドモード設定を改善

---

## [1.2.13] - 2026-02-25

### Performance
- **AO テクスチャ二重サンプリング除去**: `_USE_RAMP` 分岐の内外で同一の `SampleTex2DBlur1(_AOMap, ...)` を2回呼んでいたのを、分岐前に1回だけサンプリングしてキャッシュするように最適化。テクスチャフェッチ1回削減
- **pow(x, 2.0) → x * x 最適化**: Vertex Animation の Pulse モードで `pow(abs(sin(...)), 2.0)` を乗算に置換。pow() 命令1回削減（5-8 ALU 節約）
- **RimLight Direction 共通部分式除去（CSE）**: Rim Light 1 と Rim Light 2 で `normalize(_RimLightDirection.xyz)` を2回計算していたのを、1回だけ計算してキャッシュ。normalize() 1回削減（5 ALU 節約）

### Changed
- **シェーダーコード構造化コメント追加**: NataneToonInput.hlsl にセクション分けコメント（Core Rendering / Makeup / Lighting / Effects 等）を追加し可読性を向上
- **シェーダーバリアント Properties コメント追加**: 全3バリアント（Opaque/Cutout/Transparent）にセクション分けコメントを追加

---

## [1.2.12] - 2026-02-25

### Added
- **リムライト方向追従（lilToon方式Half-Lambert）**: `_RimDirStrength` パラメータ追加。ライトの方向にリムが追従する機能。0=全方向リム、1=ライト側のみリム。Rim Light 1/2/Environmental Rim の3種に対応
- **リムライト影マスク**: `_RimShadowMask` パラメータ追加。影の領域でリムを抑制する機能。0=影でもリム表示、1=影でリム消失
- **リムライト ForwardAdd 対応**: Rim Light 1/2/Environmental Rim がポイントライト・スポットライトに対応。各ライトの色・距離減衰・`_AdditionalLightIntensity` で変調
- **バックライト ForwardAdd 対応**: バックライトがポイントライト・スポットライトに対応
- **ディレクショナルライト非依存化**: SHフォールバックでポイント/スポット/ベイク環境対応
- **各エフェクト個別距離フェード**: 21エフェクトに個別の距離フェード機能を追加

### Fixed
- **ライト方向フォールバックチェーン強化**: ポイント/スポットライト環境でのライト方向取得を改善

---

## [1.2.10] - 2026-02-24

### Fixed
- **Outlineパス shader_feature_local 宣言欠落修正**: 全3バリアント（Opaque/Cutout/Transparent）のOutlineパスに `_OUTLINE_WIDTH_MAP`、`_OUTLINE_MULTI_COLOR`、`_OUTLINE_MASK` の `shader_feature_local` 宣言を追加。アウトラインのキーワード切り替えが正しく機能するように
- **KajiyaKay ヘアスペキュラー NaN防止**: `KajiyaKaySpecular()` の `pow()` に `saturate()` ラッパーを追加。大きな指数値での未定義動作を防止
- **VR SPI ForwardBase インスタンスID伝播修正**: ForwardBase頂点シェーダーに `UNITY_TRANSFER_INSTANCE_ID(v, o)` を追加。VR Single Pass Instanced環境でのインスタンスID伝播チェーンを完成
- **_VATPadding プロパティ宣言追加**: CBUFFERに定義されていたがProperties{}ブロックに宣言がなかった `_VATPadding` を全3バリアントに追加
- **AO強度GUI追加**: DrawAOSection()に `_AOIntensity` プロパティ表示を追加
- **影色テクスチャToggle制御追加**: DrawShadowColorTextureControls()に `_SHADOW_COLOR_TEX` キーワードトグルを追加
- **リム方向制御GUI追加**: DrawRimLightSection()にリム方向制御トグル・`_RimLightDirection`・`_RimDirectionRange` プロパティ表示を追加

### Added
- **Vertex Animation GUIセクション**: DrawAdvancedTab()にVertex Animation（Wind/Breath/Pulse）の完全なGUIセクションを追加。アニメーション種類・速度・強度・周波数・マスクの制御UI
- **SmoothNormalBaker ツール**: スムースノーマルベイクツールを追加
- **ShaderGUIStyles 分離**: GUI スタイル定義を専用ファイルに分離

---

## [1.2.9] - 2026-02-23

### Added
- **AK:EF Style 3機能追加**: Face SDF回転・ヘアスペキュラー・アウトラインテクスチャカラー

### Changed
- バージョンアップ

---

## [1.2.8] - 2026-02-23

### Fixed
- **Wirelight ジオメトリシェーダー VR SPI 完全修正**: g2f構造体に `UNITY_VERTEX_INPUT_INSTANCE_ID` を追加し、ジオメトリ→フラグメント間のインスタンスID伝播チェーンを完成。フラグメントシェーダーに `UNITY_SETUP_INSTANCE_ID` を追加。VR Single Pass Instanced 環境での片目描画崩れを修正
- **Wirelight pow() 精度喪失防止**: `pow(Triangles, 2.2)` および `pow(col.rgb * brightness, power)` に `max(x, 0.0)` ガードを追加。GPU依存の未定義動作を防止
- **Wirelight Distance Fade pow() ガード**: `pow(distanceFade, power)` に非負ガードを追加
- **Eye texCol 初期化強化**: Dead/Nervous ステートでテクスチャサンプリングを `_UseTexture` チェックの外に移動。TexturePolish に常に有効なテクスチャデータが渡されるように
- **Eye Nervous ループ sizeStep ガード強化**: `_NervousLinesSize` に最小値 0.02 を設定し、ゼロ除算のエッジケースを完全に排除
- **Eye atan2 定義域エラー修正**: IrisCaustics と ExpressionOverlay の `atan2()` にゼロベクトルガード（`dot(localUV, localUV) > 1e-10`）を追加。瞳孔中心でのNaN発生を防止
- **Eye AudioLink バンド選択型変換**: `_BandSelection` を `(int)` で明示的にキャスト。Float→Int の暗黙変換による精度問題を防止
- **Noise 負値保護**: `pattern()` の戻り値を `saturate()` で [0,1] に制限。下流の計算での負値伝播を防止
- **AudioLink ThemeColor 範囲外防止**: `AudioLinkGetThemeColor()` で取得したカラー値に `max(color, 0.0)` の非負保証と `saturate()` によるアルファ制限を追加

### Changed
- **lilToon 移行ツール改善**: 移行時に基本タブ以外の全機能をオフにした状態でマテリアルを生成するように変更。プロパティ値は保持されるため、移行後に必要な機能を個別に有効化可能

---

## [1.2.7] - 2026-02-21

### Fixed
- **VR Single Pass Instanced (SPI) 互換性修正**: Outline / ShadowCaster パスに VR ステレオインスタンシングマクロ一式を追加（`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, `#pragma multi_compile_instancing`）。全3バリアント（Opaque/Cutout/Transparent）対応
- **Fragment シェーダー VR 修正**: `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` をフラグメントシェーダー先頭に追加。VR環境でのスクリーンスペーステクスチャサンプリング（シャドウマップ、GrabPass等）が正しい目のインデックスを参照するように
- **GrabPass（屈折）VR ステレオ対応**: `sampler2D _GrabTexture` を `UNITY_DECLARE_SCREENSPACE_TEXTURE(_GrabTexture)` に置換、全サンプリングを `UNITY_SAMPLE_SCREENSPACE_TEXTURE` に変更。VR SPI モードで屈折エフェクトが両眼で正しくレンダリングされるように
- **AudioLink コンパイルエラー修正**: Rim Light / Dissolve 機能無効時に AudioLink の対応サブ機能がコンパイルエラーになる問題を修正（キーワードガード追加）
- **グリッチ RGB Split マゼンタ修正**: RGB Split がライティング未適用の生テクスチャ値と適用済みの値を混合していたため、影部分でマゼンタ色（エラー色）が発生していた問題を修正。デルタ方式に変更しライティングを保持
- **Outline パス VR 修正**: 全3バリアントの Outline フラグメントシェーダーに `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` を追加。VR でアウトラインが正しくレンダリングされるように
- **ShadowCaster パス VR 修正**: Opaque/Cutout の ShadowCaster に `UNITY_TRANSFER_INSTANCE_ID` + `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` を追加。VR でシャドウが正しく描画されるように
- **Wirelight VR 完全対応**: `#pragma multi_compile_instancing` 追加、全 `shader_feature` を `shader_feature_local` に変更、v2g 構造体にステレオ出力追加、ジオメトリシェーダーで `UNITY_TRANSFER_INSTANCE_ID` / `UNITY_TRANSFER_VERTEX_OUTPUT_STEREO` による頂点→ジオメトリ→フラグメント間のステレオ情報転送を修正

---

## [1.2.5] - 2026-02-19

### Changed
- コードコメント・ドキュメント・UIテキストのブランド参照を一般的な技術用語に統一
- LVブレンドモード名称を「Natural」に統一

### Removed
- YMToon移行ツールを削除

---

## [1.2.4] - 2026-02-19

### Changed
- LVブレンドモード名称を「Natural」に変更。機能の動作（`max(indirect, direct + additional)` 合成）をより直感的に表す名前に

---

## [1.2.3] - 2026-02-19

### Added
- **Natural ライティングパイプライン（max合成方式）**: `max(indirect, direct + additional)` 合成方式を導入。髪と顔が分かれたモデルでも LightVolume / LTCGI の環境色が全メッシュに正しく反映されるように
- **LVブレンドモード Natural(3)**: LightVolume を間接光として max() 合成に参加させる新モード。デフォルトに設定（既存の Add/Multiply/Replace も互換維持）
- **`_IndirectLightMinColor`**: 間接光の最低保証カラー。暗いワールドでもキャラクターが真っ黒にならないよう下限を設定
- **`_ShadowEnvStrength`**: 影への環境色反映強度。環境光の色味を影に反映してより自然なライティングを実現

### Changed
- NdotL 計算を生 dot 値方式（max(0,...) なし）に変更。`_ShadowOffset` との組み合わせでより柔軟な影制御が可能に
- Shadow Attenuation を NdotL から分離し、シェーディング段階に後段適用。クリーンなトゥーン境界と滑らかなシャドウマップ暗化を両立
- AO 適用位置を lighting 均一暗化から shadingValue 段階適用に変更（間接光は 50% 制限、Natural 方式）
- `_GIIntensity` デフォルト値を 0 → 0.5 に変更（既存マテリアルは保存値を使用するため影響なし）

### Fixed
- PCFシャドウのステレオインスタンシング（VR）互換性を修正

---

## [1.2.2] - 2026-02-17

### Added
- **バンドル版 LightVolumes.cginc**: RED_SIM 氏の LightVolumes.cginc を MIT License に基づきバンドル同梱。VRC Light Volumes パッケージ未インストール時でも本物のサンプリングコードが使用され、Light Volume の色を正しく受け取れるように
- パッケージインストール済み環境では引き続きパッケージ版を優先使用

### Changed
- VRC Light Volumes 未検出時のフォールバック方式を ShadeSH9 ベースからバンドル版 LightVolumes.cginc に変更
- エディタ UI のメッセージを「フォールバック」から「バンドル版で全機能利用可能」に更新
- パッケージ未検出時の HelpBox を Warning から Info に変更

### Removed
- ShadeSH9 ベースの互換関数4つ（LightVolumeSH, LightVolumeEvaluate, LightVolumeSpecular, LightVolumesEnabled）を削除。バンドル版が内部で Unity Light Probes への自動フォールバックを提供

---

## [1.2.1] - 2026-02-17

### Added
- **シャドウマップスムージング (PCF)**: ディレクショナルライトのスクリーンスペースシャドウマップに対するPCF 9タップフィルタリングを追加。影エッジの本物のアンチエイリアシングを実現
- **適応型シャドウスムージング**: ポイント/スポットライトに対する適応型中心点のsmoothstepスムージングを追加
- **多段階トゥーンシェーディングの階調なじませ**: `_ShadowSmoothing` でトゥーンステップ境界を連続的なグラデーションにブレンド。多段階影の諧調が滑らかに
- **LTCGIフォールバック実装**: LTCGIパッケージ未インストール時にSH + Reflection Probeで近似する互換レイヤーを追加

### Changed
- シャドウスムージングの処理順序を変更: Smoothing → Shadow Receive Mask（PCFが生のシャドウ値で正しく動作するように）
- シャドウスムージングのヘルプテキストを更新（PCF・多段階なじませの説明追加）

### Fixed
- VCC/VPM URLをカスタムドメイン（natanetoon.com）に変更
- リポジトリ名をnatane_toon_shaderに更新

### Documentation
- ドキュメントサイトを個別ページ構造にリストラクチャリング

---

## [1.1.4] - 2025-01-06

### 🚀 Added - Performance Optimizations
**機能を一切削除せずに大幅な軽量化を実現！**

#### Phase 1: パフォーマンス基盤の最適化
- **Luminanceマクロ**: `LUMA_WEIGHTS`と`CALC_LUMINANCE()`を追加し、重複計算を50%削減
- **Refraction Blur最適化**: テクスチャサンプル数を9→5に削減（45%削減）、品質は維持
- **half精度の活用**: Fragment/Lighting計算を最適化、GPU命令数を20-30%削減（Quest向けに特に効果的）

#### Phase 2: 計算効率の改善
- **SafeAdditiveBlend最適化**: 約40%高速化
- **条件分岐の最適化**: lerp/stepによる分岐削減でGPU効率向上

#### Phase 3: 細かい最適化
- **HSV変換のスキップ**: デフォルト値時に変換を回避
- **Vertex正規化の条件付き最適化**: Normal Map未使用時は正規化をスキップ
- **Tone Mapping関数の最適化**: half精度化とコンパイル時定数の活用

#### 総合効果
- テクスチャサンプル: 45%削減
- GPU命令数: 20-30%削減
- ドット積計算: 50%削減
- GPU分岐: 大幅削減
- **視覚品質**: 変更なし ✨

### 🎨 Added - Refraction Feature
- 物理ベースの屈折計算（Snellの法則）
- IOR（屈折率）調整: 1.0～3.0
- 最適化された5サンプルブラー
- マスクによる部分的な屈折制御

### Changed
- すべての主要計算をhalf精度に最適化
- Luminance計算のキャッシュ化
- 条件分岐をlerp/stepに置き換え

### Performance
- VRChat Quest向けに大幅な軽量化
- アバターパフォーマンスランクの改善が期待
- 既存マテリアルの互換性を完全維持

---

## [1.1.3] - 2024-11-05

### Added
- **レンダリングモード選択**: Opaque/Cutout/Transparent
- **アルファマスク機能**: 部分的な透明度制御
- **3つのシェーダーバリアント**: 用途に応じた最適化

### Changed
- フォルダ構造の整理
- Shader variantsの分離

---

## [1.1.2] - 2024-11-04

### Added
- VTuberプリセット自動生成機能
- フォルダ構造の大幅整理

### Fixed
- ディレクトリ作成エラーの修正

---

## [1.1.1] - 2024-10-31

### Fixed
- インスペクタープロパティ反映の修正
- UIバグの修正

---

## [1.1.0] - 2024-10-30

### Added
- プリセット適用時のインスペクターUI自動更新

### Changed
- UIの応答性向上

---

## [1.0.9] - 2024-10-29

### Added
- キャラクタープリセット23種追加（合計59種）
- アウトラインマスク機能
- ライトカラー影響度調整
- VTuberプリセット改善

---

## [1.0.8] - 2024-10-28

### Added
- プリセット自動生成機能
- バッチ処理機能の改善

---

## [1.0.7] - 2024-10-27

### Added
- シーンマテリアル編集ウィンドウ
- リアルタイム編集機能

### Fixed
- 各種バグ修正

---

## [1.0.6] - 2024-10-26

### Added
- Toon/NPRスタイルプリセット10種追加
- プリセットライブラリの拡充

---

## [1.0.5] - 2024-10-25

### Added
- 包括的なエラーハンドリング
- 安定性の向上

---

## [1.0.4] - 2024-10-24

### Fixed
- ShaderGUIのバグ修正
- UIの安定性向上

---

## [1.0.3] - 2024-10-23

### Fixed
- v1.0.2のバグ修正

---

## [1.0.2] - 2024-10-22

### Changed
- 内部処理の改善

---

## [1.0.1] - 2024-10-21

### Added
- **VRC Light Volumes対応**: 次世代ボクセルベースライティング
- **完全日本語UI**: すべてのインスペクターとエディタが日本語対応

### Changed
- ライティングシステムの拡張

---

## [1.0.0] - 2024-10-20

### Added
- 初回安定版リリース
- 完全日本語UI対応
- 基本的なトゥーンシェーディング機能
- VRChat最適化

---

## 凡例

- `Added`: 新機能
- `Changed`: 既存機能の変更
- `Deprecated`: 非推奨機能
- `Removed`: 削除された機能
- `Fixed`: バグ修正
- `Security`: セキュリティ修正
- `Performance`: パフォーマンス改善
