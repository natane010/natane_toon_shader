using System;
using System.Collections.Generic;

namespace NataneToon.Editor
{
    // 振り分けルールの対象アセット種別。Other は上記以外すべて。
    public enum NataneAssetKind
    {
        Material,
        Texture,
        Prefab,
        AnimationClip,
        Other
    }

    // 候補の状態。Conflict / Excluded は適用対象から外れる（安全側）。
    public enum NataneOrganizeCandidateStatus
    {
        Ready,
        Conflict
    }

    /// <summary>
    /// 1件の振り分けルール。対象種別 × 名前パターン(ワイルドカード) × 移動先フォルダ。
    /// </summary>
    [Serializable]
    public sealed class NataneAssetOrganizerRule
    {
        public bool enabled = true;
        public NataneAssetKind targetKind = NataneAssetKind.Material;
        // ワイルドカード（* と ?）。空/"*" は全一致。ファイル名（拡張子含む）に対して照合。
        public string namePattern = "*";
        // 移動先フォルダ（"Assets/..." のプロジェクト相対）。
        public string destinationFolder = "Assets";
    }

    /// <summary>
    /// プレビュー候補（提案 1 行）。UI と検証層で共有する非シリアライズの実行時モデル。
    /// </summary>
    public sealed class NataneOrganizeCandidate
    {
        public string guid;
        public string fromPath;
        public string toPath;
        public NataneAssetKind kind;
        public NataneOrganizeCandidateStatus status = NataneOrganizeCandidateStatus.Ready;
        public string conflictReason;
        // 依存元（このアセットを参照しているアセット）のパス。代表数件のみ保持。
        public List<string> dependentPaths = new List<string>();
        public int dependentCount;
        // UI のチェックボックスによる個別除外。既定は選択（除外しない）。
        public bool included = true;

        public bool IsApplicable => included && status == NataneOrganizeCandidateStatus.Ready;
    }

    /// <summary>
    /// 適用/ロールバックの結果サマリ。
    /// </summary>
    public sealed class NataneOrganizeApplyResult
    {
        public string batchId;
        public int movedCount;
        public int skippedCount;
        // 移動後に GUID が一致しなかった項目（重大）。
        public List<string> guidMismatches = new List<string>();
        // MoveAsset がエラー文字列を返した項目。
        public List<string> moveErrors = new List<string>();
        // 適用前後で依存元から解決できなくなった依存 GUID の増加数（Missing 相当）。
        public int missingReferenceIncrease;
        public List<string> warnings = new List<string>();

        public bool Success => guidMismatches.Count == 0 && moveErrors.Count == 0 && missingReferenceIncrease == 0;
    }

    // ===== 履歴（Library 配下 JSON, schemaVersion 付き）=====

    [Serializable]
    public sealed class NataneMoveHistory
    {
        public int schemaVersion = 1;
        public List<NataneMoveBatch> batches = new List<NataneMoveBatch>();
    }

    [Serializable]
    public sealed class NataneMoveBatch
    {
        public string batchId;
        public long utcTicks;
        // 既にロールバック済みかどうか（二重ロールバック防止）。
        public bool rolledBack;
        public List<NataneMoveRecord> records = new List<NataneMoveRecord>();
    }

    [Serializable]
    public sealed class NataneMoveRecord
    {
        public string guid;
        public string fromPath;
        public string toPath;
    }
}
