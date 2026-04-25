#ifndef SKYBOX_ATMOSPHERE_INCLUDED
#define SKYBOX_ATMOSPHERE_INCLUDED

#include "SkyboxCommon.hlsl"

// Single-scattering atmospheric model (Nishita 1993).
// Adapted from the widely-used wwwtyro/glsl-atmosphere reference implementation.
//
// Per pixel:
//   - 16 samples along the view ray inside the atmosphere shell
//   - 8 samples along each view-sample's ray toward the sun (transmittance)
//   - Rayleigh + Cornette-Shanks Mie phase functions applied at the end
//
// Output is HDR — produces values well above 1.0 around the sun. Requires the
// camera/scene to render in linear HDR (URP HDR On) for correct tonemapping.

#define SKY_VIEW_SAMPLES  16
#define SKY_LIGHT_SAMPLES 8

// Earth-sized planet at world origin. Camera sits 1 meter above the surface so
// the view ray properly intersects the atmosphere shell at all angles.
static const float SkyPlanetRadius     = 6371000.0;
static const float SkyAtmosphereRadius = 6471000.0;
static const float3 SkyCameraOrigin    = float3(0.0, 6371001.0, 0.0);

// Reference Earth scattering coefficients — wavelength-dependent Rayleigh + flat Mie.
static const float3 SkyKRayleigh = float3(5.5e-6, 13.0e-6, 22.4e-6);
static const float  SkyKMie      = 21e-6;
static const float  SkyHRayleigh = 8000.0;
static const float  SkyHMie      = 1200.0;

// Ray-sphere intersection in the standard quadratic form. Returns (tNear, tFar);
// (1e5, -1e5) on miss so callers can detect via x > y.
float2 SkyRSI(float3 r0, float3 rd, float sr)
{
    float a = dot(rd, rd);
    float b = 2.0 * dot(rd, r0);
    float c = dot(r0, r0) - sr * sr;
    float d = b * b - 4.0 * a * c;
    if (d < 0.0) return float2(1e5, -1e5);
    float s = sqrt(d);
    return float2((-b - s) / (2.0 * a), (-b + s) / (2.0 * a));
}

// Single-scattering atmosphere integration.
//   r:    view direction (normalized)
//   r0:   ray origin in world space (world center is the planet center)
//   pSun: direction toward the sun (normalized)
//   iSun: sun intensity multiplier
float3 SkyAtmosphereIntegral(float3 r, float3 r0, float3 pSun, float iSun)
{
    float2 p = SkyRSI(r0, r, SkyAtmosphereRadius);
    if (p.x > p.y) return float3(0.0, 0.0, 0.0);

    // Clip the integration to the planet surface so we don't integrate underground.
    p.y = min(p.y, SkyRSI(r0, r, SkyPlanetRadius).x);
    float iStepSize = (p.y - p.x) / float(SKY_VIEW_SAMPLES);

    // Phase functions (Rayleigh + Cornette-Shanks Mie). Computed once outside the loop.
    float mu   = dot(r, pSun);
    float mumu = mu * mu;
    float g    = _MieG;
    float gg   = g * g;
    float pRlh = (3.0 / (16.0 * SKY_PI)) * (1.0 + mumu);
    float pMie = (3.0 / (8.0 * SKY_PI))
               * ((1.0 - gg) * (mumu + 1.0))
               / (pow(max(1.0 + gg - 2.0 * mu * g, 0.0), 1.5) * (2.0 + gg));

    float iTime = 0.0;
    float3 totalRlh = 0.0;
    float3 totalMie = 0.0;
    float  iOdRlh = 0.0;
    float  iOdMie = 0.0;

    [loop]
    for (int i = 0; i < SKY_VIEW_SAMPLES; i++)
    {
        float3 iPos = r0 + r * (iTime + iStepSize * 0.5);
        float iHeight = length(iPos) - SkyPlanetRadius;
        float odStepRlh = exp(-iHeight / SkyHRayleigh) * iStepSize;
        float odStepMie = exp(-iHeight / SkyHMie)      * iStepSize;
        iOdRlh += odStepRlh;
        iOdMie += odStepMie;

        float jStepSize = SkyRSI(iPos, pSun, SkyAtmosphereRadius).y / float(SKY_LIGHT_SAMPLES);
        float jTime = 0.0;
        float jOdRlh = 0.0;
        float jOdMie = 0.0;

        [loop]
        for (int j = 0; j < SKY_LIGHT_SAMPLES; j++)
        {
            float3 jPos = iPos + pSun * (jTime + jStepSize * 0.5);
            float jHeight = length(jPos) - SkyPlanetRadius;
            jOdRlh += exp(-jHeight / SkyHRayleigh) * jStepSize;
            jOdMie += exp(-jHeight / SkyHMie)      * jStepSize;
            jTime  += jStepSize;
        }

        float3 attn = exp(-(SkyKMie * (iOdMie + jOdMie) + SkyKRayleigh * (iOdRlh + jOdRlh)));
        totalRlh += odStepRlh * attn;
        totalMie += odStepMie * attn;
        iTime    += iStepSize;
    }

    return iSun * (
        pRlh * SkyKRayleigh * totalRlh * _RayleighStrength +
        pMie * SkyKMie      * totalMie * _MieStrength
    );
}

float3 ComputeAtmosphere(float3 viewDir, float3 sunDir)
{
    return SkyAtmosphereIntegral(viewDir, SkyCameraOrigin, sunDir, _SunIntensity);
}

// Visible sun disc on top of the atmosphere — soft circular falloff at the disc edge.
float3 ComputeSunDisc(float3 viewDir, float3 sunDir)
{
    float cosTheta = dot(viewDir, sunDir);
    float angularRadius = max(_SunSize, 0.0001);
    float disc = smoothstep(cos(angularRadius * 1.10), cos(angularRadius * 0.90), cosTheta);
    return disc * _SunDiscColor.rgb * GetSunColor() * _SunIntensity;
}

#endif // SKYBOX_ATMOSPHERE_INCLUDED
