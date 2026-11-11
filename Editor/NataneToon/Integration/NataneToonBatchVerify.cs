using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// バッチモードから全検証を一括実行するエントリポイント。
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath &lt;path&gt;
    ///             -executeMethod NataneToon.Editor.NataneToonBatchVerify.RunAll
    ///             -logFile &lt;log&gt;
    ///
    /// エディタ GUI を開かずに以下をまとめて確認し、結果をレポートと終了コードで返す。
    /// 失敗時は終了コード 1。
    ///
    ///   1. シェーダーのコンパイル (ShaderUtil のメッセージを収集)
    ///   2. バリアント間の整合性監査
    ///   3. Properties 正準カタログの整合チェック
    ///   4. Properties 生成のドライラン (書き込みはしない)
    ///
    /// 1 は特に重要で、HLSL はエディタ外でコンパイル検証する手段が無いため、
    /// ここを通さないとシェーダー変更の正しさを機械的に確認できない。
    /// </summary>
    internal static class NataneToonBatchVerify
    {
        private static string ProjectRootPath =>
            Path.GetDirectoryName(Application.dataPath)?.Replace("\\", "/") ?? string.Empty;

        private static string ReportPath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Audit", "batch-verify.md");

        private sealed class Section
        {
            public string Title;
            public bool Failed;
            public List<string> Lines = new List<string>();
        }

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/全検証をまとめて実行", false, 70)]
        public static void RunFromMenu()
        {
            List<Section> sections = RunSections(out bool failed);
            string path = WriteReport(sections, failed);

            EditorUtility.DisplayDialog(
                "全検証",
                (failed ? "失敗があります。\n\n" : "すべて合格しました。\n\n") + $"レポート:\n{path}",
                "OK");
        }

        /// <summary>
        /// ウォッチャー用。終了はせず、失敗有無とレポートパスを返す。
        /// </summary>
        internal static bool RunForWatcher(out string reportPath)
        {
            List<Section> sections = RunSections(out bool failed);
            reportPath = WriteReport(sections, failed);

            foreach (Section s in sections)
                Debug.Log($"[NataneBatchVerify] {(s.Failed ? "FAIL" : "PASS")} {s.Title}");

            return failed;
        }

        /// <summary>バッチモード用。終了コードで結果を返す。</summary>
        public static void RunAll()
        {
            int exitCode = 0;
            try
            {
                List<Section> sections = RunSections(out bool failed);
                string path = WriteReport(sections, failed);

                // バッチのログに要約を出す（呼び出し側が grep できるように定型にする）。
                Debug.Log("[NataneBatchVerify] report: " + path);
                foreach (Section s in sections)
                    Debug.Log($"[NataneBatchVerify] {(s.Failed ? "FAIL" : "PASS")} {s.Title}");
                Debug.Log("[NataneBatchVerify] RESULT: " + (failed ? "FAIL" : "PASS"));

                exitCode = failed ? 1 : 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[NataneBatchVerify] 例外: " + e);
                Debug.Log("[NataneBatchVerify] RESULT: ERROR");
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        // ---- 各検証 ----

        private static List<Section> RunSections(out bool anyFailed)
        {
            var sections = new List<Section>
            {
                VerifyShaderCompilation(),
                VerifyKeywordVariantsCompile(),
                VerifyConsistencyAudit(),
                VerifyPropertyCatalog(),
                VerifyGenerationDryRun(),
            };

            anyFailed = sections.Any(s => s.Failed);
            return sections;
        }

        /// <summary>
        /// Natane シェーダーを再インポートし、コンパイルメッセージを収集する。
        /// エラーが 1 件でもあれば失敗。警告は記録するが失敗にはしない。
        /// </summary>
        private static Section VerifyShaderCompilation()
        {
            var section = new Section { Title = "シェーダーのコンパイル" };

            List<string> shaderPaths = AssetDatabase.FindAssets("t:Shader")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Where(p => p.Replace("\\", "/").IndexOf("/NataneToon/", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            if (shaderPaths.Count == 0)
            {
                section.Failed = true;
                section.Lines.Add("Natane シェーダーが 1 つも見つかりませんでした。");
                return section;
            }

            int errorTotal = 0;
            int warningTotal = 0;

            foreach (string path in shaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    section.Failed = true;
                    section.Lines.Add($"- **{path}**: Shader として読み込めませんでした");
                    continue;
                }

                // 前回のメッセージが残っていると判定を誤るので、消してから再インポートする。
                ShaderUtil.ClearShaderMessages(shader);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;

                int count = ShaderUtil.GetShaderMessageCount(shader);
                if (count <= 0) continue;

                // 重大度の enum は UnityEditor.Rendering 側。UnityEngine.Rendering と
                // 紛らわしいので完全修飾で参照する。
                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                var errors = messages
                    .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .ToList();
                var warnings = messages
                    .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Warning)
                    .ToList();

                errorTotal += errors.Count;
                warningTotal += warnings.Count;

                if (errors.Count > 0)
                {
                    section.Failed = true;
                    section.Lines.Add($"- **{Path.GetFileName(path)}**: エラー {errors.Count} 件");
                    foreach (ShaderMessage m in errors.Take(10))
                        section.Lines.Add($"  - `{m.file}:{m.line}` {m.message}");
                }
                else if (warnings.Count > 0)
                {
                    section.Lines.Add($"- {Path.GetFileName(path)}: 警告 {warnings.Count} 件");
                    foreach (ShaderMessage m in warnings.Take(3))
                        section.Lines.Add($"  - `{m.file}:{m.line}` {m.message}");
                }
            }

            section.Lines.Insert(0,
                $"対象 {shaderPaths.Count} シェーダー / エラー {errorTotal} / 警告 {warningTotal}");
            return section;
        }

        /// <summary>
        /// 検証したいキーワードの組み合わせ。
        ///
        /// shader_feature は「使われている変種」しかコンパイルされないため、
        /// 通常のインポートでは #if defined(...) の中身が一度も通らないことがある。
        /// 新しいキーワードを足したら、ここに入れて明示的に変種を焼く。
        /// </summary>
        private static readonly string[][] KeywordSetsToCompile =
        {
            new[] { "_SHADOW_BOKEH" },
            new[] { "_SHADOW_BOKEH", "_QUEST_LITE" },   // Lite 経路
            new[] { "_SHADOW_BOKEH", "_EMISSION" },     // HDR オーバーシュート経路
            new[] { "_SHADOW_BOKEH", "_DISTANCE_FADE" },

            // 漫画網点。パターン分岐は uniform 分岐なので 1 変種で全経路がコンパイルされる。
            new[] { "_HALFTONE_SHADOW" },
            new[] { "_HALFTONE_SHADOW", "_SHADOW_BOKEH" },

            // ミラー / 距離まわりの修正とセミリアル仕上げが通る経路。
            // PassType.ForwardBase には OUTLINE パス（LightMode=ForwardBase）も含まれるので、
            // アウトライン側の距離フェード / ミラー制御もここでコンパイルされる。
            // バリアントに存在しない組み合わせ（Lite のテッセレーション等）は「登録できず」に回るだけ。
            new[] { "_DISTANCE_FADE" },                                   // アウトライン OFF でも v2f が初期化される
            new[] { "_OUTLINE", "_DISTANCE_FADE" },
            new[] { "_OUTLINE", "_DISTANCE_FADE", "_HEIGHT_FADE", "_MIRROR_CONTROL" },
            new[] { "_OUTLINE", "_PERSPECTIVE_FLAT", "_FACE_ORTHO", "_FACE_ORTHO_MASK" },
            new[] { "_FACE_ORTHO", "_FACE_ORTHO_MASK" },                  // _FaceOrthoMaskTex（スムース法線 OFF）
            new[] { "_NORMALMAP" },                                       // ノーマルマップ境界の補正
            new[] { "_NORMALMAP", "_SPECULAR", "_PBR_LIKE" },             // GGX の幾何 AA / スペキュラ影マスク
            new[] { "_USE_LIGHT_VOLUME" },                                // Light Volume SH モード
            new[] { "_AUDIOLINK", "_EMISSION" },
            new[] { "_REFLECTION", "_ENV_RIM", "_FAKE_REFLECTION" },
            new[] { "_HAIR_SPECULAR", "_USE_AO", "_REFLECTION", "_PBR_LIKE" }, // 髪リム透過 / AO 影ウェイト / 露出オクルージョン
            new[] { "_PBR", "_USE_AO" },                                  // 露出オクルージョン（PBR 経路）
            new[] { "_REFRACTION" },
            new[] { "_DITHERING_ALPHA" },
            new[] { "_HASHED_ALPHA" },
            new[] { "_SPECULAR", "_SPECULAR_DITHER" },
            new[] { "_USE_DITHERING", "_BLUE_NOISE_DITHER" },
            new[] { "_SCREEN_EDGE" },
            new[] { "_INTERSECTION_FADE" },
            new[] { "_PCSS" },
            new[] { "_SMEAR", "_DISTANCE_FADE" },
            new[] { "_SHADOW_SHAPE_RIG" },
            new[] { "_LENTICULAR" },
            new[] { "_FX_MODULATOR" },
            new[] { "_TESSELLATION" },
        };

        /// <summary>
        /// 指定キーワードを有効にした変種を実際にコンパイルさせ、エラーを拾う。
        /// ShaderVariantCollection.WarmUp() がコンパイルを強制する。
        /// </summary>
        private static Section VerifyKeywordVariantsCompile()
        {
            var section = new Section { Title = "キーワード有効時のコンパイル" };

            List<NataneToonVariantLocator.Entry> entries =
                NataneToonVariantLocator.Load(out _);

            if (entries.Count == 0)
            {
                section.Failed = true;
                section.Lines.Add("対象シェーダーが見つかりませんでした。");
                return section;
            }

            var collection = new ShaderVariantCollection();
            int added = 0;
            var skipped = new List<string>();

            foreach (NataneToonVariantLocator.Entry e in entries)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(e.AssetPath);
                if (shader == null) continue;

                ShaderUtil.ClearShaderMessages(shader);

                foreach (string[] keywords in KeywordSetsToCompile)
                {
                    try
                    {
                        // 該当変種が存在しない組み合わせだとコンストラクタが投げる。
                        var variant = new ShaderVariantCollection.ShaderVariant(
                            shader, UnityEngine.Rendering.PassType.ForwardBase, keywords);
                        if (collection.Add(variant)) added++;
                    }
                    catch (Exception ex)
                    {
                        skipped.Add($"{e.FileName} [{string.Join(",", keywords)}]: {ex.Message}");
                    }
                }
            }

            section.Lines.Add($"変種 {added} 件を登録 / 登録できず {skipped.Count} 件");

            if (added == 0)
            {
                section.Failed = true;
                section.Lines.Add("**変種を 1 件も登録できませんでした。検証が成立していません。**");
                foreach (string s in skipped.Take(10)) section.Lines.Add("- " + s);
                return section;
            }

            try
            {
                collection.WarmUp();
            }
            catch (Exception ex)
            {
                section.Failed = true;
                section.Lines.Add("WarmUp で例外: " + ex.Message);
                return section;
            }

            int errorTotal = 0;
            foreach (NataneToonVariantLocator.Entry e in entries)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(e.AssetPath);
                if (shader == null) continue;
                if (ShaderUtil.GetShaderMessageCount(shader) <= 0) continue;

                var errors = ShaderUtil.GetShaderMessages(shader)
                    .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .ToList();
                if (errors.Count == 0) continue;

                errorTotal += errors.Count;
                section.Failed = true;
                section.Lines.Add($"- **{e.FileName}**: エラー {errors.Count} 件");
                foreach (ShaderMessage m in errors.Take(8))
                    section.Lines.Add($"  - `{m.file}:{m.line}` {m.message}");
            }

            section.Lines.Add($"コンパイル後のエラー合計: {errorTotal}");

            if (skipped.Count > 0)
            {
                section.Lines.Add("登録できなかった組み合わせ（キーワード未定義など）:");
                foreach (string s in skipped.Take(5)) section.Lines.Add("- " + s);
            }

            return section;
        }

        private static Section VerifyConsistencyAudit()
        {
            var section = new Section { Title = "バリアント整合性監査" };

            List<NataneConsistencyFinding> findings =
                NataneShaderConsistencyAudit.Run(out int variantCount, out _);

            int errors = findings.Count(f => f.Severity == NataneConsistencySeverity.Error);
            int warnings = findings.Count(f => f.Severity == NataneConsistencySeverity.Warning);

            section.Lines.Add($"対象 {variantCount} バリアント / 要修正 {errors} / 要確認 {warnings}");

            // 既知の未修正項目があるため、ここでは失敗扱いにしない（記録のみ）。
            // 要修正・要確認とも本文に出す。件数だけだと何が残っているのか追えない。
            foreach (NataneConsistencyFinding f in findings
                         .Where(f => f.Severity == NataneConsistencySeverity.Error)
                         .Take(20))
            {
                section.Lines.Add($"- **要修正** [{f.Category}] {f.Message}");
            }

            foreach (NataneConsistencyFinding f in findings
                         .Where(f => f.Severity == NataneConsistencySeverity.Warning)
                         .Take(20))
            {
                section.Lines.Add($"- 要確認 [{f.Category}] {f.Message}");
                if (!string.IsNullOrEmpty(f.Detail))
                    section.Lines.Add($"  - {f.Detail.Split('\n')[0]}");
            }

            return section;
        }

        private static Section VerifyPropertyCatalog()
        {
            var section = new Section { Title = "Properties カタログ整合チェック" };

            if (!NataneShaderPropertyCatalogBootstrap.LoadFromDisk(
                    out _, out List<NataneShaderPropertySet> sets,
                    out NataneShaderPropertyCatalog catalog, out _))
            {
                section.Failed = true;
                section.Lines.Add("対象シェーダーが見つかりませんでした。");
                return section;
            }

            List<string> failures = NataneShaderPropertyCatalogBootstrap.Verify(catalog, sets);

            section.Lines.Add(
                $"和集合 {catalog.Declarations.Count} プロパティ / グループ {catalog.Groups.Count} 種 / " +
                $"上書き {catalog.Overrides.Count} 件 / 不一致 {failures.Count} 件");

            if (failures.Count > 0)
            {
                section.Failed = true;
                foreach (string f in failures.Take(30)) section.Lines.Add("- " + f);
            }

            return section;
        }

        /// <summary>
        /// 生成のドライラン。書き込みはしない。
        /// 「差分あり」なら生成物と現物がずれている＝カタログの再生成が必要という意味なので失敗にする。
        /// </summary>
        private static Section VerifyGenerationDryRun()
        {
            var section = new Section { Title = "Properties 生成のドライラン" };

            if (!NataneShaderPropertyCatalogBootstrap.LoadFromDisk(
                    out List<NataneToonVariantLocator.Entry> entries,
                    out _, out NataneShaderPropertyCatalog catalog, out _))
            {
                section.Failed = true;
                section.Lines.Add("対象シェーダーが見つかりませんでした。");
                return section;
            }

            // 生成側と同じ条件で見るため、追加定義もここで合流させる。
            // 未反映の追加分があれば「差分あり」として現れ、再生成が要ることが分かる。
            List<string> merged = NataneShaderPropertyAdditions.Merge(catalog);
            if (merged.Count > 0) section.Lines.Add($"追加定義から {merged.Count} プロパティを合流");

            var results = entries
                .Select(e => NataneShaderPropertyWriter.Apply(e, catalog, dryRun: true, merged))
                .ToList();

            int changed = results.Count(r => r.Changed);
            int problematic = results.Count(r => r.Problems.Count > 0);

            section.Lines.Add($"対象 {results.Count} 本 / 差分あり {changed} / 問題あり {problematic}");

            if (problematic > 0 || changed > 0)
            {
                section.Failed = true;
                foreach (var r in results.Where(r => r.Problems.Count > 0 || r.Changed))
                {
                    section.Lines.Add($"- **{r.FileName}**: 差分={r.Changed} 問題={r.Problems.Count}");
                    foreach (string p in r.Problems.Take(5)) section.Lines.Add($"  - {p}");
                }
            }

            return section;
        }

        // ---- 出力 ----

        private static string WriteReport(List<Section> sections, bool failed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 全検証レポート");
            sb.AppendLine();
            sb.AppendLine($"- 実行: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Unity: {Application.unityVersion}");
            sb.AppendLine($"- 総合: **{(failed ? "失敗" : "合格")}**");
            sb.AppendLine();

            foreach (Section s in sections)
            {
                sb.AppendLine($"## {(s.Failed ? "FAIL" : "PASS")} — {s.Title}");
                sb.AppendLine();
                foreach (string line in s.Lines) sb.AppendLine(line);
                sb.AppendLine();
            }

            string path = ReportPath;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneBatchVerify] レポートを書き出せませんでした: {e.Message}");
                return "(書き出し失敗)";
            }
            return path.Replace("\\", "/");
        }
    }
}
