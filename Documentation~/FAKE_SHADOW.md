# Fake Shadow（前髪の落ち影）

前髪が顔に落とす影を、**ライティングに依存せず**板ポリまたは複製メッシュで擬似的に描く機能。

シェーダー: `Natane/Toon Shader FakeShadow`
セットアップ: `Tools > Natane > エフェクト Effects > フェイクシャドウ設定`

---

## 1. なぜ必要か

VRChat で「2Dライクなルック」を作るときの定番手法。
実際のライティングで落とす影（`_PCSS` / `_CAST_SHADOW_COLOR`）は、

- ワールドのライト方向に左右されるので、狙った位置に影が出ない
- 暗いワールドでは影そのものが消える
- 明るいワールドでは薄くなりすぎる

という問題があり、「常に同じ位置・同じ濃さで前髪の影を出したい」という用途には向かない。
そこで**影の形をした板ポリを顔の前に置く**。

本パッケージにはこれまで相当機能が無く、lilToon の FakeShadow から移行してきたユーザーは
前髪の落ち影を失っていた（移行ツールは色のみの最小移行だった）。

## 2. 2つの方式

| 方式 | 長所 | 短所 |
|---|---|---|
| **板ポリ** | 軽い（三角形2枚）。頭ボーンに追従させるだけ | 形がテクスチャ依存。髪型を変えると作り直し |
| **メッシュ複製** | 形が正確。髪型に自動で一致する | ポリゴン数が増える。前髪の面数がそのまま乗る |

どちらも**非破壊**。元の Renderer は変更せず、子 GameObject として追加する。
生成したマテリアルとメッシュは `Assets/NataneToonGenerated/FakeShadow/` へ保存する
（パッケージ内には書かない。更新時に消えるため）。

### 板ポリ方式

1. 顔の Renderer か、追従させる頭ボーンを指定
2. 前髪のシルエットを模したアルファ付きテクスチャを `_ShadowTex` に指定
3. 幅・高さ・位置オフセットを調整

**影の形テクスチャを指定しないと Quad 全面が影になる。** 必ず用意すること。

### メッシュ複製方式

1. 前髪の Renderer を指定
2. 押し出し量を調整（既定 0.002 m）

押し出さないと元メッシュと完全に同一面になり、Z ファイティングでちらつく。
`SkinnedMeshRenderer` の場合はボーン参照とブレンドシェイプの重みを引き継ぐので、
表情や揺れにそのまま追従する。

## 3. プロパティ

| プロパティ | 既定 | 内容 |
|---|---|---|
| `_ShadowColor` | (0.55, 0.5, 0.6, 1) | 影色 |
| `_ShadowAlpha` | 0.5 | 不透明度 |
| `_ShadowTex` | `"white"` | 影の形（アルファを使う） |
| `_LightColorFollow` | 0.3 | ライト色への追従量。0 で完全にライト非依存 |
| `_FadeByViewAngle` | 0 | 正面から見たときに薄くする量 |
| `_Cull` | Back | カリング |
| `_OffsetFactor` / `_OffsetUnits` | 0 | 深度オフセット |
| Stencil 一式 | — | 本体シェーダーと同じ命名 |

### `_LightColorFollow` の既定を弱めにしている理由

完全追従（1.0）にすると、暗いワールドで落ち影が消え、明るいワールドで白飛びして
「影に見えない」状態になる。ライト非依存が本機能の主旨なので、既定は 0.3 にしてある。

## 4. レンダリング設定

```
Tags { "Queue" = "AlphaTest+50" "RenderType" = "Transparent" "VRCFallback" = "ToonTransparent" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
ZTest LEqual
Cull Back
```

- 顔マテリアルの**直後**に描くため `AlphaTest+50`
- `ZWrite Off` で他の半透明の並び順を壊さない
- `VRCFallback` を `ToonTransparent` にして、セーフティでブロックされたときに落ち影が真っ黒にならないようにしている

## 5. ステンシルで顔からはみ出さないようにする

落ち影が顔の輪郭からはみ出すと破綻する。ステンシルで「顔が書いた領域のみ」に制限できる。

1. 顔のマテリアル: `_StencilRef = N` / `_StencilComp = Always` / `_StencilOp = Replace`
2. 落ち影のマテリアル: `_StencilRef = N` / `_StencilComp = Equal` / `_StencilOp = Keep`

参照値の割り当ては[ステンシルプリセットツール](STENCIL_PRESET.md)から両者まとめて設定できる。

**注意**: VRChat では他アバターのステンシルと衝突しうる。
ステンシルプリセットツールが使用中の値を検出して未使用値を提案する。

### See Through Hair と併用する場合

眉・目を前髪より前に描く構成（See Through Hair 相当）と併用するときは、
**落ち影が眉の上に乗ると破綻する**。両者の参照値を揃えて、
落ち影が Writer の書いた領域を避けるようにする。
ステンシルプリセットツールが自動で設定する。

## 6. パフォーマンス

| 方式 | 追加コスト |
|---|---|
| 板ポリ | 三角形2枚 + 半透明 1 ドローコール |
| メッシュ複製 | 前髪の面数がそのまま + 半透明 1 ドローコール |

半透明のオーバードローが増えるため、Quest では**板ポリ方式を推奨**する。
`NataneToonCore.hlsl` を include しないので、シェーダー自体の命令数は極めて少ない。

## 7. lilToon からの移行

`Tools > Natane > 移行 Migration > lilToon Migration Tool` が
lilToon の FakeShadow シェーダーを検出すると、このシェーダーへ自動変換する。

| lilToon | Natane |
|---|---|
| `_Color` | `_ShadowColor` |
| `_Color` のアルファ | `_ShadowAlpha` |
| `_MainTex` | `_ShadowTex` |
| Stencil 一式 | 同名なのでそのまま引き継ぐ |
