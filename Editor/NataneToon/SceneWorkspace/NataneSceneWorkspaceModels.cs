using System;
using System.Collections.Generic;

namespace NataneToon.Editor
{
    // Scene の開き方。順序は Profile のリスト順で表現する。
    public enum NataneSceneLoadMode
    {
        Single,
        Additive
    }

    /// <summary>
    /// Profile 内の 1 Scene エントリ。SceneAsset は GUID で保持し、移動追従を GUID に委ねる。
    /// </summary>
    [Serializable]
    public sealed class NataneSceneEntry
    {
        public string sceneGuid;
        public NataneSceneLoadMode loadMode = NataneSceneLoadMode.Additive;
        public bool isActiveScene;
    }

    /// <summary>
    /// Profile を実パスへ解決した結果。解決失敗は例外にせず warnings へ入れる（安全側）。
    /// </summary>
    public sealed class NataneSceneResolveResult
    {
        public readonly List<NataneResolvedScene> scenes = new List<NataneResolvedScene>();
        public readonly List<string> warnings = new List<string>();
        public string playModeStartScenePath;
        public string lightingScenePath;
        public string debugScenePath;

        public bool HasAnyScene => scenes.Count > 0;
    }

    // 解決済み Scene 1 件（実パス付き）。
    public sealed class NataneResolvedScene
    {
        public string guid;
        public string path;
        public NataneSceneLoadMode loadMode;
        public bool isActive;
    }

    /// <summary>
    /// ApplyProfile が組み立てる実行計画。ここには BuildSettings への変更は一切含めない
    /// （Build Settings 反映は ApplyToBuildSettings のみが担当し、ApplyProfile からは触らない）。
    /// </summary>
    public sealed class NataneSceneApplyPlan
    {
        // 先頭 = Single で開く Scene、以降 = Additive。順序は Profile 由来。
        public readonly List<NataneResolvedScene> ordered = new List<NataneResolvedScene>();
        public string activeScenePath;
        public string playModeStartScenePath;
        public readonly List<string> warnings = new List<string>();

        public bool HasAnyScene => ordered.Count > 0;
    }

    /// <summary>
    /// ApplyProfile / RestorePrevious の結果サマリ。
    /// </summary>
    public sealed class NataneSceneApplyResult
    {
        // 未保存 Scene 保護でキャンセルされた、または解決全滅で中断した場合 true。
        public bool aborted;
        public string abortReason;
        public int openedCount;
        public string activeScenePath;
        public readonly List<string> warnings = new List<string>();

        public bool Success => !aborted;
    }

    // ===== 直前構成スナップショット（SessionState 保存, schemaVersion 付き純データ）=====

    [Serializable]
    public sealed class NataneSceneSnapshot
    {
        public int schemaVersion = 1;
        public List<NataneSceneSnapshotEntry> entries = new List<NataneSceneSnapshotEntry>();
    }

    [Serializable]
    public sealed class NataneSceneSnapshotEntry
    {
        // パスと GUID を両方保持。パスが動いても GUID で復元を試みられるようにする。
        public string path;
        public string guid;
        public bool isLoaded;
        public bool isActive;
    }
}
