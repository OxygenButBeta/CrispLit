using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crisp.Rendering.Editor
{
    enum SurfaceType { Opaque = 0, Transparent = 1 }
    enum BlendMode { Alpha = 0, Premultiply = 1, Additive = 2, Multiply = 3 }
    enum RenderFace { Both = 0, Back = 1, Front = 2 }

    public static class CrispMaterialValidator
    {
        public static void Validate(Material m)
        {
            bool alphaClip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f;
            SetKeyword(m, "_ALPHATEST_ON", alphaClip);

            var surface = (SurfaceType)(m.HasProperty("_Surface") ? m.GetFloat("_Surface") : 0f);
            bool transparent = surface == SurfaceType.Transparent;
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", transparent);
            SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
            SetKeyword(m, "_ALPHAMODULATE_ON", false);

            if (transparent)
            {
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_AlphaToMask", 0f);
                var blend = (BlendMode)(m.HasProperty("_Blend") ? m.GetFloat("_Blend") : 0f);
                switch (blend)
                {
                    case BlendMode.Alpha:
                        SetBlend(m, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
                                    UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        break;
                    case BlendMode.Premultiply:
                        SetBlend(m, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
                                    UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        SetKeyword(m, "_ALPHAPREMULTIPLY_ON", true);
                        break;
                    case BlendMode.Additive:
                        SetBlend(m, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One,
                                    UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One);
                        break;
                    case BlendMode.Multiply:
                        SetBlend(m, UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.Zero,
                                    UnityEngine.Rendering.BlendMode.Zero, UnityEngine.Rendering.BlendMode.One);
                        SetKeyword(m, "_ALPHAMODULATE_ON", true);
                        break;
                }
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_AlphaToMask", alphaClip ? 1f : 0f);
                SetBlend(m, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.Zero,
                            UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.Zero);
                m.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            }

            if (m.HasProperty("_QueueOffset"))
                m.renderQueue += (int)m.GetFloat("_QueueOffset");

            if (m.HasProperty("_MaskMap"))
                SetKeyword(m, "_MASKMAP", m.GetTexture("_MaskMap") != null);
            if (m.HasProperty("_BumpMap"))
                SetKeyword(m, "_NORMALMAP", m.GetTexture("_BumpMap") != null);
            if (m.HasProperty("_EmissionColor"))
            {
                bool emission = (m.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
                SetKeyword(m, "_EMISSION", emission);
            }
            if (m.HasProperty("_ReceiveShadows"))
                SetKeyword(m, "_RECEIVE_SHADOWS_OFF", m.GetFloat("_ReceiveShadows") < 0.5f);
            if (m.HasProperty("_SpecularHighlights"))
                SetKeyword(m, "_SPECULARHIGHLIGHTS_OFF", m.GetFloat("_SpecularHighlights") < 0.5f);
            if (m.HasProperty("_EnvironmentReflections"))
                SetKeyword(m, "_ENVIRONMENTREFLECTIONS_OFF", m.GetFloat("_EnvironmentReflections") < 0.5f);
            if (m.HasProperty("_SpecularAAOff"))
                SetKeyword(m, "_SPECULARAA_OFF", m.GetFloat("_SpecularAAOff") > 0.5f);
        }

        static void SetKeyword(Material m, string keyword, bool enabled)
        {
            if (enabled) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        static void SetBlend(Material m, UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst,
                             UnityEngine.Rendering.BlendMode srcA, UnityEngine.Rendering.BlendMode dstA)
        {
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_SrcBlendAlpha", (float)srcA);
            m.SetFloat("_DstBlendAlpha", (float)dstA);
        }
    }

    static class CrispHeader
    {
        static readonly Color s_Accent = new Color(0.31f, 0.76f, 0.97f);

        public static void Draw(string title, string subtitle)
        {
            var rect = GUILayoutUtility.GetRect(0f, 46f, GUILayout.ExpandWidth(true));
            rect.x -= 14f;
            rect.width += 18f;

            var bg = EditorGUIUtility.isProSkin ? new Color(0.13f, 0.14f, 0.16f) : new Color(0.82f, 0.84f, 0.86f);
            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), s_Accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.1f) },
            };
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.65f, 0.68f) : new Color(0.3f, 0.32f, 0.34f) },
            };
            var versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = s_Accent },
            };

            GUI.Label(new Rect(rect.x + 14f, rect.y + 5f, rect.width - 80f, 20f), title, titleStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 25f, rect.width - 80f, 16f), subtitle, subStyle);
            GUI.Label(new Rect(rect.xMax - 70f, rect.y, 60f, rect.height), "v1.0", versionStyle);

            EditorGUILayout.Space(6f);
        }
    }

    public class CrispLitShaderGUI : ShaderGUI
    {
        static bool s_SurfaceOptions = true;
        static bool s_SurfaceInputs = true;
        static bool s_Advanced;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            CrispHeader.Draw("Crisp Lit", "High-fidelity PBR for URP");
            var material = (Material)materialEditor.target;

            var surface = FindProperty("_Surface", props);
            var blend = FindProperty("_Blend", props);
            var cull = FindProperty("_Cull", props);
            var alphaClip = FindProperty("_AlphaClip", props);
            var cutoff = FindProperty("_Cutoff", props);
            var receiveShadows = FindProperty("_ReceiveShadows", props);

            var baseMap = FindProperty("_BaseMap", props);
            var baseColor = FindProperty("_BaseColor", props);
            var maskMap = FindProperty("_MaskMap", props);
            var metallic = FindProperty("_Metallic", props);
            var smoothness = FindProperty("_Smoothness", props);
            var occlusion = FindProperty("_OcclusionStrength", props);
            var bumpMap = FindProperty("_BumpMap", props);
            var bumpScale = FindProperty("_BumpScale", props);
            var emissionMap = FindProperty("_EmissionMap", props);
            var emissionColor = FindProperty("_EmissionColor", props);

            var specAAOff = FindProperty("_SpecularAAOff", props);
            var specAAVariance = FindProperty("_SpecAAVariance", props);
            var specAAThreshold = FindProperty("_SpecAAThreshold", props);
            var specularHighlights = FindProperty("_SpecularHighlights", props);
            var envReflections = FindProperty("_EnvironmentReflections", props);
            var queueOffset = FindProperty("_QueueOffset", props);

            EditorGUI.BeginChangeCheck();

            s_SurfaceOptions = EditorGUILayout.BeginFoldoutHeaderGroup(s_SurfaceOptions, "Surface Options");
            if (s_SurfaceOptions)
            {
                EnumPopup<SurfaceType>(materialEditor, surface, "Surface Type");
                if ((SurfaceType)surface.floatValue == SurfaceType.Transparent)
                    EnumPopup<BlendMode>(materialEditor, blend, "Blending Mode");
                EnumPopup<RenderFace>(materialEditor, cull, "Render Face");
                Toggle(materialEditor, alphaClip, "Alpha Clipping");
                if (alphaClip.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(cutoff, "Threshold");
                    EditorGUI.indentLevel--;
                }
                Toggle(materialEditor, receiveShadows, "Receive Shadows");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            s_SurfaceInputs = EditorGUILayout.BeginFoldoutHeaderGroup(s_SurfaceInputs, "Surface Inputs");
            if (s_SurfaceInputs)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Mask Map", "MADS: R=Metallic, G=AO, B=Detail, A=Smoothness"), maskMap);

                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(metallic, "Metallic");
                materialEditor.ShaderProperty(smoothness, "Smoothness");
                if (maskMap.textureValue != null)
                    materialEditor.ShaderProperty(occlusion, "AO Strength");
                EditorGUI.indentLevel--;

                materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), bumpMap,
                    bumpMap.textureValue != null ? bumpScale : null);

                bool emissionEnabled = materialEditor.EmissionEnabledProperty();
                if (emissionEnabled)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.TexturePropertyWithHDRColor(new GUIContent("Emission Map"), emissionMap, emissionColor, false);
                    EditorGUI.indentLevel--;
                }

                materialEditor.TextureScaleOffsetProperty(baseMap);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            s_Advanced = EditorGUILayout.BeginFoldoutHeaderGroup(s_Advanced, "Advanced");
            if (s_Advanced)
            {
                InvertedToggle(materialEditor, specAAOff, "Specular AA");
                if (specAAOff.floatValue < 0.5f)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(specAAVariance, "Variance");
                    materialEditor.ShaderProperty(specAAThreshold, "Threshold");
                    EditorGUI.indentLevel--;
                }
                Toggle(materialEditor, specularHighlights, "Specular Highlights");
                Toggle(materialEditor, envReflections, "Environment Reflections");
                materialEditor.ShaderProperty(queueOffset, "Sorting Priority");
                materialEditor.EnableInstancingField();
                materialEditor.RenderQueueField();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Material target in materialEditor.targets)
                    CrispMaterialValidator.Validate(target);
            }
        }

        public override void ValidateMaterial(Material material) => CrispMaterialValidator.Validate(material);

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            CrispMaterialValidator.Validate(material);
        }

        internal static void EnumPopup<T>(MaterialEditor editor, MaterialProperty prop, string label) where T : Enum
        {
            MaterialEditor.BeginProperty(prop);
            EditorGUI.BeginChangeCheck();
            var value = (T)Enum.ToObject(typeof(T), (int)prop.floatValue);
            value = (T)EditorGUILayout.EnumPopup(label, value);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = Convert.ToInt32(value);
            MaterialEditor.EndProperty();
        }

        internal static void Toggle(MaterialEditor editor, MaterialProperty prop, string label)
        {
            MaterialEditor.BeginProperty(prop);
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.Toggle(label, prop.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = value ? 1f : 0f;
            MaterialEditor.EndProperty();
        }

        internal static void InvertedToggle(MaterialEditor editor, MaterialProperty prop, string label)
        {
            MaterialEditor.BeginProperty(prop);
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.Toggle(label, prop.floatValue < 0.5f);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = value ? 0f : 1f;
            MaterialEditor.EndProperty();
        }
    }

    public class CrispUnlitShaderGUI : ShaderGUI
    {
        static bool s_SurfaceOptions = true;
        static bool s_SurfaceInputs = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            CrispHeader.Draw("Crisp Unlit", "Unlit companion for Crisp Lit");
            var surface = FindProperty("_Surface", props);
            var blend = FindProperty("_Blend", props);
            var cull = FindProperty("_Cull", props);
            var alphaClip = FindProperty("_AlphaClip", props);
            var cutoff = FindProperty("_Cutoff", props);
            var baseMap = FindProperty("_BaseMap", props);
            var baseColor = FindProperty("_BaseColor", props);
            var queueOffset = FindProperty("_QueueOffset", props);

            EditorGUI.BeginChangeCheck();

            s_SurfaceOptions = EditorGUILayout.BeginFoldoutHeaderGroup(s_SurfaceOptions, "Surface Options");
            if (s_SurfaceOptions)
            {
                CrispLitShaderGUI.EnumPopup<SurfaceType>(materialEditor, surface, "Surface Type");
                if ((SurfaceType)surface.floatValue == SurfaceType.Transparent)
                    CrispLitShaderGUI.EnumPopup<BlendMode>(materialEditor, blend, "Blending Mode");
                CrispLitShaderGUI.EnumPopup<RenderFace>(materialEditor, cull, "Render Face");
                CrispLitShaderGUI.Toggle(materialEditor, alphaClip, "Alpha Clipping");
                if (alphaClip.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(cutoff, "Threshold");
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            s_SurfaceInputs = EditorGUILayout.BeginFoldoutHeaderGroup(s_SurfaceInputs, "Surface Inputs");
            if (s_SurfaceInputs)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                materialEditor.TextureScaleOffsetProperty(baseMap);
                materialEditor.ShaderProperty(queueOffset, "Sorting Priority");
                materialEditor.EnableInstancingField();
                materialEditor.RenderQueueField();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Material target in materialEditor.targets)
                    CrispMaterialValidator.Validate(target);
            }
        }

        public override void ValidateMaterial(Material material) => CrispMaterialValidator.Validate(material);

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            CrispMaterialValidator.Validate(material);
        }
    }
}
