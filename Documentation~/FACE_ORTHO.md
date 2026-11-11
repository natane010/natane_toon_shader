# 顔直交投影 (Face Ortho Projection)

カメラの距離・FOV・種類(デスクトップ / VRChatカメラ / ミラー)にかかわらず、顔の見た目を常に理想的なプロポーションに保つ機能です。ピボット(頭部)を中心に、対象頂点のスクリーン投影を透視投影から直交投影へブレンドします。

## 使い方

1. 顔のマテリアルで **Advanced タブ → 顔直交投影** を開き「顔直交投影を有効化」をON
2. **ピボット位置** をオブジェクト空間で頭部に合わせる(Humanoidアバターなら概ね `Y=1.4` 前後。アバターのスケールに応じて調整)
3. 顔以外のジオメトリを含むマテリアルでは **マスクテクスチャ** (Rチャンネル=適用度) を設定し、首の境目はグラデーションでぼかす

## パラメータ

| プロパティ | 説明 |
|---|---|
| `_FaceOrthoAmount` | 直交化の強さ。0=通常の透視投影、1=完全な直交投影 |
| `_FaceOrthoVRAmount` | VR(ステレオレンダリング)時の強さ。完全な直交化は立体視の奥行き知覚と干渉するため、デフォルト0.3。違和感がある場合は0に |
| `_FaceOrthoPivot` | ピボット位置(オブジェクト空間)。SkinnedMeshRendererのルートトランスフォーム基準 |
| `_FaceOrthoMaskTex` | 適用範囲マスク(R)。白=適用、黒=非適用 |

## 技術メモ

- 頂点シェーダーでNDC空間の`xy`のみを変形します。深度(`z/w`)・ワールド座標・ライティングは通常の透視投影のまま維持されるため、影・ライト・エフェクトの整合性は保たれます
- 直交投影の等価式: 各頂点のパースペクティブ除算係数`w`をピボットの`w`に置き換え(`ndcOrtho = clip.xy / pivotW`)、`_FaceOrthoAmount`でブレンド
- アウトラインパス(`NataneToonOutlinePass.hlsl`)にも同一の変形を適用しており、アウトラインは顔に追従します
- FORWARD_BASE / FORWARD_ADD 両方で適用されるため、追加ライトの深度整合も保たれます
- ピボットがニアプレーン以遠にない場合(`w <= 0.01`)は自動的に無効化されます

---

# Face Ortho Projection (English)

Keeps the face ideally proportioned at any camera distance, FOV, or camera type (desktop / VRChat camera / mirrors) by blending the screen-space projection of masked vertices from perspective toward orthographic around a pivot.

## Usage

1. On the face material, open **Advanced tab → Face Ortho Projection** and enable it
2. Set **Pivot (Object Space)** to the head position (roughly `Y=1.4` for humanoid avatars; adjust for avatar scale)
3. For materials that include non-face geometry, assign a **mask texture** (R channel = amount) and feather the neck boundary

## Notes

- Only NDC `xy` is transformed in the vertex stage; depth, world position, and lighting stay perspective-correct, so shadows/lights/effects remain consistent
- The outline pass applies the identical transform so outlines track the flattened face
- In VR, `_FaceOrthoVRAmount` (default 0.3) is used instead — full orthographic faces fight stereo depth cues
