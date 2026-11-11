using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// ユーザーが Assets/ 側へ保存する Scene 構成プロファイル。
    /// SceneAsset 参照はすべて GUID で保持し、Scene 移動/改名への追従を GUID に委ねる。
    /// Build Settings とは独立（反映は明示操作の ApplyToBuildSettings のみ）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "NataneSceneProfile",
        menuName = "Natane Toon/Scene Profile",
        order = 401)]
    public sealed class NataneSceneProfile : ScriptableObject
    {
        [SerializeField] private List<NataneSceneEntry> sceneEntries = new List<NataneSceneEntry>();
        // 任意。Lighting / Debug 用 Scene の GUID（未指定は空）。
        [SerializeField] private string lightingSceneGuid;
        [SerializeField] private string debugSceneGuid;
        // EditorSceneManager.playModeStartScene 用 Scene の GUID（任意）。
        [SerializeField] private string playModeStartSceneGuid;
        // 関連 Prefab / キャラクターの GUID（ping 用）。
        [SerializeField] private List<string> relatedPrefabGuids = new List<string>();
        [SerializeField] private bool isFavorite;

        public List<NataneSceneEntry> SceneEntries => sceneEntries;
        public List<string> RelatedPrefabGuids => relatedPrefabGuids;

        public string LightingSceneGuid { get => lightingSceneGuid; set => lightingSceneGuid = value; }
        public string DebugSceneGuid { get => debugSceneGuid; set => debugSceneGuid = value; }
        public string PlayModeStartSceneGuid { get => playModeStartSceneGuid; set => playModeStartSceneGuid = value; }
        public bool IsFavorite { get => isFavorite; set => isFavorite = value; }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
    }
}
