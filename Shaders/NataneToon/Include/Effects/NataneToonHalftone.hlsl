// NataneToonHalftone.hlsl
// Manga halftone / 漫画網点.
//
// 影の濃さを離散的な「トーンの号数」に量子化し、その段階に応じて網点の粒度を
// 変える。実物のスクリーントーンが 10%・20%・30% と決まった濃度で売られている
// のと同じ考え方で、連続的に太らせるより漫画らしくなる。
//
// 網点はドット / 万線 / クロスハッチを選べ、角度を付けられる（漫画の網点は
// 45° が基本）。エッジは画面微分でアンチエイリアスするので、拡大しても
// 階段状にならない。
//
// ここは純粋関数のみ。座標の作り方と色の合成はフラグメント側で行う。
#ifndef NATANE_TOON_HALFTONE_INCLUDED
#define NATANE_TOON_HALFTONE_INCLUDED

#define NATANE_HALFTONE_DOT        0
#define NATANE_HALFTONE_LINE       1
#define NATANE_HALFTONE_CROSSHATCH 2

#define NATANE_HALFTONE_SPACE_SCREEN 0
#define NATANE_HALFTONE_SPACE_WORLD  1
#define NATANE_HALFTONE_SPACE_UV     2
#define NATANE_HALFTONE_SPACE_OBJECT 3

/// 網点グリッドの座標を作る。
///
/// スクリーン空間は「セル1つあたりのピクセル数」、面に貼り付ける空間は
/// 「単位あたりのセル数」と、本来スケールの意味が逆になる。ここで吸収して
/// 「scale を上げるほど網点が粗くなる」という向きに揃える。
///
/// ワールドは Triplanar-lite（支配的な法線軸に正対する平面を選ぶ）を使う。
/// XZ 平面固定にすると壁やキャラの体のような垂直面で投影が潰れ、
/// 網点が線状に伸びてしまう。
/// 網点1セルの大きさは <b>ワールド基準</b>で決める。
///
/// 以前は面側だけ surfaceDensity × (30 / scale) という、2 つのパラメータが
/// 打ち消し合う式になっていた。Scale を上げると画面空間ではセルが大きくなるのに
/// 面側では小さくなる、という逆向きの挙動で、空間を切り替えると点の大きさが
/// まったく揃わなかった。
///
/// いまは surfaceDensity が「ワールド1メートルあたりのセル数」で、
/// World / Object / UV はすべて同じ数値を使う。したがって空間を切り替えても
/// 点の大きさは変わらない（UV は「UV 1 つ = 1 メートル」と見なす。
/// UV の密度はモデル依存なので、これは規約として決め打ちしている）。
///
/// Screen だけは別。紙に貼ったトーンと同じで、奥行きに関係なく画面上で
/// 一定の大きさであることに意味があるため、scale を「セル1つのピクセル数」として使う。
/// 画面上のセルの大きさを一定に保ったまま、面に貼り付けたままにするための密度。
///
/// スクリーン空間は大きさが一定な代わりに、カメラを動かすと模様の上を
/// オブジェクトが滑る（泳ぐ）。面貼り付けは泳がない代わりに、
/// 近づくと点が大きくなる。両立させるには、面に貼り付けたまま
/// <b>距離に応じて密度を切り替える</b>しかない。
///
/// 距離は<b>オブジェクト原点</b>で測る。フラグメントごとの距離で測ると、
/// 同じ面の中で密度が変わってグリッドが割れる（frac の周期が画素ごとに違ってしまう）。
/// オブジェクト単位なら面全体で密度が一定になり、継ぎ目が出ない。
///
/// 密度は 2 のべき乗へ丸める。連続的に変えるとパターンが伸び縮みして
/// 結局「泳ぐ」ので、段階的に切り替えて段の間は完全に固定する。
float NataneHalftoneScreenLockedDensity(float targetPixels)
{
    float3 objectOrigin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
    float dist = max(distance(NataneViewerPos(), objectOrigin), 1e-3);

    // UNITY_MATRIX_P._m11 = 1 / tan(fov/2)。画面高さ 1 単位あたりの投影係数。
    float projScale = abs(UNITY_MATRIX_P._m11);
    float pixelsPerWorldUnit = projScale * _ScreenParams.y * 0.5 / dist;

    float desired = pixelsPerWorldUnit / max(targetPixels, 1e-3);
    // 2 のべき乗へ丸める。段が切り替わる瞬間だけ密度が倍/半分になる。
    return exp2(round(log2(max(desired, 1e-6))));
}

/// スクリーン空間の網点を、オブジェクトに貼り付いた見え方にする。
///
/// 素のスクリーン空間は「紙に貼ったトーン」で、画面に対して固定される。
/// そのためカメラを動かすと模様の上をオブジェクトが滑り、
/// 近づいても点の大きさが変わらない。
///
/// グリッドの原点を<b>オブジェクトのスクリーン座標</b>に置き、
/// セルの大きさを<b>距離に反比例</b>させると、この 2 つが同時に解ける。
///
/// 理由: 透視投影では、オブジェクト上の点の画面上でのオフセット d は
/// 距離に反比例して伸びる。セルの画素サイズも同じく距離に反比例させると、
/// coord = d / cellPixels が距離によらず一定になる。
/// つまり模様は面に貼り付いたまま動かず、それでいて近づけば点は大きくなる。
///
/// UV も三平面投影も使わないので、UV シームや軸の切り替わりで模様が破綻しない。
/// （深度がアンカーと違う部分にはわずかな視差が残るが、実用上は問題にならない）
float2 NataneHalftoneObjectAnchoredScreenCoord(float2 screenPos, float density)
{
    float3 objectOrigin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);

    // オブジェクト原点をスクリーン座標（ピクセル）へ。
    float4 clip = mul(UNITY_MATRIX_VP, float4(objectOrigin, 1.0));
    // Origin beside / behind the camera: keep the projected anchor in front instead of
    // mirroring it through abs(w). Identical while the origin is within ~75 degrees of the view axis.
    if (!NataneIsOrthoCamera())
        clip.w = max(clip.w, 0.25 * max(distance(_WorldSpaceCameraPos, objectOrigin), 1e-3));
    // SV_POSITION convention (top-down on D3D, stereo offset) so the grid follows the object.
    float2 anchor = NataneClipToPixel(clip);

    float dist = max(distance(NataneViewerPos(), objectOrigin), 1e-3);
    float projScale = abs(UNITY_MATRIX_P._m11);
    float pixelsPerWorldUnit = projScale * _ScreenParams.y * 0.5 / dist;

    // ワールドでのセル 1 つの大きさ（1 / density メートル）を画素へ換算する。
    float cellPixels = max(pixelsPerWorldUnit / max(density, 1e-3), 1e-3);

    return (screenPos - anchor) / cellPixels;
}

float2 NataneHalftoneCoord(float space, float scale, float surfaceDensity, float screenLock,
                           float screenAnchor,
                           float2 uv, float3 objPos, float3 worldPos, float3 worldNormal,
                           float2 screenPos)
{
    int mode = (int)(space + 0.5);
    float safeScale = max(scale, 1e-3);
    float density = max(surfaceDensity, 1e-3);

    if (mode == NATANE_HALFTONE_SPACE_SCREEN)
    {
        // オブジェクトに貼り付けた画面グリッド。泳がず、近づけば点が大きくなる。
        if (screenAnchor > 0.5)
        {
            return NataneHalftoneObjectAnchoredScreenCoord(screenPos, density);
        }

        // 素のスクリーン空間（紙に貼ったトーン）。scale = セル 1 つのピクセル数。
        return screenPos / safeScale;
    }

    // 画面基準の大きさに合わせる場合は、密度を距離から導く。
    // このとき surfaceDensity は使わない（大きさは scale = 画面ピクセル数で決まる）。
    if (screenLock > 0.5)
    {
        density = NataneHalftoneScreenLockedDensity(safeScale);
    }

    if (mode == NATANE_HALFTONE_SPACE_WORLD)
    {
        // NataneProjectionCoord のモード 3 = Triplanar-lite
        float2 base = NataneProjectionCoord(3.0, uv, objPos, worldPos, worldNormal);
        return base * density;
    }

    if (mode == NATANE_HALFTONE_SPACE_UV)
    {
        return uv * density;
    }

    if (mode == NATANE_HALFTONE_SPACE_OBJECT)
    {
        // モード 1 = Object。スキニングの影響を受けないので、
        // アニメーションしても網点が張り付いたままになる。
        float2 base = NataneProjectionCoord(1.0, uv, objPos, worldPos, worldNormal);
        return base * density;
    }

    // 未知の mode。UV へ倒す（Screen は関数の先頭で処理済み）。
    return uv * density;
}

// 影の濃さを段階に量子化する。levels <= 1 なら量子化しない（連続）。
// 段の中央値を返すので、最も薄い段でも網点が消えず、最も濃い段でも潰れない。
float NataneHalftoneQuantizeTone(float tone, float levels)
{
    if (levels < 1.5) return tone;
    float n = floor(levels);
    return saturate((floor(saturate(tone) * n) + 0.5) / n);
}

// 網点グリッドの回転。漫画の網点は 45° に振るのが基本。
float2 NataneHalftoneRotate(float2 p, float angleDegrees)
{
    float a = radians(angleDegrees);
    float s, c;
    sincos(a, s, c);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

// 距離場をアンチエイリアスして「インク量」に変換する。
//
// averageInk はそのパターンが 1 セルを平均どれだけ塗るか（＝そのトーンの濃度）。
// 網点が画面上で 1px 未満に潰れる状況（遠景・浅い角度・ワールド投影の圧縮）では、
// 無理に点を描こうとするとグレーのノイズになる。解像できなくなるにつれて
// 平均インク量へフェードさせることで、遠くでは滑らかな均一トーンに落ち着く。
// 写真製版でモアレを避けるのと同じ考え方。
//
// aaScale はエッジのなじませ幅だけを制御し、この解像判定には影響させない。
// aaScale=0（硬いエッジ）にしても崩れないようにするため。
/// 網点グリッド座標の、1ピクセルあたりの広がり（フットプリント）。
///
/// これを距離場から fwidth で取ってはいけない。距離場は frac() で 1 セルごとに
/// 折り返すため、セルの境目すべてで微分が跳ね上がり、そこだけ resolve が 0 に落ちて
/// 平均インクのべた塗りになる。スクリーン空間では細かすぎて均されるが、
/// UV / Object 空間では模様として見えてしまい、UV シームでは太い筋になる。
///
/// 座標そのものの微分は折り返しの影響を受けないので、セル境界で跳ねない。
float NataneHalftoneFootprint(float2 coord)
{
    float2 dx = ddx(coord);
    float2 dy = ddy(coord);
    return max(max(length(dx), length(dy)), 1e-6);
}

float NataneHalftoneCoverage(float distanceField, float radius, float averageInk,
                             float footprint, float aaScale)
{
    float w = footprint * max(aaScale, 0.0);
    float sharp = 1.0 - smoothstep(radius - w, radius + w, distanceField);

    // セル1つが微分値に対して十分大きいときだけ網点として描く。
    // 潰れるほど細かくなったら平均インクへ寄せる（モアレ防止）。
    float resolve = saturate(1.0 - footprint / max(radius, 1e-4));

    return lerp(averageInk, sharp, resolve);
}

// ドット網点。tone が濃いほど点が太る。
// 1セルあたりの平均インク量は円の面積そのもの。
float NataneHalftoneDot(float2 coord, float radius, float footprint, float aaScale)
{
    float2 cell = frac(coord) - 0.5;
    float averageInk = saturate(3.14159265 * radius * radius);
    return NataneHalftoneCoverage(length(cell), radius, averageInk, footprint, aaScale);
}

// 万線（平行線）。tone が濃いほど線が太る。
// 平均インク量は線幅の占有率。
float NataneHalftoneLine(float coordAxis, float halfWidth, float footprint, float aaScale)
{
    float d = abs(frac(coordAxis) - 0.5);
    float averageInk = saturate(halfWidth * 2.0);
    return NataneHalftoneCoverage(d, halfWidth, averageInk, footprint, aaScale);
}

/// 網点パターンを評価してインク量 [0,1] を返す。
///
/// coord    : 網点グリッド座標（スケールと回転を適用済み）
/// tone     : 影の濃さ [0,1]。0=塗らない、1=最も濃い
/// pattern  : 0 ドット / 1 万線 / 2 クロスハッチ
/// dotMin   : tone=0 付近での網点サイズ
/// dotMax   : tone=1 での網点サイズ
/// aaScale  : アンチエイリアス幅の倍率
float NataneHalftonePattern(float2 coord, float tone, float pattern,
                            float dotMin, float dotMax, float aaScale)
{
    float t = saturate(tone);

    // 線は幅がそのまま被覆率なので tone に線形でよい。
    float lineSize = lerp(dotMin, dotMax, t) * 0.5;

    // ドットは面積が半径の2乗なので、半径を tone に線形にすると
    // 被覆率が2乗で伸びる。号数を上げたとき、薄い側の差はほとんど見えないのに
    // 濃い側だけ急に潰れる、という段差になっていた原因がこれ。
    // 半径を sqrt(tone) にすると被覆率が tone にほぼ比例し、
    // 号数の刻みが均等な濃度差として見える（実際のトーンの号数と同じ感覚になる）。
    float dotSize = lerp(dotMin, dotMax, sqrt(t)) * 0.5;

    int mode = (int)(pattern + 0.5);

    // フットプリントは座標から一度だけ求める。分岐の中で ddx/ddy を呼ぶと
    // 分岐が分かれたクアッドで微分が壊れる。
    float footprint = NataneHalftoneFootprint(coord);

    if (mode == NATANE_HALFTONE_LINE)
    {
        return NataneHalftoneLine(coord.x, lineSize, footprint, aaScale);
    }

    if (mode == NATANE_HALFTONE_CROSSHATCH)
    {
        // 薄いうちは一方向だけ、濃くなると直交方向が重なる。
        // 実際の漫画のカケアミもこの順で密度を上げる。
        float first = NataneHalftoneLine(coord.x, lineSize, footprint, aaScale);
        float secondTone = saturate(t * 2.0 - 1.0);
        float secondSize = lerp(dotMin, dotMax, secondTone) * 0.5;
        float second = NataneHalftoneLine(coord.y, secondSize, footprint, aaScale) * step(0.001, secondTone);
        return max(first, second);
    }

    return NataneHalftoneDot(coord, dotSize, footprint, aaScale);
}

#endif // NATANE_TOON_HALFTONE_INCLUDED
