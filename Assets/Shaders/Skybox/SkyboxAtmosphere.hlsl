#ifndef SKYBOX_ATMOSPHERE_INCLUDED
#define SKYBOX_ATMOSPHERE_INCLUDED

#include "SkyboxCommon.hlsl"

// Physical constants
static const float PLANET_RADIUS = 6360.0;
static const float ATMO_RADIUS   = 6420.0;
static const float HR = 8.0;
static const float HM = 1.2;
static const float HO_CENTER = 25.0;
static const float HO_WIDTH  = 15.0;
static const float3 BETA_R0 = float3(5.8, 13.5, 33.1) * 1e-3;
static const float3 BETA_O0 = float3(0.65, 1.881, 0.085) * 6e-4;

#define ATMO_VIEW_SAMPLES  16
#define ATMO_LIGHT_SAMPLES 8

struct AtmosphereResult
{
    float3 color;
};

float OzoneDensity(float altitude)
{
    float d = (altitude - HO_CENTER) / HO_WIDTH;
    return exp(-0.5 * d * d);
}

float3 AtmoOpticalDepth(float3 pos, float3 dir, float atmoDensity, float ozoneStrength)
{
    float2 atmoHit = RaySphereIntersect(pos, dir, ATMO_RADIUS);
    float pathLen = max(atmoHit.y, 0.0);
    float stepLen = pathLen / float(ATMO_LIGHT_SAMPLES);

    float depthR = 0.0;
    float depthM = 0.0;
    float depthO = 0.0;

    for (int i = 0; i < ATMO_LIGHT_SAMPLES; i++)
    {
        float3 s = pos + dir * (float(i) + 0.5) * stepLen;
        float alt = length(s) - PLANET_RADIUS;
        depthR += exp(-alt / HR) * atmoDensity;
        depthM += exp(-alt / HM) * atmoDensity;
        depthO += OzoneDensity(alt) * ozoneStrength;
    }

    return float3(depthR, depthM, depthO) * stepLen;
}

AtmosphereResult ComputeFullAtmosphere(float3 viewDir, float3 sunDir,
    float sunIntensity, float atmoDensity, float mieStrength,
    float mieAnisotropy, float ozoneStrength)
{
    AtmosphereResult result;

    float3 cameraPos = float3(0, PLANET_RADIUS + 0.001, 0);

    // Below horizon: compute at grazing angle, darken toward nadir
    float belowHorizon = saturate(-viewDir.y);
    float3 atmoDir = float3(viewDir.x, max(viewDir.y, 0.004), viewDir.z);
    atmoDir = normalize(atmoDir);

    float2 atmoHit = RaySphereIntersect(cameraPos, atmoDir, ATMO_RADIUS);
    float rayLen = atmoHit.y;

    float stepLen = rayLen / float(ATMO_VIEW_SAMPLES);

    float3 betaR = BETA_R0 * atmoDensity;
    float  betaMscat = mieStrength * atmoDensity;
    float  betaMext  = betaMscat * 1.11; // extinction > scattering (10% absorption)
    float3 betaO = BETA_O0 * ozoneStrength;

    float mu = dot(atmoDir, sunDir);
    float phaseR = RayleighPhase(mu);
    float phaseM = MiePhase(mu, mieAnisotropy);

    float3 totalR = 0.0;
    float3 totalM = 0.0;
    float optR = 0.0;
    float optM = 0.0;
    float optO = 0.0;

    for (int i = 0; i < ATMO_VIEW_SAMPLES; i++)
    {
        float3 samplePos = cameraPos + atmoDir * (float(i) + 0.5) * stepLen;
        float altitude = length(samplePos) - PLANET_RADIUS;

        float densityR = exp(-altitude / HR) * atmoDensity;
        float densityM = exp(-altitude / HM) * atmoDensity;
        float densityO = OzoneDensity(altitude) * ozoneStrength;

        optR += densityR * stepLen;
        optM += densityM * stepLen;
        optO += densityO * stepLen;

        // Earth shadow: check if sun is blocked by the planet
        float2 sunGround = RaySphereIntersect(samplePos, sunDir, PLANET_RADIUS - 0.5);
        bool inShadow = (sunGround.y > sunGround.x) && (sunGround.x > 0.0);

        if (!inShadow)
        {
            float3 lightDepth = AtmoOpticalDepth(samplePos, sunDir, atmoDensity, ozoneStrength);

            // Push sunset onset earlier: boost light extinction below 30° sun angle
            float sunsetBoost = 1.0 + saturate(0.5 - sunDir.y) * 4.0;
            lightDepth.xy *= sunsetBoost;

            float3 tau = betaR * (optR + lightDepth.x)
                       + betaMext * (optM + lightDepth.y)
                       + betaO * optO;
            float3 attenuation = exp(-tau);

            totalR += densityR * attenuation * stepLen;
            totalM += densityM * attenuation * stepLen;
        }
    }

    // Single scattering
    float3 scatter = sunIntensity * (totalR * betaR * phaseR + totalM * betaMscat * phaseM);

    // Multi-scattering (Hillaire 2020): higher-order bounces use isotropic phase.
    // Gentler boost at sunset to avoid uniform over-brightening.
    float3 ms = sunIntensity * (totalR * betaR + totalM * betaMscat) * (1.0 / (4.0 * SKY_PI));
    float msBoost = 1.0 + saturate(1.0 - sunDir.y * 3.0) * 0.8;
    scatter += ms * msBoost;

    // Night airglow
    float nightFactor = saturate(-sunDir.y * 3.0);
    scatter += float3(0.008, 0.01, 0.018) * nightFactor;

    // Below horizon: darken toward nadir (ground occludes lower atmosphere)
    scatter *= 1.0 - belowHorizon * 0.65;

    result.color = max(scatter, 0.0);
    return result;
}

float3 ComputeSunDisc(float3 viewDir, float3 sunDir,
    float discSize, float glowIntensity)
{
    float cosAngle = dot(viewDir, sunDir);
    float disc = smoothstep(discSize - 0.0015, discSize, cosAngle);

    float sunAlt = saturate(sunDir.y);
    float3 sunColor = lerp(float3(1.0, 0.5, 0.15), float3(1.0, 0.95, 0.8), sunAlt);

    // Limb darkening
    float limbDist = 1.0 - saturate((cosAngle - discSize) / (1.0 - discSize));
    float limb = 1.0 - 0.5 * limbDist * limbDist;
    float3 limbColor = lerp(sunColor, sunColor * float3(1.0, 0.75, 0.45), pow(max(limbDist, 0.0), 0.4));

    float innerGlow = pow(saturate(cosAngle), 150.0) * glowIntensity * 1.5;
    float outerGlow = pow(saturate(cosAngle * 0.5 + 0.5), 6.0) * glowIntensity * 0.08;
    outerGlow *= saturate(1.0 - sunAlt * 0.5);

    float3 result = disc * limbColor * limb * 3.0;
    result += innerGlow * sunColor;
    result += outerGlow * lerp(float3(1.0, 0.7, 0.4), sunColor, sunAlt);
    result *= saturate(sunDir.y * 5.0 + 0.5);

    return result;
}

#endif // SKYBOX_ATMOSPHERE_INCLUDED
