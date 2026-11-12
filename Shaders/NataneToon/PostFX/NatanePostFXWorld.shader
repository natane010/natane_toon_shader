// NatanePostFXWorld.shader
//
// Natane Post FX の VRChat ワールド用 (Built-in RP、GrabPass)。レンズ → 画風 → 色の 3 段 (画面コピー 3 回)。
// 画面を覆うメッシュに貼って使う。メッシュとマテリアルは
// Tools > Natane > エフェクト Effects > ワールド用ポストエフェクトを作成 が Natane Post FX の設定から作る。
// 値の詰め方は NatanePostFXCore.hlsl のコメントのとおり。_Mat 付きは NatanePostFXWorld.hlsl が時間や解像度を足して使う。
// 3 つのワールド用シェーダー (Full / Lens + Color / Color) の Properties は同じ内容に保つこと。
Shader "Natane/PostFX World"
{
    Properties
    {
        [Header(World Gimmick)]
        _NPFX_Weight ("効き Weight", Range(0, 1)) = 1
        _NPFX_Range ("範囲 (m, 0 で無制限) Range", Float) = 0
        _NPFX_RangeFade ("範囲の端のなじませ (m) Range Fade", Float) = 1
        [ToggleUI] _NPFX_UseDepth ("深度を使う (フォグ・深度の輪郭線) Use Depth", Float) = 0
        [ToggleUI] _NPFX_ShowInMirror ("ミラーにも掛ける Show In Mirror", Float) = 0
        [ToggleUI] _NPFX_ShowInCamera ("VRChat カメラ・スクショにも掛ける Show In Camera", Float) = 1

        [Header(Animation)]
        _W_Speeds ("速さ: 波, グリッチ, 集中線, 走査線 Speeds", Vector) = (2, 12, 12, 0)
        _W_Shake ("画面揺れ: 量, 速さ Shake", Vector) = (0, 20, 0, 0)
        _W_Beat ("拍: BPM, ずれ (秒), 減衰 Beat", Vector) = (0, 0, 6, 0)
        _W_BeatAmount ("拍で動かす: ズーム, フラッシュ, 色収差, 揺れ Beat Amount", Vector) = (0, 0, 0, 0)

        [Header(Textures)]
        [NoScaleOffset] _NPFX_LutTex ("LUT (strip)", 2D) = "white" {}
        [NoScaleOffset] _NPFX_OverlayTex ("Overlay Texture", 2D) = "black" {}

        [HideInInspector] _L_Distort_Mat ("Lens: distortion, chromatic, rgb shift, rgb angle", Vector) = (0,0,0,0)
        [HideInInspector] _L_Xform_Mat ("Lens: zoom, rotation, offset xy", Vector) = (1,0,0,0)
        [HideInInspector] _L_RadialBlur ("Lens: radial blur, center xy, directional blur", Vector) = (0,0.5,0.5,0)
        [HideInInspector] _L_Mirror ("Lens: mirror, kaleidoscope, kaleido rotation, tile x", Vector) = (0,0,0,1)
        [HideInInspector] _L_Wave_Mat ("Lens: wave amp, wave freq, phase, heat haze", Vector) = (0,20,0,0)
        [HideInInspector] _L_Ripple ("Lens: ripple center xy, radius, strength", Vector) = (0.5,0.5,0.3,0)
        [HideInInspector] _L_Glitch ("Glitch: block, block count, line jitter, vertical jump", Vector) = (0,16,0,0)
        [HideInInspector] _L_Glitch2_Mat ("Glitch: color drift, -, vhs, crt curvature", Vector) = (0,0,0,0)
        [HideInInspector] _L_Misc_Mat ("Lens: dir blur angle, tile y, ripple width, pixelate", Vector) = (0,1,0.1,0)
        [HideInInspector] _S_Kuwahara ("Style: kuwahara, posterize, sharpen, retro bits", Vector) = (0,0,0,0)
        [HideInInspector] _S_Edge_Mat ("Style: edge, thickness, depth sens, color sens", Vector) = (0,1,0.5,0.5)
        [HideInInspector] _S_EdgeColor ("Style: ink color", Vector) = (0,0,0,1)
        [HideInInspector] _S_Paper ("Style: paper rgb, sketch", Vector) = (1,1,1,0)
        [HideInInspector] _S_Halftone_Mat ("Style: halftone, dot size, angle, crosshatch", Vector) = (0,6,0.785,0)
        [HideInInspector] _S_Extra ("Style: retro pixel size, color halftone, emboss", Vector) = (1,0,0,0)
        [HideInInspector] _F_Fog ("Fog: density, start, affect sky", Vector) = (0,0,0,0)
        [HideInInspector] _F_FogColor ("Fog color", Vector) = (0.7,0.8,0.9,1)
        [HideInInspector] _F_Grade ("Color: exposure mul, contrast, saturation, hue", Vector) = (1,1,1,0)
        [HideInInspector] _F_Grade2 ("Color: temperature, tint, vibrance, tonemap", Vector) = (0,0,0,0)
        [HideInInspector] _F_Lift ("Color: lift", Vector) = (0,0,0,1)
        [HideInInspector] _F_Gamma ("Color: gamma", Vector) = (1,1,1,1)
        [HideInInspector] _F_Gain ("Color: gain", Vector) = (1,1,1,1)
        [HideInInspector] _F_SplitShadow ("Color: shadow tint rgb, amount", Vector) = (0.5,0.5,0.5,0)
        [HideInInspector] _F_SplitHigh ("Color: highlight tint rgb, amount", Vector) = (0.5,0.5,0.5,0)
        [HideInInspector] _F_Lut ("Color: LUT contribution, LUT size", Vector) = (0,32,0,0)
        [HideInInspector] _F_Filter ("Filter: mono, sepia, invert, solarize", Vector) = (0,0,0,0)
        [HideInInspector] _F_Filter2 ("Filter: threshold, level, night vision, thermal", Vector) = (0,0.5,0,0)
        [HideInInspector] _F_Duo ("Gradient map amount", Vector) = (0,0,0,0)
        [HideInInspector] _F_DuoA ("Gradient map dark", Vector) = (0.1,0.05,0.3,1)
        [HideInInspector] _F_DuoB ("Gradient map light", Vector) = (1,0.85,0.6,1)
        [HideInInspector] _F_Overlay ("Overlay: color rgb, amount", Vector) = (1,1,1,0)
        [HideInInspector] _F_Overlay2 ("Overlay: blend, gradient, angle, light leak", Vector) = (1,0,0,0)
        [HideInInspector] _F_GradTop ("Overlay: gradient top", Vector) = (1,0.8,0.6,1)
        [HideInInspector] _F_GradBottom ("Overlay: gradient bottom", Vector) = (0.3,0.4,0.8,1)
        [HideInInspector] _F_LeakColor ("Overlay: light leak color", Vector) = (1,0.45,0.15,1)
        [HideInInspector] _F_OverlayTexParams ("Overlay texture: amount, blend, scroll xy", Vector) = (0,2,0,0)
        [HideInInspector] _F_Flash_Mat ("Flash: rgb, amount", Vector) = (1,1,1,0)
        [HideInInspector] _F_Fade ("Fade: rgb, amount", Vector) = (0,0,0,0)
        [HideInInspector] _F_Vignette ("Vignette: amount, smoothness, roundness", Vector) = (0,0.5,0,0)
        [HideInInspector] _F_VignetteColor ("Vignette color", Vector) = (0,0,0,1)
        [HideInInspector] _F_Lines_Mat ("Speed lines: amount, density, inner radius", Vector) = (0,120,0.3,0)
        [HideInInspector] _F_LinesColor ("Speed lines color", Vector) = (0,0,0,1)
        [HideInInspector] _F_Center ("Speed lines center", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _F_Crt_Mat ("Retro: scanlines, count, phase, rgb mask", Vector) = (0,360,0,0)
        [HideInInspector] _F_Film_Mat ("Retro: grain, grain size, scratches, flicker", Vector) = (0,1.5,0,0)
        [HideInInspector] _F_Film2 ("Retro: dust, letterbox aspect, -, static", Vector) = (0,0,0,0)
    }

    CGINCLUDE
    #pragma target 3.5
    #pragma multi_compile_instancing
    #include "NatanePostFXWorld.hlsl"
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Overlay+500" "RenderType" = "Overlay" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" "PreviewType" = "Plane" "VRCFallback" = "Hidden" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        GrabPass { }
        Pass
        {
            Name "LENS"
            CGPROGRAM
            #pragma vertex NPFXWorldVert
            #pragma fragment NPFXWorldFragLens
            #pragma shader_feature_local __ NPFX_L_WARP
            #pragma shader_feature_local __ NPFX_L_GLITCH
            #pragma shader_feature_local __ NPFX_L_FRINGE
            #pragma shader_feature_local __ NPFX_L_BLUR
            ENDCG
        }

        GrabPass { }
        Pass
        {
            Name "STYLE"
            CGPROGRAM
            #pragma vertex NPFXWorldVert
            #pragma fragment NPFXWorldFragStyle
            #pragma shader_feature_local __ NPFX_S_KUWAHARA
            #pragma shader_feature_local __ NPFX_S_EDGE
            #pragma shader_feature_local __ NPFX_S_PRINT
            ENDCG
        }

        GrabPass { }
        Pass
        {
            Name "FINAL"
            CGPROGRAM
            #pragma vertex NPFXWorldVert
            #pragma fragment NPFXWorldFragFinal
            #pragma shader_feature_local __ NPFX_F_FOG
            #pragma shader_feature_local __ NPFX_F_GRADE
            #pragma shader_feature_local __ NPFX_F_OVERLAY
            #pragma shader_feature_local __ NPFX_F_RETRO
            ENDCG
        }
    }
    Fallback Off
}
