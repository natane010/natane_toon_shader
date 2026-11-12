using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NataneToon.Editor
{
    /// <summary>
    /// lilToon の設定値から、同じ見た目になる Natane Toon のパラメーターを求める。
    /// シェーダー側に互換用の経路は持たず、Natane 自身の陰影モデルの値だけで合わせる。
    /// 変換の最後に呼ぶ (ここで決めた値が最終値になる)。
    /// </summary>
    internal static class LilToonLookMatcher
    {
        /// <summary>シェーダーを差し替える前に lilToon マテリアルの全プロパティを控えたもの。</summary>
        internal sealed class Source
        {
            public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            public readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
            public readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
            public string shaderName = "";

            public bool Has(string name) { return floats.ContainsKey(name) || colors.ContainsKey(name); }
            public float F(string name, float fallback)
            {
                float v;
                return floats.TryGetValue(name, out v) ? v : fallback;
            }
            public bool On(string name) { return F(name, 0f) > 0.5f; }
            public Color C(string name, Color fallback)
            {
                Color v;
                return colors.TryGetValue(name, out v) ? v : fallback;
            }
            public Texture T(string name)
            {
                Texture v;
                return textures.TryGetValue(name, out v) ? v : null;
            }
        }

        internal static Source Capture(Material lil)
        {
            var s = new Source();
            if (lil == null || lil.shader == null) return s;
            Shader sh = lil.shader;
            s.shaderName = sh.name;
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                string name = sh.GetPropertyName(i);
                switch (sh.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        s.floats[name] = lil.GetFloat(name);
                        break;
                    case ShaderPropertyType.Color:
                        s.colors[name] = lil.GetColor(name);
                        break;
                    case ShaderPropertyType.Vector:
                        s.colors[name] = (Color)lil.GetVector(name);
                        break;
                    case ShaderPropertyType.Texture:
                        s.textures[name] = lil.GetTexture(name);
                        break;
                }
            }
            return s;
        }

        // シェーダー内の計算はプロジェクトの色空間で行われる。合成後の色を求めるときは同じ空間に直す。
        static bool Linear { get { return PlayerSettings.colorSpace == ColorSpace.Linear; } }
        static Vector3 ToShader(Color c)
        {
            if (Linear) c = c.linear;
            return new Vector3(c.r, c.g, c.b);
        }
        static Color FromShader(Vector3 v, float a)
        {
            var c = new Color(Mathf.Max(0f, v.x), Mathf.Max(0f, v.y), Mathf.Max(0f, v.z), a);
            if (Linear) c = c.gamma;
            c.a = a;
            return c;
        }

        static void SetF(Material m, string name, float value)
        {
            if (m.HasProperty(name)) m.SetFloat(name, value);
        }
        static void SetC(Material m, string name, Color value)
        {
            if (m.HasProperty(name)) m.SetColor(name, value);
        }
        static void Toggle(Material m, string prop, string keyword, bool on)
        {
            SetF(m, prop, on ? 1f : 0f);
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }

        // lilToon の lilTooningNoSaturateScale 相当 (AA 項を除く)。
        static float LilToon(float value, float border, float blur)
        {
            float lo = Mathf.Clamp01(border - blur * 0.5f);
            float hi = Mathf.Clamp01(border + blur * 0.5f);
            return Mathf.Clamp01((value - lo) / Mathf.Max(hi - lo, 1e-4f));
        }

        internal static void Apply(Source s, Material t, List<string> infos, List<string> warnings)
        {
            if (s == null || t == null || s.floats.Count == 0) return;

            // 通過時のステンシル操作は lilToon が _StencilPass、Natane が _StencilOp。
            // 移さないと顔がステンシルを書かず、前髪の落ち影や透け眉が出ない。
            if (s.Has("_StencilPass")) SetF(t, "_StencilOp", s.F("_StencilPass", 0f));

            if (t.shader != null && t.shader.name == "Natane/Toon Shader FakeShadow")
            {
                ApplyFakeShadow(s, t, infos);
                return;
            }
            if (!s.Has("_ShadowBorder") && !s.Has("_UseShadow")) return; // lilToon ではない

            ApplyBaseShading(s, t, infos, warnings);
            ApplyEffects(s, t, infos, warnings);
        }

        // lilToon の FakeShadow: 髪の複製メッシュを光の反対へ _FakeShadowVector.w だけずらし、_Color を乗算する。
        static void ApplyFakeShadow(Source s, Material t, List<string> infos)
        {
            Color c = s.C("_Color", Color.white);
            int src = Mathf.RoundToInt(s.F("_SrcBlend", 2f));
            int dst = Mathf.RoundToInt(s.F("_DstBlend", 0f));
            // DstColor*Zero と Zero*SrcColor はどちらも乗算。
            bool multiply = (src == 2 && dst == 0) || (src == 0 && dst == 3);
            NataneFakeShadowShaderGUI.ApplyBlendMode(t, multiply);
            SetC(t, "_ShadowColor", new Color(c.r, c.g, c.b, 1f));
            SetF(t, "_ShadowAlpha", multiply ? 1f : Mathf.Clamp01(c.a));
            SetF(t, "_LightColorFollow", 0f);
            SetF(t, "_FadeByViewAngle", 0f);

            Color v = s.C("_FakeShadowVector", new Color(0f, 0f, 0f, 0.05f));
            SetF(t, "_LightShift", Mathf.Max(0f, v.a));
            if (t.HasProperty("_LightShiftBias")) t.SetVector("_LightShiftBias", new Vector4(v.r, v.g, v.b, 0f));
            infos.Add(string.Format("FakeShadow: {0}、ライト方向へ {1:F3} m ずらす設定を移しました。", multiply ? "乗算" : "アルファ合成", v.a));
        }

        static float Lum(Vector3 c) { return c.x * 0.299f + c.y * 0.587f + c.z * 0.114f; }
        static float MaxC(Vector3 c) { return Mathf.Max(c.x, Mathf.Max(c.y, c.z)); }

        const int kSample = 32;
        static readonly Dictionary<Texture, Color[]> s_SampleCache = new Dictionary<Texture, Color[]>();

        // テクスチャを 32x32 に縮めて読む (読み取り不可のテクスチャでも読めるよう Blit を通す)。
        static Color[] Sample(Texture tex)
        {
            if (tex == null) return null;
            Color[] px;
            if (s_SampleCache.TryGetValue(tex, out px)) return px;
            RenderTexture rt = RenderTexture.GetTemporary(kSample, kSample, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(kSample, kSample, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, kSample, kSample), 0, 0);
                read.Apply();
                px = read.GetPixels();
                Object.DestroyImmediate(read);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            s_SampleCache[tex] = px;
            return px;
        }

        // メインテクスチャの平均色 (シェーダーの色空間)。「メインカラーを掛ける強さ」を定数で近似するのに使う。
        // mask を渡すと、エフェクトが実際に乗る範囲 (マスクの明るい所) だけで平均する。
        static Vector3 AverageAlbedo(Source s, Texture mask = null)
        {
            Vector3 tint = ToShader(s.C("_Color", Color.white));
            Color[] main = Sample(s.T("_MainTex"));
            if (main == null) return tint;
            Color[] weight = Sample(mask);
            Vector3 sum = Vector3.zero;
            float total = 0f;
            for (int i = 0; i < main.Length; i++)
            {
                float w = weight != null ? Mathf.Max(weight[i].r, Mathf.Max(weight[i].g, weight[i].b)) : 1f;
                sum += ToShader(main[i]) * w;
                total += w;
            }
            if (total < 1e-3f) return tint * 0.5f;
            return Vector3.Scale(sum / total, tint);
        }

        // Natane の加算系エフェクトは「加える色 × saturate(|加える色| × k)」と、自分の強さでもう一度
        // 絞ってから足す。lilToon は色 × 強さをそのまま足す。ピークの明るさが揃う強度を逆算する。
        // 0.7 は SafeAdditiveBlend が下地の明るさに応じて掛ける圧縮 (1 - 下地輝度 × 0.5) の代表値。
        static float AdditiveIntensity(float target, float colorLength, float k)
        {
            if (target <= 0f || colorLength <= 1e-4f) return 0f;
            float linearFrom = 1f / (colorLength * k); // これ以上は saturate が 1 に張り付き、線形になる
            float quad = Mathf.Sqrt(target / (colorLength * k * 0.7f));
            return quad <= linearFrom ? quad : target / 0.7f;
        }

        // ---- 追加エフェクト -------------------------------------------------------------
        static void ApplyEffects(Source s, Material t, List<string> infos, List<string> warnings)
        {
            Vector3 albedo = AverageAlbedo(s);

            // Natane 独自の質感補正は lilToon に無いので切る。
            SetF(t, "_Glossiness", 1f);
            SetF(t, "_MatteEffect", 0f);

            ApplyMatCap(s, t, "", "_UseMatCap", "_MatCapTex", "_MatCapColor", "_MatCapBlend", "_MatCapBlendMode", "_MatCapMainStrength",
                "_MatCap", "_MATCAP", "_MatCapIntensity", "_MatCapBlend", "_MatCapBlendMode", "_MatCapBlur",
                Mathf.Clamp01(Lum(AverageAlbedo(s, s.T("_MatCapBlendMask")))), warnings);
            ApplyMatCap(s, t, " 2nd", "_UseMatCap2nd", "_MatCap2ndTex", "_MatCap2ndColor", "_MatCap2ndBlend", "_MatCap2ndBlendMode", "_MatCap2ndMainStrength",
                "_MatCap2", "_MATCAP_2", "_MatCapIntensity2", "_MatCapBlend2", "_MatCapBlendMode2", "_MatCap2Blur",
                Mathf.Clamp01(Lum(AverageAlbedo(s, s.T("_MatCap2ndBlendMask")))), warnings);

            ApplyEmission(s, t, albedo, warnings);
            ApplyRim(s, t, albedo, infos, warnings);
            ApplySpecular(s, t, albedo, infos);

            // バックライト: lilToon は視線がライトに向かうときだけ出る。Natane のバックライトは視線に
            // よらないので、移すと順光側から見た背面まで明るくなる。切っておく。
            SetF(t, "_BacklightIntensity", 0f);
            if (s.On("_UseBacklight")) infos.Add("バックライトは視線依存の式が異なるため移行していません。");

            if (s.On("_UseRimShade")) infos.Add("リムシェード (輪郭側を暗くする) は Natane に相当する機能が無いため移行していません。");
        }

        // lilToon: col = Blend(col, tex * color.rgb, _MatCapBlend * color.a * mask, mode)  (0 通常 / 1 加算 / 2 スクリーン / 3 乗算)
        // Natane:  加算 / 乗算 / 置換 を _MatCapBlend で元の色と混ぜる。色の指定は無い。
        static void ApplyMatCap(Source s, Material t, string label, string lilUse, string lilTex, string lilColor, string lilBlend, string lilMode, string lilMain,
            string toggle, string keyword, string intensity, string blend, string mode, string blur, float albedoLum, List<string> warnings)
        {
            if (!t.HasProperty(toggle)) return;
            bool on = s.On(lilUse) && s.T(lilTex) != null;
            // UV1 に貼る MatCap (瞳のハイライトなど) は視線基準の Natane の MatCap では別物になる。
            Color uv1 = s.C(lilBlend + "UV1", Color.clear);
            if (on && Mathf.Max(uv1.r, uv1.g) > 0.5f)
            {
                on = false;
                warnings.Add("MatCap" + label + " は UV1 基準 (_MatCapBlendUV1) のため移行していません。");
            }
            Toggle(t, toggle, keyword, on);
            if (!on) return;

            Color c = s.C(lilColor, Color.white);
            Vector3 rgb = ToShader(c);
            float tint = MaxC(rgb);
            float strength = Mathf.Clamp01(s.F(lilBlend, 1f)) * Mathf.Clamp01(c.a);
            int lil = Mathf.RoundToInt(s.F(lilMode, 1f));
            float main = Mathf.Clamp01(s.F(lilMain, 0f));

            SetF(t, blur, 0f);
            SetF(t, intensity, 1f);
            if (lil == 3)
            {
                SetF(t, mode, 1f); // Multiply
                SetF(t, blend, strength);
            }
            else if (lil == 0)
            {
                SetF(t, mode, 2f); // Replace
                SetF(t, blend, strength);
            }
            else
            {
                // 加算 / スクリーン。メインカラーを掛ける分は平均の明るさで近似する。
                SetF(t, mode, 0f);
                float k = strength * tint * Mathf.Lerp(1f, albedoLum, main);
                SetF(t, blend, Mathf.Clamp01(k / 0.7f));
            }
            if (Mathf.Abs(rgb.x - rgb.y) > 0.05f || Mathf.Abs(rgb.y - rgb.z) > 0.05f)
                warnings.Add("MatCap" + label + " の色 (色相) は Natane に指定が無いため、明るさだけを移しました。");
        }

        // lilToon: col += map * color.rgb * (_EmissionBlend * color.a * mask)   (アルファが強さ)
        // Natane:  map * _EmissionColor.rgb を足し、_EmissionBlend で元の色と混ぜる。
        static void ApplyEmission(Source s, Material t, Vector3 albedo, List<string> warnings)
        {
            if (!t.HasProperty("_Emission")) return;
            Color c = s.C("_EmissionColor", Color.white);
            float strength = Mathf.Clamp01(s.F("_EmissionBlend", 1f)) * Mathf.Clamp01(c.a);
            bool on = s.On("_UseEmission") && strength > 0.001f && c.maxColorComponent > 0.001f;
            Toggle(t, "_Emission", "_EMISSION", on);
            if (!on) return;

            float main = Mathf.Clamp01(s.F("_EmissionMainStrength", 0f));
            Vector3 m = Vector3.Lerp(Vector3.one, albedo, main);
            SetC(t, "_EmissionColor", new Color(c.r * m.x, c.g * m.y, c.b * m.z, 1f));
            SetF(t, "_EmissionBlend", Mathf.Clamp01(strength / 0.7f));
            SetF(t, "_EmissionBlendMode", 0f);
            SetF(t, "_EmissionGlow", 0f);
            SetF(t, "_EmissionBlur", 0f);
            Toggle(t, "_EmissionPulse", "_EMISSION_PULSE", false);
            SetF(t, "_EmissionPulseSpeed", 0f);
            if (s.C("_EmissionBlink", Color.clear).maxColorComponent > 0.001f)
                warnings.Add("エミッションの点滅 (_EmissionBlink) は移行していません。");
        }

        // lilToon: rim = toon(pow(1 - N·V, power), border, blur)、col = Blend(col, color.rgb, rim * color.a, mode)
        // Natane:  rim = pow(1 - N·V, _RimPower) * _RimIntensity * _RimColor.rgb を足す。
        static void ApplyRim(Source s, Material t, Vector3 albedo, List<string> infos, List<string> warnings)
        {
            if (!t.HasProperty("_RimLight")) return;
            Color c = s.C("_RimColor", Color.white);
            int lil = Mathf.RoundToInt(s.F("_RimBlendMode", 1f));
            bool on = s.On("_UseRim") && c.a > 0.001f;
            if (on && lil == 3)
            {
                on = false;
                infos.Add("リムライトが乗算ブレンドのため移行していません (Natane のリムは加算系のみ)。");
            }
            Toggle(t, "_RimLight", "_RIM_LIGHT", on);
            Toggle(t, "_RimLight2", "_RIM_LIGHT_2", false);
            if (!on) return;

            // 境界: pow(x, p) = border となる x で Natane 側が半分の明るさになる指数を選ぶ。
            float p = Mathf.Max(s.F("_RimFresnelPower", 3.5f), 0.01f);
            float border = Mathf.Clamp(s.F("_RimBorder", 0.5f), 0.02f, 0.98f);
            float x0 = Mathf.Clamp(Mathf.Pow(border, 1f / p), 0.05f, 0.98f);
            float power = Mathf.Clamp(Mathf.Log(0.5f) / Mathf.Log(x0), 0.1f, 10f);

            float main = Mathf.Clamp01(s.F("_RimMainStrength", 0f));
            Vector3 rgb = Vector3.Scale(ToShader(c), Vector3.Lerp(Vector3.one, albedo, main));
            SetC(t, "_RimColor", FromShader(rgb, 1f));
            SetF(t, "_RimPower", power);
            SetF(t, "_RimIntensity", Mathf.Clamp(AdditiveIntensity(c.a, rgb.magnitude, 0.5f), 0f, 5f));
            SetF(t, "_RimSpread", 0f);
            SetF(t, "_RimBlur", 0f);
            SetF(t, "_RimBlend", 1f);
            SetF(t, "_RimBlendMode", 0f);
            SetF(t, "_RimDirStrength", 0f);
            SetF(t, "_RimDirectionRange", 0f);
            SetF(t, "_RimShadowMask", Mathf.Clamp01(s.F("_RimShadowMask", 0f)));
            if (s.F("_RimDirStrength", 0f) > 0.001f)
                warnings.Add("リムライトの方向別設定 (_RimDirStrength) は移行していません。");
        }

        // lilToon (トゥーンスペキュラ): spec = toon(pow(N·H, 1 / roughness), border, blur)
        //   col += spec * lerp(_Reflectance, albedo, metallic) * _ReflectionColor.rgb * _ReflectionColor.a * ライト色
        // Natane: spec = smoothstep(1 - size - soft, 1 - size + soft, N·H) * _SpecularColor * _SpecularIntensity * ライト色
        static void ApplySpecular(Source s, Material t, Vector3 albedo, List<string> infos)
        {
            if (!t.HasProperty("_Specular")) return;
            Color rc = s.C("_ReflectionColor", Color.white);
            bool on = s.On("_UseReflection") && s.F("_ApplySpecular", 1f) > 0.5f && rc.a > 0.001f;
            if (!on)
            {
                Toggle(t, "_Specular", "_SPECULAR", false);
                return;
            }

            float perceptual = 1f - Mathf.Clamp01(s.F("_Smoothness", 1f));
            float roughness = Mathf.Max(perceptual * perceptual, 0.002f);
            float metallic = Mathf.Clamp01(s.F("_Metallic", 0f));
            float reflectance = Mathf.Clamp01(s.F("_Reflectance", 0.04f));

            float size, soft, peak;
            if (s.F("_SpecularToon", 1f) > 0.5f)
            {
                float b = Mathf.Clamp(s.F("_SpecularBorder", 0.5f), 0.001f, 1f);
                float bl = Mathf.Clamp01(s.F("_SpecularBlur", 0f));
                float lo = Mathf.Pow(Mathf.Clamp(b - bl * 0.5f, 0.001f, 1f), roughness);
                float hi = Mathf.Pow(Mathf.Clamp(b + bl * 0.5f, 0.001f, 1f), roughness);
                size = 1f - (lo + hi) * 0.5f;
                soft = Mathf.Max((hi - lo) * 0.5f, 0.001f);
                peak = 1f;
            }
            else
            {
                // GGX ハイライトは幅 ≒ 粗さ、ピークは上限 1 で近似する。
                size = Mathf.Clamp01(roughness * 0.25f);
                soft = Mathf.Max(roughness * 0.25f, 0.01f);
                peak = Mathf.Min(1f, 0.25f / (Mathf.PI * roughness));
            }

            Vector3 rgb = Vector3.Scale(ToShader(rc), Vector3.Lerp(Vector3.one * reflectance, albedo, metallic));
            float target = peak * rc.a * MaxC(rgb);
            if (target < 0.002f)
            {
                Toggle(t, "_Specular", "_SPECULAR", false);
                return;
            }
            Toggle(t, "_Specular", "_SPECULAR", true);
            // 色は正規化して渡し、明るさは強度へ寄せる (Natane は強度で絞り込みが決まるため)。
            Vector3 unit = rgb / MaxC(rgb);
            SetC(t, "_SpecularColor", FromShader(unit, 1f));
            SetF(t, "_SpecularIntensity", Mathf.Clamp(AdditiveIntensity(target, unit.magnitude, 0.8f), 0f, 5f));
            SetF(t, "_SpecularSize", Mathf.Clamp01(size));
            SetF(t, "_SpecularSoftness", Mathf.Clamp(soft, 0.001f, 1f));
            SetF(t, "_SpecularBlur", 0f);
            SetF(t, "_SpecularBlend", 1f);
            SetF(t, "_SpecularBlendMode", 0f);
            SetF(t, "_SpecularShadowMask", 0f);
            infos.Add(string.Format("スペキュラ: size={0:F3} softness={1:F3}", size, soft));
        }

        // ---- 基本陰影 ---------------------------------------------------------------
        //
        // lilToon:  hl = N·L * 0.5 + 0.5,  t = saturate((hl - (border - blur/2)) / blur)
        //           色 = lerp(影色 * albedo, albedo, lerp(1, t, strength)) * ライト色
        // Natane:   _WrapAmount = 1 で同じハーフランバートになり、Toon モード 1 段は
        //           smoothstep(0.5 - sharpness/2, 0.5 + sharpness/2, hl + _ShadowOffset) を返す。
        //           色 = lerp(_ShadowColor, 1, 陰影値) * albedo * ライト色
        // よって offset = 0.5 - border、sharpness = blur、_ShadowColor = lerp(1, 影色, strength)。
        static void ApplyBaseShading(Source s, Material t, List<string> infos, List<string> warnings)
        {
            bool useShadow = s.On("_UseShadow");
            float border = Mathf.Clamp01(s.F("_ShadowBorder", 0.5f));
            float blur = Mathf.Clamp01(s.F("_ShadowBlur", 0.1f));
            float strength = useShadow ? Mathf.Clamp01(s.F("_ShadowStrength", 1f)) : 0f;

            SetF(t, "_ShadingMode", 0f);
            SetF(t, "_ShadowSteps", 1f);
            SetF(t, "_WrapAmount", 1f);

            // 境界のグラデーション: lilToon は境界の陰側 _ShadowBorderRange の幅で、影色を
            // _ShadowBorderColor の割合だけ明るい側へ戻す。色が白に近いほど「境界そのものが陰側へ
            // 広がったぼかし」と同じ見え方になるので、その分だけ Natane のぼかし幅を広げる。
            float rampLo = Mathf.Clamp01(border - blur * 0.5f);
            float rampHi = Mathf.Clamp01(border + blur * 0.5f);
            float range = useShadow ? Mathf.Clamp01(s.F("_ShadowBorderRange", 0f)) : 0f;
            if (range > 0.001f)
            {
                Vector3 bc = ToShader(s.C("_ShadowBorderColor", Color.black));
                float back = Mathf.Clamp01((bc.x + bc.y + bc.z) / 3f);
                rampLo = Mathf.Lerp(rampLo, Mathf.Clamp01(rampLo - range), back);
                if (Mathf.Abs(bc.x - bc.y) > 0.3f || Mathf.Abs(bc.y - bc.z) > 0.3f)
                    infos.Add("影の境界色 (_ShadowBorderColor) の色味は、ぼかし幅としてだけ移しました。");
            }
            float rampCenter = (rampLo + rampHi) * 0.5f;
            float rampWidth = Mathf.Clamp(rampHi - rampLo, 0.001f, 1f);
            SetF(t, "_ShadowOffset", Mathf.Clamp(0.5f - rampCenter, -1f, 1f));
            SetF(t, "_ShadowSharpness", rampWidth);
            SetF(t, "_StepBorderSmooth", 0f);
            SetF(t, "_ShadowSmoothing", 0f);
            SetF(t, "_ShadowBlend", 0f);
            SetF(t, "_LitSoftness", 0f);
            SetF(t, "_LightBlend", 0f);
            SetF(t, "_ShadowNaturalness", 0f);
            SetF(t, "_NormalFlattenY", 0f);
            SetF(t, "_ShadowMaxDarkness", 0f);
            SetF(t, "_ShadowAutoColor", 0f);
            SetF(t, "_ShadowHueShift", 0f);
            SetF(t, "_ShadowSaturation", 1f);
            SetF(t, "_ShadowBorderStrength", 0f);
            SetF(t, "_ShadowReceive", useShadow ? Mathf.Clamp01(s.F("_ShadowReceive", 0f)) : 0f);

            // 2 影・3 影: lilToon は hl に対する別々のしきい値で重ねる。Natane の 2 影・3 影は
            // 1 影の陰影値に対するしきい値なので同じ位置には置けない。影側 (1 影の境界より暗い側)
            // を球面上の面積比 (= hl について一様) で平均した 1 色にまとめる。
            Vector3 s1 = ToShader(s.C("_ShadowColor", new Color(0.82f, 0.76f, 0.85f, 1f)));
            Color c2 = s.C("_Shadow2ndColor", new Color(0.68f, 0.66f, 0.79f, 0f));
            Color c3 = s.C("_Shadow3rdColor", new Color(0f, 0f, 0f, 0f));
            Vector3 s2 = ToShader(c2), s3 = ToShader(c3);
            float b2 = s.F("_Shadow2ndBorder", 0.15f), bl2 = s.F("_Shadow2ndBlur", 0.1f);
            float b3 = s.F("_Shadow3rdBorder", 0.25f), bl3 = s.F("_Shadow3rdBlur", 0.1f);
            bool layered = c2.a > 0.001f || c3.a > 0.001f;

            Vector3 shadow = s1;
            if (layered)
            {
                float top = Mathf.Max(border - blur * 0.5f, 0f);
                const int Samples = 32;
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < Samples; i++)
                {
                    float hl = top * (i + 0.5f) / Samples;
                    Vector3 c = s1;
                    c = Vector3.Lerp(c, s2, c2.a * (1f - LilToon(hl, b2, bl2)));
                    c = Vector3.Lerp(c, s3, c3.a * (1f - LilToon(hl, b3, bl3)));
                    sum += c;
                }
                shadow = sum / Samples;
                infos.Add("2 影・3 影は影側の平均色 1 色にまとめました (Natane の 2 影・3 影はしきい値の基準が異なるため)。");
            }
            shadow = Vector3.Lerp(Vector3.one, shadow, strength);
            SetC(t, "_ShadowColor", FromShader(shadow, 1f));
            Toggle(t, "_UseMultiShadow", "_USE_MULTI_SHADOW", false);

            // 影色へのメインカラー乗算 (lilToon: lerp(影, 影 * albedo, _ShadowMainStrength))。
            // 影色テクスチャが空いていれば、メインテクスチャをそこへ入れて同じ式にする。
            float mainStrength = Mathf.Clamp01(s.F("_ShadowMainStrength", 0f));
            Texture shadowTex = s.T("_ShadowColorTex");
            if (shadowTex != null)
            {
                SetF(t, "_ShadowColorTexStrength", 0f);
                warnings.Add("影色テクスチャ (_ShadowColorTex) は lilToon ではメインテクスチャを置き換えますが、Natane では乗算のため移していません。");
            }
            else if (mainStrength > 0.001f && useShadow && t.HasProperty("_ShadowColorTex"))
            {
                t.SetTexture("_ShadowColorTex", s.T("_MainTex"));
                SetF(t, "_ShadowColorTexStrength", mainStrength * strength);
            }
            else
            {
                SetF(t, "_ShadowColorTexStrength", 0f);
            }

            // ライト色: lilToon は clamp(ライト + 環境光, Min, Max)。
            SetF(t, "_LightColorMin", Mathf.Clamp01(s.F("_LightMinLimit", 0.05f)));
            SetF(t, "_LightColorMax", Mathf.Clamp(s.F("_LightMaxLimit", 1f), 0f, 10f));
            SetF(t, "_MonochromeLighting", Mathf.Clamp01(s.F("_MonochromeLighting", 0f)));
            SetF(t, "_LightColorInfluence", 1f);
            SetF(t, "_LightIntensity", 1f);
            SetF(t, "_LightMinInfluence", 0f);
            SetF(t, "_LightMaxInfluence", 2f);
            SetF(t, "_AlbedoPreservation", 0f);
            SetF(t, "_Saturation", 1f);

            // 影への環境光: lilToon は lerp(影, albedo, saturate(間接光 * _ShadowEnvStrength))。
            // Natane は間接光に _IndirectLightIntensity * _GIIntensity を掛けてから同じ式に入れるので 1 に揃える。
            SetF(t, "_ShadowEnvStrength", Mathf.Clamp01(s.F("_ShadowEnvStrength", 0f)));
            SetF(t, "_IndirectLightIntensity", 1f);
            SetF(t, "_GIIntensity", 1f);
            SetC(t, "_IndirectLightMinColor", Color.black);
            // lilToon は環境光をそのまま主光源へ足す。Natane は主光源があると 0.35 倍、無いと陰側と
            // 二重に効くため、どちらにも寄せ切れない。計測で両方の誤差が最小になった中間値。
            SetF(t, "_LightVolumeIntensity", 0.5f);

            infos.Add(string.Format(
                "基本陰影: border={0:F2} blur={1:F2} strength={2:F2} → _ShadowOffset={3:F2} _ShadowSharpness={4:F3} _WrapAmount=1",
                border, blur, strength, 0.5f - rampCenter, rampWidth));
        }
    }
}
