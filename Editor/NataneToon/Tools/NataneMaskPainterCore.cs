using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// シーンビュー マスクペインターの本体 (埋め込み可能クラス)。
    /// Embeddable Scene-view mask painter. Both the standalone
    /// <see cref="NataneMaskPainter"/> window and the Texture Studio tab reuse this
    /// single instance via <see cref="DrawControls"/> + <see cref="OnSceneGUI"/>.
    ///
    /// レンダラーを選び、シェーダーのマスク系テクスチャプロパティを列挙して塗る。
    /// Select a renderer, pick a mask texture property from the shader, then paint a
    /// soft round brush in UV space directly in the Scene view. Only the chosen
    /// channel (R/G/B/A) is written so multi-channel masks (e.g. _FXModMaskTex where
    /// R=slot0, G=slot1) keep their other channels intact.
    /// </summary>
    public class NataneMaskPainterCore
    {
        // ===== Target =====
        private Renderer targetRenderer;
        private int materialIndex;
        private Material activeMaterial;

        // ===== Mask targets enumerated from the shader =====
        private class MaskTarget
        {
            public string propertyName;
            public string displayName;
        }

        private readonly List<MaskTarget> maskTargets = new List<MaskTarget>();
        private int selectedTargetIndex;

        // ===== Brush =====
        private int channel;                 // 0=R 1=G 2=B 3=A
        private float brushRadiusPx = 24f;
        private float brushHardness = 0.5f;
        private float brushOpacity = 1f;
        private int dilatePixels = 4;

        // ===== Working texture / live preview =====
        private Texture2D workingTexture;    // RGBA32 editable copy assigned for live preview
        private Color32[] workingPixels;
        private int workingWidth;
        private int workingHeight;
        private string workingPropertyName;
        private Texture originalTexture;      // to restore on deactivate if unsaved
        private string savedAssetPath;        // set once written to disk
        private bool dirty;

        // ===== New-mask creation options =====
        private static readonly int[] SizeOptions = { 512, 1024, 2048 };
        private int newMaskSize = 1024;
        private bool newMaskWhite;

        // ===== Undo snapshot stack (last ~10 strokes) =====
        private const int MaxUndoSnapshots = 10;
        private readonly List<Color32[]> undoStack = new List<Color32[]>();

        // ===== Scene painting state =====
        private bool paintingEnabled;
        private bool sceneHooked;
        private GameObject colliderObject;
        private MeshCollider tempCollider;
        private Mesh bakedMesh;
        private bool strokeActive;
        private Vector2 lastUV;
        private double lastApplyTime;

        public bool HasWorkingTexture => workingTexture != null;

        // =================================================================
        //  Activation lifecycle (called by host window / tab)
        // =================================================================

        public void Activate()
        {
            SyncFromSelection();
        }

        public void Deactivate()
        {
            SetPaintingEnabled(false);
            RestoreOriginalIfUnsaved();
            CleanupCollider();
        }

        public void Dispose()
        {
            Deactivate();
            DestroyWorkingTexture();
            undoStack.Clear();
        }

        private void SyncFromSelection()
        {
            if (targetRenderer == null && Selection.activeGameObject != null)
            {
                var r = Selection.activeGameObject.GetComponent<Renderer>();
                if (r is SkinnedMeshRenderer || r is MeshRenderer)
                {
                    targetRenderer = r;
                }
            }
            RefreshMaterialAndTargets();
        }

        // =================================================================
        //  GUI (reused by window and studio tab)
        // =================================================================

        /// <summary>
        /// レイアウト GUI を描画する。標準の EditorGUILayout を使用するため、
        /// 単体ウィンドウ・統合スタジオタブの双方から呼び出せる。
        /// Draws the painter controls with EditorGUILayout so it works inside both
        /// the standalone window and the tabbed studio (which wraps it in a scroll).
        /// </summary>
        public void DrawControls()
        {
            HandleUndoShortcut(Event.current);

            EditorGUILayout.HelpBox(
                L("レンダラーとマスクプロパティを選び、シーンビューでマスクを直接ペイントします。Ctrl で消去、[ ] でブラシサイズ変更。",
                  "Select a renderer and a mask property, then paint the mask directly in the Scene view. Hold Ctrl to erase, use [ and ] to resize the brush."),
                MessageType.Info);

            DrawTargetSection();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawMaskTargetSection();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawBrushSection();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawActionSection();
        }

        /// <summary>
        /// Rect 指定版 (仕様要求の再利用エントリ)。内部で <see cref="DrawControls"/> を呼ぶ。
        /// Rect-based entry point (per spec). Wraps DrawControls in a layout area.
        /// </summary>
        public void OnGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);
            DrawControls();
            GUILayout.EndArea();
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("対象", "Target"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newRenderer = (Renderer)EditorGUILayout.ObjectField(
                L("レンダラー", "Renderer"), targetRenderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (newRenderer == null || newRenderer is SkinnedMeshRenderer || newRenderer is MeshRenderer)
                {
                    targetRenderer = newRenderer;
                    DiscardWorking();
                    RefreshMaterialAndTargets();
                    RebuildColliderIfPainting();
                }
            }

            if (targetRenderer != null)
            {
                Material[] mats = targetRenderer.sharedMaterials;
                if (mats != null && mats.Length > 1)
                {
                    string[] names = new string[mats.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        names[i] = string.Format("{0}: {1}", i, mats[i] != null ? mats[i].name : "(none)");
                    }
                    EditorGUI.BeginChangeCheck();
                    materialIndex = EditorGUILayout.Popup(L("マテリアルスロット", "Material Slot"),
                        Mathf.Clamp(materialIndex, 0, mats.Length - 1), names);
                    if (EditorGUI.EndChangeCheck())
                    {
                        DiscardWorking();
                        RefreshMaterialAndTargets();
                    }
                }
                else
                {
                    materialIndex = 0;
                }
            }

            if (activeMaterial == null)
            {
                EditorGUILayout.HelpBox(
                    L("Natane マテリアルを持つレンダラーを選択してください。",
                      "Select a renderer that uses a Natane material."),
                    MessageType.Warning);
            }
            else if (activeMaterial.shader != null && !activeMaterial.shader.name.Contains("Natane"))
            {
                EditorGUILayout.HelpBox(
                    L("このマテリアルは Natane シェーダーではありません。マスクプロパティが見つからない場合があります。",
                      "This material is not a Natane shader. Mask properties may not be found."),
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMaskTargetSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("マスクターゲット", "Mask Target"), EditorStyles.boldLabel);

            if (maskTargets.Count == 0)
            {
                EditorGUILayout.LabelField(L("マスクプロパティが見つかりません。", "No mask properties found."));
                EditorGUILayout.EndVertical();
                return;
            }

            string[] labels = new string[maskTargets.Count];
            for (int i = 0; i < maskTargets.Count; i++)
            {
                labels[i] = maskTargets[i].displayName;
            }

            EditorGUI.BeginChangeCheck();
            selectedTargetIndex = EditorGUILayout.Popup(L("マスク", "Mask"),
                Mathf.Clamp(selectedTargetIndex, 0, maskTargets.Count - 1), labels);
            if (EditorGUI.EndChangeCheck())
            {
                DiscardWorking();
            }

            // Channel selector R/G/B/A
            channel = GUILayout.Toolbar(channel, new[] { "R", "G", "B", "A" });
            EditorGUILayout.LabelField(
                L("塗りチャンネル (他チャンネルは保持されます)", "Paint channel (other channels are preserved)"),
                EditorStyles.miniLabel);

            MaskTarget target = maskTargets[Mathf.Clamp(selectedTargetIndex, 0, maskTargets.Count - 1)];
            Texture assigned = (activeMaterial != null && activeMaterial.HasProperty(target.propertyName))
                ? activeMaterial.GetTexture(target.propertyName)
                : null;

            if (workingTexture == null && assigned == null)
            {
                EditorGUILayout.Space(SPACE_TINY);
                EditorGUILayout.LabelField(
                    L("このスロットにはまだテクスチャがありません。", "This slot has no texture yet."),
                    EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                newMaskSize = EditorGUILayout.IntPopup(L("サイズ", "Size"), newMaskSize,
                    new[] { "512", "1024", "2048" }, SizeOptions);
                newMaskWhite = EditorGUILayout.ToggleLeft(
                    newMaskWhite ? L("白で初期化", "White init") : L("黒で初期化", "Black init"),
                    newMaskWhite, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button(L("新規マスク作成", "Create New Mask"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
                {
                    CreateNewMask(target.propertyName);
                }
            }
            else if (workingTexture == null && assigned != null)
            {
                if (GUILayout.Button(L("既存マスクを編集用に読み込み", "Load Existing Mask for Editing"),
                    GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
                {
                    BeginEditing(target.propertyName, assigned);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("ブラシ", "Brush"), EditorStyles.boldLabel);

            brushRadiusPx = EditorGUILayout.Slider(L("サイズ (px)", "Size (px)"), brushRadiusPx, 1f, 256f);
            brushHardness = EditorGUILayout.Slider(L("硬さ", "Hardness"), brushHardness, 0f, 1f);
            brushOpacity = EditorGUILayout.Slider(L("不透明度", "Opacity"), brushOpacity, 0f, 1f);
            dilatePixels = EditorGUILayout.IntSlider(L("保存時のにじみ (px)", "Bleed on save (px)"), dilatePixels, 0, 16);

            EditorGUILayout.EndVertical();
        }

        private void DrawActionSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            using (new EditorGUI.DisabledScope(workingTexture == null))
            {
                bool enable = EditorGUILayout.ToggleLeft(
                    L("シーンビューでペイントを有効化", "Enable Scene-view painting"), paintingEnabled);
                if (enable != paintingEnabled)
                {
                    SetPaintingEnabled(enable);
                }

                EditorGUILayout.Space(SPACE_TINY);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(undoStack.Count == 0))
                {
                    if (GUILayout.Button(L("元に戻す (Ctrl+Z)", "Undo (Ctrl+Z)"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
                    {
                        UndoLastStroke();
                    }
                }
                using (new EditorGUI.DisabledScope(!dirty && string.IsNullOrEmpty(savedAssetPath)))
                {
                    if (GUILayout.Button(L("保存 (PNG)", "Save (PNG)"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
                    {
                        SaveMask();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (workingTexture != null)
            {
                EditorGUILayout.Space(SPACE_TINY);
                EditorGUILayout.LabelField(
                    string.Format("{0}: {1}x{2}  {3}",
                        L("編集中", "Editing"), workingWidth, workingHeight,
                        dirty ? L("(未保存)", "(unsaved)") : string.Empty));
                Rect preview = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUI.DrawTextureTransparent(preview, workingTexture, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.EndVertical();
        }

        // =================================================================
        //  Scene view painting
        // =================================================================

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!paintingEnabled || workingTexture == null || tempCollider == null)
            {
                return;
            }

            Event e = Event.current;
            HandleUndoShortcut(e);
            HandleSizeShortcut(e);

            // Keep the scene-view control so clicks paint instead of selecting.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            bool hitOk = RaycastTarget(ray, out hit);

            if (hitOk)
            {
                DrawBrushPreview(hit);
                SceneView.RepaintAll();
            }

            bool erase = e.control || e.command;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && hitOk)
                    {
                        PushUndoSnapshot();
                        strokeActive = true;
                        lastUV = hit.textureCoord;
                        PaintAt(hit.textureCoord, erase);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && !e.alt && strokeActive && hitOk)
                    {
                        PaintStroke(lastUV, hit.textureCoord, erase);
                        lastUV = hit.textureCoord;
                        ThrottledApply();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 0 && strokeActive)
                    {
                        strokeActive = false;
                        ApplyWorkingTexture();
                        e.Use();
                    }
                    break;
            }
        }

        private bool RaycastTarget(Ray ray, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == tempCollider && hits[i].distance < best)
                {
                    best = hits[i].distance;
                    hit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        private void DrawBrushPreview(RaycastHit hit)
        {
            float uvFraction = brushRadiusPx / Mathf.Max(1, workingWidth);
            float worldRadius = uvFraction * targetRenderer.bounds.size.magnitude * 0.5f;
            worldRadius = Mathf.Max(worldRadius, HandleUtility.GetHandleSize(hit.point) * 0.02f);

            Handles.color = (Event.current.control || Event.current.command)
                ? new Color(1f, 0.4f, 0.4f, 1f)
                : new Color(0.3f, 0.7f, 1f, 1f);
            Handles.DrawWireDisc(hit.point, hit.normal, worldRadius);
        }

        private void PaintAt(Vector2 uv, bool erase)
        {
            NataneMaskPaintUtil.SplatBuffer(workingPixels, workingWidth, workingHeight,
                uv, brushRadiusPx, brushHardness, brushOpacity, channel, erase);
            dirty = true;
        }

        private void PaintStroke(Vector2 fromUV, Vector2 toUV, bool erase)
        {
            // Interpolate along the drag so fast strokes stay continuous.
            float uvDist = Vector2.Distance(
                new Vector2(fromUV.x * workingWidth, fromUV.y * workingHeight),
                new Vector2(toUV.x * workingWidth, toUV.y * workingHeight));
            int steps = Mathf.Max(1, Mathf.CeilToInt(uvDist / Mathf.Max(1f, brushRadiusPx * 0.5f)));
            for (int i = 1; i <= steps; i++)
            {
                Vector2 uv = Vector2.Lerp(fromUV, toUV, i / (float)steps);
                PaintAt(uv, erase);
            }
        }

        private void ThrottledApply()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastApplyTime > 0.03)
            {
                ApplyWorkingTexture();
                lastApplyTime = now;
            }
        }

        private void ApplyWorkingTexture()
        {
            if (workingTexture == null || workingPixels == null)
            {
                return;
            }
            workingTexture.SetPixels32(workingPixels);
            workingTexture.Apply(false);
        }

        private void SetPaintingEnabled(bool enable)
        {
            paintingEnabled = enable;
            if (enable)
            {
                RebuildCollider();
                if (!sceneHooked)
                {
                    SceneView.duringSceneGui += OnSceneGUI;
                    sceneHooked = true;
                }
            }
            else
            {
                if (sceneHooked)
                {
                    SceneView.duringSceneGui -= OnSceneGUI;
                    sceneHooked = false;
                }
                CleanupCollider();
            }
            SceneView.RepaintAll();
        }

        private void RebuildColliderIfPainting()
        {
            if (paintingEnabled)
            {
                RebuildCollider();
            }
        }

        private void RebuildCollider()
        {
            CleanupCollider();
            if (targetRenderer == null)
            {
                return;
            }

            Mesh mesh = null;
            bool skinned = targetRenderer is SkinnedMeshRenderer;
            if (skinned)
            {
                bakedMesh = new Mesh { name = "__NatanePaintBake" };
                ((SkinnedMeshRenderer)targetRenderer).BakeMesh(bakedMesh);
                mesh = bakedMesh;
            }
            else
            {
                MeshFilter mf = targetRenderer.GetComponent<MeshFilter>();
                mesh = mf != null ? mf.sharedMesh : null;
            }

            if (mesh == null)
            {
                return;
            }

            colliderObject = new GameObject("__NataneMaskPaintCollider")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Transform t = targetRenderer.transform;
            colliderObject.transform.SetPositionAndRotation(t.position, t.rotation);
            // BakeMesh already applies the skinned renderer scale, so use unit scale there.
            colliderObject.transform.localScale = skinned ? Vector3.one : t.lossyScale;

            tempCollider = colliderObject.AddComponent<MeshCollider>();
            tempCollider.sharedMesh = mesh;
        }

        private void CleanupCollider()
        {
            if (colliderObject != null)
            {
                Object.DestroyImmediate(colliderObject);
                colliderObject = null;
            }
            tempCollider = null;
            if (bakedMesh != null)
            {
                Object.DestroyImmediate(bakedMesh);
                bakedMesh = null;
            }
        }

        // =================================================================
        //  Mask lifecycle: create / load / save
        // =================================================================

        private void CreateNewMask(string propertyName)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                L("マスクを保存", "Save Mask"),
                propertyName.TrimStart('_') + "_Mask", "png",
                L("新規マスクの保存先を選択してください。", "Choose where to save the new mask."));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            int size = newMaskSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color32 fill = newMaskWhite ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = fill;
            }
            tex.SetPixels32(px);
            tex.Apply(false);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureMaskImporter(path);

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            AssignToMaterial(propertyName, imported);
            savedAssetPath = path;
            BeginEditing(propertyName, imported);
            dirty = false;
        }

        private void BeginEditing(string propertyName, Texture source)
        {
            DestroyWorkingTexture();

            int w = source != null ? Mathf.Max(1, source.width) : newMaskSize;
            int h = source != null ? Mathf.Max(1, source.height) : newMaskSize;

            workingTexture = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = propertyName + "_Working",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp
            };

            if (source != null)
            {
                CopyReadable(source, workingTexture);
            }
            else
            {
                var black = new Color32[w * h];
                for (int i = 0; i < black.Length; i++)
                {
                    black[i] = new Color32(0, 0, 0, 255);
                }
                workingTexture.SetPixels32(black);
            }
            workingTexture.Apply(false);

            workingPixels = workingTexture.GetPixels32();
            workingWidth = w;
            workingHeight = h;
            workingPropertyName = propertyName;
            originalTexture = source;
            savedAssetPath = AssetDatabase.GetAssetPath(source);
            dirty = false;
            undoStack.Clear();

            // Assign the editable copy for live preview.
            AssignToMaterial(propertyName, workingTexture);
        }

        private void SaveMask()
        {
            if (workingTexture == null || workingPixels == null)
            {
                return;
            }

            string path = savedAssetPath;
            if (string.IsNullOrEmpty(path))
            {
                path = EditorUtility.SaveFilePanelInProject(
                    L("マスクを保存", "Save Mask"),
                    (workingPropertyName ?? "Mask").TrimStart('_') + "_Mask", "png",
                    L("マスクの保存先を選択してください。", "Choose where to save the mask."));
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
            }

            // Dilate a copy so on-screen editing stays crisp but the saved file bleeds.
            Color32[] outPixels = (Color32[])workingPixels.Clone();
            NataneMaskPaintUtil.DilateChannel(outPixels, workingWidth, workingHeight, channel, dilatePixels);

            var outTex = new Texture2D(workingWidth, workingHeight, TextureFormat.RGBA32, false, true);
            outTex.SetPixels32(outPixels);
            outTex.Apply(false);
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureMaskImporter(path);

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (activeMaterial != null && !string.IsNullOrEmpty(workingPropertyName))
            {
                Undo.RecordObject(activeMaterial, "Assign Painted Mask");
                AssignToMaterial(workingPropertyName, imported);
            }

            savedAssetPath = path;
            originalTexture = imported;
            dirty = false;

            EditorUtility.DisplayDialog(
                L("保存完了", "Saved"),
                string.Format("{0}\n{1}", L("マスクを保存しました:", "Mask saved to:"), path), "OK");
        }

        private void ConfigureMaskImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            // Masks are data, not colour: keep them linear (sRGB off) and readable.
            importer.sRGBTexture = false;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 読み込み可否に関わらず、ソーステクスチャの内容を編集用 Texture2D へコピーする。
        /// Copy the source texture into <paramref name="dest"/> via a RenderTexture blit
        /// so it works even when the source asset is not marked readable.
        /// </summary>
        private static void CopyReadable(Texture source, Texture2D dest)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                dest.width, dest.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                dest.ReadPixels(new Rect(0, 0, dest.width, dest.height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private void AssignToMaterial(string propertyName, Texture texture)
        {
            if (activeMaterial != null && activeMaterial.HasProperty(propertyName))
            {
                activeMaterial.SetTexture(propertyName, texture);
                EditorUtility.SetDirty(activeMaterial);
            }
        }

        private void RestoreOriginalIfUnsaved()
        {
            if (workingTexture != null && dirty && activeMaterial != null &&
                !string.IsNullOrEmpty(workingPropertyName) && activeMaterial.HasProperty(workingPropertyName))
            {
                // Point the material back at the on-disk asset (or none) so it never
                // keeps a dangling reference to a non-asset working texture.
                activeMaterial.SetTexture(workingPropertyName, originalTexture);
                EditorUtility.SetDirty(activeMaterial);
            }
        }

        private void DiscardWorking()
        {
            SetPaintingEnabled(false);
            RestoreOriginalIfUnsaved();
            DestroyWorkingTexture();
            undoStack.Clear();
            dirty = false;
            savedAssetPath = null;
            workingPropertyName = null;
            originalTexture = null;
        }

        private void DestroyWorkingTexture()
        {
            if (workingTexture != null)
            {
                Object.DestroyImmediate(workingTexture);
                workingTexture = null;
            }
            workingPixels = null;
            workingWidth = 0;
            workingHeight = 0;
        }

        // =================================================================
        //  Undo stack
        // =================================================================

        private void PushUndoSnapshot()
        {
            if (workingPixels == null)
            {
                return;
            }
            undoStack.Add((Color32[])workingPixels.Clone());
            while (undoStack.Count > MaxUndoSnapshots)
            {
                undoStack.RemoveAt(0);
            }
        }

        private void UndoLastStroke()
        {
            if (undoStack.Count == 0 || workingPixels == null)
            {
                return;
            }
            Color32[] snapshot = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            if (snapshot.Length == workingPixels.Length)
            {
                System.Array.Copy(snapshot, workingPixels, workingPixels.Length);
                ApplyWorkingTexture();
                dirty = true;
                SceneView.RepaintAll();
            }
        }

        private void HandleUndoShortcut(Event e)
        {
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Z &&
                (e.control || e.command) && undoStack.Count > 0)
            {
                UndoLastStroke();
                e.Use();
            }
        }

        private void HandleSizeShortcut(Event e)
        {
            if (e == null || e.type != EventType.KeyDown)
            {
                return;
            }
            if (e.keyCode == KeyCode.LeftBracket)
            {
                brushRadiusPx = Mathf.Max(1f, brushRadiusPx - 2f);
                e.Use();
            }
            else if (e.keyCode == KeyCode.RightBracket)
            {
                brushRadiusPx = Mathf.Min(256f, brushRadiusPx + 2f);
                e.Use();
            }
        }

        // =================================================================
        //  Shader mask-property enumeration
        // =================================================================

        private void RefreshMaterialAndTargets()
        {
            activeMaterial = null;
            maskTargets.Clear();
            selectedTargetIndex = 0;

            if (targetRenderer == null)
            {
                return;
            }

            Material[] mats = targetRenderer.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                return;
            }
            materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
            activeMaterial = mats[materialIndex];

            if (activeMaterial == null || activeMaterial.shader == null)
            {
                return;
            }

            EnumerateMaskProperties(activeMaterial.shader, maskTargets);
        }

        /// <summary>
        /// シェーダーのテクスチャプロパティのうち、名前に "Mask" を含むものを列挙する。
        /// Enumerate texture (TexEnv) properties whose name contains "Mask" (covers
        /// _OutlineMask, _EmissionMask, _FaceOrthoMaskTex, _FXModMaskTex, etc.).
        /// </summary>
        public static void EnumerateMaskProperties(Shader shader, List<MaskTargetInfo> results)
        {
            if (shader == null || results == null)
            {
                return;
            }
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    continue;
                }
                string name = ShaderUtil.GetPropertyName(shader, i);
                if (name.IndexOf("Mask", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                string desc = ShaderUtil.GetPropertyDescription(shader, i);
                results.Add(new MaskTargetInfo
                {
                    propertyName = name,
                    displayName = string.IsNullOrEmpty(desc) ? name : string.Format("{0}  ({1})", desc, name)
                });
            }
        }

        private void EnumerateMaskProperties(Shader shader, List<MaskTarget> into)
        {
            var infos = new List<MaskTargetInfo>();
            EnumerateMaskProperties(shader, infos);
            foreach (var info in infos)
            {
                into.Add(new MaskTarget { propertyName = info.propertyName, displayName = info.displayName });
            }
        }

        /// <summary>Public struct for enumeration results (used by tests/tools).</summary>
        public struct MaskTargetInfo
        {
            public string propertyName;
            public string displayName;
        }
    }
}
