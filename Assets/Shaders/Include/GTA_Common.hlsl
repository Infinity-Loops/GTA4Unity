#ifndef GTA_COMMON_INCLUDED
#define GTA_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---------------------------------------------------------------------------
// Vertex input — covers all GTA IV shader variants.
// ---------------------------------------------------------------------------
struct GTA_Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float4 color      : COLOR;
    float2 uv         : TEXCOORD0;
    float2 uv2        : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ---------------------------------------------------------------------------
// Interpolators
// ---------------------------------------------------------------------------
struct GTA_Varyings
{
    float4 positionCS  : SV_POSITION;
    float2 uv          : TEXCOORD0;
    float3 normalWS    : TEXCOORD1;
    float3 positionWS  : TEXCOORD2;
    half4  color       : TEXCOORD3;   // rgb = vertex color, a = day/night blend (NOT transparency)
    float  fogFactor   : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct GTA_Varyings_NM
{
    float4 positionCS  : SV_POSITION;
    float2 uv          : TEXCOORD0;
    float3 normalWS    : TEXCOORD1;
    float3 tangentWS   : TEXCOORD2;
    float3 bitangentWS : TEXCOORD3;
    float3 positionWS  : TEXCOORD4;
    float3 viewDirWS   : TEXCOORD5;
    half4  color       : TEXCOORD6;
    float  fogFactor   : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct GTA_Varyings_Shadow
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---------------------------------------------------------------------------
// Normal map unpacking — GTA IV normals are standard RGB (XYZ in RGB),
// NOT Unity's DXT5nm (XY in AG). UnpackNormal() would read wrong channels.
// ---------------------------------------------------------------------------
// Engine normal unpacking (gta_normalPS1.asm lines 154-168):
// Auto-detects DXT5nm (X in Alpha, Y in Green) vs standard (X in Red, Y in Green).
// Check: if (1 - A - R >= 0) → DXT5nm channels (A, G); else standard (R, G).
// Then subtract 0.5 from XY and derive Z = sqrt(1 - X² - Y²).
half3 GTA_UnpackNormal(half4 packedNormal)
{
    // Try .grb swizzle (Y,X,Z) — common DX9→Unity tangent space mismatch
    return UnpackNormalRGB(packedNormal.rgba, 1);
}

// Transform tangent-space normal to world space using TBN from vertex data.
// Falls back to vertex normal if the normal map produces an invalid result.
// Per-pixel cotangent frame from screen-space derivatives (Ben Golus method).
// Works without mesh tangent data — uses ddx/ddy of world position and UV.
// Ref: https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a
float3x3 GTA_CotangentFrame(float3 N, float3 positionWS, float2 uv)
{
    float3 dp1 = ddx(positionWS);
    float3 dp2 = ddy(positionWS);
    float2 duv1 = ddx(uv);
    float2 duv2 = ddy(uv);

    float3 dp2perp = cross(dp2, N);
    float3 dp1perp = cross(N, dp1);
    float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 B = dp2perp * duv1.y + dp1perp * duv2.y;

    float invmax = rsqrt(max(dot(T, T), dot(B, B)));
    return float3x3(T * invmax, B * invmax, N);
}

// Transform tangent-space normal to world space using cotangent frame.
// No mesh tangent data required — computed per-pixel from derivatives.
float3 GTA_NormalFromTBN(half3 normalTS, float3 tangentWS, float3 bitangentWS, float3 normalWS)
{
    // tangentWS/bitangentWS unused — kept for API compatibility.
    // The cotangent frame is computed in GTA_ApplyNormalMap instead.
    return normalize(normalWS);
}

// Full normal map application using cotangent frame. Call this in fragment shaders.
float3 GTA_ApplyNormalMap(half4 bumpSample, float3 normalWS, float3 positionWS, float2 uv)
{
    float3 N = normalize(normalWS);
    half3 normalTS = GTA_UnpackNormal(bumpSample);
    float3x3 tbn = GTA_CotangentFrame(N, positionWS, uv);
    float3 result = normalize(mul(normalTS, tbn));
    return any(isnan(result)) ? N : result;
}

// ---------------------------------------------------------------------------
// Stipple fade — LodFadeSystem writes StippleAlpha per sub-mesh each frame.
// Each shader must include _StippleAlpha in its CBUFFER and DOTS block.
// Call GTA_StippleFade(positionCS) at the start of every fragment shader.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Vertex transforms
// ---------------------------------------------------------------------------
GTA_Varyings GTA_VertexSimple(GTA_Attributes v)
{
    GTA_Varyings o = (GTA_Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS = vpi.positionCS;
    o.positionWS = vpi.positionWS;
    o.normalWS   = normalize(TransformObjectToWorldNormal(v.normalOS));
    o.uv         = v.uv;
    o.color      = v.color;
    o.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
    return o;
}

GTA_Varyings_NM GTA_VertexNormalMapped(GTA_Attributes v)
{
    GTA_Varyings_NM o = (GTA_Varyings_NM)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS  = vpi.positionCS;
    o.positionWS  = vpi.positionWS;
    o.normalWS    = normalize(TransformObjectToWorldNormal(v.normalOS));
    o.tangentWS   = normalize(TransformObjectToWorldDir(v.tangentOS.xyz));
    o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangentOS.w;
    o.viewDirWS   = GetWorldSpaceNormalizeViewDir(vpi.positionWS);
    o.uv          = v.uv;
    o.color       = v.color;
    o.fogFactor   = ComputeFogFactor(vpi.positionCS.z);
    return o;
}

GTA_Varyings_Shadow GTA_VertexShadow(GTA_Attributes v, float3 lightDir)
{
    GTA_Varyings_Shadow o = (GTA_Varyings_Shadow)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normWS = TransformObjectToWorldNormal(v.normalOS);
    posWS = ApplyShadowBias(posWS, normWS, lightDir);
    o.positionCS = TransformWorldToHClip(posWS);
    o.uv = v.uv;

    #if UNITY_REVERSED_Z
        o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif

    return o;
}

// ---------------------------------------------------------------------------
// Lighting — forward pass targeting deferred-quality results.
//
// Squared half-lambert diffuse (energy-conserving wrap, Valve HL2 technique),
// SH ambient with SSAO, Fresnel ambient rim for silhouette environment pickup,
// micro-shadow at the terminator, and per-pixel additional lights.
//
// Gate keywords in each shader's #pragma block to activate features:
//   _ADDITIONAL_LIGHTS              — per-pixel point/spot lights
//   _ADDITIONAL_LIGHT_SHADOWS       — shadows from additional lights
//   _SCREEN_SPACE_OCCLUSION         — SSAO integration
// ---------------------------------------------------------------------------
half3 GTA_Lighting(half3 albedo, float3 normalWS, float3 positionWS)
{
    Light mainLight = GetMainLight();
    half NdotL = dot(normalWS, mainLight.direction);
    half wrapped = NdotL * 0.5 + 0.5;

    half3 ambient = max(SampleSH(normalWS), 0.03);

    half3 direct = mainLight.color * (wrapped * wrapped);

    float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
    half NdotV = saturate(dot(normalWS, viewDir));
    half fresnel = pow(1.0 - NdotV, 4.0) * 0.15;

    half3 lighting = ambient + direct + ambient * fresnel;

    #ifdef _ADDITIONAL_LIGHTS
        uint count = GetAdditionalLightsCount();
        for (uint j = 0u; j < count; j++)
        {
            Light addLight = GetAdditionalLight(j, positionWS);
            half addNdotL = saturate(dot(normalWS, addLight.direction));
            half addWrap = addNdotL * 0.5 + 0.5;
            lighting += addLight.color * (addWrap * addNdotL)
                      * addLight.distanceAttenuation
                      * addLight.shadowAttenuation;
        }
    #endif

    return clamp(albedo * lighting, 0.0, 16.0);
}

half3 GTA_LightingWithShadow(half3 albedo, float3 normalWS, float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half NdotL = dot(normalWS, mainLight.direction);
    half wrapped = NdotL * 0.5 + 0.5;
    half shadow = mainLight.shadowAttenuation;

    shadow *= saturate(NdotL * 4.0 + 0.5);

    half3 ambient = max(SampleSH(normalWS), 0.03);
    ambient *= lerp(0.4, 1.0, shadow);

    #if defined(_SCREEN_SPACE_OCCLUSION)
        float4 clipPos = TransformWorldToHClip(positionWS);
        float2 screenUV = clipPos.xy / clipPos.w * 0.5 + 0.5;
        #if UNITY_UV_STARTS_AT_TOP
            screenUV.y = 1.0 - screenUV.y;
        #endif
        AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenUV);
        ambient *= aoFactor.indirectAmbientOcclusion;
        shadow = min(shadow, aoFactor.directAmbientOcclusion);
    #endif

    half3 direct = mainLight.color * (wrapped * wrapped) * shadow;

    float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
    half NdotV = saturate(dot(normalWS, viewDir));
    half fresnel = pow(1.0 - NdotV, 4.0) * 0.15;
    half3 rim = ambient * fresnel;

    half3 lighting = ambient + direct + rim;

    #ifdef _ADDITIONAL_LIGHTS
        uint count = GetAdditionalLightsCount();
        for (uint j = 0u; j < count; j++)
        {
            Light addLight = GetAdditionalLight(j, positionWS);
            half addNdotL = saturate(dot(normalWS, addLight.direction));
            half addWrap = addNdotL * 0.5 + 0.5;
            lighting += addLight.color * (addWrap * addNdotL)
                      * addLight.distanceAttenuation
                      * addLight.shadowAttenuation;
        }
    #endif

    return clamp(albedo * lighting, 0.0, 16.0);
}

// ---------------------------------------------------------------------------
// Specular — GTA IV uses subtle specular, not PBR-level. The specular map
// is a mask/intensity (often very bright raw values). We dampen it to avoid
// the "snowflake" look from overly harsh Blinn-Phong highlights.
// ---------------------------------------------------------------------------
half3 GTA_Specular(float3 normalWS, float3 positionWS, float3 lightDir, half3 lightColor,
                   half specMask, float shininess, half shadow)
{
    float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
    float3 halfDir = normalize(lightDir + viewDir);
    half NdotH = saturate(dot(normalWS, halfDir));
    half spec = pow(NdotH, max(shininess, 1.0)) * saturate(specMask) * 0.15;
    return min(lightColor * spec * shadow, 1.0);
}

// ---------------------------------------------------------------------------
// Fog
// ---------------------------------------------------------------------------
half3 GTA_ApplyFog(half3 color, float fogFactor)
{
    return MixFog(color, fogFactor);
}

// ---------------------------------------------------------------------------
// Alpha / color helpers.
// GTA IV vertex color encodes day/night blend weights (gDayNightEffects),
// NOT diffuse tint. The old ShaderGraph shader did NOT multiply vertex color
// into the diffuse. Only terrain shaders use vertex color (layer blending).
// ---------------------------------------------------------------------------
half GTA_AlphaFromTexture(half4 diffuseTex)
{
    return diffuseTex.a;
}

half3 GTA_DiffuseColor(half4 diffuseTex, half4 vertexColor)
{
    return diffuseTex.rgb;
}

#endif // GTA_COMMON_INCLUDED
