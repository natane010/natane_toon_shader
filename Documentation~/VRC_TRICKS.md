# VRChat 不思議機能ガイド (VRC Tricks)

VRChat専用のグローバル変数とステンシルを活用した「不思議系」演出のガイドです。

## 鏡・カメラ写り分けテクスチャ (Mirror/Camera Alt Texture)

**Advanced タブ → 鏡・カメラ写り分けテクスチャ**

VRChatのミラーやカメラに映ったときだけ、ベースカラーを別のテクスチャ・カラーに切り替えます。

| プロパティ | 説明 |
|---|---|
| `_MirrorAltTex` / `_MirrorAltColor` | 鏡・カメラ内での見た目 |
| `_MirrorTexBlend` | 切り替えの強さ (0.5で半透過的な「二重の姿」) |
| `_MirrorTexApplyMirror` | ミラーに適用 (既定: ON) |
| `_MirrorTexApplyCamera` | VRChatカメラ(手持ち/デスクトップ/スクショ)に適用 |

**演出例**:
- 直接見ると普通の服、鏡の中では模様が浮かぶ
- 写真にだけ写るメッセージ/透かし
- 鏡の中の自分だけ色違い・オーラ付き

陰影・リムライト等のシェーディングは切り替え後の見た目にもそのまま適用されます。ミラー内で完全に消えたい/ミラーにだけ映りたい場合は既存の「ミラー・カメラ制御」を使ってください。

## ステンシルプリセット

**Advanced タブ → レンダリング設定 → Stencil → プリセット**

| プリセット | 設定値 | 用途 |
|---|---|---|
| 標準 | Ref=0, Comp=Always, Pass=Keep | ステンシル不使用(既定) |
| 書き込み | Ref=1, Comp=Always, Pass=Replace | マスク領域を書き込む側 |
| 一致で表示 | Ref=1, Comp=Equal, Pass=Keep | マスク越しにだけ見える側 |
| 不一致で表示 | Ref=1, Comp=NotEqual, Pass=Keep | マスク領域では消える側 |

**組み方の基本**: 「書き込み」マテリアル(例: 眼鏡のレンズ、窓、魔法陣)を先に描画し(Render Queueを表示側より小さく)、「一致で表示」を適用したマテリアルはそのマスク越しにだけ描画されます。

**演出例**:
- **覗き窓**: レンズ=書き込み、レンズ越しにだけ見える「本当の姿」=一致で表示
- **隠し模様**: 特定のアイテムをかざしたときだけ見えるタトゥー・紋章
- **消える体**: 魔法陣の上では体の一部が消える(不一致で表示)
- 複数系統のマスクを使う場合は参照値(1〜255)を系統ごとに変えてください

⚠️ VRChatでは他人のシェーダーもステンシルバッファを共有します。Ref値は控えめにユニークな値を選ぶと事故が減ります。

---

# VRC Tricks (English)

## Mirror/Camera Alt Texture
Swaps the base color only when rendered in a VRChat mirror or by the VRChat camera (handheld/desktop/screenshot). Use for mirror-only appearances, photo-only watermarks, or partial-blend "second self" effects. Shading applies to the swapped look. To fully hide/show in mirrors use Mirror/Camera Control instead.

## Stencil Presets
One-click Writer / Reader (Equal) / Reader (NotEqual) presets in Rendering Settings → Stencil. Apply "Writer" to the masking material (lens, window, magic circle) with a lower render queue, and "Reader" to the material that should only appear through the mask. Match Reference values per mask group; VRChat shares the stencil buffer with other avatars, so pick uncommon Ref values.
