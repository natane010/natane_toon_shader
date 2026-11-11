using System.IO;
using System.Linq;
using NUnit.Framework;
using NataneToon.Editor;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// アセット整理サービスのテスト。一時フォルダを生成し TearDown で削除する。
    /// リポジトリ側には一切アセットを残さない（スクラッチプロジェクトの Assets 上で完結）。
    /// </summary>
    public class NataneAssetOrganizerTests
    {
        private const string Root = "Assets/NataneOrganizerTest";

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            AssetDatabase.CreateFolder("Assets", "NataneOrganizerTest");
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRoot();
        }

        private static void DeleteRoot()
        {
            if (AssetDatabase.IsValidFolder(Root))
            {
                AssetDatabase.DeleteAsset(Root);
            }
            AssetDatabase.Refresh();
        }

        private static void CreateFolder(string relative)
        {
            string full = Root + "/" + relative;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(Root, relative);
            }
        }

        private static string CreateMaterial(string relativePath)
        {
            string path = Root + "/" + relativePath;
            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        // ---- 純関数: ワイルドカード ----

        [Test]
        public void Wildcard_MatchesStarAndQuestion()
        {
            Assert.IsTrue(NataneAssetOrganizerService.MatchesWildcard("Body.mat", "*"));
            Assert.IsTrue(NataneAssetOrganizerService.MatchesWildcard("Body.mat", "*.mat"));
            Assert.IsTrue(NataneAssetOrganizerService.MatchesWildcard("Body.mat", "Body.?at"));
            Assert.IsTrue(NataneAssetOrganizerService.MatchesWildcard("BODY.MAT", "body*"));
            Assert.IsFalse(NataneAssetOrganizerService.MatchesWildcard("Hair.mat", "Body*"));
        }

        [Test]
        public void IsPathOrganizable_ExcludesPackagesAndNonAssets()
        {
            Assert.IsFalse(NataneAssetOrganizerService.IsPathOrganizable("Packages/com.natane.toonshader/foo.mat"));
            Assert.IsFalse(NataneAssetOrganizerService.IsPathOrganizable("Library/x.mat"));
            Assert.IsFalse(NataneAssetOrganizerService.IsPathOrganizable("/tmp/x.mat"));
        }

        // ---- GUID 維持移動 ----

        [Test]
        public void Apply_PreservesGuid()
        {
            CreateFolder("src");
            CreateFolder("dst");
            string from = CreateMaterial("src/GuidKeep.mat");
            string guidBefore = AssetDatabase.AssetPathToGUID(from);

            var candidate = new NataneOrganizeCandidate
            {
                guid = guidBefore,
                fromPath = from,
                toPath = Root + "/dst/GuidKeep.mat",
                kind = NataneAssetKind.Material
            };

            NataneOrganizeApplyResult result = NataneAssetOrganizerService.Apply(new[] { candidate });

            Assert.AreEqual(1, result.movedCount);
            Assert.IsEmpty(result.guidMismatches);
            string guidAfter = AssetDatabase.AssetPathToGUID(Root + "/dst/GuidKeep.mat");
            Assert.AreEqual(guidBefore, guidAfter, "移動後に GUID が変化しました");
        }

        // ---- 衝突検出（同名存在時は適用対象外）----

        [Test]
        public void BuildProposal_DetectsConflict()
        {
            CreateFolder("src");
            CreateFolder("dst");
            // 移動先に同名の別アセットを先に用意する。
            CreateMaterial("dst/Dup.mat");
            string from = CreateMaterial("src/Dup.mat");

            var ruleSet = ScriptableObject.CreateInstance<NataneAssetOrganizerRuleSet>();
            ruleSet.Rules.Add(new NataneAssetOrganizerRule
            {
                targetKind = NataneAssetKind.Material,
                namePattern = "*",
                destinationFolder = Root + "/dst"
            });

            var candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
            NataneOrganizeCandidate conflict = candidates.FirstOrDefault(c => c.fromPath == from);

            Assert.IsNotNull(conflict, "移動元候補が生成されていません");
            Assert.AreEqual(NataneOrganizeCandidateStatus.Conflict, conflict.status);
            Assert.IsFalse(conflict.IsApplicable, "衝突候補が適用対象になっています");

            Object.DestroyImmediate(ruleSet);
        }

        // ---- ロールバックで元パスへ戻り GUID 不変 ----

        [Test]
        public void Rollback_RestoresOriginalPathAndGuid()
        {
            CreateFolder("src");
            CreateFolder("dst");
            string from = CreateMaterial("src/Roll.mat");
            string to = Root + "/dst/Roll.mat";
            string guidBefore = AssetDatabase.AssetPathToGUID(from);

            var candidate = new NataneOrganizeCandidate
            {
                guid = guidBefore,
                fromPath = from,
                toPath = to,
                kind = NataneAssetKind.Material
            };

            NataneOrganizeApplyResult applied = NataneAssetOrganizerService.Apply(new[] { candidate });
            Assert.AreEqual(1, applied.movedCount);
            Assert.IsFalse(string.IsNullOrEmpty(applied.batchId));

            NataneOrganizeApplyResult rolled = NataneAssetOrganizerService.RollbackBatch(applied.batchId);
            Assert.AreEqual(1, rolled.movedCount);
            Assert.IsEmpty(rolled.moveErrors);

            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(from)), "元パスにアセットが戻っていません");
            Assert.AreEqual(guidBefore, AssetDatabase.AssetPathToGUID(from), "ロールバック後に GUID が変化しました");
        }

        // ---- pin 除外 ----

        [Test]
        public void BuildProposal_ExcludesPinned()
        {
            CreateFolder("src");
            string from = CreateMaterial("src/Pinned.mat");
            string guid = AssetDatabase.AssetPathToGUID(from);

            var ruleSet = ScriptableObject.CreateInstance<NataneAssetOrganizerRuleSet>();
            ruleSet.Rules.Add(new NataneAssetOrganizerRule
            {
                targetKind = NataneAssetKind.Material,
                namePattern = "*",
                destinationFolder = Root + "/dst"
            });
            ruleSet.PinnedGuids.Add(guid);

            var candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
            Assert.IsFalse(candidates.Any(c => c.guid == guid), "pin されたアセットが提案に含まれています");

            Object.DestroyImmediate(ruleSet);
        }

        // ---- Packages 配下が候補に出ない ----

        [Test]
        public void BuildProposal_DoesNotIncludePackages()
        {
            var ruleSet = ScriptableObject.CreateInstance<NataneAssetOrganizerRuleSet>();
            ruleSet.Rules.Add(new NataneAssetOrganizerRule
            {
                targetKind = NataneAssetKind.Material,
                namePattern = "*",
                destinationFolder = Root + "/dst"
            });

            var candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
            Assert.IsFalse(candidates.Any(c => c.fromPath.StartsWith("Packages/")),
                "Packages 配下のアセットが候補に含まれています");

            Object.DestroyImmediate(ruleSet);
        }
    }
}
