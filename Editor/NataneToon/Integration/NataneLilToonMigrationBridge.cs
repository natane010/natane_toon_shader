using System;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// インスペクター（NataneToon.Editor）から lilToon 変換（NataneToon.Editor.Migration）を呼ぶための橋。
    ///
    /// Migration アセンブリは Editor アセンブリを参照しているため、逆向きには直接呼べない。
    /// Migration 側が起動時にここへ処理を登録し、インスペクター側は登録があるときだけ使う。
    /// Migration アセンブリが無い / コンパイルできていない構成でも、インスペクターは壊れない。
    /// </summary>
    public static class NataneLilToonMigrationBridge
    {
        private const string AutoConvertPrefsKey = "NataneToon_AutoConvertLilToonOnShaderChange";

        /// <summary>
        /// lilToon のままのマテリアルを、その場で Natane Toon へ変換する。
        /// 第 2 引数は変換先シェーダー（null なら自動判定）。成功したら true。
        /// </summary>
        public static Func<Material, Shader, bool> ConvertInPlace;

        public static bool IsAvailable { get { return ConvertInPlace != null; } }

        /// <summary>シェーダー欄で lilToon から切り替えたとき、設定を自動で引き継ぐか。既定は有効。</summary>
        public static bool AutoConvertOnShaderChange
        {
            get { return EditorPrefs.GetBool(AutoConvertPrefsKey, true); }
            set { EditorPrefs.SetBool(AutoConvertPrefsKey, value); }
        }

        /// <summary>
        /// シェーダー欄の切り替えを変換として扱えるなら変換して true を返す。
        /// false のときは呼び出し側が通常のシェーダー割り当てを行う。
        /// </summary>
        public static bool TryConvertOnShaderChange(Material material, Shader oldShader, Shader newShader)
        {
            if (!IsAvailable || !AutoConvertOnShaderChange) return false;
            if (material == null || oldShader == null || newShader == null) return false;
            if (!NataneShaderCatalog.IsLilToonShader(oldShader.name)) return false;
            if (!NataneShaderCatalog.IsNataneShader(newShader.name)) return false;
            // この時点でマテリアルがまだ lilToon のままであること（プロパティを読めること）が前提。
            if (material.shader != oldShader) return false;

            // 基本の不透明シェーダーを選んだ場合は「Natane Toon にしたい」という意図とみなし、
            // カットアウト / 半透明を元マテリアルから自動判定する。明示的に別バリアントを
            // 選んだ場合はその選択を尊重する。
            Shader target = newShader.name == "Natane/Toon Shader" ? null : newShader;

            try
            {
                return ConvertInPlace(material, target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NataneToon] lilToon からの自動引き継ぎに失敗したため、通常のシェーダー切り替えを行います: " + ex.Message);
                return false;
            }
        }
    }
}
