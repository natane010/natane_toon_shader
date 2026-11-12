// URP 17 以降でのみコンパイルされる (NataneToon.Runtime.URP.asmdef の defineConstraints)。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NataneToon
{
    /// <summary>
    /// Natane Post FX を URP (RenderGraph) で描く。設定はカメラに付けた NatanePostFX コンポーネントが持ち、
    /// この Feature は同じ描画手順 (NatanePostFXPlan) を RenderGraph のパスに置き換えて実行するだけ。
    /// </summary>
    [Tooltip("Natane Post FX を URP で描きます。効果の設定はカメラに付けた Natane Post FX コンポーネントで行います。")]
    public sealed class NatanePostFXRendererFeature : ScriptableRendererFeature
    {
        public enum InjectionPoint
        {
            /// <summary>URP のポストプロセスの前 (HDR のシーンカラーにかける)。</summary>
            BeforePostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
            /// <summary>URP のポストプロセスの後 (トーンマップ済みの色にかける)。</summary>
            AfterPostProcessing = RenderPassEvent.AfterRenderingPostProcessing,
        }

        [Tooltip("効果をかけるタイミング。After Post Processing は URP のポストプロセス (Volume) の後、Before Post Processing は前です。")]
        public InjectionPoint injectionPoint = InjectionPoint.AfterPostProcessing;

        PostFXPass m_Pass;

        public override void Create()
        {
            m_Pass = new PostFXPass();
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            m_Pass = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Pass == null) return;
            Camera camera = renderingData.cameraData.camera;
            if (camera == null) return;
            CameraType cameraType = camera.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (!camera.TryGetComponent(out NatanePostFX fx) || !fx.isActiveAndEnabled) return;

            m_Pass.Setup(fx, (RenderPassEvent)injectionPoint);
            renderer.EnqueuePass(m_Pass);
        }

        sealed class PostFXPass : ScriptableRenderPass
        {
            const string k_PassName = "Natane Post FX";

            static readonly MaterialPropertyBlock s_Block = new MaterialPropertyBlock();

            NatanePostFX m_FX;
            readonly List<TextureHandle> m_Slots = new List<TextureHandle>();
            // 残像用の履歴テクスチャ (コンポーネントが持つ RenderTexture) を RenderGraph に渡すための包み。
            readonly Dictionary<RenderTexture, RTHandle> m_HistoryHandles = new Dictionary<RenderTexture, RTHandle>();
            readonly List<RenderTexture> m_Dead = new List<RenderTexture>();

            sealed class StepData
            {
                public Material material;
                public int pass;
                public int count;
                public readonly int[] ids = new int[NatanePostFXPlan.MaxBindings + 1];
                public readonly TextureHandle[] textures = new TextureHandle[NatanePostFXPlan.MaxBindings + 1];
                public Vector4 sourceSize;
                public Vector4 stepParams;
            }

            sealed class MaskData
            {
                public NatanePostFX fx;
            }

            public PostFXPass()
            {
                profilingSampler = new ProfilingSampler(k_PassName);
                // バックバッファは読めないので中間テクスチャを要求する。
                requiresIntermediateTexture = true;
            }

            public void Setup(NatanePostFX fx, RenderPassEvent passEvent)
            {
                m_FX = fx;
                renderPassEvent = passEvent;
                ConfigureInput(fx.NeedsDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
            }

            public void Dispose()
            {
                foreach (KeyValuePair<RenderTexture, RTHandle> pair in m_HistoryHandles) pair.Value?.Release();
                m_HistoryHandles.Clear();
            }

            RTHandle GetHistoryHandle(RenderTexture history)
            {
                // 作り直されて破棄されたテクスチャの包みを捨てる。
                m_Dead.Clear();
                foreach (KeyValuePair<RenderTexture, RTHandle> pair in m_HistoryHandles)
                    if (pair.Key == null) m_Dead.Add(pair.Key);
                if (m_Dead.Count > 0)
                {
                    // 破棄済みのキーは辞書から引けないので、生きているものだけ残して作り直す。
                    var alive = new List<KeyValuePair<RenderTexture, RTHandle>>();
                    foreach (KeyValuePair<RenderTexture, RTHandle> pair in m_HistoryHandles)
                    {
                        if (pair.Key != null) alive.Add(pair);
                        else pair.Value?.Release();
                    }
                    m_HistoryHandles.Clear();
                    foreach (KeyValuePair<RenderTexture, RTHandle> pair in alive) m_HistoryHandles[pair.Key] = pair.Value;
                }

                if (!m_HistoryHandles.TryGetValue(history, out RTHandle handle) || handle == null)
                {
                    handle = RTHandles.Alloc(history);
                    m_HistoryHandles[history] = handle;
                }
                return handle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                NatanePostFX fx = m_FX;
                if (fx == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                if (!source.IsValid()) return;

                int width = cameraData.cameraTargetDescriptor.width;
                int height = cameraData.cameraTargetDescriptor.height;

                TextureHandle depth = resources.cameraDepthTexture;
                bool hasDepth = fx.NeedsDepth && depth.IsValid();

                if (!fx.PrepareFrame(cameraData.camera, width, height, hasDepth ? 2 : 0, out RenderTexture history)) return;
                NatanePostFXPlan plan = fx.Plan;

                // スロットごとのテクスチャ
                m_Slots.Clear();
                m_Slots.Add(source);
                for (int i = 1; i < plan.slotCount; i++)
                {
                    int div = plan.GetDivisor(i);
                    var desc = new TextureDesc(NatanePostFXPlan.Scaled(width, div), NatanePostFXPlan.Scaled(height, div))
                    {
                        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                        depthBufferBits = DepthBits.None,
                        msaaSamples = MSAASamples.None,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        clearBuffer = false,
                        name = "_NatanePostFX" + i,
                    };
                    m_Slots.Add(renderGraph.CreateTexture(desc));
                }

                TextureHandle historyHandle = TextureHandle.nullHandle;
                if (plan.needsHistory && history != null)
                    historyHandle = renderGraph.ImportTexture(GetHistoryHandle(history));

                TextureHandle maskHandle = TextureHandle.nullHandle;
                if (plan.needsMask)
                {
                    var desc = new TextureDesc(width, height)
                    {
                        colorFormat = GraphicsFormat.R32_SFloat,
                        depthBufferBits = DepthBits.None,
                        msaaSamples = MSAASamples.None,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                        clearBuffer = true,
                        clearColor = NatanePostFX.MaskClearColor,
                        name = "_NatanePostFXMask",
                    };
                    maskHandle = renderGraph.CreateTexture(desc);
                    using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Natane Post FX Mask", out MaskData data, profilingSampler))
                    {
                        data.fx = fx;
                        builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
                        builder.SetRenderFunc(static (MaskData d, RasterGraphContext context) =>
                        {
                            Material material = d.fx != null ? d.fx.MaskMaterial : null;
                            if (material == null) return;
                            IReadOnlyList<Renderer> renderers = d.fx.MaskRenderers;
                            for (int i = 0; i < renderers.Count; i++)
                            {
                                Renderer r = renderers[i];
                                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                                int subMeshes = Mathf.Max(1, r.sharedMaterials.Length);
                                for (int s = 0; s < subMeshes; s++) context.cmd.DrawRenderer(r, material, s, 0);
                            }
                        });
                    }
                }

                for (int i = 0; i < plan.stepCount; i++)
                {
                    NatanePostFXPlan.Step step = plan.GetStep(i);
                    TextureHandle dst = Resolve(step.dst, historyHandle, maskHandle);
                    if (!dst.IsValid()) continue;

                    using (var builder = renderGraph.AddRasterRenderPass<StepData>(k_PassName, out StepData data, profilingSampler))
                    {
                        data.material = fx.Material;
                        data.pass = step.pass;
                        data.count = 0;
                        for (int b = 0; b < step.bindingCount; b++)
                        {
                            TextureHandle tex = Resolve(step.slots[b], historyHandle, maskHandle);
                            if (!tex.IsValid()) continue;
                            data.ids[data.count] = step.ids[b];
                            data.textures[data.count] = tex;
                            data.count++;
                            builder.UseTexture(tex, AccessFlags.Read);
                        }
                        if (hasDepth && (step.pass == NatanePostFXPass.Style || step.pass == NatanePostFXPass.DofPrep || step.pass == NatanePostFXPass.Final))
                        {
                            data.ids[data.count] = NatanePostFXIds.DepthTex;
                            data.textures[data.count] = depth;
                            data.count++;
                            builder.UseTexture(depth, AccessFlags.Read);
                        }

                        int srcDiv = plan.GetDivisor(step.sourceSlot);
                        int sw = NatanePostFXPlan.Scaled(width, srcDiv), sh = NatanePostFXPlan.Scaled(height, srcDiv);
                        data.sourceSize = new Vector4(1f / sw, 1f / sh, sw, sh);
                        data.stepParams = step.stepParams;

                        // 全ピクセルを書き直すので前の内容は読まない。
                        builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);
                        builder.SetRenderFunc(static (StepData d, RasterGraphContext context) => Draw(d, context.cmd));
                    }
                }

                // 結果をカメラカラーへ書き戻す (カメラスタックでもそのまま次のカメラへ渡る)。
                TextureHandle result = Resolve(plan.resultSlot, historyHandle, maskHandle);
                if (plan.resultSlot == NatanePostFXPlan.SlotSource || !result.IsValid()) return;
                using (var builder = renderGraph.AddRasterRenderPass<StepData>("Natane Post FX Copy Back", out StepData data, profilingSampler))
                {
                    data.material = fx.Material;
                    data.pass = NatanePostFXPass.Copy;
                    data.count = 1;
                    data.ids[0] = NatanePostFXIds.Source;
                    data.textures[0] = result;
                    data.sourceSize = new Vector4(1f / width, 1f / height, width, height);
                    data.stepParams = Vector4.zero;
                    builder.UseTexture(result, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (StepData d, RasterGraphContext context) => Draw(d, context.cmd));
                }
            }

            TextureHandle Resolve(int slot, TextureHandle history, TextureHandle mask)
            {
                if (slot == NatanePostFXPlan.SlotHistory) return history;
                if (slot == NatanePostFXPlan.SlotMask) return mask;
                return m_Slots[slot];
            }

            static void Draw(StepData d, RasterCommandBuffer cmd)
            {
                if (d.material == null) return;
                s_Block.Clear();
                for (int i = 0; i < d.count; i++)
                {
                    RTHandle handle = d.textures[i];
                    if (handle == null) continue;
                    Texture texture = handle;
                    if (texture != null) s_Block.SetTexture(d.ids[i], texture);
                }
                s_Block.SetVector(NatanePostFXIds.SourceSize, d.sourceSize);
                s_Block.SetVector(NatanePostFXIds.StepParams, d.stepParams);
                cmd.DrawProcedural(Matrix4x4.identity, d.material, d.pass, MeshTopology.Triangles, 3, 1, s_Block);
            }
        }
    }
}
