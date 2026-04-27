#ifndef SKYBOX_STARS_INCLUDED
#define SKYBOX_STARS_INCLUDED

#include "SkyboxCommon.hlsl"

float3 ComputeStars(float3 viewDir, float sunAltitude,
    float starDensity, float starBrightness, float twinkleSpeed, float time)
{
    float nightVisibility = saturate(-sunAltitude * 5.0);
    if (nightVisibility < 0.001)
        return float3(0, 0, 0);

    float3 result = float3(0, 0, 0);
    float3 cellPos = viewDir * starDensity;
    float3 cellBase = floor(cellPos);

    for (int dx = 0; dx <= 1; dx++)
    for (int dy = 0; dy <= 1; dy++)
    for (int dz = 0; dz <= 1; dz++)
    {
        float3 cellId = cellBase + float3(dx, dy, dz);
        float cellHash = Hash31(cellId);

        if (cellHash > 0.05)
            continue;

        float3 starPos = cellId + Hash33(cellId + 0.5) * 0.9 + 0.05;
        float dist = length(cellPos - starPos);

        float star = smoothstep(0.07, 0.01, dist);
        if (star < 0.001)
            continue;

        float mag = lerp(0.3, 1.0, pow(Hash11(cellHash * 73.1), 2.0));

        float temp = Hash11(cellHash * 191.7);
        float3 starColor = lerp(float3(0.8, 0.85, 1.0), float3(1.0, 0.95, 0.85), temp);

        float phase = Hash11(cellHash * 347.9) * SKY_TWO_PI;
        float twinkle = sin(time * twinkleSpeed * (0.5 + Hash11(cellHash * 521.3)) + phase);
        twinkle = twinkle * 0.3 + 0.7;

        result += star * starColor * twinkle * mag * starBrightness;
    }

    result *= smoothstep(0.0, 0.1, viewDir.y);

    return result * nightVisibility;
}

#endif // SKYBOX_STARS_INCLUDED
