using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// 監査・カタログ生成が共通で使う Toon バリアントの探索。
    ///
    /// 対象は Shaders/NataneToon/ 配下の NataneToonShader*.shader。
    /// Eye / Wirelight / ScreenFXOverlay は構造が別系統なので含めない。
    /// Particle は Properties が 44 行しかなく Effects 系機能を持たない別系統のため
    /// 既定で除外する（除外した事実は呼び出し側へ返す）。
    /// FakeShadow も同様に別系統。NataneToonCore.hlsl を include しない単一パスの極小
    /// シェーダーで、本体の機能セットを一切持たない。相互比較の母集団に入れると
    /// 「990 プロパティが欠落している」というノイズしか出ない。
    /// </summary>
    internal static class NataneToonVariantLocator
    {
        private const string ToonShaderDirFragment = "/Shaders/NataneToon/";
        private const string ToonShaderFilePrefix = "NataneToonShader";

        public const string ParticleFileName = "NataneToonShader_Particle.shader";
        public const string FakeShadowFileName = "NataneToonShader_FakeShadow.shader";
        public const string MainFileName = "NataneToonShader.shader";

        internal sealed class Entry
        {
            public string FileName;
            public string AssetPath;
            public string FullPath;
            public string Source;
        }

        /// <summary>
        /// バリアントを列挙する。読めなかったものは黙って捨てず warning を出す。
        /// 並びは本体を先頭に、以降ファイル名の序数順。
        /// </summary>
        public static List<Entry> Load(out List<string> excluded, bool includeParticle = false)
        {
            var entries = new List<Entry>();
            excluded = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;

                string normalized = assetPath.Replace("\\", "/");
                if (normalized.IndexOf(ToonShaderDirFragment, StringComparison.OrdinalIgnoreCase) < 0) continue;

                string fileName = Path.GetFileName(normalized);
                if (!fileName.StartsWith(ToonShaderFilePrefix, StringComparison.Ordinal)) continue;
                if (!fileName.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)) continue;

                if (!includeParticle && string.Equals(fileName, ParticleFileName, StringComparison.Ordinal))
                {
                    excluded.Add(fileName);
                    continue;
                }

                if (string.Equals(fileName, FakeShadowFileName, StringComparison.Ordinal))
                {
                    excluded.Add(fileName);
                    continue;
                }

                string fullPath;
                try { fullPath = Path.GetFullPath(assetPath); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NataneToonShader] バリアント探索: {assetPath} のパス解決に失敗 ({e.Message})");
                    continue;
                }

                if (!File.Exists(fullPath)) continue;

                string source;
                try { source = File.ReadAllText(fullPath); }
                catch (IOException e)
                {
                    Debug.LogWarning($"[NataneToonShader] バリアント探索: {assetPath} を読めませんでした ({e.Message})");
                    continue;
                }

                entries.Add(new Entry
                {
                    FileName = fileName,
                    AssetPath = assetPath,
                    FullPath = fullPath,
                    Source = source
                });
            }

            entries.Sort((a, b) =>
            {
                // 本体を先頭に固定し、正準順の基準にする。
                bool aMain = a.FileName == MainFileName;
                bool bMain = b.FileName == MainFileName;
                if (aMain != bMain) return aMain ? -1 : 1;
                return string.CompareOrdinal(a.FileName, b.FileName);
            });

            excluded.Sort(StringComparer.Ordinal);
            return entries;
        }
    }
}
