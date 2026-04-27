#ifndef SKYBOX_ATMOSPHERE_INCLUDED
#define SKYBOX_ATMOSPHERE_INCLUDED

#include "SkyboxCommon.hlsl"

// Gradient-based atmosphere with sunset/sunrise injection.
// No raymarching — vibrant color bands, fast.

struct AtmosphereResult
{
    float3 color;
};

AtmosphereResult ComputeFullAtmosphere(float3 viewDir, float3 sunDir,
    float3 zenithColor, float3 horizonColor, float3 groundColor,
    float3 nightAmbient, float3 sunsetColor, float sunsetSpread)
{
    AtmosphereResult result;
    float sunAlt = sunDir.y;
    float elevation = viewDir.y;

    float dayFactor = smoothstep(-0.1, 0.3, sunAlt);
    float sunsetFactor = smoothstep(0.3, 0.0, sunAlt) * smoothstep(-0.15, 0.0, sunAlt);

    float2 viewH = normalize(float2(viewDir.x, viewDir.z) + 0.0001);
    float2 sunH = normalize(float2(sunDir.x, sunDir.z) + 0.0001);
    float sunFacing = saturate(dot(viewH, sunH) * 0.5 + 0.5);
    sunFacing = pow(sunFacing, sunsetSpread);

    float3 dayZenith = zenithColor;
    float3 dayHorizon = horizonColor;
    dayHorizon = lerp(dayHorizon, dayHorizon * float3(1.1, 1.0, 0.9), sunFacing * 0.3);

    float3 nightZenith = nightAmbient;
    float3 nightHorizon = nightAmbient * 1.5 + float3(0.01, 0.01, 0.02);

    float3 zenith = lerp(nightZenith, dayZenith, dayFactor);
    float3 horizon = lerp(nightHorizon, dayHorizon, dayFactor);

    float3 sunsetHorizon = sunsetColor;
    float3 sunsetAway = sunsetColor * float3(0.5, 0.3, 0.6);
    horizon = lerp(horizon, sunsetHorizon, sunsetFactor * sunFacing);
    horizon = lerp(horizon, sunsetAway, sunsetFactor * (1.0 - sunFacing) * 0.4);
    zenith = lerp(zenith, zenithColor * 0.4 + sunsetColor * 0.15, sunsetFactor);

    float3 finalColor;
    if (elevation >= 0.0)
    {
        float skyGrad = pow(saturate(elevation), 0.55);
        finalColor = lerp(horizon, zenith, skyGrad);
    }
    else
    {
        float groundGrad = smoothstep(0.0, 0.5, -elevation);
        float3 litGround = groundColor * max(dayFactor, 0.05);
        float3 groundHorizon = lerp(horizon, horizon * 0.7, 0.3);
        finalColor = lerp(groundHorizon, litGround, groundGrad);
    }

    result.color = max(finalColor, 0.0);
    return result;
}

float3 ComputeSunDisc(float3 viewDir, float3 sunDir,
    float discSize, float glowIntensity)
{
    float cosAngle = dot(viewDir, sunDir);

    float disc = smoothstep(discSize - 0.0015, discSize, cosAngle);

    float sunAlt = saturate(sunDir.y);
    float3 sunColor = lerp(float3(1.0, 0.5, 0.15), float3(1.0, 0.95, 0.8), sunAlt);

    float glow = pow(saturate(cosAngle), 100.0) * glowIntensity;

    float3 result = disc * sunColor * 3.0;
    result += glow * sunColor;
    result *= saturate(sunDir.y * 5.0 + 0.5);

    return result;
}

#endif // SKYBOX_ATMOSPHERE_INCLUDED
