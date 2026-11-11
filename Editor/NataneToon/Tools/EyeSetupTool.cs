using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Eye Setup Tool
    /// 目のセットアップツール
    ///
    /// 1. Material separation: extract eye triangles into their own submesh and
    ///    material slot so they can use the dedicated "Natane/Eye" shader.
    ///    マテリアル分け: 目のポリゴンを専用サブメッシュ + マテリアルスロットへ分離し、
    ///    "Natane/Eye" シェーダーを割り当てられるようにします。
    /// 2. UV relayout: rescale the UV island(s) of a submesh into a target rect
    ///    (overlap or side-by-side left/right eye layouts supported).
    ///    UV再配置: サブメッシュの UV アイランドをターゲット矩形へ拡大配置します
    ///    （左右の目の重ね配置 / 並べ配置に対応）。
    ///
    /// All mesh output is written to NEW mesh assets; imported meshes are never modified.
    /// メッシュは必ず新規アセットとして保存され、インポート元のメッシュは変更しません。
    /// </summary>
    public class EyeSetupTool : EditorWindow
    {
        // ===== Target =====
        private Renderer targetRenderer;
        private Mesh lastMesh;

        // ===== Tabs =====
        private int activeTab;
        private Vector2 scrollPosition;

        // ===== Material separation =====
        private EyeSelectionMode selectionMode = EyeSelectionMode.UVRect;
        private int selectedSlot;
        private Vector2 uvRectMin = new Vector2(0.3f, 0.3f);
        private Vector2 uvRectMax = new Vector2(0.7f, 0.7f);
        private Texture2D maskTexture;
        private float maskThreshold = 0.5f;
        private bool createEyeMaterial = true;

        private bool[] selectedTriangles;
        private int selectedTriangleCount;
        private bool selectionDirty = true;

        private bool draggingRect;
        private Vector2 dragStartUV;

        // ===== UV relayout =====
        private int relayoutSubmesh;
        private int relayoutUVChannel; // 0 = uv0, 1 = uv1
        private Rect targetRect = new Rect(0f, 0f, 1f, 1f);
        private float relayoutPadding = 0.02f;
        private EyeIslandLayout islandLayout = EyeIslandLayout.OverlapLeftRight;
        private bool mirrorSecondGroup = true;
        private bool preserveAspect = true;

        private Vector2[] remapPreviewUVs;
        private EyeUVRemapReport remapReport;
        private bool remapDirty = true;

        // ===== Preview caches =====
        private Mesh previewMesh;
        private Vector2[] previewUV0;
        private List<int[]> previewSubTriangles;

        private Mesh beforeUVMesh;
        private int beforeUVChannel = -1;
        private Vector2[] beforeUVs;

        private Texture2D cachedMaskSource;
        private Texture2D cachedMaskReadable;
        private Material lineMaterial;

        private const int MaxPreviewTriangles = 6000;
        private const float PreviewMaxSize = 320f;

        [MenuItem(NataneToolMenuPaths.EyeSetupTool, false, 62)]
        public static void ShowWindow()
        {
            var window = GetWindow<EyeSetupTool>(L("目のセットアップ", "Eye Setup Tool"));
            window.minSize = new Vector2(480, 620);
            window.Show();
        }

        private void OnDisable()
        {
            ReleaseMaskCache();
            if (lineMaterial != null)
            {
                DestroyImmediate(lineMaterial);
                lineMaterial = null;
            }
        }

        // =====================================================================
        // GUI
        // =====================================================================

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            NataneToonShaderGUIUtility.DrawToolHeader("目のセットアップ", "Eye Setup Tool", "EyeSetupTool");
            EditorGUILayout.HelpBox(
                L("目のポリゴンを専用マテリアルへ分離し、UVを再配置するツールです。メッシュは新規アセットとして保存され、元のメッシュは変更されません。",
                  "Separate eye polygons into a dedicated material and relayout their UVs. Meshes are saved as new assets; the original mesh is never modified."),
                MessageType.Info);
            EditorGUILayout.Space(4);

            DrawTargetSection();

            Mesh mesh = GetTargetMesh();
            if (mesh != lastMesh)
            {
                lastMesh = mesh;
                OnTargetMeshChanged(mesh);
            }

            EditorGUILayout.Space(6);
            int newTab = GUILayout.Toolbar(activeTab, new[]
            {
                L("1. マテリアル分け", "1. Material Separation"),
                L("2. UV再配置", "2. UV Relayout")
            }, GUILayout.Height(24));
            if (newTab != activeTab)
            {
                activeTab = newTab;
                remapDirty = true;
            }
            EditorGUILayout.Space(4);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (mesh == null)
            {
                EditorGUILayout.HelpBox(
                    L("対象のレンダラーを選択してください（SkinnedMeshRenderer または MeshRenderer + MeshFilter）。",
                      "Select a target renderer (SkinnedMeshRenderer, or MeshRenderer with a MeshFilter)."),
                    MessageType.Warning);
            }
            else if (activeTab == 0)
            {
                DrawSeparationTab(mesh);
            }
            else
            {
                DrawRelayoutTab(mesh);
            }
            EditorGUILayout.EndScrollView();
        }

        // =====================================================================
        // Target section
        // =====================================================================

        private void DrawTargetSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("対象レンダラー", "Target Renderer"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            var newRenderer = (Renderer)EditorGUILayout.ObjectField(
                targetRenderer, typeof(Renderer), true);
            if (newRenderer != targetRenderer)
            {
                if (newRenderer == null || IsSupportedRenderer(newRenderer))
                    targetRenderer = newRenderer;
                else
                    ShowNotification(new GUIContent(
                        L("SkinnedMeshRenderer / MeshRenderer のみ対応しています。",
                          "Only SkinnedMeshRenderer / MeshRenderer are supported.")));
            }

            if (GUILayout.Button(L("選択から取得", "From Selection"), GUILayout.Width(110)))
            {
                Renderer picked = PickRendererFromSelection();
                if (picked != null)
                    targetRenderer = picked;
                else
                    ShowNotification(new GUIContent(
                        L("選択オブジェクトにレンダラーが見つかりません。",
                          "No supported renderer found on the selection.")));
            }
            EditorGUILayout.EndHorizontal();

            Mesh mesh = GetTargetMesh();
            if (targetRenderer != null && mesh == null)
            {
                EditorGUILayout.HelpBox(
                    L("メッシュが見つかりません。SkinnedMeshRenderer の Mesh、または MeshFilter を確認してください。",
                      "No mesh found. Check the SkinnedMeshRenderer mesh or the MeshFilter."),
                    MessageType.Warning);
            }
            else if (mesh != null)
            {
                EditorGUILayout.LabelField(
                    L("メッシュ情報", "Mesh Info"),
                    string.Format("{0}  |  {1} verts  |  {2} tris  |  {3} submeshes",
                        mesh.name,
                        mesh.vertexCount,
                        EyeSetupMeshUtility.TotalTriangleCount(mesh),
                        mesh.subMeshCount));
            }

            EditorGUILayout.EndVertical();
        }

        private static bool IsSupportedRenderer(Renderer renderer)
        {
            return renderer is SkinnedMeshRenderer || renderer is MeshRenderer;
        }

        private static Renderer PickRendererFromSelection()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
                return null;

            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
                return smr;
            var mr = go.GetComponentInChildren<MeshRenderer>(true);
            return mr;
        }

        private Mesh GetTargetMesh()
        {
            if (targetRenderer == null)
                return null;

            var smr = targetRenderer as SkinnedMeshRenderer;
            if (smr != null)
                return smr.sharedMesh;

            var mf = targetRenderer.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        private void OnTargetMeshChanged(Mesh mesh)
        {
            selectionDirty = true;
            remapDirty = true;
            selectedTriangles = null;
            previewMesh = null;
            beforeUVMesh = null;

            if (mesh != null)
            {
                selectedSlot = Mathf.Clamp(selectedSlot, 0, mesh.subMeshCount - 1);
                relayoutSubmesh = Mathf.Clamp(relayoutSubmesh, 0, mesh.subMeshCount - 1);
            }
        }

        // =====================================================================
        // Tab 1: Material separation
        // =====================================================================

        private void DrawSeparationTab(Mesh mesh)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("目の選択方法", "Eye Selection Method"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            selectionMode = (EyeSelectionMode)EditorGUILayout.Popup(
                L("選択モード", "Selection Mode"),
                (int)selectionMode,
                new[]
                {
                    L("既存マテリアルスロット", "Existing Material Slot"),
                    L("UV範囲（プレビューでドラッグ可）", "UV Rect (drag on preview)"),
                    L("マスクテクスチャ（白=目）", "Mask Texture (white = eye)")
                });

            switch (selectionMode)
            {
                case EyeSelectionMode.MaterialSlot:
                    selectedSlot = EditorGUILayout.Popup(
                        L("マテリアルスロット", "Material Slot"),
                        Mathf.Clamp(selectedSlot, 0, mesh.subMeshCount - 1),
                        BuildSlotNames(mesh));
                    EditorGUILayout.HelpBox(
                        L("このモードではメッシュは変更されず、選択したスロットに新しい目マテリアルを割り当てます。",
                          "In this mode the mesh is not modified; a new eye material is assigned to the selected slot."),
                        MessageType.None);
                    break;

                case EyeSelectionMode.UVRect:
                    uvRectMin = EditorGUILayout.Vector2Field(L("UV最小 (Min)", "UV Min"), uvRectMin);
                    uvRectMax = EditorGUILayout.Vector2Field(L("UV最大 (Max)", "UV Max"), uvRectMax);
                    EditorGUILayout.HelpBox(
                        L("下のプレビュー上をドラッグしても範囲を指定できます。UV重心が範囲内の三角形が選択されます。",
                          "You can also drag a rectangle on the preview below. Triangles whose UV centroid is inside the rect are selected."),
                        MessageType.None);
                    break;

                case EyeSelectionMode.TextureMask:
                    maskTexture = (Texture2D)EditorGUILayout.ObjectField(
                        L("マスクテクスチャ", "Mask Texture"), maskTexture, typeof(Texture2D), false);
                    maskThreshold = EditorGUILayout.Slider(
                        L("しきい値", "Threshold"), maskThreshold, 0f, 1f);
                    EditorGUILayout.HelpBox(
                        L("UV重心の位置でマスクをサンプリングし、輝度がしきい値以上の三角形を選択します。",
                          "The mask is sampled at each triangle's UV centroid; triangles above the threshold are selected."),
                        MessageType.None);
                    break;
            }

            createEyeMaterial = EditorGUILayout.ToggleLeft(
                L("\"Natane/Eye\" マテリアルを新規作成して割り当てる（メインテクスチャ/色を引き継ぎ）",
                  "Create and assign a new \"Natane/Eye\" material (copies main texture / base color)"),
                createEyeMaterial);

            if (EditorGUI.EndChangeCheck())
                selectionDirty = true;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            EnsureSelection(mesh);

            // Preview
            EditorGUILayout.LabelField(L("UVプレビュー（uv0）", "UV Preview (uv0)"), EditorStyles.boldLabel);
            Rect previewRect = ReservePreviewRect();
            HandleRectDrag(previewRect);
            DrawSeparationPreview(previewRect, mesh);
            DrawPreviewLegend(new[]
            {
                L("グレー: 全ポリゴン", "Gray: all polygons"),
                L("緑: 選択中（目）", "Green: selected (eye)"),
                selectionMode == EyeSelectionMode.UVRect ? L("オレンジ: UV範囲", "Orange: UV rect") : null
            });

            EditorGUILayout.Space(4);
            DrawSeparationSummary(mesh);
            EditorGUILayout.Space(4);

            bool canApply = CanApplySeparation(mesh, out string blockReason);
            if (!canApply && !string.IsNullOrEmpty(blockReason))
                EditorGUILayout.HelpBox(blockReason, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (NataneToonShaderGUIUtility.DrawPrimaryButton(
                        L("マテリアル分けを実行", "Apply Material Separation"), 220, 32))
                {
                    ApplySeparation(mesh);
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private string[] BuildSlotNames(Mesh mesh)
        {
            Material[] materials = targetRenderer != null ? targetRenderer.sharedMaterials : null;
            var names = new string[mesh.subMeshCount];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                string matName = (materials != null && i < materials.Length && materials[i] != null)
                    ? materials[i].name
                    : L("(マテリアルなし)", "(no material)");
                names[i] = string.Format("[{0}] {1}", i, matName);
            }
            return names;
        }

        private void EnsureSelection(Mesh mesh)
        {
            if (!selectionDirty && selectedTriangles != null)
                return;

            selectedTriangles = null;
            selectedTriangleCount = 0;

            switch (selectionMode)
            {
                case EyeSelectionMode.MaterialSlot:
                    selectedTriangles = EyeSetupMeshUtility.SelectByMaterialSlot(mesh, selectedSlot);
                    break;
                case EyeSelectionMode.UVRect:
                    selectedTriangles = EyeSetupMeshUtility.SelectByUVRect(mesh, GetUVRect());
                    break;
                case EyeSelectionMode.TextureMask:
                    Texture2D readable = GetReadableMask();
                    if (readable != null)
                        selectedTriangles = EyeSetupMeshUtility.SelectByTextureMask(mesh, readable, maskThreshold);
                    break;
            }

            selectedTriangleCount = EyeSetupMeshUtility.CountSelected(selectedTriangles);
            selectionDirty = false;
        }

        private Rect GetUVRect()
        {
            Vector2 min = Vector2.Min(uvRectMin, uvRectMax);
            Vector2 max = Vector2.Max(uvRectMin, uvRectMax);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void DrawSeparationSummary(Mesh mesh)
        {
            int total = EyeSetupMeshUtility.TotalTriangleCount(mesh);
            var lines = new List<string>();
            lines.Add(L("=== 適用内容の概要 ===", "=== Summary of Changes ==="));
            lines.Add(string.Format(
                L("選択中の三角形: {0} / {1}", "Selected triangles: {0} / {1}"),
                selectedTriangleCount, total));

            if (selectionMode == EyeSelectionMode.MaterialSlot)
            {
                lines.Add(L("メッシュ: 変更なし", "Mesh: unchanged"));
                lines.Add(string.Format(
                    L("マテリアル: スロット {0} に新規 \"Natane/Eye\" マテリアルを割り当て",
                      "Materials: assign a new \"Natane/Eye\" material to slot {0}"),
                    selectedSlot));
            }
            else
            {
                lines.Add(string.Format(
                    L("メッシュ: 新規アセットを保存（サブメッシュ数 {0} → {1}）",
                      "Mesh: new asset will be saved (submeshes {0} -> {1})"),
                    mesh.subMeshCount, mesh.subMeshCount + 1));
                lines.Add(createEyeMaterial
                    ? L("マテリアル: 新しいスロットに \"Natane/Eye\" マテリアルを新規作成して割り当て",
                        "Materials: create and assign a new \"Natane/Eye\" material on the new slot")
                    : L("マテリアル: 新しいスロットは空のまま（後から手動で割り当て）",
                        "Materials: the new slot is left empty (assign manually later)"));
                lines.Add(L("レンダラー: 新メッシュとマテリアル配列を割り当て（Undo可）",
                            "Renderer: new mesh and material array are assigned (undoable)"));
            }

            EditorGUILayout.HelpBox(string.Join("\n", lines.ToArray()), MessageType.None);
        }

        private bool CanApplySeparation(Mesh mesh, out string reason)
        {
            reason = null;
            if (mesh == null || targetRenderer == null)
            {
                reason = L("対象レンダラーとメッシュが必要です。", "A target renderer and mesh are required.");
                return false;
            }

            if (selectionMode == EyeSelectionMode.TextureMask && maskTexture == null)
            {
                reason = L("マスクテクスチャを設定してください。", "Set a mask texture.");
                return false;
            }

            if (selectionMode == EyeSelectionMode.UVRect || selectionMode == EyeSelectionMode.TextureMask)
            {
                Vector2[] uv = mesh.uv;
                if (uv == null || uv.Length == 0)
                {
                    reason = L("メッシュに uv0 がありません。UV範囲/マスク選択には uv0 が必要です。",
                               "The mesh has no uv0. UV rect / mask selection requires uv0.");
                    return false;
                }
            }

            if (selectedTriangleCount <= 0)
            {
                reason = L("三角形が選択されていません。選択条件を調整してください。",
                           "No triangles are selected. Adjust the selection settings.");
                return false;
            }

            int total = EyeSetupMeshUtility.TotalTriangleCount(mesh);
            if (selectionMode != EyeSelectionMode.MaterialSlot && selectedTriangleCount >= total)
            {
                reason = L("全ての三角形が選択されています。目の領域のみに絞り込んでください。",
                           "All triangles are selected. Narrow the selection down to the eye region.");
                return false;
            }

            return true;
        }

        private void ApplySeparation(Mesh mesh)
        {
            if (selectionMode == EyeSelectionMode.MaterialSlot)
            {
                ApplySlotMaterialAssignment(mesh);
                return;
            }

            Mesh newMesh = EyeSetupMeshUtility.ExtractSelectedTriangles(mesh, selectedTriangles);
            if (newMesh == null)
            {
                EditorUtility.DisplayDialog(
                    L("エラー", "Error"),
                    L("サブメッシュの分離に失敗しました。選択を確認してください。",
                      "Failed to extract the submesh. Check the selection."),
                    L("閉じる", "Close"));
                return;
            }

            string savePath = EditorUtility.SaveFilePanelInProject(
                L("分離メッシュの保存先", "Save Separated Mesh"),
                mesh.name + "_EyeSplit",
                "asset",
                L("分離した目サブメッシュを含む新規メッシュアセットの保存先を選択してください。",
                  "Choose where to save the new mesh asset containing the separated eye submesh."),
                GetAssetDirectory(mesh));

            if (string.IsNullOrEmpty(savePath))
            {
                DestroyImmediate(newMesh);
                return;
            }

            AssetDatabase.CreateAsset(newMesh, savePath);

            // Build the new material array (existing slots + eye slot at the end).
            Material[] oldMaterials = targetRenderer.sharedMaterials;
            var newMaterials = new Material[newMesh.subMeshCount];
            for (int i = 0; i < newMaterials.Length - 1; i++)
                newMaterials[i] = (oldMaterials != null && i < oldMaterials.Length) ? oldMaterials[i] : null;

            Material eyeMaterial = null;
            if (createEyeMaterial)
            {
                string materialPath = Path.ChangeExtension(savePath, null) + "_Eye.mat";
                eyeMaterial = CreateEyeMaterialAsset(GetPrimarySourceMaterial(mesh), materialPath);
            }
            newMaterials[newMaterials.Length - 1] = eyeMaterial;

            // Assign with Undo (renderer and mesh holder).
            AssignMeshWithUndo(newMesh);
            Undo.RecordObject(targetRenderer, "Eye Material Separation");
            targetRenderer.sharedMaterials = newMaterials;

            AssetDatabase.SaveAssets();

            relayoutSubmesh = newMesh.subMeshCount - 1;
            selectionDirty = true;
            remapDirty = true;
            previewMesh = null;
            beforeUVMesh = null;

            string message = string.Format(
                L("目のサブメッシュを分離しました。\n\n三角形数: {0}\nメッシュ: {1}\n{2}\n\n次は「UV再配置」タブで目のUVを整えられます。",
                  "Eye submesh separated.\n\nTriangles: {0}\nMesh: {1}\n{2}\n\nUse the \"UV Relayout\" tab next to arrange the eye UVs."),
                selectedTriangleCount,
                savePath,
                eyeMaterial != null
                    ? string.Format(L("マテリアル: {0}", "Material: {0}"), AssetDatabase.GetAssetPath(eyeMaterial))
                    : L("マテリアル: 未割り当て", "Material: not assigned"));

            EditorUtility.DisplayDialog(L("完了", "Complete"), message, L("閉じる", "Close"));
        }

        private void ApplySlotMaterialAssignment(Mesh mesh)
        {
            Material[] materials = targetRenderer.sharedMaterials;
            if (materials == null || selectedSlot >= materials.Length)
            {
                EditorUtility.DisplayDialog(
                    L("エラー", "Error"),
                    L("レンダラーのマテリアル配列に選択したスロットがありません。",
                      "The renderer's material array does not contain the selected slot."),
                    L("閉じる", "Close"));
                return;
            }

            string sourceName = materials[selectedSlot] != null ? materials[selectedSlot].name : mesh.name;
            string savePath = EditorUtility.SaveFilePanelInProject(
                L("目マテリアルの保存先", "Save Eye Material"),
                sourceName + "_Eye",
                "mat",
                L("新規 \"Natane/Eye\" マテリアルの保存先を選択してください。",
                  "Choose where to save the new \"Natane/Eye\" material."),
                GetAssetDirectory(mesh));

            if (string.IsNullOrEmpty(savePath))
                return;

            Material eyeMaterial = CreateEyeMaterialAsset(materials[selectedSlot], savePath);
            if (eyeMaterial == null)
                return;

            Undo.RecordObject(targetRenderer, "Assign Eye Material");
            materials[selectedSlot] = eyeMaterial;
            targetRenderer.sharedMaterials = materials;
            AssetDatabase.SaveAssets();

            relayoutSubmesh = Mathf.Clamp(selectedSlot, 0, mesh.subMeshCount - 1);
            remapDirty = true;

            EditorUtility.DisplayDialog(
                L("完了", "Complete"),
                string.Format(
                    L("スロット {0} に目マテリアルを割り当てました。\n\nマテリアル: {1}",
                      "Assigned the eye material to slot {0}.\n\nMaterial: {1}"),
                    selectedSlot, savePath),
                L("閉じる", "Close"));
        }

        /// <summary>Material of the submesh that contains the most selected triangles.</summary>
        private Material GetPrimarySourceMaterial(Mesh mesh)
        {
            Material[] materials = targetRenderer != null ? targetRenderer.sharedMaterials : null;
            if (materials == null || materials.Length == 0 || selectedTriangles == null)
                return null;

            var subTris = EyeSetupMeshUtility.GetSubmeshTriangles(mesh);
            int bestSlot = -1;
            int bestCount = 0;
            int cursor = 0;
            for (int s = 0; s < subTris.Count; s++)
            {
                int triCount = subTris[s].Length / 3;
                int count = 0;
                for (int t = 0; t < triCount; t++, cursor++)
                {
                    if (cursor < selectedTriangles.Length && selectedTriangles[cursor])
                        count++;
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestSlot = s;
                }
            }

            return (bestSlot >= 0 && bestSlot < materials.Length) ? materials[bestSlot] : null;
        }

        private Material CreateEyeMaterialAsset(Material sourceMaterial, string assetPath)
        {
            Shader eyeShader = Shader.Find("Natane/Eye");
            if (eyeShader == null)
            {
                EditorUtility.DisplayDialog(
                    L("エラー", "Error"),
                    L("シェーダー \"Natane/Eye\" が見つかりませんでした。パッケージが正しくインポートされているか確認してください。",
                      "Shader \"Natane/Eye\" was not found. Check that the package is imported correctly."),
                    L("閉じる", "Close"));
                return null;
            }

            var material = new Material(eyeShader);
            material.name = Path.GetFileNameWithoutExtension(assetPath);

            if (sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty("_MainTex"))
                {
                    Texture mainTex = sourceMaterial.GetTexture("_MainTex");
                    if (mainTex != null)
                    {
                        material.SetTexture("_MainTex", mainTex);
                        material.SetFloat("_UseTexture", 1f);
                        material.EnableKeyword("_USE_TEXTURE");
                    }
                }
                if (sourceMaterial.HasProperty("_Color"))
                    material.SetColor("_MainColor", sourceMaterial.GetColor("_Color"));
            }

            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            return material;
        }

        // =====================================================================
        // Tab 2: UV relayout
        // =====================================================================

        private void DrawRelayoutTab(Mesh mesh)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("UV再配置設定", "UV Relayout Settings"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            relayoutSubmesh = EditorGUILayout.Popup(
                L("対象サブメッシュ", "Target Submesh"),
                Mathf.Clamp(relayoutSubmesh, 0, mesh.subMeshCount - 1),
                BuildSlotNames(mesh));

            relayoutUVChannel = EditorGUILayout.Popup(
                L("書き込みUVチャンネル", "Target UV Channel"),
                relayoutUVChannel,
                new[] { "uv0", "uv1" });

            targetRect = EditorGUILayout.RectField(
                L("ターゲット矩形 (UV)", "Target Rect (UV)"), targetRect);
            relayoutPadding = EditorGUILayout.Slider(
                L("パディング", "Padding"), relayoutPadding, 0f, 0.2f);

            islandLayout = (EyeIslandLayout)EditorGUILayout.Popup(
                L("左右の目の配置", "Left/Right Eye Layout"),
                (int)islandLayout,
                new[]
                {
                    L("全体を1ブロックとして配置", "Treat all islands as one block"),
                    L("左右を重ねる（1枚のテクスチャを共有）", "Overlap left/right (share one texture area)"),
                    L("左右を並べる", "Place left/right side by side")
                });

            if (islandLayout != EyeIslandLayout.SingleBlock)
            {
                mirrorSecondGroup = EditorGUILayout.ToggleLeft(
                    L("片側の目をU方向に反転（ミラー）", "Mirror one eye horizontally (flip U)"),
                    mirrorSecondGroup);
            }

            preserveAspect = EditorGUILayout.ToggleLeft(
                L("アスペクト比を維持（等倍スケール）", "Preserve aspect ratio (uniform scale)"),
                preserveAspect);

            if (EditorGUI.EndChangeCheck())
                remapDirty = true;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            EnsureRemapPreview(mesh);

            EditorGUILayout.LabelField(L("UVプレビュー（変更前/変更後）", "UV Preview (Before / After)"), EditorStyles.boldLabel);
            Rect previewRect = ReservePreviewRect();
            DrawRelayoutPreview(previewRect, mesh);
            DrawPreviewLegend(new[]
            {
                L("グレー: 変更前のUV", "Gray: UVs before"),
                L("緑: 変更後のUV", "Green: UVs after"),
                L("水色: ターゲット矩形", "Cyan: target rect")
            });

            EditorGUILayout.Space(4);
            DrawRelayoutSummary(mesh);
            EditorGUILayout.Space(4);

            bool canApply = CanApplyRelayout(mesh, out string blockReason);
            if (!canApply && !string.IsNullOrEmpty(blockReason))
                EditorGUILayout.HelpBox(blockReason, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (NataneToonShaderGUIUtility.DrawPrimaryButton(
                        L("UV再配置を実行", "Apply UV Relayout"), 220, 32))
                {
                    ApplyRelayout(mesh);
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void EnsureRemapPreview(Mesh mesh)
        {
            if (!remapDirty && remapPreviewUVs != null)
                return;

            remapPreviewUVs = EyeSetupMeshUtility.ComputeRemappedUVs(
                mesh, relayoutSubmesh, relayoutUVChannel,
                targetRect, relayoutPadding,
                islandLayout, mirrorSecondGroup, preserveAspect,
                out remapReport);
            remapDirty = false;
        }

        private void DrawRelayoutSummary(Mesh mesh)
        {
            var lines = new List<string>();
            lines.Add(L("=== 適用内容の概要 ===", "=== Summary of Changes ==="));

            if (remapReport == null || remapPreviewUVs == null)
            {
                lines.Add(L("計算できませんでした。対象サブメッシュとUVを確認してください。",
                            "Could not compute the relayout. Check the target submesh and UVs."));
                EditorGUILayout.HelpBox(string.Join("\n", lines.ToArray()), MessageType.Warning);
                return;
            }

            lines.Add(string.Format(
                L("検出したアイランド数: {0}", "Detected islands: {0}"), remapReport.islandCount));
            if (remapReport.groupBVertexCount > 0)
            {
                lines.Add(string.Format(
                    L("左右グループの頂点数: {0} / {1}", "Left/right group vertices: {0} / {1}"),
                    remapReport.groupAVertexCount, remapReport.groupBVertexCount));
            }
            lines.Add(string.Format(
                L("再配置される頂点数: {0}（書き込み先: uv{1}）",
                  "Vertices to be remapped: {0} (written to uv{1})"),
                remapReport.remappedVertexCount, relayoutUVChannel));

            if (remapReport.fallbackToSingle)
                lines.Add(L("注意: アイランドが1つしか見つからないため、全体を1ブロックとして配置します。",
                            "Note: only one island was found; the whole submesh is placed as a single block."));
            if (remapReport.seededFromUV0)
                lines.Add(L("注意: 対象チャンネルにUVがないため、uv0を元に生成します。",
                            "Note: the target channel has no UVs; values are seeded from uv0."));
            if (remapReport.sharedVertexCount > 0)
                lines.Add(string.Format(
                    L("警告: 他のサブメッシュと共有している頂点が {0} 個あります。そのUVも移動します。",
                      "Warning: {0} vertices are shared with other submeshes; their UVs will move too."),
                    remapReport.sharedVertexCount));

            string meshPath = AssetDatabase.GetAssetPath(mesh);
            bool editable = !string.IsNullOrEmpty(meshPath) && meshPath.EndsWith(".asset");
            lines.Add(editable
                ? string.Format(L("メッシュ: {0} を直接更新", "Mesh: {0} is updated in place"), meshPath)
                : L("メッシュ: インポート品/シーン内メッシュのため、新規アセットとして複製してから書き込みます。",
                    "Mesh: imported/scene mesh; it will be duplicated to a new asset before writing."));

            EditorGUILayout.HelpBox(string.Join("\n", lines.ToArray()), MessageType.None);
        }

        private bool CanApplyRelayout(Mesh mesh, out string reason)
        {
            reason = null;
            if (mesh == null || targetRenderer == null)
            {
                reason = L("対象レンダラーとメッシュが必要です。", "A target renderer and mesh are required.");
                return false;
            }
            if (targetRect.width <= 0f || targetRect.height <= 0f)
            {
                reason = L("ターゲット矩形の幅と高さは正の値にしてください。",
                           "The target rect width and height must be positive.");
                return false;
            }
            if (remapPreviewUVs == null)
            {
                reason = L("UV再配置を計算できませんでした。対象サブメッシュにポリゴンとUVがあるか確認してください。",
                           "Could not compute the UV relayout. Check that the target submesh has polygons and UVs.");
                return false;
            }
            return true;
        }

        private void ApplyRelayout(Mesh mesh)
        {
            EnsureRemapPreview(mesh);
            if (remapPreviewUVs == null)
                return;

            string meshPath = AssetDatabase.GetAssetPath(mesh);
            bool editableAsset = !string.IsNullOrEmpty(meshPath) && meshPath.EndsWith(".asset");
            Mesh outputMesh = mesh;
            string savedPath = meshPath;

            if (!editableAsset)
            {
                string savePath = EditorUtility.SaveFilePanelInProject(
                    L("UV再配置メッシュの保存先", "Save UV Relayout Mesh"),
                    mesh.name + "_EyeUV",
                    "asset",
                    L("元のメッシュは変更できないため、新規メッシュアセットとして保存します。",
                      "The original mesh cannot be modified; it will be saved as a new mesh asset."),
                    GetAssetDirectory(mesh));

                if (string.IsNullOrEmpty(savePath))
                    return;

                outputMesh = Instantiate(mesh);
                outputMesh.name = Path.GetFileNameWithoutExtension(savePath);
                AssetDatabase.CreateAsset(outputMesh, savePath);
                savedPath = savePath;

                AssignMeshWithUndo(outputMesh);
            }
            else
            {
                Undo.RecordObject(outputMesh, "Eye UV Relayout");
            }

            EyeSetupMeshUtility.ApplyUVs(outputMesh, relayoutUVChannel, remapPreviewUVs);
            EditorUtility.SetDirty(outputMesh);
            AssetDatabase.SaveAssets();

            remapDirty = true;
            previewMesh = null;
            beforeUVMesh = null;

            EditorUtility.DisplayDialog(
                L("完了", "Complete"),
                string.Format(
                    L("UV再配置を適用しました。\n\n対象: サブメッシュ {0}（uv{1}）\n頂点数: {2}\nメッシュ: {3}",
                      "UV relayout applied.\n\nTarget: submesh {0} (uv{1})\nVertices: {2}\nMesh: {3}"),
                    relayoutSubmesh, relayoutUVChannel,
                    remapReport != null ? remapReport.remappedVertexCount : 0,
                    savedPath),
                L("閉じる", "Close"));
        }

        // =====================================================================
        // Shared apply helpers
        // =====================================================================

        private void AssignMeshWithUndo(Mesh newMesh)
        {
            var smr = targetRenderer as SkinnedMeshRenderer;
            if (smr != null)
            {
                Undo.RecordObject(smr, "Assign Eye Setup Mesh");
                smr.sharedMesh = newMesh;
                return;
            }

            var mf = targetRenderer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Undo.RecordObject(mf, "Assign Eye Setup Mesh");
                mf.sharedMesh = newMesh;
            }
        }

        private static string GetAssetDirectory(Object asset)
        {
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            if (string.IsNullOrEmpty(path))
                return "Assets";

            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !dir.Replace('\\', '/').StartsWith("Assets"))
                return "Assets";
            return dir.Replace('\\', '/');
        }

        private Texture2D GetReadableMask()
        {
            if (maskTexture == null)
            {
                ReleaseMaskCache();
                return null;
            }

            if (cachedMaskSource != maskTexture || cachedMaskReadable == null)
            {
                ReleaseMaskCache();
                cachedMaskReadable = EyeSetupMeshUtility.CreateReadableCopy(maskTexture);
                cachedMaskSource = maskTexture;
            }
            return cachedMaskReadable;
        }

        private void ReleaseMaskCache()
        {
            if (cachedMaskReadable != null)
                DestroyImmediate(cachedMaskReadable);
            cachedMaskReadable = null;
            cachedMaskSource = null;
        }

        // =====================================================================
        // UV preview drawing
        // =====================================================================

        private Rect ReservePreviewRect()
        {
            float size = Mathf.Min(position.width - 40f, PreviewMaxSize);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return rect;
        }

        private void DrawPreviewLegend(string[] entries)
        {
            var parts = new List<string>();
            foreach (string entry in entries)
            {
                if (!string.IsNullOrEmpty(entry))
                    parts.Add(entry);
            }
            EditorGUILayout.LabelField(string.Join("   /   ", parts.ToArray()), EditorStyles.miniLabel);
        }

        private void EnsurePreviewCache(Mesh mesh)
        {
            if (previewMesh == mesh && previewSubTriangles != null)
                return;

            previewMesh = mesh;
            previewUV0 = mesh != null ? mesh.uv : null;
            previewSubTriangles = mesh != null ? EyeSetupMeshUtility.GetSubmeshTriangles(mesh) : null;
        }

        private Vector2[] GetBeforeUVs(Mesh mesh, int channel)
        {
            if (beforeUVMesh == mesh && beforeUVChannel == channel && beforeUVs != null)
                return beforeUVs;

            var list = new List<Vector2>();
            mesh.GetUVs(channel, list);
            if (list.Count != mesh.vertexCount)
                mesh.GetUVs(0, list);

            beforeUVs = list.Count == mesh.vertexCount ? list.ToArray() : null;
            beforeUVMesh = mesh;
            beforeUVChannel = channel;
            return beforeUVs;
        }

        private void HandleRectDrag(Rect previewRect)
        {
            if (selectionMode != EyeSelectionMode.UVRect || activeTab != 0)
                return;

            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && previewRect.Contains(e.mousePosition))
                    {
                        draggingRect = true;
                        dragStartUV = GUIToUV(previewRect, e.mousePosition);
                        uvRectMin = dragStartUV;
                        uvRectMax = dragStartUV;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (draggingRect)
                    {
                        Vector2 current = GUIToUV(previewRect, e.mousePosition);
                        uvRectMin = Vector2.Min(dragStartUV, current);
                        uvRectMax = Vector2.Max(dragStartUV, current);
                        selectionDirty = true;
                        Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (draggingRect)
                    {
                        draggingRect = false;
                        selectionDirty = true;
                        Repaint();
                        e.Use();
                    }
                    break;
            }
        }

        private static Vector2 GUIToUV(Rect rect, Vector2 guiPosition)
        {
            float u = Mathf.Clamp01((guiPosition.x - rect.x) / Mathf.Max(1f, rect.width));
            float v = Mathf.Clamp01(1f - (guiPosition.y - rect.y) / Mathf.Max(1f, rect.height));
            return new Vector2(u, v);
        }

        private void DrawSeparationPreview(Rect rect, Mesh mesh)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EnsurePreviewCache(mesh);
            DrawPreviewBackground(rect, GetPreviewTexture(mesh));

            if (previewUV0 == null || previewUV0.Length == 0)
            {
                DrawCenteredPreviewLabel(rect, L("uv0 がありません", "No uv0 data"));
                return;
            }

            EnsureLineMaterial();
            if (lineMaterial == null)
                return;

            GUI.BeginClip(rect);
            GL.PushMatrix();
            lineMaterial.SetPass(0);
            GL.Begin(GL.LINES);

            var gray = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            var green = new Color(0.25f, 1f, 0.35f, 0.95f);

            int totalTris = 0;
            foreach (int[] tris in previewSubTriangles)
                totalTris += tris.Length / 3;
            int step = Mathf.Max(1, totalTris / MaxPreviewTriangles);

            int cursor = 0;
            for (int s = 0; s < previewSubTriangles.Count; s++)
            {
                int[] tris = previewSubTriangles[s];
                for (int t = 0; t < tris.Length; t += 3, cursor++)
                {
                    bool isSelected = selectedTriangles != null &&
                                      cursor < selectedTriangles.Length &&
                                      selectedTriangles[cursor];
                    if (!isSelected && (cursor % step) != 0)
                        continue;

                    GL.Color(isSelected ? green : gray);
                    DrawTriangleEdges(rect, previewUV0, tris[t], tris[t + 1], tris[t + 2]);
                }
            }

            // UV rect overlay
            if (selectionMode == EyeSelectionMode.UVRect)
            {
                GL.Color(new Color(1f, 0.6f, 0.1f, 1f));
                DrawUVRectOutline(rect, GetUVRect());
            }

            GL.End();
            GL.PopMatrix();
            GUI.EndClip();
        }

        private void DrawRelayoutPreview(Rect rect, Mesh mesh)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EnsurePreviewCache(mesh);
            DrawPreviewBackground(rect, GetPreviewTexture(mesh));

            if (previewSubTriangles == null ||
                relayoutSubmesh < 0 || relayoutSubmesh >= previewSubTriangles.Count)
            {
                DrawCenteredPreviewLabel(rect, L("サブメッシュがありません", "No submesh"));
                return;
            }

            int[] tris = previewSubTriangles[relayoutSubmesh];
            Vector2[] before = GetBeforeUVs(mesh, relayoutUVChannel);

            EnsureLineMaterial();
            if (lineMaterial == null)
                return;

            GUI.BeginClip(rect);
            GL.PushMatrix();
            lineMaterial.SetPass(0);
            GL.Begin(GL.LINES);

            int triCount = tris.Length / 3;
            int step = Mathf.Max(1, triCount / MaxPreviewTriangles);

            if (before != null)
            {
                GL.Color(new Color(0.55f, 0.55f, 0.55f, 0.55f));
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (((t / 3) % step) != 0)
                        continue;
                    DrawTriangleEdges(rect, before, tris[t], tris[t + 1], tris[t + 2]);
                }
            }

            if (remapPreviewUVs != null)
            {
                GL.Color(new Color(0.25f, 1f, 0.35f, 0.95f));
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (((t / 3) % step) != 0)
                        continue;
                    DrawTriangleEdges(rect, remapPreviewUVs, tris[t], tris[t + 1], tris[t + 2]);
                }
            }

            GL.Color(new Color(0.3f, 0.9f, 1f, 1f));
            DrawUVRectOutline(rect, targetRect);

            GL.End();
            GL.PopMatrix();
            GUI.EndClip();
        }

        private void DrawPreviewBackground(Rect rect, Texture texture)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            if (texture != null)
            {
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
                GUI.color = prev;
            }

            // 0..1 border and center guides
            var guide = new Color(1f, 1f, 1f, 0.12f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * 0.5f, rect.width, 1f), guide);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * 0.5f, rect.y, 1f, rect.height), guide);
            var border = new Color(1f, 1f, 1f, 0.25f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        private static void DrawCenteredPreviewLabel(Rect rect, string text)
        {
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            GUI.Label(rect, text, style);
        }

        private Texture GetPreviewTexture(Mesh mesh)
        {
            if (targetRenderer == null)
                return null;

            Material[] materials = targetRenderer.sharedMaterials;
            if (materials == null)
                return null;

            // Prefer the material of the slot in focus, then any main texture.
            int focusSlot = (activeTab == 0 && selectionMode == EyeSelectionMode.MaterialSlot)
                ? selectedSlot
                : (activeTab == 1 ? relayoutSubmesh : -1);

            if (focusSlot >= 0 && focusSlot < materials.Length && materials[focusSlot] != null &&
                materials[focusSlot].HasProperty("_MainTex") && materials[focusSlot].mainTexture != null)
                return materials[focusSlot].mainTexture;

            foreach (Material material in materials)
            {
                if (material != null && material.HasProperty("_MainTex") && material.mainTexture != null)
                    return material.mainTexture;
            }
            return null;
        }

        private void EnsureLineMaterial()
        {
            if (lineMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return;

            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        /// <summary>Convert a UV coordinate to local (clip-space) pixel position of the preview.</summary>
        private static Vector2 UVToLocal(Rect rect, Vector2 uv)
        {
            return new Vector2(uv.x * rect.width, (1f - uv.y) * rect.height);
        }

        private static void DrawTriangleEdges(Rect rect, Vector2[] uv, int i0, int i1, int i2)
        {
            if (i0 >= uv.Length || i1 >= uv.Length || i2 >= uv.Length)
                return;

            Vector2 a = UVToLocal(rect, uv[i0]);
            Vector2 b = UVToLocal(rect, uv[i1]);
            Vector2 c = UVToLocal(rect, uv[i2]);

            GL.Vertex3(a.x, a.y, 0f); GL.Vertex3(b.x, b.y, 0f);
            GL.Vertex3(b.x, b.y, 0f); GL.Vertex3(c.x, c.y, 0f);
            GL.Vertex3(c.x, c.y, 0f); GL.Vertex3(a.x, a.y, 0f);
        }

        private static void DrawUVRectOutline(Rect rect, Rect uvRect)
        {
            Vector2 p0 = UVToLocal(rect, new Vector2(uvRect.xMin, uvRect.yMin));
            Vector2 p1 = UVToLocal(rect, new Vector2(uvRect.xMax, uvRect.yMin));
            Vector2 p2 = UVToLocal(rect, new Vector2(uvRect.xMax, uvRect.yMax));
            Vector2 p3 = UVToLocal(rect, new Vector2(uvRect.xMin, uvRect.yMax));

            GL.Vertex3(p0.x, p0.y, 0f); GL.Vertex3(p1.x, p1.y, 0f);
            GL.Vertex3(p1.x, p1.y, 0f); GL.Vertex3(p2.x, p2.y, 0f);
            GL.Vertex3(p2.x, p2.y, 0f); GL.Vertex3(p3.x, p3.y, 0f);
            GL.Vertex3(p3.x, p3.y, 0f); GL.Vertex3(p0.x, p0.y, 0f);
        }
    }
}
