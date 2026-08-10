# Changelog

## 1.0.0

First release. Built and verified against Unity 6000.3.14f1 with URP 17.3.0.

- `Crisp/Lit` and `Crisp/Unlit` shaders, Forward+ only.
- BRDF: GGX distribution, height-correlated Smith visibility, Schlick Fresnel,
  multiscatter energy compensation, Disney diffuse, AO-derived micro-shadowing,
  specular and horizon occlusion, geometric specular anti-aliasing.
- Preintegrated split-sum DFG LUT (128×128, shipped prebuilt) with an analytic
  fallback when the LUT is missing.
- MADS mask map (R metallic, G occlusion, B unused, A smoothness).
- Custom material inspector with automatic keyword handling.
- `Tools/Crisp` menu: convert selected materials, convert scene materials,
  regenerate the DFG LUT. The converter packs URP's separate metallic-smoothness
  and occlusion maps into a single MADS texture.
