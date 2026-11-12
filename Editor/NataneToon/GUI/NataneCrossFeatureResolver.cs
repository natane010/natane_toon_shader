using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Cross-feature optimization rules and artifact detection/fix.
    /// Resolves interactions between simultaneously enabled features.
    /// </summary>
    public static class NataneCrossFeatureResolver
    {
        /// <summary>
        /// Run all cross-feature resolution rules on a material.
        /// Call after all features have been configured.
        /// </summary>
        public static void Resolve(Material mat)
        {
            if (mat == null) return;

            ResolveSSSAndSpecular(mat);
            ResolveOutlineAndNormalFlatten(mat);
            ResolveHairSpecAndSpecular(mat);
            ResolveMatCapAndReflection(mat);
            ResolveSpecularFromShadowSharpness(mat);
            ResolveRimFromShadowDarkness(mat);
        }

        /// <summary>
        /// Detect and fix known visual artifacts.
        /// </summary>
        public static void DetectAndFixArtifacts(Material mat)
        {
            if (mat == null) return;

            FixBanding(mat);
            FixSpecularJaggies(mat);
            FixRimHalo(mat);
            FixTextureKeywordMismatch(mat);
        }

        // ===== Cross-feature rules =====

        /// <summary>
        /// C-1: SSS ON → soften Specular to avoid overbright highlights on translucent surfaces.
        /// </summary>
        private static void ResolveSSSAndSpecular(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_SSS") || !IsFeatureEnabled(mat, "_Specular"))
                return;

            float intensity = GetFloat(mat, "_SpecularIntensity", 1f);
            mat.SetFloat("_SpecularIntensity", intensity * 0.6f);

            float softness = GetFloat(mat, "_SpecularSoftness", 0f);
            mat.SetFloat("_SpecularSoftness", Mathf.Min(softness + 0.15f, 1f));
        }

        /// <summary>
        /// C-2: Outline ON → ensure NormalFlattenY ≥ 0.15 to prevent outline breaks.
        /// </summary>
        private static void ResolveOutlineAndNormalFlatten(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_Outline"))
                return;

            float flattenY = GetFloat(mat, "_NormalFlattenY", 0f);
            if (flattenY < 0.15f)
                mat.SetFloat("_NormalFlattenY", Mathf.Max(flattenY, 0.15f));
        }

        /// <summary>
        /// C-3: HairSpecular ON → disable normal Specular (mutual exclusion).
        /// </summary>
        private static void ResolveHairSpecAndSpecular(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_HairSpecular") || !IsFeatureEnabled(mat, "_Specular"))
                return;

            mat.SetFloat("_Specular", 0f);
            mat.DisableKeyword("_SPECULAR");
        }

        /// <summary>
        /// C-4: MatCap + Reflection → cap combined intensity ≤ 1.2.
        /// </summary>
        private static void ResolveMatCapAndReflection(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_MatCap") || !IsFeatureEnabled(mat, "_Reflection"))
                return;

            float matCapI = GetFloat(mat, "_MatCapIntensity", 0f);
            float reflI = GetFloat(mat, "_ReflectionIntensity", 0f);
            float total = matCapI + reflI;
            if (total > 1.2f)
            {
                float scale = 1.2f / total;
                mat.SetFloat("_MatCapIntensity", matCapI * scale);
                mat.SetFloat("_ReflectionIntensity", reflI * scale);
            }
        }

        /// <summary>
        /// C-5: Derive SpecularSize from ShadowSharpness (shader formula: smoothstep correlation).
        /// </summary>
        private static void ResolveSpecularFromShadowSharpness(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_Specular"))
                return;

            float sharpness = GetFloat(mat, "_ShadowSharpness", 0.5f);
            mat.SetFloat("_SpecularSize", Mathf.Lerp(0.25f, 0.85f, sharpness));
            mat.SetFloat("_SpecularSoftness", Mathf.Lerp(0.5f, 0.1f, sharpness));
        }

        /// <summary>
        /// C-6: Derive RimIntensity from shadow darkness.
        /// </summary>
        private static void ResolveRimFromShadowDarkness(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_RimLight"))
                return;
            if (!mat.HasProperty("_ShadowColor"))
                return;

            Color shadowCol = mat.GetColor("_ShadowColor");
            float shadowLum = 0.299f * shadowCol.r + 0.587f * shadowCol.g + 0.114f * shadowCol.b;
            mat.SetFloat("_RimIntensity", Mathf.Lerp(1.5f, 0.5f, shadowLum));
        }

        // ===== Artifact detection & fix =====

        /// <summary>
        /// Banding fix: if ShadowSteps > 6 and DitheringStrength is too low, increase it.
        /// </summary>
        private static void FixBanding(Material mat)
        {
            float steps = GetFloat(mat, "_ShadowSteps", 2f);
            float dithering = GetFloat(mat, "_DitheringStrength", 0f);
            if (steps > 6f && dithering < 0.1f)
            {
                mat.SetFloat("_DitheringStrength", 0.3f);
                if (mat.HasProperty("_UseDithering"))
                {
                    mat.SetFloat("_UseDithering", 1f);
                    mat.EnableKeyword("_USE_DITHERING");
                }
            }
        }

        /// <summary>
        /// Specular jaggies fix: enable anti-aliasing for large specular spots.
        /// </summary>
        private static void FixSpecularJaggies(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_Specular"))
                return;

            float size = GetFloat(mat, "_SpecularSize", 0f);
            if (size > 0.7f && mat.HasProperty("_SpecularAA"))
            {
                mat.SetFloat("_SpecularAA", 1f);
                mat.EnableKeyword("_SPECULAR_AA");
            }
        }

        /// <summary>
        /// Rim halo fix: reduce rim when NormalFlattenY causes full-perimeter glow.
        /// </summary>
        private static void FixRimHalo(Material mat)
        {
            if (!IsFeatureEnabled(mat, "_RimLight"))
                return;

            float flattenY = GetFloat(mat, "_NormalFlattenY", 0f);
            float rimI = GetFloat(mat, "_RimIntensity", 0f);
            if (flattenY > 0.3f && rimI > 0.6f)
            {
                mat.SetFloat("_RimIntensity", rimI - (flattenY - 0.3f) * 0.5f);
            }
        }

        /// <summary>
        /// Texture/keyword mismatch: disable keyword if texture is null, enable if texture exists.
        /// </summary>
        private static void FixTextureKeywordMismatch(Material mat)
        {
            CheckTextureKeyword(mat, "_SDFMap", "_SDF_MAP", "_UseSDFMap");
            CheckTextureKeyword(mat, "_BumpMap", "_USE_NORMAL_MAP", "_UseNormalMap");
        }

        private static void CheckTextureKeyword(Material mat, string texProp, string keyword, string toggleProp)
        {
            if (!mat.HasProperty(texProp)) return;
            bool hasTex = mat.GetTexture(texProp) != null;
            bool isOn = mat.IsKeywordEnabled(keyword);

            if (isOn && !hasTex)
            {
                mat.DisableKeyword(keyword);
                if (mat.HasProperty(toggleProp))
                    mat.SetFloat(toggleProp, 0f);
            }
            else if (!isOn && hasTex && mat.HasProperty(toggleProp))
            {
                float val = mat.GetFloat(toggleProp);
                if (val > 0.5f)
                    mat.EnableKeyword(keyword);
            }
        }

        // ===== Utility =====

        private static bool IsFeatureEnabled(Material mat, string propertyName)
        {
            if (!mat.HasProperty(propertyName)) return false;
            return mat.GetFloat(propertyName) > 0.5f;
        }

        private static float GetFloat(Material mat, string propertyName, float defaultValue)
        {
            if (!mat.HasProperty(propertyName)) return defaultValue;
            return mat.GetFloat(propertyName);
        }
    }
}
