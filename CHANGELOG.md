# Changelog

## 1.1.0

Deferred renderer support. `Crisp/Lit` previously declared its lit pass as `UniversalForward`,
which a deferred renderer never draws — opaque materials were invisible there rather than falling
back to forward.

- `ForwardLit` is now tagged `UniversalForwardOnly`, so it runs under both forward and deferred
  renderers. Lighting stays entirely Crisp's in both.
- The depth-normals pass is now tagged `DepthNormalsOnly`, which the deferred prepass requires and
  the forward prepass also accepts.
- Added a `UniversalGBuffer` pass that fills the GBuffer without lighting, for consumers that read
  it back — URP's rendering debugger, and screen-space GI/AO packages that draw their own
  `UniversalGBuffer` renderer list.

`Crisp/Unlit` needed no change: its main pass is unnamed, so it is treated as `SRPDefaultUnlit`,
which deferred renderers already draw forward-only.

## 1.0.0

First release. Built and verified against Unity 6000.3.14f1 with URP 17.3.0.

- `Crisp/Lit` and `Crisp/Unlit` shaders.
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
