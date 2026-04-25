#ifndef SKYBOX_COMMON_INCLUDED
#define SKYBOX_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define SKY_PI 3.14159265359

// Property cbuffer shared across all skybox include files.
CBUFFER_START(UnityPerMaterial)
    // --- Sun ---
    float4 _SunDirection;       // xyz toward the sun; w > 0 enables override
    float  _SunSize;            // angular radius of the visible sun disc (radians)
    float  _SunIntensity;       // master intensity (drives atmosphere brightness)
    float4 _SunDiscColor;

    // --- Atmosphere ---
    float  _RayleighStrength;
    float  _MieStrength;
    float  _MieG;               // Mie phase anisotropy (~0.758 for atmosphere)

    // --- Cloud layer geometry ---
    // Each "layer" is a flat altitude plane onto which 2D noise is projected.
    // Multiple layers at different altitudes give parallax / depth illusion.
    float  _CloudCoverage;          // 0 = clear, 1 = fully overcast
    float  _CloudDensity;           // overall opacity multiplier
    float  _CloudEdgeSoftness;      // half-width of the smoothstep band at the coverage
                                    // threshold — bigger = softer, fluffier edges; smaller
                                    // = crisper, harder edges. Smooths the cloud boundary
                                    // upstream of self-shadow / lighting / alpha so high
                                    // absorption doesn't produce visibly hard shadow lines.
    float  _CloudScale;             // base noise frequency (smaller = larger blobs)
    float  _CloudDetailScale;       // detail noise frequency multiplier
    float  _CloudDetailWeight;      // strength of detail erosion at edges
    float4 _CloudWindSpeed;         // xyz drift in m/s

    // --- Guerrilla-style shape controls (Perlin-Worley + weather + curl) ---
    float  _CloudPerlinWorleyMix;   // 0 = pure Perlin (smooth), 1 = pure inverted Worley
                                    // (puffy billows). ~0.55 hits the cumulus look.
    float  _CloudWeatherScale;      // frequency of the large-scale coverage modulation
                                    // map. Smaller = bigger cluster sizes.
    float  _CloudWeatherStrength;   // 0 = no clustering (uniform coverage), 1 = full
                                    // weather-map control over local coverage.
    float  _CloudCurlScale;         // frequency of the curl-distortion field.
    float  _CloudCurlStrength;      // amplitude of UV warp from curl noise (in noise-
                                    // space units). 0 disables the swirl.

    float  _CloudLayer1Altitude;
    float  _CloudLayer2Altitude;
    float  _CloudLayer3Altitude;
    float  _CloudLayerHeightFalloff; // sharpness of altitude limit (low = soft transition)

    // --- Cloud lighting ---
    float  _CloudLightAbsorption;   // strength of fake self-shadowing from gradient
    float  _CloudPhaseG;            // forward-scatter Henyey-Greenstein g (sun glow)
    float  _CloudPhaseGBack;        // backward lobe g (negative recommended) — gives the
                                    // dim "back-lit" lobe when looking away from the sun.
    float  _CloudPhaseLobeMix;      // 0 = forward only, 1 = back only. ~0.5 = balanced.
    float  _CloudPhaseStrength;     // multiplier on phase contribution
    float  _CloudAmbient;           // ambient floor for shadowed cloud sides
    float  _CloudSunIntensity;      // brightness of sunlight on clouds, INDEPENDENT of the scene's
                                    // directional light intensity (clouds inherit the sun's color
                                    // hue but the magnitude comes from this property)
    float4 _CloudColor;             // overall cloud tint
    float4 _CloudShadowColor;       // tint of the shadowed (sun-blocked) side
    float4 _CloudAmbientSky;        // hemisphere-up ambient (sky bounce)
    float4 _CloudAmbientGround;     // hemisphere-down ambient (ground bounce — warm at sunset)
    float  _CloudPowderStrength;    // 0 = no powder (Beer's law only), 1 = full Beer-Powder.
                                    // Powder darkens thin lit regions, sharpening the
                                    // perceived edge of lit cloud bodies.
    float  _CloudMultiScatterA;     // multi-scatter approx — energy retained per octave
                                    // (0..1). 0 disables multi-scatter; ~0.5 looks great.
    float  _CloudMultiScatterB;     // multi-scatter approx — extinction reduction per octave.
    float  _CloudMultiScatterC;     // multi-scatter approx — phase narrowing per octave.

    // --- Self-shadow (internal volumetric feel) ---
    float  _CloudSelfShadowAbsorption;  // Beer-Lambert coefficient for the 2D lightmarch
    float  _CloudSelfShadowDistance;    // base step distance per sample (meters)
    int    _CloudSelfShadowSamples;     // number of samples along the sun direction
    float  _CloudSelfShadowDarknessFloor; // lower clamp on shadow transmittance
    float  _CloudSelfShadowConeRadius;  // cone-spread radius (fraction of step distance) —
                                        // 0 = straight march toward sun, ~0.3 = soft cone.
CBUFFER_END

// Resolve the world-space direction TOWARD the sun.
float3 GetSunDirection()
{
    if (_SunDirection.w > 0.5)
    {
        return normalize(_SunDirection.xyz);
    }
    return normalize(_MainLightPosition.xyz);
}

float3 GetSunColor()
{
    return _MainLightColor.rgb;
}

// Cloud-specific sunlight color. Returns the sun's COLOR HUE (so sunset oranges
// still tint the clouds correctly) but with magnitude controlled exclusively by
// _CloudSunIntensity — independent of the scene directional light's brightness.
float3 GetCloudSunLight()
{
    float3 c = GetSunColor();
    float maxC = max(max(c.r, c.g), max(c.b, 1e-5));
    return (c / maxC) * _CloudSunIntensity;
}

#endif // SKYBOX_COMMON_INCLUDED
