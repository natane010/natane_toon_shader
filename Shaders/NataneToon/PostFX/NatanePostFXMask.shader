// NatanePostFXMask.shader
//
// Natane Post FX の「キャラクターマスク」用。指定したレンダラーをこのシェーダーで描き、
// カメラからの距離を書き込む。仕上げパスがシーンの深度と比べて、隠れていない所だけをマスクにする。
// BlendOp Min で手前の距離を残すので、深度バッファは要らない。
Shader "Hidden/Natane/PostFXMask"
{
    SubShader
    {
        Pass
        {
            Name "MASK"
            Cull Off
            ZWrite Off
            ZTest Always
            BlendOp Min
            Blend One One

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // 画面の上下の向きを中間テクスチャに合わせるため、行列は C# から渡す。
            float4x4 _NPFX_MaskVP;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float eyeDepth : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(vertex.xyz, 1.0));
                o.pos = mul(_NPFX_MaskVP, world);
                o.eyeDepth = o.pos.w;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(i.eyeDepth, 0, 0, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
