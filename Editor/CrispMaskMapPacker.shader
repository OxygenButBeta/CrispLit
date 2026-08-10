Shader "Hidden/Crisp/MaskMapPacker"
{
    Properties
    {
        _MSTex("MetallicSmoothness", 2D) = "white" {}
        _AOTex("Occlusion", 2D) = "white" {}
        _AlbedoTex("Albedo", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MSTex;
            sampler2D _AOTex;
            sampler2D _AlbedoTex;
            float _HasMS;
            float _HasAO;
            float _SmoothFromAlbedo;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 ms = tex2D(_MSTex, i.uv);
                fixed ao = tex2D(_AOTex, i.uv).g;
                fixed albedoA = tex2D(_AlbedoTex, i.uv).a;

                fixed metallic = _HasMS > 0.5 ? ms.r : 1.0;
                fixed occlusion = _HasAO > 0.5 ? ao : 1.0;
                fixed smoothness = _SmoothFromAlbedo > 0.5 ? albedoA : (_HasMS > 0.5 ? ms.a : 1.0);

                return fixed4(metallic, occlusion, 1.0, smoothness);
            }
            ENDCG
        }
    }
}
