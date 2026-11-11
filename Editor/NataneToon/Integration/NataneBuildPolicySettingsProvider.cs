using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    internal static class NataneBuildPolicySettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Natane Toon/Scalability", SettingsScope.Project)
            {
                label = "Natane Toon Scalability",
                guiHandler = _ => DrawSettingsGui()
            };
        }

        private static void DrawSettingsGui()
        {
            NataneBuildPolicySettings settings = NataneBuildPolicySettings.instance;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build Policy", EditorStyles.boldLabel);
            settings.BuildMode = (NataneBuildMode)EditorGUILayout.EnumPopup("Build Mode", settings.BuildMode);
            settings.AutoIndexInEditor = EditorGUILayout.Toggle("Auto Index In Editor", settings.AutoIndexInEditor);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor UX", EditorStyles.boldLabel);
            settings.PageSize = EditorGUILayout.IntField("Page Size", settings.PageSize);
            settings.IssueSampleLimit = EditorGUILayout.IntField("Issue Sample Limit", settings.IssueSampleLimit);
            settings.EditorUpdateBudgetMs = EditorGUILayout.IntSlider("Index Update Budget (ms)", settings.EditorUpdateBudgetMs, 1, 50);
            settings.FullRebuildDirtyThreshold = EditorGUILayout.IntField("Full Rebuild Threshold", settings.FullRebuildDirtyThreshold);

            EditorGUILayout.Space();
            DrawFolderList("Indexed Material Folders", settings.IndexedMaterialFolders, value => ReplaceFolders(settings.IndexedMaterialFolders, value, true));
            DrawFolderList("Indexed Prefab Folders", settings.IndexedPrefabFolders, value => ReplaceFolders(settings.IndexedPrefabFolders, value, false));

            EditorGUILayout.Space();
            DrawBuildOptimizationSection(settings);

            EditorGUILayout.Space();
            if (GUILayout.Button(L("設定を保存", "Save Settings"), GUILayout.Height(28f)))
            {
                settings.SaveSettings();
            }
        }

        private static void DrawBuildOptimizationSection(NataneBuildPolicySettings settings)
        {
            EditorGUILayout.LabelField(L("ビルド最適化", "Build Optimization"), EditorStyles.boldLabel);

            settings.OptimizationMode = (NataneOptimizationMode)EditorGUILayout.EnumPopup(
                L("最適化モード", "Optimization Mode"), settings.OptimizationMode);
            if (settings.OptimizationMode == NataneOptimizationMode.Aggressive)
            {
                EditorGUILayout.HelpBox(
                    L("Aggressive は現段階では未実装のため Safe 相当で動作します。実データでの動作確認が済むまで使用は推奨しません。",
                      "Aggressive is not yet implemented; it currently behaves like Safe. Not recommended until validated."),
                    MessageType.Warning);
            }

            settings.StrictFailurePolicy = (NataneStrictFailurePolicy)EditorGUILayout.EnumPopup(
                L("失敗時ポリシー (Snapshot/Registry)", "Failure Policy (Snapshot/Registry)"), settings.StrictFailurePolicy);
            settings.UnknownKeywordPolicy = (NataneUnknownKeywordPolicy)EditorGUILayout.EnumPopup(
                L("未知キーワード方針", "Unknown Keyword Policy"), settings.UnknownKeywordPolicy);

            EditorGUILayout.Space();
            settings.VariantStrippingEnabled = EditorGUILayout.Toggle(
                L("バリアントストリップ (差分式)", "Variant Stripping (diff)"), settings.VariantStrippingEnabled);
            settings.LegacyExactSetStrippingEnabled = EditorGUILayout.Toggle(
                L("レガシー完全一致式ストリップ", "Legacy Exact-Set Stripping"), settings.LegacyExactSetStrippingEnabled);

            EditorGUILayout.Space();
            settings.BuildPrewarmEnabled = EditorGUILayout.Toggle(
                L("ビルド時プリウォーム", "Build Prewarm"), settings.BuildPrewarmEnabled);
            EditorGUILayout.HelpBox(
                L("ビルド時プリウォームは VRChat アバターアップロードには効きません（Editor 内の WarmUp のみ）。",
                  "Build prewarm does not affect VRChat avatar uploads (Editor-only WarmUp)."),
                MessageType.None);
            settings.PrewarmAutoFindVariants = EditorGUILayout.Toggle(
                L("手動プリウォーム時にバリアントを自動検出", "Auto-Find Variants (manual prewarm only)"), settings.PrewarmAutoFindVariants);

            EditorGUILayout.Space();
            settings.BuildTextureConsolidation = EditorGUILayout.Toggle(
                L("ビルド時テクスチャ統合 (VRChat)", "Build Texture Consolidation (VRChat)"), settings.BuildTextureConsolidation);

            EditorGUILayout.Space();
            settings.HlslFeatureGuardMode = (NataneHlslFeatureGuardMode)EditorGUILayout.EnumPopup(
                L("HLSL フィーチャーガード", "HLSL Feature Guard"), settings.HlslFeatureGuardMode);
            if (settings.HlslFeatureGuardMode == NataneHlslFeatureGuardMode.Advanced)
            {
                EditorGUILayout.HelpBox(
                    L("Advanced: ビルド前に HLSL を書換えて未使用機能をソースレベルで無効化します。全 Natane シェーダーの再インポートが発生します（キャッシュ一致時はスキップ）。" +
                      "通常は統合ストリッパー(Safe)が同等以上の削減を行うため Off を推奨します。",
                      "Advanced: rewrites HLSL before builds to disable unused features at the source level. Reimports all Natane shaders (skipped on cache hit). " +
                      "The unified stripper (Safe) usually achieves equal or better reduction, so Off is recommended."),
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    L("Off（推奨）: HLSL 書換えも全再インポートも行いません。統合ストリッパー(Safe)が上位互換で削減を担います。",
                      "Off (recommended): no HLSL rewrite or full reimport. The unified stripper (Safe) handles reduction and is a superset."),
                    MessageType.None);
            }
            EditorGUILayout.HelpBox(
                L("Off / Advanced の削減効果は Tools > Natane > ビルド最適化 > HLSL方式との比較レポート で確認できます。",
                  "Compare Off vs Advanced reduction via Tools > Natane > Build Optimization > Compare vs HLSL Method."),
                MessageType.None);

            EditorGUILayout.Space();
            DrawStringList(L("常時保持キーワード", "Always Keep Keywords"), settings.AlwaysKeepKeywords);
            DrawGuidObjectList<Shader>(L("常時保持シェーダー", "Always Keep Shaders"), settings.AlwaysKeepShaderGuids);
            DrawGuidObjectList<Material>(L("常時保持マテリアル", "Always Keep Materials"), settings.AlwaysKeepMaterialGuids);
            DrawGuidObjectList<ShaderVariantCollection>(L("保持する Shader Variant Collection", "Kept Shader Variant Collections"), settings.ShaderVariantCollectionGuids);
            DrawStringList(L("Runtime 動的キーワード", "Runtime Dynamic Keywords"), settings.RuntimeDynamicKeywords);
        }

        private static void DrawStringList(string label, List<string> list)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string edited = EditorGUILayout.TextField(list[i]);
                if (edited != list[i])
                {
                    list[i] = edited;
                    NataneBuildPolicySettings.instance.SaveSettings();
                }
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    list.RemoveAt(i);
                    NataneBuildPolicySettings.instance.SaveSettings();
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(L("追加", "Add"), GUILayout.Width(120f)))
            {
                list.Add(string.Empty);
                NataneBuildPolicySettings.instance.SaveSettings();
            }
        }

        // ObjectField で選択させ、GUID を保存する（GUID がビルド結果の正）。
        private static void DrawGuidObjectList<T>(string label, List<string> guids) where T : Object
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            for (int i = 0; i < guids.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
                T next = (T)EditorGUILayout.ObjectField(current, typeof(T), false);
                if (next != current)
                {
                    guids[i] = next == null
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(next));
                    NataneBuildPolicySettings.instance.SaveSettings();
                }
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    guids.RemoveAt(i);
                    NataneBuildPolicySettings.instance.SaveSettings();
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(L("追加", "Add"), GUILayout.Width(120f)))
            {
                guids.Add(string.Empty);
                NataneBuildPolicySettings.instance.SaveSettings();
            }
        }

        private static void DrawFolderList(string label, IReadOnlyList<string> folders, System.Action<List<string>> assign)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            List<string> editable = new List<string>(folders);

            for (int i = 0; i < editable.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                editable[i] = EditorGUILayout.TextField(editable[i]);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    editable.RemoveAt(i);
                    assign(editable);
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Folder"))
            {
                editable.Add("Assets");
                assign(editable);
                return;
            }

            assign(editable);
        }

        private static void ReplaceFolders(IReadOnlyList<string> current, List<string> next, bool materialFolders)
        {
            NataneBuildPolicySettings settings = NataneBuildPolicySettings.instance;
            var target = materialFolders ? new List<string>(settings.IndexedMaterialFolders) : new List<string>(settings.IndexedPrefabFolders);
            bool changed = target.Count != next.Count;
            if (!changed)
            {
                for (int i = 0; i < target.Count; i++)
                {
                    if (target[i] != next[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                return;
            }

            var serializedObject = new SerializedObject(settings);
            var propertyName = materialFolders ? "indexedMaterialFolders" : "indexedPrefabFolders";
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = next.Count;
            for (int i = 0; i < next.Count; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = next[i];
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            settings.SaveSettings();
        }
    }
}
