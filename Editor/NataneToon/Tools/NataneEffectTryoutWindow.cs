using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// 「効果を試す」ウィンドウ。
    ///
    /// どのタブにあるかを知らなくても、一覧から効果を 1 クリックで試し、気に入らなければ
    /// 1 クリックで元へ戻せる。試すときは「見て分かる値」まで設定する
    /// （トグルを入れただけでは既定値のままで変化が見えない効果が多いため）。
    ///
    /// 見える値はショーケース生成と同じ <see cref="NataneShowcaseFeatureSetups"/> を使う。
    /// 一覧の名前・説明・場所はインスペクターと同じ <see cref="NataneToonInspectorSectionRegistry"/>
    /// から取るので、表記が食い違わない。
    /// </summary>
    public sealed class NataneEffectTryoutWindow : EditorWindow
    {
        public const string MenuPath = NataneToolMenuPaths.EffectTryout;
        public const string BridgeKey = "EffectTryout";
        private const string GeneratedFolder = "Assets/NataneToonGenerated/TryEffect";

        private Material _target;
        // お試し前の状態。null でなければ「お試し中」。
        private Material _snapshot;
        private string _trialKey;
        private string _search = string.Empty;
        private int _difficultyFilter; // 0 = すべて
        private Vector2 _scroll;
        private GUIStyle _wrapMini;
        private GUIStyle _wrapBold;

        [MenuItem(MenuPath, false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneEffectTryoutWindow>(L("効果を試す", "Try Effects"));
            window.minSize = new Vector2(280f, 320f);

            Material material;
            int unused;
            if (NataneToolBridge.TryConsume(BridgeKey, out material, out unused) && material != null)
            {
                window.SetTarget(material);
            }
            else if (window._target == null)
            {
                window.SetTarget(FindMaterialInSelection());
            }

            window.Show();
        }

        [MenuItem("Assets/Natane/効果を試す Try Effects", false, 102)]
        private static void ShowFromAssets()
        {
            NataneToolBridge.OpenConsolidatedWindow(MenuPath, 0, Selection.activeObject as Material, BridgeKey);
        }

        [MenuItem("Assets/Natane/効果を試す Try Effects", true)]
        private static bool ShowFromAssetsValidate()
        {
            return IsSupported(Selection.activeObject as Material);
        }

        private static bool IsSupported(Material material)
        {
            return material != null && material.shader != null &&
                   NataneShaderCatalog.IsNataneShader(material.shader.name);
        }

        private static Material FindMaterialInSelection()
        {
            Material direct = Selection.activeObject as Material;
            if (IsSupported(direct)) return direct;

            GameObject go = Selection.activeGameObject;
            if (go == null) return null;
            Renderer renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return null;
            return renderer.sharedMaterials.FirstOrDefault(IsSupported);
        }

        private void SetTarget(Material material)
        {
            if (material == _target) return;
            // 対象を替えるときは、試している途中の変更をそのまま採用する（黙って巻き戻さない）。
            DiscardSnapshot();
            _target = material;
            Repaint();
        }

        private void OnDisable()
        {
            DiscardSnapshot();
        }

        private void DiscardSnapshot()
        {
            if (_snapshot != null) DestroyImmediate(_snapshot);
            _snapshot = null;
            _trialKey = null;
        }

        // ----------------------------------------------------------------
        // 試す / 戻す / 採用
        // ----------------------------------------------------------------

        private void Try(NataneInspectorSectionDescriptor feature)
        {
            if (_target == null) return;

            // 効果は 1 つずつ見比べる。前のお試しが残っていれば先に戻す。
            if (_snapshot != null) Revert();

            _snapshot = new Material(_target) { hideFlags = HideFlags.HideAndDontSave };
            _trialKey = feature.Key;

            Undo.RecordObject(_target, L("効果を試す: ", "Try effect: ") + feature.Label);
            if (_target.HasProperty(feature.ToggleProperty)) _target.SetFloat(feature.ToggleProperty, 1f);
            _target.EnableKeyword(feature.ToggleKeyword);

            EnsureGeneratedFolder();
            using (NataneShowcaseAssets.UseFolder(GeneratedFolder))
            {
                NataneShowcaseFeatureSetups.TryApply(feature.ToggleKeyword, _target);
            }

            NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(_target);
            EditorUtility.SetDirty(_target);
            SceneView.RepaintAll();
        }

        private void Revert()
        {
            if (_target != null && _snapshot != null)
            {
                Undo.RecordObject(_target, L("お試しを元に戻す", "Revert trial"));
                _target.CopyPropertiesFromMaterial(_snapshot);
                _target.shaderKeywords = _snapshot.shaderKeywords;
                _target.renderQueue = _snapshot.renderQueue;
                EditorUtility.SetDirty(_target);
                SceneView.RepaintAll();
            }
            DiscardSnapshot();
        }

        private void Keep()
        {
            DiscardSnapshot();
        }

        private static void EnsureGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder)) return;
            string[] parts = GeneratedFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>アバターが手元に無くても試せるよう、サンプルの球を置いて対象にする。</summary>
        private void CreateSampleSphere()
        {
            Shader shader = Shader.Find("Natane/Toon Shader");
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    L("効果を試す", "Try Effects"),
                    L("Natane Toon Shader が見つかりません。パッケージが正しく読み込まれているか確認してください。",
                      "Natane Toon Shader was not found. Check that the package is imported correctly."),
                    "OK");
                return;
            }

            EnsureGeneratedFolder();
            var material = new Material(shader);
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.95f, 0.72f, 0.62f, 1f));
            string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder + "/TrySphere.mat");
            AssetDatabase.CreateAsset(material, path);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Natane Try Sphere";
            Undo.RegisterCreatedObjectUndo(sphere, L("サンプルの球を置く", "Create sample sphere"));
            sphere.GetComponent<Renderer>().sharedMaterial = material;

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                sphere.transform.position = view.pivot;
                Selection.activeGameObject = sphere;
                view.FrameSelected();
            }
            else
            {
                Selection.activeGameObject = sphere;
            }

            SetTarget(material);
        }

        // ----------------------------------------------------------------
        // 描画
        // ----------------------------------------------------------------

        private void EnsureStyles()
        {
            if (_wrapMini != null) return;
            _wrapMini = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            _wrapBold = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
        }

        private void OnSelectionChange()
        {
            // 何も対象が無いときだけ選択に追従する。試している最中に対象が勝手に替わらないようにする。
            if (_target == null)
            {
                SetTarget(FindMaterialInSelection());
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // このウィンドウのラベルは短いので、幅を詰めても入力欄が潰れないよう固定の小さめにする。
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.3f, 70f, 130f);
            try
            {
                DrawContents();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        private void DrawContents()
        {
            bool narrow = position.width < 380f;

            NataneToonShaderGUIUtility.DrawToolHeader("効果を試す", "Try Effects", nameof(NataneEffectTryoutWindow));
            GUILayout.Label(
                L("一覧の「試す」を押すと、見て分かる設定でその効果がオンになります。気に入らなければ「元に戻す」で試す前の状態に戻ります。",
                  "Press Try to switch an effect on with settings you can actually see. If you do not like it, Revert restores the state from before the trial."),
                _wrapMini);
            EditorGUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            var picked = (Material)EditorGUILayout.ObjectField(L("対象", "Target"), _target, typeof(Material), false);
            if (EditorGUI.EndChangeCheck()) SetTarget(picked);

            if (_target == null || !IsSupported(_target))
            {
                EditorGUILayout.HelpBox(
                    _target == null
                        ? L("Natane Toon のマテリアルを選ぶか、下のボタンでサンプルの球を置いてください。",
                            "Pick a Natane Toon material, or place a sample sphere with the button below.")
                        : L("このマテリアルは Natane Toon Shader ではありません。lilToon のマテリアルなら、右クリック >「Natane > lilToon から変換」で移行できます。",
                            "This material does not use Natane Toon Shader. A lilToon material can be migrated from right-click > Natane > Convert from lilToon."),
                    MessageType.Info);

                if (GUILayout.Button(L("選択中のオブジェクトから取得", "Use Current Selection")))
                {
                    SetTarget(FindMaterialInSelection());
                }
                if (GUILayout.Button(L("サンプルの球を置いて試す", "Place a Sample Sphere"), GUILayout.Height(26f)))
                {
                    CreateSampleSphere();
                }
                return;
            }

            DrawTrialBar(narrow);
            DrawFilterRow(narrow);

            List<NataneInspectorSectionDescriptor> features = GetFeatures();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (features.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("条件に合う効果がありません。検索語や絞り込みを変えてみてください。",
                      "No effects match. Try a different search or filter."),
                    MessageType.Info);
            }
            foreach (NataneInspectorSectionDescriptor feature in features)
            {
                DrawFeatureCard(feature, narrow);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTrialBar(bool narrow)
        {
            if (_snapshot == null) return;

            NataneInspectorSectionDescriptor trial = NataneToonInspectorSectionRegistry.Find(_trialKey);
            string name = trial != null ? trial.Label : _trialKey;

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.75f, 1f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = previous;

            GUILayout.Label(L("お試し中: ", "Trying: ") + name, _wrapBold);
            if (trial != null)
            {
                GUILayout.Label(
                    L("細かい調整はマテリアルの「", "Fine-tune it in the material's \"") + TabLabel(trial.Tab) +
                    L("」タブ >「", "\" tab > \"") + trial.Label + L("」でできます。", "\"."),
                    _wrapMini);
            }

            // 幅が無いときはボタンを縦に積む。横に並べると右側が切れて押せなくなる。
            if (narrow) EditorGUILayout.BeginVertical(); else EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("このまま使う", "Keep It"), GUILayout.Height(24f))) Keep();
            if (GUILayout.Button(L("元に戻す", "Revert"), GUILayout.Height(24f))) Revert();
            if (narrow) EditorGUILayout.EndVertical(); else EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        private void DrawFilterRow(bool narrow)
        {
            string[] difficultyNames =
            {
                L("すべて", "All"),
                L("かんたん", "Basic"),
                L("標準", "Intermediate"),
                L("上級", "Advanced")
            };

            if (narrow) EditorGUILayout.BeginVertical(); else EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(L("検索", "Search"), _search ?? string.Empty);
            _difficultyFilter = narrow
                ? EditorGUILayout.Popup(L("難しさ", "Level"), _difficultyFilter, difficultyNames)
                : EditorGUILayout.Popup(_difficultyFilter, difficultyNames, GUILayout.Width(110f));
            if (narrow) EditorGUILayout.EndVertical(); else EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
        }

        // 一覧の絞り込みと並べ替えは OnGUI のたび（Layout / Repaint / マウス移動）にやり直す必要がない。
        // 対象・シェーダー・検索語・絞り込み・表示言語が変わったときだけ作り直す。
        private List<NataneInspectorSectionDescriptor> _featureCache;
        private Material _featureCacheTarget;
        private Shader _featureCacheShader;
        private string _featureCacheSearch;
        private int _featureCacheDifficulty = -1;
        private bool _featureCacheJapanese;

        private List<NataneInspectorSectionDescriptor> GetFeatures()
        {
            Shader shader = _target != null ? _target.shader : null;
            bool japanese = NataneToonLocalization.IsJapanese;
            if (_featureCache == null || _featureCacheTarget != _target || _featureCacheShader != shader ||
                _featureCacheDifficulty != _difficultyFilter || _featureCacheJapanese != japanese ||
                !string.Equals(_featureCacheSearch, _search, System.StringComparison.Ordinal))
            {
                _featureCache = CollectFeatures();
                _featureCacheTarget = _target;
                _featureCacheShader = shader;
                _featureCacheSearch = _search;
                _featureCacheDifficulty = _difficultyFilter;
                _featureCacheJapanese = japanese;
            }
            return _featureCache;
        }

        private List<NataneInspectorSectionDescriptor> CollectFeatures()
        {
            IEnumerable<NataneInspectorSectionDescriptor> source = NataneToonInspectorSectionRegistry.All
                .Where(f => !string.IsNullOrEmpty(f.ToggleKeyword) && !string.IsNullOrEmpty(f.ToggleProperty))
                // バリアントが持っていない効果（Lite に無い機能など）は出さない。押しても何も起きないため。
                .Where(f => _target.HasProperty(f.ToggleProperty));

            if (_difficultyFilter > 0)
            {
                var wanted = (NataneInspectorDifficulty)(_difficultyFilter - 1);
                source = source.Where(f => f.Difficulty == wanted);
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                source = source.Where(f => f.GetSearchScore(_search) > 0 ||
                                           f.Description.ToLowerInvariant().Contains(_search.Trim().ToLowerInvariant()));
            }

            // 見て分かる設定を持つ効果を先に、その中ではやさしい順に並べる。
            return source
                .OrderByDescending(f => NataneShowcaseFeatureSetups.Has(f.ToggleKeyword))
                .ThenBy(f => (int)f.Difficulty)
                .ThenBy(f => (int)f.Tab)
                .ThenBy(f => f.Order)
                .ToList();
        }

        private void DrawFeatureCard(NataneInspectorSectionDescriptor feature, bool narrow)
        {
            bool isOn = _target.HasProperty(feature.ToggleProperty) && _target.GetFloat(feature.ToggleProperty) > 0.5f;
            bool isTrial = _snapshot != null && feature.Key == _trialKey;
            bool hasDemoValues = NataneShowcaseFeatureSetups.Has(feature.ToggleKeyword);
            var evaluation = NataneToonSamplerBudgetEstimator.EvaluateEnable(_target, feature.ToggleKeyword);
            bool blocked = !isOn && !evaluation.CanEnable;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string state = isTrial ? L("  ● お試し中", "  ● Trying")
                         : isOn ? L("  ● オン", "  ● On")
                         : string.Empty;
            GUILayout.Label(feature.Label + state, _wrapBold);

            if (!string.IsNullOrEmpty(feature.Description))
            {
                GUILayout.Label(feature.Description, _wrapMini);
            }

            string note = L("場所: ", "Where: ") + TabLabel(feature.Tab) + L(" タブ", " tab");
            if (!hasDemoValues) note += L("  ／  オンにするだけ（見え方は設定次第）", "  /  Switches on only (look depends on settings)");
            if (blocked) note += L("  ／  テクスチャ枠が足りません。他の効果をオフにすると試せます", "  /  Not enough texture slots. Switch another effect off first");
            GUILayout.Label(note, _wrapMini);

            if (narrow) EditorGUILayout.BeginVertical(); else EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(blocked || isTrial))
            {
                string tryLabel = isOn && !isTrial
                    ? L("見やすい設定で試す", "Try With Demo Settings")
                    : L("試す", "Try");
                if (GUILayout.Button(tryLabel, GUILayout.Height(22f))) Try(feature);
            }
            if (isTrial)
            {
                if (GUILayout.Button(L("元に戻す", "Revert"), GUILayout.Height(22f))) Revert();
            }
            if (narrow) EditorGUILayout.EndVertical(); else EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static string TabLabel(NataneInspectorTab tab)
        {
            switch ((int)tab)
            {
                case 0: return L("セットアップ", "Setup");
                case 1: return L("ライティング", "Lighting");
                case 2: return L("サーフェス", "Surface");
                case 3: return L("エフェクト", "Effects");
                default: return L("VRChat・出力", "VRChat & Output");
            }
        }
    }
}
