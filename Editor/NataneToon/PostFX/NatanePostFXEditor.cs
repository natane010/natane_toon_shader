using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NataneToon.Editor
{
    /// <summary>NPFX 属性の付いた値を、日本語 / 英語のラベルとスライダーで描く。</summary>
    [CustomPropertyDrawer(typeof(NPFXAttribute))]
    public sealed class NPFXDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (NPFXAttribute)attribute;
            var content = new GUIContent(NataneToonLocalization.L(attr.ja, attr.en), label.tooltip);
            // 配列の要素にも同じ属性が付くので、要素は Unity の既定のラベル (Element 0 など) のままにする。
            if (property.propertyPath.EndsWith("]")) content = label;
            if (attr.hasRange && property.propertyType == SerializedPropertyType.Float)
            {
                EditorGUI.BeginProperty(position, content, property);
                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.Slider(position, content, property.floatValue, attr.min, attr.max);
                if (EditorGUI.EndChangeCheck()) property.floatValue = value;
                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.PropertyField(position, property, content, true);
            }
        }
    }

    [CustomEditor(typeof(NatanePostFX))]
    [CanEditMultipleObjects]
    public sealed class NatanePostFXEditor : UnityEditor.Editor
    {
        /// <summary>
        /// URP 側のエディターアセンブリが差し込むフック (このアセンブリは URP を参照できないため)。
        /// CheckFeature: 使用中の Renderer に Natane Post FX の Renderer Feature があるか (null = 判定できない)。
        /// </summary>
        public static Func<bool?> CheckUrpFeature;
        public static Action AddUrpFeature;

        struct Section
        {
            public string field, ja, en;
            public Section(string field, string ja, string en) { this.field = field; this.ja = ja; this.en = en; }
        }

        static readonly Section[] s_Sections =
        {
            new Section("color", "色 (カラーグレーディング・フィルター)", "Color"),
            new Section("glow", "光 (ブルーム・光条・光芒・ディフュージョン)", "Light"),
            new Section("focus", "ピント (被写界深度・チルトシフト・ぼかし)", "Focus"),
            new Section("lens", "レンズ・画面の変形", "Lens & Distortion"),
            new Section("glitch", "グリッチ・VHS", "Glitch"),
            new Section("style", "画風 (輪郭線・網点・油彩・減色)", "Stylize"),
            new Section("overlay", "重ねもの (色かけ・ビネット・集中線・フラッシュ・フェード)", "Overlays"),
            new Section("retro", "フィルム・ブラウン管", "Film & CRT"),
            new Section("fog", "距離フォグ", "Distance Fog"),
            new Section("mask", "キャラクターマスク (指定した対象と背景を分ける)", "Character Mask"),
            new Section("time", "時間 (残像・コマ落ち)", "Temporal"),
            new Section("beat", "拍に合わせる (BPM)", "Beat Sync"),
        };

        int m_PresetIndex;

        static string L(string ja, string en) { return NataneToonLocalization.L(ja, en); }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawStatus();
            DrawPresets();
            EditorGUILayout.Space(4);

            foreach (Section section in s_Sections)
            {
                SerializedProperty group = serializedObject.FindProperty(section.field);
                if (group == null) continue;
                string key = "NatanePostFX.Foldout." + section.field;
                bool open = SessionState.GetBool(key, section.field == "color");
                bool nowOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, L(section.ja, section.en));
                EditorGUILayout.EndFoldoutHeaderGroup();
                if (nowOpen != open) SessionState.SetBool(key, nowOpen);
                if (!nowOpen) continue;

                EditorGUI.indentLevel++;
                SerializedProperty child = group.Copy();
                SerializedProperty end = group.GetEndProperty();
                bool enter = true;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                {
                    enter = false;
                    EditorGUILayout.PropertyField(child, true);
                }
                if (section.field == "mask") DrawMaskNotes();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useCustomTime"));
            if (serializedObject.FindProperty("useCustomTime").boolValue)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("customTime"));

            serializedObject.ApplyModifiedProperties();

            // 編集中も動きのある効果 (ノイズ・グリッチなど) が見えるように描き直す。
            if (!Application.isPlaying && Event.current.type == EventType.Repaint) UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        void DrawStatus()
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            if (!urp)
            {
                EditorGUILayout.HelpBox(L(
                    "Built-in RP: このコンポーネントがカメラの映像に効果をかけます。値はすべて Animation / Timeline でキーフレームを打てます。",
                    "Built-in RP: this component applies the effects to the camera image. Every value can be keyframed in Animation / Timeline."),
                    MessageType.Info);
                return;
            }

            bool? hasFeature = CheckUrpFeature != null ? CheckUrpFeature() : null;
            if (hasFeature == true)
            {
                EditorGUILayout.HelpBox(L(
                    "URP: Renderer に Natane Post FX が登録されています。値はすべて Animation / Timeline でキーフレームを打てます。",
                    "URP: the renderer has the Natane Post FX feature. Every value can be keyframed in Animation / Timeline."),
                    MessageType.Info);
            }
            else if (CheckUrpFeature == null)
            {
                EditorGUILayout.HelpBox(L(
                    "このレンダーパイプラインには対応していません。URP は 17 以降 (Unity 6) が必要です。",
                    "This render pipeline is not supported. URP needs version 17 or newer (Unity 6)."),
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(L(
                    "URP: 使用中の Renderer に Natane Post FX の Renderer Feature がありません。追加するまで効果は出ません。",
                    "URP: the active renderer has no Natane Post FX Renderer Feature. Nothing is drawn until it is added."),
                    MessageType.Warning);
                if (AddUrpFeature != null && GUILayout.Button(L("Renderer に追加する", "Add to Renderer"))) AddUrpFeature();
            }
        }

        void DrawPresets()
        {
            NatanePostFXPresets.Preset[] presets = NatanePostFXPresets.All;
            var names = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++) names[i] = L(presets[i].ja, presets[i].en);

            using (new EditorGUILayout.HorizontalScope())
            {
                m_PresetIndex = EditorGUILayout.Popup(L("プリセット", "Preset"), Mathf.Clamp(m_PresetIndex, 0, names.Length - 1), names);
                if (GUILayout.Button(L("適用", "Apply"), GUILayout.Width(60)))
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (UnityEngine.Object o in targets)
                    {
                        var fx = (NatanePostFX)o;
                        Undo.RecordObject(fx, "Apply Natane Post FX Preset");
                        NatanePostFXPresets.ResetAll(fx);
                        presets[m_PresetIndex].apply(fx);
                        fx.RefreshMaskRenderers();
                        EditorUtility.SetDirty(fx);
                    }
                    serializedObject.Update();
                }
            }
            EditorGUILayout.LabelField(L("プリセットは全項目を上書きします (マスク対象などの参照は残ります)。", "A preset overwrites every value (object references are kept)."),
                EditorStyles.miniLabel);
        }

        void DrawMaskNotes()
        {
            var fx = (NatanePostFX)target;
            if (fx.mask.HasEffect && (fx.mask.targets == null || fx.mask.targets.Length == 0))
            {
                EditorGUILayout.HelpBox(L(
                    "「対象」にアバターなどのルートを入れてください。その下のメッシュがキャラクターとして扱われます。",
                    "Add the avatar root (or any objects) to Targets. Meshes under them are treated as the character."),
                    MessageType.Warning);
            }
            if (GUILayout.Button(L("対象のメッシュを集め直す", "Refresh Target Meshes"))) fx.RefreshMaskRenderers();
        }
    }

    public static class NatanePostFXMenu
    {
        [MenuItem(NataneToolMenuPaths.PostFXSetup, false, 2101)]
        static void AddToCamera()
        {
            Camera camera = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Camera>() : null;
            if (camera == null) camera = Camera.main;
            if (camera == null)
            {
                EditorUtility.DisplayDialog("Natane Post FX",
                    NataneToonLocalization.L("カメラが見つかりません。カメラを選択してから実行してください。", "No camera found. Select a camera first."), "OK");
                return;
            }

            NatanePostFX fx = camera.GetComponent<NatanePostFX>();
            if (fx == null) fx = Undo.AddComponent<NatanePostFX>(camera.gameObject);
            Selection.activeGameObject = camera.gameObject;
            EditorGUIUtility.PingObject(fx);

            if (GraphicsSettings.currentRenderPipeline != null && NatanePostFXEditor.CheckUrpFeature != null &&
                NatanePostFXEditor.CheckUrpFeature() == false && NatanePostFXEditor.AddUrpFeature != null)
            {
                NatanePostFXEditor.AddUrpFeature();
            }
        }
    }
}
