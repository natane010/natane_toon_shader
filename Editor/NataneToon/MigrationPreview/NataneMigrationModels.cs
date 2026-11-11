using System;
using System.Collections.Generic;

namespace NataneToon.Editor
{
    // 変更の種別。書換えは Material / AnimationClip に限定し、Prefab/Scene は影響表示のみ。
    public enum NataneMigrationChangeKind
    {
        MaterialProperty, // 旧 float プロパティ値 -> 新プロパティへ写す
        MaterialKeyword,  // 旧 Keyword 無効化 + 新 Keyword 有効化
        MaterialShader,   // material.shader を差替え
        ClipBinding       // AnimationClip の EditorCurveBinding を張替え
    }

    /// <summary>
    /// プレビュー 1 行（実行時モデル・非シリアライズ）。前後値を並記し、個別除外チェックを持つ。
    /// </summary>
    public sealed class NataneMigrationChange
    {
        public NataneMigrationChangeKind kind;
        public string assetGuid;
        public string assetPath;
        public string assetName;

        // 変更対象の識別（プロパティ名 / Keyword / Shader 名 / binding propertyName）。
        public string oldName;
        public string newName;

        // float 値変換を伴う場合のみ有効。
        public bool hasFloatValue;
        public float oldFloatValue;
        public float newFloatValue;

        // Clip binding 用（path/type は張替え時に保持）。
        public string bindingPath;
        public string bindingTypeName;

        public bool autoApplicable;
        public bool manualReview;
        public string note;

        // UI の個別除外。manualReview の行は既定で除外（included=false）。
        public bool included = true;

        // 前後値の表示文字列（UI 用）。
        public string CurrentValueText
        {
            get
            {
                switch (kind)
                {
                    case NataneMigrationChangeKind.MaterialProperty:
                        return hasFloatValue ? oldFloatValue.ToString("0.###") : "(値なし)";
                    case NataneMigrationChangeKind.MaterialKeyword:
                        return oldName + " (ON)";
                    case NataneMigrationChangeKind.MaterialShader:
                        return oldName;
                    case NataneMigrationChangeKind.ClipBinding:
                        return "material." + oldName;
                    default:
                        return string.Empty;
                }
            }
        }

        public string AppliedValueText
        {
            get
            {
                switch (kind)
                {
                    case NataneMigrationChangeKind.MaterialProperty:
                        return newName + " = " + (hasFloatValue ? newFloatValue.ToString("0.###") : "(値なし)");
                    case NataneMigrationChangeKind.MaterialKeyword:
                        return newName + " (ON) / " + oldName + " (OFF)";
                    case NataneMigrationChangeKind.MaterialShader:
                        return newName;
                    case NataneMigrationChangeKind.ClipBinding:
                        return "material." + newName;
                    default:
                        return string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// 移行プラン。変更候補と、影響のみ表示する Prefab/Scene 参照数を保持する。
    /// </summary>
    public sealed class NataneMigrationPlan
    {
        public List<NataneMigrationChange> changes = new List<NataneMigrationChange>();
        // 影響表示のみ（書換えはしない）。Material GUID -> それを参照する Prefab/Scene パス。
        public List<string> affectedPrefabPaths = new List<string>();
        public List<string> warnings = new List<string>();

        public int AutoApplicableCount
        {
            get
            {
                int c = 0;
                foreach (NataneMigrationChange ch in changes)
                {
                    if (ch.autoApplicable && !ch.manualReview) c++;
                }
                return c;
            }
        }

        public int ManualReviewCount
        {
            get
            {
                int c = 0;
                foreach (NataneMigrationChange ch in changes)
                {
                    if (ch.manualReview) c++;
                }
                return c;
            }
        }
    }

    /// <summary>適用/ロールバックの結果サマリ。</summary>
    public sealed class NataneMigrationApplyResult
    {
        public string batchId;
        public int appliedCount;
        public int skippedCount;
        public int materialCount;
        public int clipCount;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();

        // 適用後の再検査サマリ（履歴対象アセットの Missing 参照 / 旧 Keyword 残留）。
        public int missingReferenceCount;
        public int residualOldKeywordCount;

        public bool Success => errors.Count == 0;
    }

    // ===== 履歴（Library/NataneToon/Migration 配下 JSON, schemaVersion 付き）=====
    // 前後値を記録し、ロールバック（逆適用）に用いる。

    [Serializable]
    public sealed class NataneMigrationHistory
    {
        public int schemaVersion = 1;
        public List<NataneMigrationBatch> batches = new List<NataneMigrationBatch>();
    }

    [Serializable]
    public sealed class NataneMigrationBatch
    {
        public string batchId;
        public long utcTicks;
        public bool rolledBack;
        public string packageVersion;
        public string migrationVersion;
        public List<NataneMigrationRecord> records = new List<NataneMigrationRecord>();
    }

    [Serializable]
    public sealed class NataneMigrationRecord
    {
        public int kind; // NataneMigrationChangeKind
        public string assetGuid;
        public string assetPath;
        public string oldName;
        public string newName;

        // Material プロパティ: 適用前の新プロパティ値（ロールバックで書き戻す）と適用後値。
        public bool hasFloatValue;
        public float preApplyNewValue;
        public float appliedNewValue;

        // Clip binding: 張替え情報。
        public string bindingPath;
        public string bindingTypeName;
    }

    /// <summary>
    /// Shader Updates タブが消費する状態 DTO（UI 非依存）。Stage I の統合 UI から参照する。
    /// </summary>
    public sealed class NataneUpdateStatus
    {
        public bool hasAudit;
        public string lastAuditPackageVersion;
        public string currentPackageVersion;
        public string currentUnityVersion;

        public int unknownKeywordCount;
        public int orphanDefinitionCount;
        public int missingTargetShaderCount;
        public int missingPropertyCount;
        public int unparseableItemCount;

        public int migrationCandidateCount;   // Registry の移行定義件数
        public int migrationDefinitionCount;   // 同上（明示）

        public bool registrySchemaMatches;     // 保存済み監査の schema が現行 schema と一致するか
        public bool auditStale;                 // 依存/Registry/version が変化して再監査が必要か
        public List<string> notes = new List<string>();
    }
}
