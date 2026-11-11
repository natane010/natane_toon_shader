using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NataneToon.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// Scene Workspace サービスのテスト。実 Scene 開閉は不安定なため、サービスを検証可能に分割して純データ中心に検証する。
    /// 実 Scene を要する検証(現構成キャプチャ)のみ一時 Scene を生成し、TearDown で必ず削除する。
    /// </summary>
    public class NataneSceneWorkspaceTests
    {
        private const string Root = "Assets/NataneSceneWorkspaceTest";

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            AssetDatabase.CreateFolder("Assets", "NataneSceneWorkspaceTest");
        }

        [TearDown]
        public void TearDown()
        {
            // 空の Single Scene に戻してから一時アセットを削除（開いたままだと削除できない）。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DeleteRoot();
            SessionState.EraseString(NataneSceneWorkspaceService.PrevSnapshotSessionKey);
        }

        private static void DeleteRoot()
        {
            if (AssetDatabase.IsValidFolder(Root))
            {
                AssetDatabase.DeleteAsset(Root);
            }
            AssetDatabase.Refresh();
        }

        // 一時 Scene を作成して保存し、GUID を返す。
        private static string CreateScene(string relativeName, NewSceneMode mode)
        {
            string path = Root + "/" + relativeName + ".unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            EditorSceneManager.SaveScene(scene, path);
            return AssetDatabase.AssetPathToGUID(path);
        }

        // ---- Profile シリアライズ往復（GUID 保持）----

        [Test]
        public void Profile_SerializationRoundTrip_PreservesGuids()
        {
            var profile = ScriptableObject.CreateInstance<NataneSceneProfile>();
            profile.SceneEntries.Add(new NataneSceneEntry { sceneGuid = "aaaa1111", loadMode = NataneSceneLoadMode.Single, isActiveScene = true });
            profile.SceneEntries.Add(new NataneSceneEntry { sceneGuid = "bbbb2222", loadMode = NataneSceneLoadMode.Additive, isActiveScene = false });
            profile.PlayModeStartSceneGuid = "pm3333";
            profile.LightingSceneGuid = "lt4444";
            profile.RelatedPrefabGuids.Add("pf5555");
            profile.IsFavorite = true;

            string json = JsonUtility.ToJson(profile);
            var restored = ScriptableObject.CreateInstance<NataneSceneProfile>();
            JsonUtility.FromJsonOverwrite(json, restored);

            Assert.AreEqual(2, restored.SceneEntries.Count);
            Assert.AreEqual("aaaa1111", restored.SceneEntries[0].sceneGuid);
            Assert.AreEqual(NataneSceneLoadMode.Single, restored.SceneEntries[0].loadMode);
            Assert.IsTrue(restored.SceneEntries[0].isActiveScene);
            Assert.AreEqual("bbbb2222", restored.SceneEntries[1].sceneGuid);
            Assert.AreEqual(NataneSceneLoadMode.Additive, restored.SceneEntries[1].loadMode);
            Assert.AreEqual("pm3333", restored.PlayModeStartSceneGuid);
            Assert.AreEqual("lt4444", restored.LightingSceneGuid);
            CollectionAssert.Contains(restored.RelatedPrefabGuids, "pf5555");
            Assert.IsTrue(restored.IsFavorite);

            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(restored);
        }

        // ---- GUID 解決失敗は警告リストへ（例外にならない）----

        [Test]
        public void ResolveProfile_UnresolvableGuid_GoesToWarningsNotException()
        {
            var profile = ScriptableObject.CreateInstance<NataneSceneProfile>();
            profile.SceneEntries.Add(new NataneSceneEntry { sceneGuid = "does_not_exist_guid", loadMode = NataneSceneLoadMode.Additive });
            profile.PlayModeStartSceneGuid = "also_missing";

            NataneSceneResolveResult result = null;
            Assert.DoesNotThrow(() => result = NataneSceneWorkspaceService.ResolveProfile(profile));
            Assert.IsFalse(result.HasAnyScene, "解決不能な GUID が解決済みに含まれています");
            Assert.IsNotEmpty(result.warnings, "解決失敗が警告に入っていません");

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void BuildApplyPlan_AllUnresolvable_ReturnsEmptyPlanWithWarnings()
        {
            var profile = ScriptableObject.CreateInstance<NataneSceneProfile>();
            profile.SceneEntries.Add(new NataneSceneEntry { sceneGuid = "nope", loadMode = NataneSceneLoadMode.Single });

            NataneSceneApplyPlan plan = NataneSceneWorkspaceService.BuildApplyPlan(profile);
            Assert.IsFalse(plan.HasAnyScene);
            Assert.IsNotEmpty(plan.warnings);

            Object.DestroyImmediate(profile);
        }

        // ---- 現構成キャプチャ（一時 Scene 2 つ・Additive 構成）----

        [Test]
        public void CaptureCurrentAsProfile_RecordsOrderModeAndActive()
        {
            string guid1 = CreateScene("Base", NewSceneMode.Single);
            string guid2 = CreateScene("Extra", NewSceneMode.Additive);

            // Additive 側を Active にして、Active 検出も併せて確認する。
            Scene extra = SceneManager.GetSceneByPath(Root + "/Extra.unity");
            EditorSceneManager.SetActiveScene(extra);

            string profilePath = Root + "/Captured.asset";
            NataneSceneProfile profile = NataneSceneWorkspaceService.CaptureCurrentAsProfile(profilePath);

            Assert.AreEqual(2, profile.SceneEntries.Count, "エントリ数が現構成と一致しません");
            Assert.AreEqual(guid1, profile.SceneEntries[0].sceneGuid);
            Assert.AreEqual(NataneSceneLoadMode.Single, profile.SceneEntries[0].loadMode, "先頭は Single であるべき");
            Assert.AreEqual(guid2, profile.SceneEntries[1].sceneGuid);
            Assert.AreEqual(NataneSceneLoadMode.Additive, profile.SceneEntries[1].loadMode, "2 件目は Additive であるべき");

            NataneSceneEntry active = profile.SceneEntries.FirstOrDefault(e => e.isActiveScene);
            Assert.IsNotNull(active, "Active Scene が記録されていません");
            Assert.AreEqual(guid2, active.sceneGuid, "Active Scene の記録が一致しません");
        }

        // ---- RestorePrevious スナップショット往復（純データ）----

        [Test]
        public void Snapshot_SerializationRoundTrip_PreservesData()
        {
            var snapshot = new NataneSceneSnapshot();
            snapshot.entries.Add(new NataneSceneSnapshotEntry { path = "Assets/A.unity", guid = "g-a", isLoaded = true, isActive = false });
            snapshot.entries.Add(new NataneSceneSnapshotEntry { path = "Assets/B.unity", guid = "g-b", isLoaded = true, isActive = true });

            string json = NataneSceneWorkspaceService.SerializeSnapshot(snapshot);
            NataneSceneSnapshot back = NataneSceneWorkspaceService.DeserializeSnapshot(json);

            Assert.IsNotNull(back);
            Assert.AreEqual(2, back.entries.Count);
            Assert.AreEqual("Assets/A.unity", back.entries[0].path);
            Assert.AreEqual("g-a", back.entries[0].guid);
            Assert.AreEqual("Assets/B.unity", back.entries[1].path);
            Assert.IsTrue(back.entries[1].isActive);
        }

        [Test]
        public void DeserializeSnapshot_SchemaMismatch_ReturnsNull()
        {
            // schemaVersion を意図的にずらしたデータは復元に使わない（安全側）。
            string json = "{\"schemaVersion\":99,\"entries\":[]}";
            Assert.IsNull(NataneSceneWorkspaceService.DeserializeSnapshot(json));
            Assert.IsNull(NataneSceneWorkspaceService.DeserializeSnapshot(null));
        }

        // ---- 呼び出し分離: ApplyProfile は ApplyToBuildSettings を呼ばない ----

        [Test]
        public void ApplyProfile_DoesNotTouchBuildSettings()
        {
            string guid = CreateScene("Applied", NewSceneMode.Single);
            var profile = ScriptableObject.CreateInstance<NataneSceneProfile>();
            profile.SceneEntries.Add(new NataneSceneEntry { sceneGuid = guid, loadMode = NataneSceneLoadMode.Single, isActiveScene = true });

            int before = NataneSceneWorkspaceService.BuildSettingsMutationCount;

            // dirtyGuard=続行, executor=no-op で Scene 開閉を伴わずに適用経路のみ走らせる。
            NataneSceneApplyResult result = NataneSceneWorkspaceService.ApplyProfileInternal(
                profile, () => true, _ => { });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(before, NataneSceneWorkspaceService.BuildSettingsMutationCount,
                "ApplyProfile 経路で Build Settings が変更されました");

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ApplyToBuildSettings_IsSeparatePublicMethod_AndActuallyMutates()
        {
            // 構造確認: 2 つは別々の public static メソッドとして存在する。
            MethodInfo apply = typeof(NataneSceneWorkspaceService).GetMethod(
                "ApplyProfile", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(NataneSceneProfile) }, null);
            MethodInfo toBuild = typeof(NataneSceneWorkspaceService).GetMethod(
                "ApplyToBuildSettings", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(NataneSceneProfile) }, null);
            Assert.IsNotNull(apply);
            Assert.IsNotNull(toBuild);
            Assert.AreNotEqual(apply, toBuild);

            // カウンタが実際に意味を持つ（ApplyToBuildSettings でのみ増える）ことを確認。
            string guid = CreateScene("BuildScene", NewSceneMode.Single);
            var profile = ScriptableObject.CreateInstance<NataneSceneProfile>();
            profile.SceneEntries.Add(new NataneSceneEntry { sceneGuid = guid, loadMode = NataneSceneLoadMode.Single });

            EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            try
            {
                int before = NataneSceneWorkspaceService.BuildSettingsMutationCount;
                NataneSceneWorkspaceService.ApplyToBuildSettings(profile);
                Assert.AreEqual(before + 1, NataneSceneWorkspaceService.BuildSettingsMutationCount);
            }
            finally
            {
                // Build Settings をテスト前へ戻す（スクラッチプロジェクトを汚さない）。
                EditorBuildSettings.scenes = original;
                Object.DestroyImmediate(profile);
            }
        }
    }
}
