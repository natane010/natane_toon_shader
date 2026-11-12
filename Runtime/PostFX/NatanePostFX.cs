using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NataneToon
{
    /// <summary>
    /// Natane Post FX。カメラに付けると、MV・映像制作向けの画面効果をまとめてかける。
    /// Built-in RP ではこのコンポーネントの OnRenderImage が、URP では Natane Post FX Renderer Feature が描画する。
    /// すべての値は Animation / Timeline でそのままキーフレームを打てる。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Natane/Natane Post FX")]
    public sealed class NatanePostFX : MonoBehaviour
    {
        public NPFXLens lens = new NPFXLens();
        public NPFXGlitch glitch = new NPFXGlitch();
        public NPFXStyle style = new NPFXStyle();
        public NPFXLight glow = new NPFXLight();
        public NPFXFocus focus = new NPFXFocus();
        public NPFXMask mask = new NPFXMask();
        public NPFXColor color = new NPFXColor();
        public NPFXOverlay overlay = new NPFXOverlay();
        public NPFXRetro retro = new NPFXRetro();
        public NPFXFog fog = new NPFXFog();
        public NPFXTime time = new NPFXTime();
        public NPFXBeat beat = new NPFXBeat();

        [NPFX("時間を自分で指定する (Timeline で頭出ししても絵が揃う)", "Use Custom Time")] public bool useCustomTime;
        [NPFX("時間 (秒)", "Time (s)")] public float customTime;

        // ビルドに含めるため、シェーダーは参照として持つ (Shader.Find だけだとビルドで落ちる)。
        [SerializeField, HideInInspector] Shader m_Shader;
        [SerializeField, HideInInspector] Shader m_MaskShader;

        public const string ShaderName = "Hidden/Natane/PostFX";
        public const string MaskShaderName = "Hidden/Natane/PostFXMask";
        const float k_MaskFar = 10000f;

        Camera m_Camera;
        Material m_Material;
        Material m_MaskMaterial;
        RenderTexture m_History;
        bool m_HistoryValid;
        float m_NextHoldTime;
        float m_LastTime;
        readonly NatanePostFXPlan m_Plan = new NatanePostFXPlan();
        readonly List<Renderer> m_MaskRenderers = new List<Renderer>();
        GameObject[] m_MaskTargetsSeen = new GameObject[0];
        int m_MaskRefreshFrame = -1;

        // Built-in RP 実行用
        CommandBuffer m_Cmd;
        MaterialPropertyBlock m_Block;
        readonly List<RenderTexture> m_Temps = new List<RenderTexture>();

        NPFXFeature m_Features;
        Material m_FeaturesMaterial;

        public NatanePostFXPlan Plan => m_Plan;
        /// <summary>今のフレームで有効にしたシェーダーのまとまり。</summary>
        public NPFXFeature ActiveFeatures => m_Features;
        public Material Material => m_Material;
        public Material MaskMaterial => m_MaskMaterial;
        public IReadOnlyList<Renderer> MaskRenderers => m_MaskRenderers;
        public static Color MaskClearColor => new Color(k_MaskFar, 0f, 0f, 0f);

        /// <summary>効果で使う時間。</summary>
        public float EffectTime => useCustomTime ? customTime : (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);

        void Reset() { FindShaders(); }
        void OnValidate() { FindShaders(); }

        void OnEnable()
        {
            FindShaders();
            m_Camera = GetComponent<Camera>();
            m_HistoryValid = false;
            RefreshMaskRenderers();
        }

        void OnDisable()
        {
            ReleaseHistory();
            DestroySafe(m_Material);
            DestroySafe(m_MaskMaterial);
            m_Material = null;
            m_MaskMaterial = null;
            if (m_Cmd != null) { m_Cmd.Release(); m_Cmd = null; }
        }

        void FindShaders()
        {
            if (m_Shader == null) m_Shader = Shader.Find(ShaderName);
            if (m_MaskShader == null) m_MaskShader = Shader.Find(MaskShaderName);
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        bool EnsureMaterials()
        {
            if (m_Shader == null || m_MaskShader == null) FindShaders();
            if (m_Shader == null || !m_Shader.isSupported) return false;
            if (m_Material == null) m_Material = new Material(m_Shader) { hideFlags = HideFlags.HideAndDontSave };
            if (m_MaskMaterial == null && m_MaskShader != null)
                m_MaskMaterial = new Material(m_MaskShader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        public bool NeedsDepth =>
            focus.depthOfField || (style.edge > 1e-4f && style.edgeDepth > 1e-4f) || fog.density > 1e-5f ||
            (mask.HasEffect && m_MaskRenderers.Count > 0);

        /// <summary>マスク対象のレンダラーを集め直す。対象の子を増減したときに呼ぶ。</summary>
        public void RefreshMaskRenderers()
        {
            m_MaskRenderers.Clear();
            GameObject[] targets = mask.targets;
            int count = targets != null ? targets.Length : 0;
            if (m_MaskTargetsSeen.Length != count) m_MaskTargetsSeen = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                m_MaskTargetsSeen[i] = targets[i];
                if (targets[i] == null) continue;
                foreach (Renderer r in targets[i].GetComponentsInChildren<Renderer>(false))
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer) m_MaskRenderers.Add(r);
                }
            }
            m_MaskRefreshFrame = Time.frameCount;
        }

        void RefreshMaskRenderersIfChanged()
        {
            GameObject[] targets = mask.targets;
            int count = targets != null ? targets.Length : 0;
            bool changed = count != m_MaskTargetsSeen.Length;
            for (int i = 0; !changed && i < count; i++) changed = targets[i] != m_MaskTargetsSeen[i];
            // 対象の中身 (子の増減や表示の切り替え) は検知できないので、エディターの編集中だけ定期的に集め直す。
            if (!changed && !Application.isPlaying && mask.HasEffect && Time.frameCount - m_MaskRefreshFrame > 30) changed = true;
            if (changed) RefreshMaskRenderers();
        }

        /// <summary>光芒の中心 (画面 UV)。ライト指定があればその位置。</summary>
        public Vector2 ShaftCenterUV { get; private set; } = new Vector2(0.5f, 0.8f);
        float m_ShaftVisibility = 1f;

        RenderTexture EnsureHistory(int width, int height)
        {
            if (m_History != null && (m_History.width != width || m_History.height != height)) ReleaseHistory();
            if (m_History == null)
            {
                m_History = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "NatanePostFX History",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                m_History.Create();
                m_HistoryValid = false;
            }
            return m_History;
        }

        void ReleaseHistory()
        {
            if (m_History != null)
            {
                m_History.Release();
                DestroySafe(m_History);
                m_History = null;
            }
            m_HistoryValid = false;
        }

        /// <summary>
        /// 1 フレーム分の準備。手順を組み、マテリアルに値を入れる。両パイプラインの実行部が描画の直前に呼ぶ。
        /// depthMode: 0 = 深度なし, 1 = _CameraDepthTexture, 2 = _NPFX_DepthTex (実行部が渡す)。
        /// 戻り値が false なら何も描かない。history は残像を使うときだけ返る。
        /// </summary>
        public bool PrepareFrame(Camera cam, int width, int height, int depthMode, out RenderTexture history)
        {
            history = null;
            if (!isActiveAndEnabled || cam == null || width <= 0 || height <= 0) return false;
            if (!EnsureMaterials()) return false;

            RefreshMaskRenderersIfChanged();
            float t = EffectTime;

            // 光芒の中心
            m_ShaftVisibility = 1f;
            ShaftCenterUV = glow.shaftCenter;
            if (glow.shaftLight != null)
            {
                Light l = glow.shaftLight;
                Vector3 world = l.type == LightType.Directional
                    ? cam.transform.position - l.transform.forward * 1000f
                    : l.transform.position;
                Vector3 vp = cam.WorldToViewportPoint(world);
                ShaftCenterUV = new Vector2(vp.x, vp.y);
                // ライトがカメラの後ろなら光芒は出さない。画面の外へ出るにつれて弱める。
                float off = Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f));
                m_ShaftVisibility = vp.z > 0f ? Mathf.Clamp01(1.5f - off) : 0f;
            }

            // コマ落ち: 次の更新時刻まで前の絵を出し続ける。
            bool hold = false;
            if (time.frameHoldFps > 0.5f)
            {
                if (t < m_LastTime) m_NextHoldTime = t; // 時間が巻き戻った (Timeline の頭出しなど)
                if (t >= m_NextHoldTime) m_NextHoldTime = Mathf.Max(m_NextHoldTime + 1f / time.frameHoldFps, t);
                else hold = true;
            }
            m_LastTime = t;

            bool maskAvailable = m_MaskMaterial != null && m_MaskRenderers.Count > 0;
            bool historyValid = m_HistoryValid && m_History != null && m_History.width == width && m_History.height == height;
            m_Plan.Build(this, width, height, maskAvailable, historyValid, hold && historyValid);

            // 使っていない効果のコードをシェーダーから外す (キーワード)。
            NPFXFeature features = NatanePostFXKeywords.forceAll
                ? NPFXFeature.All
                : NatanePostFXKeywords.Needed(this, m_Plan.needsComposite, m_Plan.needsMask);

            // 何も効いていないなら描かない (呼び出し側は元の画像をそのまま出す)。
            if (features == NPFXFeature.None && m_Plan.stepCount == 1 && !m_Plan.needsHistory &&
                overlay.flash <= 1e-4f && overlay.fade <= 1e-4f && !(beat.On && beat.flash > 1e-4f))
            {
                if (m_History != null) ReleaseHistory();
                return false;
            }

            if (m_Plan.needsHistory)
            {
                history = EnsureHistory(width, height);
                m_HistoryValid = true; // このフレームの最後に書き込まれる
            }
            else if (m_History != null)
            {
                ReleaseHistory();
            }

            if (m_Plan.needsMask)
            {
                // 中間テクスチャに描くので、上下の向きはレンダーテクスチャ用にする。
                Matrix4x4 vp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
                m_MaskMaterial.SetMatrix(NatanePostFXIds.MaskVP, vp);
            }

            ApplyUniforms(m_Material, cam, width, height, m_Plan.needsMask || NeedsDepth ? depthMode : 0, t, hold && historyValid, false);

            // キーワードは変わったときだけ設定し直す。
            if (features != m_Features || m_FeaturesMaterial != m_Material)
            {
                NatanePostFXKeywords.Apply(m_Material, features);
                m_Features = features;
                m_FeaturesMaterial = m_Material;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // VRChat ワールド用 (Natane/PostFX World 系のシェーダー) への書き出し
        // ------------------------------------------------------------------
        // ワールド用シェーダーが時間や解像度を足してから使う値。マテリアルでは名前に _Mat が付く。
        static readonly string[] s_WorldDynamic =
        {
            "_L_Distort", "_L_Xform", "_L_Wave", "_L_Glitch2", "_L_Misc", "_S_Edge", "_S_Halftone",
            "_F_Flash", "_F_Lines", "_F_Crt", "_F_Film",
        };
        static readonly string[] s_WorldStatic =
        {
            "_L_RadialBlur", "_L_Mirror", "_L_Ripple", "_L_Glitch",
            "_S_Kuwahara", "_S_EdgeColor", "_S_Paper", "_S_Extra",
            "_F_Fog", "_F_FogColor", "_F_Grade", "_F_Grade2", "_F_Lift", "_F_Gamma", "_F_Gain",
            "_F_SplitShadow", "_F_SplitHigh", "_F_Lut", "_F_Filter", "_F_Filter2", "_F_Duo", "_F_DuoA", "_F_DuoB",
            "_F_Overlay", "_F_Overlay2", "_F_GradTop", "_F_GradBottom", "_F_LeakColor", "_F_OverlayTexParams",
            "_F_Fade", "_F_Vignette", "_F_VignetteColor", "_F_LinesColor", "_F_Center", "_F_Film2",
        };

        /// <summary>ワールド用で「レンズ」段が要るか。</summary>
        public bool WorldNeedsLens => lens.Active || glitch.Active || beat.AffectsLens;
        /// <summary>ワールド用で「画風」段が要るか。</summary>
        public bool WorldNeedsStyle => style.Active;
        /// <summary>ワールド用で深度テクスチャが要るか (フォグ、深度による輪郭線)。</summary>
        public bool WorldNeedsDepth => fog.density > 1e-5f || (style.edge > 1e-4f && style.edgeDepth > 1e-4f);

        /// <summary>
        /// 今の設定を、ワールド用シェーダーのマテリアルに書き出す。
        /// 中間テクスチャが要る効果 (ブルーム、被写界深度、ぼかし、マスク、残像) は書き出されない。
        /// </summary>
        public bool BakeWorldMaterial(Material target)
        {
            if (target == null || !EnsureMaterials()) return false;
            var temp = new Material(m_Material) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                // 1080p 基準 (ピクセル単位の値はシェーダーが実際の解像度に合わせて伸縮する)。
                ApplyUniforms(temp, null, 1920, 1080, 0, 0f, false, true);
                foreach (string name in s_WorldDynamic) target.SetVector(name + "_Mat", temp.GetVector(name));
                foreach (string name in s_WorldStatic) target.SetVector(name, temp.GetVector(name));
                target.SetTexture(NatanePostFXIds.LutTex, temp.GetTexture(NatanePostFXIds.LutTex));
                target.SetTexture(NatanePostFXIds.OverlayTex, temp.GetTexture(NatanePostFXIds.OverlayTex));
            }
            finally
            {
                DestroySafe(temp);
            }
            target.SetVector("_W_Speeds", new Vector4(lens.waveSpeed, glitch.speed, overlay.lineFps, retro.scanlineSpeed));
            target.SetVector("_W_Shake", new Vector4(lens.shake, lens.shakeSpeed, 0f, 0f));
            target.SetVector("_W_Beat", new Vector4(beat.bpm, beat.offset, beat.decay, 0f));
            target.SetVector("_W_BeatAmount", new Vector4(beat.zoom, beat.flash, beat.chromatic, beat.shake));
            target.SetFloat("_NPFX_UseDepth", WorldNeedsDepth ? 1f : 0f);
            return true;
        }

        // ------------------------------------------------------------------
        // マテリアルへ値を渡す。詰め方は NatanePostFXCore.hlsl のコメントと一致させること。
        // ------------------------------------------------------------------
        static readonly int s_LDistort = Shader.PropertyToID("_L_Distort");
        static readonly int s_LXform = Shader.PropertyToID("_L_Xform");
        static readonly int s_LRadialBlur = Shader.PropertyToID("_L_RadialBlur");
        static readonly int s_LMirror = Shader.PropertyToID("_L_Mirror");
        static readonly int s_LWave = Shader.PropertyToID("_L_Wave");
        static readonly int s_LRipple = Shader.PropertyToID("_L_Ripple");
        static readonly int s_LGlitch = Shader.PropertyToID("_L_Glitch");
        static readonly int s_LGlitch2 = Shader.PropertyToID("_L_Glitch2");
        static readonly int s_LMisc = Shader.PropertyToID("_L_Misc");
        static readonly int s_SKuwahara = Shader.PropertyToID("_S_Kuwahara");
        static readonly int s_SEdge = Shader.PropertyToID("_S_Edge");
        static readonly int s_SEdgeColor = Shader.PropertyToID("_S_EdgeColor");
        static readonly int s_SPaper = Shader.PropertyToID("_S_Paper");
        static readonly int s_SHalftone = Shader.PropertyToID("_S_Halftone");
        static readonly int s_SExtra = Shader.PropertyToID("_S_Extra");
        static readonly int s_BThreshold = Shader.PropertyToID("_B_Threshold");
        static readonly int s_FDof = Shader.PropertyToID("_F_Dof");
        static readonly int s_FTilt = Shader.PropertyToID("_F_Tilt");
        static readonly int s_FBloom = Shader.PropertyToID("_F_Bloom");
        static readonly int s_FBloomTint = Shader.PropertyToID("_F_BloomTint");
        static readonly int s_FStreakTint = Shader.PropertyToID("_F_StreakTint");
        static readonly int s_FShaftTint = Shader.PropertyToID("_F_ShaftTint");
        static readonly int s_FFog = Shader.PropertyToID("_F_Fog");
        static readonly int s_FFogColor = Shader.PropertyToID("_F_FogColor");
        static readonly int s_FMask = Shader.PropertyToID("_F_Mask");
        static readonly int s_FMask2 = Shader.PropertyToID("_F_Mask2");
        static readonly int s_FMaskOutlineColor = Shader.PropertyToID("_F_MaskOutlineColor");
        static readonly int s_FMaskFillColor = Shader.PropertyToID("_F_MaskFillColor");
        static readonly int s_FGrade = Shader.PropertyToID("_F_Grade");
        static readonly int s_FGrade2 = Shader.PropertyToID("_F_Grade2");
        static readonly int s_FLift = Shader.PropertyToID("_F_Lift");
        static readonly int s_FGamma = Shader.PropertyToID("_F_Gamma");
        static readonly int s_FGain = Shader.PropertyToID("_F_Gain");
        static readonly int s_FSplitShadow = Shader.PropertyToID("_F_SplitShadow");
        static readonly int s_FSplitHigh = Shader.PropertyToID("_F_SplitHigh");
        static readonly int s_FLut = Shader.PropertyToID("_F_Lut");
        static readonly int s_FFilter = Shader.PropertyToID("_F_Filter");
        static readonly int s_FFilter2 = Shader.PropertyToID("_F_Filter2");
        static readonly int s_FDuo = Shader.PropertyToID("_F_Duo");
        static readonly int s_FDuoA = Shader.PropertyToID("_F_DuoA");
        static readonly int s_FDuoB = Shader.PropertyToID("_F_DuoB");
        static readonly int s_FOverlay = Shader.PropertyToID("_F_Overlay");
        static readonly int s_FOverlay2 = Shader.PropertyToID("_F_Overlay2");
        static readonly int s_FGradTop = Shader.PropertyToID("_F_GradTop");
        static readonly int s_FGradBottom = Shader.PropertyToID("_F_GradBottom");
        static readonly int s_FLeakColor = Shader.PropertyToID("_F_LeakColor");
        static readonly int s_FOverlayTexParams = Shader.PropertyToID("_F_OverlayTexParams");
        static readonly int s_FFlash = Shader.PropertyToID("_F_Flash");
        static readonly int s_FFade = Shader.PropertyToID("_F_Fade");
        static readonly int s_FVignette = Shader.PropertyToID("_F_Vignette");
        static readonly int s_FVignetteColor = Shader.PropertyToID("_F_VignetteColor");
        static readonly int s_FLines = Shader.PropertyToID("_F_Lines");
        static readonly int s_FLinesColor = Shader.PropertyToID("_F_LinesColor");
        static readonly int s_FCenter = Shader.PropertyToID("_F_Center");
        static readonly int s_FCrt = Shader.PropertyToID("_F_Crt");
        static readonly int s_FFilm = Shader.PropertyToID("_F_Film");
        static readonly int s_FFilm2 = Shader.PropertyToID("_F_Film2");
        static readonly int s_TParams = Shader.PropertyToID("_T_Params");

        // インスペクタの色は sRGB。リニア空間のプロジェクトではシェーダーに渡す前に変換する。
        static Vector4 C(Color c)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? (Vector4)c.linear : (Vector4)c;
        }

        static Vector4 C(Color c, float a)
        {
            Vector4 v = C(c);
            v.w = a;
            return v;
        }

        // world = true のとき: 時間・拍・画面揺れで動く分は入れない (ワールド用シェーダーが自分で計算する)。
        void ApplyUniforms(Material m, Camera cam, int width, int height, int depthMode, float t, bool holdFrame, bool world)
        {
            const float d2r = Mathf.Deg2Rad;
            // 画面の高さに合わせてピクセル単位の値を伸縮する (1080p 基準)。
            float pixelScale = height / 1080f;

            // 拍のパルス (拍の頭で 1、そこから減衰)
            float pulse = 0f;
            if (beat.bpm > 0.01f && !world)
            {
                float secondsPerBeat = 60f / beat.bpm;
                float since = Mathf.Repeat(t - beat.offset, secondsPerBeat);
                pulse = Mathf.Exp(-since * beat.decay);
            }

            m.SetVector(NatanePostFXIds.ScreenSize, new Vector4(1f / width, 1f / height, width, height));
            m.SetFloat(NatanePostFXIds.Time, t);
            m.SetFloat(NatanePostFXIds.DepthMode, depthMode);

            // ---- レンズ ----
            float shakeAmount = world ? 0f : lens.shake + beat.shake * pulse;
            Vector2 shakeOffset = Vector2.zero;
            float shakeRot = 0f;
            if (shakeAmount > 1e-4f)
            {
                float st = t * lens.shakeSpeed;
                shakeOffset = new Vector2(Mathf.PerlinNoise(st, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, st) - 0.5f) * (shakeAmount * 0.1f);
                shakeRot = (Mathf.PerlinNoise(st, st * 0.5f + 5f) - 0.5f) * shakeAmount * 0.1f;
            }
            float zoom = Mathf.Max(lens.zoom, 0.01f) * (1f + beat.zoom * pulse * 0.15f);
            m.SetVector(s_LDistort, new Vector4(lens.distortion, lens.chromaticAberration + beat.chromatic * pulse, lens.rgbShift, lens.rgbShiftAngle * d2r));
            m.SetVector(s_LXform, new Vector4(zoom, lens.rotation * d2r + shakeRot, lens.offset.x + shakeOffset.x, lens.offset.y + shakeOffset.y));
            m.SetVector(s_LRadialBlur, new Vector4(lens.radialBlur, lens.radialCenter.x, lens.radialCenter.y, lens.directionalBlur));
            m.SetVector(s_LMirror, new Vector4((float)lens.mirror, lens.kaleidoscope >= 2f ? Mathf.Round(lens.kaleidoscope) : 0f, lens.kaleidoscopeRotation * d2r, lens.tileX));
            m.SetVector(s_LWave, new Vector4(lens.wave * 0.05f, lens.waveFrequency, t * lens.waveSpeed, lens.heatHaze));
            m.SetVector(s_LRipple, new Vector4(lens.rippleCenter.x, lens.rippleCenter.y, lens.rippleRadius, lens.ripple));
            m.SetVector(s_LGlitch, new Vector4(glitch.block, glitch.blockCount, glitch.lineJitter, glitch.verticalJump));
            m.SetVector(s_LGlitch2, new Vector4(glitch.colorDrift, Mathf.Repeat(Mathf.Floor(t * glitch.speed), 1024f), glitch.vhs, lens.crtCurvature));
            m.SetVector(s_LMisc, new Vector4(lens.directionalAngle * d2r, lens.tileY, lens.rippleWidth, lens.pixelate * pixelScale));

            // ---- 画風 ----
            m.SetVector(s_SKuwahara, new Vector4(style.kuwahara, style.posterize, style.sharpen, style.retroBits));
            m.SetVector(s_SEdge, new Vector4(style.edge, style.edgeThickness * Mathf.Max(pixelScale, 0.5f), style.edgeDepth, style.edgeColorSensitivity));
            m.SetVector(s_SEdgeColor, C(style.inkColor));
            m.SetVector(s_SPaper, C(style.paperColor, style.sketch));
            m.SetVector(s_SHalftone, new Vector4(style.halftone, style.halftoneSize * Mathf.Max(pixelScale, 0.5f), style.halftoneAngle * d2r, style.crosshatch));
            m.SetVector(s_SExtra, new Vector4(style.retroPixelSize, style.halftoneColor ? 1f : 0f, style.emboss, 0f));

            // ---- 光 ----
            m.SetVector(s_BThreshold, new Vector4(glow.threshold, glow.knee, 64f, 0f));
            float bloom = (glow.bloom + beat.bloom * pulse * 2f) / Mathf.Max(1, m_Plan.bloomLevels) * 2f;
            m.SetVector(s_FBloom, new Vector4(bloom, glow.streak, glow.shafts * m_ShaftVisibility, glow.diffusion));
            m.SetVector(s_FBloomTint, C(glow.bloomTint));
            m.SetVector(s_FStreakTint, C(glow.streakTint));
            m.SetVector(s_FShaftTint, C(glow.shaftTint));

            // ---- ピント ----
            float focusDistance = focus.focusDistance;
            if (focus.focusTarget != null && cam != null)
                focusDistance = Mathf.Max(0.01f, Vector3.Dot(focus.focusTarget.position - cam.transform.position, cam.transform.forward));
            bool dof = focus.depthOfField && depthMode != 0;
            m.SetVector(s_FDof, new Vector4(dof ? 1f : 0f, focusDistance, focus.focusRange, focus.bokehSize * pixelScale));
            m.SetVector(s_FTilt, new Vector4(focus.tiltShift, focus.tiltCenter, focus.tiltWidth, focus.blur));

            // ---- フォグ・マスク ----
            m.SetVector(s_FFog, new Vector4(fog.density * 0.2f, fog.start, fog.affectSky ? 1f : 0f, 0f));
            m.SetVector(s_FFogColor, C(fog.color, 1f));
            bool maskOn = m_Plan.needsMask;
            m.SetVector(s_FMask, new Vector4(mask.backgroundDesaturate, mask.backgroundDarken, mask.backgroundBlur, mask.outlineWidth * pixelScale));
            m.SetVector(s_FMask2, new Vector4(mask.outline, mask.fill, maskOn ? 1f : 0f, mask.outlineSoftness));
            m.SetVector(s_FMaskOutlineColor, C(mask.outlineColor, mask.outlineColor.a));
            m.SetVector(s_FMaskFillColor, C(mask.fillColor, mask.fillColor.a));

            // ---- 色 ----
            m.SetVector(s_FGrade, new Vector4(Mathf.Pow(2f, color.exposure), color.contrast, color.saturation, color.hueShift / 360f));
            m.SetVector(s_FGrade2, new Vector4(color.temperature, color.tint, color.vibrance, (float)color.tonemap));
            m.SetVector(s_FLift, C(color.lift));
            m.SetVector(s_FGamma, (Vector4)color.gamma); // 係数なので変換しない
            m.SetVector(s_FGain, (Vector4)color.gain);
            m.SetVector(s_FSplitShadow, C(color.shadowTint, color.shadowTintAmount));
            m.SetVector(s_FSplitHigh, C(color.highlightTint, color.highlightTintAmount));
            bool lutOn = color.lut != null && color.lutContribution > 1e-4f;
            m.SetVector(s_FLut, new Vector4(lutOn ? color.lutContribution : 0f, lutOn ? color.lut.height : 32f, 0f, 0f));
            m.SetTexture(NatanePostFXIds.LutTex, lutOn ? (Texture)color.lut : Texture2D.whiteTexture);
            m.SetVector(s_FFilter, new Vector4(color.monochrome, color.sepia, color.invert, color.solarize));
            m.SetVector(s_FFilter2, new Vector4(color.threshold, color.thresholdLevel, color.nightVision, color.thermal));
            m.SetVector(s_FDuo, new Vector4(color.gradientMap, 0f, 0f, 0f));
            m.SetVector(s_FDuoA, C(color.gradientDark));
            m.SetVector(s_FDuoB, C(color.gradientLight));

            // ---- 重ねもの ----
            m.SetVector(s_FOverlay, C(overlay.color, overlay.colorAmount));
            m.SetVector(s_FOverlay2, new Vector4((float)overlay.blend, overlay.gradient, overlay.gradientAngle * d2r, overlay.lightLeak));
            m.SetVector(s_FGradTop, C(overlay.gradientTop));
            m.SetVector(s_FGradBottom, C(overlay.gradientBottom));
            m.SetVector(s_FLeakColor, C(overlay.lightLeakColor));
            bool texOn = overlay.texture != null && overlay.textureAmount > 1e-4f;
            m.SetVector(s_FOverlayTexParams, new Vector4(texOn ? overlay.textureAmount : 0f, (float)overlay.textureBlend, overlay.textureScroll.x, overlay.textureScroll.y));
            m.SetTexture(NatanePostFXIds.OverlayTex, texOn ? overlay.texture : Texture2D.blackTexture);
            m.SetVector(s_FFlash, C(overlay.flashColor, Mathf.Clamp01(overlay.flash + beat.flash * pulse)));
            m.SetVector(s_FFade, C(overlay.fadeColor, overlay.fade));
            m.SetVector(s_FVignette, new Vector4(overlay.vignette, overlay.vignetteSmoothness, overlay.vignetteRoundness, 0f));
            m.SetVector(s_FVignetteColor, C(overlay.vignetteColor, 1f));
            float lineSeed = overlay.lineFps > 0.01f ? Mathf.Repeat(Mathf.Floor(t * overlay.lineFps), 256f) : 0f;
            m.SetVector(s_FLines, new Vector4(overlay.speedLines, overlay.lineDensity, overlay.lineInnerRadius, lineSeed));
            m.SetVector(s_FLinesColor, C(overlay.lineColor, overlay.lineColor.a));
            m.SetVector(s_FCenter, new Vector4(overlay.lineCenter.x, overlay.lineCenter.y, 0f, 0f));

            // ---- レトロ・フィルム ----
            m.SetVector(s_FCrt, new Vector4(retro.scanlines, retro.scanlineCount, t * retro.scanlineSpeed, retro.rgbMask));
            m.SetVector(s_FFilm, new Vector4(retro.grain, retro.grainSize * Mathf.Max(pixelScale, 0.5f), retro.scratches, retro.flicker));
            m.SetVector(s_FFilm2, new Vector4(retro.dust, overlay.letterbox, 0f, retro.staticNoise));

            // ---- 時間 ----
            m.SetVector(s_TParams, new Vector4(holdFrame ? 1f : time.afterimage, time.lightTrail, 0f, 0f));
        }

        // ------------------------------------------------------------------
        // Built-in RP の実行部
        // ------------------------------------------------------------------
        void OnPreCull()
        {
            if (GraphicsSettings.currentRenderPipeline != null) return;
            if (m_Camera == null) m_Camera = GetComponent<Camera>();
            if (NeedsDepth) m_Camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (m_Camera == null) m_Camera = GetComponent<Camera>();
            int width = source.width, height = source.height;
            if (!PrepareFrame(m_Camera, width, height, 1, out RenderTexture history))
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (m_Cmd == null) m_Cmd = new CommandBuffer { name = "Natane Post FX" };
            if (m_Block == null) m_Block = new MaterialPropertyBlock();
            CommandBuffer cmd = m_Cmd;
            cmd.Clear();

            // 一時テクスチャ (スロット 1 以降)
            // 結果を destination へ直接描けるなら、最後のコピー 1 回を省く。
            // destination が null (画面) のときは上下の向きを Unity に任せたいので、従来どおり Blit する。
            bool direct = destination != null && m_Plan.resultSlot > 0 &&
                          destination.width == width && destination.height == height;
            m_Temps.Clear();
            m_Temps.Add(source);
            for (int i = 1; i < m_Plan.slotCount; i++)
            {
                if (direct && i == m_Plan.resultSlot) { m_Temps.Add(destination); continue; }
                int div = m_Plan.GetDivisor(i);
                RenderTexture rt = RenderTexture.GetTemporary(NatanePostFXPlan.Scaled(width, div), NatanePostFXPlan.Scaled(height, div), 0,
                    RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                rt.filterMode = FilterMode.Bilinear;
                rt.wrapMode = TextureWrapMode.Clamp;
                m_Temps.Add(rt);
            }

            RenderTexture maskRT = null;
            if (m_Plan.needsMask)
            {
                maskRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
                maskRT.filterMode = FilterMode.Point;
                maskRT.wrapMode = TextureWrapMode.Clamp;
                cmd.SetRenderTarget(maskRT);
                cmd.ClearRenderTarget(false, true, MaskClearColor);
                DrawMask(cmd);
            }

            for (int i = 0; i < m_Plan.stepCount; i++)
            {
                NatanePostFXPlan.Step step = m_Plan.GetStep(i);
                m_Block.Clear();
                for (int b = 0; b < step.bindingCount; b++)
                    m_Block.SetTexture(step.ids[b], Resolve(step.slots[b], history, maskRT));
                Texture src = Resolve(step.sourceSlot, history, maskRT);
                m_Block.SetVector(NatanePostFXIds.SourceSize, new Vector4(1f / src.width, 1f / src.height, src.width, src.height));
                m_Block.SetVector(NatanePostFXIds.StepParams, step.stepParams);
                cmd.SetRenderTarget((RenderTexture)Resolve(step.dst, history, maskRT));
                cmd.DrawProcedural(Matrix4x4.identity, m_Material, step.pass, MeshTopology.Triangles, 3, 1, m_Block);
            }

            Graphics.ExecuteCommandBuffer(cmd);
            if (!direct) Graphics.Blit(m_Temps[m_Plan.resultSlot], destination);

            for (int i = 1; i < m_Temps.Count; i++)
            {
                if (m_Temps[i] != destination) RenderTexture.ReleaseTemporary(m_Temps[i]);
            }
            m_Temps.Clear();
            if (maskRT != null) RenderTexture.ReleaseTemporary(maskRT);
        }

        Texture Resolve(int slot, RenderTexture history, RenderTexture maskRT)
        {
            if (slot == NatanePostFXPlan.SlotHistory) return history;
            if (slot == NatanePostFXPlan.SlotMask) return maskRT;
            return m_Temps[slot];
        }

        void DrawMask(CommandBuffer cmd)
        {
            for (int i = 0; i < m_MaskRenderers.Count; i++)
            {
                Renderer r = m_MaskRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                int subMeshes = Mathf.Max(1, r.sharedMaterials.Length);
                for (int s = 0; s < subMeshes; s++) cmd.DrawRenderer(r, m_MaskMaterial, s, 0);
            }
        }
    }
}
