// ステッカー風の縁取り (Sticker variant 専用)。
// シルエットの外側だけに均一な幅の縁を描く。ジオメトリなので MSAA が効き、ポストプロセスは不要。
// NATANE_STICKER_BACK を定義すると、縁の外側に敷く「台紙」(外枠の線 / 落ち影) になる。
#ifndef NATANE_TOON_STICKER_PASS_INCLUDED
#define NATANE_TOON_STICKER_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "../Utils/NataneToonCamera.hlsl"

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    UNITY_FOG_COORDS(0)
    UNITY_VERTEX_OUTPUT_STEREO
};

float _Sticker;
float _StickerWidth;
float4 _StickerColor;
float _StickerScreenSpace;
float _StickerDepthOffset;
float _StickerLightInfluence;
float _StickerBack;
float _StickerBackWidth;
float4 _StickerBackColor;
float4 _StickerBackOffset;
float _OutlineCornerSmooth;
#ifdef _SMOOTH_NORMAL
    float _SmoothNormalMode;
    sampler2D _SmoothNormalTex;
#endif
#ifdef _MIRROR_CONTROL
    float _MirrorControl;
    float _MirrorMode;
    float _CameraMode;
#endif

// 押し出し方向。アウトラインと同じスムース法線の設定を使う。
float3 NataneStickerNormal(appdata v)
{
    float3 n = v.normal;
    #ifdef _SMOOTH_NORMAL
        if (_SmoothNormalMode < 0.5)
        {
            n = v.color.rgb * 2.0 - 1.0;
        }
        else
        {
            float3 ts = _SmoothNormalMode < 1.5
                ? v.color.rgb * 2.0 - 1.0
                : tex2Dlod(_SmoothNormalTex, float4(v.uv, 0, 0)).rgb * 2.0 - 1.0;
            float3 binormal = cross(v.normal, v.tangent.xyz) * v.tangent.w;
            n = mul(ts, float3x3(v.tangent.xyz, binormal, v.normal));
        }
    #else
        if (_OutlineCornerSmooth > 0.001)
            n = lerp(v.normal, normalize(v.vertex.xyz), _OutlineCornerSmooth);
    #endif
    return normalize(n);
}

v2f vert(appdata v)
{
    v2f o;
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    #ifdef NATANE_STICKER_BACK
        bool enabled = _Sticker > 0.5 && _StickerBack > 0.5;
    #else
        bool enabled = _Sticker > 0.5;
    #endif
    if (!enabled)
    {
        // 縮退させてラスタライズを止める。
        o.pos = float4(0, 0, 0, 0);
        return o;
    }

    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;
    float2 dir = mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(NataneStickerNormal(v))).xy;

    // 画面上の向きだけを使い、長さは一定にする (どの角度でも同じ幅の縁になる)。
    // カメラを向いた法線は向きが不安定だが、そこは本体に隠れる。
    float dirLen = length(dir);
    dir = dir / max(dirLen, 1e-5) * saturate(dirLen * 16.0);

    // 幅は 1m 先での cm。ScreenSpace = 1 で距離によらず画面上の太さが一定になる。
    float scale = 0.01 * lerp(1.0, NataneViewerDistance(worldPos), _StickerScreenSpace);
    float width = _StickerWidth;
    float depthOffset = _StickerDepthOffset;
    #ifdef NATANE_STICKER_BACK
        width += _StickerBackWidth;
        viewPos.xy += _StickerBackOffset.xy * scale;
        depthOffset += 0.02;
    #endif
    viewPos.xy += dir * width * scale;

    // フチ全体を「オブジェクト原点の奥行き + オフセット」の面まで下げる (それより奥の頂点はその場)。
    // 手前に出した腕のフチも胴体の後ろに回るので、別マテリアルの部位が後から描かれても上に残らない。
    // 視線に沿って動かすので画面上の位置は変わらず、ミラーの斜め投影でも深度が壊れない。
    float pivotDepth = -mul(UNITY_MATRIX_V, float4(unity_ObjectToWorld._m03_m13_m23, 1.0)).z;
    float surfaceDepth = max(-viewPos.z, 0.01);
    float targetDepth = max(surfaceDepth, pivotDepth) + depthOffset;
    if (NataneIsOrthoCamera())
        viewPos.z = -targetDepth;
    else
        viewPos *= targetDepth / surfaceDepth;

    o.pos = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));

    #ifdef NATANE_STICKER_BACK
        fixed4 col = _StickerBackColor;
    #else
        fixed4 col = _StickerColor;
    #endif
    // 暗いワールドで縁だけ光らないよう、環境光 + メインライトの明るさを掛ける。
    float3 light = saturate(_LightColor0.rgb + ShadeSH9(float4(0, 0, 0, 1)));
    col.rgb *= lerp(float3(1, 1, 1), light, _StickerLightInfluence);
    o.color = col;

    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    #ifdef _MIRROR_CONTROL
    if (_MirrorControl >= 0.5)
    {
        bool inMirror = _VRChatMirrorMode > 0.5;
        bool inCamera = _VRChatCameraMode > 0.5;
        if (_MirrorMode > 0.5 && _MirrorMode < 1.5 && !inMirror) discard;
        if (_MirrorMode > 1.5 && inMirror) discard;
        if (_CameraMode > 0.5 && _CameraMode < 1.5 && !inCamera) discard;
        if (_CameraMode > 1.5 && inCamera) discard;
    }
    #endif
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    fixed4 col = i.color;
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif // NATANE_TOON_STICKER_PASS_INCLUDED
