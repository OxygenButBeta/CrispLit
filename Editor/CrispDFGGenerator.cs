using UnityEditor;
using UnityEngine;

namespace Crisp.Rendering.Editor
{
    public static class CrispDFGGenerator
    {
        const int Size = 128;
        const int Samples = 1024;

        // The package ships a prebuilt LUT; regenerating only makes sense while the package
        // is embedded or local, since a git/registry install lives in a read-only cache.
        static string ResolveAssetPath()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CrispDFGGenerator).Assembly);
            string root = info != null ? info.assetPath : "Assets/CrispLit";
            return root + "/Runtime/Resources/CrispDFG.asset";
        }

        [MenuItem("Tools/Crisp/Generate DFG LUT")]
        public static void Generate()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBAHalf, false, true)
            {
                name = "CrispDFG",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float perceptualRoughness = (y + 0.5f) / Size;
                float roughness = perceptualRoughness * perceptualRoughness;
                for (int x = 0; x < Size; x++)
                {
                    float nov = (x + 0.5f) / Size;
                    Vector2 ab = IntegrateBRDF(nov, roughness);
                    pixels[y * Size + x] = new Color(ab.x, ab.y, 0f, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);

            string AssetPath = ResolveAssetPath();
            string folder = System.IO.Path.GetDirectoryName(AssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(tex, existing);
                Object.DestroyImmediate(tex);
            }
            else
            {
                AssetDatabase.CreateAsset(tex, AssetPath);
            }
            AssetDatabase.SaveAssets();
            CrispDFGBinder.Bind();
            Debug.Log("CrispDFG LUT generated: " + AssetPath);
        }

        static Vector2 IntegrateBRDF(float nov, float roughness)
        {
            var v = new Vector3(Mathf.Sqrt(Mathf.Max(0f, 1f - nov * nov)), 0f, nov);
            float a = 0f, b = 0f;

            for (int i = 0; i < Samples; i++)
            {
                Vector2 xi = Hammersley(i, Samples);
                Vector3 h = ImportanceSampleGGX(xi, roughness);
                Vector3 l = 2f * Vector3.Dot(v, h) * h - v;

                float nol = l.z;
                if (nol <= 0f) continue;

                float noh = Mathf.Max(h.z, 0f);
                float voh = Mathf.Max(Vector3.Dot(v, h), 0f);

                float g = GSmithIBL(nov, nol, roughness);
                float gVis = g * voh / Mathf.Max(noh * nov, 1e-6f);
                float fc = Mathf.Pow(1f - voh, 5f);

                a += (1f - fc) * gVis;
                b += fc * gVis;
            }
            return new Vector2(a / Samples, b / Samples);
        }

        static float GSmithIBL(float nov, float nol, float roughness)
        {
            // Karis IBL k'si (a^2/2) - split-sum turetimiyle tutarli olan varyant
            float k = roughness * roughness / 2f;
            float gv = nov / (nov * (1f - k) + k);
            float gl = nol / (nol * (1f - k) + k);
            return gv * gl;
        }

        static Vector3 ImportanceSampleGGX(Vector2 xi, float roughness)
        {
            float a = roughness;
            float phi = 2f * Mathf.PI * xi.x;
            float cosTheta = Mathf.Sqrt((1f - xi.y) / (1f + (a * a - 1f) * xi.y));
            float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
            return new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), cosTheta);
        }

        static Vector2 Hammersley(int i, int n)
        {
            uint bits = (uint)i;
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
            bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
            bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
            return new Vector2((float)i / n, bits * 2.3283064365386963e-10f);
        }
    }
}
