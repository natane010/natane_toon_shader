using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// ユーザーが Assets/ 側に保存する振り分けルールセット。
    /// Editor 専用型（NataneToon.Editor アセンブリ）として定義し、NataneBuildPolicySettings を
    /// 肥大化させない。pin（固定）した GUID は以後の提案から除外する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "NataneAssetOrganizerRuleSet",
        menuName = "Natane Toon/Asset Organizer Rule Set",
        order = 400)]
    public sealed class NataneAssetOrganizerRuleSet : ScriptableObject
    {
        [SerializeField] private List<NataneAssetOrganizerRule> rules = new List<NataneAssetOrganizerRule>();
        // 提案から除外する固定アセット（GUID で記憶しパス変更に追従）。
        [SerializeField] private List<string> pinnedGuids = new List<string>();

        public List<NataneAssetOrganizerRule> Rules => rules;
        public List<string> PinnedGuids => pinnedGuids;

        public bool IsPinned(string guid)
        {
            return !string.IsNullOrEmpty(guid) && pinnedGuids.Contains(guid);
        }

        public void Pin(string guid)
        {
            if (!string.IsNullOrEmpty(guid) && !pinnedGuids.Contains(guid))
            {
                pinnedGuids.Add(guid);
                Save();
            }
        }

        public void Unpin(string guid)
        {
            if (pinnedGuids.Remove(guid))
            {
                Save();
            }
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>
        /// 既定ルールを流し込む。空のルールセットに対する初期テンプレート。
        /// 実際の移動は提案→確認→適用を経るため、ここでの副作用はメモリ上のみ。
        /// </summary>
        public void PopulateDefaults()
        {
            rules.Clear();
            rules.Add(new NataneAssetOrganizerRule { targetKind = NataneAssetKind.Material, namePattern = "*", destinationFolder = "Assets/Materials" });
            rules.Add(new NataneAssetOrganizerRule { targetKind = NataneAssetKind.Texture, namePattern = "*", destinationFolder = "Assets/Textures" });
            rules.Add(new NataneAssetOrganizerRule { targetKind = NataneAssetKind.Prefab, namePattern = "*", destinationFolder = "Assets/Prefabs" });
            rules.Add(new NataneAssetOrganizerRule { targetKind = NataneAssetKind.AnimationClip, namePattern = "*", destinationFolder = "Assets/Animations" });
        }
    }
}
