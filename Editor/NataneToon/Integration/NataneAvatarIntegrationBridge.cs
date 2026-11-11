using System;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// アバター側（VRChat SDK / Modular Avatar）への書き込み結果。
    /// </summary>
    public sealed class NataneAvatarWireResult
    {
        public bool Success;

        /// <summary>ユーザーへ見せる要約。失敗理由もここに入れる。</summary>
        public string Message = string.Empty;

        /// <summary>Modular Avatar による非破壊構成で組めたか。</summary>
        public bool UsedModularAvatar;

        /// <summary>FX レイヤーへ直接書き込む前に取ったバックアップのパス（取っていなければ null）。</summary>
        public string BackupPath;
    }

    /// <summary>
    /// アバター用パラメータ／メニューの組み込みを担うハンドラ。
    ///
    /// 実装は VRChat SDK が入っているときだけコンパイルされる
    /// <c>NataneToon.Editor.VRChat</c> アセンブリ側にある。
    /// </summary>
    public interface INataneAvatarIntegration
    {
        /// <summary>この環境で実際に組めるか（SDK の有無など）。</summary>
        bool IsAvailable { get; }

        /// <summary>Modular Avatar が導入されているか。</summary>
        bool HasModularAvatar { get; }

        /// <summary>
        /// AnimatorController をアバターへ接続し、パラメータとメニュー項目を追加する。
        /// </summary>
        NataneAvatarWireResult WireToggle(
            GameObject avatarRoot,
            RuntimeAnimatorController controller,
            string parameterName,
            bool isFloatParameter,
            string menuLabel);
    }

    /// <summary>
    /// アバター統合ハンドラの登録先。
    ///
    /// <c>NataneToon.Editor.Tools</c>（Dissolve Studio がある側）は
    /// <c>NataneToon.Editor.VRChat</c> を参照できない。参照させると、VRChat 側が
    /// <c>NataneToon.Editor</c> を参照している構成と合わせて依存が絡まるうえ、
    /// SDK 非導入プロジェクトでは VRChat 側アセンブリ自体が存在しなくなる
    /// （<c>defineConstraints: ["VRC_SDK_VRCSDK3"]</c>）。
    ///
    /// そこで、両者が参照している <c>NataneToon.Editor</c> に受け口だけを置き、
    /// VRChat 側が <c>[InitializeOnLoad]</c> で自分を登録する。
    /// SDK が無ければ登録が起きず、<see cref="Current"/> は null のままになる。
    /// </summary>
    public static class NataneAvatarIntegrationBridge
    {
        private static INataneAvatarIntegration _current;

        /// <summary>登録済みハンドラ。VRChat SDK 非導入なら null。</summary>
        public static INataneAvatarIntegration Current => _current;

        public static bool IsAvailable => _current != null && _current.IsAvailable;

        public static bool HasModularAvatar => _current != null && _current.HasModularAvatar;

        public static void Register(INataneAvatarIntegration integration)
        {
            if (integration == null)
            {
                return;
            }

            if (_current != null && !ReferenceEquals(_current, integration))
            {
                // 二重登録は設定ミスの兆候なので黙らせない。後勝ちにはする。
                Debug.LogWarning(
                    "[NataneToon] アバター統合ハンドラが複数登録されました。" +
                    $"最後の登録 ({integration.GetType().FullName}) を使用します。");
            }

            _current = integration;
        }

        /// <summary>
        /// ハンドラが無い場合でも呼び出し側が分岐せずに済むよう、
        /// 失敗を表す結果を返す。
        /// </summary>
        public static NataneAvatarWireResult WireToggle(
            GameObject avatarRoot,
            RuntimeAnimatorController controller,
            string parameterName,
            bool isFloatParameter,
            string menuLabel)
        {
            if (_current == null || !_current.IsAvailable)
            {
                return new NataneAvatarWireResult
                {
                    Success = false,
                    Message = "VRChat SDK が見つからないため、アバターへの組み込みは行いませんでした。" +
                              "生成した AnimationClip と AnimatorController はそのまま使えます。"
                };
            }

            try
            {
                return _current.WireToggle(avatarRoot, controller, parameterName, isFloatParameter, menuLabel);
            }
            catch (Exception e)
            {
                // アバター資産の書き換え中の例外を握り潰すと、半端な状態のまま
                // 「成功しました」と出しかねない。明示的に失敗として返す。
                return new NataneAvatarWireResult
                {
                    Success = false,
                    Message = "アバターへの組み込み中にエラーが発生しました: " + e.Message
                };
            }
        }
    }
}
