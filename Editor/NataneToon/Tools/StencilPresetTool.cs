using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Stencil preset tool — the "See Through Hair" workflow.
    ///
    /// Drawing the eyebrows and eyes in front of the bangs is the standard way to get the
    /// 2D-illustration look where the eyes read through the hair. The package already has
    /// every stencil property exposed (<c>_StencilRef</c> / <c>_StencilComp</c> / ... on the
    /// main shader), but users had to work out the Ref / Comp / Op combination themselves,
    /// with no presets and no documentation. The mechanism existed; the workflow did not,
    /// and that is what kept people on lilToon and Poiyomi for this effect.
    ///
    /// Nothing is written without Undo, and the tool never touches a material it was not
    /// pointed at.
    /// </summary>
    public class StencilPresetTool : EditorWindow
    {
        /// <summary>ステンシルにおける役割。</summary>
        public enum StencilRole
        {
            /// <summary>眉・目・まつげ。髪より前に描き、ステンシルへ参照値を書き込む。</summary>
            Writer,

            /// <summary>前髪。Writer が書いた領域では描かない（完全透過）。</summary>
            CutterOpaque,

            /// <summary>前髪。完全には消さず薄く残す（半透明）。</summary>
            CutterTranslucent,

            /// <summary>ステンシルを使わない状態へ戻す。</summary>
            None
        }

        private sealed class Entry
        {
            public Renderer Renderer;
            public StencilRole Role = StencilRole.None;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private GameObject _avatarRoot;
        private int _stencilRef = 1;
        private float _translucentAlpha = 0.35f;
        private Vector2 _scroll;
        private bool _showOrderPreview = true;

        // Writer を髪より前に出すためのキュー。Geometry(2000) より前に置く。
        private const int WriterQueue = 1990;

        [MenuItem(NataneToolMenuPaths.StencilPresetTool, false, 46)]
        public static void ShowWindow()
        {
            var window = GetWindow<StencilPresetTool>(L("ステンシルプリセット", "Stencil Preset"));
            window.minSize = new Vector2(560, 620);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("ステンシルプリセット（髪から目を透かす）", "Stencil Preset (see-through hair)"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("眉・目を前髪より前に描き、前髪側をくり抜きます。参照値の割り当てまで面倒を見ます。",
                  "Draws eyebrows and eyes in front of the bangs and cuts the bangs away. Reference values are assigned for you."),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            _avatarRoot = (GameObject)EditorGUILayout.ObjectField(
                L("アバターのルート", "Avatar Root"), _avatarRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                CollectRenderers();
            }

            if (_avatarRoot == null)
            {
                EditorGUILayout.HelpBox(
                    L("アバターのルートを指定すると、配下の Renderer を一覧します。",
                      "Assign the avatar root to list the renderers underneath it."),
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            DrawReferenceValue();

            EditorGUILayout.Space(6);
            DrawRoleAssignment();

            EditorGUILayout.Space(6);
            DrawValidation();

            EditorGUILayout.Space(8);
            DrawApply();
        }

        // ---- 参照値 ----

        private void DrawReferenceValue()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("ステンシル参照値", "Stencil Reference"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _stencilRef = EditorGUILayout.IntSlider(L("参照値", "Reference"), _stencilRef, 1, 255);
            if (GUILayout.Button(L("未使用値を探す", "Find unused"), GUILayout.Width(120)))
            {
                _stencilRef = FindUnusedReference();
            }
            EditorGUILayout.EndHorizontal();

            HashSet<int> inUse = CollectReferencesInUse();
            if (inUse.Contains(_stencilRef))
            {
                EditorGUILayout.HelpBox(
                    L($"参照値 {_stencilRef} はプロジェクト内の他のマテリアルで既に使われています。" +
                      "同じアバター内での意図的な共有でなければ、別の値を選んでください。",
                      $"Reference {_stencilRef} is already used by other materials in the project. " +
                      "Pick another value unless you are intentionally sharing it within this avatar."),
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                L("⚠ VRChat では他のアバターのステンシルと衝突しえます。衝突すると相手の見た目を壊す" +
                  "可能性があるため、広く使われていそうな小さい値（1〜10）は避けるのが無難です。",
                  "⚠ In VRChat this can collide with other avatars' stencils. A collision can break how they " +
                  "look, so avoid the low values (1-10) that everyone reaches for first."),
                MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// プロジェクト内のマテリアルが使っている参照値を集める。
        /// 他アバターとの衝突までは検出できないが、少なくとも自分の中での衝突は防げる。
        /// </summary>
        private static HashSet<int> CollectReferencesInUse()
        {
            var used = new HashSet<int>();

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets", StringComparison.Ordinal)) continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_StencilRef")) continue;

                int value = Mathf.RoundToInt(material.GetFloat("_StencilRef"));
                if (value > 0) used.Add(value);
            }

            return used;
        }

        private static int FindUnusedReference()
        {
            HashSet<int> used = CollectReferencesInUse();
            // 低い値は他アバターと衝突しやすいので、少し上から探す。
            for (int i = 32; i <= 255; i++)
            {
                if (!used.Contains(i)) return i;
            }
            for (int i = 1; i < 32; i++)
            {
                if (!used.Contains(i)) return i;
            }
            return 1;
        }

        // ---- 役割の割り当て ----

        private void CollectRenderers()
        {
            _entries.Clear();
            if (_avatarRoot == null) return;

            foreach (Renderer renderer in _avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                _entries.Add(new Entry { Renderer = renderer, Role = GuessRole(renderer) });
            }
        }

        /// <summary>
        /// 名前からの推測。当たらなくても実害は無く、ユーザーが直せばよいので
        /// 素直な部分一致にしてある。
        /// </summary>
        private static StencilRole GuessRole(Renderer renderer)
        {
            string name = renderer.name.ToLowerInvariant();

            if (name.Contains("eyebrow") || name.Contains("brow") || name.Contains("mayu") ||
                name.Contains("eyelash") || name.Contains("lash") || name.Contains("eye"))
            {
                return StencilRole.Writer;
            }

            if (name.Contains("hair") || name.Contains("bang") || name.Contains("kami") ||
                name.Contains("front"))
            {
                return StencilRole.CutterOpaque;
            }

            return StencilRole.None;
        }

        private void DrawRoleAssignment()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("役割の割り当て", "Role Assignment"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                L("Writer = 眉・目・まつげ（髪より前に描く）\n" +
                  "Cutter (完全透過) = 前髪。Writer の領域を完全にくり抜く\n" +
                  "Cutter (半透明) = 前髪。薄く残す。眉は完全に透かし前髪は少し残す、という使い分け用",
                  "Writer = eyebrows / eyes / lashes (drawn in front of the hair)\n" +
                  "Cutter (opaque) = bangs, fully cut away where the writer drew\n" +
                  "Cutter (translucent) = bangs, left faintly visible — for \"fully show brows, keep some hair\""),
                MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(160), GUILayout.MaxHeight(280));

            foreach (Entry entry in _entries)
            {
                if (entry.Renderer == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(entry.Renderer, typeof(Renderer), true, GUILayout.MinWidth(180));
                entry.Role = (StencilRole)EditorGUILayout.EnumPopup(entry.Role, GUILayout.Width(160));

                int queue = entry.Renderer.sharedMaterial != null ? entry.Renderer.sharedMaterial.renderQueue : -1;
                EditorGUILayout.LabelField(queue >= 0 ? $"Q{queue}" : "-", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ---- 検証 ----

        private void DrawValidation()
        {
            int writers = _entries.Count(e => e.Role == StencilRole.Writer);
            int cutters = _entries.Count(e => e.Role == StencilRole.CutterOpaque || e.Role == StencilRole.CutterTranslucent);

            if (writers > 0 && cutters == 0)
            {
                EditorGUILayout.HelpBox(
                    L("Writer だけが指定されています。Cutter（前髪）が無いと、" +
                      "眉や目が単に手前に描かれるだけで透過になりません。",
                      "Only writers are assigned. Without a cutter (the bangs) the brows and eyes are simply " +
                      "drawn in front — nothing becomes see-through."),
                    MessageType.Warning);
            }
            else if (cutters > 0 && writers == 0)
            {
                EditorGUILayout.HelpBox(
                    L("Cutter だけが指定されています。Writer（眉・目）が無いと、" +
                      "前髪がくり抜かれる領域が存在せず何も起きません。",
                      "Only cutters are assigned. Without a writer (brows / eyes) there is no region to cut, " +
                      "so nothing happens."),
                    MessageType.Warning);
            }

            if (_showOrderPreview && (writers > 0 || cutters > 0))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(L("適用後の描画順", "Draw order after apply"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    L("Writer のキューを前へ出すため、不透明のソート順が変わります。",
                      "Moving the writer queue forward changes opaque sort order."),
                    EditorStyles.miniLabel);

                foreach (Entry entry in _entries.Where(e => e.Role != StencilRole.None)
                                                .OrderBy(e => PlannedQueue(e)))
                {
                    Material mat = entry.Renderer != null ? entry.Renderer.sharedMaterial : null;
                    int before = mat != null ? mat.renderQueue : -1;
                    int after = PlannedQueue(entry);

                    string arrow = before == after ? "=" : $"{before} → {after}";
                    EditorGUILayout.LabelField($"  [{entry.Role}] {entry.Renderer.name}", arrow);
                }
                EditorGUILayout.EndVertical();
            }

            _showOrderPreview = EditorGUILayout.ToggleLeft(
                L("描画順の差分を表示", "Show draw order diff"), _showOrderPreview);
        }

        private int PlannedQueue(Entry entry)
        {
            Material mat = entry.Renderer != null ? entry.Renderer.sharedMaterial : null;
            int current = mat != null ? mat.renderQueue : 2000;

            switch (entry.Role)
            {
                case StencilRole.Writer:
                    return WriterQueue;
                case StencilRole.CutterTranslucent:
                    return Mathf.Max(current, 3000);
                default:
                    return current;
            }
        }

        // ---- 適用 ----

        private void DrawApply()
        {
            _translucentAlpha = EditorGUILayout.Slider(
                L("半透明カッターの不透明度", "Translucent cutter alpha"), _translucentAlpha, 0f, 1f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(L("ステンシル設定を適用", "Apply Stencil Setup"), GUILayout.Height(30)))
            {
                Apply();
            }

            if (GUILayout.Button(L("ステンシルを解除", "Clear Stencil"), GUILayout.Height(30), GUILayout.Width(160)))
            {
                Clear();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                L("マテリアルを直接書き換えます。Undo（Ctrl+Z）で戻せます。",
                  "Materials are edited in place. Undo (Ctrl+Z) reverts it."),
                MessageType.None);
        }

        private void Apply()
        {
            var touched = new List<Material>();

            foreach (Entry entry in _entries)
            {
                if (entry.Renderer == null || entry.Role == StencilRole.None) continue;

                foreach (Material material in entry.Renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty("_StencilRef")) continue;
                    if (touched.Contains(material)) continue;

                    Undo.RecordObject(material, "Apply Stencil Preset");
                    ApplyRole(material, entry.Role);
                    EditorUtility.SetDirty(material);
                    touched.Add(material);
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                L("ステンシルプリセット", "Stencil Preset"),
                L($"{touched.Count} 個のマテリアルへ適用しました。参照値: {_stencilRef}",
                  $"Applied to {touched.Count} material(s). Reference: {_stencilRef}"),
                "OK");
        }

        private void ApplyRole(Material material, StencilRole role)
        {
            switch (role)
            {
                case StencilRole.Writer:
                    // Always(8) で必ず通し、Replace(2) で参照値を書き込む。
                    material.SetFloat("_StencilRef", _stencilRef);
                    material.SetFloat("_StencilComp", (float)CompareFunction.Always);
                    material.SetFloat("_StencilOp", (float)StencilOp.Replace);
                    material.SetFloat("_StencilFail", (float)StencilOp.Keep);
                    material.SetFloat("_StencilZFail", (float)StencilOp.Keep);
                    // 髪より前に描く。ここを前へ出さないと、髪が先に描かれて
                    // ステンシルを読む相手が居なくなる。
                    material.renderQueue = WriterQueue;
                    break;

                case StencilRole.CutterOpaque:
                    // NotEqual(6): Writer が書いた領域以外にだけ描く = そこだけ髪が消える。
                    material.SetFloat("_StencilRef", _stencilRef);
                    material.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
                    material.SetFloat("_StencilOp", (float)StencilOp.Keep);
                    material.SetFloat("_StencilFail", (float)StencilOp.Keep);
                    material.SetFloat("_StencilZFail", (float)StencilOp.Keep);
                    break;

                case StencilRole.CutterTranslucent:
                    material.SetFloat("_StencilRef", _stencilRef);
                    material.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
                    material.SetFloat("_StencilOp", (float)StencilOp.Keep);
                    material.SetFloat("_StencilFail", (float)StencilOp.Keep);
                    material.SetFloat("_StencilZFail", (float)StencilOp.Keep);

                    // 「薄く残す」ため、透明系バリアントであればアルファを下げる。
                    // 不透明バリアントではアルファが効かないので触らない。
                    if (material.HasProperty("_Alpha"))
                    {
                        material.SetFloat("_Alpha", _translucentAlpha);
                        material.renderQueue = Mathf.Max(material.renderQueue, 3000);
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        Color c = material.GetColor("_Color");
                        c.a = _translucentAlpha;
                        material.SetColor("_Color", c);
                    }
                    break;
            }
        }

        private void Clear()
        {
            var touched = new List<Material>();

            foreach (Entry entry in _entries)
            {
                if (entry.Renderer == null) continue;

                foreach (Material material in entry.Renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty("_StencilRef")) continue;
                    if (touched.Contains(material)) continue;

                    Undo.RecordObject(material, "Clear Stencil Preset");
                    material.SetFloat("_StencilRef", 0f);
                    material.SetFloat("_StencilComp", (float)CompareFunction.Always);
                    material.SetFloat("_StencilOp", (float)StencilOp.Keep);
                    material.SetFloat("_StencilFail", (float)StencilOp.Keep);
                    material.SetFloat("_StencilZFail", (float)StencilOp.Keep);
                    // キューはシェーダー既定へ戻す。-1 が「シェーダーに従う」の意味。
                    material.renderQueue = -1;
                    EditorUtility.SetDirty(material);
                    touched.Add(material);
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                L("ステンシルプリセット", "Stencil Preset"),
                L($"{touched.Count} 個のマテリアルのステンシルを解除しました。",
                  $"Cleared stencil settings on {touched.Count} material(s)."),
                "OK");
        }
    }
}
