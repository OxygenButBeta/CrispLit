using UnityEngine;

namespace Crisp.Rendering
{
    public static class CrispDFGBinder
    {
        const string ResourcePath = "CrispDFG";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BindRuntime() => Bind();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void BindEditor() => Bind();
#endif

        public static void Bind()
        {
            var lut = Resources.Load<Texture2D>(ResourcePath);
            if (lut != null)
            {
                Shader.SetGlobalTexture("_CrispDFG", lut);
                Shader.SetGlobalFloat("_CrispDFGBound", 1f);
            }
            else
            {
                Shader.SetGlobalFloat("_CrispDFGBound", 0f);
            }
        }
    }
}
