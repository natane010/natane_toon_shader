// URP 17 以降でのみコンパイルされる (NataneToon.Runtime.URP.asmdef の defineConstraints)。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NataneToon
{
    /// <summary>Natane Screen FX を URP で描く。GrabPass の代わりにカメラカラーを読んで全画面パスをかける。</summary>
    [Tooltip("Natane Screen FX (画面全体の撮影効果) を URP で描きます。Natane/Screen FX Overlay のマテリアルを割り当ててください。")]
    public sealed class NataneScreenFXRendererFeature : ScriptableRendererFeature
    {
        public enum InjectionPoint
        {
            /// <summary>ポストプロセスの前 (HDR のシーンカラーにかける)。</summary>
            BeforePostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
            /// <summary>ポストプロセスの後 (トーンマップ済みの最終的な色にかける)。</summary>
            AfterPostProcessing = RenderPassEvent.AfterRenderingPostProcessing,
        }

        [Tooltip("Natane/Screen FX Overlay シェーダーのマテリアル。効果の設定はこのマテリアルで行います。")]
        public Material material;

        [Tooltip("効果をかけるタイミング。After Post Processing はトーンマップ後の色に、Before Post Processing は HDR のシーンカラーにかけます。")]
        public InjectionPoint injectionPoint = InjectionPoint.AfterPostProcessing;

        [Tooltip("シーンビューにも効果をかけます。")]
        public bool applyToSceneView = true;

        static readonly int s_IntensityId = Shader.PropertyToID("_Intensity");

        ScreenFXPass m_Pass;

        public override void Create()
        {
            m_Pass = new ScreenFXPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || m_Pass == null) return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (cameraType == CameraType.SceneView && !applyToSceneView) return;

            // URP 用のパスを持たないシェーダーなら何もしない。
            int passIndex = material.FindPass(ScreenFXPass.ShaderPassName);
            if (passIndex < 0) return;

            // ブレンド 0 なら元の画面と同じなので、パスごと省く。
            if (material.HasProperty(s_IntensityId) && material.GetFloat(s_IntensityId) <= 0f) return;

            m_Pass.Setup(material, passIndex, (RenderPassEvent)injectionPoint);
            renderer.EnqueuePass(m_Pass);
        }

        sealed class ScreenFXPass : ScriptableRenderPass
        {
            public const string ShaderPassName = "NataneScreenFX";
            const string k_PassName = "Natane Screen FX";
            const string k_CopyBackPassName = "Natane Screen FX Copy Back";

            static readonly Vector4 s_ScaleBias = new Vector4(1f, 1f, 0f, 0f);

            Material m_Material;
            int m_PassIndex;

            sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public int passIndex;
            }

            public ScreenFXPass()
            {
                profilingSampler = new ProfilingSampler(k_PassName);
                // バックバッファは読めないので中間テクスチャを要求する。
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material, int passIndex, RenderPassEvent passEvent)
            {
                m_Material = material;
                m_PassIndex = passIndex;
                renderPassEvent = passEvent;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Material == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                if (!source.IsValid()) return;

                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_NataneScreenFXColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out PassData passData, profilingSampler))
                {
                    passData.source = source;
                    passData.material = m_Material;
                    passData.passIndex = m_PassIndex;

                    builder.UseTexture(source, AccessFlags.Read);
                    // 全ピクセルを書き直すので前の内容は読まない (タイルベース GPU でロードを省ける)。
                    builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, s_ScaleBias, data.material, data.passIndex);
                    });
                }

                // 単独のカメラなら、書き込んだテクスチャをカメラカラーに差し替えて書き戻しを省く。
                // カメラスタック中はカラーターゲットがカメラ間で持ち越されるので、差し替えずに書き戻す。
                bool canSwap = cameraData.renderType == CameraRenderType.Base && cameraData.resolveFinalTarget;
                if (canSwap)
                {
                    resources.cameraColor = destination;
                    return;
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_CopyBackPassName, out PassData passData, profilingSampler))
                {
                    passData.source = destination;
                    passData.material = null;
                    passData.passIndex = 0;

                    builder.UseTexture(destination, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, s_ScaleBias, 0f, false);
                    });
                }
            }
        }
    }
}
