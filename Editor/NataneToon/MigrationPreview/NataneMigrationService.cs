using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Migration プレビュー/適用のコアサービス（UI 非依存・検証可能）。
    /// 提案（BuildMigrationPlan・純関数）→ 確認 → 適用（Undo + schema 付き JSON 履歴）→ 再検査 の流れ。
    /// Shader 更新検出だけで Material/Clip を自動保存しない。SaveAssets は適用/ロールバック確定時のみ 1 回。
    /// 既存の lilToon 移行（Editor/NataneToon/Migration・別 asmdef）とは別系統。
    /// </summary>
    public static class NataneMigrationService
    {
        private const string HistoryFileName = "migration-history-v1.json";
        private const int HistorySchemaVersion = 1;

        private static string ProjectRootPath => Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");

        internal static string HistoryPath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Migration", HistoryFileName);

        // ============================================================
        //  収集（GUID ベース・ボタン押下時のみ）
        // ============================================================

        /// <summary>
        /// 移行対象 Material を集める。AssetIndex（Natane マテリアル）を優先し、
        /// index が空なら FindAssets へフォールバックする。定義の oldShaderName 一致分も加える。
        /// </summary>
        public static List<Material> CollectTargetMaterials(IReadOnlyList<NataneMigrationDefinition> definitions)
        {
            var byGuid = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            foreach (MaterialIndexEntry entry in NataneAssetIndexService.EnumerateMaterialEntries(e => e.isNataneShader).ToList())
            {
                Material material = NataneAssetIndexService.LoadMaterial(entry);
                if (material != null && !string.IsNullOrEmpty(entry.guid))
                {
                    byGuid[entry.guid] = material;
                }
            }

            // 定義に含まれる旧 Shader 名（カタログ外もあり得る）。
            var oldShaderNames = new HashSet<string>(StringComparer.Ordinal);
            if (definitions != null)
            {
                foreach (NataneMigrationDefinition def in definitions)
                {
                    if (!string.IsNullOrEmpty(def.oldShaderName))
                    {
                        oldShaderNames.Add(def.oldShaderName);
                    }
                }
            }

            // index が空、または旧 Shader 名指定がある場合のみ全走査（ボタン押下時のみの明示操作）。
            if (byGuid.Count == 0 || oldShaderNames.Count > 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || byGuid.ContainsKey(guid))
                    {
                        continue;
                    }

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null)
                    {
                        continue;
                    }

                    string shaderName = material.shader.name;
                    if (NataneShaderCatalog.IsNataneShader(shaderName) || oldShaderNames.Contains(shaderName))
                    {
                        byGuid[guid] = material;
                    }
                }
            }

            return byGuid.Values.ToList();
        }

        /// <summary>
        /// 移行対象 AnimationClip を集める。定義の旧 Property を "material._Old" 形式で参照するクリップのみ。
        /// </summary>
        public static List<AnimationClip> CollectTargetClips(IReadOnlyList<NataneMigrationDefinition> definitions)
        {
            var clips = new List<AnimationClip>();
            if (definitions == null)
            {
                return clips;
            }

            var oldProps = new HashSet<string>(
                definitions.Where(d => d.HasPropertyRename).Select(d => "material." + d.oldPropertyName),
                StringComparer.Ordinal);
            if (oldProps.Count == 0)
            {
                return clips;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    continue;
                }

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                if (bindings.Any(b => oldProps.Contains(b.propertyName)))
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        // ============================================================
        //  プランナ（純関数・テスト対象）
        // ============================================================

        /// <summary>
        /// 定義・Material・Clip から変更候補を生成する（副作用なし。読み取りのみ）。
        /// requiresManualReview の候補は included=false（既定除外）で返す。
        /// </summary>
        public static NataneMigrationPlan BuildMigrationPlan(
            IReadOnlyList<NataneMigrationDefinition> definitions,
            IList<Material> materials,
            IList<AnimationClip> clips)
        {
            var plan = new NataneMigrationPlan();
            if (definitions == null || definitions.Count == 0)
            {
                return plan;
            }

            var affectedMaterialGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (materials != null)
            {
                foreach (Material material in materials)
                {
                    if (material == null || material.shader == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(material);
                    string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                    string shaderName = material.shader.name;

                    foreach (NataneMigrationDefinition def in definitions)
                    {
                        // Shader スコープ限定: oldShaderName 指定時はその Shader のみ対象。
                        bool shaderScoped = !string.IsNullOrEmpty(def.oldShaderName);
                        if (shaderScoped && !string.Equals(shaderName, def.oldShaderName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (def.HasShaderRename && string.Equals(shaderName, def.oldShaderName, StringComparison.Ordinal))
                        {
                            AddShaderChange(plan, def, material, guid, path, affectedMaterialGuids);
                        }

                        if (def.HasPropertyRename)
                        {
                            AddPropertyChange(plan, def, material, guid, path, affectedMaterialGuids);
                        }

                        if (def.HasKeywordRename)
                        {
                            AddKeywordChange(plan, def, material, guid, path, affectedMaterialGuids);
                        }
                    }
                }
            }

            if (clips != null)
            {
                foreach (AnimationClip clip in clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    AddClipChanges(plan, definitions, clip);
                }
            }

            // 影響表示のみ（書換え対象外）: 対象 Material を参照する Prefab。
            if (affectedMaterialGuids.Count > 0)
            {
                foreach (PrefabDependencyEntry prefab in NataneAssetIndexService.GetPrefabsUsingMaterialGuids(affectedMaterialGuids))
                {
                    plan.affectedPrefabPaths.Add(prefab.path);
                }
            }

            return plan;
        }

        private static void AddPropertyChange(
            NataneMigrationPlan plan, NataneMigrationDefinition def, Material material,
            string guid, string path, HashSet<string> affected)
        {
            // 旧値: shader が旧プロパティを保持していれば GetFloat、無ければ serialize 済み値を読む。
            if (!TryGetMaterialFloat(material, def.oldPropertyName, out float oldValue))
            {
                return; // 旧値が無ければ移行対象外
            }

            // 新プロパティは現行 shader が宣言している必要がある（Set 可能でないと写せない）。
            if (!material.HasProperty(def.newPropertyName))
            {
                plan.warnings.Add($"{material.name}: 新プロパティ {def.newPropertyName} が Shader に存在しません");
                return;
            }

            float newValue = def.ConvertFloat(oldValue);
            plan.changes.Add(new NataneMigrationChange
            {
                kind = NataneMigrationChangeKind.MaterialProperty,
                assetGuid = guid,
                assetPath = path,
                assetName = material.name,
                oldName = def.oldPropertyName,
                newName = def.newPropertyName,
                hasFloatValue = true,
                oldFloatValue = oldValue,
                newFloatValue = newValue,
                autoApplicable = def.autoApplicable,
                manualReview = def.requiresManualReview,
                included = !def.requiresManualReview,
                note = def.note
            });
            if (!string.IsNullOrEmpty(guid)) affected.Add(guid);
        }

        private static void AddKeywordChange(
            NataneMigrationPlan plan, NataneMigrationDefinition def, Material material,
            string guid, string path, HashSet<string> affected)
        {
            if (Array.IndexOf(material.shaderKeywords, def.oldKeyword) < 0)
            {
                return; // 旧 Keyword が有効でなければ移行不要
            }

            plan.changes.Add(new NataneMigrationChange
            {
                kind = NataneMigrationChangeKind.MaterialKeyword,
                assetGuid = guid,
                assetPath = path,
                assetName = material.name,
                oldName = def.oldKeyword,
                newName = def.newKeyword,
                autoApplicable = def.autoApplicable,
                manualReview = def.requiresManualReview,
                included = !def.requiresManualReview,
                note = def.note
            });
            if (!string.IsNullOrEmpty(guid)) affected.Add(guid);
        }

        private static void AddShaderChange(
            NataneMigrationPlan plan, NataneMigrationDefinition def, Material material,
            string guid, string path, HashSet<string> affected)
        {
            if (Shader.Find(def.newShaderName) == null)
            {
                plan.warnings.Add($"{material.name}: 新 Shader {def.newShaderName} が見つかりません");
                return;
            }

            plan.changes.Add(new NataneMigrationChange
            {
                kind = NataneMigrationChangeKind.MaterialShader,
                assetGuid = guid,
                assetPath = path,
                assetName = material.name,
                oldName = def.oldShaderName,
                newName = def.newShaderName,
                autoApplicable = def.autoApplicable,
                manualReview = def.requiresManualReview,
                included = !def.requiresManualReview,
                note = def.note
            });
            if (!string.IsNullOrEmpty(guid)) affected.Add(guid);
        }

        private static void AddClipChanges(
            NataneMigrationPlan plan, IReadOnlyList<NataneMigrationDefinition> definitions, AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (NataneMigrationDefinition def in definitions)
            {
                if (!def.HasPropertyRename)
                {
                    continue;
                }

                string oldProp = "material." + def.oldPropertyName;
                foreach (EditorCurveBinding binding in bindings)
                {
                    if (!string.Equals(binding.propertyName, oldProp, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    plan.changes.Add(new NataneMigrationChange
                    {
                        kind = NataneMigrationChangeKind.ClipBinding,
                        assetGuid = guid,
                        assetPath = path,
                        assetName = clip.name,
                        oldName = def.oldPropertyName,
                        newName = def.newPropertyName,
                        bindingPath = binding.path,
                        bindingTypeName = binding.type != null ? binding.type.AssemblyQualifiedName : null,
                        autoApplicable = def.autoApplicable,
                        manualReview = def.requiresManualReview,
                        included = !def.requiresManualReview,
                        note = def.note
                    });
                }
            }
        }

        /// <summary>
        /// float プロパティ値を取得する。shader が宣言していれば GetFloat、
        /// そうでなければ .mat の serialize 済み m_Floats から旧値を拾う（Shader 更新後の残存値対応）。
        /// </summary>
        public static bool TryGetMaterialFloat(Material material, string propertyName, out float value)
        {
            value = 0f;
            if (material == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            if (material.HasProperty(propertyName))
            {
                try
                {
                    value = material.GetFloat(propertyName);
                    return true;
                }
                catch
                {
                    // float 以外のプロパティ（Color/Vector 等）は本移行の対象外。
                    return false;
                }
            }

            return TryGetSerializedFloat(material, propertyName, out value);
        }

        private static bool TryGetSerializedFloat(Material material, string propertyName, out float value)
        {
            value = 0f;
            var so = new SerializedObject(material);
            SerializedProperty floats = so.FindProperty("m_SavedProperties.m_Floats");
            if (floats == null)
            {
                return false;
            }

            for (int i = 0; i < floats.arraySize; i++)
            {
                SerializedProperty element = floats.GetArrayElementAtIndex(i);
                SerializedProperty key = element.FindPropertyRelative("first");
                if (key != null && string.Equals(key.stringValue, propertyName, StringComparison.Ordinal))
                {
                    value = element.FindPropertyRelative("second").floatValue;
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        //  適用
        // ============================================================

        /// <summary>
        /// included な変更を適用する。Undo.RecordObject で Material/Clip を書換え、前後値を履歴へ記録し、
        /// 最後に SaveAssets を 1 回だけ実行する。適用後に Update Audit を再実行して簡易サマリを付す。
        /// </summary>
        public static NataneMigrationApplyResult Apply(NataneMigrationPlan plan)
        {
            var result = new NataneMigrationApplyResult();
            if (plan == null)
            {
                return result;
            }

            List<NataneMigrationChange> applicable = plan.changes.Where(c => c != null && c.included).ToList();
            result.skippedCount = plan.changes.Count - applicable.Count;
            if (applicable.Count == 0)
            {
                return result;
            }

            var batch = new NataneMigrationBatch
            {
                batchId = Guid.NewGuid().ToString("N"),
                utcTicks = DateTime.UtcNow.Ticks,
                packageVersion = NataneShaderFeatureRegistry.ShaderCompatibilityVersion,
                migrationVersion = NataneShaderFeatureRegistry.MigrationVersion.ToString()
            };

            var touchedMaterialGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var touchedMaterials = new List<Material>();
            var touchedClips = new List<AnimationClip>();

            foreach (NataneMigrationChange change in applicable)
            {
                try
                {
                    if (change.kind == NataneMigrationChangeKind.ClipBinding)
                    {
                        var clip = LoadClip(change);
                        if (clip == null)
                        {
                            result.errors.Add($"Clip をロードできません: {change.assetPath}");
                            continue;
                        }

                        if (ApplyClipBinding(clip, change, out NataneMigrationRecord clipRecord))
                        {
                            batch.records.Add(clipRecord);
                            result.appliedCount++;
                            if (!touchedClips.Contains(clip)) touchedClips.Add(clip);
                        }
                        else
                        {
                            result.warnings.Add($"{change.assetName}: binding {change.oldName} のカーブが取得できませんでした");
                        }
                        continue;
                    }

                    Material material = LoadMaterial(change);
                    if (material == null)
                    {
                        result.errors.Add($"Material をロードできません: {change.assetPath}");
                        continue;
                    }

                    Undo.RecordObject(material, "Natane Migration");
                    NataneMigrationRecord record = ApplyMaterialChange(material, change);
                    if (record != null)
                    {
                        batch.records.Add(record);
                        result.appliedCount++;
                        EditorUtility.SetDirty(material);
                        if (!touchedMaterials.Contains(material)) touchedMaterials.Add(material);
                        if (!string.IsNullOrEmpty(change.assetGuid)) touchedMaterialGuids.Add(change.assetGuid);
                    }
                }
                catch (Exception ex)
                {
                    result.errors.Add($"{change.assetName}: {ex.Message}");
                }
            }

            result.materialCount = touchedMaterials.Count;
            result.clipCount = touchedClips.Count;

            if (batch.records.Count > 0)
            {
                result.batchId = batch.batchId;
                AppendBatch(batch);
                AssetDatabase.SaveAssets(); // 適用確定時のみ 1 回

                NataneAssetIndexService.MarkAssetsDirty(
                    touchedMaterials.Select(m => AssetDatabase.GetAssetPath(m)).ToArray(),
                    Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

                RunPostApplyRecheck(result, touchedMaterialGuids);
            }

            return result;
        }

        private static NataneMigrationRecord ApplyMaterialChange(Material material, NataneMigrationChange change)
        {
            var record = new NataneMigrationRecord
            {
                kind = (int)change.kind,
                assetGuid = change.assetGuid,
                assetPath = change.assetPath,
                oldName = change.oldName,
                newName = change.newName
            };

            switch (change.kind)
            {
                case NataneMigrationChangeKind.MaterialProperty:
                    // ロールバック用に適用前の新プロパティ値を控える。旧プロパティ値は .mat に残存するため保持不要。
                    record.hasFloatValue = true;
                    record.preApplyNewValue = material.HasProperty(change.newName) ? material.GetFloat(change.newName) : 0f;
                    record.appliedNewValue = change.newFloatValue;
                    material.SetFloat(change.newName, change.newFloatValue);
                    return record;

                case NataneMigrationChangeKind.MaterialKeyword:
                    material.DisableKeyword(change.oldName);
                    material.EnableKeyword(change.newName);
                    return record;

                case NataneMigrationChangeKind.MaterialShader:
                    Shader newShader = Shader.Find(change.newName);
                    if (newShader == null)
                    {
                        return null;
                    }
                    material.shader = newShader;
                    return record;

                default:
                    return null;
            }
        }

        private static bool ApplyClipBinding(AnimationClip clip, NataneMigrationChange change, out NataneMigrationRecord record)
        {
            record = null;
            EditorCurveBinding oldBinding = BuildBinding(change.bindingPath, change.bindingTypeName, "material." + change.oldName);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, oldBinding);
            if (curve == null)
            {
                return false;
            }

            EditorCurveBinding newBinding = BuildBinding(change.bindingPath, change.bindingTypeName, "material." + change.newName);

            Undo.RecordObject(clip, "Natane Migration Clip");
            AnimationUtility.SetEditorCurve(clip, oldBinding, null);
            AnimationUtility.SetEditorCurve(clip, newBinding, curve);
            EditorUtility.SetDirty(clip);

            record = new NataneMigrationRecord
            {
                kind = (int)NataneMigrationChangeKind.ClipBinding,
                assetGuid = change.assetGuid,
                assetPath = change.assetPath,
                oldName = change.oldName,
                newName = change.newName,
                bindingPath = change.bindingPath,
                bindingTypeName = change.bindingTypeName
            };
            return true;
        }

        // ============================================================
        //  ロールバック（履歴から逆適用）
        // ============================================================

        public static NataneMigrationApplyResult RollbackLastBatch()
        {
            NataneMigrationHistory history = LoadHistory();
            NataneMigrationBatch last = history.batches.LastOrDefault(b => !b.rolledBack && b.records.Count > 0);
            if (last == null)
            {
                var empty = new NataneMigrationApplyResult();
                empty.warnings.Add("ロールバック可能なバッチがありません");
                return empty;
            }

            return RollbackBatch(last.batchId);
        }

        public static NataneMigrationApplyResult RollbackBatch(string batchId)
        {
            var result = new NataneMigrationApplyResult { batchId = batchId };
            NataneMigrationHistory history = LoadHistory();
            NataneMigrationBatch batch = history.batches.FirstOrDefault(b => b.batchId == batchId);
            if (batch == null || batch.rolledBack)
            {
                result.warnings.Add("対象バッチが見つからないか、既にロールバック済みです");
                return result;
            }

            var touchedMaterials = new List<Material>();

            foreach (NataneMigrationRecord record in batch.records)
            {
                try
                {
                    var kind = (NataneMigrationChangeKind)record.kind;
                    if (kind == NataneMigrationChangeKind.ClipBinding)
                    {
                        RollbackClipBinding(record, result);
                        continue;
                    }

                    Material material = LoadMaterialByGuidOrPath(record.assetGuid, record.assetPath);
                    if (material == null)
                    {
                        result.errors.Add($"Material をロードできません: {record.assetPath}");
                        continue;
                    }

                    Undo.RecordObject(material, "Natane Migration Rollback");
                    switch (kind)
                    {
                        case NataneMigrationChangeKind.MaterialProperty:
                            if (material.HasProperty(record.newName))
                            {
                                material.SetFloat(record.newName, record.preApplyNewValue);
                            }
                            break;
                        case NataneMigrationChangeKind.MaterialKeyword:
                            material.EnableKeyword(record.oldName);
                            material.DisableKeyword(record.newName);
                            break;
                        case NataneMigrationChangeKind.MaterialShader:
                            Shader oldShader = Shader.Find(record.oldName);
                            if (oldShader != null) material.shader = oldShader;
                            break;
                    }

                    EditorUtility.SetDirty(material);
                    result.appliedCount++;
                    if (!touchedMaterials.Contains(material)) touchedMaterials.Add(material);
                }
                catch (Exception ex)
                {
                    result.errors.Add($"{record.assetPath}: {ex.Message}");
                }
            }

            if (result.errors.Count == 0)
            {
                batch.rolledBack = true;
                SaveHistory(history);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void RollbackClipBinding(NataneMigrationRecord record, NataneMigrationApplyResult result)
        {
            string path = AssetDatabase.GUIDToAssetPath(record.assetGuid);
            if (string.IsNullOrEmpty(path)) path = record.assetPath;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                result.errors.Add($"Clip をロードできません: {record.assetPath}");
                return;
            }

            EditorCurveBinding newBinding = BuildBinding(record.bindingPath, record.bindingTypeName, "material." + record.newName);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, newBinding);
            if (curve == null)
            {
                result.warnings.Add($"{clip.name}: 逆張替え対象カーブが見つかりません");
                return;
            }

            EditorCurveBinding oldBinding = BuildBinding(record.bindingPath, record.bindingTypeName, "material." + record.oldName);
            Undo.RecordObject(clip, "Natane Migration Rollback Clip");
            AnimationUtility.SetEditorCurve(clip, newBinding, null);
            AnimationUtility.SetEditorCurve(clip, oldBinding, curve);
            EditorUtility.SetDirty(clip);
            result.appliedCount++;
        }

        // ============================================================
        //  適用後の再検査
        // ============================================================

        private static void RunPostApplyRecheck(NataneMigrationApplyResult result, HashSet<string> touchedMaterialGuids)
        {
            // Update Audit を再実行し保存する（明示適用に付随する再監査）。
            NataneShaderAuditData audit = NataneShaderUpdateAudit.Run();
            NataneShaderUpdateAudit.Save(audit);

            // 履歴対象 Material のみの簡易チェック: Missing 参照 / 旧 Keyword 残留。
            var oldKeywords = new HashSet<string>(
                NataneShaderFeatureRegistry.MigrationDefinitions
                    .Where(d => d.HasKeywordRename)
                    .Select(d => d.oldKeyword),
                StringComparer.Ordinal);

            foreach (string guid in touchedMaterialGuids)
            {
                Material material = NataneAssetIndexService.LoadMaterial(guid);
                if (material == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    material = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
                }

                if (material == null)
                {
                    result.missingReferenceCount++;
                    continue;
                }

                if (material.shader == null)
                {
                    result.missingReferenceCount++;
                    continue;
                }

                foreach (string kw in material.shaderKeywords)
                {
                    if (oldKeywords.Contains(kw))
                    {
                        result.residualOldKeywordCount++;
                    }
                }
            }
        }

        // ============================================================
        //  Update Status DTO（Stage I の Shader Updates タブが消費）
        // ============================================================

        public static NataneUpdateStatus GetUpdateStatus()
        {
            var status = new NataneUpdateStatus
            {
                currentPackageVersion = NataneShaderFeatureRegistry.ShaderCompatibilityVersion,
                currentUnityVersion = Application.unityVersion,
                migrationDefinitionCount = NataneShaderFeatureRegistry.MigrationDefinitions.Count,
                migrationCandidateCount = NataneShaderFeatureRegistry.MigrationDefinitions.Count,
                auditStale = NataneShaderUpdateAudit.IsStale()
            };

            if (NataneShaderUpdateAudit.TryLoad(out NataneShaderAuditData audit))
            {
                status.hasAudit = true;
                status.lastAuditPackageVersion = audit.packageVersion;
                status.unknownKeywordCount = audit.unknownKeywords.Count;
                status.orphanDefinitionCount = audit.orphanDefinitions.Count;
                status.missingTargetShaderCount = audit.missingTargetShaders.Count;
                status.missingPropertyCount = audit.missingProperties.Count;
                status.unparseableItemCount = audit.unparseableItems.Count;
                status.registrySchemaMatches = audit.registrySchemaVersion == NataneShaderFeatureRegistry.RegistrySchemaVersion;
            }
            else
            {
                status.notes.Add("監査結果がありません。Shader Update Audit を実行してください。");
                status.registrySchemaMatches = true; // 監査未実施時は不整合扱いにしない
            }

            if (status.auditStale)
            {
                status.notes.Add("Shader/Registry/Version が変化しています。再監査を推奨します。");
            }

            return status;
        }

        // ============================================================
        //  履歴 I/O
        // ============================================================

        public static NataneMigrationHistory LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryPath))
                {
                    var loaded = JsonUtility.FromJson<NataneMigrationHistory>(File.ReadAllText(HistoryPath));
                    if (loaded != null && loaded.schemaVersion == HistorySchemaVersion && loaded.batches != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Migration] 履歴の読み込みに失敗: {ex.Message}");
            }

            return new NataneMigrationHistory();
        }

        private static void AppendBatch(NataneMigrationBatch batch)
        {
            NataneMigrationHistory history = LoadHistory();
            history.batches.Add(batch);
            SaveHistory(history);
        }

        private static void SaveHistory(NataneMigrationHistory history)
        {
            try
            {
                string dir = Path.GetDirectoryName(HistoryPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(HistoryPath, JsonUtility.ToJson(history, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Migration] 履歴の保存に失敗: {ex.Message}");
            }
        }

        // ============================================================
        //  ヘルパ
        // ============================================================

        private static Material LoadMaterial(NataneMigrationChange change)
        {
            return LoadMaterialByGuidOrPath(change.assetGuid, change.assetPath);
        }

        private static Material LoadMaterialByGuidOrPath(string guid, string fallbackPath)
        {
            string path = string.IsNullOrEmpty(guid) ? fallbackPath : AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) path = fallbackPath;
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static AnimationClip LoadClip(NataneMigrationChange change)
        {
            string path = string.IsNullOrEmpty(change.assetGuid) ? change.assetPath : AssetDatabase.GUIDToAssetPath(change.assetGuid);
            if (string.IsNullOrEmpty(path)) path = change.assetPath;
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static EditorCurveBinding BuildBinding(string path, string typeName, string propertyName)
        {
            Type type = ResolveType(typeName) ?? typeof(Renderer);
            return new EditorCurveBinding
            {
                path = path ?? string.Empty,
                type = type,
                propertyName = propertyName
            };
        }

        private static Type ResolveType(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
            {
                return null;
            }

            try
            {
                return Type.GetType(assemblyQualifiedName);
            }
            catch
            {
                return null;
            }
        }
    }
}
