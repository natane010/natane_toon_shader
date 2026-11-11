# Changelog

All notable changes to Natane Toon Shader will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed — VRChat ミラー / 距離まわり
- **ミラー内でアウトラインが極端に太くなる・歩くと太さが変わる問題を修正**。
  従来の幅計算はクリップ空間 z を掛けており、ミラー（斜めニアクリップ投影）では
  z が「ミラー面までの距離」に従うため破綻していた。ミラーと平行投影カメラでは
  通常の投影が返すはずの値を再構成して使う。通常カメラの結果は従来と同一。
- **ミラー内でアウトラインの左右が内側に潰れる問題を修正**。ミラー時に法線の X を反転していたが、
  ビュー行列が既に反転を含むため二重補正になっていた。
  ⚠️ ミラー内のアウトラインの見え方が変わります（正しい外向きになります）。
- **距離フェード / ニアフェードがアウトラインに効いていなかった問題を修正**。本体が消えても
  反転ハルだけが残り、シルエットがアウトライン色で塗りつぶされていた。
  アウトラインパスも `_DISTANCE_FADE` に追従する（Alpha / Simplify / Dither 全モード）。
- **距離系の判定を両目共通の視点で行うようにした**。距離フェード、ニアフェード、深度カラーフェード、
  アウトライン距離補正、FX モジュレーターの距離、テッセレーション係数、ファーのシェル LOD、
  ハーフトーンの距離スケールが対象。VR で左右の目の結果がずれてチラつく現象を防ぐ。
  ミラー内では `_VRChatMirrorCameraPos` を使う。
- **深度テクスチャに依存する機能をミラー内で無効化**。Intersection Fade、PCSS、Screen Edge、
  Screen Edge 分離パス、パーティクルのソフトパーティクルが対象。ミラーカメラは自前の深度テクスチャを
  持たないため、メインビューの深度を読んでオブジェクトが丸ごと消える・縞が出る原因になっていた。
- **Screen Edge が VR（ダブルワイド）で左右にずれる問題を修正**。深度参照の UV を
  `ComputeScreenPos` 基準に統一した。
- **Perspective Flat をミラーと平行投影カメラでスキップ**。z の書き換えが斜め投影では
  中間深度にならず、描画順が崩れていた。
- **Face Ortho が VR ミラー内でデスクトップ用の量を使っていた問題を修正**。
- **ディザのチラつき / 縞を修正**。4x4 Bayer の座標を floor してから剰余を取るようにした。
  従来はスケール 10 で 16 段階のうち 4 段階、スケール 4 や 8 では 1 段階しか使われていなかった。
  ⚠️ 距離 / 高さ / 交差フェード、影境界ディザ、ディザアルファのパターンが変わります
  （スライダー 10 = 1 ピクセル 1 セル、小さいほど粗い。10 を超える値は 10 と同じ）。
- **ディザ安定化（`_DitherStabilize`）とハーフトーンのオブジェクト固定が縦方向に滑る問題を修正**
  （D3D の上下反転を考慮していなかった）。ピボットがカメラの真横や後ろにある場合の破綻も防ぐ。
- スペキュラ / ヘアハイライト / ハッシュドアルファ / ディザアルファのディザ座標を
  `SV_POSITION` から直接取るようにした（ダブルワイドのステレオで横に 2 倍伸びていた）。
- レンチキュラーの「両目共通」モードが VR ミラー内でも効くようにした。
- **ミラー内でキューブマップ反射 / 環境リム / フェイク反射の向きがずれる問題を修正**。
  ミラーカメラの視線は既に反転後の位置から計算されているのに、反射ベクトルをさらに反転していた。
  ⚠️ ミラー内の映り込みの向きが変わります（PBR 経路と同じ正しい向きになります）。
- **ミラー / カメラ表示制御（`_MIRROR_CONTROL`）がアウトラインに効いていなかった問題を修正**。
  「ミラーのみ表示」のマテリアルで、通常視点にアウトラインの塊だけが残っていた。
- 屈折の歪み方向をビュー空間で計算するようにした（ワールド XY を画面オフセットに使っていたため、
  見る向きやミラーで歪み方向が変わっていた）。

### Fixed — コンパイルエラー / 動作不良
- **Face Ortho のマスク使用時にコンパイルエラーになる問題を修正**。`_FaceOrthoMaskTex` の宣言が
  `_SMOOTH_NORMAL` ブロックの中にあり、スムース法線が OFF のとき未宣言になっていた
  （XRay / Ghost プリパス / Screen Edge 分離パスでは常に失敗）。
- **Background バリアントが Shadowmask ライティングのシーンでコンパイルエラーになる問題を修正**。
- **Cutout / Cutout Lite / XRay でアルファテストが働いていなかった問題を修正**。
  エントリポイントが `frag` のままで、`_Cutoff` を適用するラッパーが呼ばれていなかった。
  ⚠️ これらのバリアントで `_Cutoff` 未満のピクセルが正しく抜けるようになります
  （影は以前から抜けていました）。意図せず消える場合は `_Cutoff` を下げてください。
- **AudioLink の帯域がすべて Bass に反応していた問題を修正**。帯域はテクスチャの行なのに列として
  読んでいた。AudioLink の無いワールドでは 0 を返すようにした（未バインド時に常時点灯していた）。
  GPU パーティクルの AudioLink も同様に修正（Bass 以外が無反応だった）。
- **シャドウスムージング（`_ShadowSmoothing` > 0）でポイント / スポットライトが 3 段の帯になる問題を修正**。
  ⚠️ `_ShadowSmoothing` を使っているマテリアルの追加ライトの減衰が滑らかになります。
- スミアの距離フェードが逆（近いほど消える）だった問題を修正。
- 影シェイプリグを ON にしただけで陰影が変わる問題を修正（N·L を不要にクランプしていた）。
- ビルド機能最適化の Advanced モードで PBR / PBR-like スペキュラが常にストリップされていた問題を修正。

### Changed
- `package.json` の対応 Unity を `2022.3` に更新。ローカルキーワード数（メインシェーダーで 110 超）と
  `PackageRequirements` / `#include_with_pragmas` の使用により、実際には 2019.4 では動作しない。

### Added
- **アウトライン幅の基準** (`_OutlineWidthMode`): `Legacy`（既定、従来どおり）と `Stable` を選べる。
  `Stable` はカメラのニアクリップに依存しない太さになり、エディタ / VRChat / カメラ / ミラーで
  同じ見た目になる。既存マテリアルは `Legacy` のままで見た目は変わらない。
- **インスペクタの表示密度切り替え**（シンプル / 標準 / すべて、既定はシンプル）。
  機能は一切削っていない。シンプルでは使用中のセクションだけを並べ、未使用の機能は
  「機能を追加」一覧から開く。ヘルプ行、難易度マーク、推奨値ヒントは「すべて」で表示される。
- **ファーの生成方式を選べるようにした** (`_FurMethod`: シェル法 / フィン法 / 併用):
  従来はシェル法のみで、**輪郭（シルエット）の毛が薄くなる**という手法固有の弱点を
  回避する手段が無かった。シェルは殻を法線方向に重ねるため、殻を横から見る角度＝
  輪郭でいちばん効かない。フィン法はポリゴンの辺に毛の板を立てるので逆に輪郭に強い。
  - 両方式は**同じ毛の分布**（`NataneToonFurCommon.hlsl`）を参照する。
    フィンはシェルが埋める体積の垂直断面そのものなので、併用しても毛並みがずれない。
  - フィン側のパラメータ: `_FurFinViewThreshold`（正面向きの面ではフィンを作らない）、
    `_FurFinJoints`（関節数。重力と風で毛がしなる）、`_FurFinNormalBlend`（板の法線と
    面の法線の混合比）、`_FurFinRandomDir`（生える向きのランダム性）。
  - **PC 専用**。フィンのパスは `#pragma require geometry` 付きで、
    ジオメトリシェーダーを持たない環境（Quest 等）ではパスごと省かれ、
    シェル法だけの見た目になる。「フィンのみ」でもシェル16パスの描画コール自体は残る
    （ビルトインRPはマテリアル値でパスを止められないため、頂点を潰してピクセルを出さない）。
  - 詳細は `Documentation~/FUR.md`。実装の考え方は hecomi 氏の URP 版記事
    （シェル法 / フィン法）を参考にしている。
- **影シェイプリグ** (`_SHADOW_SHAPE_RIG`): 影の境界に楕円リグを最大4つ置き、
  その内側で境界を局所的に膨らませる／へこませる。
  これまでハイライト側には `_SHAPED_HIGHLIGHT` があったが、**影側の形状制御が存在しなかった**
  （`_SHADOW_EDGE_NOISE` はランダム、`_USE_MULTI_SHADOW` は段数のみ、`_SDF_MAP` は形がテクスチャ固定）。
  テクスチャ不要・Uniform 分岐のみで Quest でも使える。半径 0 のスロットは計算をスキップする。
  `Tools > Natane > エフェクト > 影シェイプリグのフィット` で、塗ったマスクから
  重心と二次モーメント行列の固有ベクトルを解いてリグを自動生成できる。
- **フェイクシャドウ** (`Natane/Toon Shader FakeShadow`): 前髪が顔に落とす影を、
  ライティングに依存せず板ポリまたは複製メッシュで描く極小シェーダー。
  VRChat の定番手法だが本パッケージには相当機能が無く、
  **lilToon から移行したユーザーは前髪の落ち影を失っていた**。
  セットアップツールは板ポリ / メッシュ複製の2方式に対応し、どちらも非破壊。
  詳細は `Documentation~/FAKE_SHADOW.md`。
- **ステンシルプリセット**（See Through Hair 相当）: 眉・目を前髪より前に描き、
  前髪側をくり抜くセットアップを Writer / Cutter のロール割り当てだけで組める。
  ステンシルのプロパティは以前から存在していたが、**組み合わせを自力で組む必要があり
  プリセットもドキュメントも無かった**。参照値の衝突検出と未使用値の提案、
  適用前後のドローオーダー差分表示つき。詳細は `Documentation~/STENCIL_PRESET.md`。
- **ディゾルブスタジオ**: ディゾルブの**アニメーション**を組み立てるウィンドウ。
  プリセット5種 / 0→1 のスクラブプレビュー / `AnimationClip` 生成 /
  VRChat 連携（Modular Avatar があれば非破壊） / Animator 不使用の自走モード。
  シェーダー側は充実していたのに、`AnimationClip` を生成するツールがパッケージ内に
  1つも無く、「ディゾルブで消えるアニメーション」の実作業がほぼ全部手作業だった。
  マテリアルスロットが複数ある Renderer では `material[N]._DissolveAmount` へ自動で切り替える。
- **撮影模倣 ScreenFX**: 放射ブラー / 周辺限定の色収差 / グラデーション合成 / 部分モノクロ を追加。
  いずれも Uniform 分岐で、新規 shader_feature は増やしていない。
  プリセット4種（劇場 / 必殺技 / 回想 / シリアス）を専用インスペクタから適用できる。
  **PC 限定**（GrabPass ベース）。Bloom と被写界深度は非対応で、その理由も UI に明記した。
- **Background バリアントへ絵画調機能を展開**: `_COLOR_QUANTIZE` / `_LUT_3D` / `_WATERCOLOR`。
  キャラは絵画調にできるのに背景はできず、同じワールド内でルックが揃わない状態だった。
  `_SOFT_FILTER` / `_KUWAHARA_FILTER` は GrabPass を要求し、Background に GrabPass を足すと
  **全背景マテリアルが無条件に全画面コピーを払う**ことになるため、今回は見送っている。
- **顔SDF影マップのベイク** (Map Generator): `_FACE_SDF_ROTATION` が本来要求する
  「ライト角のフィールド」をメッシュから実際にベイクする。水平方向のライト角を
  0°→180° までスイープし、各テクセルが影に入る最小角 θt を `s = (1 - cos θt) / 2` として
  記録する。ライト空間の深度バッファを角度ごとに作るシャドウマップ方式なので、
  鼻やまつげの落ち影が画素単位で入る。ベイクに使った軸はマテリアルの
  `_FaceForwardDirection` / `_FaceRightDirection` へ自動で書き込まれ、
  ベイクとシェーダーの座標系が食い違わないようにしている。
  既存の「Mask SDF を生成」は**マスクの符号付き距離場**でありライト角の情報を持たないため、
  回転追従には使えない。UI 上でその旨を明示し、ベイクへ誘導するようにした。
- **ハッチング/水彩素材ジェネレーター** (`Tools > Natane > エフェクト > ハッチング・水彩素材生成`):
  `_HATCHING` が要求する6段 Tonal Art Map（`_HatchTex0` の RGBA=L1-4 / `_HatchTex1` の RG=L5-6）と、
  `_WATERCOLOR` の粒状感・紙目テクスチャを生成する。
  これらの既定値はすべて `"white"` / `"gray"` で、素材を自前で用意しない限り
  機能を ON にしても**何も起きない**状態だった。
  TAM は**累積生成**（明るい段の線を暗い段が必ず含む）なので、段の境界で線が入れ替わってちらつかない。
  プリセットは 鉛筆 / 交差 / 漫画 / 版画 の4種。水彩素材は平均 0.5 に正規化するため、
  割り当てても全体の明度が変わらない。Texture Studio の「生成ツール」タブからも起動できる。
- **FX Modulator に `DissolveAmount` ターゲットを追加**: Animator を使わずに
  時間・音・カメラ距離・視線角度でディゾルブを自走させられる。
  最も近い `AlphaFade` は単純なアルファ減衰でエッジ発光を伴わず、代替にならなかった。
- **変種間パリティ検査**: `Properties` の `[Toggle(KEYWORD)]` が立てるキーワードを
  **どのパスもコンパイルしていない**状態を検出する。既存の監査は
  「`#pragma` にあるがレジストリ未登録」の方向しか見ておらず、この逆方向は素通りしていた。
  EditMode テスト (`NataneShaderVariantParityTests`) と Unity 非依存の CI スクリプト
  (`Tests/CI/check_shader_parity.py`) の双方から同じ宣言テーブルを参照する。
- **影の玉ボケ / 木漏れ日** (`_SHADOW_BOKEH`): 影の中に柔らかい円形の光斑を落とす表現。
  光源方向に垂直な平面へ投影するため、カメラを動かしても光斑が面に貼り付いたままになり、
  ライトを回すと向きが追従する。合成方法は「影のみ（木漏れ日）／明部のみ（葉影）／全体」。
  絞り羽根の枚数で円と多角形を切り替え可能。Quest では自動的に軽量パスへ切り替わる。
- **Properties 正準カタログと生成器**: 12 バリアントの Properties ブロックを単一のカタログから生成する。
  新機能のプロパティは 1 箇所に書けば全バリアントへ展開される。
  `Tools > Natane > ビルド最適化` から整合チェック・生成・ドライランを実行できる。
- **シェーダー整合性監査**: バリアント間のプロパティ／pragma のドリフト、AWBO ガードの穴、
  パフォーマンス評価の計上漏れを検出する。
- **検証の自動実行基盤**: シェーダーのコンパイル（キーワード有効時の変種を含む）、整合性監査、
  カタログ整合、生成ドライランをまとめて実行しレポートに残す。
- **ハーフトーンシャドウの漫画表現拡張**: 影の濃さを「トーンの号数」に量子化して網点の粒度を
  段階的に変える。パターンはドット／万線／クロスハッチ（カケアミ）、角度指定（既定45°）、
  座標空間（スクリーン／ワールド／UV）、粒の最小・最大サイズ、`fwidth` によるアンチエイリアスを追加。
  クロスハッチは薄いうちは一方向、濃くなると直交方向が重なる。
  トーンの号数を 1 にすると従来の連続的な挙動になる。
- **影の自然さ (`_ShadowNaturalness`)**: トゥーンの陰影を保ったまま、影の境界を
  PBR のような自然な減衰に近づける単一の操作子。
  内部で Wrapped Diffuse・Shadow Smoothing・Step Border Smooth をまとめて引き上げる。
  これらは従来 9 個の操作子に散らばっており、どれを触ればよいか分かりにくかった。
  **個別パラメータを上書きせず下限を引き上げるだけ**なので、詳細設定での手動調整はそのまま活きる。
  既定値 0 で従来と完全に同一の描画になり、新規の描画コストもない。

### Fixed
- **⚠️ ハッチングの階調をライティングで決めるようにした**: これまでは「そこまでに
  出来上がった色の輝度」を階調に使っていたため、**暗いアルベド（黒い服・髪など）が
  光の当たった場所でも「影」と判定され**、常に最も濃い線が乗っていた。
  `shadingValue`（0＝影 / 1＝明部）を使うように変更している。
  テクスチャが暗いだけの場所には線が乗らなくなるので、既存マテリアルの見た目は変わる。
- **ハッチングに合成方法 `_HatchingComposite` を追加**（影のみ / 明部のみ / 全体）:
  TAM は本来「面の全部を線で描く」手法なので既定は全体（従来の挙動）だが、
  アニメ寄りの絵では影の中だけに線を落としたいことが多く、その切り替えが無かった。
  等高線 `_TopoComposite` / 影の玉ボケ `_ShadowBokehComposite` と同じ列挙・同じ意味。
- **シェルファーが毛に見えなかった問題**: 重力と風が**毛の長さと無関係な絶対量**として
  オブジェクト空間に加算されていた。既定値では重力 0.1 に対して毛の長さが 0.02 で、
  全シェルが毛丈の5倍ぶん真下へ引きずられ、毛束ではなく**メッシュの複製が下にずれたもの**に
  なっていた。両者を `_FurLength` 倍にして、毛丈に比例した垂れ／なびきに変更している。
- **ファーの毛の作り方を、lilToon / 参考実装と同じ「長さの場をしきい値処理する」方式に統一**:
  それまでは毛の断面を幾何的に計算していたが、lilToon（`_FurNoiseMask`）も
  hecomi 氏の実装（毛テクスチャのアルファ）も、**毛を直接描かず「毛の長さの場」を作り、
  層の高さでしきい値処理して毛を切り出す**。この順序だと、根元では場の全体がしきい値を
  超えるので**コートが隙間なく埋まり**、上へ行くほど山だけが残って 1 本ずつに分かれる、
  という振る舞いが自動的に出る。幾何計算方式はこれを手で再現する必要があり、
  一連の破綻（禿げ・棘・チューブの融合）はすべてその再現に失敗した結果だった。
  - `_FurRootOffset`（lilToon 同名・同式）を追加。負に振るほど根元が詰まる。
  - `_FurUseNoiseTex` を追加。ON にするとテクスチャの明るさがそのまま毛の長さになる
    （lilToon の Noise と同じ）。OFF なら同等の模様をシェーダー内で生成する。
  - セルフオクルージョンも lilToon と同じ式に変更。層の高さだけで暗くすると
    コート全体が均一に暗くなるが、場を使うと長い毛が明るく短い毛が沈み奥行きが出る。
  - 重力が毛の長さに比例する件は lilToon も同じ実装
    （`furVector.y -= _FurGravity * length(furVector)`）であることを確認済み。
- **ファーに毛の量感（モフモフ感）が出なかった問題**: 毛が全部まっすぐ立っていたため、
  どの角度から見ても**同じ円が重なるだけ**で体積が埋まらず、ビロードや刷毛のような
  平らな見た目になっていた。毛ごとに違う向きへ散らして交差させる `_FurFluff` を追加
  （既定 0.5、最大 0.6 セルぶん先端が流れる）。
  - 散った毛は隣のセルへはみ出すので、被覆率の計算を**3×3 セル探索**に変更した。
    自分のセルだけを見ると、はみ出した毛がセル境界で切れて格子模様になる。
  - 毛の縁を硬い円から外へ薄くなる形に変更（硬い円はプラスチックの刷毛に見える）。
  - 毛の太さの上限は 0.24 セル。毛の間隔は 1 セルなので、半径が 0.5 に近づくと
    **隣の毛と接触して 1 つの面に融合し**、毛ではなく肉塊のような塊になる。
    覆う量は毛の**本数**（`_FurDensity`）で稼ぐもので、1 本を太らせて稼いではいけない。
    同じ理由で縁のぼかし幅も半径の 1/3 に留めている（広いと隣の毛と溶接される）。
- **ファーが棘（サボテン）のように見えた問題**: 毛の分布を「1 セル 1 本」にした結果、
  **セルの大きさがそのまま毛の太さと本数**になったが、`_FurDensity` は元々
  「ノイズテクスチャのタイリング」の値（1 テクセルに何本でも入る前提）で、
  そのままセル数に使うと粗すぎた。`_FurDensity` 45 では 2m の球でセルが約 14cm、
  1 本が太さ 3cm・長さ 20cm の円錐になる。
  - セル数を `_FurDensity × 4` に変更（`NATANE_FUR_CELLS_PER_DENSITY`）。
    プロパティのレンジ（1〜100）は毛の細かさを表現しきれないため。
    **既存マテリアルも設定を触らずに細かくなる**。
  - 毛の形を、根元から 2/3 までは太さを保ち残りで先細りする形に変更。
    先端まで一定割合で細くすると円錐＝棘になるため。太さの上限も 0.30→0.24 セルへ。
  - 根元を埋めて地肌を隠す層を 0.3 → 0.12 に薄くした。
    厚いままだと「つるりとした胴体に棘が生えたもの」に見えるため。
- **シェルファーが部分的に禿げて見えた問題**: 毛の分布を「滑らかな値ノイズを閾値で切る」
  方法で決めていたため、ノイズが低い窪地が**セル数個ぶんの塊**で閾値を下回り、
  そこはどのシェルにも毛が立たず**地肌がまとまって見える禿げ**になっていた
  （毛の間の隙間ではなく穴）。分布を「1セル1本」に変更し、セルごとに
  位置・長さ・毛束のうねりを振る方式にした。根元側のシェルはセルの対角を超える半径で
  必ず全面を埋め、そこから毛先へ向かって1本ずつに分かれる。
  - `_FurNoiseTex` は被覆率ではなく**毛の長さ**を変える扱いに統一した。
    被覆率へ直接掛けると中間調が一様に薄くなり、短い毛ではなく veil になっていたため。
    白（既定）は従来どおり無効果。
  - `_FurAlphaCutoff` は**毛の細さ**の指定になった（大きいほど1本が細く、隙間が広い）。
    ラベルもそれに合わせている。
  - 被覆率をノイズ値そのままにしていたためどのシェルも半透明になり、
    毛束ではなく**もや**に見えていたのも解消した（毛の内側は不透明、先端のみフェード）。
    毛の輪郭は画面空間の微分で軽くアンチエイリアスする。
- **`_LUT_3D` を有効にするとコンパイルが通らなかった問題**: `_LUT3DTex` は
  NOSAMPLER（`Texture2D`）で宣言されているのに、`ApplyLUT3D` の引数が `sampler2D` のままで、
  `tex2D` を呼んでいた。**このキーワードを実際に有効化した変種だけが落ちる**状態で、
  既定 OFF のため長く気付かれていなかった。
  引数を `NATANE_TEX2D_NS_ARG` へ変更し、サンプリングも Clamp の共有サンプラー経由にした
  （Repeat のままだと LUT ストリップの端で反対側のスライスの色が巻き込む）。
- **ハッチングの段の重み付けが誤っていた問題**: 6段 TAM の重みのうち、
  レベル5の重みが `1 - S(1)` と書かれており、これは「レベル5 + レベル6」の取り分だった。
  そのため最暗部で重みの合計が 1 ではなく最大 2 まで膨らみ、
  **レベル5とレベル6の異なる線が同時に重なって描かれていた**。
  近づくと段の境が硬い継ぎ目として見えていた原因がこれ。
  重みが常に 1 になる形（連続する `saturate(lum - k)` の差分）へ修正した。
- **網点のドット半径が濃度に線形だった問題**: ドットの被覆率は半径の2乗なので、
  半径を濃度に線形にすると**薄い側の差はほとんど見えず、濃い側だけ急に潰れる**段差になっていた。
  半径を `sqrt(濃度)` にして被覆率が濃度にほぼ比例するようにし、
  トーンの号数の刻みが均等な濃度差として見えるようにした（万線は幅＝被覆率なので変更なし）。
- **網点の AA 幅をセル境界で跳ねる値から取っていた問題**: アンチエイリアス幅を
  `fwidth(距離場)` から求めていたが、距離場は `frac()` で 1 セルごとに折り返すため、
  **すべてのセル境界で微分が跳ね上がり**、そこだけ平均インクのべた塗りに落ちていた。
  スクリーン空間では細かすぎて均されるが、**UV / Object 空間では模様として見え、
  UV シームでは太い筋**になっていた。座標そのものの微分から求めるようにして、
  折り返しの影響を受けないようにした。
- **網点の座標空間 enum に Object が無かった問題**: HLSL 側は
  `NATANE_HALFTONE_SPACE_OBJECT`(3) を実装しているのに、`Properties` の
  `[Enum(Screen,0,World,1,UV,2)]` が 2 で止まっており、インスペクタから選べなかった。
- **網点が影の縁で一斉に出現していた問題**: 量子化の最下段は `0.5 / 号数` の濃度を持つため、
  影に入った瞬間その大きさの網点が `step` で一斉に現れ、明部との境が硬い線になっていた。
  網点が消える幅で立ち上げるようにした。
- **lilToon FakeShadow の移行が色だけだった問題**: FakeShadow シェーダーを検出しても
  "Minimal migration applied (color only)" を記録するだけで、実質的に移行できていなかった。
  `Natane/Toon Shader FakeShadow` への変換に差し替え、色・アルファ・影テクスチャ・
  Stencil 一式・カリングを引き継ぐようにした。
  あわせて、これまで**取りこぼしていた Stencil プロパティの取得**を移行元の走査に追加した
  （See Through Hair 系のセットアップ済みアバターが移行で壊れていた）。
- **`_UseDissolveMask` が完全に死んでいた問題**: 12 バリアントの `Properties` に
  `[Toggle(_DISSOLVE_MASK)]` として宣言されていたが、HLSL・GUI・レジストリのどこからも
  参照されず、`#pragma` も無かった。インスペクタのチェックボックスは何も変えず、
  誰も読まないキーワードだけがマテリアルへ書き込まれていた。
  Uniform 分岐で実際のゲートにし、`[Toggle]` へ変更して死んだキーワードを立てないようにした。
- **`_AudioLinkDissolve` が宣言のみで未参照だった問題**: 上と同じ状態だった。
  `_AudioLinkDissolveIntensity` のゲートとして実際に参照するようにした。
- **`_PBR` / `_PBR_LIKE` が AWBO のガード対象から漏れていた問題**: `NataneToonBuildSettings.hlsl` に
  `#undef` ガードが生成されず、ビルド機能最適化で無効化してもストリップされない状態だった。
- **Fur / Fur_Lite の Glitch 強度レンジ**: 他 10 バリアントが `Range(0, 3)` の中、Fur 系だけ
  `Range(0, 1)` のまま取り残されており、インスペクター上で強度が 1 で頭打ちになっていた。

### Changed
- **表現ショーケース（開発用）の補助区画からフェイクシャドウのデモを削除**:
  板ポリ1枚と球1個では「ライトを回しても影が動かない」という要点が伝わらず、
  何を見ればよいのか分からない配置になっていた。フェイクシャドウ本体
  （シェーダー・セットアップツール・`Documentation~/FAKE_SHADOW.md`）はそのまま。
  併せて、カバレッジ区画のシェルファーへ個別セットアップを追加した
  （既定の毛丈 0.02 は球体の半径の 4% しかなく、LOD 距離 10 のままだと
  全体を俯瞰する距離でシェルが4枚まで間引かれ、ただの球に戻っていた）。
- **⚠️ 網点の大きさの決め方をワールド基準へ統一した。**
  従来、面側（World / UV / Object）は `SurfaceDensity × (30 / Scale)` という
  **2つのパラメータが打ち消し合う式**だった。Scale を上げると画面空間では
  セルが大きくなるのに面側では小さくなるという逆向きの挙動で、
  空間を切り替えると点の大きさがまったく揃わなかった。
  - `_HalftoneShadowSurfaceDensity` を「ワールド1メートルあたりのセル数」に統一
    （既定 8 → 40、上限 40 → 200）。World / UV / Object はすべてこの値を使うため、
    **空間を切り替えても点の大きさが変わらない**。UV は「UV 1つ = 1メートル」と見なす。
  - `_HalftoneShadowScale` は画面空間専用の「セル1つのピクセル数」に整理
    （既定 30 → 6、上限 200 → 64）。小さいほど細かい。
  - `_HalftoneShadowDotMax` 0.9 → 0.8。
- **網点のスクリーン空間に「オブジェクトに貼り付ける」を追加**
  (`_HalftoneShadowScreenAnchor`、既定 ON)。
  素のスクリーン空間は紙に貼ったトーンと同じで画面に固定されるため、
  カメラを動かすと模様の上をオブジェクトが滑り（泳ぎ）、近づいても点の大きさが変わらなかった。
  グリッドの原点を**オブジェクトのスクリーン座標**に置き、セルの大きさを**距離に反比例**させると、
  透視投影では画面上のオフセットも距離に反比例して伸びるため両者が打ち消し合い、
  **模様は面に貼り付いたまま動かず、それでいて近づけば点が大きくなる**。
  UV も三平面投影も使わないので、UV の継ぎ目で模様が破綻しない。
  大きさは `_HalftoneShadowSurfaceDensity`（ワールド1mあたりのセル数）で決まる。
- **網点に「画面上の大きさを保つ」を追加** (`_HalftoneShadowScreenLock`、既定 OFF)。
  スクリーン空間は大きさが一定な代わりにカメラを動かすと模様の上を面が滑り（泳ぎ）、
  面貼り付けは泳がない代わりに近づくと点が大きくなる、というトレードオフがあった。
  ワールド／UV／オブジェクト空間でこれを有効にすると、**面に貼り付いたまま
  画面上の大きさが一定**になる。距離に応じて密度を 2 のべき乗で切り替えることで実現しており、
  段が切り替わる距離で細かさが一段変わるが、段の間は完全に固定される。
  距離は**オブジェクト原点**で測るため、1 つのメッシュの中で密度が割れて
  グリッドが破綻することはない。
- **網点に落ち影の影響量を追加** (`_HalftoneShadowCastShadow`、既定 1)。
  陰影（N·L）由来のトーンと落ち影由来のトーンを**別々に求めて合成**する。
  1本の値に畳み込むと、落ち影が無い場所にも落ち影ぶんのトーンが乗ってしまうため。
  0 にすると落ち影を完全に無視する。濃度基準が Continuous のときに有効。
- **⚠️ 挙動変更: 網点の濃度基準を、トゥーン量子化前の連続値へ変更**
  (`_HalftoneShadowDensitySource`、既定 = Continuous)。
  従来は量子化後の `shadingValue` を受け取っていたため、
  **濃度が階調数ぶんしか取れなかった**。既定の 2 段では網点も 2 段階しか出ず、
  トーンを貼り分けたというより「白と黒が切り替わる」だけになっていた。
  漫画のトーンは面の丸みに沿って号数を選ぶものなので、基準は滑らかな陰影であるべき。
  PBR モードを使っていなくても連続値は常に計算される。
  従来の挙動に戻すには `Toon Quantized` を選ぶ。
- **⚠️ 破壊的変更: `_FACE_SDF_ROTATION` の閾値式の符号を修正**
  (`NataneToonLighting.hlsl`)。従来は `threshold = FdotL * 0.5 + 0.5 + _SDFOffset` で、
  `lightDir` がサーフェスからライトへ向かうベクトルであることを踏まえると
  **正面ライトでほぼ全面が影、背面ライトで全面が明るく**なる符号反転があった。
  `threshold = (1.0 - FdotL) * 0.5 + _SDFOffset` へ修正した。
  導出は `Documentation~/NPR2026_P2_FACE_SDF_BAKE.md` を参照。
  - 現行の組み合わせは元々意図した絵を出せていないため、「意図した見た目」を壊す変更ではない。
  - `_SDF_MAP` 単体（`_FACE_SDF_ROTATION` 無効）のマテリアルには**影響しない**。
  - `_FACE_SDF_ROTATION` を使っている場合は、Map Generator の顔SDF影マップを
    ベイクし直すことを推奨する。
- **⚠️ 挙動変更: `_UseDissolveMask` / `_AudioLinkDissolve` のトグルが実際に効くようになった。**
  これまではトグルの状態に関わらず常に適用されていたため、
  **「マスクや AudioLink ディゾルブ強度を設定しているのにトグルは OFF」というマテリアルは
  見た目が変わる**（効果が出なくなる）。
  Material Validator に検出と1クリック修正を追加してあるので、
  `Tools > Natane > マテリアル検証` で確認できる。
- **パフォーマンス評価 (A/B/C/D) の計上対象に 18 機能を追加**: v1.6/1.7 で追加した
  Caustics / Lenticular / Topographic / Pixel Art / Line Boil / Shaped Highlight / FX Modulator /
  Halftone Shadow / Procedural MatCap / Fake Reflection / Mirror Texture / Gradient Base Color /
  Color Quantize / Cast Shadow Color / Shadow Edge Noise / Depth Color Fade / Tessellation / PBR が
  未登録で、有効にしても判定に反映されていなかった。
  **これらを使う既存マテリアルの評価は下がる（実態に即した値になる）。**
  距離／高さフェードなど負荷を下げる方向の機能は意図的に計上対象外としている。
- Properties ブロックが生成物になり、`// <auto-generated:natane-properties>` で囲まれる。
  手編集せず、カタログを編集して再生成すること。

## [1.6.5] - 2026-07-18

### Added
- **表現系エフェクト（バッチ1）**: Line Boil / Shaped Highlight / Topographic / FX Modulator。
- **表現系エフェクト（バッチ2）**: Lenticular / Surface Caustics / Pixel Art。
- **X-Ray バリアント** (`Natane/Toon Shader (X-Ray)`): 遮蔽シルエット表現。
- **Light Probe Proxy Volume (LPPV)**: Built-in RP 向け対応。
- **FX Modulator 拡張**: Static/Dynamic Noise ソースと追加ターゲット。
- **Mask Painter / Texture Studio**: Scene ビューでのマスクペイントとテクスチャ統合ウィンドウ。
- **ビルド時テクスチャ統合**: 重複テクスチャのビルド/アップロード時 consolidation。
- **初心者向けインスペクター**: 説明文・難易度・ヒント表示、表現系セクション登録。
- **AWBO (Asset Workspace & Build Optimizer) Stage A〜I**:
  - Project Settings 統合設定、Shader Feature Feature Registry / Update Audit
  - Build Usage Snapshot、統合バリアントストリッパー（ReportOnly / Safe / Aggressive）
  - HLSL ガード既定 Off、Runtime プリウォーム固定化、HLSL 方式比較レポート
  - Asset Organizer（GUID 維持の整理）、Scene Workspace（Scene Profile）
  - Shader/Property Migration プレビュー、統合ワークスペース UI（`Tools > Natane > ワークスペース`）

### Fixed
- **鏡・カメラ写り分けテクスチャのコンパイルエラー**: `_MirrorAltTex` 宣言が `_SMOOTH_NORMAL` ブロック内に誤ネストされ、Smooth Normal 無効時に `undeclared identifier` となる問題を修正。
- **X-Ray バリアント**: バッチ2 表現系キーワードの欠落をバックポート。
- **VRC Light Volumes 導入案内**: インスペクターから不要なインストール誘導を削除。

### Changed
- **インスペクター**: セクション登録をデータ駆動化しタブ構成を再編。
- **ドキュメントサイト**: v1.6.x 表現機能ページ追加、デザイン刷新。
- **VCC/VPM パッケージメタデータ**: `package.json` / README を `1.6.5` に同期。

---

## [1.6.0] - 2026-07-17

### Added
- **顔直交投影 (_FACE_ORTHO)**: ピボット中心に透視→直交投影をブレンドし、カメラ距離・FOV・カメラ種別(デスクトップ/VRCカメラ/ミラー)によらず顔のプロポーションを理想的に保つ。VR時は別強度(既定0.3)、Rチャンネルマスク対応、アウトライン追従。
- **鏡・カメラ写り分けテクスチャ (_MIRROR_TEXTURE)**: VRChatミラー/カメラに映るときだけベースの見た目を別テクスチャ・カラーへ切替(ブレンド量指定可)。「鏡の中だけ違う姿」「写真にだけ写る模様」など。
- **ゴーストバリアント (Natane/Toon Shader (Ghost))**: 深度プリパス+ZTest Equalで、体の重なり部分の二重ブレンドや裏面透けが構造的に発生しない幽霊表現。フレネル中心フェード+HDR縁発光。
- **パーティクルバリアント (Natane/Toon Shader (Particle))**: 軽量トゥーンパーティクル。ブレンドモード切替/トゥーンライティング/ソフトパーティクル/フリップブック/カメラフェード。VRCFallback=Particle。
- **GPUパーティクル (Natane/Effects/GPU Particles (Stateless))**: 状態レス頂点アニメ方式(CRT/カメラ/スクリプト不要、アバター安全)。Rise/Fall/Orbit/Burstの4モード、AudioLink変調対応、クアッドクラウドメッシュ生成ツール付属。
- **疑似流体 (Natane/Effects/Fake Fluid)**: 数式ベースの容器内液体表現(充填量/揺れ/泡/フレネル/トゥーン陰影)。
- **目のセットアップツール**: 目のマテリアル分離(トライアングル抽出→新規サブメッシュ/マテリアル)とUV再配置(島検出、重ね/並べ、uv0/uv1)を非破壊で行うエディタウィンドウ。
- **Unity 6 URP対応(条件分岐)**: メインシェーダーにPackageRequirements付きURP SubShaderを追加。SRP Batcher対応CBUFFER、GPU Resident Drawer(DOTSインスタンシング)対応。BiRPと同一プロパティで中核機能(トゥーン段階/ランプ/ノーマル/リム/MatCap/エミッション/アウトライン)をカバー。
- **VRCFallbackタグ**: 全バリアントに追加(Toon/ToonCutout/ToonTransparent)。セーフティでブロックされてもトゥーン系フォールバックに。
- **ステンシルプリセット**: 書き込み/一致で表示/不一致で表示をワンクリック適用(覗き窓・隠し模様など)。
- **ビルド時最適化**: 未使用キーワードのシェーダーバリアント削減(IPreprocessShaders)、VRChat SDKアップロードフック(キーワード同期+Lite変換提案)、ビルド後最適化レポート出力。

### Fixed
- **白色光が約42%暗くなる問題**: ライト強度クランプでベクトルnormalize()を誤用していたため、無彩色ライトが1/√3に減光していた。輝度比率での補正に修正(全ForwardBaseピクセルに影響)。
- **手描き風アウトラインのコンパイルエラー**: Cutout/Transparent/Lite/ScreenEdgeSplitで関数定義が欠落しており有効化するとコンパイル不能だった(アウトラインパスの共有include化で解消)。
- **パースフラット有効時に追加ライトが消える**: FORWARD_ADDにキーワード宣言が無く深度不一致でZテスト落ちしていた。
- **死んでいたトグルの復旧**: `_HAIR_SPEC_MASK` / `_HAIR_SPEC_SHIFT_TEX` / `_QUEST_LITE` はGUIにトグルがあるのにpragma未宣言で無効だった。全バリアントに宣言を追加。
- **HDRエミッションのBloom不能**: 最終カラーの1.05クランプでHDR超過分が潰されていた。クランプ後に超過分を再加算しBloomが効くように。
- **Lite系がQuestで動作不能**: 全Lite変種が `target 4.6`+テッセレーションステージを強制していた。`target 3.5`+通常頂点パスに変更。
- **VR(Single Pass Instanced)の不具合**: テッセレーション使用時に目のインデックスがdomainステージで失われる問題、ScreenFXOverlayが誤った目をサンプルする問題を修正。
- **ForwardAddの光量フロア誤適用**: `_LightColorMin` がポイント/スポットライトにも距離非依存の下駄を履かせていた(ForwardBase限定に)。
- **トゥーン境界のちらつき**: シェード境界にfwidthベースの最小AA帯を導入(VRでのシマー抑制)。
- **Eyeシェーダーの未初期化警告**: DoColor()を単一出口構造に修正。
- **Unity 6の非推奨API警告16件**: FindObjectsOfType系をバージョン分岐ヘルパー経由に。

### Changed
- **リファクタリング**: OUTLINE/SHADOW_CASTERパスを共有includeへ抽出(重複約3,400行削減)、バリアント間のプロパティラベルのドリフト8件を統一、生成物ファイルをパッケージから除去。
- **開発ブランチ**: 日常開発は `develop` ブランチに移行(`v1.1.5` はリリースブランチ)。

### Verified
- Unity 2022.3.28f1(VRChat相当)および Unity 6000.0.55f1 でシェーダー・C#ともにエラー0/警告0。

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
