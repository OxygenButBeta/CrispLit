using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Crisp.Rendering.Editor
{
    public static class CrispMaterialConverter
    {
        const string UrpLitName = "Universal Render Pipeline/Lit";
        const string CrispLitName = "Crisp/Lit";

        [MenuItem("Tools/Crisp/Convert Selected Materials")]
        static void ConvertSelected()
        {
            var materials = Selection.GetFiltered<Material>(SelectionMode.Assets);
            Run(materials, "selected");
        }

        [MenuItem("Tools/Crisp/Convert Selected Materials", true)]
        static bool ConvertSelectedValidate() => Selection.GetFiltered<Material>(SelectionMode.Assets).Length > 0;

        [MenuItem("Tools/Crisp/Convert Scene Materials")]
        static void ConvertScene()
        {
            var found = new HashSet<Material>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && mat.shader.name == UrpLitName)
                        found.Add(mat);
                }
            }

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Crisp Converter", "No URP Lit materials found in the open scene.", "OK");
                return;
            }

            string list = string.Join("\n", found.Take(15).Select(m => "• " + m.name));
            if (found.Count > 15) list += $"\n… (+{found.Count - 15})";
            if (!EditorUtility.DisplayDialog("Crisp Converter",
                    $"Found {found.Count} URP Lit material(s) in this scene:\n\n{list}\n\nThey are converted as assets, so every user of them in the project is affected. Continue?",
                    "Convert", "Cancel"))
                return;

            Run(found, "scene");
        }

        static void Run(IEnumerable<Material> materials, string label)
        {
            int converted = 0, skipped = 0, packed = 0;
            var log = new System.Text.StringBuilder();

            foreach (var mat in materials)
            {
                string reason;
                var result = Convert(mat, out reason);
                if (result == ConvertResult.Converted) converted++;
                else if (result == ConvertResult.ConvertedWithMask) { converted++; packed++; }
                else { skipped++; log.AppendLine($"  skipped: {mat.name} ({reason})"); }
            }

            string summary = $"Crisp Converter: converted {converted} {label} material(s) ({packed} mask map(s) packed), skipped {skipped}.";
            Debug.Log(summary + (log.Length > 0 ? "\n" + log : ""));
            EditorUtility.DisplayDialog("Crisp Converter", summary, "OK");
        }

        enum ConvertResult { Converted, ConvertedWithMask, Skipped }

        static ConvertResult Convert(Material mat, out string reason)
        {
            reason = null;
            if (mat.shader == null) { reason = "no shader"; return ConvertResult.Skipped; }
            if (mat.shader.name == CrispLitName) { reason = "already Crisp/Lit"; return ConvertResult.Skipped; }
            if (mat.shader.name != UrpLitName) { reason = "not URP Lit: " + mat.shader.name; return ConvertResult.Skipped; }
            if (mat.HasProperty("_WorkflowMode") && mat.GetFloat("_WorkflowMode") < 0.5f)
            {
                reason = "specular workflow is not supported";
                return ConvertResult.Skipped;
            }

            var crisp = Shader.Find(CrispLitName);
            if (crisp == null) { reason = "Crisp/Lit shader not found"; return ConvertResult.Skipped; }

            var msMap = mat.GetTexture("_MetallicGlossMap") as Texture2D;
            var aoMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") as Texture2D : null;
            var albedo = mat.GetTexture("_BaseMap") as Texture2D;
            bool smoothFromAlbedo = mat.HasProperty("_SmoothnessTextureChannel") && mat.GetFloat("_SmoothnessTextureChannel") > 0.5f;
            bool hasDetail = mat.GetTexture("_DetailAlbedoMap") != null || mat.GetTexture("_DetailNormalMap") != null;
            if (hasDetail)
                Debug.LogWarning($"Crisp Converter: '{mat.name}' uses detail maps, which Crisp/Lit does not support - they were dropped.", mat);

            Undo.RecordObject(mat, "Convert to Crisp Lit");

            // Identically named properties (_BaseMap/_BaseColor/_BumpMap/_Smoothness/_Surface/...) carry
            // over automatically from their serialised values when the shader is swapped.
            mat.shader = crisp;

            foreach (var stale in new[]
                     {
                         "_METALLICSPECGLOSSMAP", "_METALLICGLOSSMAP", "_SPECGLOSSMAP", "_OCCLUSIONMAP",
                         "_SPECULAR_SETUP", "_PARALLAXMAP", "_DETAIL_MULX2", "_DETAIL_SCALED",
                         "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                     })
                mat.DisableKeyword(stale);

            bool needsMask = msMap != null || aoMap != null || (smoothFromAlbedo && albedo != null);
            if (needsMask)
            {
                var packedTex = PackMaskMap(mat, msMap, aoMap, smoothFromAlbedo ? albedo : null);
                if (packedTex != null)
                {
                    mat.SetTexture("_MaskMap", packedTex);
                    // URP ignores the _Metallic slider when a map is present; Crisp multiplies mask.r by it.
                    if (msMap != null) mat.SetFloat("_Metallic", 1f);
                    if (smoothFromAlbedo) mat.SetFloat("_Smoothness", mat.GetFloat("_Smoothness"));
                }
            }

            CrispMaterialValidator.Validate(mat);
            EditorUtility.SetDirty(mat);
            return needsMask ? ConvertResult.ConvertedWithMask : ConvertResult.Converted;
        }

        static Texture2D PackMaskMap(Material sourceMat, Texture2D msMap, Texture2D aoMap, Texture2D albedoForSmoothness)
        {
            var packer = Shader.Find("Hidden/Crisp/MaskMapPacker");
            if (packer == null) { Debug.LogError("Crisp Converter: MaskMapPacker shader not found."); return null; }

            int width = Mathf.Max(msMap != null ? msMap.width : 0, aoMap != null ? aoMap.width : 0,
                                  albedoForSmoothness != null ? albedoForSmoothness.width : 0, 4);
            int height = Mathf.Max(msMap != null ? msMap.height : 0, aoMap != null ? aoMap.height : 0,
                                   albedoForSmoothness != null ? albedoForSmoothness.height : 0, 4);

            var blitMat = new Material(packer);
            blitMat.SetTexture("_MSTex", msMap != null ? (Texture)msMap : Texture2D.whiteTexture);
            blitMat.SetTexture("_AOTex", aoMap != null ? (Texture)aoMap : Texture2D.whiteTexture);
            blitMat.SetTexture("_AlbedoTex", albedoForSmoothness != null ? (Texture)albedoForSmoothness : Texture2D.whiteTexture);
            blitMat.SetFloat("_HasMS", msMap != null ? 1f : 0f);
            blitMat.SetFloat("_HasAO", aoMap != null ? 1f : 0f);
            blitMat.SetFloat("_SmoothFromAlbedo", albedoForSmoothness != null ? 1f : 0f);

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prevActive = RenderTexture.active;
            Graphics.Blit(null, rt, blitMat);

            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(blitMat);

            string matPath = AssetDatabase.GetAssetPath(sourceMat);
            string folder = string.IsNullOrEmpty(matPath) ? "Assets/CrispConverted" : Path.GetDirectoryName(matPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            string texPath = folder + "/" + sourceMat.name + "_MaskMap.png";

            File.WriteAllBytes(texPath, readback.EncodeToPNG());
            Object.DestroyImmediate(readback);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }
    }
}
