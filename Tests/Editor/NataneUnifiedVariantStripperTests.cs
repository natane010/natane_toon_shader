using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// 統合ストリッパーの Safe 判定（純関数 NataneUnifiedStripDecider.Decide）と SVC 保持判定のユニットテスト。
    /// ShaderCompilerData 非依存。実プレイヤービルドは不要。
    /// </summary>
    public class NataneUnifiedVariantStripperTests
    {
        private const string CoreShader = "Natane/Toon Shader";
        private const string ParticleShader = "Natane/Toon Shader (Particle)";
        private const string SafeKeyword = "_EMISSION";        // KeywordMappings 由来（SafeStrippable / 非 AlwaysKeep）
        private const string ParticleKeyword = "_SOFT_PARTICLES"; // Particle スコープ（SafeStrippable）
        private const string UnknownKeyword = "_NATANE_TEST_UNKNOWN_KW";

        [SetUp]
        public void SetUp()
        {
            Assume.That(NataneShaderFeatureRegistry.TryGetByKeyword(SafeKeyword, out var def), Is.True,
                "前提: Registry に " + SafeKeyword + " が存在する");
            Assume.That(def.SafeStrippable && !def.AlwaysKeep, Is.True,
                "前提: " + SafeKeyword + " は SafeStrippable かつ非 AlwaysKeep");
            Assume.That(NataneShaderCatalog.IsNataneShader(CoreShader), Is.True);
        }

        // ---- ヘルパ ----

        private static NataneStripSnapshotView MakeView(
            Dictionary<string, IReadOnlyCollection<string>> used,
            IEnumerable<string> animation = null,
            IEnumerable<string> runtime = null,
            IEnumerable<string> alwaysKeepKeywords = null,
            IEnumerable<string> alwaysKeepShaderNames = null,
            IReadOnlyDictionary<string, IReadOnlyCollection<IReadOnlyCollection<string>>> configs = null)
        {
            return new NataneStripSnapshotView(
                hasSnapshot: true,
                usedByShader: used,
                animationDrivenKeywords: animation,
                runtimeDynamicKeywords: runtime,
                alwaysKeepKeywords: alwaysKeepKeywords,
                alwaysKeepShaderNames: alwaysKeepShaderNames,
                configsByShader: configs);
        }

        // Aggressive 用: 1 シェーダーの実在 Material 構成集合を作る（各 string[] が 1 構成）。
        private static IReadOnlyDictionary<string, IReadOnlyCollection<IReadOnlyCollection<string>>> Configs(
            string shader, params string[][] configs)
        {
            var list = new List<IReadOnlyCollection<string>>();
            foreach (var c in configs) list.Add(c);
            return new Dictionary<string, IReadOnlyCollection<IReadOnlyCollection<string>>> { { shader, list } };
        }

        private static NataneStripPolicyView AggressivePolicy(bool gatePassed)
        {
            return new NataneStripPolicyView { Mode = NataneOptimizationMode.Aggressive, AggressiveGatePassed = gatePassed };
        }

        private static Dictionary<string, IReadOnlyCollection<string>> Used(string shader, params string[] keywords)
        {
            return new Dictionary<string, IReadOnlyCollection<string>>
            {
                { shader, new List<string>(keywords) }
            };
        }

        private static NataneStripPolicyView Policy(NataneOptimizationMode mode)
        {
            return new NataneStripPolicyView { Mode = mode };
        }

        private static StripDecision Decide(string shader, string[] variantKeywords, NataneStripSnapshotView view, NataneOptimizationMode mode)
        {
            return NataneUnifiedStripDecider.Decide(shader, variantKeywords, view, Policy(mode));
        }

        // ---- Safe 判定 ----

        [Test]
        public void Safe_StripsUnusedKnownKeywordVariant()
        {
            var view = MakeView(Used(CoreShader)); // 使用キーワードなし
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Strip));
            Assert.That(d.TriggerKeywords, Does.Contain(SafeKeyword));
        }

        [Test]
        public void Safe_KeepsWhenKeywordInUse()
        {
            var view = MakeView(Used(CoreShader, SafeKeyword));
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
        }

        [Test]
        public void Safe_KeepsUnknownKeywordVariant_AndRecords()
        {
            Assume.That(NataneShaderFeatureRegistry.IsKnownKeyword(UnknownKeyword), Is.False);
            var view = MakeView(Used(CoreShader));
            var d = Decide(CoreShader, new[] { UnknownKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
            Assert.That(d.UnknownKeywords, Does.Contain(UnknownKeyword));
        }

        [Test]
        public void Safe_KeepsBaseVariant()
        {
            var view = MakeView(Used(CoreShader));
            var d = Decide(CoreShader, new string[0], view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
            Assert.That(d.IsBaseVariant, Is.True);
        }

        [Test]
        public void Safe_KeepsAnimationDrivenKeyword()
        {
            var view = MakeView(Used(CoreShader), animation: new[] { SafeKeyword });
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
        }

        [Test]
        public void Safe_KeepsRuntimeDynamicKeyword()
        {
            var view = MakeView(Used(CoreShader), runtime: new[] { SafeKeyword });
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
        }

        [Test]
        public void Safe_KeepsAlwaysKeepKeyword()
        {
            var view = MakeView(Used(CoreShader), alwaysKeepKeywords: new[] { SafeKeyword });
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
        }

        [Test]
        public void Safe_KeepsAlwaysKeepShader()
        {
            var view = MakeView(Used(CoreShader), alwaysKeepShaderNames: new[] { CoreShader });
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
            Assert.That(d.Reason, Does.Contain("常時保持シェーダー"));
        }

        [Test]
        public void NullSnapshot_KeepsAll()
        {
            var view = NataneStripSnapshotView.Null();
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
            Assert.That(d.Reason, Does.Contain("Snapshot 不在"));
        }

        [Test]
        public void NonCatalogShader_KeepsAll()
        {
            var view = MakeView(Used(CoreShader));
            var d = Decide("Standard", new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
            Assert.That(d.Reason, Does.Contain("カタログ対象外"));
        }

        // ---- ReportOnly / Aggressive ----

        [Test]
        public void ReportOnly_ProducesStripCandidate()
        {
            // ReportOnly でも判定ロジックは Safe と同じ Strip 候補を返す（実削除は本体側で抑止）。
            var view = MakeView(Used(CoreShader));
            var d = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.ReportOnly);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Strip));
        }

        [Test]
        public void Aggressive_GateFailed_DowngradesToSafe()
        {
            // 続行ガード不通過（AggressiveGatePassed=false）のとき Safe へ降格して判定する。
            var view = MakeView(Used(CoreShader));
            var d = NataneUnifiedStripDecider.Decide(CoreShader, new[] { SafeKeyword }, view, AggressivePolicy(gatePassed: false));

            Assert.That(d.AggressiveDowngraded, Is.True);
            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Strip), "降格後は Safe 判定（未使用 → strip）");
        }

        // ---- Aggressive（続行ガード通過時の本判定） ----

        [Test]
        public void Aggressive_KeepsExistingMaterialConfig()
        {
            Assume.That(NataneShaderFeatureRegistry.TryGetByKeyword(SafeKeyword, out var def) && def.AggressiveStrippable, Is.True);

            // 実在構成: _EMISSION を使う Material が 1 つ存在する。
            var view = MakeView(Used(CoreShader), configs: Configs(CoreShader, new[] { SafeKeyword }));
            var d = NataneUnifiedStripDecider.Decide(CoreShader, new[] { SafeKeyword }, view, AggressivePolicy(gatePassed: true));

            Assert.That(d.AggressiveDowngraded, Is.False);
            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep), "実在構成に一致するバリアントは保持");
        }

        [Test]
        public void Aggressive_StripsNonExistingConfig()
        {
            // 実在構成はベース（キーワードなし）のみ。_EMISSION 構成は存在しない → strip。
            var view = MakeView(Used(CoreShader), configs: Configs(CoreShader, new string[0]));
            var d = NataneUnifiedStripDecider.Decide(CoreShader, new[] { SafeKeyword }, view, AggressivePolicy(gatePassed: true));

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Strip));
            Assert.That(d.TriggerKeywords, Does.Contain(SafeKeyword));
        }

        [Test]
        public void Aggressive_KeepsAnimationDrivenApproximation()
        {
            // 実在構成はベースのみだが、_EMISSION が Animation 由来 → 実在構成に加算した構成として保持。
            var view = MakeView(Used(CoreShader), animation: new[] { SafeKeyword },
                configs: Configs(CoreShader, new string[0]));
            var d = NataneUnifiedStripDecider.Decide(CoreShader, new[] { SafeKeyword }, view, AggressivePolicy(gatePassed: true));

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep), "Animation 由来加算近似で保持");
        }

        [Test]
        public void Aggressive_KeepsNonAggressiveStrippableKeyword()
        {
            const string derived = "_PBR_LIKE"; // AggressiveStrippable=false / AlwaysKeep=true
            Assume.That(NataneShaderFeatureRegistry.TryGetByKeyword(derived, out var def) &&
                        !def.AggressiveStrippable, Is.True, "前提: _PBR_LIKE は Aggressive 非対象");

            var view = MakeView(Used(CoreShader), configs: Configs(CoreShader, new string[0]));
            var d = NataneUnifiedStripDecider.Decide(CoreShader, new[] { derived }, view, AggressivePolicy(gatePassed: true));

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep), "Aggressive 非対象キーワードを含む構成は保持");
        }

        [Test]
        public void Aggressive_KeepsUnknownKeyword()
        {
            Assume.That(NataneShaderFeatureRegistry.IsKnownKeyword(UnknownKeyword), Is.False);
            var view = MakeView(Used(CoreShader), configs: Configs(CoreShader, new string[0]));
            var d = NataneUnifiedStripDecider.Decide(CoreShader, new[] { UnknownKeyword }, view, AggressivePolicy(gatePassed: true));

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep), "未知キーワード（判定不能）は保持");
        }

        // ---- Aggressive 続行ガード（純関数） ----

        [Test]
        public void Gate_AllClear_Proceeds()
        {
            var r = NataneAggressiveGate.Evaluate(false, false, false, false);
            Assert.That(r.Proceed, Is.True);
            Assert.That(NataneAggressiveGate.Resolve(r.Proceed, NataneStrictFailurePolicy.FallbackKeepAll),
                Is.EqualTo(NataneAggressiveResolution.Proceed));
        }

        [Test]
        public void Gate_UnknownKeyword_DowngradesUnderNormalPolicy()
        {
            var r = NataneAggressiveGate.Evaluate(hasUnknownKeywords: true, registrySchemaMismatch: false,
                auditStale: false, hasUnparseableItems: false);
            Assert.That(r.Proceed, Is.False);
            Assert.That(NataneAggressiveGate.Resolve(r.Proceed, NataneStrictFailurePolicy.FallbackKeepAll),
                Is.EqualTo(NataneAggressiveResolution.DowngradeToSafe));
        }

        [Test]
        public void Gate_Failure_FailsBuildUnderStrict()
        {
            var r = NataneAggressiveGate.Evaluate(hasUnknownKeywords: false, registrySchemaMismatch: false,
                auditStale: true, hasUnparseableItems: false);
            Assert.That(r.Proceed, Is.False);
            Assert.That(NataneAggressiveGate.Resolve(r.Proceed, NataneStrictFailurePolicy.FailBuild),
                Is.EqualTo(NataneAggressiveResolution.FailBuild));
        }

        // ---- シェーダー間の非波及 ----

        [Test]
        public void ParticleKeyword_DoesNotBleedToOtherShader()
        {
            var used = new Dictionary<string, IReadOnlyCollection<string>>
            {
                { ParticleShader, new List<string> { ParticleKeyword } },
                { CoreShader, new List<string>() }
            };
            var view = MakeView(used);

            // Core の保持集合に Particle のキーワードが混入しない。
            Assert.That(view.GetKeepSet(CoreShader), Does.Not.Contain(ParticleKeyword));

            // Particle は使用中なので保持。
            var particle = Decide(ParticleShader, new[] { ParticleKeyword }, view, NataneOptimizationMode.Safe);
            Assert.That(particle.Action, Is.EqualTo(NataneStripAction.Keep));

            // Core は自分の未使用キーワードのみで判定される（Particle の使用に影響されない）。
            var core = Decide(CoreShader, new[] { SafeKeyword }, view, NataneOptimizationMode.Safe);
            Assert.That(core.Action, Is.EqualTo(NataneStripAction.Strip));
        }

        // ---- ビルトインキーワード ----

        [Test]
        public void BuiltinKeywords_AreIgnored()
        {
            Assert.That(NataneUnifiedStripDecider.IsBuiltinKeyword("DIRECTIONAL"), Is.True);
            Assert.That(NataneUnifiedStripDecider.IsBuiltinKeyword("INSTANCING_ON"), Is.True);
            Assert.That(NataneUnifiedStripDecider.IsBuiltinKeyword("UNITY_HDR_ON"), Is.True);
            Assert.That(NataneUnifiedStripDecider.IsBuiltinKeyword("_ALPHATEST_ON"), Is.True);
            Assert.That(NataneUnifiedStripDecider.IsBuiltinKeyword(SafeKeyword), Is.False);

            var view = MakeView(Used(CoreShader));
            // ビルトインのみ → 管理対象 0 個の base variant として保持（未知扱いにしない）。
            var d = Decide(CoreShader, new[] { "DIRECTIONAL", "_ALPHATEST_ON", "INSTANCING_ON" }, view, NataneOptimizationMode.Safe);

            Assert.That(d.Action, Is.EqualTo(NataneStripAction.Keep));
            Assert.That(d.IsBaseVariant, Is.True);
            Assert.That(d.UnknownKeywords, Is.Empty);
        }

        // ---- SVC 保持（実 SVC アセットを一時生成）----

        [Test]
        public void Svc_ContainsVariant_IsHeld()
        {
            var shader = Shader.Find("Standard");
            Assume.That(shader, Is.Not.Null, "前提: Standard シェーダーが存在する");

            var svc = new ShaderVariantCollection();
            var variant = new ShaderVariantCollection.ShaderVariant(shader, PassType.ForwardBase, "_EMISSION");
            svc.Add(variant);

            const string assetPath = "Assets/__NataneTmpSvc.shadervariants";
            AssetDatabase.CreateAsset(svc, assetPath);
            AssetDatabase.SaveAssets();
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            var settings = NataneBuildPolicySettings.instance;
            var saved = new List<string>(settings.ShaderVariantCollectionGuids);
            settings.ShaderVariantCollectionGuids.Clear();
            settings.ShaderVariantCollectionGuids.Add(guid);
            settings.SaveSettings();

            try
            {
                bool held = NataneUnifiedVariantStripper.IsVariantHeldBySvc(
                    shader, PassType.ForwardBase, new List<string> { "_EMISSION" });
                Assert.That(held, Is.True, "SVC に含まれるバリアントは保持されるべき");
            }
            finally
            {
                settings.ShaderVariantCollectionGuids.Clear();
                settings.ShaderVariantCollectionGuids.AddRange(saved);
                settings.SaveSettings();
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
