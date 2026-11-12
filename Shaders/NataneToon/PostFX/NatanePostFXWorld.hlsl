// NatanePostFXWorld.hlsl
//
// Natane Post FX を VRChat のワールドギミックとして使うための入口。
// ワールドでは C# が動かないので、画面を覆うメッシュ + GrabPass で NatanePostFXCore.hlsl の
// LENS / STYLE / FINAL を実行する。値はマテリアルに持ち、Animator や Udon から動かす。
// 中間テクスチャが要る効果 (ブルーム、被写界深度、ぼかし、キャラクターマスク、残像) は使えない。
#ifndef NATANE_POSTFX_WORLD_INCLUDED
#define NATANE_POSTFX_WORLD_INCLUDED

#define NPFX_WORLD
#include "NatanePostFXCore.hlsl"
#include "../Include/Utils/NataneToonCamera.hlsl"

float _NPFX_Weight;        // 全体の効き (0 で描かない)
float _NPFX_Range;         // このオブジェクトからの距離 (m)。0 なら距離に関係なく掛かる
float _NPFX_RangeFade;     // 範囲の端でなじませる幅 (m)
float _NPFX_UseDepth;      // _CameraDepthTexture を使う (フォグ、深度の輪郭線)
float _NPFX_ShowInMirror;
float _NPFX_ShowInCamera;

float4 _W_Speeds;      // x 波の速さ, y グリッチの変化 (回 / 秒), z 集中線の動き (回 / 秒), w 走査線の流れる速さ
float4 _W_Shake;       // x 画面揺れ, y 揺れの速さ
float4 _W_Beat;        // x BPM (0 でオフ), y 拍の頭のずれ (秒), z 減衰の速さ
float4 _W_BeatAmount;  // x 拍でズーム, y 拍でフラッシュ, z 拍で色収差, w 拍で画面揺れ

// NatanePostFX.ApplyUniforms が毎フレーム C# で計算している値を、シェーダーの中で作る。
void NPFXWorldSetup()
{
    float t = _Time.y;
    float pixelScale = _ScreenParams.y / 1080.0;
    float pixelScaleMin = max(pixelScale, 0.5);

    float pulse = 0.0;
    if (_W_Beat.x > 0.01)
    {
        float secondsPerBeat = 60.0 / _W_Beat.x;
        float since = (t - _W_Beat.y) - floor((t - _W_Beat.y) / secondsPerBeat) * secondsPerBeat;
        pulse = exp(-since * _W_Beat.z);
    }

    float shakeAmount = _W_Shake.x + _W_BeatAmount.w * pulse;
    float2 shakeOffset = float2(0, 0);
    float shakeRot = 0.0;
    if (shakeAmount > 1e-4)
    {
        float st = t * _W_Shake.y;
        shakeOffset = (float2(NPFXNoise(float2(st, 0.3)), NPFXNoise(float2(0.7, st))) - 0.5) * (shakeAmount * 0.1);
        shakeRot = (NPFXNoise(float2(st, st * 0.5 + 5.0)) - 0.5) * shakeAmount * 0.1;
    }

    _L_Distort = _L_Distort_Mat;
    _L_Distort.y += _W_BeatAmount.z * pulse;
    _L_Xform = _L_Xform_Mat;
    _L_Xform.x = max(_L_Xform.x, 0.01) * (1.0 + _W_BeatAmount.x * pulse * 0.15);
    _L_Xform.y += shakeRot;
    _L_Xform.zw += shakeOffset;
    _L_Wave = _L_Wave_Mat;
    _L_Wave.z += t * _W_Speeds.x;
    _L_Glitch2 = _L_Glitch2_Mat;
    _L_Glitch2.y = fmod(floor(t * _W_Speeds.y), 1024.0);
    _L_Misc = _L_Misc_Mat;
    _L_Misc.w *= pixelScale;
    _S_Edge = _S_Edge_Mat;
    _S_Edge.y *= pixelScaleMin;
    _S_Halftone = _S_Halftone_Mat;
    _S_Halftone.y *= pixelScaleMin;
    _F_Flash = _F_Flash_Mat;
    _F_Flash.a = saturate(_F_Flash.a + _W_BeatAmount.y * pulse);
    _F_Lines = _F_Lines_Mat;
    _F_Lines.w = _W_Speeds.z > 0.01 ? fmod(floor(t * _W_Speeds.z), 256.0) : 0.0;
    _F_Crt = _F_Crt_Mat;
    _F_Crt.z += t * _W_Speeds.w;
    _F_Film = _F_Film_Mat;
    _F_Film.y *= pixelScaleMin;

    // ミラーのカメラには正しい深度テクスチャが無い。
    _NPFX_DepthMode = (_NPFX_UseDepth > 0.5 && _VRChatMirrorMode < 0.5) ? 1.0 : 0.0;
}

struct NPFXWorldAttributes
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// メッシュの UV (0-1) をそのまま画面いっぱいに広げる。メッシュの形や位置は描画に関係しない
// (カリングされないよう、バウンディングボックスだけ大きくしておく)。
NPFXVaryings NPFXWorldVert(NPFXWorldAttributes v)
{
    NPFXVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(NPFXVaryings, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float weight = saturate(_NPFX_Weight);
    if (_NPFX_Range > 0.0001)
    {
        float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
        float dist = NataneViewerDistance(origin);
        weight *= saturate((_NPFX_Range - dist) / max(_NPFX_RangeFade, 0.001));
    }
    if (_VRChatMirrorMode > 0.5 && _NPFX_ShowInMirror < 0.5) weight = 0.0;
    if (_VRChatCameraMode > 0.5 && _NPFX_ShowInCamera < 0.5) weight = 0.0;

    o.pos = float4(v.uv * 2.0 - 1.0, 0.5, 1.0);
    float4 grab = ComputeGrabScreenPos(o.pos);
    o.uv = grab.xy / grab.w;
    o.weight = weight;
    // 効きが 0 のときは画面の外へ出して、ピクセルを 1 つも描かない。
    if (weight < 0.0005) o.pos = float4(2.0, 2.0, 2.0, 1.0);
    return o;
}

#define NPFX_WORLD_FRAG(name, inner) \
float4 name(NPFXVaryings i) : SV_Target \
{ \
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); \
    NPFXWorldSetup(); \
    float3 src = NPFXSample(i.uv); \
    float3 fx = inner(i).rgb; \
    return float4(lerp(src, fx, i.weight), 1.0); \
}

NPFX_WORLD_FRAG(NPFXWorldFragLens, NPFXFragLens)
NPFX_WORLD_FRAG(NPFXWorldFragStyle, NPFXFragStyle)
NPFX_WORLD_FRAG(NPFXWorldFragFinal, NPFXFragFinal)

#endif // NATANE_POSTFX_WORLD_INCLUDED
