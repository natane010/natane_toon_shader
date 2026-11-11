using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// Stage E: HLSL フィーチャーガードの Gate 判定 / キャッシュ判定 / 期待 HLSL 生成の決定性 /
    /// 旧 Runtime 生成物の掃除判定を、Unity ビルド非依存の純関数として検証する。
    /// </summary>
    public class NataneHlslGuardTests
    {
        // ---- Gate 判定（Off なら書換え関数を呼ばない = ShouldRunHlslGuard=false） ----

        [Test]
        public void Gate_Off_DoesNotRunGuard()
        {
            Assert.That(NataneBuildFeatureOptimizer.ShouldRunHlslGuard(NataneHlslFeatureGuardMode.Off), Is.False,
                "Off（既定）ではガードを実行してはならない");
        }

        [Test]
        public void Gate_Advanced_RunsGuard()
        {
            Assert.That(NataneBuildFeatureOptimizer.ShouldRunHlslGuard(NataneHlslFeatureGuardMode.Advanced), Is.True);
        }

        // ---- キャッシュ判定（全一致→true / 1 項目不一致→false / null(欠損/読取失敗)→false） ----

        private static NataneHlslGuardState MakeState()
        {
            return new NataneHlslGuardState
            {
                schemaVersion = 1,
                expectedHlslHash = "hash-A",
                shaderDependencyFingerprint = "dep-A",
                registryFingerprint = "reg-A",
                packageVersion = "1.2.3",
                unityVersion = "2022.3.0f1",
                buildTarget = "StandaloneWindows64",
                graphicsApis = new List<string> { "Direct3D11" },
                snapshotFingerprint = "snap-A",
            };
        }

        [Test]
        public void Cache_AllMatch_IsFresh()
        {
            Assert.That(NataneBuildFeatureOptimizer.IsGuardStateFresh(MakeState(), MakeState()), Is.True);
        }

        [Test]
        public void Cache_HashMismatch_NotFresh()
        {
            var saved = MakeState();
            var expected = MakeState();
            expected.expectedHlslHash = "hash-B";
            Assert.That(NataneBuildFeatureOptimizer.IsGuardStateFresh(saved, expected), Is.False);
        }

        [Test]
        public void Cache_DependencyFingerprintMismatch_NotFresh()
        {
            var saved = MakeState();
            var expected = MakeState();
            expected.shaderDependencyFingerprint = "dep-B";
            Assert.That(NataneBuildFeatureOptimizer.IsGuardStateFresh(saved, expected), Is.False);
        }

        [Test]
        public void Cache_GraphicsApiMismatch_NotFresh()
        {
            var saved = MakeState();
            var expected = MakeState();
            expected.graphicsApis = new List<string> { "Direct3D11", "Vulkan" };
            Assert.That(NataneBuildFeatureOptimizer.IsGuardStateFresh(saved, expected), Is.False);
        }

        [Test]
        public void Cache_MissingOrUnreadableState_NotFresh()
        {
            // 状態ファイル欠損 / 読取失敗は saved=null で表現され、キャッシュ不使用（false）となる。
            Assert.That(NataneBuildFeatureOptimizer.IsGuardStateFresh(null, MakeState()), Is.False);
        }

        [Test]
        public void GuardState_SaveLoadRoundTrips()
        {
            // 実プロジェクトの Library 配下を一時的に使用し、テスト後に削除する。
            bool hadPrevious = NataneBuildFeatureOptimizer.TryLoadGuardState(out var previous);
            try
            {
                var state = MakeState();
                NataneBuildFeatureOptimizer.SaveGuardState(state);

                Assert.That(NataneBuildFeatureOptimizer.TryLoadGuardState(out var loaded), Is.True);
                Assert.That(NataneBuildFeatureOptimizer.IsGuardStateFresh(loaded, state), Is.True,
                    "保存→読込のラウンドトリップで全項目一致するべき");
            }
            finally
            {
                if (hadPrevious)
                    NataneBuildFeatureOptimizer.SaveGuardState(previous);
                else
                    NataneBuildFeatureOptimizer.DeleteGuardState();
            }
        }

        // ---- 期待 HLSL 内容の生成が決定的（同入力→同出力・順序非依存） ----

        [Test]
        public void ExpectedHlsl_IsDeterministic()
        {
            var a = NataneBuildFeatureOptimizer.BuildFeatureHlslContent(new[] { "_A", "_B", "_C" });
            var b = NataneBuildFeatureOptimizer.BuildFeatureHlslContent(new[] { "_A", "_B", "_C" });
            Assert.That(a, Is.EqualTo(b), "同一入力で同一出力になるべき");
        }

        [Test]
        public void ExpectedHlsl_IsOrderIndependent()
        {
            var sorted = NataneBuildFeatureOptimizer.BuildFeatureHlslContent(new[] { "_A", "_B", "_C" });
            var shuffled = NataneBuildFeatureOptimizer.BuildFeatureHlslContent(new[] { "_C", "_A", "_B" });
            Assert.That(shuffled, Is.EqualTo(sorted), "入力順に依存せず同一出力になるべき（ソート出力）");
        }

        [Test]
        public void ExpectedHlsl_ContainsFeatureDefinesForUsedKeywords()
        {
            var content = NataneBuildFeatureOptimizer.BuildFeatureHlslContent(new[] { "_FOO" });
            Assert.That(content, Does.Contain("#define NATANE_FEATURE_FOO"));
            // ビルド状態マーカーは含まれない（全機能有効の既定とは異なる）。
            Assert.That(content, Does.Not.Contain("#define NATANE_BUILD_ALL_FEATURES"));
        }

        // ---- 旧 Runtime 生成物の掃除判定（存在時のみ削除対象） ----

        [Test]
        public void LegacyRuntimeScript_PresentOnlyWhenFileExists()
        {
            string path = Path.Combine(Path.GetTempPath(), "NataneLegacyPrewarm_" + System.Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                Assert.That(ShaderPrewarmingSettingsWindow.IsLegacyRuntimeScriptPresent(path), Is.False, "未作成時は掃除対象でない");

                File.WriteAllText(path, "// dummy");
                Assert.That(ShaderPrewarmingSettingsWindow.IsLegacyRuntimeScriptPresent(path), Is.True, "存在時は掃除対象");

                File.Delete(path);
                Assert.That(ShaderPrewarmingSettingsWindow.IsLegacyRuntimeScriptPresent(path), Is.False, "削除後は掃除対象でない");
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* テンポラリ削除失敗は無視 */ }
            }
        }

        [Test]
        public void LegacyRuntimeScript_NullOrEmptyPath_NotPresent()
        {
            Assert.That(ShaderPrewarmingSettingsWindow.IsLegacyRuntimeScriptPresent(null), Is.False);
            Assert.That(ShaderPrewarmingSettingsWindow.IsLegacyRuntimeScriptPresent(string.Empty), Is.False);
        }
    }
}
