using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace NataneToon.Editor
{
    // ==== 純粋な判定ロジック（ShaderCompilerData 非依存・ユニットテスト対象） ====

    public enum NataneStripAction
    {
        Keep,
        Strip
    }

    /// <summary>
    /// 1 バリアントに対する Safe 判定結果。SVC 保持や実削除の可否は呼出側（ストリッパー本体）が最終決定する。
    /// </summary>
    public sealed class StripDecision
    {
        public NataneStripAction Action = NataneStripAction.Keep;
        public string Reason;                              // 保持/削除理由（日本語）
        public List<string> TriggerKeywords = new List<string>(); // strip を誘発した未使用の削除可能キーワード
        public List<string> UnknownKeywords = new List<string>(); // 検出した未知キーワード（保持理由）
        public bool AggressiveDowngraded;                  // Aggressive 指定を Safe へ降格した場合 true
        public bool IsBaseVariant;                         // 管理対象キーワード 0 個
    }

    /// <summary>
    /// モード等のポリシー読み取りビュー（テストで容易に差し替えるため設定資産から分離）。
    /// </summary>
    public sealed class NataneStripPolicyView
    {
        public NataneOptimizationMode Mode = NataneOptimizationMode.Safe;

        // Aggressive 続行ガードを通過したか。false なら Aggressive は Safe へ降格する。
        // ガード判定はセッション単位で 1 回行い（NataneAggressiveGate）、本フラグへ反映する。
        public bool AggressiveGatePassed = true;
    }

    // Aggressive 続行ガードの結果。Proceed=false のとき Reasons に不通過理由を並べる。
    public sealed class NataneAggressiveGateResult
    {
        public bool Proceed = true;
        public List<string> Reasons = new List<string>();
    }

    // ガード不通過時の解決策。通常=Safe 降格 / Strict(FailBuild)=ビルド停止。
    public enum NataneAggressiveResolution
    {
        Proceed,
        DowngradeToSafe,
        FailBuild
    }

    /// <summary>
    /// Aggressive 続行ガード（純関数・ユニットテスト対象）。
    /// 未知キーワード / Registry schema 不一致 / Audit stale / 解析不能項目のいずれかがあれば続行しない。
    /// </summary>
    public static class NataneAggressiveGate
    {
        public static NataneAggressiveGateResult Evaluate(
            bool hasUnknownKeywords,
            bool registrySchemaMismatch,
            bool auditStale,
            bool hasUnparseableItems)
        {
            var r = new NataneAggressiveGateResult { Proceed = true };
            if (hasUnknownKeywords) { r.Proceed = false; r.Reasons.Add("未知キーワードが監査結果に存在するため"); }
            if (registrySchemaMismatch) { r.Proceed = false; r.Reasons.Add("Registry schema が不一致のため"); }
            if (auditStale) { r.Proceed = false; r.Reasons.Add("Shader 監査が stale（要再監査）のため"); }
            if (hasUnparseableItems) { r.Proceed = false; r.Reasons.Add("解析不能項目が存在するため"); }
            return r;
        }

        public static NataneAggressiveResolution Resolve(bool proceed, NataneStrictFailurePolicy policy)
        {
            if (proceed) return NataneAggressiveResolution.Proceed;
            return policy == NataneStrictFailurePolicy.FailBuild
                ? NataneAggressiveResolution.FailBuild
                : NataneAggressiveResolution.DowngradeToSafe;
        }
    }

    /// <summary>
    /// Snapshot のうち Safe 判定に必要な情報だけを名前ベースで保持する不変ビュー。
    /// シェーダー単位分離のため used キーワードは shaderName で引く（Particle/Wirelight 等の波及禁止）。
    /// Animation 由来 / Runtime 動的 / 常時保持キーワードは全シェーダー共通の保持集合として扱う。
    /// </summary>
    public sealed class NataneStripSnapshotView
    {
        public bool HasSnapshot { get; private set; }

        private readonly Dictionary<string, HashSet<string>> _usedByShader;
        private readonly HashSet<string> _globalKeep; // anim + runtime + alwaysKeep の和集合（Safe 用）
        private readonly HashSet<string> _alwaysKeepShaderNames;

        // Aggressive 用: シェーダー別の実在 Material 構成（生キーワード列）と、保持集合を分離保持する。
        private readonly Dictionary<string, List<string[]>> _configsByShader;
        private readonly HashSet<string> _animationKeywords;
        private readonly HashSet<string> _runtimeOrAlwaysKeepKeywords; // runtime 動的 ∪ 常時保持キーワード

        public NataneStripSnapshotView(
            bool hasSnapshot,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> usedByShader,
            IEnumerable<string> animationDrivenKeywords,
            IEnumerable<string> runtimeDynamicKeywords,
            IEnumerable<string> alwaysKeepKeywords,
            IEnumerable<string> alwaysKeepShaderNames,
            IReadOnlyDictionary<string, IReadOnlyCollection<IReadOnlyCollection<string>>> configsByShader = null)
        {
            HasSnapshot = hasSnapshot;
            _usedByShader = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (usedByShader != null)
            {
                foreach (var kv in usedByShader)
                {
                    _usedByShader[kv.Key] = new HashSet<string>(
                        kv.Value ?? Array.Empty<string>(), StringComparer.Ordinal);
                }
            }

            _animationKeywords = new HashSet<string>(StringComparer.Ordinal);
            AddAll(_animationKeywords, animationDrivenKeywords);

            _runtimeOrAlwaysKeepKeywords = new HashSet<string>(StringComparer.Ordinal);
            AddAll(_runtimeOrAlwaysKeepKeywords, runtimeDynamicKeywords);
            AddAll(_runtimeOrAlwaysKeepKeywords, alwaysKeepKeywords);

            _globalKeep = new HashSet<string>(_animationKeywords, StringComparer.Ordinal);
            _globalKeep.UnionWith(_runtimeOrAlwaysKeepKeywords);

            _alwaysKeepShaderNames = new HashSet<string>(
                alwaysKeepShaderNames ?? Array.Empty<string>(), StringComparer.Ordinal);

            _configsByShader = new Dictionary<string, List<string[]>>(StringComparer.Ordinal);
            if (configsByShader != null)
            {
                foreach (var kv in configsByShader)
                {
                    var list = new List<string[]>();
                    if (kv.Value != null)
                        foreach (var cfg in kv.Value)
                            list.Add(cfg == null ? Array.Empty<string>() : cfg.ToArray());
                    _configsByShader[kv.Key] = list;
                }
            }
        }

        private static void AddAll(HashSet<string> target, IEnumerable<string> src)
        {
            if (src == null) return;
            foreach (var s in src)
                if (!string.IsNullOrEmpty(s)) target.Add(s);
        }

        public static NataneStripSnapshotView Null()
        {
            return new NataneStripSnapshotView(false, null, null, null, null, null);
        }

        /// <summary>Aggressive 用: 当該シェーダーの実在 Material 構成（生キーワード列）。無ければ空。</summary>
        public IReadOnlyList<string[]> GetMaterialConfigs(string shaderName)
        {
            if (shaderName != null && _configsByShader.TryGetValue(shaderName, out var list))
                return list;
            return Array.Empty<string[]>();
        }

        /// <summary>Aggressive 用: AnimationClip 由来キーワード集合（実在構成への加算近似に用いる）。</summary>
        public IReadOnlyCollection<string> AnimationKeywords => _animationKeywords;

        /// <summary>Aggressive 用: Runtime 動的利用 or 常時保持に指定されたキーワードか。</summary>
        public bool IsRuntimeOrAlwaysKeepKeyword(string keyword)
        {
            return keyword != null && _runtimeOrAlwaysKeepKeywords.Contains(keyword);
        }

        /// <summary>当該シェーダーの保持集合（使用キーワード + 全シェーダー共通の保持集合）。</summary>
        public HashSet<string> GetKeepSet(string shaderName)
        {
            var result = new HashSet<string>(_globalKeep, StringComparer.Ordinal);
            if (shaderName != null && _usedByShader.TryGetValue(shaderName, out var used))
            {
                result.UnionWith(used);
            }
            return result;
        }

        public bool IsAlwaysKeepShader(string shaderName)
        {
            return shaderName != null && _alwaysKeepShaderNames.Contains(shaderName);
        }

        /// <summary>実 Snapshot から名前ベースビューを構築する。</summary>
        public static NataneStripSnapshotView FromSnapshot(NataneBuildUsageSnapshot s)
        {
            if (s == null) return Null();

            var usedByShader = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
            var shaderNameByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var su in s.shaderUsages)
            {
                if (string.IsNullOrEmpty(su.shaderName)) continue;
                usedByShader[su.shaderName] = new List<string>(su.usedKeywords ?? new List<string>());
                if (!string.IsNullOrEmpty(su.shaderGuid) && !shaderNameByGuid.ContainsKey(su.shaderGuid))
                    shaderNameByGuid[su.shaderGuid] = su.shaderName;
            }

            // alwaysKeepMaterialGuids のマテリアル構成は「使用中」として当該シェーダーの保持集合へ加える。
            if (s.alwaysKeepMaterialGuids != null && s.alwaysKeepMaterialGuids.Count > 0)
            {
                var pinned = new HashSet<string>(s.alwaysKeepMaterialGuids, StringComparer.OrdinalIgnoreCase);
                foreach (var mc in s.materialConfigs)
                {
                    if (mc == null || string.IsNullOrEmpty(mc.shaderName)) continue;
                    if (!pinned.Contains(mc.materialGuid)) continue;
                    if (string.IsNullOrEmpty(mc.keywordSetKey)) continue;

                    if (!usedByShader.TryGetValue(mc.shaderName, out var existing))
                    {
                        existing = new List<string>();
                        usedByShader[mc.shaderName] = existing;
                    }
                    var list = (List<string>)existing;
                    foreach (var kw in mc.keywordSetKey.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        if (!list.Contains(kw)) list.Add(kw);
                }
            }

            var animation = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in s.animationDrivenKeywords)
                if (a?.keywords != null) foreach (var k in a.keywords) animation.Add(k);

            // Aggressive 用: シェーダー別の実在 Material 構成（keywordSetKey を ';' で分解した生キーワード列）。
            var configsByShader = new Dictionary<string, IReadOnlyCollection<IReadOnlyCollection<string>>>(StringComparer.Ordinal);
            var configsAccum = new Dictionary<string, List<IReadOnlyCollection<string>>>(StringComparer.Ordinal);
            foreach (var mc in s.materialConfigs)
            {
                if (mc == null || string.IsNullOrEmpty(mc.shaderName)) continue;
                string[] kws = string.IsNullOrEmpty(mc.keywordSetKey)
                    ? Array.Empty<string>()
                    : mc.keywordSetKey.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (!configsAccum.TryGetValue(mc.shaderName, out var listForShader))
                {
                    listForShader = new List<IReadOnlyCollection<string>>();
                    configsAccum[mc.shaderName] = listForShader;
                }
                listForShader.Add(kws);
            }
            foreach (var kv in configsAccum)
                configsByShader[kv.Key] = kv.Value;

            // 常時保持シェーダー GUID → 名前解決（shaderUsages に載っている分のみ。載っていない分は本体側で GUID 直接判定）。
            var alwaysKeepShaderNames = new List<string>();
            if (s.alwaysKeepShaderGuids != null)
            {
                foreach (var g in s.alwaysKeepShaderGuids)
                    if (!string.IsNullOrEmpty(g) && shaderNameByGuid.TryGetValue(g, out var n))
                        alwaysKeepShaderNames.Add(n);
            }

            return new NataneStripSnapshotView(
                hasSnapshot: true,
                usedByShader: usedByShader,
                animationDrivenKeywords: animation,
                runtimeDynamicKeywords: s.runtimeDynamicKeywords,
                alwaysKeepKeywords: s.alwaysKeepKeywords,
                alwaysKeepShaderNames: alwaysKeepShaderNames,
                configsByShader: configsByShader);
        }
    }

    /// <summary>
    /// Safe 判定の純関数。ShaderCompilerData 非依存でユニットテスト可能。
    /// SVC 保持と実削除の可否は本判定の外（ストリッパー本体）で最終決定する。
    /// </summary>
    public static class NataneUnifiedStripDecider
    {
        // 判定に用いない Unity ビルトイン（'_' 始まりの一部のみ明示。'_' 始まりでないものは既定でビルトイン扱い）。
        private static readonly HashSet<string> BuiltinUnderscoreKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "_ALPHATEST_ON", "_ALPHABLEND_ON", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON"
        };

        /// <summary>
        /// multi_compile / Unity ビルトイン（DIRECTIONAL, FOG_*, UNITY_*, LIGHTPROBE_SH, INSTANCING_ON,
        /// STEREO_*, SHADOWS_* 等）は Natane の管理対象外。'_' 始まりでない語は全てビルトインとみなす。
        /// </summary>
        public static bool IsBuiltinKeyword(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return true;
            if (keyword[0] != '_') return true;                       // DIRECTIONAL / INSTANCING_ON / STEREO_* 等
            if (keyword.StartsWith("UNITY_", StringComparison.Ordinal)) return true;
            if (BuiltinUnderscoreKeywords.Contains(keyword)) return true;
            return false;
        }

        public static StripDecision Decide(
            string shaderName,
            IReadOnlyCollection<string> variantKeywords,
            NataneStripSnapshotView snapshot,
            NataneStripPolicyView policy)
        {
            var d = new StripDecision();

            // Aggressive はセッション単位の続行ガード（NataneAggressiveGate）を通過した場合のみ実行する。
            // 不通過なら Safe へ降格（理由はセッションレポートへ本体側が記録済み）。
            NataneOptimizationMode effective = policy?.Mode ?? NataneOptimizationMode.Safe;
            if (effective == NataneOptimizationMode.Aggressive && (policy == null || !policy.AggressiveGatePassed))
            {
                d.AggressiveDowngraded = true;
                effective = NataneOptimizationMode.Safe;
            }

            // Disabled は呼出側で除外済み。防御的に全保持。
            if (effective == NataneOptimizationMode.Disabled)
            {
                d.Reason = "最適化 Disabled のため保持";
                return d;
            }

            if (!NataneShaderCatalog.IsNataneShader(shaderName))
            {
                d.Reason = "カタログ対象外シェーダーのため保持";
                return d;
            }

            if (snapshot == null || !snapshot.HasSnapshot)
            {
                d.Reason = "Snapshot 不在のため全保持";
                return d;
            }

            if (snapshot.IsAlwaysKeepShader(shaderName))
            {
                d.Reason = "常時保持シェーダーのため保持";
                return d;
            }

            // キーワード分類: ビルトイン=無視 / 既知=管理対象 / '_' 始まりの未知=保持理由。
            var managed = new List<string>();
            if (variantKeywords != null)
            {
                foreach (var kw in variantKeywords)
                {
                    if (string.IsNullOrEmpty(kw)) continue;
                    if (IsBuiltinKeyword(kw)) continue;
                    if (NataneShaderFeatureRegistry.IsKnownKeyword(kw))
                        managed.Add(kw);
                    else
                        d.UnknownKeywords.Add(kw); // '_' 始まりのローカル形状の未知キーワード
                }
            }

            if (d.UnknownKeywords.Count > 0)
            {
                d.UnknownKeywords = d.UnknownKeywords.Distinct(StringComparer.Ordinal)
                    .OrderBy(k => k, StringComparer.Ordinal).ToList();
                d.Reason = "未知キーワード含有のため保持";
                return d;
            }

            if (managed.Count == 0)
            {
                d.IsBaseVariant = true;
                d.Reason = "ベースバリアント（管理対象キーワード 0 個）のため保持";
                return d;
            }

            if (effective == NataneOptimizationMode.Aggressive)
            {
                DecideAggressive(shaderName, variantKeywords, managed, snapshot, d);
                return d;
            }

            DecideSafe(shaderName, managed, snapshot, d);
            return d;
        }

        // Safe/ReportOnly の削除判定: 「使用中/Animation/Runtime/常時保持」に無い削除可能キーワードがあれば strip。
        private static void DecideSafe(
            string shaderName, List<string> managed, NataneStripSnapshotView snapshot, StripDecision d)
        {
            var keep = snapshot.GetKeepSet(shaderName);
            foreach (var kw in managed)
            {
                // 判定対象は「既知 かつ SafeStrippable かつ !AlwaysKeep」のみ。それ以外は保持側。
                if (!NataneShaderFeatureRegistry.TryGetByKeyword(kw, out var def)) continue;
                if (!def.SafeStrippable || def.AlwaysKeep) continue;
                if (!keep.Contains(kw))
                    d.TriggerKeywords.Add(kw);
            }

            if (d.TriggerKeywords.Count > 0)
            {
                d.TriggerKeywords = d.TriggerKeywords.Distinct(StringComparer.Ordinal)
                    .OrderBy(k => k, StringComparer.Ordinal).ToList();
                d.Action = NataneStripAction.Strip;
                d.Reason = "未使用の削除可能キーワードを含む: " + string.Join(",", d.TriggerKeywords);
            }
            else
            {
                d.Reason = "全キーワードが使用中/保持対象のため保持";
            }
        }

        // Aggressive の削除判定: 実在 Material の管理対象構成（+Animation 加算近似）に一致しなければ strip。
        // 続行ガードは呼出前に通過済み（未知/schema/stale/解析不能なし）。判定不能要素は全て保持側へ倒す。
        private static void DecideAggressive(
            string shaderName, IReadOnlyCollection<string> variantKeywords,
            List<string> managed, NataneStripSnapshotView snapshot, StripDecision d)
        {
            // (g) Registry 上 AggressiveStrippable==false / AlwaysKeep の管理キーワードを含む構成は保持。
            foreach (var kw in managed)
            {
                if (!NataneShaderFeatureRegistry.TryGetByKeyword(kw, out var def)) continue;
                if (!def.AggressiveStrippable || def.AlwaysKeep)
                {
                    d.Reason = "Aggressive 非対象キーワードを含むため保持: " + kw;
                    return;
                }
            }

            // (c) Runtime 動的宣言 / 常時保持キーワードを含む構成は保持。
            foreach (var kw in variantKeywords)
            {
                if (snapshot.IsRuntimeOrAlwaysKeepKeyword(kw))
                {
                    d.Reason = "Runtime 動的/常時保持キーワードを含むため保持: " + kw;
                    return;
                }
            }

            // (f) 管理対象（Aggressive 対象）キーワードが 0 個ならベースバリアント相当で保持。
            string variantKey = BuildAggressiveSetKey(variantKeywords);
            if (variantKey.Length == 0)
            {
                d.IsBaseVariant = true;
                d.Reason = "Aggressive 対象キーワード 0 個のため保持";
                return;
            }

            // (a)(b)(d) 実在 Material 構成集合（常時保持 Material 構成含む）＋ Animation 由来加算近似を許可集合に。
            var allowed = BuildAllowedAggressiveKeys(shaderName, snapshot);
            if (allowed.Contains(variantKey))
            {
                d.Reason = "実在 Material 構成（Animation 加算近似含む）に一致するため保持";
                return;
            }

            // どの実在構成にも一致しない → strip。
            d.TriggerKeywords = variantKey.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            d.Action = NataneStripAction.Strip;
            d.Reason = "実在 Material 構成に無い管理キーワード構成のため削除: " + variantKey;
        }

        /// <summary>
        /// 許可構成キー集合を作る: 各実在構成の正規化キー ＋（各実在構成 ∪ Animation 由来キーワード）の正規化キー。
        /// 組合せ爆発を避けるため「実在構成に Animation 由来を全加算した構成」だけを追加する保守的近似。
        /// </summary>
        private static HashSet<string> BuildAllowedAggressiveKeys(string shaderName, NataneStripSnapshotView snapshot)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            var animation = snapshot.AnimationKeywords;
            foreach (var config in snapshot.GetMaterialConfigs(shaderName))
            {
                allowed.Add(BuildAggressiveSetKey(config));
                if (animation != null && animation.Count > 0)
                {
                    var withAnim = new List<string>(config);
                    withAnim.AddRange(animation);
                    allowed.Add(BuildAggressiveSetKey(withAnim));
                }
            }
            return allowed;
        }

        /// <summary>
        /// 管理対象キーワード構成のセットキーを作る（既知 ∧ AggressiveStrippable ∧ 非 AlwaysKeep のみ、
        /// Ordinal ソート＋ ';' 連結）。Snapshot.materialConfigs.keywordSetKey と同一正規化。
        /// </summary>
        internal static string BuildAggressiveSetKey(IEnumerable<string> keywords)
        {
            if (keywords == null) return string.Empty;
            var managed = new List<string>();
            foreach (var kw in keywords)
            {
                if (string.IsNullOrEmpty(kw) || IsBuiltinKeyword(kw)) continue;
                if (!NataneShaderFeatureRegistry.TryGetByKeyword(kw, out var def)) continue;
                if (!def.AggressiveStrippable || def.AlwaysKeep) continue;
                managed.Add(kw);
            }
            return string.Join(";", managed.Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// ビルドセッション毎の集計。IPostprocessBuild(order 999) / VRChat フックで JSON 保存 + Console 要約。
    /// </summary>
    public static class NataneStrippingSessionReport
    {
        private sealed class PassCounters
        {
            public int kept;
            public int stripped;
            public int candidates; // ReportOnly での「Safe なら削除される」候補数
        }

        private static readonly Dictionary<string, Dictionary<string, PassCounters>> ByShaderPass =
            new Dictionary<string, Dictionary<string, PassCounters>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> KeepReasonCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> StrippedKeywordCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> UnknownKeywordSet =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> FallbackReasons = new List<string>();

        private static int _svcKeptCount;
        private static int _aggressiveDowngradeCount;  // Aggressive を Safe へ降格したバリアント数
        private static int _aggressiveCandidateCount;  // ReportOnly での「Aggressive なら削除される」候補数
        private static double _elapsedMs;
        private static bool _hasData;
        private static string _mode = "";

        public static bool HasData => _hasData || FallbackReasons.Count > 0;
        public static int TotalKept { get; private set; }
        public static int TotalStripped { get; private set; }
        public static int TotalCandidates { get; private set; }
        public static int TotalSeen => TotalKept + TotalStripped;

        public static void Reset()
        {
            ByShaderPass.Clear();
            KeepReasonCounts.Clear();
            StrippedKeywordCounts.Clear();
            UnknownKeywordSet.Clear();
            FallbackReasons.Clear();
            _svcKeptCount = 0;
            _aggressiveDowngradeCount = 0;
            _aggressiveCandidateCount = 0;
            _elapsedMs = 0;
            _hasData = false;
            _mode = NataneBuildPolicySettings.instance.OptimizationMode.ToString();
            TotalKept = 0;
            TotalStripped = 0;
            TotalCandidates = 0;
        }

        public static void AddElapsed(double ms) => _elapsedMs += ms;

        /// <summary>ReportOnly で「Aggressive なら削除される」候補を 1 件記録する。</summary>
        public static void RecordAggressiveCandidate() => _aggressiveCandidateCount++;

        public static int AggressiveCandidateCount => _aggressiveCandidateCount;

        public static void RecordFallback(string reason)
        {
            if (!string.IsNullOrEmpty(reason) && !FallbackReasons.Contains(reason))
                FallbackReasons.Add(reason);
        }

        public static void RecordUnknown(IEnumerable<string> keywords)
        {
            if (keywords == null) return;
            foreach (var k in keywords)
                if (!string.IsNullOrEmpty(k)) UnknownKeywordSet.Add(k);
        }

        public static void RecordSvcKept() => _svcKeptCount++;

        private static PassCounters GetCounters(string shader, string pass)
        {
            if (!ByShaderPass.TryGetValue(shader, out var passes))
            {
                passes = new Dictionary<string, PassCounters>(StringComparer.Ordinal);
                ByShaderPass[shader] = passes;
            }
            if (!passes.TryGetValue(pass, out var c))
            {
                c = new PassCounters();
                passes[pass] = c;
            }
            return c;
        }

        /// <summary>1 バリアントの判定結果を集計へ反映する。</summary>
        public static void RecordDecision(string shader, string pass, StripDecision d, bool actuallyStripped, bool reportOnly)
        {
            _hasData = true;
            var c = GetCounters(shader, pass);

            if (d.UnknownKeywords != null && d.UnknownKeywords.Count > 0)
                RecordUnknown(d.UnknownKeywords);
            if (d.AggressiveDowngraded)
                _aggressiveDowngradeCount++;

            bool wouldStrip = d.Action == NataneStripAction.Strip;

            if (actuallyStripped)
            {
                c.stripped++;
                TotalStripped++;
                if (d.TriggerKeywords != null)
                    foreach (var kw in d.TriggerKeywords)
                        StrippedKeywordCounts[kw] = StrippedKeywordCounts.TryGetValue(kw, out var n) ? n + 1 : 1;
            }
            else
            {
                c.kept++;
                TotalKept++;
                string reason = string.IsNullOrEmpty(d.Reason) ? "(理由なし)" : d.Reason;
                if (reportOnly && wouldStrip)
                    reason = "ReportOnly: Safe なら削除される候補";
                KeepReasonCounts[reason] = KeepReasonCounts.TryGetValue(reason, out var m) ? m + 1 : 1;
            }

            if (wouldStrip)
            {
                c.candidates++;
                TotalCandidates++;
            }
        }

        public static string BuildConsoleSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Natane 統合ストリッパー] バリアントストリップ要約");
            sb.AppendLine($"  モード: {_mode}");
            sb.AppendLine($"  処理対象: {TotalSeen} / 保持: {TotalKept} / 削除: {TotalStripped} / 削除候補(Safe): {TotalCandidates}");
            sb.AppendLine($"  SVC 由来保持: {_svcKeptCount} / 未知キーワード: {UnknownKeywordSet.Count} / 処理時間: {_elapsedMs:F1}ms");
            if (_aggressiveCandidateCount > 0)
                sb.AppendLine($"  Aggressive 削除候補（ReportOnly 集計）: {_aggressiveCandidateCount}");
            if (_aggressiveDowngradeCount > 0)
                sb.AppendLine($"  ※ Aggressive 指定を Safe へ降格して処理（{_aggressiveDowngradeCount} バリアント）");
            if (FallbackReasons.Count > 0)
            {
                sb.AppendLine("  フォールバック（全保持）理由:");
                foreach (var r in FallbackReasons) sb.AppendLine("    - " + r);
            }
            if (KeepReasonCounts.Count > 0)
            {
                sb.AppendLine("  保持理由内訳:");
                foreach (var kv in KeepReasonCounts.OrderByDescending(k => k.Value))
                    sb.AppendLine($"    - {kv.Key}: {kv.Value}");
            }
            return sb.ToString();
        }

        // ---- JSON 保存（Library/NataneToon/Reports/variant-stripping-<timestamp>.json） ----

        [Serializable]
        private sealed class PassReport
        {
            public string pass;
            public int kept;
            public int stripped;
            public int candidates;
        }

        [Serializable]
        private sealed class ShaderReport
        {
            public string shader;
            public List<PassReport> passes = new List<PassReport>();
        }

        [Serializable]
        private sealed class CountEntry
        {
            public string key;
            public int count;
        }

        [Serializable]
        private sealed class DeltaReport
        {
            public string previousReportFile; // 直前レポートのファイル名（無ければ空）
            public int previousKept;
            public int previousStripped;
            public int previousCandidates;
            public int deltaKept;
            public int deltaStripped;
            public int deltaCandidates;
        }

        [Serializable]
        private sealed class StageTimingsReport
        {
            public double indexCatchUpMs;
            public double auditMs;
            public double snapshotBuildMs;
            public double strippingMs;
        }

        [Serializable]
        private sealed class RootReport
        {
            public string context;
            public string mode;
            public string generatedAt;
            public int totalSeen;
            public int totalKept;
            public int totalStripped;
            public int totalCandidates;
            public int aggressiveCandidateCount;
            public int aggressiveDowngradeCount;
            public int svcKeptCount;
            public double elapsedMs;
            public StageTimingsReport stageTimings = new StageTimingsReport();
            public DeltaReport previousComparison = new DeltaReport();
            public List<string> unknownKeywords = new List<string>();
            public List<string> fallbackReasons = new List<string>();
            public List<CountEntry> keepReasonCounts = new List<CountEntry>();
            public List<CountEntry> strippedKeywordCounts = new List<CountEntry>();
            public List<ShaderReport> shaders = new List<ShaderReport>();
        }

        // 直前レポート（今回より前の最新 JSON）の総数を読み取り、差分算出に用いる（読取失敗は無視）。
        [Serializable]
        private sealed class PrevTotals
        {
            public int totalKept;
            public int totalStripped;
            public int totalCandidates;
        }

        public static string SaveJson(string context)
        {
            var root = new RootReport
            {
                context = context,
                mode = _mode,
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalSeen = TotalSeen,
                totalKept = TotalKept,
                totalStripped = TotalStripped,
                totalCandidates = TotalCandidates,
                aggressiveCandidateCount = _aggressiveCandidateCount,
                aggressiveDowngradeCount = _aggressiveDowngradeCount,
                svcKeptCount = _svcKeptCount,
                elapsedMs = _elapsedMs,
                unknownKeywords = UnknownKeywordSet.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                fallbackReasons = new List<string>(FallbackReasons)
            };

            // ビルド段階の計測時間を集約（未計測は 0）。
            root.stageTimings.indexCatchUpMs = NataneBuildStageTimings.IndexCatchUpMs;
            root.stageTimings.auditMs = NataneBuildStageTimings.AuditMs;
            root.stageTimings.snapshotBuildMs = NataneBuildStageTimings.SnapshotBuildMs;
            root.stageTimings.strippingMs = _elapsedMs;

            // 直前レポート（今回書き出す前の最新 JSON）との保持/削除/候補差分。
            PopulatePreviousComparison(root);
            foreach (var kv in KeepReasonCounts.OrderByDescending(k => k.Value))
                root.keepReasonCounts.Add(new CountEntry { key = kv.Key, count = kv.Value });
            foreach (var kv in StrippedKeywordCounts.OrderByDescending(k => k.Value))
                root.strippedKeywordCounts.Add(new CountEntry { key = kv.Key, count = kv.Value });
            foreach (var s in ByShaderPass.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var sr = new ShaderReport { shader = s.Key };
                foreach (var p in s.Value.OrderBy(k => k.Key, StringComparer.Ordinal))
                    sr.passes.Add(new PassReport
                    {
                        pass = p.Key,
                        kept = p.Value.kept,
                        stripped = p.Value.stripped,
                        candidates = p.Value.candidates
                    });
                root.shaders.Add(sr);
            }

            try
            {
                string dir = NataneStrippingReportPaths.ReportsDir;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir,
                    $"variant-stripping-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(root, true));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane 統合ストリッパー] レポート JSON 保存に失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 直前レポート（今回書き出す前の最新 variant-stripping-*.json）を読み、保持/削除/候補の差分を埋める。
        /// 読取不能・不在は差分 0 のまま（安全側）。
        /// </summary>
        private static void PopulatePreviousComparison(RootReport root)
        {
            try
            {
                string dir = NataneStrippingReportPaths.ReportsDir;
                if (!System.IO.Directory.Exists(dir)) return;

                string[] files = System.IO.Directory.GetFiles(dir, "variant-stripping-*.json");
                if (files == null || files.Length == 0) return;

                Array.Sort(files, StringComparer.Ordinal); // タイムスタンプ名のため辞書順＝時刻順
                string prevFile = files[files.Length - 1]; // 今回はまだ未書出しのため最後＝直前

                var prev = JsonUtility.FromJson<PrevTotals>(System.IO.File.ReadAllText(prevFile));
                if (prev == null) return;

                root.previousComparison.previousReportFile = System.IO.Path.GetFileName(prevFile);
                root.previousComparison.previousKept = prev.totalKept;
                root.previousComparison.previousStripped = prev.totalStripped;
                root.previousComparison.previousCandidates = prev.totalCandidates;
                root.previousComparison.deltaKept = TotalKept - prev.totalKept;
                root.previousComparison.deltaStripped = TotalStripped - prev.totalStripped;
                root.previousComparison.deltaCandidates = TotalCandidates - prev.totalCandidates;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane 統合ストリッパー] 前回レポートとの差分算出に失敗（差分省略）: {ex.Message}");
            }
        }

        /// <summary>データがあれば Console 要約 + JSON を書き出す（VRChat 経路の遅延保存で利用）。</summary>
        public static void FlushIfPending(string context)
        {
            if (!HasData) return;
            Debug.Log(BuildConsoleSummary());
            SaveJson(context);
        }
    }

    /// <summary>
    /// ビルド段階（Index キャッチアップ / Audit / Snapshot 生成）の計測時間をセッション単位で保持する。
    /// SnapshotBuilder が計測値を書き込み、ストリップレポートが集約する。ドメイン内 static（ビルドは同一ドメイン完結）。
    /// </summary>
    public static class NataneBuildStageTimings
    {
        public static double IndexCatchUpMs { get; private set; }
        public static double AuditMs { get; private set; }
        public static double SnapshotBuildMs { get; private set; }

        public static void Reset()
        {
            IndexCatchUpMs = 0;
            AuditMs = 0;
            SnapshotBuildMs = 0;
        }

        public static void SetIndexCatchUpMs(double ms) => IndexCatchUpMs = ms;
        public static void SetAuditMs(double ms) => AuditMs = ms;
        public static void SetSnapshotBuildMs(double ms) => SnapshotBuildMs = ms;
    }

    internal static class NataneStrippingReportPaths
    {
        public static string ReportsDir
        {
            get
            {
                string root = System.IO.Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
                return System.IO.Path.Combine(root, "Library", "NataneToon", "Reports");
            }
        }
    }

    /// <summary>
    /// 既存 2 系統（NataneShaderVariantStripper / Tools.ShaderVariantStripper）を置き換える単一の
    /// IPreprocessShaders 実装。旧 2 系統は OnProcessShader 冒頭で即 return し無効化済み。
    /// callbackOrder=110（旧 100 と分離）。判定は純関数 NataneUnifiedStripDecider へ委譲する。
    /// </summary>
    public sealed class NataneUnifiedVariantStripper : IPreprocessShaders, IPreprocessBuildWithReport
    {
        public int callbackOrder => 110;

        // ビルドセッション内でのみ有効な Snapshot/View キャッシュ（同一ドメイン内で完結）。
        private static NataneStripSnapshotView _cachedView;
        private static bool _acquisitionAttempted;
        private static bool _acquisitionFailed;

        // Aggressive 続行ガードの評価結果（Snapshot 取得時に 1 回だけ算出）。
        private static NataneAggressiveGateResult _aggressiveGate;
        // Strict 時にガード不通過を検出したら 1 回だけ例外を投げるためのフラグ。
        private static bool _aggressiveStrictFailAnnounced;

        public void OnPreprocessBuild(BuildReport report)
        {
            // 通常のプレイヤー/アセットバンドルビルド開始時に状態を初期化。
            // （VRChat では本コールバックは発火しないため、VRChat フック側でも初期化する。）
            BeginSession();
        }

        /// <summary>ビルドセッション開始。前回分の未保存レポートを掃き出してから初期化する。</summary>
        public static void BeginSession()
        {
            NataneStrippingSessionReport.FlushIfPending("previous-session");
            ResetState();
        }

        internal static void ResetState()
        {
            _cachedView = null;
            _acquisitionAttempted = false;
            _acquisitionFailed = false;
            _aggressiveGate = null;
            _aggressiveStrictFailAnnounced = false;
            NataneBuildStageTimings.Reset();
            NataneStrippingSessionReport.Reset();
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            var settings = NataneBuildPolicySettings.instance;
            NataneOptimizationMode mode = settings.OptimizationMode;

            // Disabled もしくはマスタースイッチ OFF は全保持（即 return）。
            if (mode == NataneOptimizationMode.Disabled || !settings.VariantStrippingEnabled)
                return;
            if (shader == null || !NataneShaderCatalog.IsNataneShader(shader.name))
                return;
            if (data == null || data.Count == 0)
                return;

            var sw = Stopwatch.StartNew();
            try
            {
                if (!EnsureSnapshotView(settings))
                {
                    // Snapshot 取得失敗: strict=FailBuild は中止、それ以外は全保持。
                    if (settings.StrictFailurePolicy == NataneStrictFailurePolicy.FailBuild)
                    {
                        throw new BuildFailedException(
                            "[Natane 統合ストリッパー] Build Usage Snapshot を取得できず、strict 方針(FailBuild)によりビルドを中止しました。");
                    }
                    return; // フォールバック理由は EnsureSnapshotView 内で記録済み。
                }

                // 常時保持シェーダー（GUID 直接判定。View の名前解決漏れを補完）。
                if (IsAlwaysKeepShaderByGuid(shader, settings))
                    return;

                // Aggressive 指定時は続行ガードを評価。Strict 不通過ならここでビルド停止。
                bool aggressiveGatePassed = ResolveAggressiveGate(mode, settings);

                var policy = new NataneStripPolicyView { Mode = mode, AggressiveGatePassed = aggressiveGatePassed };
                bool reportOnly = mode == NataneOptimizationMode.ReportOnly;
                string passLabel = string.IsNullOrEmpty(snippet.passName)
                    ? snippet.passType.ToString()
                    : $"{snippet.passType}:{snippet.passName}";

                for (int i = data.Count - 1; i >= 0; i--)
                {
                    List<string> keywords = GetKeywordNames(data[i]);
                    StripDecision decision = NataneUnifiedStripDecider.Decide(shader.name, keywords, _cachedView, policy);
                    bool actuallyStripped = false;

                    // ReportOnly では実削除しない代わりに、Aggressive なら削除される候補を別途集計する。
                    if (reportOnly)
                    {
                        var aggressivePolicy = new NataneStripPolicyView
                        {
                            Mode = NataneOptimizationMode.Aggressive,
                            AggressiveGatePassed = aggressiveGatePassed
                        };
                        StripDecision aggressive = NataneUnifiedStripDecider.Decide(
                            shader.name, keywords, _cachedView, aggressivePolicy);
                        if (aggressive.Action == NataneStripAction.Strip)
                            NataneStrippingSessionReport.RecordAggressiveCandidate();
                    }

                    // ReportOnly は削除しない。Safe/Aggressive のみ削除実行。
                    if (!reportOnly && decision.Action == NataneStripAction.Strip)
                    {
                        if (IsVariantHeldBySvc(shader, snippet.passType, keywords))
                        {
                            NataneStrippingSessionReport.RecordSvcKept();
                        }
                        else
                        {
                            data.RemoveAt(i);
                            actuallyStripped = true;
                        }
                    }

                    NataneStrippingSessionReport.RecordDecision(shader.name, passLabel, decision, actuallyStripped, reportOnly);
                }
            }
            catch (BuildFailedException)
            {
                throw; // strict 中止はそのまま伝播。
            }
            catch (Exception ex)
            {
                // 判定中の予期せぬ例外はビルドを止めず全保持で継続。
                Debug.LogWarning($"[Natane 統合ストリッパー] 判定中に例外が発生。全保持で継続します: {ex.Message}");
                NataneStrippingSessionReport.RecordFallback("判定中の例外: " + ex.Message);
            }
            finally
            {
                sw.Stop();
                NataneStrippingSessionReport.AddElapsed(sw.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Snapshot 取得: セッション優先 → ストア(鮮度確認) → その場生成(自己救済)。
        /// 取得できたら名前ベースビューを構築しキャッシュする。全滅なら false。
        /// </summary>
        private static bool EnsureSnapshotView(NataneBuildPolicySettings settings)
        {
            if (_acquisitionAttempted)
                return !_acquisitionFailed;
            _acquisitionAttempted = true;

            NataneBuildUsageSnapshot snap = null;

            if (NataneBuildSession.IsActive && NataneBuildSession.CurrentSnapshot != null)
                snap = NataneBuildSession.CurrentSnapshot;

            if (snap == null &&
                NataneBuildUsageSnapshotStore.TryLoad(out var loaded) &&
                !NataneBuildUsageSnapshotStore.IsSnapshotStale(loaded))
            {
                snap = loaded;
            }

            if (snap == null)
            {
                // VRChat 等で IPreprocessShaders のみ発火するケースの自己救済。
                try
                {
                    SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(
                        EditorUserBuildSettings.activeBuildTarget, scenePathsOrNull: null, saveSyncToDisk: false);
                    if (result.Succeeded)
                    {
                        snap = result.Snapshot;
                        NataneBuildUsageSnapshotStore.Save(snap);
                        if (NataneBuildSession.IsActive)
                            NataneBuildSession.SetSnapshot(snap);
                    }
                    else
                    {
                        NataneStrippingSessionReport.RecordFallback("Snapshot 生成失敗: " + result.FailureReason);
                    }
                }
                catch (Exception ex)
                {
                    NataneStrippingSessionReport.RecordFallback("Snapshot 生成例外: " + ex.Message);
                }
            }

            if (snap == null)
            {
                _acquisitionFailed = true;
                Debug.LogWarning("[Natane 統合ストリッパー] Build Usage Snapshot を取得できませんでした（全保持で継続）。");
                return false;
            }

            _cachedView = NataneStripSnapshotView.FromSnapshot(snap);
            if (snap.auditSummary?.unknownKeywords != null)
                NataneStrippingSessionReport.RecordUnknown(snap.auditSummary.unknownKeywords);
            return true;
        }

        /// <summary>
        /// Aggressive 続行ガードを評価して通過可否を返す。Aggressive 以外は常に false（ガード無関係）扱いで、
        /// Decide 側は Safe として処理する。ガード不通過かつ Strict(FailBuild) の場合はここでビルドを止める。
        /// 降格時は理由をセッションレポート＋警告ログへ 1 回だけ記録する。
        /// </summary>
        private static bool ResolveAggressiveGate(NataneOptimizationMode mode, NataneBuildPolicySettings settings)
        {
            if (mode != NataneOptimizationMode.Aggressive)
                return false;

            if (_aggressiveGate == null)
                _aggressiveGate = EvaluateAggressiveGateFromView();

            NataneAggressiveResolution resolution =
                NataneAggressiveGate.Resolve(_aggressiveGate.Proceed, settings.StrictFailurePolicy);

            if (resolution == NataneAggressiveResolution.Proceed)
                return true;

            string reasonText = _aggressiveGate.Reasons.Count > 0
                ? string.Join(" / ", _aggressiveGate.Reasons)
                : "続行ガード不通過";

            if (resolution == NataneAggressiveResolution.FailBuild)
            {
                throw new BuildFailedException(
                    "[Natane 統合ストリッパー] Aggressive 続行ガード不通過（" + reasonText +
                    "）かつ strict 方針(FailBuild)のためビルドを中止しました。");
            }

            // 降格（通常方針）。理由記録は 1 回のみ。
            if (!_aggressiveStrictFailAnnounced)
            {
                _aggressiveStrictFailAnnounced = true;
                NataneStrippingSessionReport.RecordFallback("Aggressive を Safe へ降格: " + reasonText);
                Debug.LogWarning("[Natane 統合ストリッパー] Aggressive 続行ガード不通過のため Safe へ降格します: " + reasonText);
            }
            return false;
        }

        /// <summary>取得済み Snapshot / 現状の監査状態から Aggressive 続行ガードを評価する。</summary>
        private static NataneAggressiveGateResult EvaluateAggressiveGateFromView()
        {
            bool hasUnknown = false;
            bool schemaMismatch = false;
            bool hasUnparseable = false;

            if (NataneBuildUsageSnapshotStore.TryLoad(out var snap) && snap != null)
            {
                hasUnknown = snap.auditSummary != null && snap.auditSummary.unknownKeywordCount > 0;
                hasUnparseable = (snap.unparseableItems != null && snap.unparseableItems.Count > 0) ||
                                 (snap.auditSummary != null && snap.auditSummary.unparseableItemCount > 0);
                schemaMismatch = snap.registrySchemaVersion != NataneShaderFeatureRegistry.RegistrySchemaVersion ||
                                 snap.registryFingerprint != NataneShaderFeatureRegistry.ComputeRegistryFingerprint();
            }

            bool auditStale = NataneShaderUpdateAudit.IsStale();
            return NataneAggressiveGate.Evaluate(hasUnknown, schemaMismatch, auditStale, hasUnparseable);
        }

        private static bool IsAlwaysKeepShaderByGuid(Shader shader, NataneBuildPolicySettings settings)
        {
            var guids = settings.AlwaysKeepShaderGuids;
            if (guids == null || guids.Count == 0) return false;
            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path)) return false;
            string guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid) && guids.Contains(guid);
        }

        private static List<string> GetKeywordNames(ShaderCompilerData variant)
        {
            var list = new List<string>();
            foreach (ShaderKeyword k in variant.shaderKeywordSet.GetShaderKeywords())
            {
                if (!string.IsNullOrEmpty(k.name))
                    list.Add(k.name);
            }
            return list;
        }

        /// <summary>
        /// 指定 SVC 群のいずれかが当該バリアントを含めば true（=保持）。公開 API のみ・Reflection 禁止。
        /// ShaderVariant コンストラクタ/Contains は無効な組合せで例外を投げるため、例外時は保持側へ倒す。
        /// </summary>
        internal static bool IsVariantHeldBySvc(Shader shader, PassType passType, IReadOnlyList<string> keywords)
        {
            var guids = NataneBuildPolicySettings.instance.ShaderVariantCollectionGuids;
            if (guids == null || guids.Count == 0) return false;

            string[] kwArray = keywords == null
                ? Array.Empty<string>()
                : keywords.Where(k => !string.IsNullOrEmpty(k)).ToArray();

            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
                if (svc == null) continue;

                try
                {
                    var variant = new ShaderVariantCollection.ShaderVariant(shader, passType, kwArray);
                    if (svc.Contains(variant))
                        return true;
                }
                catch (Exception ex)
                {
                    // 読取/構築失敗はそのバリアントを保持側へ倒す（削除継続禁止）。
                    Debug.LogWarning($"[Natane 統合ストリッパー] SVC 判定に失敗したため保持します（{path}）: {ex.Message}");
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// プレイヤー/アセットバンドルビルド後にストリップ集計を保存 + Console 要約する（order 999）。
    /// VRChat では IPostprocessBuild が不発のため、次回ビルド開始時の掃き出し（BeginSession）で永続化される。
    /// </summary>
    public sealed class NataneStrippingReportPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 999;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                if (NataneStrippingSessionReport.HasData)
                {
                    Debug.Log(NataneStrippingSessionReport.BuildConsoleSummary());
                    NataneStrippingSessionReport.SaveJson(
                        report != null ? "player-build:" + report.summary.platform : "player-build");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane 統合ストリッパー] ビルド後レポート出力に失敗: {ex.Message}");
            }
        }
    }
}
