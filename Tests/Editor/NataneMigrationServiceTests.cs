using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NataneToon.Editor;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// Migration サービスのテスト。一時 Material/Clip を生成し TearDown で削除する。
    /// リポジトリ側にはアセットを残さない（スクラッチプロジェクトの Assets 上で完結）。
    /// 定義はテスト専用にインジェクション（Standard シェーダーの float プロパティを利用）。
    /// </summary>
    public class NataneMigrationServiceTests
    {
        private const string Root = "Assets/NataneMigrationTest";

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            AssetDatabase.CreateFolder("Assets", "NataneMigrationTest");
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

        private static Material CreateMaterial(string name)
        {
            string path = Root + "/" + name + ".mat";
            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static NataneMigrationDefinition PropRename(
            string oldP, string newP, NataneValueConversion conv = NataneValueConversion.CopyAsIs, float scale = 1f, bool manual = false)
        {
            return new NataneMigrationDefinition
            {
                oldPropertyName = oldP,
                newPropertyName = newP,
                valueConversion = conv,
                scale = scale,
                autoApplicable = !manual,
                requiresManualReview = manual
            };
        }

        // ---- 値変換（純関数） ----

        [Test]
        public void ConvertFloat_ScaleAndInvert()
        {
            var scale = new NataneMigrationDefinition { valueConversion = NataneValueConversion.FloatScale, scale = 0.5f };
            Assert.AreEqual(0.4f, scale.ConvertFloat(0.8f), 1e-5f);

            var invert = new NataneMigrationDefinition { valueConversion = NataneValueConversion.Invert };
            Assert.AreEqual(0.7f, invert.ConvertFloat(0.3f), 1e-5f);

            var copy = new NataneMigrationDefinition { valueConversion = NataneValueConversion.CopyAsIs };
            Assert.AreEqual(0.8f, copy.ConvertFloat(0.8f), 1e-5f);
        }

        // ---- Property リネーム候補生成 ----

        [Test]
        public void BuildPlan_GeneratesPropertyRenameCandidate()
        {
            Material material = CreateMaterial("Prop");
            material.SetFloat("_Metallic", 0.8f);

            var defs = new List<NataneMigrationDefinition> { PropRename("_Metallic", "_Glossiness") };
            NataneMigrationPlan plan = NataneMigrationService.BuildMigrationPlan(defs, new[] { material }, null);

            NataneMigrationChange change = plan.changes.FirstOrDefault(c => c.kind == NataneMigrationChangeKind.MaterialProperty);
            Assert.IsNotNull(change, "Property リネーム候補が生成されていません");
            Assert.AreEqual("_Glossiness", change.newName);
            Assert.AreEqual(0.8f, change.oldFloatValue, 1e-5f);
            Assert.AreEqual(0.8f, change.newFloatValue, 1e-5f);
            Assert.IsTrue(change.included);
        }

        // ---- 値変換を伴うプラン ----

        [Test]
        public void BuildPlan_AppliesFloatScaleInPlan()
        {
            Material material = CreateMaterial("Scale");
            material.SetFloat("_Metallic", 0.8f);

            var defs = new List<NataneMigrationDefinition> { PropRename("_Metallic", "_Glossiness", NataneValueConversion.FloatScale, 0.5f) };
            NataneMigrationPlan plan = NataneMigrationService.BuildMigrationPlan(defs, new[] { material }, null);

            NataneMigrationChange change = plan.changes.First(c => c.kind == NataneMigrationChangeKind.MaterialProperty);
            Assert.AreEqual(0.4f, change.newFloatValue, 1e-5f, "FloatScale がプランに反映されていません");
        }

        // ---- manualReview は既定除外 ----

        [Test]
        public void BuildPlan_ManualReviewExcludedByDefault()
        {
            Material material = CreateMaterial("Manual");
            material.SetFloat("_Metallic", 0.5f);

            var defs = new List<NataneMigrationDefinition> { PropRename("_Metallic", "_Glossiness", manual: true) };
            NataneMigrationPlan plan = NataneMigrationService.BuildMigrationPlan(defs, new[] { material }, null);

            NataneMigrationChange change = plan.changes.First();
            Assert.IsTrue(change.manualReview);
            Assert.IsFalse(change.included, "manualReview 候補が既定で選択されています");
            Assert.AreEqual(1, plan.ManualReviewCount);
        }

        // ---- Keyword リネームプラン ----

        [Test]
        public void BuildPlan_GeneratesKeywordRenameCandidate()
        {
            Material material = CreateMaterial("Kw");
            material.EnableKeyword("_OLD_TESTKW");

            var defs = new List<NataneMigrationDefinition>
            {
                new NataneMigrationDefinition { oldKeyword = "_OLD_TESTKW", newKeyword = "_NEW_TESTKW", autoApplicable = true }
            };
            NataneMigrationPlan plan = NataneMigrationService.BuildMigrationPlan(defs, new[] { material }, null);

            NataneMigrationChange change = plan.changes.FirstOrDefault(c => c.kind == NataneMigrationChangeKind.MaterialKeyword);
            Assert.IsNotNull(change, "Keyword リネーム候補が生成されていません");
            Assert.AreEqual("_NEW_TESTKW", change.newName);
        }

        // ---- AnimationClip バインディング張替えプラン ----

        [Test]
        public void BuildPlan_GeneratesClipBindingCandidate()
        {
            var clip = new AnimationClip { name = "MigClip" };
            string clipPath = Root + "/MigClip.anim";
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(MeshRenderer), "material._Metallic");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.ImportAsset(clipPath);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            var defs = new List<NataneMigrationDefinition> { PropRename("_Metallic", "_Glossiness") };
            NataneMigrationPlan plan = NataneMigrationService.BuildMigrationPlan(defs, null, new[] { clip });

            NataneMigrationChange change = plan.changes.FirstOrDefault(c => c.kind == NataneMigrationChangeKind.ClipBinding);
            Assert.IsNotNull(change, "Clip binding 張替え候補が生成されていません");
            Assert.AreEqual("_Glossiness", change.newName);
        }

        // ---- 適用 → 履歴 → ロールバックで前値復元 ----

        [Test]
        public void ApplyThenRollback_RestoresPreviousValue()
        {
            Material material = CreateMaterial("Cycle");
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Glossiness", 0.1f); // 適用前の新プロパティ値
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var defs = new List<NataneMigrationDefinition> { PropRename("_Metallic", "_Glossiness") };
            NataneMigrationPlan plan = NataneMigrationService.BuildMigrationPlan(defs, new[] { material }, null);
            Assert.AreEqual(1, plan.changes.Count);

            NataneMigrationApplyResult applied = NataneMigrationService.Apply(plan);
            Assert.AreEqual(1, applied.appliedCount);
            Assert.IsFalse(string.IsNullOrEmpty(applied.batchId));
            Assert.AreEqual(0.8f, material.GetFloat("_Glossiness"), 1e-5f, "適用後に新プロパティへ旧値が写っていません");

            NataneMigrationApplyResult rolled = NataneMigrationService.RollbackBatch(applied.batchId);
            Assert.IsEmpty(rolled.errors);
            Assert.AreEqual(0.1f, material.GetFloat("_Glossiness"), 1e-5f, "ロールバックで前値が復元されていません");
        }

        // ---- 空定義ではプラン 0 件 ----

        [Test]
        public void BuildPlan_EmptyDefinitions_ProducesNoChanges()
        {
            Material material = CreateMaterial("Empty");
            var plan = NataneMigrationService.BuildMigrationPlan(new List<NataneMigrationDefinition>(), new[] { material }, null);
            Assert.AreEqual(0, plan.changes.Count);
        }
    }
}
