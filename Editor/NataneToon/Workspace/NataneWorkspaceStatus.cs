using System.Collections.Generic;

namespace NataneToon.Editor
{
    // アーティスト向けステータスバナーの状態。判定は純関数 ComputeWorkspaceStatus に集約する。
    public enum NataneWorkspaceStatusLevel
    {
        Safe,                 // 緑: Safe 動作・監査新鮮・未知 0
        Attention,            // 黄: 未知あり or Audit stale or ReportOnly
        Danger,               // 赤: Aggressive 有効
        OptimizationStopped,  // 灰: Disabled or ストリッピング OFF
        KeepAllFallback       // 橙: 直近レポートにフォールバック理由あり
    }

    /// <summary>ステータス判定入力（Unity アセット非依存・テスト容易）。</summary>
    public struct NataneWorkspaceStatusInput
    {
        public NataneOptimizationMode mode;
        public bool variantStrippingEnabled;
        public bool auditStale;
        public int unknownKeywordCount;
        public bool hasFallbackReason;   // 直近ストリップレポートに全保持フォールバック理由があるか
    }

    /// <summary>ステータス判定結果（レベルと日本語/英語メッセージ）。</summary>
    public struct NataneWorkspaceStatus
    {
        public NataneWorkspaceStatusLevel level;
        public string messageJa;
        public string messageEn;
    }

    public static class NataneWorkspaceStatusEvaluator
    {
        /// <summary>
        /// ステータスを 1 つに集約する純関数。優先順位:
        /// ①最適化停止中（Disabled/ストリップ OFF）②全保持フォールバック中 ③危険（Aggressive）
        /// ④要確認（未知 or stale or ReportOnly）⑤安全。
        /// 停止中を最優先にするのは「何も削除されない」事実を最初に伝えるため。
        /// フォールバックを Aggressive より上にするのは、実際に全保持になっている状態を優先表示するため。
        /// </summary>
        public static NataneWorkspaceStatus ComputeWorkspaceStatus(NataneWorkspaceStatusInput input)
        {
            if (input.mode == NataneOptimizationMode.Disabled || !input.variantStrippingEnabled)
            {
                return Make(NataneWorkspaceStatusLevel.OptimizationStopped,
                    "最適化停止中: バリアント削除は行われません（Disabled / ストリッピング OFF）。",
                    "Optimization stopped: no variant stripping (Disabled / stripping off).");
            }

            if (input.hasFallbackReason)
            {
                return Make(NataneWorkspaceStatusLevel.KeepAllFallback,
                    "全保持フォールバック中: 直近ビルドで安全側の全保持理由が記録されています。",
                    "Keep-all fallback: the latest build recorded a safe keep-all reason.");
            }

            if (input.mode == NataneOptimizationMode.Aggressive)
            {
                return Make(NataneWorkspaceStatusLevel.Danger,
                    "危険: Aggressive が有効です。実在構成以外は削除されます（続行ガード通過時のみ実行）。",
                    "Danger: Aggressive is enabled. Non-existing configs are stripped (only when the guard passes).");
            }

            if (input.unknownKeywordCount > 0 || input.auditStale || input.mode == NataneOptimizationMode.ReportOnly)
            {
                var reasonsJa = new List<string>();
                var reasonsEn = new List<string>();
                if (input.unknownKeywordCount > 0)
                {
                    reasonsJa.Add($"未知キーワード {input.unknownKeywordCount} 件");
                    reasonsEn.Add($"{input.unknownKeywordCount} unknown keyword(s)");
                }
                if (input.auditStale) { reasonsJa.Add("監査が stale"); reasonsEn.Add("audit stale"); }
                if (input.mode == NataneOptimizationMode.ReportOnly) { reasonsJa.Add("ReportOnly"); reasonsEn.Add("ReportOnly"); }
                return Make(NataneWorkspaceStatusLevel.Attention,
                    "要確認: " + string.Join(" / ", reasonsJa) + "。",
                    "Attention: " + string.Join(" / ", reasonsEn) + ".");
            }

            return Make(NataneWorkspaceStatusLevel.Safe,
                "安全: Safe 動作・監査は新鮮・未知キーワード 0 件です。",
                "Safe: Safe mode, audit fresh, zero unknown keywords.");
        }

        private static NataneWorkspaceStatus Make(NataneWorkspaceStatusLevel level, string ja, string en)
        {
            return new NataneWorkspaceStatus { level = level, messageJa = ja, messageEn = en };
        }
    }
}
