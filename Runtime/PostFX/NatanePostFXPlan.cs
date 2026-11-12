using System.Collections.Generic;
using UnityEngine;

namespace NataneToon
{
    /// <summary>Hidden/Natane/PostFX のパス番号。シェーダーのパスの並びと一致させること。</summary>
    public static class NatanePostFXPass
    {
        public const int Copy = 0;
        public const int Lens = 1;
        public const int Style = 2;
        public const int Prefilter = 3;
        public const int Down = 4;
        public const int Up = 5;
        public const int Blur = 6;
        public const int Radial = 7;
        public const int Dof = 8;
        public const int Final = 9;
        public const int Temporal = 10;
    }

    /// <summary>
    /// 1 フレーム分の描画手順。パイプラインに依存しない「スロット番号」でテクスチャを指し、
    /// Built-in RP 側 (NatanePostFX.OnRenderImage) と URP 側 (Renderer Feature) が同じ手順を実行する。
    /// </summary>
    public sealed class NatanePostFXPlan
    {
        /// <summary>カメラの画像 (入力)。</summary>
        public const int SlotSource = 0;
        /// <summary>前のフレームの結果 (残像用、フレームをまたいで残る)。</summary>
        public const int SlotHistory = -2;
        /// <summary>キャラクターマスク。</summary>
        public const int SlotMask = -3;

        public const int MaxBindings = 8;

        public sealed class Step
        {
            public int pass;
            public int dst;
            public readonly int[] ids = new int[MaxBindings];
            public readonly int[] slots = new int[MaxBindings];
            public int bindingCount;
            public Vector4 stepParams;
            /// <summary>_NPFX_Source に入れたスロット (サイズの計算用)。</summary>
            public int sourceSlot;

            public Step Bind(int id, int slot)
            {
                ids[bindingCount] = id;
                slots[bindingCount] = slot;
                bindingCount++;
                if (id == NatanePostFXIds.Source) sourceSlot = slot;
                return this;
            }
        }

        readonly List<Step> m_Pool = new List<Step>();
        readonly List<int> m_SlotDivisor = new List<int>();

        public int stepCount { get; private set; }
        /// <summary>最終結果が入っているスロット。</summary>
        public int resultSlot { get; private set; }
        /// <summary>マスクを描く必要があるか。</summary>
        public bool needsMask { get; private set; }
        /// <summary>履歴テクスチャ (残像) を使うか。</summary>
        public bool needsHistory { get; private set; }
        /// <summary>一時テクスチャの数 (スロット 1 から slotCount - 1)。</summary>
        public int slotCount => m_SlotDivisor.Count;

        public Step GetStep(int index) => m_Pool[index];
        /// <summary>スロットの解像度がカメラの何分の 1 か。</summary>
        public int GetDivisor(int slot) => slot > 0 && slot < m_SlotDivisor.Count ? m_SlotDivisor[slot] : 1;

        public static int Scaled(int size, int divisor) => Mathf.Max(1, size / Mathf.Max(1, divisor));

        int NewSlot(int divisor)
        {
            m_SlotDivisor.Add(divisor);
            return m_SlotDivisor.Count - 1;
        }

        Step Add(int pass, int dst, int source)
        {
            if (stepCount == m_Pool.Count) m_Pool.Add(new Step());
            Step s = m_Pool[stepCount++];
            s.pass = pass;
            s.dst = dst;
            s.bindingCount = 0;
            s.stepParams = Vector4.zero;
            s.sourceSlot = source;
            s.Bind(NatanePostFXIds.Source, source);
            return s;
        }

        /// <summary>設定から手順を組み立てる。マテリアルの値は NatanePostFX.ApplyUniforms が別に設定する。</summary>
        public void Build(NatanePostFX fx, int width, int height, bool maskAvailable, bool historyValid, bool holdFrame)
        {
            stepCount = 0;
            m_SlotDivisor.Clear();
            m_SlotDivisor.Add(1); // スロット 0 = 入力
            needsMask = maskAvailable && fx.mask.HasEffect;
            needsHistory = false;

            int cur = SlotSource;

            if (fx.lens.Active || fx.glitch.Active || fx.beat.AffectsLens)
            {
                int t = NewSlot(1);
                Add(NatanePostFXPass.Lens, t, cur);
                cur = t;
            }

            if (fx.style.Active)
            {
                int t = NewSlot(1);
                Add(NatanePostFXPass.Style, t, cur);
                cur = t;
            }

            int bloomSlot = -1, streakSlot = -1, shaftSlot = -1, blurSlot = -1, dofSlot = -1;
            NPFXLight light = fx.glow;
            bool bloomOn = light.bloom > 1e-4f || fx.beat.AffectsBloom;

            if (light.NeedsPrefilter || bloomOn)
            {
                int pf = NewSlot(2);
                Add(NatanePostFXPass.Prefilter, pf, cur);

                if (bloomOn)
                {
                    int maxLevels = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Min(width, height) / 2f, 2f)) - 2, 1, 6);
                    int levels = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, maxLevels, light.scatter)), 1, maxLevels);
                    m_Down[0] = pf;
                    for (int i = 1; i < levels; i++)
                    {
                        m_Down[i] = NewSlot(2 << i);
                        Add(NatanePostFXPass.Down, m_Down[i], m_Down[i - 1]);
                    }
                    int up = m_Down[levels - 1];
                    for (int i = levels - 2; i >= 0; i--)
                    {
                        int t = NewSlot(2 << i);
                        Step s = Add(NatanePostFXPass.Up, t, up).Bind(NatanePostFXIds.Aux, m_Down[i]);
                        s.stepParams = new Vector4(Mathf.Lerp(0.5f, 2f, light.scatter), 0f, 0f, 0f);
                        up = t;
                    }
                    bloomSlot = up;
                    bloomLevels = levels;
                }

                if (light.streak > 1e-4f)
                {
                    int a = NewSlot(4), b = NewSlot(4);
                    Add(NatanePostFXPass.Down, a, pf);
                    float len = Mathf.Lerp(0.5f, 3f, light.streakLength);
                    Add(NatanePostFXPass.Blur, b, a).stepParams = new Vector4(len, 0f, 1f, 0f);
                    Add(NatanePostFXPass.Blur, a, b).stepParams = new Vector4(len * 3f, 0f, 1f, 0f);
                    Add(NatanePostFXPass.Blur, b, a).stepParams = new Vector4(len * 9f, 0f, 1f, 0f);
                    streakSlot = b;
                }

                if (light.shafts > 1e-4f)
                {
                    int a = NewSlot(4), b = NewSlot(4), c = NewSlot(4);
                    Add(NatanePostFXPass.Down, a, pf);
                    Vector2 center = fx.ShaftCenterUV;
                    float len = Mathf.Lerp(0.2f, 1f, light.shaftLength);
                    Add(NatanePostFXPass.Radial, b, a).stepParams = new Vector4(center.x, center.y, len, 0.93f);
                    Add(NatanePostFXPass.Radial, c, b).stepParams = new Vector4(center.x, center.y, len * 0.25f, 0.97f);
                    shaftSlot = c;
                }
            }

            bool maskBlur = needsMask && fx.mask.backgroundBlur > 1e-4f;
            if (light.diffusion > 1e-4f || fx.focus.blur > 1e-4f || maskBlur)
            {
                float radius = maskBlur ? 0.5f : 0f;
                if (light.diffusion > 1e-4f) radius = Mathf.Max(radius, light.diffusionRadius);
                if (fx.focus.blur > 1e-4f) radius = Mathf.Max(radius, fx.focus.blurRadius);
                float r = Mathf.Lerp(0.5f, 3f, radius);

                int h = NewSlot(2), q = NewSlot(4), b1 = NewSlot(4), b2 = NewSlot(4);
                Add(NatanePostFXPass.Down, h, cur);
                Add(NatanePostFXPass.Down, q, h);
                Add(NatanePostFXPass.Blur, b1, q).stepParams = new Vector4(r, 0f, 1f, 0f);
                Add(NatanePostFXPass.Blur, b2, b1).stepParams = new Vector4(0f, r, 1f, 0f);
                blurSlot = b2;
            }

            if (fx.focus.NeedsDof)
            {
                dofSlot = NewSlot(2);
                Add(NatanePostFXPass.Dof, dofSlot, cur);
            }

            {
                int t = NewSlot(1);
                Step s = Add(NatanePostFXPass.Final, t, cur);
                if (bloomSlot >= 0) s.Bind(NatanePostFXIds.BloomTex, bloomSlot);
                if (streakSlot >= 0) s.Bind(NatanePostFXIds.StreakTex, streakSlot);
                if (shaftSlot >= 0) s.Bind(NatanePostFXIds.ShaftTex, shaftSlot);
                if (blurSlot >= 0) s.Bind(NatanePostFXIds.BlurTex, blurSlot);
                if (dofSlot >= 0) s.Bind(NatanePostFXIds.DofTex, dofSlot);
                if (needsMask) s.Bind(NatanePostFXIds.MaskTex, SlotMask);
                cur = t;
            }

            if (fx.time.Active)
            {
                needsHistory = true;
                if (historyValid)
                {
                    int t = NewSlot(1);
                    Step s = Add(NatanePostFXPass.Temporal, t, cur).Bind(NatanePostFXIds.HistoryTex, SlotHistory);
                    // コマ落ちで「止める」フレームは履歴をそのまま出す。
                    s.stepParams = new Vector4(holdFrame ? 1f : 0f, 0f, 0f, 0f);
                    cur = t;
                }
                Add(NatanePostFXPass.Copy, SlotHistory, cur);
            }

            resultSlot = cur;
        }

        /// <summary>ブルームのピラミッドの段数 (明るさの正規化に使う)。</summary>
        public int bloomLevels { get; private set; } = 1;

        readonly int[] m_Down = new int[8];
    }

    public static class NatanePostFXIds
    {
        public static readonly int Source = Shader.PropertyToID("_NPFX_Source");
        public static readonly int Aux = Shader.PropertyToID("_NPFX_Aux");
        public static readonly int BloomTex = Shader.PropertyToID("_NPFX_BloomTex");
        public static readonly int BlurTex = Shader.PropertyToID("_NPFX_BlurTex");
        public static readonly int DofTex = Shader.PropertyToID("_NPFX_DofTex");
        public static readonly int StreakTex = Shader.PropertyToID("_NPFX_StreakTex");
        public static readonly int ShaftTex = Shader.PropertyToID("_NPFX_ShaftTex");
        public static readonly int MaskTex = Shader.PropertyToID("_NPFX_MaskTex");
        public static readonly int HistoryTex = Shader.PropertyToID("_NPFX_HistoryTex");
        public static readonly int DepthTex = Shader.PropertyToID("_NPFX_DepthTex");
        public static readonly int LutTex = Shader.PropertyToID("_NPFX_LutTex");
        public static readonly int OverlayTex = Shader.PropertyToID("_NPFX_OverlayTex");
        public static readonly int SourceSize = Shader.PropertyToID("_NPFX_SourceSize");
        public static readonly int ScreenSize = Shader.PropertyToID("_NPFX_ScreenSize");
        public static readonly int StepParams = Shader.PropertyToID("_NPFX_StepParams");
        public static readonly int Time = Shader.PropertyToID("_NPFX_Time");
        public static readonly int DepthMode = Shader.PropertyToID("_NPFX_DepthMode");
        public static readonly int MaskVP = Shader.PropertyToID("_NPFX_MaskVP");
    }
}
