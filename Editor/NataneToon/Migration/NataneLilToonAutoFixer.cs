using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// lilToon → Natane マイグレーション後に自動的にマテリアルの見た目を最適化する AutoFixer。
    /// 既存の Auto Setup System コンポーネント (TextureAnalyzer, MeshAnalyzer, CrossFeatureResolver,
    /// FeaturePresetTable, ShaderKeywordSynchronizer) を統合して使用する。
    /// </summary>
    public static class NataneLilToonAutoFixer
    {
        // ===== 結果クラス =====

        /// <summary>
        /// AutoFix の実行結果。品質スコア・実行アクション・警告・検出ロールを含む。
        /// </summary>
        public class AutoFixResult
        {
            /// <summary>品質スコア (0-100)。高いほど自動設定の完成度が高い。</summary>
            public int qualityScore;

            /// <summary>実行されたアクションの一覧。</summary>
            public List<string> actions = new List<string>();

            /// <summary>手動確認が必要な警告の一覧。</summary>
            public List<string> warnings = new List<string>();

            /// <summary>マテリアル名から自動検出されたロール。</summary>
            public AutoSetupRole detectedRole;
        }

        // ===== ロール自動検出用キーワード =====

        private static readonly (string[] keywords, AutoSetupRole role)[] RoleDetectionRules =
        {
            // [1] マテリアル名からロールを推定
            (new[] { "skin", "face", "肌", "顔", "body" }, AutoSetupRole.Face),
            (new[] { "hair", "髪" }, AutoSetupRole.Hair),
            (new[] { "eye", "目", "iris" }, AutoSetupRole.Eye),
            (new[] { "cloth", "服", "dress", "shirt", "pants", "skirt" }, AutoSetupRole.Clothing),
        };

        // ===== テクスチャベース機能の自動有効化マッピング =====

        private static readonly (string textureProp, string toggleProp, string keyword, string actionLabel)[] TextureFeatureMappings =
        {
            ("_MatCapTex",   "_MatCap",    "_MATCAP",     "MatCap テクスチャ検出 → MatCap 有効化"),
            ("_EmissionMap", "_Emission",  "_EMISSION",   "Emission テクスチャ検出 → Emission 有効化"),
            ("_BumpMap",     "_UseNormalMap", "_NORMALMAP", "NormalMap テクスチャ検出 → NormalMap 有効化"),
            ("_SDFMap",      "_UseSDFMap", "_SDF_MAP",    "SDF テクスチャ検出 → SDF 有効化"),
        };

        // ===== メイン API =====

        /// <summary>
        /// lilToon からマイグレーションされたマテリアルを自動最適化する。
        /// </summary>
        /// <param name="material">最適化対象のマテリアル。</param>
        /// <param name="lilToonSourceProperties">lilToon 側の元プロパティ値 (キー: プロパティ名, 値: object)。</param>
        /// <returns>AutoFix 結果。null マテリアルの場合はスコア 0 の結果を返す。</returns>
        public static AutoFixResult Fix(Material material, Dictionary<string, object> lilToonSourceProperties)
        {
            var result = new AutoFixResult();

            // null チェック
            if (material == null)
            {
                result.qualityScore = 0;
                result.warnings.Add("マテリアルが null です。AutoFix をスキップしました。");
                return result;
            }

            if (lilToonSourceProperties == null)
                lilToonSourceProperties = new Dictionary<string, object>();

            // Undo 登録
            Undo.RecordObject(material, "Natane AutoFix");

            // 品質スコア計算用カウンタ
            int parityWarnings = 0;
            int unconfiguredFeatures = 0;
            int textureColorApplied = 0;

            // ----------------------------------------------------------------
            // [1] ロール自動検出
            // ----------------------------------------------------------------
            result.detectedRole = DetectRole(material.name);
            result.actions.Add($"ロール自動検出: {result.detectedRole} (マテリアル名: {material.name})");

            // ----------------------------------------------------------------
            // [2] シャドウ境界パラメータ最適化 (lilToon → Natane 変換式)
            // ----------------------------------------------------------------
            try
            {
                ApplyShadowBoundaryOptimization(material, lilToonSourceProperties, result);
            }
            catch (Exception e)
            {
                result.warnings.Add($"シャドウ境界最適化でエラー: {e.Message}");
                unconfiguredFeatures++;
            }

            // ----------------------------------------------------------------
            // [3] カラー Alpha → Intensity 自動分離
            // ----------------------------------------------------------------
            try
            {
                ApplyColorAlphaSeparation(material, result);
            }
            catch (Exception e)
            {
                result.warnings.Add($"カラー Alpha 分離でエラー: {e.Message}");
                unconfiguredFeatures++;
            }

            // ----------------------------------------------------------------
            // [4] テクスチャベース機能の自動有効化
            // ----------------------------------------------------------------
            try
            {
                ApplyTextureBasedFeatureEnable(material, result);
            }
            catch (Exception e)
            {
                result.warnings.Add($"テクスチャベース機能有効化でエラー: {e.Message}");
                unconfiguredFeatures++;
            }

            // ----------------------------------------------------------------
            // [5] NataneTextureAnalyzer 統合 (色がデフォルト/未設定の場合)
            // ----------------------------------------------------------------
            try
            {
                textureColorApplied = ApplyTextureAnalysis(material, result);
            }
            catch (Exception e)
            {
                result.warnings.Add($"テクスチャ解析でエラー: {e.Message}");
                unconfiguredFeatures++;
            }

            // ----------------------------------------------------------------
            // [6] NataneMeshAnalyzer 統合 (シーン内にメッシュが見つかった場合)
            // ----------------------------------------------------------------
            try
            {
                ApplyMeshAnalysis(material, result);
            }
            catch (Exception e)
            {
                result.warnings.Add($"メッシュ解析でエラー: {e.Message}");
                unconfiguredFeatures++;
            }

            // ----------------------------------------------------------------
            // [7] NataneCrossFeatureResolver 統合
            // ----------------------------------------------------------------
            try
            {
                NataneCrossFeatureResolver.Resolve(material);
                result.actions.Add("CrossFeatureResolver: 機能間最適化を実行");

                NataneCrossFeatureResolver.DetectAndFixArtifacts(material);
                result.actions.Add("CrossFeatureResolver: アーティファクト検出・修正を実行");
            }
            catch (Exception e)
            {
                result.warnings.Add($"CrossFeatureResolver でエラー: {e.Message}");
                parityWarnings++;
            }

            // ----------------------------------------------------------------
            // [8] NataneFeaturePresetTable 統合
            //     lilToon 側でデフォルト値だった機能にプリセットテーブル値を適用
            // ----------------------------------------------------------------
            try
            {
                ApplyFeaturePresetDefaults(material, lilToonSourceProperties, result, ref unconfiguredFeatures);
            }
            catch (Exception e)
            {
                result.warnings.Add($"FeaturePresetTable 適用でエラー: {e.Message}");
                unconfiguredFeatures++;
            }

            // ----------------------------------------------------------------
            // [9] キーワード同期 + 品質スコア計算
            // ----------------------------------------------------------------
            try
            {
                bool keywordsChanged = NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(material);
                if (keywordsChanged)
                    result.actions.Add("ShaderKeywordSynchronizer: キーワード同期を実行");
            }
            catch (Exception e)
            {
                result.warnings.Add($"キーワード同期でエラー: {e.Message}");
                parityWarnings++;
            }

            // 品質スコア計算
            //   基本: 100
            //   -5: parity 警告ごと
            //   -3: 自動設定できなかった機能ごと
            //   +2: テクスチャ解析で適用された色ごと
            //   0-100 にクランプ
            result.qualityScore = Mathf.Clamp(
                100 - (parityWarnings * 5) - (unconfiguredFeatures * 3) + (textureColorApplied * 2),
                0, 100);

            result.actions.Add($"品質スコア: {result.qualityScore}/100");

            // マテリアルをダーティに設定
            EditorUtility.SetDirty(material);

            return result;
        }

        // ===== [1] ロール自動検出 =====

        /// <summary>
        /// マテリアル名からロールを推定する。
        /// 該当なしの場合は Clothing (汎用) を返す。
        /// </summary>
        private static AutoSetupRole DetectRole(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return AutoSetupRole.Clothing;

            string lower = materialName.ToLowerInvariant();

            foreach (var rule in RoleDetectionRules)
            {
                foreach (string keyword in rule.keywords)
                {
                    if (lower.Contains(keyword))
                        return rule.role;
                }
            }

            // デフォルトは Clothing (汎用ロール)
            return AutoSetupRole.Clothing;
        }

        // ===== [2] シャドウ境界パラメータ最適化 =====

        /// <summary>
        /// lilToon のシャドウ境界パラメータを Natane の ShadowSharpness / ShadowOffset に変換する。
        /// 変換式:
        ///   _ShadowSharpness = max(lilBlur * 1.2, 0.05)
        ///   _ShadowOffset = (0.5 - lilBorder) * (1.0 + lilBlur * 0.3)
        /// </summary>
        private static void ApplyShadowBoundaryOptimization(
            Material material,
            Dictionary<string, object> lilProps,
            AutoFixResult result)
        {
            // lilToon のシャドウパラメータを取得
            // マテリアル上の値を優先、なければ lilToonSourceProperties から取得
            float lilBorder = GetLilToonFloat(material, lilProps, "_STShadowBorder", 0.5f);
            float lilBlur = GetLilToonFloat(material, lilProps, "_STShadowBlur", 0.1f);

            // Natane 変換式
            float shadowSharpness = Mathf.Max(lilBlur * 1.2f, 0.05f);
            float shadowOffset = (0.5f - lilBorder) * (1.0f + lilBlur * 0.3f);

            // 適用
            if (material.HasProperty("_ShadowSharpness"))
            {
                material.SetFloat("_ShadowSharpness", shadowSharpness);
                result.actions.Add($"シャドウ境界最適化: _ShadowSharpness = {shadowSharpness:F3} (lilBlur={lilBlur:F3})");
            }

            if (material.HasProperty("_ShadowOffset"))
            {
                material.SetFloat("_ShadowOffset", shadowOffset);
                result.actions.Add($"シャドウ境界最適化: _ShadowOffset = {shadowOffset:F3} (lilBorder={lilBorder:F3})");
            }
        }

        // ===== [3] カラー Alpha → Intensity 自動分離 =====

        /// <summary>
        /// lilToon では色の Alpha で強度を表現することがあるが、
        /// Natane では別パラメータ (Intensity) で管理するため分離する。
        /// </summary>
        private static void ApplyColorAlphaSeparation(Material material, AutoFixResult result)
        {
            // RimColor: Alpha < 1.0 → _RimIntensity に抽出、Alpha を 1.0 に
            if (material.HasProperty("_RimColor") && material.HasProperty("_RimIntensity"))
            {
                Color rimColor = material.GetColor("_RimColor");
                if (rimColor.a < 0.99f)
                {
                    float extractedIntensity = rimColor.a;
                    rimColor.a = 1.0f;
                    material.SetColor("_RimColor", rimColor);
                    material.SetFloat("_RimIntensity", extractedIntensity);
                    result.actions.Add($"RimColor Alpha 分離: Alpha={extractedIntensity:F2} → _RimIntensity");
                }
            }

            // Shadow2ndColor: Alpha < 1.0 → 強度乗数として適用
            if (material.HasProperty("_Shadow2ndColor"))
            {
                Color shadow2nd = material.GetColor("_Shadow2ndColor");
                if (shadow2nd.a < 0.99f)
                {
                    float strengthMultiplier = shadow2nd.a;
                    // Alpha を使って RGB を減衰させ、Alpha を 1.0 にする
                    shadow2nd.r *= strengthMultiplier;
                    shadow2nd.g *= strengthMultiplier;
                    shadow2nd.b *= strengthMultiplier;
                    shadow2nd.a = 1.0f;
                    material.SetColor("_Shadow2ndColor", shadow2nd);
                    result.actions.Add($"Shadow2ndColor Alpha 分離: Alpha={strengthMultiplier:F2} → RGB に乗算");
                }
            }
        }

        // ===== [4] テクスチャベース機能の自動有効化 =====

        /// <summary>
        /// テクスチャが設定されているのに対応する機能が無効な場合、自動的に有効にする。
        /// </summary>
        private static void ApplyTextureBasedFeatureEnable(Material material, AutoFixResult result)
        {
            foreach (var mapping in TextureFeatureMappings)
            {
                if (!material.HasProperty(mapping.textureProp))
                    continue;
                if (!material.HasProperty(mapping.toggleProp))
                    continue;

                Texture tex = material.GetTexture(mapping.textureProp);
                float toggleValue = material.GetFloat(mapping.toggleProp);

                // テクスチャあり & 機能 OFF → 有効化
                if (tex != null && toggleValue < 0.5f)
                {
                    material.SetFloat(mapping.toggleProp, 1.0f);
                    material.EnableKeyword(mapping.keyword);
                    result.actions.Add(mapping.actionLabel);
                }
            }
        }

        // ===== [5] NataneTextureAnalyzer 統合 =====

        /// <summary>
        /// テクスチャ解析で色がデフォルト/未設定の場合に自動適用する。
        /// </summary>
        /// <returns>適用された色の数。</returns>
        private static int ApplyTextureAnalysis(Material material, AutoFixResult result)
        {
            int appliedCount = 0;

            // 色がデフォルト（灰色や白）に近い場合のみテクスチャ解析を実行
            bool colorsAreDefault = AreColorsDefault(material);
            if (!colorsAreDefault)
            {
                result.actions.Add("テクスチャ解析: カスタム色が設定済みのためスキップ");
                return 0;
            }

            // AutoSetupRecord を作成（テクスチャ解析結果の記録用）
            var record = new AutoSetupRecord
            {
                role = result.detectedRole,
                look = AutoSetupLook.Anime,
                quality = AutoSetupQuality.Standard,
            };

            var colorResult = NataneTextureAnalyzer.AnalyzeAndApplyColors(
                material, result.detectedRole, record);

            if (colorResult.valid)
            {
                result.actions.Add("テクスチャ解析: ShadowColor を自動設定");
                appliedCount++;

                if (material.HasProperty("_RimColor"))
                {
                    result.actions.Add("テクスチャ解析: RimColor を自動設定");
                    appliedCount++;
                }
                if (material.HasProperty("_OutlineColor"))
                {
                    result.actions.Add("テクスチャ解析: OutlineColor を自動設定");
                    appliedCount++;
                }
                if (material.HasProperty("_SpecularColor"))
                {
                    result.actions.Add("テクスチャ解析: SpecularColor を自動設定");
                    appliedCount++;
                }

                // BumpScale 自動調整
                if (material.HasProperty("_BumpMap") && material.HasProperty("_BumpScale"))
                {
                    var bumpMap = material.GetTexture("_BumpMap") as Texture2D;
                    if (bumpMap != null)
                    {
                        float autoScale = NataneTextureAnalyzer.AutoBumpScale(bumpMap);
                        material.SetFloat("_BumpScale", autoScale);
                        result.actions.Add($"テクスチャ解析: BumpScale = {autoScale:F2}");
                        appliedCount++;
                    }
                }

                // Record を保存
                record.Save(material);
            }
            else
            {
                result.warnings.Add("テクスチャ解析: MainTex が見つからないか読み取り不可");
            }

            return appliedCount;
        }

        // ===== [6] NataneMeshAnalyzer 統合 =====

        /// <summary>
        /// シーン内のメッシュを探してアウトライン幅・NormalFlattenY・BumpScale を最適化する。
        /// </summary>
        private static void ApplyMeshAnalysis(Material material, AutoFixResult result)
        {
            // シーン内でこのマテリアルを使っているメッシュを検索
            Mesh mesh = NataneMeshAnalyzer.FindMeshForMaterial(material);
            if (mesh == null)
            {
                result.warnings.Add("メッシュ解析: シーン内に対応メッシュが見つかりません（手動調整推奨）");
                return;
            }

            // テクスチャ付きで解析（テクセル密度の正確な計算のため）
            var mainTex = material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex") as Texture2D
                : null;

            NataneMeshAnalyzer.MeshAnalysisResult meshResult;
            if (mainTex != null)
                meshResult = NataneMeshAnalyzer.Analyze(mesh, result.detectedRole, mainTex);
            else
                meshResult = NataneMeshAnalyzer.Analyze(mesh, result.detectedRole);

            if (!meshResult.valid)
            {
                result.warnings.Add("メッシュ解析: 解析結果が無効です");
                return;
            }

            // OutlineWidth 補正
            if (material.HasProperty("_OutlineWidth"))
            {
                material.SetFloat("_OutlineWidth", meshResult.outlineWidth);
                result.actions.Add($"メッシュ解析: OutlineWidth = {meshResult.outlineWidth:F4} (形状: {meshResult.shape})");
            }

            // NormalFlattenY 補正
            if (material.HasProperty("_NormalFlattenY"))
            {
                material.SetFloat("_NormalFlattenY", meshResult.normalFlattenY);
                result.actions.Add($"メッシュ解析: NormalFlattenY = {meshResult.normalFlattenY:F2}");
            }

            // UV 解析結果から BumpScale を最適化
            if (meshResult.uvResult.valid && material.HasProperty("_BumpScale"))
            {
                float recommendedBumpScale = meshResult.uvResult.recommendedBumpScale;
                material.SetFloat("_BumpScale", recommendedBumpScale);
                result.actions.Add($"メッシュ解析: BumpScale = {recommendedBumpScale:F2} (テクセル密度ベース)");
            }

            // スムースノーマルが必要な場合、Renderer があれば自動ベイクを試行
            if (meshResult.needsSmoothNormals)
            {
                Renderer renderer = FindRendererForMaterial(material);
                if (renderer != null)
                {
                    try
                    {
                        Mesh bakedMesh = SmoothNormalBaker.BakeSmoothNormals(mesh, useTangentSpace: true);
                        string sourcePath = AssetDatabase.GetAssetPath(mesh);
                        string directory = !string.IsNullOrEmpty(sourcePath)
                            ? System.IO.Path.GetDirectoryName(sourcePath)
                            : "Assets";
                        string savePath = directory + "/" + mesh.name + "_SmoothNormal.asset";
                        savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

                        AssetDatabase.CreateAsset(bakedMesh, savePath);
                        AssetDatabase.SaveAssets();

                        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savePath);
                        if (savedMesh != null)
                        {
                            if (renderer is SkinnedMeshRenderer smr)
                            {
                                Undo.RecordObject(smr, "Auto Bake Smooth Normals");
                                smr.sharedMesh = savedMesh;
                                EditorUtility.SetDirty(smr);
                            }
                            else if (renderer is MeshRenderer)
                            {
                                var mf = renderer.GetComponent<MeshFilter>();
                                if (mf != null)
                                {
                                    Undo.RecordObject(mf, "Auto Bake Smooth Normals");
                                    mf.sharedMesh = savedMesh;
                                    EditorUtility.SetDirty(mf);
                                }
                            }
                            result.actions.Add($"メッシュ解析: スムースノーマルを自動ベイクしました → {savePath}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NataneLilToonAutoFixer] スムースノーマル自動ベイク失敗: {e.Message}");
                        result.warnings.Add("メッシュ解析: スムースノーマルのベイクが推奨されます（アウトラインの品質向上）");
                    }
                }
                else
                {
                    result.warnings.Add("メッシュ解析: スムースノーマルのベイクが推奨されます（アウトラインの品質向上）");
                }
            }
        }

        // ===== [8] NataneFeaturePresetTable 統合 =====

        /// <summary>
        /// lilToon 側でデフォルト値だった機能に対して、
        /// FeaturePresetTable からロール × ルックに最適な値を適用する。
        /// </summary>
        private static void ApplyFeaturePresetDefaults(
            Material material,
            Dictionary<string, object> lilProps,
            AutoFixResult result,
            ref int unconfiguredFeatures)
        {
            // 対象キーワード一覧（FeaturePresetTable に登録されているもの）
            string[] featureKeywords =
            {
                "_SSS", "_SPECULAR", "_HAIR_SPECULAR", "_RIM_LIGHT",
                "_OUTLINE", "_MAT_CAP", "_EMISSION", "_REFLECTION",
                "_PARALLAX", "_ENV_RIM",
            };

            AutoSetupLook look = AutoSetupLook.Anime; // lilToon からの変換はアニメ調が妥当

            foreach (string keyword in featureKeywords)
            {
                // この機能が有効かチェック
                // キーワードに対応するトグルプロパティを探す
                string toggleProp = FindTogglePropertyForKeyword(keyword);
                if (string.IsNullOrEmpty(toggleProp))
                    continue;
                if (!material.HasProperty(toggleProp))
                    continue;
                if (material.GetFloat(toggleProp) < 0.5f)
                    continue; // 機能が OFF なのでスキップ

                // lilToon 側で値が明示的に設定されていたかチェック
                // → 設定されていなかった (デフォルト) 場合のみプリセット値を適用
                var preset = NataneFeaturePresetTable.Get(result.detectedRole, look, keyword);
                if (preset.notRecommended)
                {
                    result.warnings.Add($"FeaturePreset: {keyword} はロール {result.detectedRole} に非推奨 ({preset.notRecommendedReason})");
                    unconfiguredFeatures++;
                    continue;
                }

                // float プロパティの適用
                if (preset.floatProperties != null)
                {
                    bool anyApplied = false;
                    foreach (var (propName, value) in preset.floatProperties)
                    {
                        if (!material.HasProperty(propName))
                            continue;

                        // lilToon 側で明示的に設定されていた場合はスキップ
                        if (WasExplicitlySetInLilToon(propName, lilProps))
                            continue;

                        material.SetFloat(propName, value);
                        anyApplied = true;
                    }

                    if (anyApplied)
                        result.actions.Add($"FeaturePreset: {keyword} にロール {result.detectedRole} の最適値を適用");
                }

                // color プロパティの適用
                if (preset.colorProperties != null)
                {
                    foreach (var (propName, color) in preset.colorProperties)
                    {
                        if (!material.HasProperty(propName))
                            continue;
                        if (WasExplicitlySetInLilToon(propName, lilProps))
                            continue;

                        material.SetColor(propName, color);
                    }
                }
            }
        }

        // ===== ヘルパーメソッド =====

        /// <summary>
        /// lilToon の float プロパティ値を取得する。
        /// マテリアル上の値を優先し、なければ lilToonSourceProperties から取得する。
        /// </summary>
        private static float GetLilToonFloat(
            Material material,
            Dictionary<string, object> lilProps,
            string propertyName,
            float defaultValue)
        {
            // まず lilToonSourceProperties から取得
            if (lilProps.TryGetValue(propertyName, out object val))
            {
                if (val is float f) return f;
                if (val is double d) return (float)d;
                if (val is int i) return i;
            }

            // マテリアル上にプロパティがあればそこから取得
            if (material.HasProperty(propertyName))
                return material.GetFloat(propertyName);

            return defaultValue;
        }

        /// <summary>
        /// マテリアルの主要な色がデフォルト (白・灰色) に近いかどうかを判定する。
        /// デフォルトに近い場合、テクスチャ解析による色の自動設定が有益。
        /// </summary>
        private static bool AreColorsDefault(Material material)
        {
            // ShadowColor がデフォルト (灰色〜白) かどうかをチェック
            if (material.HasProperty("_ShadowColor"))
            {
                Color shadow = material.GetColor("_ShadowColor");
                // 灰色 (0.7-1.0 の範囲) かどうか
                if (shadow.grayscale > 0.65f && Mathf.Abs(shadow.r - shadow.g) < 0.1f && Mathf.Abs(shadow.g - shadow.b) < 0.1f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// キーワード文字列から対応するトグルプロパティ名を見つける。
        /// NataneShaderKeywordSynchronizer のマッピングテーブルを参照する。
        /// </summary>
        private static string FindTogglePropertyForKeyword(string keyword)
        {
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                if (mapping.keyword == keyword)
                    return mapping.propertyName;
            }
            return null;
        }

        /// <summary>
        /// lilToon 側でプロパティが明示的に設定されていたかどうかを判定する。
        /// lilToonSourceProperties に含まれていれば「設定済み」とみなす。
        /// </summary>
        private static bool WasExplicitlySetInLilToon(string propName, Dictionary<string, object> lilProps)
        {
            return lilProps.ContainsKey(propName);
        }

        /// <summary>
        /// シーン内でこのマテリアルを使用している Renderer を検索する。
        /// </summary>
        private static Renderer FindRendererForMaterial(Material mat)
        {
            if (mat == null) return null;
            var renderers = NataneEditorCompat.FindObjectsOfTypeCompat<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials == null) continue;
                foreach (var sharedMat in renderer.sharedMaterials)
                {
                    if (sharedMat == mat)
                        return renderer;
                }
            }
            return null;
        }
    }
}
