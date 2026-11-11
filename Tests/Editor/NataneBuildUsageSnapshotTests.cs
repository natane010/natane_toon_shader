using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    public class NataneBuildUsageSnapshotTests
    {
        private NataneOptimizationMode _savedMode;
        private List<string> _savedAlwaysKeepKeywords;

        [SetUp]
        public void SetUp()
        {
            NataneBuildUsageSnapshotBuilder.TestFailureHook = null;
            var s = NataneBuildPolicySettings.instance;
            _savedMode = s.OptimizationMode;
            _savedAlwaysKeepKeywords = new List<string>(s.AlwaysKeepKeywords);
        }

        [TearDown]
        public void TearDown()
        {
            NataneBuildUsageSnapshotBuilder.TestFailureHook = null;
            var s = NataneBuildPolicySettings.instance;
            s.OptimizationMode = _savedMode;
            s.AlwaysKeepKeywords.Clear();
            s.AlwaysKeepKeywords.AddRange(_savedAlwaysKeepKeywords);
            s.SaveSettings();
        }

        // ---- fingerprint（純データ・Unity アセット非依存）----

        [Test]
        public void Fingerprint_ExcludesGeneratedAt_StableForSameContent()
        {
            var a = MakeMinimalSnapshot();
            var b = MakeMinimalSnapshot();
            b.generatedAtUtcTicks = a.generatedAtUtcTicks + 100000; // 生成時刻のみ差

            Assert.That(b.ComputeFingerprint(), Is.EqualTo(a.ComputeFingerprint()),
                "generatedAtUtc は fingerprint 対象外であるべき");
        }

        [Test]
        public void Fingerprint_ChangesWhenOptimizationDigestChanges()
        {
            var a = MakeMinimalSnapshot();
            string before = a.ComputeFingerprint();

            a.optimizationDigest.mode = "Aggressive"; // 設定ダイジェスト変更
            string after = a.ComputeFingerprint();

            Assert.That(after, Is.Not.EqualTo(before),
                "最適化設定ダイジェスト変更で fingerprint が変化するべき");
        }

        // ---- IsSnapshotStale ----

        [Test]
        public void IsSnapshotStale_TrueWhenRegistryFingerprintMismatch()
        {
            SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(
                EditorUserBuildSettings.activeBuildTarget, scenePathsOrNull: null, saveSyncToDisk: false);
            Assert.That(result.Succeeded, Is.True, "テスト前提: Snapshot 生成成功");

            // Registry フィンガープリントのみ不一致にする → stale=true を期待。
            result.Snapshot.registryFingerprint = "deadbeef-mismatch";
            Assert.That(NataneBuildUsageSnapshotStore.IsSnapshotStale(result.Snapshot), Is.True);
        }

        // ---- 失敗経路 ----

        [Test]
        public void Build_ReturnsFailureResult_WhenExceptionInjected()
        {
            NataneBuildUsageSnapshotBuilder.TestFailureHook =
                () => new System.InvalidOperationException("injected failure");

            SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(
                EditorUserBuildSettings.activeBuildTarget, scenePathsOrNull: null, saveSyncToDisk: false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Does.Contain("injected failure"));
            Assert.That(result.Snapshot, Is.Not.Null, "失敗時も参考用 Snapshot を返すべき");
        }

        // ---- 生成の安定性 ----

        [Test]
        public void Build_ProducesStableFingerprintAcrossTwoRuns()
        {
            SnapshotBuildResult first = NataneBuildUsageSnapshotBuilder.Build(
                EditorUserBuildSettings.activeBuildTarget, scenePathsOrNull: null, saveSyncToDisk: false);
            SnapshotBuildResult second = NataneBuildUsageSnapshotBuilder.Build(
                EditorUserBuildSettings.activeBuildTarget, scenePathsOrNull: null, saveSyncToDisk: false);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Snapshot.snapshotFingerprint,
                Is.EqualTo(first.Snapshot.snapshotFingerprint),
                "同一プロジェクト状態では 2 回生成しても fingerprint が一致するべき");
        }

        // ---- AnimationClip 由来キーワード抽出 ----

        [Test]
        public void AnimationExtraction_DetectsEmissionKeywordFromMaterialCurve()
        {
            var map = NataneBuildUsageSnapshotBuilder.BuildAnimatablePropertyToKeywordMap();
            Assume.That(map.ContainsKey("_Emission"),
                "前提: Registry に _Emission -> _EMISSION のアニメ可能定義が存在する");

            var clip = new AnimationClip { name = "TmpEmissionClip" };
            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(Renderer),
                propertyName = "material._Emission"
            };
            // >=0.5 に到達するカーブ（有効化し得る）。
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(new Keyframe(0f, 1f)));

            List<string> keywords = NataneBuildUsageSnapshotBuilder.ExtractAnimationDrivenKeywords(clip, map);

            Assert.That(keywords, Does.Contain("_EMISSION"),
                "material._Emission を >=0.5 にするクリップから _EMISSION を検出するべき");

            Object.DestroyImmediate(clip);
        }

        // ---- 常時保持リストの反映 ----

        [Test]
        public void Build_ReflectsAlwaysKeepKeywordsFromPolicy()
        {
            const string sentinel = "_NATANE_TEST_ALWAYS_KEEP";
            var s = NataneBuildPolicySettings.instance;
            if (!s.AlwaysKeepKeywords.Contains(sentinel))
            {
                s.AlwaysKeepKeywords.Add(sentinel);
            }
            s.SaveSettings();

            SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(
                EditorUserBuildSettings.activeBuildTarget, scenePathsOrNull: null, saveSyncToDisk: false);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.alwaysKeepKeywords, Does.Contain(sentinel),
                "PolicySettings の常時保持キーワードが Snapshot へ反映されるべき");
        }

        // ---- ヘルパ ----

        private static NataneBuildUsageSnapshot MakeMinimalSnapshot()
        {
            var s = new NataneBuildUsageSnapshot
            {
                schemaVersion = NataneBuildUsageSnapshot.CurrentSchemaVersion,
                registrySchemaVersion = 1,
                registryMigrationVersion = 0,
                registryFingerprint = "rf",
                shaderCompatibilityVersion = "1.0.0",
                packageVersion = "1.0.0",
                shaderDependencyFingerprint = "sd",
                unityVersion = "2022.3.28f1",
                buildTarget = "StandaloneWindows64",
                graphicsApis = new List<string> { "Direct3D11" },
                optimizationDigest = new NataneOptimizationDigest
                {
                    mode = "Safe",
                    strictFailurePolicy = "FallbackKeepAll",
                    unknownKeywordPolicy = "Keep",
                    hlslFeatureGuardMode = "Off",
                    buildPrewarmEnabled = false,
                    variantStrippingEnabled = true,
                    legacyExactSetStrippingEnabled = false
                }
            };
            s.generatedAtUtcTicks = 123456789L;
            s.snapshotFingerprint = s.ComputeFingerprint();
            return s;
        }
    }
}
