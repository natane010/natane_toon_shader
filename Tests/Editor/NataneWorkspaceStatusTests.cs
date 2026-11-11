using NUnit.Framework;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// アーティスト向けステータスバナー判定（純関数 ComputeWorkspaceStatus）のユニットテスト。
    /// </summary>
    public class NataneWorkspaceStatusTests
    {
        private static NataneWorkspaceStatus Eval(
            NataneOptimizationMode mode, bool stripping, bool auditStale, int unknown, bool fallback)
        {
            return NataneWorkspaceStatusEvaluator.ComputeWorkspaceStatus(new NataneWorkspaceStatusInput
            {
                mode = mode,
                variantStrippingEnabled = stripping,
                auditStale = auditStale,
                unknownKeywordCount = unknown,
                hasFallbackReason = fallback
            });
        }

        [Test]
        public void Safe_WhenSafeFreshNoUnknown()
        {
            var s = Eval(NataneOptimizationMode.Safe, true, false, 0, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.Safe));
        }

        [Test]
        public void Attention_WhenUnknownPresent()
        {
            var s = Eval(NataneOptimizationMode.Safe, true, false, 3, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.Attention));
        }

        [Test]
        public void Attention_WhenAuditStale()
        {
            var s = Eval(NataneOptimizationMode.Safe, true, true, 0, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.Attention));
        }

        [Test]
        public void Attention_WhenReportOnly()
        {
            var s = Eval(NataneOptimizationMode.ReportOnly, true, false, 0, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.Attention));
        }

        [Test]
        public void Danger_WhenAggressive()
        {
            var s = Eval(NataneOptimizationMode.Aggressive, true, false, 0, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.Danger));
        }

        [Test]
        public void Stopped_WhenDisabled()
        {
            var s = Eval(NataneOptimizationMode.Disabled, true, true, 5, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.OptimizationStopped));
        }

        [Test]
        public void Stopped_WhenStrippingOff()
        {
            var s = Eval(NataneOptimizationMode.Safe, false, false, 0, false);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.OptimizationStopped));
        }

        [Test]
        public void Fallback_TakesPrecedenceOverAggressive()
        {
            // フォールバック中は Aggressive より優先（実際に全保持になっている状態を表示）。
            var s = Eval(NataneOptimizationMode.Aggressive, true, false, 0, true);
            Assert.That(s.level, Is.EqualTo(NataneWorkspaceStatusLevel.KeepAllFallback));
        }
    }
}
