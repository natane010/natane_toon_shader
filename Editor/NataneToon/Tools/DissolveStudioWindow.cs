using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Dissolve Studio — ディゾルブ表現と、その<b>アニメーション</b>を組み立てる。
    ///
    /// シェーダー側の <c>_DISSOLVE</c> は 18 プロパティを持ち、UV/World/Local の座標モードや
    /// エッジ発光まで揃っている。素材生成も <see cref="DissolvePatternGenerator"/> がある。
    /// 足りていなかったのは「時間変化を作る」部分で、そこがまるごと空白だった：
    /// <c>AnimationClip</c> を生成するツールがパッケージ内に 1 つも無く、
    /// Animator レイヤーや VRChat のメニュー生成も無かった。
    /// 結果として「ディゾルブで消えるアニメーション」の実作業がほぼ全部手作業になっていた。
    ///
    /// 素材生成は <see cref="DissolvePatternGenerator"/> に任せ、こちらは
    /// <b>表現とアニメーションの組み立て</b>に専念する。
    /// </summary>
    public class DissolveStudioWindow : EditorWindow
    {
        private enum Tab
        {
            Preset,
            Preview,
            Animation,
            Avatar,
            SelfRunning
        }

        private Tab _tab = Tab.Preset;
        private Material _material;
        private Vector2 _scroll;

        // ---- プレビュー ----
        private bool _previewActive;
        private float _previewValue;
        private float _previewOriginal;
        private bool _previewPlaying;
        private double _previewStartTime;
        private float _previewDuration = 1.5f;

        // ---- アニメーション ----
        private enum Direction { Vanish, Appear, PingPong }
        private enum Easing { Linear, EaseIn, EaseOut, EaseInOut, Step }

        private readonly List<Renderer> _targets = new List<Renderer>();
        private GameObject _collectRoot;
        private Direction _direction = Direction.Vanish;
        private Easing _easing = Easing.EaseInOut;
        private float _clipLength = 1f;
        private float _holdSeconds;
        private int _stepFps = 8;
        private bool _animateEdgeIntensity = true;
        private bool _toggleFeatureInClip = true;
        private string _outputFolder = "Assets/NataneToonGenerated/Dissolve";
        private string _clipName = "Dissolve";

        // ---- アバター ----
        private GameObject _avatarRoot;
        private bool _useFloatParameter;
        private string _parameterName = "NataneDissolve";
        private string _menuLabel = "Dissolve";
        private AnimationClip _lastVanishClip;
        private AnimationClip _lastAppearClip;

        [MenuItem(NataneToolMenuPaths.DissolveStudio, false, 47)]
        public static void ShowWindow()
        {
            var window = GetWindow<DissolveStudioWindow>(L("ディゾルブスタジオ", "Dissolve Studio"));
            window.minSize = new Vector2(560, 660);
            window.Show();
        }

        public static void ShowWindow(Material material)
        {
            var window = GetWindow<DissolveStudioWindow>(L("ディゾルブスタジオ", "Dissolve Studio"));
            window._material = material;
            window.minSize = new Vector2(560, 660);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            // ウィンドウを閉じたらプレビューを必ず元へ戻す。
            // 戻さないとマテリアルが「半分溶けたまま」保存されてしまう。
            StopPreview(restore: true);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("ディゾルブスタジオ", "Dissolve Studio"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("ルックを決め、消える／現れるアニメーションを生成します。",
                  "Pick a look, then generate the vanish / appear animation."),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUI.BeginChangeCheck();
            _material = (Material)EditorGUILayout.ObjectField(
                L("マテリアル", "Material"), _material, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                // マテリアルを差し替えたら、前のマテリアルのプレビューを戻してから切り替える。
                StopPreview(restore: true);
            }

            if (_material != null && !_material.HasProperty("_DissolveAmount"))
            {
                EditorGUILayout.HelpBox(
                    L("このマテリアルは _DissolveAmount を持っていません。Natane Toon Shader を割り当ててください。",
                      "This material has no _DissolveAmount. Assign a Natane Toon Shader."),
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[]
            {
                L("プリセット", "Preset"),
                L("プレビュー", "Preview"),
                L("アニメーション", "Animation"),
                L("VRChat", "VRChat"),
                L("自走モード", "Self-running")
            });

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case Tab.Preset: DrawPresetTab(); break;
                case Tab.Preview: DrawPreviewTab(); break;
                case Tab.Animation: DrawAnimationTab(); break;
                case Tab.Avatar: DrawAvatarTab(); break;
                case Tab.SelfRunning: DrawSelfRunningTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ================= プリセット =================

        private sealed class DissolvePreset
        {
            public string LabelJp;
            public string LabelEn;
            public string DescriptionJp;
            public string DescriptionEn;
            public Action<Material> Apply;
        }

        private static readonly DissolvePreset[] Presets =
        {
            new DissolvePreset
            {
                LabelJp = "焼失", LabelEn = "Burn",
                DescriptionJp = "紙や布が焼けて消える。橙のエッジが強く出る。",
                DescriptionEn = "Paper or cloth burning away, with a strong orange edge.",
                Apply = m =>
                {
                    SetFloat(m, "_DissolveCoordMode", 0f);          // UV
                    SetFloat(m, "_DissolveEdgeWidth", 0.08f);
                    SetFloat(m, "_DissolveEdgeIntensity", 4f);
                    SetFloat(m, "_DissolveBlur", 0f);
                    SetColor(m, "_DissolveEdgeColor", new Color(3.0f, 1.1f, 0.25f, 1f));
                }
            },
            new DissolvePreset
            {
                LabelJp = "転送", LabelEn = "Teleport",
                DescriptionJp = "足元から上へ、光の帯で消える。World Y 座標を使う。",
                DescriptionEn = "A band of light sweeping upward from the feet, driven by world Y.",
                Apply = m =>
                {
                    SetFloat(m, "_DissolveCoordMode", 1f);          // World
                    SetFloat(m, "_DissolveWorldAxis", 1f);          // Y
                    SetFloat(m, "_DissolveNoiseBlend", 0.2f);
                    SetFloat(m, "_DissolveEdgeWidth", 0.15f);
                    SetFloat(m, "_DissolveEdgeIntensity", 6f);
                    SetColor(m, "_DissolveEdgeColor", new Color(0.3f, 2.4f, 3.0f, 1f));
                }
            },
            new DissolvePreset
            {
                LabelJp = "データ化", LabelEn = "Digital",
                DescriptionJp = "四角いブロックで分解。エッジは細く鋭い白。",
                DescriptionEn = "Breaking into square blocks with a thin, sharp white edge.",
                Apply = m =>
                {
                    SetFloat(m, "_DissolveCoordMode", 0f);
                    SetFloat(m, "_DissolveEdgeWidth", 0.02f);
                    SetFloat(m, "_DissolveEdgeIntensity", 8f);
                    SetFloat(m, "_DissolveBlur", 0f);
                    SetColor(m, "_DissolveEdgeColor", new Color(3f, 3f, 3f, 1f));
                }
            },
            new DissolvePreset
            {
                LabelJp = "風化", LabelEn = "Erode",
                DescriptionJp = "砂のように崩れる。エッジは広く鈍い灰色。",
                DescriptionEn = "Crumbling like sand, with a wide, dull grey edge.",
                Apply = m =>
                {
                    SetFloat(m, "_DissolveCoordMode", 2f);          // Local
                    SetFloat(m, "_DissolveWorldAxis", 1f);
                    SetFloat(m, "_DissolveNoiseBlend", 0.6f);
                    SetFloat(m, "_DissolveEdgeWidth", 0.2f);
                    SetFloat(m, "_DissolveEdgeIntensity", 1f);
                    SetColor(m, "_DissolveEdgeColor", new Color(0.6f, 0.55f, 0.5f, 1f));
                }
            },
            new DissolvePreset
            {
                LabelJp = "霧散", LabelEn = "Mist",
                DescriptionJp = "ふわっと薄れて消える。境界をぼかす。",
                DescriptionEn = "Fading away softly, with a blurred boundary.",
                Apply = m =>
                {
                    SetFloat(m, "_DissolveCoordMode", 0f);
                    SetFloat(m, "_DissolveEdgeWidth", 0.3f);
                    SetFloat(m, "_DissolveEdgeIntensity", 2f);
                    SetFloat(m, "_DissolveBlur", 0.6f);
                    SetColor(m, "_DissolveEdgeColor", new Color(1.2f, 1.3f, 1.5f, 1f));
                }
            }
        };

        private void DrawPresetTab()
        {
            if (_material == null)
            {
                EditorGUILayout.HelpBox(L("マテリアルを指定してください。", "Assign a material."), MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                L("プリセットを選ぶとルックが決まります。ノイズテクスチャが未設定なら自動生成します。",
                  "Picking a preset sets the look. A noise texture is generated automatically if none is assigned."),
                MessageType.Info);

            foreach (DissolvePreset preset in Presets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(L(preset.LabelJp, preset.LabelEn), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    L(preset.DescriptionJp, preset.DescriptionEn), EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button(L("適用", "Apply"), GUILayout.Height(22)))
                {
                    ApplyPreset(preset);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("出現方向", "Emerge"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                L("「出現」は独立したプリセットではなく、アニメーションの方向で決まります。" +
                  "アニメーションタブで「現れる (1→0)」を選んでください。",
                  "\"Emerge\" is not a separate preset — it is the animation direction. " +
                  "Choose \"Appear (1 to 0)\" on the Animation tab."),
                MessageType.None);
        }

        private void ApplyPreset(DissolvePreset preset)
        {
            Undo.RecordObject(_material, "Apply Dissolve Preset");

            _material.SetFloat("_Dissolve", 1f);
            _material.EnableKeyword("_DISSOLVE");
            SetFloat(_material, "_DissolveBlend", 1f);
            preset.Apply(_material);

            EditorUtility.SetDirty(_material);

            if (_material.HasProperty("_DissolveTex") && _material.GetTexture("_DissolveTex") == null)
            {
                EditorUtility.DisplayDialog(
                    L("ディゾルブスタジオ", "Dissolve Studio"),
                    L("プリセットを適用しました。ディゾルブテクスチャが未設定なので、" +
                      "ディゾルブパターン生成でノイズを作って割り当ててください。",
                      "Preset applied. No dissolve texture is assigned — generate one with the " +
                      "Dissolve Pattern Generator."),
                    "OK");
            }
        }

        // ================= プレビュー =================

        private void DrawPreviewTab()
        {
            if (_material == null)
            {
                EditorGUILayout.HelpBox(L("マテリアルを指定してください。", "Assign a material."), MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                L("0→1 をスクラブしてシーンビューで確認します。ウィンドウを閉じるか「戻す」で元の値へ復帰します。",
                  "Scrub 0 to 1 and watch the scene view. Closing the window or pressing Restore returns the original value."),
                MessageType.Info);

            if (!_previewActive)
            {
                if (GUILayout.Button(L("プレビューを開始", "Start Preview"), GUILayout.Height(26)))
                {
                    StartPreview();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            _previewValue = EditorGUILayout.Slider(L("ディゾルブ量", "Dissolve Amount"), _previewValue, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreviewValue(_previewValue);
            }

            EditorGUILayout.Space(4);
            _previewDuration = EditorGUILayout.Slider(L("再生時間 (秒)", "Play Duration (s)"), _previewDuration, 0.2f, 6f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_previewPlaying ? L("停止", "Stop") : L("再生", "Play"), GUILayout.Height(24)))
            {
                _previewPlaying = !_previewPlaying;
                _previewStartTime = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button(L("戻す", "Restore"), GUILayout.Height(24)))
            {
                StopPreview(restore: true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                L($"開始時の値: {_previewOriginal:0.###}", $"Original value: {_previewOriginal:0.###}"),
                EditorStyles.miniLabel);
        }

        private void StartPreview()
        {
            if (_material == null) return;

            _previewOriginal = _material.GetFloat("_DissolveAmount");
            _previewValue = _previewOriginal;
            _previewActive = true;
            _previewPlaying = false;

            // プレビューの開始も Undo に積む。ここを積まないと、途中で Unity が
            // クラッシュした場合に元の値を復元する手段が無くなる。
            Undo.RecordObject(_material, "Dissolve Preview");
        }

        private void ApplyPreviewValue(float value)
        {
            if (_material == null) return;
            _material.SetFloat("_DissolveAmount", value);
            EditorUtility.SetDirty(_material);
            SceneView.RepaintAll();
        }

        private void StopPreview(bool restore)
        {
            if (!_previewActive) return;

            _previewPlaying = false;
            _previewActive = false;

            if (restore && _material != null)
            {
                _material.SetFloat("_DissolveAmount", _previewOriginal);
                EditorUtility.SetDirty(_material);
                SceneView.RepaintAll();
            }
        }

        private void OnEditorUpdate()
        {
            if (!_previewActive || !_previewPlaying || _material == null) return;

            float elapsed = (float)(EditorApplication.timeSinceStartup - _previewStartTime);
            float t = Mathf.Repeat(elapsed / Mathf.Max(_previewDuration, 0.01f), 1f);

            _previewValue = t;
            ApplyPreviewValue(t);
            Repaint();
        }

        // ================= アニメーション =================

        private void DrawAnimationTab()
        {
            EditorGUILayout.LabelField(L("対象", "Targets"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _collectRoot = (GameObject)EditorGUILayout.ObjectField(
                L("収集ルート", "Collect Root"), _collectRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _collectRoot != null)
            {
                CollectTargets();
            }

            EditorGUILayout.LabelField(
                L($"対象 Renderer: {_targets.Count} 個", $"Target renderers: {_targets.Count}"),
                EditorStyles.miniLabel);

            if (_collectRoot != null && GUILayout.Button(L("配下の Renderer を再収集", "Re-collect renderers")))
            {
                CollectTargets();
            }

            if (_targets.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (Renderer r in _targets.Take(10))
                {
                    int slots = r.sharedMaterials.Length;
                    EditorGUILayout.LabelField(
                        $"  {r.name}",
                        slots > 1 ? $"material[N] × {slots}" : "material");
                }
                if (_targets.Count > 10)
                {
                    EditorGUILayout.LabelField($"  … 他 {_targets.Count - 10} 個", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("アニメーション", "Animation"), EditorStyles.boldLabel);
            _direction = (Direction)EditorGUILayout.EnumPopup(L("方向", "Direction"), _direction);
            _clipLength = EditorGUILayout.Slider(L("長さ (秒)", "Length (s)"), _clipLength, 0.1f, 10f);
            _holdSeconds = EditorGUILayout.Slider(L("保持 (秒)", "Hold (s)"), _holdSeconds, 0f, 5f);
            _easing = (Easing)EditorGUILayout.EnumPopup(L("イージング", "Easing"), _easing);

            if (_easing == Easing.Step)
            {
                EditorGUI.indentLevel++;
                _stepFps = EditorGUILayout.IntSlider(L("コマ打ち fps", "Step fps"), _stepFps, 2, 24);
                EditorGUILayout.HelpBox(
                    L("接線を Constant にして、指定 fps でカクカク動かします。Line Boil と同じ「低fpsで見せる」思想です。",
                      "Tangents are set to Constant so it steps at the given fps — the same \"show it at low fps\" idea as Line Boil."),
                    MessageType.None);
                EditorGUI.indentLevel--;
            }

            _animateEdgeIntensity = EditorGUILayout.Toggle(
                L("エッジ発光も動かす", "Animate edge intensity"), _animateEdgeIntensity);
            _toggleFeatureInClip = EditorGUILayout.Toggle(
                L("クリップ内で機能をON/OFF", "Toggle feature inside clip"), _toggleFeatureInClip);

            if (_toggleFeatureInClip)
            {
                EditorGUILayout.HelpBox(
                    L("クリップ先頭で _Dissolve を 1、末尾で 0 に戻します。" +
                      "常時ONにしておくより、使っていない間の負荷を避けられます。",
                      "Sets _Dissolve to 1 at the start of the clip and back to 0 at the end, so it does not " +
                      "cost anything while unused."),
                    MessageType.None);
            }

            EditorGUILayout.Space(6);
            _outputFolder = EditorGUILayout.TextField(L("保存先", "Output Folder"), _outputFolder);
            _clipName = EditorGUILayout.TextField(L("クリップ名", "Clip Name"), _clipName);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_targets.Count == 0))
            {
                if (GUILayout.Button(L("AnimationClip を生成", "Generate AnimationClip"), GUILayout.Height(30)))
                {
                    GenerateClips();
                }
            }

            if (_targets.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("収集ルートを指定して Renderer を集めてください。",
                      "Assign a collect root to gather renderers."),
                    MessageType.Warning);
            }
        }

        private void CollectTargets()
        {
            _targets.Clear();
            if (_collectRoot == null) return;

            foreach (Renderer renderer in _collectRoot.GetComponentsInChildren<Renderer>(true))
            {
                // ディゾルブを持つマテリアルが 1 つでもあれば対象にする。
                bool hasDissolve = renderer.sharedMaterials.Any(
                    m => m != null && m.HasProperty("_DissolveAmount"));
                if (hasDissolve) _targets.Add(renderer);
            }
        }

        private void GenerateClips()
        {
            string folder = EnsureFolder(_outputFolder);
            if (folder == null)
            {
                EditorUtility.DisplayDialog(
                    L("ディゾルブスタジオ", "Dissolve Studio"),
                    L("保存先フォルダを作成できませんでした。Assets 配下のパスを指定してください。",
                      "Could not create the output folder. Use a path under Assets."),
                    "OK");
                return;
            }

            var created = new List<string>();

            if (_direction == Direction.Vanish || _direction == Direction.PingPong)
            {
                _lastVanishClip = BuildClip(0f, 1f, _clipName + "_Vanish", folder);
                if (_lastVanishClip != null) created.Add(_lastVanishClip.name);
            }

            if (_direction == Direction.Appear || _direction == Direction.PingPong)
            {
                _lastAppearClip = BuildClip(1f, 0f, _clipName + "_Appear", folder);
                if (_lastAppearClip != null) created.Add(_lastAppearClip.name);
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                L("ディゾルブスタジオ", "Dissolve Studio"),
                created.Count > 0
                    ? L($"{created.Count} 個のクリップを生成しました。\n{string.Join("\n", created)}",
                        $"Generated {created.Count} clip(s).\n{string.Join("\n", created)}")
                    : L("クリップを生成できませんでした。", "No clip could be generated."),
                "OK");
        }

        private AnimationClip BuildClip(float from, float to, string name, string folder)
        {
            if (_collectRoot == null) return null;

            var clip = new AnimationClip { name = name, frameRate = 60f };

            float total = _clipLength + _holdSeconds;
            AnimationCurve amountCurve = BuildCurve(from, to, _clipLength, _holdSeconds);

            foreach (Renderer renderer in _targets)
            {
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, _collectRoot.transform);
                int slotCount = renderer.sharedMaterials.Length;

                for (int slot = 0; slot < slotCount; slot++)
                {
                    Material slotMaterial = renderer.sharedMaterials[slot];
                    if (slotMaterial == null || !slotMaterial.HasProperty("_DissolveAmount")) continue;

                    // マテリアルスロットが複数ある Renderer は "material._X" では
                    // 先頭スロットしか動かない。スロット数を見て添字付きへ切り替える。
                    string prefix = slotCount > 1 ? $"material[{slot}]." : "material.";

                    SetCurve(clip, path, renderer.GetType(), prefix + "_DissolveAmount", amountCurve);

                    if (_animateEdgeIntensity && slotMaterial.HasProperty("_DissolveEdgeIntensity"))
                    {
                        // 山型。溶けている最中だけ縁が光り、始点と終点では落ち着く。
                        float baseIntensity = slotMaterial.GetFloat("_DissolveEdgeIntensity");
                        var edgeCurve = new AnimationCurve(
                            new Keyframe(0f, baseIntensity * 0.4f),
                            new Keyframe(_clipLength * 0.5f, baseIntensity),
                            new Keyframe(total, baseIntensity * 0.4f));
                        SetCurve(clip, path, renderer.GetType(), prefix + "_DissolveEdgeIntensity", edgeCurve);
                    }

                    if (_toggleFeatureInClip && slotMaterial.HasProperty("_Dissolve"))
                    {
                        // 先頭で 1、末尾で 0。末尾のキーは Constant にして、
                        // 途中で中途半端な値にならないようにする。
                        var toggleCurve = new AnimationCurve(
                            new Keyframe(0f, 1f),
                            new Keyframe(Mathf.Max(total - 1f / clip.frameRate, 0.0001f), 1f),
                            new Keyframe(total, 0f));
                        MakeConstant(toggleCurve);
                        SetCurve(clip, path, renderer.GetType(), prefix + "_Dissolve", toggleCurve);
                    }
                }
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.anim");
            AssetDatabase.CreateAsset(clip, assetPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static void SetCurve(AnimationClip clip, string path, Type rendererType, string property, AnimationCurve curve)
        {
            var binding = new EditorCurveBinding
            {
                path = path,
                type = rendererType,
                propertyName = property
            };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private AnimationCurve BuildCurve(float from, float to, float length, float hold)
        {
            if (_easing == Easing.Step)
            {
                // 指定 fps の刻みでキーを打ち、全接線を Constant にする。
                int frames = Mathf.Max(2, Mathf.RoundToInt(length * _stepFps));
                var keys = new Keyframe[frames + 1];
                for (int i = 0; i <= frames; i++)
                {
                    float t = i / (float)frames;
                    keys[i] = new Keyframe(t * length, Mathf.Lerp(from, to, t));
                }
                var stepCurve = new AnimationCurve(keys);
                MakeConstant(stepCurve);

                if (hold > 0f)
                {
                    stepCurve.AddKey(new Keyframe(length + hold, to));
                    MakeConstant(stepCurve);
                }
                return stepCurve;
            }

            var curve = new AnimationCurve(
                new Keyframe(0f, from),
                new Keyframe(length, to));

            switch (_easing)
            {
                case Easing.Linear:
                    // 線形は接線を傾きに揃える。既定の smooth だと端が寝てしまう。
                    float slope = (to - from) / Mathf.Max(length, 0.0001f);
                    curve.keys = new[]
                    {
                        new Keyframe(0f, from, slope, slope),
                        new Keyframe(length, to, slope, slope)
                    };
                    break;
                case Easing.EaseIn:
                    curve.keys = new[]
                    {
                        new Keyframe(0f, from, 0f, 0f),
                        new Keyframe(length, to, 2f * (to - from) / Mathf.Max(length, 0.0001f), 0f)
                    };
                    break;
                case Easing.EaseOut:
                    curve.keys = new[]
                    {
                        new Keyframe(0f, from, 0f, 2f * (to - from) / Mathf.Max(length, 0.0001f)),
                        new Keyframe(length, to, 0f, 0f)
                    };
                    break;
                case Easing.EaseInOut:
                    // 既定の smooth 接線がそのまま ease-in-out になる。
                    break;
            }

            if (hold > 0f)
            {
                curve.AddKey(new Keyframe(length + hold, to));
            }

            return curve;
        }

        private static void MakeConstant(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
        }

        // ================= VRChat =================

        private void DrawAvatarTab()
        {
            EditorGUILayout.LabelField(L("アバターへの組み込み", "Wire into the avatar"), EditorStyles.boldLabel);

            bool sdkAvailable = NataneAvatarIntegrationBridge.IsAvailable;
            bool hasModularAvatar = NataneAvatarIntegrationBridge.HasModularAvatar;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                sdkAvailable
                    ? L("VRChat SDK: 検出済み", "VRChat SDK: detected")
                    : L("VRChat SDK: 未検出", "VRChat SDK: not detected"),
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                hasModularAvatar
                    ? L("Modular Avatar: 検出済み（非破壊で組みます）", "Modular Avatar: detected (non-destructive setup)")
                    : L("Modular Avatar: 未検出（FX レイヤーへ直接書き込みます）",
                        "Modular Avatar: not detected (writes directly into the FX layer)"),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            if (!sdkAvailable)
            {
                EditorGUILayout.HelpBox(
                    L("VRChat SDK が無いため、アバターへの組み込みは行えません。" +
                      "AnimatorController の生成までは実行できます。",
                      "Without the VRChat SDK the avatar wiring cannot run. Generating the " +
                      "AnimatorController still works."),
                    MessageType.Info);
            }
            else if (!hasModularAvatar)
            {
                EditorGUILayout.HelpBox(
                    L("Modular Avatar が無いため FX レイヤーへ直接書き込みます。" +
                      "書き込み前にバックアップを取ります。",
                      "Without Modular Avatar the FX layer is written directly. A backup is taken first."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(6);
            _avatarRoot = (GameObject)EditorGUILayout.ObjectField(
                L("アバターのルート", "Avatar Root"), _avatarRoot, typeof(GameObject), true);

            _parameterName = EditorGUILayout.TextField(L("パラメータ名", "Parameter Name"), _parameterName);
            _menuLabel = EditorGUILayout.TextField(L("メニュー表示名", "Menu Label"), _menuLabel);
            _useFloatParameter = EditorGUILayout.Toggle(
                L("float（ラジアル）にする", "Use float (radial)"), _useFloatParameter);

            EditorGUILayout.HelpBox(
                _useFloatParameter
                    ? L("float は 8bit 消費します。手動でスクラブしたい場合向け。",
                        "A float costs 8 bits. Use it when you want to scrub manually.")
                    : L("bool は 1bit 消費します。消えたまま維持するトグル向け。",
                        "A bool costs 1 bit. Use it for a toggle that stays dissolved."),
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                L($"使用するクリップ: {(_lastVanishClip != null ? _lastVanishClip.name : "未生成")}",
                  $"Clip in use: {(_lastVanishClip != null ? _lastVanishClip.name : "not generated")}"),
                EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_lastVanishClip == null || _avatarRoot == null))
            {
                if (GUILayout.Button(L("AnimatorController を生成して組み込む", "Generate controller and wire"), GUILayout.Height(30)))
                {
                    WireToAvatar();
                }
            }

            if (_lastVanishClip == null)
            {
                EditorGUILayout.HelpBox(
                    L("先にアニメーションタブでクリップを生成してください。",
                      "Generate a clip on the Animation tab first."),
                    MessageType.Warning);
            }
        }

        private void WireToAvatar()
        {
            string folder = EnsureFolder(_outputFolder);
            if (folder == null) return;

            AnimatorController controller = BuildController(folder);
            if (controller == null) return;

            NataneAvatarWireResult result = NataneAvatarIntegrationBridge.WireToggle(
                _avatarRoot, controller, _parameterName, _useFloatParameter, _menuLabel);

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                L("ディゾルブスタジオ", "Dissolve Studio"),
                (result.Success
                    ? L("アバターへ組み込みました。\n", "Wired into the avatar.\n")
                    : L("AnimatorController は生成しましたが、組み込みは行われませんでした。\n",
                        "The AnimatorController was generated, but wiring did not run.\n"))
                + result.Message
                + (result.BackupPath != null
                    ? L($"\n\nバックアップ: {result.BackupPath}", $"\n\nBackup: {result.BackupPath}")
                    : string.Empty),
                "OK");
        }

        /// <summary>
        /// Off → DissolveOut → Hold → DissolveIn の遷移を持つコントローラ。
        /// Write Defaults はオフ。オンのままだと、他のレイヤーが触っていない
        /// プロパティが勝手に既定値へ戻されて事故になる。
        /// </summary>
        private AnimatorController BuildController(string folder)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{_clipName}_Controller.controller");
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            if (_useFloatParameter)
            {
                controller.AddParameter(_parameterName, AnimatorControllerParameterType.Float);
            }
            else
            {
                controller.AddParameter(_parameterName, AnimatorControllerParameterType.Bool);
            }

            AnimatorControllerLayer layer = controller.layers[0];
            layer.name = _clipName;
            layer.defaultWeight = 1f;
            AnimatorStateMachine machine = layer.stateMachine;

            AnimatorState off = machine.AddState("Off");
            off.writeDefaultValues = false;

            AnimatorState outState = machine.AddState("DissolveOut");
            outState.writeDefaultValues = false;
            outState.motion = _lastVanishClip;

            machine.defaultState = off;

            if (_useFloatParameter)
            {
                // ラジアルはクリップ内を直接スクラブする。遷移ではなく再生位置で表現する。
                outState.timeParameterActive = true;
                outState.timeParameter = _parameterName;
                outState.speed = 0f;
                machine.defaultState = outState;
                machine.RemoveState(off);
            }
            else
            {
                AnimatorStateTransition toOut = off.AddTransition(outState);
                toOut.hasExitTime = false;
                toOut.duration = 0f;
                toOut.AddCondition(AnimatorConditionMode.If, 0f, _parameterName);

                AnimatorState inState;
                if (_lastAppearClip != null)
                {
                    inState = machine.AddState("DissolveIn");
                    inState.writeDefaultValues = false;
                    inState.motion = _lastAppearClip;

                    AnimatorStateTransition toIn = outState.AddTransition(inState);
                    toIn.hasExitTime = false;
                    toIn.duration = 0f;
                    toIn.AddCondition(AnimatorConditionMode.IfNot, 0f, _parameterName);

                    AnimatorStateTransition backToOff = inState.AddTransition(off);
                    backToOff.hasExitTime = true;
                    backToOff.exitTime = 1f;
                    backToOff.duration = 0f;
                }
                else
                {
                    AnimatorStateTransition backToOff = outState.AddTransition(off);
                    backToOff.hasExitTime = false;
                    backToOff.duration = 0f;
                    backToOff.AddCondition(AnimatorConditionMode.IfNot, 0f, _parameterName);
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ================= 自走モード =================

        private void DrawSelfRunningTab()
        {
            if (_material == null)
            {
                EditorGUILayout.HelpBox(L("マテリアルを指定してください。", "Assign a material."), MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                L("FX Modulator でディゾルブ量を駆動します。Animator レイヤーもパラメータも消費しないため、" +
                  "Quest やパラメータ枠が厳しいアバターではこちらを推奨します。",
                  "Drives the dissolve amount with the FX Modulator. It consumes no animator layer and no " +
                  "expression parameter, so prefer it on Quest or when the parameter budget is tight."),
                MessageType.Info);

            DrawSelfRunningPreset(
                L("呼吸するように薄れる", "Breathing fade"),
                L("Sine で緩やかに 0↔0.3 を往復します。", "Sine, drifting between 0 and 0.3."),
                source: 0f, amount: 0.3f, speed: 0.5f, invert: false);

            DrawSelfRunningPreset(
                L("音に合わせて分解", "Dissolve to the beat"),
                L("AudioLink の低域に反応します。AudioLink が必要です。",
                  "Reacts to the AudioLink bass band. Requires AudioLink."),
                source: 5f, amount: 0.6f, speed: 1f, invert: false);

            DrawSelfRunningPreset(
                L("近づくと現れる", "Appear when close"),
                L("カメラ距離で駆動し、反転して「近いほど実体化」にします。",
                  "Driven by camera distance and inverted, so getting closer solidifies it."),
                source: 10f, amount: 1f, speed: 1f, invert: true);

            DrawSelfRunningPreset(
                L("一定間隔で明滅", "Periodic flicker"),
                L("Pulse でオン・オフを繰り返します。", "Pulse, switching on and off."),
                source: 3f, amount: 0.8f, speed: 0.2f, invert: false);
        }

        private void DrawSelfRunningPreset(string title, string description,
                                           float source, float amount, float speed, bool invert)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button(L("適用", "Apply"), GUILayout.Height(22)))
            {
                Undo.RecordObject(_material, "Apply Self-Running Dissolve");

                _material.SetFloat("_Dissolve", 1f);
                _material.EnableKeyword("_DISSOLVE");
                _material.SetFloat("_FXModulator", 1f);
                _material.EnableKeyword("_FX_MODULATOR");

                SetFloat(_material, "_FXModSource0", source);
                SetFloat(_material, "_FXModTarget0", 13f);   // DissolveAmount
                SetFloat(_material, "_FXModAmount0", amount);
                SetFloat(_material, "_FXModSpeed0", speed);
                SetFloat(_material, "_FXModInvert0", invert ? 1f : 0f);
                SetFloat(_material, "_FXModMin0", 0f);
                SetFloat(_material, "_FXModMax0", 1f);
                SetFloat(_material, "_FXModCurve0", 1f);

                // 自走させるので、静的な値は 0 にしておく。ここが 0 でないと
                // 変調分が上乗せされて常に溶けた状態から始まる。
                SetFloat(_material, "_DissolveAmount", 0f);

                EditorUtility.SetDirty(_material);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ================= 共通 =================

        private static void SetFloat(Material m, string prop, float value)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, value);
        }

        private static void SetColor(Material m, string prop, Color value)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, value);
        }

        private static string EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                return null;
            }
            if (AssetDatabase.IsValidFolder(assetPath)) return assetPath;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            return AssetDatabase.IsValidFolder(current) ? current : null;
        }
    }
}
