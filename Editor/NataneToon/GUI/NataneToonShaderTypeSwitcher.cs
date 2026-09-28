using UnityEngine;
using UnityEditor;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    /// <summary>
    /// Shader type definitions and switching utility.
    /// </summary>
    public enum ShaderType
    {
        Toon = 0,
        Eye = 1,
        Wirelight = 2,
        StandardToon = 3
    }

    public static class NataneToonShaderTypeSwitcher
    {
        private static readonly string[] ToonShaderNames = new string[]
        {
            "Natane/Toon Shader",
            "Natane/Toon Shader (ScreenEdge Split)",
            "Natane/Toon Shader (Cutout)",
            "Natane/Toon Shader (Transparent)",
            "Natane/Toon Shader (Fur)",
            "Natane/Toon Shader (Background)",
            // Lite variants (no GrabPass)
            "Natane/Toon Shader (Lite)",
            "Natane/Toon Shader (Cutout Lite)",
            "Natane/Toon Shader (Transparent Lite)",
            "Natane/Toon Shader (Fur Lite)",
            // Tessellation variants (hull/domain stages)
            "Natane/Toon Shader (Tessellation)",
            "Natane/Toon Shader (Cutout Tessellation)",
            "Natane/Toon Shader (Transparent Tessellation)"
        };

        private const string EyeShaderName = "Natane/Eye";
        private const string WirelightShaderName = "Natane/Toon Shader Wirelight";

        // Display names (bilingual)
        private static string[] ShaderTypeLabels => new string[]
        {
            L("トゥーン", "Toon"),
            L("瞳", "Eye"),
            L("ワイヤーライト", "Wirelight"),
            L("lilToon互換ベース", "lilToon Compatibility Base")
        };

        private static readonly ShaderType[] ShaderTypeDisplayOrder =
        {
            ShaderType.Toon,
            ShaderType.Eye,
            ShaderType.Wirelight,
            ShaderType.StandardToon
        };

        /// <summary>
        /// Detect the shader type used by a material.
        /// </summary>
        public static ShaderType DetectShaderType(Material material)
        {
            if (material == null || material.shader == null)
                return ShaderType.Toon;

            string shaderName = material.shader.name;

            // Toon variants (check for StandardToon mode first)
            for (int i = 0; i < ToonShaderNames.Length; i++)
            {
                if (shaderName == ToonShaderNames[i])
                {
                    // StandardToon uses the same shader but only the 2.x shading mode range.
                    if (material.HasProperty("_ShadingMode"))
                    {
                        float shadingModeValue = material.GetFloat("_ShadingMode");
                        if (shadingModeValue >= 1.5f && shadingModeValue < 2.5f)
                            return ShaderType.StandardToon;
                    }
                    return ShaderType.Toon;
                }
            }

            if (shaderName == EyeShaderName)
                return ShaderType.Eye;

            if (shaderName == WirelightShaderName)
                return ShaderType.Wirelight;


            // Fallback: treat unknown shaders as Toon.
            return ShaderType.Toon;
        }

        /// <summary>
        /// Get the default shader name for the given shader type.
        /// </summary>
        public static string GetDefaultShaderName(ShaderType type)
        {
            switch (type)
            {
                case ShaderType.Toon: return ToonShaderNames[0];
                case ShaderType.Eye: return EyeShaderName;
                case ShaderType.Wirelight: return WirelightShaderName;
                case ShaderType.StandardToon: return ToonShaderNames[0]; // same shader as Toon
                default: return ToonShaderNames[0];
            }
        }

        /// <summary>
        /// Change the shader type with Undo support.
        /// Returns true on success, false on failure (with error dialog shown).
        /// </summary>
        public static bool SetShaderType(Material material, ShaderType type, MaterialEditor editor)
        {
            if (material == null)
            {
                NataneToonErrorDialog.ShowNullMaterialError(L("Change Shader Type", "Change Shader Type"));
                return false;
            }

            Undo.RecordObject(material, "Change Shader Type");

            if (type == ShaderType.StandardToon)
            {
                // StandardToon uses the same Toon shader but with _ShadingMode = 2
                string newShaderName = ToonShaderNames[0];
                Shader newShader = Shader.Find(newShaderName);
                if (newShader == null)
                {
                    Debug.LogError($"[NataneToonShaderTypeSwitcher] Shader not found: {newShaderName}");
                    NataneToonErrorDialog.ShowShaderNotFoundError(newShaderName);
                    return false;
                }
                material.shader = newShader;
                material.SetFloat("_ShadingMode", 2.0f);
            }
            else
            {
                string newShaderName = GetDefaultShaderName(type);
                Shader newShader = Shader.Find(newShaderName);

                if (newShader == null)
                {
                    Debug.LogError($"[NataneToonShaderTypeSwitcher] Shader not found: {newShaderName}");
                    NataneToonErrorDialog.ShowShaderNotFoundError(newShaderName);
                    return false;
                }

                if (material.shader == newShader && type != ShaderType.Toon) return true;

                material.shader = newShader;

                // Toon type: reset _ShadingMode if was StandardToon
                if (type == ShaderType.Toon && material.HasProperty("_ShadingMode"))
                {
                    float shadingModeValue = material.GetFloat("_ShadingMode");
                    if (shadingModeValue >= 1.5f && shadingModeValue < 2.5f)
                    {
                        material.SetFloat("_ShadingMode", 0.0f);
                    }
                }
            }

            EditorUtility.SetDirty(material);

            if (editor != null)
            {
                editor.Repaint();
            }

            return true;
        }

        /// <summary>
        /// Draw the shader type dropdown.
        /// Returns true when the shader type changed and the caller should exit early.
        /// </summary>
        public static bool DrawShaderTypeDropdown(Material material, MaterialEditor editor, out bool shouldReturn)
        {
            shouldReturn = false;
            ShaderType currentType = DetectShaderType(material);

            EditorGUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            int currentIndex = Mathf.Max(0, System.Array.IndexOf(ShaderTypeDisplayOrder, currentType));
            int nextIndex = EditorGUILayout.Popup(L("シェーダータイプ", "Shader Type"), currentIndex, ShaderTypeLabels);
            ShaderType newType = ShaderTypeDisplayOrder[Mathf.Clamp(nextIndex, 0, ShaderTypeDisplayOrder.Length - 1)];

            if (EditorGUI.EndChangeCheck() && newType != currentType)
            {
                if (EditorUtility.DisplayDialog(
                    L("シェーダータイプ変更", "Change Shader Type"),
                    L("シェーダータイプを変更すると、一部の現在設定が失われることがあります。\n続行しますか？", "Changing the shader type may cause some current settings to be lost.\nContinue?"),
                    L("変更", "Change"),
                    L("キャンセル", "Cancel")))
                {
                    bool success = SetShaderType(material, newType, editor);
                    if (success)
                    {
                        shouldReturn = true;
                        return true;
                    }
                }
            }

            return currentType != ShaderType.Toon && currentType != ShaderType.StandardToon;
        }
    }
}
