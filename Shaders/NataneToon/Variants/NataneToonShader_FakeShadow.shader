// NataneToonShader_FakeShadow.shader
//
// 前髪が顔に落とす影を、ライティングに依存せず板ポリ／複製メッシュで擬似的に描くための
// 極小シェーダー。VRChat では「2Dライクなルック」を作る定番手法として定着しており、
// lilToon には専用の FakeShadow シェーダーがある。本パッケージには相当機能が無く、
// lilToon から移行してきたユーザーは前髪の落ち影を失っていた。
//
// 設計方針:
//   - NataneToonCore.hlsl を include しない。146 機能を載せる意味が無いうえ、
//     依存を持たせると本体側の変更でこの極小シェーダーが巻き添えで壊れる。
//   - ライト非依存を既定にする。暗いワールドで落ち影だけが浮くのを避けるため。
//     必要なら _LightColorFollow でライト色へ追従させる。
//   - ステンシルの命名は本体（NataneToonShader.shader の Stencil ブロック）と揃える。
//     See Through Hair 系のセットアップで、本体と同じ Ref を素通しで共有できる。
Shader "Natane/Toon Shader FakeShadow"
{
    Properties
    {
        [Header(Shadow)]
        _ShadowColor ("Shadow Color (影色)", Color) = (0.55, 0.5, 0.6, 1)
        _ShadowAlpha ("Shadow Alpha (不透明度)", Range(0, 1)) = 0.5
        _ShadowTex ("Shadow Shape (影の形 A)", 2D) = "white" {}

        [Header(Light Response)]
        // 0 = 完全にライト非依存。1 = ライト色をそのまま乗せる。
        _LightColorFollow ("Light Color Follow (ライト色追従)", Range(0, 1)) = 0.3

        [Header(Fade)]
        // 正面から見たときに薄くする。真横から見たとき板ポリが目立つのを抑える用途もある。
        _FadeByViewAngle ("Fade By View Angle (正面で薄く)", Range(0, 1)) = 0

        [Header(Stencil)]
        _StencilRef ("Stencil Reference", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Pass Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail Operation", Float) = 0
        _StencilReadMask ("Read Mask", Range(0, 255)) = 255
        _StencilWriteMask ("Write Mask", Range(0, 255)) = 255

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OffsetFactor ("Depth Offset Factor", Range(-10, 10)) = 0
        _OffsetUnits ("Depth Offset Units", Range(-100, 100)) = 0
    }

    SubShader
    {
        Tags
        {
            // 顔マテリアル（AlphaTest 相当）の直後に描く。
            "Queue" = "AlphaTest+50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            // セーフティでブロックされたときに Toon 系へ落ちるようにする。
            "VRCFallback" = "ToonTransparent"
        }

        Pass
        {
            Name "FAKE_SHADOW"
            Tags { "LightMode" = "ForwardBase" }

            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilOp]
                Fail [_StencilFail]
                ZFail [_StencilZFail]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            Blend SrcAlpha OneMinusSrcAlpha
            // ZWrite Off: 他の半透明の並び順を壊さない。落ち影は「上に薄く乗せる」だけ。
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _ShadowTex;
            float4 _ShadowTex_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _ShadowAlpha;
                half _LightColorFollow;
                half _FadeByViewAngle;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _ShadowTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 shape = tex2D(_ShadowTex, i.uv);

                half3 color = _ShadowColor.rgb;

                // ライト色への追従。既定は弱め。完全追従にすると、暗いワールドで
                // 落ち影が消え、明るいワールドで白飛びして「影に見えない」ため。
                half3 lightColor = _LightColor0.rgb + max(unity_AmbientSky.rgb, 0.0h);
                color = lerp(color, color * lightColor, _LightColorFollow);

                half alpha = _ShadowColor.a * shape.a * _ShadowAlpha;

                // 視線と面の角度でフェード。板ポリを真横から見たときの「紙が立っている」
                // 見え方を消す用途にも使う。
                if (_FadeByViewAngle > 0.001h)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                    half facing = saturate(abs(dot(normalize(i.worldNormal), viewDir)));
                    alpha *= lerp(1.0h, facing, _FadeByViewAngle);
                }

                fixed4 result = fixed4(color, alpha);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    // 本体の NataneToonShaderGUI は 990 プロパティ前提の巨大な GUI なので使わない。
    // このシェーダー専用の小さなインスペクタを当てる。
    CustomEditor "NataneToon.Editor.NataneFakeShadowShaderGUI"
    Fallback "Unlit/Transparent"
}
