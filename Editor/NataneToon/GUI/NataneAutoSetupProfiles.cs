using System;
using System.Collections.Generic;
using UnityEngine;

namespace NataneToon.Editor
{
    // ===== Enums =====

    /// <summary>
    /// Material role — what part of the character/object this material represents.
    /// </summary>
    public enum AutoSetupRole
    {
        Face = 0,
        Hair = 1,
        Clothing = 2,
        Eye = 3,
        Metal = 4,
    }

    /// <summary>
    /// Visual look target — the overall art direction style.
    /// </summary>
    public enum AutoSetupLook
    {
        Anime = 0,
        GameCharacter = 1,
        SemiRealistic = 2,
    }

    /// <summary>
    /// Quality target for performance budgeting.
    /// </summary>
    public enum AutoSetupQuality
    {
        High = 0,
        Standard = 1,
        Mobile = 2,
    }

    // ===== SetupRecord =====

    /// <summary>
    /// Records the auto-setup state so Stage 2/3 can reference it.
    /// Stored as a JSON string in the material's custom data via EditorPrefs (keyed by material instance ID).
    /// </summary>
    [Serializable]
    public class AutoSetupRecord
    {
        public AutoSetupRole role;
        public AutoSetupLook look;
        public AutoSetupQuality quality;
        public long timestamp;

        /// <summary>Feature keywords that were enabled by Stage 1.</summary>
        public List<string> stage1EnabledKeywords = new List<string>();

        /// <summary>Feature keywords enabled by Stage 2 role-aware toggle.</summary>
        public List<string> stage2EnabledKeywords = new List<string>();

        /// <summary>Property values set by auto-setup (name → float value).</summary>
        public SerializableDictionary autoValues = new SerializableDictionary();

        public static string GetPrefsKey(Material mat)
        {
            if (mat == null) return null;
            string path = UnityEditor.AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(path))
                return $"NataneAutoSetup_{mat.GetInstanceID()}";
            return $"NataneAutoSetup_{path}";
        }

        public static AutoSetupRecord Load(Material mat)
        {
            string key = GetPrefsKey(mat);
            if (key == null) return null;
            string json = UnityEditor.EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<AutoSetupRecord>(json); }
            catch { return null; }
        }

        public void Save(Material mat)
        {
            string key = GetPrefsKey(mat);
            if (key == null) return;
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            UnityEditor.EditorPrefs.SetString(key, JsonUtility.ToJson(this));
        }

        public void RecordAutoFloat(string propName, float value)
        {
            autoValues.Set(propName, value);
        }

        public bool TryGetAutoFloat(string propName, out float value)
        {
            return autoValues.TryGet(propName, out value);
        }
    }

    /// <summary>
    /// Simple serializable string→float dictionary for JsonUtility compatibility.
    /// </summary>
    [Serializable]
    public class SerializableDictionary
    {
        public List<string> keys = new List<string>();
        public List<float> values = new List<float>();

        public void Set(string key, float value)
        {
            int idx = keys.IndexOf(key);
            if (idx >= 0)
            {
                values[idx] = value;
            }
            else
            {
                keys.Add(key);
                values.Add(value);
            }
        }

        public bool TryGet(string key, out float value)
        {
            int idx = keys.IndexOf(key);
            if (idx >= 0 && idx < values.Count)
            {
                value = values[idx];
                return true;
            }
            value = 0f;
            return false;
        }
    }

    // ===== Profile Table =====

    /// <summary>
    /// A single setup profile — the set of keywords and parameters for a Role × Look combination.
    /// </summary>
    public struct AutoSetupProfile
    {
        /// <summary>Which base style method to invoke (maps to existing ApplyXxxStyle).</summary>
        public AutoSetupLook baseStyle;

        /// <summary>Keywords to enable for this profile.</summary>
        public string[] enableKeywords;

        /// <summary>Float property overrides (name, value).</summary>
        public (string name, float value)[] floatOverrides;

        /// <summary>Color property overrides (name, color).</summary>
        public (string name, Color value)[] colorOverrides;

        /// <summary>Whether SDF should be auto-generated.</summary>
        public bool generateSDF;

        /// <summary>Whether smooth normals should be auto-baked.</summary>
        public bool bakeSmoothNormals;
    }

    /// <summary>
    /// Static lookup table for 15 Role × Look profiles.
    /// </summary>
    public static class NataneAutoSetupProfiles
    {
        private static readonly Dictionary<(AutoSetupRole, AutoSetupLook), AutoSetupProfile> Table
            = new Dictionary<(AutoSetupRole, AutoSetupLook), AutoSetupProfile>();

        static NataneAutoSetupProfiles()
        {
            BuildTable();
        }

        public static AutoSetupProfile Get(AutoSetupRole role, AutoSetupLook look)
        {
            if (Table.TryGetValue((role, look), out var profile))
                return profile;
            // Fallback to Anime look
            if (Table.TryGetValue((role, AutoSetupLook.Anime), out profile))
                return profile;
            return default;
        }

        private static void BuildTable()
        {
            // ===== Face =====
            Table[(AutoSetupRole.Face, AutoSetupLook.Anime)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.Anime,
                enableKeywords = new[] { "_SSS", "_SDF_MAP" },
                floatOverrides = new[]
                {
                    ("_SSSIntensity", 0.6f), ("_SSSPower", 2.0f), ("_SSSDistortion", 0.3f),
                    ("_NormalFlattenY", 0.2f),
                    ("_ShadowSteps", 2f), ("_ShadowSharpness", 0.03f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = true,
                bakeSmoothNormals = false,
            };

            Table[(AutoSetupRole.Face, AutoSetupLook.GameCharacter)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.GameCharacter,
                enableKeywords = new[] { "_SSS", "_SDF_MAP" },
                floatOverrides = new[]
                {
                    ("_SSSIntensity", 0.6f), ("_SSSPower", 2.0f), ("_SSSDistortion", 0.3f),
                    ("_NormalFlattenY", 0.2f),
                    ("_RimLight", 0f), ("_Specular", 0f), ("_Outline", 0f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = true,
                bakeSmoothNormals = false,
            };

            Table[(AutoSetupRole.Face, AutoSetupLook.SemiRealistic)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.SemiRealistic,
                enableKeywords = new[] { "_SSS", "_SDF_MAP", "_SPECULAR" },
                floatOverrides = new[]
                {
                    ("_SSSIntensity", 0.5f), ("_SSSPower", 2.5f), ("_SSSDistortion", 0.2f),
                    ("_SpecularSize", 0.08f), ("_SpecularSoftness", 0.15f), ("_SpecularIntensity", 0.5f),
                    ("_NormalFlattenY", 0.15f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = true,
                bakeSmoothNormals = false,
            };

            // ===== Hair =====
            Table[(AutoSetupRole.Hair, AutoSetupLook.Anime)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.Anime,
                enableKeywords = new[] { "_HAIR_SPECULAR", "_OUTLINE", "_RIM_LIGHT" },
                floatOverrides = new[]
                {
                    ("_HairSpecShift1", -0.1f), ("_HairSpecShift2", 0.1f),
                    ("_HairSpecWidth1", 40f), ("_HairSpecIntensity", 1.5f),
                    ("_OutlineWidth", 0.06f), ("_OutlineTexColorBlend", 0.9f),
                    ("_RimPower", 2.0f), ("_RimIntensity", 2.0f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = true,
            };

            Table[(AutoSetupRole.Hair, AutoSetupLook.GameCharacter)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.GameCharacter,
                enableKeywords = new[] { "_HAIR_SPECULAR", "_OUTLINE", "_RIM_LIGHT" },
                floatOverrides = new[]
                {
                    ("_HairSpecShift1", -0.1f), ("_HairSpecShift2", 0.1f),
                    ("_HairSpecWidth1", 40f), ("_HairSpecIntensity", 1.5f),
                    ("_OutlineWidth", 0.06f), ("_OutlineTexColorBlend", 0.9f),
                    ("_RimPower", 2.0f), ("_RimIntensity", 2.0f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = true,
            };

            Table[(AutoSetupRole.Hair, AutoSetupLook.SemiRealistic)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.SemiRealistic,
                enableKeywords = new[] { "_HAIR_SPECULAR", "_OUTLINE", "_RIM_LIGHT" },
                floatOverrides = new[]
                {
                    ("_HairSpecShift1", -0.1f), ("_HairSpecShift2", 0.1f),
                    ("_HairSpecWidth1", 64f), ("_HairSpecIntensity", 1.2f),
                    ("_OutlineWidth", 0.04f), ("_OutlineTexColorBlend", 0.9f),
                    ("_RimPower", 2.5f), ("_RimIntensity", 1.5f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = true,
            };

            // ===== Clothing =====
            Table[(AutoSetupRole.Clothing, AutoSetupLook.Anime)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.Anime,
                enableKeywords = new[] { "_OUTLINE", "_SPECULAR", "_USE_NORMAL_MAP" },
                floatOverrides = new[]
                {
                    ("_OutlineWidth", 0.08f), ("_OutlineTexColorBlend", 0.8f),
                    ("_SpecularSize", 0.06f), ("_SpecularSoftness", 0.1f), ("_SpecularIntensity", 0.8f),
                    ("_NormalFlattenY", 0.05f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = true,
            };

            Table[(AutoSetupRole.Clothing, AutoSetupLook.GameCharacter)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.GameCharacter,
                enableKeywords = new[] { "_OUTLINE", "_SPECULAR", "_USE_NORMAL_MAP", "_RIM_LIGHT" },
                floatOverrides = new[]
                {
                    ("_OutlineWidth", 0.08f), ("_OutlineTexColorBlend", 0.8f),
                    ("_SpecularSize", 0.06f), ("_SpecularSoftness", 0.1f), ("_SpecularIntensity", 0.8f),
                    ("_RimPower", 2.5f), ("_RimIntensity", 1.0f),
                    ("_NormalFlattenY", 0.05f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = true,
            };

            Table[(AutoSetupRole.Clothing, AutoSetupLook.SemiRealistic)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.SemiRealistic,
                enableKeywords = new[] { "_OUTLINE", "_SPECULAR", "_USE_NORMAL_MAP", "_RIM_LIGHT" },
                floatOverrides = new[]
                {
                    ("_OutlineWidth", 0.06f), ("_OutlineTexColorBlend", 0.8f),
                    ("_SpecularSize", 0.06f), ("_SpecularSoftness", 0.1f), ("_SpecularIntensity", 0.8f),
                    ("_RimPower", 3.0f), ("_RimIntensity", 0.8f),
                    ("_NormalFlattenY", 0.05f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = true,
            };

            // ===== Eye =====
            Table[(AutoSetupRole.Eye, AutoSetupLook.Anime)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.Anime,
                enableKeywords = new[] { "_PARALLAX", "_EMISSION", "_MAT_CAP" },
                floatOverrides = new[]
                {
                    ("_ParallaxScale", 0.02f), ("_EyeDepth", 0.15f),
                    ("_EmissionGlow", 0.3f),
                    ("_MatCapIntensity", 0.4f), ("_MatCapBlendMode", 1f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = false,
            };

            Table[(AutoSetupRole.Eye, AutoSetupLook.GameCharacter)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.GameCharacter,
                enableKeywords = new[] { "_PARALLAX", "_EMISSION", "_MAT_CAP" },
                floatOverrides = new[]
                {
                    ("_ParallaxScale", 0.02f), ("_EyeDepth", 0.15f),
                    ("_EmissionGlow", 0.3f),
                    ("_MatCapIntensity", 0.4f), ("_MatCapBlendMode", 1f),
                    ("_RimLight", 0f), ("_Specular", 0f), ("_Outline", 0f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = false,
            };

            Table[(AutoSetupRole.Eye, AutoSetupLook.SemiRealistic)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.SemiRealistic,
                enableKeywords = new[] { "_PARALLAX", "_EMISSION", "_MAT_CAP", "_SPECULAR" },
                floatOverrides = new[]
                {
                    ("_ParallaxScale", 0.02f), ("_EyeDepth", 0.15f),
                    ("_EmissionGlow", 0.3f),
                    ("_MatCapIntensity", 0.4f), ("_MatCapBlendMode", 1f),
                    ("_SpecularSize", 0.15f), ("_SpecularSoftness", 0.05f), ("_SpecularIntensity", 2.0f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = false,
            };

            // ===== Metal =====
            Table[(AutoSetupRole.Metal, AutoSetupLook.Anime)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.Anime,
                enableKeywords = new[] { "_REFLECTION", "_SPECULAR", "_MAT_CAP" },
                floatOverrides = new[]
                {
                    ("_ReflectionIntensity", 0.6f), ("_ReflectionSmoothness", 0.8f), ("_Metallic", 0.9f),
                    ("_SpecularSize", 0.12f), ("_SpecularSoftness", 0.03f), ("_SpecularIntensity", 2.5f),
                    ("_MatCapIntensity", 0.7f), ("_MatCapBlendMode", 1f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = false,
            };

            Table[(AutoSetupRole.Metal, AutoSetupLook.GameCharacter)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.GameCharacter,
                enableKeywords = new[] { "_REFLECTION", "_SPECULAR", "_MAT_CAP", "_RIM_LIGHT", "_OUTLINE" },
                floatOverrides = new[]
                {
                    ("_ReflectionIntensity", 0.6f), ("_ReflectionSmoothness", 0.8f), ("_Metallic", 0.9f),
                    ("_SpecularSize", 0.12f), ("_SpecularSoftness", 0.03f), ("_SpecularIntensity", 2.5f),
                    ("_MatCapIntensity", 0.7f), ("_MatCapBlendMode", 1f),
                    ("_RimPower", 2.0f), ("_RimIntensity", 1.5f),
                    ("_OutlineWidth", 0.04f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = false,
            };

            Table[(AutoSetupRole.Metal, AutoSetupLook.SemiRealistic)] = new AutoSetupProfile
            {
                baseStyle = AutoSetupLook.SemiRealistic,
                enableKeywords = new[] { "_REFLECTION", "_SPECULAR", "_MAT_CAP", "_RIM_LIGHT" },
                floatOverrides = new[]
                {
                    ("_ReflectionIntensity", 0.7f), ("_ReflectionSmoothness", 0.85f), ("_Metallic", 0.95f),
                    ("_SpecularSize", 0.12f), ("_SpecularSoftness", 0.03f), ("_SpecularIntensity", 2.5f),
                    ("_MatCapIntensity", 0.5f), ("_MatCapBlendMode", 1f),
                    ("_RimPower", 2.0f), ("_RimIntensity", 1.5f),
                },
                colorOverrides = Array.Empty<(string, Color)>(),
                generateSDF = false,
                bakeSmoothNormals = false,
            };
        }
    }
}
