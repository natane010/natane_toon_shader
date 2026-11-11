using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NataneToon.Editor
{
    // 機能スコープ。ストリップ時の波及禁止境界（Particle/Wirelight/Eye/Background 非波及）と一致させる。
    public enum NataneFeatureScope
    {
        CoreFamily,
        Particle,
        Wirelight,
        Eye,
        Background,
        Named
    }

    // 判定方式。Toggle=単一プロパティ、Enum/Composite=複数値、Derived=他プロパティから派生、
    // CustomEvaluator=専用ロジック要。自動判定が未確立の Derived/Composite は安全側（非削除）で扱う。
    public enum NataneFeatureEvaluationKind
    {
        Toggle,
        Enum,
        Composite,
        Derived,
        CustomEvaluator
    }

    /// <summary>
    /// シェーダー機能1件の定義。KeywordMappings を土台に追加メタデータを保持する。
    /// multi_compile 系および Unity ビルトインキーワードは本 Registry の対象外
    /// （それらは管理対象キーワードではなく、監査でも別リストへ分離する）。
    /// </summary>
    public sealed class NataneFeatureDefinition
    {
        public string PropertyName;              // Derived/Composite では null 可（駆動プロパティが単一でない）
        public string Keyword;
        public NataneFeatureScope Scope;
        public string[] TargetShaders;           // Scope=Named のときのみ有効
        public NataneFeatureEvaluationKind EvaluationKind;
        public bool IsAnimatable;
        public bool IsRuntimeMutable;
        public bool SafeStrippable;
        public bool AggressiveStrippable;
        public bool AlwaysKeep;
        public string IntroducedVersion;         // 導入 package version（不明は null）
        public string DeprecatedVersion;         // 廃止 package version（現役は null）
        public string[] LegacyPropertyNames;
        public string[] LegacyKeywords;
        public string MigrationNote;
    }

    /// <summary>
    /// KeywordMappings を唯一の情報源へ発展させる機能レジストリ。
    /// 後方互換のため KeywordMappings 自体は現行のまま（Synchronizer が参照）。本クラスは
    /// KeywordMappings を Toggle/CoreFamily として自動取り込みし、追加メタデータ（新規機能・
    /// スコープ限定・派生判定）を別テーブルで重ねる。定義の二重記述はしない。
    /// GUI/Synchronizer/Optimizer/Stripper/Report からの重複定義を将来的にここへ集約する足場。
    /// </summary>
    public static class NataneShaderFeatureRegistry
    {
        // Registry のスキーマ版。schema 不一致の cache/snapshot は再利用しない判定に用いる。
        public const int RegistrySchemaVersion = 1;

        // Catalog と一致させる特殊シェーダー名（スコープ判定の一元管理）。
        private const string ParticleShaderName = "Natane/Toon Shader (Particle)";
        private const string WirelightShaderName = "Natane/Toon Shader Wirelight";
        private const string EyeShaderName = "Natane/Eye";
        private const string BackgroundShaderName = "Natane/Toon Shader (Background)";

        private static readonly Dictionary<string, NataneFeatureDefinition> ByKeyword;
        private static readonly List<NataneFeatureDefinition> All;
        private static readonly List<NataneMigrationDefinition> Migrations;

        static NataneShaderFeatureRegistry()
        {
            ByKeyword = new Dictionary<string, NataneFeatureDefinition>(StringComparer.Ordinal);

            // 1. KeywordMappings を Toggle/CoreFamily として自動取り込み（既定は削除許可・アニメ/Runtime 可）。
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                if (string.IsNullOrEmpty(mapping.keyword) || ByKeyword.ContainsKey(mapping.keyword))
                    continue;

                ByKeyword[mapping.keyword] = new NataneFeatureDefinition
                {
                    PropertyName = mapping.propertyName,
                    Keyword = mapping.keyword,
                    Scope = NataneFeatureScope.CoreFamily,
                    EvaluationKind = NataneFeatureEvaluationKind.Toggle,
                    IsAnimatable = true,
                    IsRuntimeMutable = true,
                    SafeStrippable = true,
                    AggressiveStrippable = true,
                    AlwaysKeep = false
                };
            }

            // 2. 追加メタデータテーブル。KeywordMappings に無い新規機能を登録し、
            //    既存キーワードに対しては該当フィールドを上書きする（現状は新規登録のみ）。
            foreach (var def in BuildMetadataOverrides())
            {
                ByKeyword[def.Keyword] = def; // 同一キーワードなら上書き
            }

            All = ByKeyword.Values
                .OrderBy(d => d.Keyword, StringComparer.Ordinal)
                .ToList();

            Migrations = BuildMigrationTable();
        }

        /// <summary>
        /// Migration 定義テーブル。実データ（CHANGELOG v1.3.6 の歴史的リネーム）を静的登録し、
        /// 併せて各 FeatureDefinition の Legacy* フィールドから自動導出できる分を追加する
        /// （現状 Legacy* は未設定のため導出分は 0 件）。
        /// 新規移行を追加する際は下記コード例を参考に静的エントリを足すこと。
        /// </summary>
        private static List<NataneMigrationDefinition> BuildMigrationTable()
        {
            var list = new List<NataneMigrationDefinition>();

            // --- 実データ: v1.3.6（2026-03-03）で確定した Property リネーム ---
            // 旧名称は現行シェーダーに存在せず、新名称のみ実在することを確認済み。
            // 値の意味は不変のため CopyAsIs。全 Natane マテリアル対象（oldShaderName=null）。
            list.Add(PropertyRename("_ToonSteps", "_ShadowSteps", "1.3.6",
                "v1.3.6 でリネーム。段階数の意味は不変。"));
            list.Add(PropertyRename("_SpecularSharpness", "_SpecularSoftness", "1.3.6",
                "v1.3.6 でリネーム。値はそのまま写す。"));
            list.Add(PropertyRename("_EmissionIntensity", "_EmissionGlow", "1.3.6",
                "v1.3.6 でリネーム。強度値は不変。"));

            // --- 実データ: v1.3.6 の Keyword リネーム（_RIM -> _RIM_LIGHT）---
            list.Add(new NataneMigrationDefinition
            {
                oldKeyword = "_RIM",
                newKeyword = "_RIM_LIGHT",
                introducedVersion = "1.3.6",
                removedVersion = "1.3.6",
                valueConversion = NataneValueConversion.None,
                autoApplicable = true,
                requiresManualReview = false,
                note = "v1.3.6 でリムライトの Keyword を統一。"
            });

            // --- 自動導出: FeatureDefinition.Legacy* から旧→新の Keyword/Property を補完する ---
            // （新機能追加時に Legacy* を埋めれば、ここで移行候補が自動生成される足場）。
            foreach (NataneFeatureDefinition def in All)
            {
                if (def.LegacyKeywords != null)
                {
                    foreach (string legacy in def.LegacyKeywords)
                    {
                        if (string.IsNullOrEmpty(legacy) || legacy == def.Keyword)
                            continue;
                        if (list.Exists(m => m.oldKeyword == legacy && m.newKeyword == def.Keyword))
                            continue;

                        list.Add(new NataneMigrationDefinition
                        {
                            oldKeyword = legacy,
                            newKeyword = def.Keyword,
                            introducedVersion = def.IntroducedVersion,
                            valueConversion = NataneValueConversion.None,
                            autoApplicable = true,
                            note = def.MigrationNote
                        });
                    }
                }

                if (def.LegacyPropertyNames != null && !string.IsNullOrEmpty(def.PropertyName))
                {
                    foreach (string legacy in def.LegacyPropertyNames)
                    {
                        if (string.IsNullOrEmpty(legacy) || legacy == def.PropertyName)
                            continue;
                        if (list.Exists(m => m.oldPropertyName == legacy && m.newPropertyName == def.PropertyName))
                            continue;

                        list.Add(PropertyRename(legacy, def.PropertyName, def.IntroducedVersion, def.MigrationNote));
                    }
                }
            }

            // --- コード例（無効化）: Shader リネーム＋値スケール＋手動確認要のパターン ---
            // list.Add(new NataneMigrationDefinition
            // {
            //     oldShaderName = "Natane/Old Shader Name",
            //     newShaderName = "Natane/New Shader Name",
            //     oldPropertyName = "_OldRange", newPropertyName = "_NewRange",
            //     valueConversion = NataneValueConversion.FloatScale, scale = 0.01f,
            //     introducedVersion = "1.7.0", removedVersion = "1.7.0",
            //     autoApplicable = false, requiresManualReview = true,
            //     note = "スケール変更を伴うため手動確認必須。"
            // });

            return list;
        }

        private static NataneMigrationDefinition PropertyRename(
            string oldProperty, string newProperty, string version, string note)
        {
            return new NataneMigrationDefinition
            {
                oldPropertyName = oldProperty,
                newPropertyName = newProperty,
                introducedVersion = version,
                removedVersion = version,
                valueConversion = NataneValueConversion.CopyAsIs,
                autoApplicable = true,
                requiresManualReview = false,
                note = note
            };
        }

        /// <summary>移行定義（読み取り専用）。プレビュー/プランナが唯一の情報源として参照する。</summary>
        public static IReadOnlyList<NataneMigrationDefinition> MigrationDefinitions => Migrations;

        /// <summary>
        /// Migration テーブル内容に連動した版番号（int）。テーブルが変われば値が変化し、
        /// Snapshot/cache の鮮度判定（registryMigrationVersion）に反映される。
        /// </summary>
        public static int MigrationVersion => ComputeMigrationVersion();

        private static int ComputeMigrationVersion()
        {
            var sb = new StringBuilder();
            foreach (NataneMigrationDefinition m in Migrations)
            {
                sb.Append(m.oldShaderName ?? "").Append('|')
                  .Append(m.newShaderName ?? "").Append('|')
                  .Append(m.oldPropertyName ?? "").Append('|')
                  .Append(m.newPropertyName ?? "").Append('|')
                  .Append(m.oldKeyword ?? "").Append('|')
                  .Append(m.newKeyword ?? "").Append('|')
                  .Append((int)m.valueConversion).Append('|')
                  .Append(m.scale.ToString("R")).Append('\n');
            }

            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                // 先頭 4 バイトを非負 int へ畳み込む（安定・決定的）。
                int value = (hash[0] << 23) ^ (hash[1] << 16) ^ (hash[2] << 8) ^ hash[3];
                return value & 0x7fffffff;
            }
        }

        /// <summary>
        /// 調査で判明した KeywordMappings 欠落分の登録＋既存定義のメタデータ上書き。
        /// 各プロパティ名は実シェーダーの [Toggle(...)] 属性から取得している。
        /// </summary>
        private static IEnumerable<NataneFeatureDefinition> BuildMetadataOverrides()
        {
            // --- Particle 系（Natane/Toon Shader (Particle)、Particle.shader:104-108 の shader_feature_local）---
            yield return Toggle("_PARTICLE_TOON_LIGHTING", "_ParticleToonLighting", NataneFeatureScope.Particle);
            yield return Toggle("_SOFT_PARTICLES", "_SoftParticlesEnabled", NataneFeatureScope.Particle);
            yield return Toggle("_FLIPBOOK_BLENDING", "_FlipbookBlending", NataneFeatureScope.Particle);
            yield return Toggle("_CAMERA_FADE", "_CameraFadeEnabled", NataneFeatureScope.Particle);

            // --- Wirelight 系（Natane/Toon Shader Wirelight、Wirelight.shader:165-172 の shader_feature_local）---
            yield return Toggle("_WIRELIGHT", "_Wirelight", NataneFeatureScope.Wirelight);
            yield return Toggle("_USE_VERTEX_COLOR_POS", "_SpaceSelector", NataneFeatureScope.Wirelight);
            yield return Toggle("_CYBER_SCANLINE", "_CyberScanline", NataneFeatureScope.Wirelight);
            yield return Toggle("_CYBER_CHROMA", "_CyberChromaShift", NataneFeatureScope.Wirelight);
            yield return Toggle("_CYBER_GLITCH", "_CyberGlitch", NataneFeatureScope.Wirelight);
            yield return Toggle("_CYBER_DATASTREAM", "_CyberDataStream", NataneFeatureScope.Wirelight);
            yield return Toggle("_WL_DISTANCE_FADE", "_UseWLDistanceFade", NataneFeatureScope.Wirelight);

            // --- Eye Parallax ---
            // 調査では Eye/ExtraKeywords 出自とされたが、実 pragma は CoreFamily 変種
            // （NataneToonShader.shader:1430 ほか [Toggle(_EYE_PARALLAX)] _EyeParallax:603）に存在し、
            // Natane/Eye シェーダーには無い。誤検出（対象 Shader 不在/未知）を避けるため Scope=CoreFamily とする。
            yield return new NataneFeatureDefinition
            {
                PropertyName = "_EyeParallax",
                Keyword = "_EYE_PARALLAX",
                Scope = NataneFeatureScope.CoreFamily,
                EvaluationKind = NataneFeatureEvaluationKind.Toggle,
                IsAnimatable = true,
                IsRuntimeMutable = true,
                SafeStrippable = true,
                AggressiveStrippable = true,
                MigrationNote = "旧 NataneShaderVariantStripper.ExtraKeywords 由来。実 pragma は CoreFamily 変種。"
            };

            // --- Background 専用 PBR ライティング（Background.shader:577 [Toggle(_PBR)] _EnablePBR、:1011 pragma）---
            yield return Toggle("_PBR", "_EnablePBR", NataneFeatureScope.Background);

            // --- _PBR_LIKE（派生キーワード。単一トグルでは駆動されない）---
            // GUI 連動条件（NataneToonShaderGUI.cs / NataneToonShaderGUIHelpers.cs より）:
            //   _ShadingMode >= 2.5（StandardToon/PBRLike 域, PBR_LIKE_MODE_THRESHOLD）
            //   または _LookMode == PBR(3)
            //   または Hybrid かつ _PbrWeight >= 0.6
            //   または _PBR_LIKE / _PBR キーワードが明示 ON
            // 自動判定が確立するまで削除しない（SafeStrippable=false / AlwaysKeep=true）。
            yield return new NataneFeatureDefinition
            {
                PropertyName = null,
                Keyword = "_PBR_LIKE",
                Scope = NataneFeatureScope.CoreFamily,
                EvaluationKind = NataneFeatureEvaluationKind.Derived,
                IsAnimatable = false,
                IsRuntimeMutable = false,
                SafeStrippable = false,
                AggressiveStrippable = false,
                AlwaysKeep = true,
                MigrationNote = "_ShadingMode>=2.5 / _LookMode==PBR / (_PbrWeight>=0.6 in Hybrid) から派生。"
            };
        }

        private static NataneFeatureDefinition Toggle(string keyword, string property, NataneFeatureScope scope)
        {
            return new NataneFeatureDefinition
            {
                PropertyName = property,
                Keyword = keyword,
                Scope = scope,
                EvaluationKind = NataneFeatureEvaluationKind.Toggle,
                IsAnimatable = true,
                IsRuntimeMutable = true,
                SafeStrippable = true,
                AggressiveStrippable = true,
                AlwaysKeep = false
            };
        }

        // ---- 公開 API ----

        public static IReadOnlyList<NataneFeatureDefinition> AllDefinitions => All;

        public static bool TryGetByKeyword(string keyword, out NataneFeatureDefinition definition)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                definition = null;
                return false;
            }

            return ByKeyword.TryGetValue(keyword, out definition);
        }

        public static bool IsKnownKeyword(string keyword)
        {
            return !string.IsNullOrEmpty(keyword) && ByKeyword.ContainsKey(keyword);
        }

        /// <summary>
        /// シェーダー名→スコープ。Catalog の名称を一元参照する。Natane 以外は CoreFamily を返さず false。
        /// </summary>
        public static bool TryGetScopeForShader(string shaderName, out NataneFeatureScope scope)
        {
            scope = NataneFeatureScope.CoreFamily;
            if (!NataneShaderCatalog.IsNataneShader(shaderName))
            {
                return false;
            }

            switch (shaderName)
            {
                case ParticleShaderName: scope = NataneFeatureScope.Particle; break;
                case WirelightShaderName: scope = NataneFeatureScope.Wirelight; break;
                case EyeShaderName: scope = NataneFeatureScope.Eye; break;
                case BackgroundShaderName: scope = NataneFeatureScope.Background; break;
                default: scope = NataneFeatureScope.CoreFamily; break;
            }

            return true;
        }

        public static NataneFeatureScope GetScopeForShader(string shaderName)
        {
            TryGetScopeForShader(shaderName, out var scope);
            return scope;
        }

        /// <summary>
        /// 当該シェーダーに関連する管理対象キーワード（宣言的な想定集合）。
        /// Background は CoreFamily を包含する変種のため CoreFamily+Background を返す。
        /// Particle/Wirelight/Eye は自スコープのみ。Named は対象シェーダー一致分を加える。
        /// 監査の未知/孤児判定は本 API ではなく実ソース解析集合を根拠とする（宣言と実体の乖離検出のため）。
        /// </summary>
        public static IReadOnlyList<string> GetKeywordsForShader(string shaderName)
        {
            var result = new List<string>();
            if (!TryGetScopeForShader(shaderName, out var scope))
            {
                return result;
            }

            bool coreMember = scope == NataneFeatureScope.CoreFamily || scope == NataneFeatureScope.Background;

            foreach (var def in All)
            {
                bool include;
                if (def.Scope == NataneFeatureScope.Named)
                {
                    include = def.TargetShaders != null &&
                              Array.IndexOf(def.TargetShaders, shaderName) >= 0;
                }
                else if (coreMember)
                {
                    include = def.Scope == NataneFeatureScope.CoreFamily || def.Scope == scope;
                }
                else
                {
                    include = def.Scope == scope;
                }

                if (include)
                {
                    result.Add(def.Keyword);
                }
            }

            return result;
        }

        // ---- バージョン管理 ----

        /// <summary>package.json の version（Shader 互換性版）。取得不能時は "unknown"。</summary>
        public static string ShaderCompatibilityVersion
        {
            get
            {
                try
                {
                    var info = PackageInfo.FindForAssembly(typeof(NataneShaderFeatureRegistry).Assembly);
                    if (info != null && !string.IsNullOrEmpty(info.version))
                    {
                        return info.version;
                    }
                }
                catch
                {
                    // 埋め込みパッケージ等では null になり得るため package.json 直読へフォールバック。
                }

                return ReadPackageVersionFromJson() ?? "unknown";
            }
        }

        private static string ReadPackageVersionFromJson()
        {
            try
            {
                if (!NatanePackagePathResolver.TryResolvePackageAssetPath("package.json", out string assetPath))
                {
                    return null;
                }

                string fullPath = System.IO.Path.GetFullPath(assetPath);
                if (!System.IO.File.Exists(fullPath))
                {
                    return null;
                }

                foreach (string line in System.IO.File.ReadAllLines(fullPath))
                {
                    int idx = line.IndexOf("\"version\"", StringComparison.Ordinal);
                    if (idx < 0) continue;
                    int colon = line.IndexOf(':', idx);
                    if (colon < 0) continue;
                    string rest = line.Substring(colon + 1).Trim().TrimEnd(',').Trim().Trim('"');
                    return string.IsNullOrEmpty(rest) ? null : rest;
                }
            }
            catch
            {
                // 解析不能時は null（呼び出し側で "unknown" 扱い）。
            }

            return null;
        }

        /// <summary>
        /// 全定義の安定ハッシュ。定義内容が変われば変化する（cache/snapshot の鮮度判定に用いる）。
        /// </summary>
        public static string ComputeRegistryFingerprint()
        {
            var sb = new StringBuilder();
            sb.Append("schema=").Append(RegistrySchemaVersion).Append(';');
            sb.Append("migration=").Append(MigrationVersion).Append('\n');

            foreach (var def in All) // All はキーワード昇順で安定
            {
                sb.Append(def.Keyword).Append('|')
                  .Append(def.PropertyName ?? string.Empty).Append('|')
                  .Append((int)def.Scope).Append('|')
                  .Append((int)def.EvaluationKind).Append('|')
                  .Append(def.IsAnimatable ? '1' : '0')
                  .Append(def.IsRuntimeMutable ? '1' : '0')
                  .Append(def.SafeStrippable ? '1' : '0')
                  .Append(def.AggressiveStrippable ? '1' : '0')
                  .Append(def.AlwaysKeep ? '1' : '0').Append('|')
                  .Append(def.TargetShaders != null ? string.Join(",", def.TargetShaders) : string.Empty).Append('\n');
            }

            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2"));
                }

                return hex.ToString();
            }
        }
    }
}
