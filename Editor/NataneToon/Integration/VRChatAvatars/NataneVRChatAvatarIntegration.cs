// このファイルは NataneToon.Editor.VRChatAvatars アセンブリにある。
// asmdef の versionDefines が com.vrchat.avatars の存在を NATANE_VRC_AVATARS へ変換し、
// defineConstraints がそれを要求しているため、Avatars SDK が無いプロジェクト
// （ワールド専用など）ではアセンブリごとコンパイル対象外になる。
// VRC_SDK_VRCSDK3 だけでは足りない: ワールド専用プロジェクトでもこの define は立つが、
// VRC.SDK3.Avatars の型は存在しない。
#if NATANE_VRC_AVATARS

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace NataneToon.Editor
{
    /// <summary>
    /// VRChat アバターへの組み込み。
    ///
    /// このファイルは <c>defineConstraints: ["VRC_SDK_VRCSDK3"]</c> のアセンブリにあるため、
    /// SDK 非導入プロジェクトではそもそもコンパイルされない。Dissolve Studio 側は
    /// <see cref="NataneAvatarIntegrationBridge"/> 越しに呼ぶので、
    /// SDK の有無で分岐を書く必要がない。
    ///
    /// Modular Avatar がある場合は非破壊構成（MA コンポーネント）で組む。
    /// 無い場合は FX レイヤーへ直接書き込むが、その前に必ずバックアップを取る。
    /// </summary>
    [InitializeOnLoad]
    internal sealed class NataneVRChatAvatarIntegration : INataneAvatarIntegration
    {
        // Modular Avatar のコンポーネント型。アセンブリ参照を足さずに済むよう型名で引く。
        // MA のバージョン差で名前空間が変わると null になるが、その場合は
        // 素直に「MA 無し」として扱えばよい（壊れるより退化するほうが安全）。
        private const string MaMergeAnimatorType = "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator, nadena.dev.modular-avatar.core";
        private const string MaParametersType = "nadena.dev.modular_avatar.core.ModularAvatarParameters, nadena.dev.modular-avatar.core";
        private const string MaMenuItemType = "nadena.dev.modular_avatar.core.ModularAvatarMenuItem, nadena.dev.modular-avatar.core";

        static NataneVRChatAvatarIntegration()
        {
            NataneAvatarIntegrationBridge.Register(new NataneVRChatAvatarIntegration());
        }

        public bool IsAvailable => true;

        public bool HasModularAvatar => ResolveType(MaMergeAnimatorType) != null;

        public NataneAvatarWireResult WireToggle(
            GameObject avatarRoot,
            RuntimeAnimatorController controller,
            string parameterName,
            bool isFloatParameter,
            string menuLabel)
        {
            if (avatarRoot == null || controller == null)
            {
                return Fail("アバターのルートと AnimatorController を指定してください。");
            }

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                return Fail("VRCAvatarDescriptor が見つかりません。アバターのルートを指定してください。");
            }

            return HasModularAvatar
                ? WireWithModularAvatar(avatarRoot, controller, parameterName, isFloatParameter, menuLabel)
                : WireDirectly(descriptor, controller, parameterName, isFloatParameter, menuLabel);
        }

        // ---- Modular Avatar 経由（非破壊）----

        private NataneAvatarWireResult WireWithModularAvatar(
            GameObject avatarRoot,
            RuntimeAnimatorController controller,
            string parameterName,
            bool isFloatParameter,
            string menuLabel)
        {
            Type mergeType = ResolveType(MaMergeAnimatorType);
            Type parametersType = ResolveType(MaParametersType);
            Type menuItemType = ResolveType(MaMenuItemType);

            if (mergeType == null)
            {
                return Fail("Modular Avatar の型を解決できませんでした。");
            }

            var host = new GameObject("Natane Dissolve (MA)");
            Undo.RegisterCreatedObjectUndo(host, "Wire Dissolve (Modular Avatar)");
            host.transform.SetParent(avatarRoot.transform, false);

            Component merge = Undo.AddComponent(host, mergeType);
            SetMember(merge, "animator", controller);
            // FX レイヤーへマージする。MergeAnimatorMode / layerType はバージョンで
            // 名前が違うことがあるため、設定できたものだけを反映する。
            SetEnumMember(merge, "layerType", "FX");
            SetMember(merge, "deleteAttachedAnimator", true);
            SetMember(merge, "matchAvatarWriteDefaults", false);

            if (parametersType != null)
            {
                Undo.AddComponent(host, parametersType);
                // パラメータの中身は MA のバージョン差が大きい（parameters リストの要素型が
                // 内部クラス）。ここで無理に組むと壊れやすいので、コンポーネントだけ足して
                // 中身はユーザーに任せ、その旨をメッセージで伝える。
            }

            if (menuItemType != null)
            {
                var menuHost = new GameObject(string.IsNullOrEmpty(menuLabel) ? "Dissolve" : menuLabel);
                Undo.RegisterCreatedObjectUndo(menuHost, "Wire Dissolve (Modular Avatar)");
                menuHost.transform.SetParent(host.transform, false);
                Undo.AddComponent(menuHost, menuItemType);
            }

            Selection.activeGameObject = host;

            return new NataneAvatarWireResult
            {
                Success = true,
                UsedModularAvatar = true,
                Message =
                    "Modular Avatar で非破壊に組みました（アバター本体の FX レイヤーは変更していません）。\n" +
                    $"パラメータ名: {parameterName} / 型: {(isFloatParameter ? "float (8bit)" : "bool (1bit)")}\n" +
                    "MA Parameters と MA Menu Item の中身（既定値・同期設定・アイコン）は、" +
                    "MA のバージョン差が大きいためインスペクタで確認してください。"
            };
        }

        // ---- FX レイヤーへ直接書き込み（破壊的）----

        private NataneAvatarWireResult WireDirectly(
            VRCAvatarDescriptor descriptor,
            RuntimeAnimatorController controller,
            string parameterName,
            bool isFloatParameter,
            string menuLabel)
        {
            VRCAvatarDescriptor.CustomAnimLayer[] layers = descriptor.baseAnimationLayers;
            int fxIndex = Array.FindIndex(layers,
                l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);

            if (fxIndex < 0)
            {
                return Fail("FX レイヤーが見つかりませんでした。");
            }

            var fxController = layers[fxIndex].animatorController as AnimatorController;
            if (fxController == null)
            {
                return Fail("FX レイヤーに AnimatorController が設定されていません。");
            }

            // 破壊的な書き込みの前にバックアップ。ここを飛ばすと、
            // 失敗したときにユーザーが元へ戻す手段を失う。
            string backupPath = BackupAsset(fxController);

            var newController = controller as AnimatorController;
            if (newController == null)
            {
                return Fail("生成した AnimatorController を解決できませんでした。");
            }

            Undo.RecordObject(fxController, "Wire Dissolve into FX layer");

            // パラメータ。同名があれば追加しない（重複するとアップロードで弾かれる）。
            if (!fxController.parameters.Any(p => p.name == parameterName))
            {
                fxController.AddParameter(parameterName,
                    isFloatParameter ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool);
            }

            foreach (AnimatorControllerLayer layer in newController.layers)
            {
                fxController.AddLayer(layer);
            }

            EditorUtility.SetDirty(fxController);
            AssetDatabase.SaveAssets();

            string menuNote = AppendExpressionParameter(descriptor, parameterName, isFloatParameter, menuLabel);

            return new NataneAvatarWireResult
            {
                Success = true,
                UsedModularAvatar = false,
                BackupPath = backupPath,
                Message =
                    $"FX レイヤーへ直接書き込みました。パラメータ名: {parameterName} / " +
                    $"型: {(isFloatParameter ? "float (8bit)" : "bool (1bit)")}\n" + menuNote
            };
        }

        /// <summary>
        /// Expression Parameters へ追加し、残りメモリを報告する。
        /// メニュー項目までは自動生成しない（既存メニュー構成を壊しうるため）。
        /// </summary>
        private static string AppendExpressionParameter(
            VRCAvatarDescriptor descriptor, string parameterName, bool isFloatParameter, string menuLabel)
        {
            VRCExpressionParameters parameters = descriptor.expressionParameters;
            if (parameters == null)
            {
                return "Expression Parameters が未設定のため、パラメータの登録は行いませんでした。";
            }

            if (parameters.parameters != null &&
                parameters.parameters.Any(p => p != null && p.name == parameterName))
            {
                return $"パラメータ {parameterName} は既に登録済みです。";
            }

            Undo.RecordObject(parameters, "Add Dissolve Parameter");

            var list = parameters.parameters?.ToList() ?? new System.Collections.Generic.List<VRCExpressionParameters.Parameter>();
            list.Add(new VRCExpressionParameters.Parameter
            {
                name = parameterName,
                valueType = isFloatParameter
                    ? VRCExpressionParameters.ValueType.Float
                    : VRCExpressionParameters.ValueType.Bool,
                saved = true,
                defaultValue = 0f,
                networkSynced = true
            });
            parameters.parameters = list.ToArray();

            EditorUtility.SetDirty(parameters);
            AssetDatabase.SaveAssets();

            int used = parameters.CalcTotalCost();
            int max = VRCExpressionParameters.MAX_PARAMETER_COST;

            return $"Expression Parameters へ追加しました（{used} / {max} bit 使用中、残り {max - used} bit）。\n" +
                   $"メニュー項目「{menuLabel}」は既存メニュー構成を壊さないよう自動追加していません。" +
                   "Expression Menu に手動で追加してください。";
        }

        private static string BackupAsset(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return null;

            string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(dir)) return null;

            string backupPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}_NataneBackup{ext}");
            return AssetDatabase.CopyAsset(path, backupPath) ? backupPath : null;
        }

        // ---- 反射ヘルパ ----

        private static Type ResolveType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            if (type != null) return type;

            // アセンブリ名が違う版もあるので、型名だけで全アセンブリを走査する。
            string typeName = assemblyQualifiedName.Split(',')[0].Trim();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch
                {
                    // 読み込めないアセンブリは無視する（動的生成アセンブリなど）。
                }
            }

            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            Type type = target.GetType();

            var field = type.GetField(name);
            if (field != null && (value == null || field.FieldType.IsInstanceOfType(value)))
            {
                field.SetValue(target, value);
                return;
            }

            var property = type.GetProperty(name);
            if (property != null && property.CanWrite &&
                (value == null || property.PropertyType.IsInstanceOfType(value)))
            {
                property.SetValue(target, value, null);
            }
        }

        private static void SetEnumMember(object target, string name, string enumValueName)
        {
            if (target == null) return;

            var field = target.GetType().GetField(name);
            if (field == null || !field.FieldType.IsEnum) return;

            if (Enum.GetNames(field.FieldType).Contains(enumValueName))
            {
                field.SetValue(target, Enum.Parse(field.FieldType, enumValueName));
            }
        }

        private static NataneAvatarWireResult Fail(string message)
        {
            return new NataneAvatarWireResult { Success = false, Message = message };
        }
    }
}

#endif
