using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Blender アドオン (Blender~/natane_toon_preview) の「Unity 用に書き出し」で作った
    /// *.natanetoon.json を読み、Natane Toon のマテリアルをパラメーターごと作ってモデルに割り当てる。
    /// </summary>
    public static class NataneBlenderImporter
    {
        public const string Extension = ".natanetoon.json";
        public const string FormatName = "natane-toon-material-set";
        const string AutoImportPref = "NataneToon.BlenderImporter.AutoImport";
        const string DefaultShaderName = "Natane/Toon Shader";

        // ---- JSON (JsonUtility で読めるよう、辞書は使わず配列だけ) ----
        [Serializable] public sealed class FloatEntry { public string name; public float value; }
        [Serializable] public sealed class ColorEntry { public string name; public float r, g, b, a = 1f; }
        [Serializable]
        public sealed class TextureEntry
        {
            public string name;
            public string path;
            public string guid;
            public float scaleX = 1f, scaleY = 1f, offsetX, offsetY;
        }
        [Serializable]
        public sealed class MaterialEntry
        {
            public string name;
            public string shader;
            public string shaderGuid;
            public string sourceMaterialGuid;
            public int renderQueue = -1;
            public string[] keywords = new string[0];
            public FloatEntry[] floats = new FloatEntry[0];
            public ColorEntry[] colors = new ColorEntry[0];
            public TextureEntry[] textures = new TextureEntry[0];
        }
        [Serializable]
        public sealed class MaterialSet
        {
            public string format;
            public int version;
            public string generator;
            public string model;
            public MaterialEntry[] materials = new MaterialEntry[0];
        }

        public sealed class Result
        {
            public int created, updated, texturesMissing, propertiesSkipped;
            public bool modelRemapped;
            public string modelPath;
            public readonly List<string> messages = new List<string>();
            public readonly List<Material> materials = new List<Material>();
        }

        public static bool AutoImport
        {
            get { return EditorPrefs.GetBool(AutoImportPref, true); }
            set { EditorPrefs.SetBool(AutoImportPref, value); }
        }

        static string L(string ja, string en) { return NataneToonLocalization.L(ja, en); }

        // ------------------------------------------------------------------
        // メニュー
        // ------------------------------------------------------------------
        [MenuItem(NataneToolMenuPaths.BlenderImport, false, 2301)]
        static void ImportFromMenu()
        {
            string file = EditorUtility.OpenFilePanel(L("Blender から書き出した .natanetoon.json を選択", "Select the .natanetoon.json exported from Blender"), "", "json");
            if (string.IsNullOrEmpty(file)) return;

            string assetPath = ToAssetPath(file);
            if (assetPath == null)
            {
                // プロジェクトの外にある場合は、モデルとテクスチャごと Assets 内へコピーする。
                assetPath = CopyIntoProject(file);
                if (assetPath == null) return;
            }

            Result result = Import(assetPath, true);
            if (result == null) return;
            ShowResult(result);
        }

        [MenuItem(NataneToolMenuPaths.BlenderAutoImport, false, 2302)]
        static void ToggleAutoImport() { AutoImport = !AutoImport; }

        [MenuItem(NataneToolMenuPaths.BlenderAutoImport, true)]
        static bool ToggleAutoImportValidate()
        {
            Menu.SetChecked(NataneToolMenuPaths.BlenderAutoImport, AutoImport);
            return true;
        }

        static void ShowResult(Result result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(L("マテリアル: 新規 " + result.created + " / 更新 " + result.updated,
                            "Materials: " + result.created + " created / " + result.updated + " updated"));
            if (!string.IsNullOrEmpty(result.modelPath))
                sb.AppendLine(result.modelRemapped
                    ? L("モデルに割り当てました: " + result.modelPath, "Assigned to the model: " + result.modelPath)
                    : L("モデルへの割り当てはできませんでした: " + result.modelPath, "Could not assign to the model: " + result.modelPath));
            if (result.texturesMissing > 0)
                sb.AppendLine(L("見つからなかったテクスチャ: " + result.texturesMissing, "Textures not found: " + result.texturesMissing));
            if (result.propertiesSkipped > 0)
                sb.AppendLine(L("シェーダーに無くて飛ばしたプロパティ: " + result.propertiesSkipped, "Properties skipped (not in the shader): " + result.propertiesSkipped));
            if (result.messages.Count > 0)
                sb.AppendLine(L("詳細は Console を見てください。", "See the Console for details."));
            EditorUtility.DisplayDialog("Natane Toon - Blender", sb.ToString(), "OK");
            if (result.materials.Count > 0) EditorGUIUtility.PingObject(result.materials[0]);
        }

        // ------------------------------------------------------------------
        // 本体
        // ------------------------------------------------------------------
        /// <summary>プロジェクト内の .natanetoon.json を読み込んでマテリアルを作る。失敗したら null。</summary>
        public static Result Import(string jsonAssetPath, bool interactive)
        {
            MaterialSet set;
            try
            {
                set = JsonUtility.FromJson<MaterialSet>(File.ReadAllText(jsonAssetPath, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Report(interactive, L("JSON を読めませんでした: ", "Could not read the JSON: ") + jsonAssetPath + "\n" + e.Message);
                return null;
            }
            if (set == null || set.format != FormatName || set.materials == null)
            {
                Report(interactive, L("Natane Toon の書き出しファイルではありません: ", "Not a Natane Toon export file: ") + jsonAssetPath);
                return null;
            }

            var result = new Result();
            if (set.version > 1)
                result.messages.Add(L("新しい形式 (version " + set.version + ") です。読めない項目は無視します。",
                                      "Newer format (version " + set.version + "); unknown fields are ignored."));

            string dir = Path.GetDirectoryName(jsonAssetPath).Replace('\\', '/');
            string materialDir = dir + "/Materials";

            var byName = new Dictionary<string, Material>();
            foreach (MaterialEntry entry in set.materials)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name)) continue;
                Material material = ImportMaterial(entry, dir, materialDir, result);
                if (material != null)
                {
                    byName[entry.name] = material;
                    result.materials.Add(material);
                }
            }
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(set.model))
            {
                result.modelPath = dir + "/" + set.model.Replace('\\', '/');
                result.modelRemapped = RemapModel(result.modelPath, byName, result);
            }

            foreach (string message in result.messages) Debug.LogWarning("[Natane Toon] " + message);
            return result;
        }

        static void Report(bool interactive, string message)
        {
            Debug.LogError("[Natane Toon] " + message);
            if (interactive) EditorUtility.DisplayDialog("Natane Toon - Blender", message, "OK");
        }

        static Shader ResolveShader(MaterialEntry entry, Result result)
        {
            if (!string.IsNullOrEmpty(entry.shaderGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.shaderGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    var byGuid = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (byGuid != null) return byGuid;
                }
            }
            if (!string.IsNullOrEmpty(entry.shader))
            {
                Shader byName = Shader.Find(entry.shader);
                if (byName != null) return byName;
            }
            result.messages.Add(L("シェーダーが見つからないため標準の Natane/Toon Shader を使います: ", "Shader not found, using Natane/Toon Shader: ")
                + entry.name + " (" + entry.shader + ")");
            return Shader.Find(DefaultShaderName);
        }

        static Material ImportMaterial(MaterialEntry entry, string dir, string materialDir, Result result)
        {
            Shader shader = ResolveShader(entry, result);
            if (shader == null)
            {
                result.messages.Add(L("シェーダーが無いので作れませんでした: ", "No shader, skipped: ") + entry.name);
                return null;
            }

            string path = materialDir + "/" + SafeFileName(entry.name) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(materialDir))
                {
                    Directory.CreateDirectory(materialDir);
                    AssetDatabase.ImportAsset(materialDir);
                }
                material = new Material(shader) { name = entry.name };
                AssetDatabase.CreateAsset(material, path);
                Undo.RegisterCreatedObjectUndo(material, "Import Natane Toon Material");
                result.created++;
            }
            else
            {
                Undo.RecordObject(material, "Import Natane Toon Material");
                if (material.shader != shader) material.shader = shader;
                result.updated++;
            }

            foreach (FloatEntry f in entry.floats ?? new FloatEntry[0])
            {
                if (f == null || string.IsNullOrEmpty(f.name)) continue;
                if (material.HasProperty(f.name)) material.SetFloat(f.name, f.value);
                else Skip(result, entry, f.name);
            }

            foreach (ColorEntry c in entry.colors ?? new ColorEntry[0])
            {
                if (c == null || string.IsNullOrEmpty(c.name)) continue;
                if (material.HasProperty(c.name)) material.SetColor(c.name, new Color(c.r, c.g, c.b, c.a));
                else Skip(result, entry, c.name);
            }

            foreach (TextureEntry t in entry.textures ?? new TextureEntry[0])
            {
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                if (!material.HasProperty(t.name)) { Skip(result, entry, t.name); continue; }

                Texture texture = ResolveTexture(t, dir);
                bool wanted = !string.IsNullOrEmpty(t.path) || !string.IsNullOrEmpty(t.guid);
                if (texture == null && wanted)
                {
                    result.texturesMissing++;
                    result.messages.Add(L("テクスチャが見つかりません: ", "Texture not found: ") + entry.name + " / " + t.name + " (" + t.path + ")");
                }
                // 書き出し側で「テクスチャなし」とされた欄は空に戻す。見つからなかっただけの欄は今の設定を残す。
                if (texture != null || !wanted) material.SetTexture(t.name, texture);
                material.SetTextureScale(t.name, new Vector2(t.scaleX == 0f ? 1f : t.scaleX, t.scaleY == 0f ? 1f : t.scaleY));
                material.SetTextureOffset(t.name, new Vector2(t.offsetX, t.offsetY));
            }

            material.shaderKeywords = entry.keywords ?? new string[0];
            material.renderQueue = entry.renderQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Skip(Result result, MaterialEntry entry, string property)
        {
            result.propertiesSkipped++;
            if (result.propertiesSkipped <= 20)
                result.messages.Add(L("シェーダーに無いプロパティ: ", "Property not in the shader: ") + entry.name + " / " + property);
        }

        static Texture ResolveTexture(TextureEntry t, string dir)
        {
            // 元のテクスチャがプロジェクトにあればそれを使う (Unity から読み込んで Blender で調整した場合)。
            if (!string.IsNullOrEmpty(t.guid))
            {
                string byGuid = AssetDatabase.GUIDToAssetPath(t.guid);
                if (!string.IsNullOrEmpty(byGuid))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(byGuid);
                    if (texture != null) return texture;
                }
            }
            if (string.IsNullOrEmpty(t.path)) return null;

            string path = (dir + "/" + t.path.Replace('\\', '/')).Replace("/./", "/");
            if (!File.Exists(path)) return null;
            if (AssetDatabase.LoadAssetAtPath<Texture>(path) == null) AssetDatabase.ImportAsset(path);

            // 法線マップは取り込み設定を合わせる。
            if (IsNormalMapProperty(t.name))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        static bool IsNormalMapProperty(string name)
        {
            return name.IndexOf("Bump", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("NormalMap", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>モデル (FBX) のマテリアルを、作ったマテリアルに差し替える (Materials タブの Remap と同じ)。</summary>
        static bool RemapModel(string modelPath, Dictionary<string, Material> byName, Result result)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                result.messages.Add(L("モデルが見つかりません: ", "Model not found: ") + modelPath);
                return false;
            }

            bool changed = false;
            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> current = importer.GetExternalObjectMap();
            foreach (KeyValuePair<string, Material> pair in byName)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (current.TryGetValue(id, out UnityEngine.Object existing) && existing == pair.Value) continue;
                importer.AddRemap(id, pair.Value);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            return true;
        }

        static string SafeFileName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name) sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        static string ToAssetPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            if (absolute.StartsWith(data + "/", StringComparison.OrdinalIgnoreCase)) return "Assets" + absolute.Substring(data.Length);
            return null;
        }

        /// <summary>プロジェクト外の書き出し一式 (json・モデル・テクスチャ) を Assets 内へコピーする。</summary>
        static string CopyIntoProject(string jsonFile)
        {
            MaterialSet set;
            try { set = JsonUtility.FromJson<MaterialSet>(File.ReadAllText(jsonFile, Encoding.UTF8)); }
            catch (Exception e)
            {
                Report(true, L("JSON を読めませんでした: ", "Could not read the JSON: ") + e.Message);
                return null;
            }
            if (set == null || set.format != FormatName)
            {
                Report(true, L("Natane Toon の書き出しファイルではありません。", "Not a Natane Toon export file."));
                return null;
            }

            string sourceDir = Path.GetDirectoryName(jsonFile);
            string baseName = Path.GetFileName(jsonFile);
            baseName = baseName.Substring(0, baseName.Length - (baseName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? Extension.Length : ".json".Length));
            string target = EditorUtility.SaveFolderPanel(L("コピー先のフォルダー (Assets の中) を選択", "Choose a destination folder inside Assets"), Application.dataPath, baseName);
            if (string.IsNullOrEmpty(target)) return null;
            string targetAsset = ToAssetPath(target + "/x");
            if (targetAsset == null)
            {
                Report(true, L("コピー先はこのプロジェクトの Assets フォルダーの中にしてください。", "The destination must be inside this project's Assets folder."));
                return null;
            }

            var files = new List<string> { Path.GetFileName(jsonFile) };
            if (!string.IsNullOrEmpty(set.model)) files.Add(set.model);
            foreach (MaterialEntry m in set.materials ?? new MaterialEntry[0])
                foreach (TextureEntry t in (m != null ? m.textures : null) ?? new TextureEntry[0])
                    if (t != null && !string.IsNullOrEmpty(t.path) && !files.Contains(t.path)) files.Add(t.path);

            s_SuppressAuto = true;
            try
            {
                foreach (string relative in files)
                {
                    // 書き出しフォルダーの外を指すパスはコピーしない。
                    if (Path.IsPathRooted(relative) || relative.Contains("..")) continue;
                    string from = Path.Combine(sourceDir, relative);
                    if (!File.Exists(from)) continue;
                    string to = Path.Combine(target, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(from, to, true);
                }
                AssetDatabase.Refresh();
            }
            finally
            {
                s_SuppressAuto = false;
            }
            return ToAssetPath(target + "/" + Path.GetFileName(jsonFile));
        }

        static bool s_SuppressAuto;

        /// <summary>プロジェクトに .natanetoon.json が入ったら (または上書きされたら) 自動で反映する。</summary>
        sealed class Postprocessor : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (s_SuppressAuto || !AutoImport) return;
                foreach (string path in imported)
                {
                    if (!path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) continue;
                    string captured = path;
                    // インポート処理の最中にアセットを作れないので、終わってから実行する。
                    EditorApplication.delayCall += () =>
                    {
                        if (!File.Exists(captured)) return;
                        Result result = Import(captured, false);
                        if (result != null)
                            Debug.Log("[Natane Toon] " + L("Blender の書き出しを反映しました: ", "Applied the Blender export: ") + captured +
                                      " (" + result.created + " new / " + result.updated + " updated)");
                    };
                }
            }
        }
    }
}
