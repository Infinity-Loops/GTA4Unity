#ifndef SKYBOX_MOON_INCLUDED
#define SKYBOX_MOON_INCLUDED

#include "SkyboxCommon.hlsl"

float3 ComputeMoon(float3 viewDir, float3 sunDir, float sunAltitude,
    float3 moonDir, float moonSize, float moonGlow)
{
    float visibility = saturate(-sunAltitude * 3.0 + 0.3);
    if (visibility < 0.001)
        return float3(0, 0, 0);

    float cosAngle = dot(viewDir, moonDir);
    float angDist = 1.0 - cosAngle;

    float glow = exp(-angDist / (moonSize * moonSize * 5.0)) * 0.08 * moonGlow;
    float3 glowColor = float3(0.6, 0.65, 0.8);
    float3 result = glowColor * glow;

    float discThreshold = 1.0 - moonSize * moonSize;
    float disc = smoothstep(discThreshold - 0.002, discThreshold, cosAngle);

    if (disc < 0.001)
        return result * visibility;

    float3 moonColor = float3(0.92, 0.92, 0.88);

    float3 moonToSun = sunDir - moonDir * dot(sunDir, moonDir);
    float tangentLen = length(moonToSun);

    if (tangentLen > 0.001)
    {
        float3 tangent = moonToSun / tangentLen;

        float3 viewOffset = viewDir - moonDir * cosAngle;
        float phaseDot = dot(viewOffset, tangent);

        float phaseAmount = saturate(tangentLen);
        float phase = smoothstep(-moonSize * 0.3, moonSize * 0.5, phaseDot);
        moonColor *= lerp(1.0, phase, phaseAmount * 0.8);
    }

    float edgeDist = (cosAngle - discThreshold) / (1.0 - discThreshold);
    float edgeRing = smoothstep(0.0, 0.1, edgeDist) * smoothstep(0.15, 0.05, edgeDist);
    moonColor += edgeRing * 0.08;

    result = lerp(result, moonColor, disc);
    return result * visibility;
}

float MoonMask(float3 viewDir, float3 moonDir, float moonSize)
{
    float cosAngle = dot(viewDir, moonDir);
    float discThreshold = 1.0 - moonSize * moonSize;
    return smoothstep(discThreshold - 0.001, discThreshold, cosAngle);
}

#endif // SKYBOX_MOON_INCLUDED
