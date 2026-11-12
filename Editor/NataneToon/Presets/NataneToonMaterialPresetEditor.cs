using UnityEngine;
using UnityEditor;
using NataneToon.MaterialSystem;

namespace NataneToon.Editor
{
    /// <summary>
    /// Editor utilities for NataneToonMaterialPreset
    /// Handles inspector UI state updates when presets are applied
    /// </summary>
    public static class NataneToonMaterialPresetEditor
    {
        /// <summary>
        /// Apply preset to material and update inspector UI state
        /// </summary>
        public static void ApplyPresetWithUIUpdate(NataneToonMaterialPreset preset, Material material)
        {
            if (preset == null || material == null) return;

            // Apply preset to material
            preset.ApplyToMaterial(material);

            // Update inspector UI foldout states based on preset
            UpdateInspectorUIState(material, preset.parameters);

            // Force inspector to refresh completely
            ForceInspectorRefresh(material);
        }

        /// <summary>
        /// Apply preset to material with MaterialEditor reference for better refresh
        /// </summary>
        public static void ApplyPresetWithUIUpdate(NataneToonMaterialPreset preset, Material material, MaterialEditor materialEditor)
        {
            if (preset == null || material == null) return;

            // Apply preset to material
            preset.ApplyToMaterial(material);

            // Update inspector UI foldout states based on preset
            UpdateInspectorUIState(material, preset.parameters);

            // Force inspector to refresh with MaterialEditor
            ForceInspectorRefresh(material, materialEditor);
        }

        /// <summary>
        /// Force a complete refresh of the inspector
        /// </summary>
        private static void ForceInspectorRefresh(Material material)
        {
            EditorUtility.SetDirty(material);

            var currentSelection = Selection.activeObject;
            Selection.activeObject = null;
            EditorApplication.delayCall += () =>
            {
                Selection.activeObject = currentSelection;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            };
        }

        /// <summary>
        /// Force a complete refresh of the inspector with MaterialEditor reference
        /// </summary>
        private static void ForceInspectorRefresh(Material material, MaterialEditor materialEditor)
        {
            EditorUtility.SetDirty(material);

            if (materialEditor != null)
            {
                var serializedObject = new SerializedObject(material);
                serializedObject.UpdateIfRequiredOrScript();

                materialEditor.Repaint();
                materialEditor.PropertiesChanged();
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// Update inspector UI foldout states based on preset parameters
        /// </summary>
        private static void UpdateInspectorUIState(Material material, MaterialParameterData parameters)
        {
            string matKey = GetMaterialKey(material);

            // Update foldout states based on which features are enabled in the preset
            EditorPrefs.SetBool(matKey + "_ShowSpecular", parameters.useSpecular);
            EditorPrefs.SetBool(matKey + "_ShowRimLight", parameters.useRimLight);
            EditorPrefs.SetBool(matKey + "_ShowSSS", parameters.useSSS);
            EditorPrefs.SetBool(matKey + "_ShowMatCap", parameters.useMatCap);
            EditorPrefs.SetBool(matKey + "_ShowOutline", parameters.useOutline);
            EditorPrefs.SetBool(matKey + "_ShowEmission", parameters.useEmission);
            EditorPrefs.SetBool(matKey + "_ShowEmissionAnimation", parameters.useEmissionAnimation);
            EditorPrefs.SetBool(matKey + "_ShowDissolve", parameters.useDissolve);
            EditorPrefs.SetBool(matKey + "_ShowHueShift", parameters.useHueShift);
            EditorPrefs.SetBool(matKey + "_ShowReflection", parameters.useReflection);
            EditorPrefs.SetBool(matKey + "_ShowEnvRim", parameters.useEnvRim);
            EditorPrefs.SetBool(matKey + "_ShowParallax", parameters.useParallax);
            EditorPrefs.SetBool(matKey + "_ShowRefraction", parameters.useRefraction);

            // Always show basic settings and shading
            EditorPrefs.SetBool(matKey + "_ShowBasic", true);
            EditorPrefs.SetBool(matKey + "_ShowShading", true);

            // Show advanced settings if metallic, smoothness, or rendering settings are customized
            bool showAdvanced = parameters.metallic > 0.01f ||
                               parameters.smoothness > 0.51f ||
                               parameters.renderQueue != 2000 ||
                               parameters.cullMode != 2;
            EditorPrefs.SetBool(matKey + "_ShowAdvanced", showAdvanced);
        }

        /// <summary>
        /// Get a unique key for storing material-specific EditorPrefs
        /// </summary>
        private static string GetMaterialKey(Material material)
        {
            // Use material's instance ID for unique identification
            return $"NataneToon_Material_{NataneToon.Editor.NataneEditorCompat.GetObjectId(material)}";
        }

        /// <summary>
        /// Reset all UI foldout states for a material to default
        /// </summary>
        public static void ResetUIState(Material material)
        {
            if (material == null) return;

            string matKey = GetMaterialKey(material);

            // Reset to default state (only basic settings shown)
            EditorPrefs.SetBool(matKey + "_ShowBasic", true);
            EditorPrefs.SetBool(matKey + "_ShowShading", true);
            EditorPrefs.SetBool(matKey + "_ShowSpecular", false);
            EditorPrefs.SetBool(matKey + "_ShowRimLight", false);
            EditorPrefs.SetBool(matKey + "_ShowSSS", false);
            EditorPrefs.SetBool(matKey + "_ShowMatCap", false);
            EditorPrefs.SetBool(matKey + "_ShowOutline", false);
            EditorPrefs.SetBool(matKey + "_ShowEmission", false);
            EditorPrefs.SetBool(matKey + "_ShowEmissionAnimation", false);
            EditorPrefs.SetBool(matKey + "_ShowDissolve", false);
            EditorPrefs.SetBool(matKey + "_ShowHueShift", false);
            EditorPrefs.SetBool(matKey + "_ShowReflection", false);
            EditorPrefs.SetBool(matKey + "_ShowEnvRim", false);
            EditorPrefs.SetBool(matKey + "_ShowParallax", false);
            EditorPrefs.SetBool(matKey + "_ShowRefraction", false);
            EditorPrefs.SetBool(matKey + "_ShowAdvanced", false);

            // Repaint all inspectors
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// Expand all UI sections for a material
        /// </summary>
        public static void ExpandAllSections(Material material)
        {
            if (material == null) return;

            string matKey = GetMaterialKey(material);

            EditorPrefs.SetBool(matKey + "_ShowBasic", true);
            EditorPrefs.SetBool(matKey + "_ShowShading", true);
            EditorPrefs.SetBool(matKey + "_ShowSpecular", true);
            EditorPrefs.SetBool(matKey + "_ShowRimLight", true);
            EditorPrefs.SetBool(matKey + "_ShowSSS", true);
            EditorPrefs.SetBool(matKey + "_ShowMatCap", true);
            EditorPrefs.SetBool(matKey + "_ShowOutline", true);
            EditorPrefs.SetBool(matKey + "_ShowEmission", true);
            EditorPrefs.SetBool(matKey + "_ShowEmissionAnimation", true);
            EditorPrefs.SetBool(matKey + "_ShowDissolve", true);
            EditorPrefs.SetBool(matKey + "_ShowHueShift", true);
            EditorPrefs.SetBool(matKey + "_ShowReflection", true);
            EditorPrefs.SetBool(matKey + "_ShowEnvRim", true);
            EditorPrefs.SetBool(matKey + "_ShowParallax", true);
            EditorPrefs.SetBool(matKey + "_ShowRefraction", true);
            EditorPrefs.SetBool(matKey + "_ShowAdvanced", true);

            // Repaint all inspectors
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// Collapse all UI sections for a material
        /// </summary>
        public static void CollapseAllSections(Material material)
        {
            if (material == null) return;

            string matKey = GetMaterialKey(material);

            EditorPrefs.SetBool(matKey + "_ShowBasic", false);
            EditorPrefs.SetBool(matKey + "_ShowShading", false);
            EditorPrefs.SetBool(matKey + "_ShowSpecular", false);
            EditorPrefs.SetBool(matKey + "_ShowRimLight", false);
            EditorPrefs.SetBool(matKey + "_ShowSSS", false);
            EditorPrefs.SetBool(matKey + "_ShowMatCap", false);
            EditorPrefs.SetBool(matKey + "_ShowOutline", false);
            EditorPrefs.SetBool(matKey + "_ShowEmission", false);
            EditorPrefs.SetBool(matKey + "_ShowEmissionAnimation", false);
            EditorPrefs.SetBool(matKey + "_ShowDissolve", false);
            EditorPrefs.SetBool(matKey + "_ShowHueShift", false);
            EditorPrefs.SetBool(matKey + "_ShowReflection", false);
            EditorPrefs.SetBool(matKey + "_ShowEnvRim", false);
            EditorPrefs.SetBool(matKey + "_ShowParallax", false);
            EditorPrefs.SetBool(matKey + "_ShowRefraction", false);
            EditorPrefs.SetBool(matKey + "_ShowAdvanced", false);

            // Repaint all inspectors
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// Get material-specific EditorPrefs key
        /// </summary>
        public static string GetMaterialPrefsKey(Material material, string suffix)
        {
            return GetMaterialKey(material) + "_" + suffix;
        }
    }
}
