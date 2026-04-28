#ifndef SKYBOX_COMMON_INCLUDED
#define SKYBOX_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define SKY_PI 3.14159265359
#define SKY_TWO_PI 6.28318530718

// ============================================================================
//  Material CBUFFER (SRP Batcher compatible)
// ============================================================================

CBUFFER_START(UnityPerMaterial)
    float  _SunIntensity;
    float  _AtmoDensity;
    float  _MieStrength;
    float  _MieAnisotropy;
    float  _OzoneStrength;

    float  _SunDiscSize;
    float  _SunGlowIntensity;

    float  _MoonSize;
    float  _MoonGlow;

    float  _StarDensity;
    float  _StarBrightness;
    float  _TwinkleSpeed;

    float  _CloudHeight;
    float  _CloudCoverage;
    float  _CloudSpeed;
    float  _CloudScale;
    float  _CloudEdge;

    float  _Exposure;
CBUFFER_END

// ============================================================================
//  Hash functions
// ============================================================================

float Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float Hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float3 Hash33(float3 p)
{
    p = float3(
        dot(p, float3(127.1, 311.7, 74.7)),
        dot(p, float3(269.5, 183.3, 246.1)),
        dot(p, float3(113.5, 271.9, 124.6))
    );
    return frac(sin(p) * 43758.5453);
}

// ============================================================================
//  Value noise
// ============================================================================

float ValueNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n00 = Hash21(i);
    float n10 = Hash21(i + float2(1, 0));
    float n01 = Hash21(i + float2(0, 1));
    float n11 = Hash21(i + float2(1, 1));

    return lerp(
        lerp(n00, n10, f.x),
        lerp(n01, n11, f.x),
        f.y);
}

float ValueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = Hash31(i);
    float n100 = Hash31(i + float3(1, 0, 0));
    float n010 = Hash31(i + float3(0, 1, 0));
    float n110 = Hash31(i + float3(1, 1, 0));
    float n001 = Hash31(i + float3(0, 0, 1));
    float n101 = Hash31(i + float3(1, 0, 1));
    float n011 = Hash31(i + float3(0, 1, 1));
    float n111 = Hash31(i + float3(1, 1, 1));

    return lerp(
        lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
        lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
        f.z);
}

// ============================================================================
//  FBM with rotation matrix between octaves
// ============================================================================

static const float3x3 FBM_ROT = float3x3(
     0.00,  1.60,  1.20,
    -1.60,  0.72, -0.96,
    -1.20, -0.96,  1.28
);

float FBM3(float3 p, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * ValueNoise3D(p);
        p = mul(FBM_ROT, p) * 1.1;
        amplitude *= 0.5;
    }
    return value;
}

float FBM6(float3 p)
{
    float f = 0.0;
    float w = 0.5;
    for (int i = 0; i < 6; i++)
    {
        f += w * ValueNoise3D(p);
        p = mul(FBM_ROT, p) * 1.1;
        w *= 0.5;
    }
    return f;
}

// ============================================================================
//  Phase functions
// ============================================================================

float HGPhase(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / (4.0 * SKY_PI * pow(max(denom, 0.0001), 1.5));
}

float DualHGPhase(float cosTheta, float g1, float g2, float blend)
{
    return lerp(HGPhase(cosTheta, g1), HGPhase(cosTheta, g2), blend);
}

float RayleighPhase(float mu)
{
    return 3.0 / (16.0 * SKY_PI) * (1.0 + mu * mu);
}

float MiePhase(float mu, float g)
{
    float gg = g * g;
    return (3.0 / (8.0 * SKY_PI)) * ((1.0 - gg) * (mu * mu + 1.0))
         / (pow(1.0 + gg - 2.0 * mu * g, 1.5) * (2.0 + gg));
}

// ============================================================================
//  Ray-sphere intersection
// ============================================================================

float2 RaySphereIntersect(float3 r0, float3 rd, float sr)
{
    float a = dot(rd, rd);
    float b = 2.0 * dot(rd, r0);
    float c = dot(r0, r0) - sr * sr;
    float d = b * b - 4.0 * a * c;
    if (d < 0.0) return float2(1e5, -1e5);
    d = sqrt(d);
    return float2((-b - d) / (2.0 * a), (-b + d) / (2.0 * a));
}

// ============================================================================
//  Utility
// ============================================================================

float SkyRemap(float value, float low1, float high1, float low2, float high2)
{
    return low2 + (value - low1) * (high2 - low2) / (high1 - low1);
}

#endif // SKYBOX_COMMON_INCLUDED
