using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    internal enum NataneBuildMode
    {
        Passive,
        AutoPrepare,
        Strict
    }

    // ビルド最適化の段階モード。Aggressive は Stage A では未実装（Safe 相当で動作）。
    public enum NataneOptimizationMode
    {
        Disabled,
        ReportOnly,
        Safe,
        Aggressive
    }

    // Snapshot 失敗 / Registry 不整合時の方針。
    public enum NataneStrictFailurePolicy
    {
        FallbackKeepAll,
        FailBuild
    }

    // 未知キーワードの扱い。将来拡張用に enum 化（現状 Keep のみ）。
    public enum NataneUnknownKeywordPolicy
    {
        Keep
    }

    // HLSL フィーチャーガード。Stage E で参照。Stage A では表示のみで挙動不変。
    public enum NataneHlslFeatureGuardMode
    {
        Off,
        Advanced
    }

    [FilePath("ProjectSettings/NataneToonBuildPolicy.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class NataneBuildPolicySettings : ScriptableSingleton<NataneBuildPolicySettings>
    {
        [SerializeField] private NataneBuildMode buildMode = NataneBuildMode.AutoPrepare;
        [SerializeField] private bool autoIndexInEditor = true;
        [SerializeField] private int pageSize = 100;
        [SerializeField] private int issueSampleLimit = 500;
        [SerializeField] private int editorUpdateBudgetMs = 8;
        [SerializeField] private int fullRebuildDirtyThreshold = 10000;
        [SerializeField] private List<string> indexedMaterialFolders = new List<string> { "Assets" };
        [SerializeField] private List<string> indexedPrefabFolders = new List<string> { "Assets" };

        // --- ビルド最適化設定（新規導入時の標準: Safe / Prewarm OFF / Keep All / Guard Off） ---
        [SerializeField] private NataneOptimizationMode optimizationMode = NataneOptimizationMode.Safe;
        [SerializeField] private NataneStrictFailurePolicy strictFailurePolicy = NataneStrictFailurePolicy.FallbackKeepAll;
        [SerializeField] private NataneUnknownKeywordPolicy unknownKeywordPolicy = NataneUnknownKeywordPolicy.Keep;
        [SerializeField] private NataneHlslFeatureGuardMode hlslFeatureGuardMode = NataneHlslFeatureGuardMode.Off;
        [SerializeField] private bool buildPrewarmEnabled = false;
        [SerializeField] private bool prewarmAutoFindVariants = false;
        [SerializeField] private bool buildTextureConsolidation = true;

        // 差分式ストリッパー(NataneShaderVariantStripper)の有効フラグ。旧 DisableVariantStripping の反転。
        [SerializeField] private bool variantStrippingEnabled = true;
        // レガシー完全一致式ストリッパー(Tools/ShaderVariantStripper)の有効フラグ。
        [SerializeField] private bool legacyExactSetStrippingEnabled = false;

        // 消費は後続ステージ。Stage A では保存と UI 編集のみ。
        [SerializeField] private List<string> alwaysKeepKeywords = new List<string>();
        [SerializeField] private List<string> alwaysKeepShaderGuids = new List<string>();
        [SerializeField] private List<string> alwaysKeepMaterialGuids = new List<string>();
        [SerializeField] private List<string> shaderVariantCollectionGuids = new List<string>();
        [SerializeField] private List<string> runtimeDynamicKeywords = new List<string>();

        // 旧 EditorPrefs からの一度きり移行のバージョン管理（0→1）。
        [SerializeField] private int migratedLegacyEditorPrefsVersion = 0;

        private const int CurrentLegacyMigrationVersion = 1;

        internal NataneBuildMode BuildMode
        {
            get => buildMode;
            set => buildMode = value;
        }

        internal bool AutoIndexInEditor
        {
            get => autoIndexInEditor;
            set => autoIndexInEditor = value;
        }

        public int PageSize
        {
            get => Mathf.Max(25, pageSize);
            set => pageSize = Mathf.Max(25, value);
        }

        public int IssueSampleLimit
        {
            get => Mathf.Max(50, issueSampleLimit);
            set => issueSampleLimit = Mathf.Max(50, value);
        }

        internal int EditorUpdateBudgetMs
        {
            get => Mathf.Clamp(editorUpdateBudgetMs, 1, 50);
            set => editorUpdateBudgetMs = Mathf.Clamp(value, 1, 50);
        }

        internal int FullRebuildDirtyThreshold
        {
            get => Mathf.Max(1000, fullRebuildDirtyThreshold);
            set => fullRebuildDirtyThreshold = Mathf.Max(1000, value);
        }

        internal IReadOnlyList<string> IndexedMaterialFolders => indexedMaterialFolders;
        internal IReadOnlyList<string> IndexedPrefabFolders => indexedPrefabFolders;

        // 他アセンブリ(Tools / VRChat)から参照されるため public。
        public NataneOptimizationMode OptimizationMode
        {
            get => optimizationMode;
            set => optimizationMode = value;
        }

        public NataneStrictFailurePolicy StrictFailurePolicy
        {
            get => strictFailurePolicy;
            set => strictFailurePolicy = value;
        }

        public NataneUnknownKeywordPolicy UnknownKeywordPolicy
        {
            get => unknownKeywordPolicy;
            set => unknownKeywordPolicy = value;
        }

        public NataneHlslFeatureGuardMode HlslFeatureGuardMode
        {
            get => hlslFeatureGuardMode;
            set => hlslFeatureGuardMode = value;
        }

        public bool BuildPrewarmEnabled
        {
            get => buildPrewarmEnabled;
            set => buildPrewarmEnabled = value;
        }

        public bool PrewarmAutoFindVariants
        {
            get => prewarmAutoFindVariants;
            set => prewarmAutoFindVariants = value;
        }

        public bool BuildTextureConsolidation
        {
            get => buildTextureConsolidation;
            set => buildTextureConsolidation = value;
        }

        public bool VariantStrippingEnabled
        {
            get => variantStrippingEnabled;
            set => variantStrippingEnabled = value;
        }

        public bool LegacyExactSetStrippingEnabled
        {
            get => legacyExactSetStrippingEnabled;
            set => legacyExactSetStrippingEnabled = value;
        }

        // UI から add/remove できるよう実体リストを返す（編集後は SaveSettings を呼ぶこと）。
        public List<string> AlwaysKeepKeywords => alwaysKeepKeywords;
        public List<string> AlwaysKeepShaderGuids => alwaysKeepShaderGuids;
        public List<string> AlwaysKeepMaterialGuids => alwaysKeepMaterialGuids;
        public List<string> ShaderVariantCollectionGuids => shaderVariantCollectionGuids;
        public List<string> RuntimeDynamicKeywords => runtimeDynamicKeywords;

        public string[] GetMaterialSearchFolders()
        {
            return SanitizeFolders(indexedMaterialFolders);
        }

        public string[] GetPrefabSearchFolders()
        {
            return SanitizeFolders(indexedPrefabFolders);
        }

        public void SaveSettings()
        {
            Save(true);
        }

        // エディタ起動 / ドメインリロード時に一度だけ移行を試みる。
        [InitializeOnLoadMethod]
        private static void MigrateOnLoad()
        {
            instance.MigrateLegacyEditorPrefsIfNeeded();
        }

        /// <summary>
        /// 旧 EditorPrefs（ビルド結果に影響していたキー）から設定資産へ一度だけ移行する。
        /// 旧キーは他バージョンとの共存のため削除しない。
        /// </summary>
        internal void MigrateLegacyEditorPrefsIfNeeded()
        {
            if (migratedLegacyEditorPrefsVersion >= CurrentLegacyMigrationVersion)
                return;

            // プリウォームは新方針で既定 OFF。旧既定(true)は引き継がない。
            // 「旧キーが存在し値が false なら false を維持、存在しない/true でも新既定 false を採用」だが、
            // 新既定も false のため結果は常に false となる（意図的な降格）。
            buildPrewarmEnabled = false;

            // 旧オプトアウト(DisableVariantStripping, 既定 false)を反転して有効フラグへ。
            bool legacyDisableStripping = EditorPrefs.GetBool("NataneToon_DisableVariantStripping", false);
            variantStrippingEnabled = !legacyDisableStripping;

            // レガシー完全一致式ストリッパーの有効フラグ（旧既定 false を尊重）。
            legacyExactSetStrippingEnabled = EditorPrefs.GetBool("NataneToon_VariantStrippingEnabled", false);

            // ビルド時テクスチャ統合（旧既定 true を尊重）。
            buildTextureConsolidation = EditorPrefs.GetBool("NataneToon_BuildTextureConsolidation", true);

            migratedLegacyEditorPrefsVersion = CurrentLegacyMigrationVersion;
            Save(true);
        }

        private static string[] SanitizeFolders(List<string> source)
        {
            var folders = new List<string>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    string folder = NormalizeFolder(source[i]);
                    if (!string.IsNullOrEmpty(folder) && !folders.Contains(folder))
                    {
                        folders.Add(folder);
                    }
                }
            }

            if (folders.Count == 0)
            {
                folders.Add("Assets");
            }

            return folders.ToArray();
        }

        private static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return null;
            }

            folder = folder.Replace("\\", "/").Trim();
            while (folder.EndsWith("/"))
            {
                folder = folder.Substring(0, folder.Length - 1);
            }

            return folder;
        }
    }
}
