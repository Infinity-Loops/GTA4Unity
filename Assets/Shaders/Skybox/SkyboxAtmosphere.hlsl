#ifndef SKYBOX_ATMOSPHERE_INCLUDED
#define SKYBOX_ATMOSPHERE_INCLUDED

#include "SkyboxCommon.hlsl"

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
    float mu = dot(viewDir, sunDir);

    float dayFactor = smoothstep(-0.1, 0.3, sunAlt);
    float sunsetFactor = smoothstep(0.35, 0.0, sunAlt) * smoothstep(-0.15, 0.05, sunAlt);

    // Rayleigh extinction: determines how sunlight is colored after
    // traveling through the atmosphere. At low sun angles the long path
    // scatters away blue light, leaving orange/red.
    float3 betaR = float3(5.8, 13.5, 33.1) * 1e-3;
    float sunDepth = 1.0 / (max(sunAlt, 0.0) + 0.045);
    float3 sunTransmittance = exp(-betaR * sunDepth * 1.1);

    // Horizontal facing toward sun
    float2 viewH = normalize(float2(viewDir.x, viewDir.z) + 0.0001);
    float2 sunH  = normalize(float2(sunDir.x, sunDir.z) + 0.0001);
    float sunFacing = saturate(dot(viewH, sunH) * 0.5 + 0.5);
    sunFacing = pow(sunFacing, sunsetSpread);

    // Mie halo: forward-scattered glow around the sun in the sky
    float mieGlow = MiePhase(mu, 0.76) * 0.12;

    // Day palette — horizon picks up Rayleigh tinting when facing the sun
    float3 dayZenith  = zenithColor;
    float3 dayHorizon = horizonColor;
    dayHorizon *= lerp(1.0, sunTransmittance * 1.8, sunFacing * 0.4);

    // Night palette
    float3 nightZenith  = nightAmbient;
    float3 nightHorizon = nightAmbient * 1.5 + float3(0.01, 0.01, 0.02);

    // Blend day/night
    float3 zenith  = lerp(nightZenith, dayZenith, dayFactor);
    float3 horizon = lerp(nightHorizon, dayHorizon, dayFactor);

    // Sunset injection — sun-facing side gets warm, opposite gets purple
    float3 sunsetHorizon = sunsetColor;
    float3 sunsetAway    = sunsetColor * float3(0.5, 0.3, 0.6);
    horizon = lerp(horizon, sunsetHorizon, sunsetFactor * sunFacing);
    horizon = lerp(horizon, sunsetAway, sunsetFactor * (1.0 - sunFacing) * 0.4);
    zenith  = lerp(zenith, zenithColor * 0.3 + sunsetColor * 0.2, sunsetFactor);

    // Twilight: purple-blue zenith during blue hour
    float twilightFactor = smoothstep(0.05, -0.08, sunAlt) * smoothstep(-0.2, -0.05, sunAlt);
    zenith  = lerp(zenith, float3(0.1, 0.05, 0.18), twilightFactor);
    horizon = lerp(horizon, float3(0.15, 0.1, 0.12), twilightFactor * 0.5);

    float3 finalColor;
    if (elevation >= 0.0)
    {
        float skyGrad = pow(saturate(elevation), 0.5);
        finalColor = lerp(horizon, zenith, skyGrad);

        // Mie halo tinted by sun transmittance
        finalColor += sunTransmittance * mieGlow * dayFactor;

        // Rayleigh deepening: sky opposite the sun is deeper blue
        float antiSun = 1.0 - sunFacing;
        finalColor *= lerp(1.0, 1.0 + float3(0.0, 0.0, 0.15) * skyGrad, antiSun * dayFactor);

        // Aerial perspective: haze builds near the horizon
        float haze = pow(1.0 - saturate(elevation * 2.5), 3.0);
        finalColor = lerp(finalColor, horizon * 1.1, haze * 0.35 * dayFactor);
    }
    else
    {
        float groundGrad = smoothstep(0.0, 0.5, -elevation);
        float3 litGround = groundColor * max(dayFactor, 0.05);
        float3 groundHorizon = horizon * 0.7;
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

    // Tight inner corona
    float innerGlow = pow(saturate(cosAngle), 150.0) * glowIntensity * 1.5;

    // Wide outer bloom — stronger at low sun angles
    float outerGlow = pow(saturate(cosAngle * 0.5 + 0.5), 6.0) * glowIntensity * 0.08;
    outerGlow *= saturate(1.0 - sunAlt * 0.5);

    float3 result = disc * sunColor * 3.0;
    result += innerGlow * sunColor;
    result += outerGlow * lerp(float3(1.0, 0.7, 0.4), sunColor, sunAlt);
    result *= saturate(sunDir.y * 5.0 + 0.5);

    return result;
}

#endif // SKYBOX_ATMOSPHERE_INCLUDED
