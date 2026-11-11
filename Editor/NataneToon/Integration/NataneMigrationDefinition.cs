using System;

namespace NataneToon.Editor
{
    // 値変換方式。None=値を持たない（Keyword/Shader 専用）、CopyAsIs=旧値をそのまま、
    // FloatScale=旧値×scale、Invert=1-旧値（0..1 トグル/スライダーの反転）。
    public enum NataneValueConversion
    {
        None,
        CopyAsIs,
        FloatScale,
        Invert
    }

    /// <summary>
    /// Shader/Property/Keyword の移行定義 1 件。旧新の Shader 名・Property 名・Keyword 名を任意に持つ
    /// （いずれも null 可）。値変換は float プロパティに対してのみ意味を持つ。
    /// 自動適用可否と手動確認要否は分離しており、requiresManualReview の項目はプレビューで既定除外される。
    /// </summary>
    public sealed class NataneMigrationDefinition
    {
        public string oldShaderName;    // null = 全 Natane マテリアルが対象（Shader 差替えなし）
        public string newShaderName;    // null = Shader 名は変更しない
        public string oldPropertyName;  // null = Property 移行なし
        public string newPropertyName;  // oldPropertyName とペアで指定
        public string oldKeyword;       // null = Keyword 移行なし
        public string newKeyword;       // oldKeyword とペアで指定
        public string introducedVersion; // 新名称が導入された package version（不明は null）
        public string removedVersion;     // 旧名称が廃止された package version（不明は null）
        public NataneValueConversion valueConversion = NataneValueConversion.CopyAsIs;
        public float scale = 1f;         // FloatScale の係数
        public bool autoApplicable;      // 確認後に一括適用してよいか
        public bool requiresManualReview; // true=プレビューで既定除外・危険強調
        public string note;

        public bool HasPropertyRename =>
            !string.IsNullOrEmpty(oldPropertyName) && !string.IsNullOrEmpty(newPropertyName);

        public bool HasKeywordRename =>
            !string.IsNullOrEmpty(oldKeyword) && !string.IsNullOrEmpty(newKeyword);

        public bool HasShaderRename =>
            !string.IsNullOrEmpty(oldShaderName) && !string.IsNullOrEmpty(newShaderName);

        /// <summary>
        /// 旧 float 値を変換方式に従って新値へ写す（純関数・テスト対象）。
        /// None は変換対象外のため旧値をそのまま返す（呼び出し側で値を使わない前提）。
        /// </summary>
        public float ConvertFloat(float oldValue)
        {
            switch (valueConversion)
            {
                case NataneValueConversion.FloatScale:
                    return oldValue * scale;
                case NataneValueConversion.Invert:
                    return 1f - oldValue;
                case NataneValueConversion.CopyAsIs:
                case NataneValueConversion.None:
                default:
                    return oldValue;
            }
        }
    }
}
