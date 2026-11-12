// NatanePostFX.shader
//
// Natane Post FX の全パス。Built-in RP では NatanePostFX コンポーネント (OnRenderImage)、
// URP では Natane Post FX Renderer Feature が、このシェーダーのパスを番号で呼ぶ。
// パスの並びは Runtime/PostFX/NatanePostFXPlan.cs の定数と一致させること。
// 入力は MaterialPropertyBlock で渡すので、SubShader はパイプライン共通の 1 つだけ。
Shader "Hidden/Natane/PostFX"
{
    Properties
    {
        [HideInInspector] _NPFX_Source ("Source", 2D) = "black" {}
        [HideInInspector] _NPFX_Aux ("Aux", 2D) = "black" {}
        [HideInInspector] _NPFX_BloomTex ("Bloom", 2D) = "black" {}
        [HideInInspector] _NPFX_BlurTex ("Blur", 2D) = "black" {}
        [HideInInspector] _NPFX_DofTex ("Dof", 2D) = "black" {}
        [HideInInspector] _NPFX_StreakTex ("Streak", 2D) = "black" {}
        [HideInInspector] _NPFX_ShaftTex ("Shaft", 2D) = "black" {}
        [HideInInspector] _NPFX_MaskTex ("Mask", 2D) = "white" {}
        [HideInInspector] _NPFX_HistoryTex ("History", 2D) = "black" {}
        [HideInInspector] _NPFX_LutTex ("Lut", 2D) = "white" {}
        [HideInInspector] _NPFX_OverlayTex ("Overlay", 2D) = "black" {}
        [HideInInspector] _NPFX_DepthTex ("Depth", 2D) = "black" {}
    }

    CGINCLUDE
    #pragma target 3.5
    #include "NatanePostFXCore.hlsl"
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass // 0
        {
            Name "COPY"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragCopy
            ENDCG
        }
        Pass // 1
        {
            Name "LENS"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragLens
            #pragma multi_compile_local __ NPFX_L_WARP
            #pragma multi_compile_local __ NPFX_L_GLITCH
            #pragma multi_compile_local __ NPFX_L_FRINGE
            #pragma multi_compile_local __ NPFX_L_BLUR
            ENDCG
        }
        Pass // 2
        {
            Name "STYLE"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragStyle
            #pragma multi_compile_local __ NPFX_S_KUWAHARA
            #pragma multi_compile_local __ NPFX_S_EDGE
            #pragma multi_compile_local __ NPFX_S_PRINT
            ENDCG
        }
        Pass // 3
        {
            Name "PREFILTER"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragPrefilter
            ENDCG
        }
        Pass // 4
        {
            Name "DOWN"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragDown
            ENDCG
        }
        Pass // 5
        {
            Name "UP"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragUp
            ENDCG
        }
        Pass // 6
        {
            Name "BLUR"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragBlur
            ENDCG
        }
        Pass // 7
        {
            Name "RADIAL"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragRadial
            ENDCG
        }
        Pass // 8
        {
            Name "DOF"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragDof
            ENDCG
        }
        Pass // 9
        {
            Name "FINAL"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragFinal
            #pragma multi_compile_local __ NPFX_F_COMPOSITE
            #pragma multi_compile_local __ NPFX_F_MASK
            #pragma multi_compile_local __ NPFX_F_FOG
            #pragma multi_compile_local __ NPFX_F_GRADE
            #pragma multi_compile_local __ NPFX_F_OVERLAY
            #pragma multi_compile_local __ NPFX_F_RETRO
            ENDCG
        }
        Pass // 10
        {
            Name "TEMPORAL"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragTemporal
            ENDCG
        }
        Pass // 11
        {
            Name "DOFPREP"
            CGPROGRAM
            #pragma vertex NPFXVert
            #pragma fragment NPFXFragDofPrep
            ENDCG
        }
    }
    Fallback Off
}
