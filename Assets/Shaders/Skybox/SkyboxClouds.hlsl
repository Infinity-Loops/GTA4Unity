#ifndef SKYBOX_CLOUDS_INCLUDED
#define SKYBOX_CLOUDS_INCLUDED

#include "SkyboxCommon.hlsl"

static const float2x2 CLOUD_FBM_ROT = float2x2(0.8, 0.6, -0.6, 0.8);

float CloudFBM(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;

    for (int i = 0; i < 5; i++)
    {
        value += amplitude * ValueNoise2D(p);
        p = mul(CLOUD_FBM_ROT, p) * 2.1 + float2(1.7, 3.5);
        amplitude *= 0.5;
    }
    return value;
}

float CloudDensityAt(float2 uv, float cloudScale, float coverage, float edgeSoftness)
{
    float2 warp = float2(
        ValueNoise2D(uv * cloudScale * 0.4 + 100.0),
        ValueNoise2D(uv * cloudScale * 0.4 + 200.0)
    );
    float noise = CloudFBM((uv + warp * 0.25) * cloudScale);
    float threshold = 1.0 - coverage;
    return smoothstep(threshold - edgeSoftness, threshold + edgeSoftness, noise);
}

float4 ComputeClouds(float3 viewDir, float3 sunDir, float sunAltitude,
    float cloudHeight, float coverage, float speed, float edgeSoftness,
    float cloudScale, float time)
{
    if (viewDir.y < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = viewDir.xz / viewDir.y * cloudHeight;
    uv += float2(time * speed, time * speed * 0.3);

    // Domain warp + FBM once for this pixel.
    float2 warp = float2(
        ValueNoise2D(uv * cloudScale * 0.4 + 100.0),
        ValueNoise2D(uv * cloudScale * 0.4 + 200.0)
    );
    float2 warpedUV = uv + warp * 0.25;
    float noise = CloudFBM(warpedUV * cloudScale);

    float threshold = 1.0 - coverage;
    float cloudShape = smoothstep(threshold - edgeSoftness, threshold + edgeSoftness, noise);

    if (cloudShape < 0.001)
        return float4(0, 0, 0, 0);

    // Internal shade from noise value (bright centers, dark edges).
    float shade = smoothstep(threshold, threshold + 0.15, noise);

    // Self-shadow: 12 samples stepping toward the sun across the altitude plane.
    float2 sunStep = -sunDir.xz * cloudHeight / max(sunDir.y, 0.05) * 0.08;
    float opticalDepth = 0.0;
    [unroll]
    for (int s = 1; s <= 12; s++)
    {
        float w = 1.0 - float(s - 1) / 12.0;
        opticalDepth += CloudDensityAt(uv + sunStep * float(s), cloudScale, coverage, edgeSoftness) * w;
    }
    float shadow = exp(-opticalDepth * 0.9);
    shadow = max(shadow, 0.02);

    // Lighting.
    float dayStrength = saturate(sunAltitude * 3.0 + 0.3);

    float3 brightColor = float3(1.0, 1.0, 1.0);
    float3 shadowColor = lerp(float3(0.25, 0.28, 0.45), float3(0.55, 0.58, 0.68), dayStrength);

    // Shadow darkens the whole cloud, not just the shade term.
    float3 cloudColor = lerp(shadowColor, brightColor, shade * dayStrength);
    cloudColor *= lerp(0.5, 1.0, shadow);

    // Sunset tinting.
    float sunsetFactor = smoothstep(0.25, 0.0, sunAltitude) * smoothstep(-0.1, 0.0, sunAltitude);
    cloudColor *= lerp(float3(1, 1, 1), float3(1.2, 0.8, 0.65), sunsetFactor);

    // Night darkening.
    float nightFactor = saturate(-sunAltitude * 3.0);
    cloudColor *= lerp(1.0, 0.2, nightFactor);

    // Horizon fade.
    cloudShape *= smoothstep(0.01, 0.2, viewDir.y);

    return float4(cloudColor, cloudShape);
}

#endif // SKYBOX_CLOUDS_INCLUDED
