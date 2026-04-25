#ifndef SKYBOX_CLOUDS_INCLUDED
#define SKYBOX_CLOUDS_INCLUDED

#include "SkyboxCommon.hlsl"
#include "SkyboxNoise.hlsl"

// 2D layered cloud renderer with Guerrilla "Nubis"-style shaping and lighting.
//
// Why 2D and not raymarched volumetric:
//   The viewer is always BELOW the cloud layer in a skybox — they never fly
//   through clouds. Under that constraint, 2D noise sampled at the world-space
//   position where each view ray hits an altitude plane gives identical visual
//   results to a true volumetric march, at constant per-pixel cost. No banding,
//   no chunkiness, no sample-count tradeoff.
//
// Multiple layers at different altitudes give parallax depth — clouds higher up
// appear smaller and move at different rates, the way real cumulus stacks do.
//
// Shape pipeline (per pixel):
//   weather map (low freq) → modulates local coverage threshold
//        ↓
//   curl-noise UV warp     → swirly, wind-sheared edges (divergence-free)
//        ↓
//   Perlin-Worley FBM      → puffy cumulus billows as base shape
//        ↓
//   coverage smoothstep    → soft binary mask
//        ↓
//   inverted-Worley erode  → bubbly subtraction at edges (keeps body, frays edges)
//
// Lighting pipeline:
//   gradient "normal" + wrap-shaded NdotL    → cohesive directional shading
//   cone-sampled self-shadow (Beer-Lambert)  → interior darkening for "volume" feel
//   dual-lobe Henyey-Greenstein              → forward sun-glow + backward lobe
//   Beer-Powder term                         → defined edge contrast on lit side
//   3-octave multi-scatter approximation     → bright bodies, less flat shading
//   hemisphere-gradient ambient              → warm bottom (ground) + cool top (sky)


// ============================================================================
//  Coverage sampling
// ============================================================================

// Sample cloud coverage at a 2D world-space XZ position. Returns 0..1 density.
float SampleCloudCoverageAt(float2 worldXZ, float windScale)
{
    float2 wind = _CloudWindSpeed.xz * (_Time.y * 0.01 * windScale);

    // Stage 1 — weather map. Low-frequency fBm modulates the local coverage
    // threshold so dense regions cluster and clear sky opens between them
    // instead of producing a uniform sprinkle.
    float weather = SkyWeatherMap2D(worldXZ * _CloudWeatherScale + wind * 0.5);
    // Remap weather around 1.0 so _CloudCoverage stays the global average.
    float coverageMod = lerp(1.0, weather * 2.0, saturate(_CloudWeatherStrength));
    float coverage    = saturate(_CloudCoverage * coverageMod);
    if (coverage <= 0.001) return 0.0;

    // Stage 2 — curl-noise UV warp. Stirs the sample position with a divergence-
    // free swirl, producing wind-sheared cloud edges instead of axis-aligned blobs.
    float2 curl = SkyCurl2D(worldXZ * _CloudCurlScale + wind * 0.25);
    float2 q    = (worldXZ + curl * _CloudCurlStrength) * _CloudScale + wind;

    // Stage 3 — Perlin-Worley base shape. Worley contribution gives the puffy
    // cumulus billow look; Perlin keeps it from revealing the cell grid.
    float baseShape = SkyPerlinWorley2D(q, _CloudPerlinWorleyMix);

    // Stage 4 — coverage threshold (smoothstep band, NOT saturate-divide).
    float threshold = 1.0 - coverage;
    float softness  = max(_CloudEdgeSoftness, 1e-4);
    float density   = smoothstep(threshold - softness, threshold + softness, baseShape);
    if (density <= 0.0) return 0.0;

    // Stage 5 — inverted-Worley detail erosion. Bubbly subtraction at edges:
    // strong where density is low (frays), gentle where high (preserves body).
    float detail  = 1.0 - SkyWorleyFBM2D(q * _CloudDetailScale);
    float erosion = detail * _CloudDetailWeight * (1.0 - density);
    density = smoothstep(0.0, softness * 2.0, density - erosion);

    return density;
}

// Project the view ray onto an altitude plane and sample cloud density there.
float SampleCloudLayer(float3 viewDir, float altitude, float windScale)
{
    float t = altitude / max(viewDir.y, 0.0001);
    if (t <= 0) return 0.0;

    float3 p = viewDir * t;
    return SampleCloudCoverageAt(p.xz, windScale);
}


// ============================================================================
//  Phase functions
// ============================================================================

float CloudHGPhase(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-5), 1.5);
    return (1.0 - g2) / (4.0 * SKY_PI * denom);
}

// Dual-lobe Henyey-Greenstein. Real cloud particles produce a strong forward
// scatter peak (silver lining when sun is behind) and a smaller backward lobe
// (the cloud you're looking at glows faintly when looking AWAY from the sun
// because light scattered back from far cloud reaches you). One HG can't model
// both — sum two with opposite-signed g.
float CloudDualLobeHGPhase(float cosTheta)
{
    float fwd  = CloudHGPhase(cosTheta,  _CloudPhaseG);
    float back = CloudHGPhase(cosTheta,  _CloudPhaseGBack);
    return lerp(fwd, back, saturate(_CloudPhaseLobeMix));
}


// ============================================================================
//  Self-shadow — cone-sampled 2D lightmarch toward the sun
// ============================================================================
//
// At each step, sample cloud density and accumulate optical depth, then convert
// to transmittance via Beer's law. This is what gives the "internal volumetric
// feel" — areas behind dense regions (relative to the sun) get visibly darker.
//
// Cone sampling spreads the steps in a small disc around the sun direction.
// Pure-line march reads aliasing from the same noise points twice; cone steps
// average over a wider region and produce softer, perceptually-correct shadow
// edges. The cone widens linearly with distance so far samples cover a larger
// region than near samples — same idea as Guerrilla's 6-sample 3D cone.

static const float2 kCloudConeOffsets[8] =
{
    float2( 0.000,  0.000),
    float2( 0.707, -0.707),
    float2(-0.707,  0.707),
    float2( 0.500,  0.866),
    float2(-0.500, -0.866),
    float2( 0.866, -0.500),
    float2(-0.866,  0.500),
    float2( 0.000,  1.000),
};

float SampleCloudSelfShadow(float2 worldXZ, float3 sunDir, float windScale)
{
    int n = max(1, _CloudSelfShadowSamples);

    // Walk TOWARD where the light comes from — opposite of sunDir's XZ.
    float2 sunBackXZ = -sunDir.xz;

    // Build an orthonormal 2D basis (forward + right) so cone offsets are
    // expressed relative to the sun direction in the XZ plane.
    float2 forward = (length(sunBackXZ) > 1e-4) ? normalize(sunBackXZ) : float2(1.0, 0.0);
    float2 right   = float2(-forward.y, forward.x);

    // Stretch the step when the sun is low (more horizontal traversal per unit of light path).
    float sunY = max(sunDir.y, 0.05);
    float stepDist = _CloudSelfShadowDistance / sunY;

    float opticalDepth = 0.0;
    [unroll]
    for (int i = 1; i <= 8; i++)
    {
        if (i > n) break;

        // Cone widens with distance — far steps cover a larger area than near ones.
        float fi      = float(i);
        float fwdDist = stepDist * fi;
        float radius  = stepDist * fi * _CloudSelfShadowConeRadius;
        float2 off    = kCloudConeOffsets[i - 1];

        float2 p = worldXZ
                 + forward * fwdDist
                 + (forward * off.y + right * off.x) * radius;

        opticalDepth += SampleCloudCoverageAt(p, windScale);
    }

    float transmittance = exp(-opticalDepth * stepDist * 0.001 * _CloudSelfShadowAbsorption);
    return _CloudSelfShadowDarknessFloor +
           transmittance * (1.0 - _CloudSelfShadowDarknessFloor);
}


// ============================================================================
//  Fake "cloud normal" from coverage gradient
// ============================================================================
//
// Sample distance (h) is large (200m) on purpose: small h reads high-frequency
// detail noise as part of the gradient, which produces patchy, noisy normals.
// Big h averages over many noise periods and gives a smooth, large-scale gradient
// that yields cohesive shading across the cloud silhouette.

float3 CloudNormalFromGradient(float3 viewDir, float altitude)
{
    const float h = 200.0;
    float t = altitude / max(viewDir.y, 0.0001);
    float3 p = viewDir * t;

    float center = SampleCloudCoverageAt(p.xz,                  1.0);
    float dx     = SampleCloudCoverageAt(p.xz + float2(h, 0.0), 1.0);
    float dz     = SampleCloudCoverageAt(p.xz + float2(0.0, h), 1.0);

    float2 grad = float2(dx - center, dz - center) / h;

    return normalize(float3(-grad.x, _CloudLightAbsorption, -grad.y));
}


// ============================================================================
//  Beer-Powder & multi-scatter approximation
// ============================================================================
//
// Beer's law alone darkens densely-lit regions monotonically — thin lit cloud
// looks the same brightness as thick lit cloud, which reads as flat. The
// "powder" term `1 - exp(-2 * d)` rises from 0 at d=0 to 1 at high density,
// darkening thin regions so the lit side of a cloud has visible contrast at
// its edges. Wrenninge / Bouthors (2008), used by Guerrilla for the lit lobe.
float BeerPowder(float density)
{
    float beer    = exp(-density * _CloudSelfShadowAbsorption);
    float powder  = 1.0 - exp(-density * 2.0);
    return lerp(beer, beer * powder * 2.0, saturate(_CloudPowderStrength));
}

// Multi-scattering approximation (Wrenninge / Schneider). Sums N Beer-Lambert
// terms with progressively reduced extinction, energy and phase anisotropy.
// Approximates the effect of light bouncing multiple times inside the cloud,
// which is what makes real cumulus bodies appear bright instead of pure-Beer
// dark — the integrand of true multi-scatter has no closed form, but this
// geometric series matches it well in practice.
//
//   I(N) = sum_{n=0..N-1} a^n * exp(-density * extinction * b^n)
//                                * phase(cosTheta, g * c^n)
float CloudMultiScatter(float density, float cosTheta, float NdotL)
{
    float a = saturate(_CloudMultiScatterA);
    float b = saturate(_CloudMultiScatterB);
    float c = saturate(_CloudMultiScatterC);

    float energy   = 1.0;
    float ext      = 1.0;
    float gFwd     = _CloudPhaseG;
    float gBack    = _CloudPhaseGBack;
    float lobeMix  = saturate(_CloudPhaseLobeMix);
    float total    = 0.0;

    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float beer   = exp(-density * _CloudSelfShadowAbsorption * ext);
        float powder = 1.0 - exp(-density * 2.0 * ext);
        float lit    = lerp(beer, beer * powder * 2.0, saturate(_CloudPowderStrength));

        float phase  = lerp(CloudHGPhase(cosTheta, gFwd),
                            CloudHGPhase(cosTheta, gBack),
                            lobeMix);

        // NdotL for the surface-like wrap term + phase for sun-direction glow.
        // Both contribute on each octave but with reduced amplitude.
        total += energy * lit * (NdotL + phase * _CloudPhaseStrength);

        energy *= a;
        ext    *= b;
        gFwd   *= c;
        gBack  *= c;
    }

    return total;
}


// ============================================================================
//  Hemisphere ambient
// ============================================================================
//
// Sky-bounce ambient hits the top of the cloud (cool blue), ground-bounce hits
// the bottom (warmer at sunset). Mix by view direction Y so clouds overhead
// receive a different ambient than clouds near the horizon.

float3 CloudAmbientHemisphere(float3 viewDir)
{
    float t = saturate(viewDir.y * 0.5 + 0.5);
    return lerp(_CloudAmbientGround.rgb, _CloudAmbientSky.rgb, t);
}


// ============================================================================
//  Render
// ============================================================================

struct CloudResult
{
    float3 scatteredLight;
    float  transmittance;   // 1 = clear, 0 = fully opaque
};

CloudResult RenderClouds(float3 viewDir, float3 sunDir, float3 sunColor)
{
    CloudResult result;
    result.scatteredLight = 0.0.xxx;
    result.transmittance  = 1.0;

    if (viewDir.y < 0.02) return result;

    float horizonFade = smoothstep(0.02, 0.18, viewDir.y);

    // Three layers at successive altitudes for parallax / depth.
    float density1 = SampleCloudLayer(viewDir, _CloudLayer1Altitude, 1.0);
    float density2 = SampleCloudLayer(viewDir, _CloudLayer2Altitude, 1.5);
    float density3 = SampleCloudLayer(viewDir, _CloudLayer3Altitude, 2.0);

    float density = 1.0 - (1.0 - density1) * (1.0 - density2) * (1.0 - density3);
    density *= _CloudDensity * horizonFade;
    if (density <= 0.001) return result;

    // Lighting from the dominant (densest) layer's gradient and self-shadow.
    float dominantAltitude;
    float dominantWindScale;
    if (density1 >= density2 && density1 >= density3)
    {
        dominantAltitude  = _CloudLayer1Altitude;
        dominantWindScale = 1.0;
    }
    else if (density2 >= density3)
    {
        dominantAltitude  = _CloudLayer2Altitude;
        dominantWindScale = 1.5;
    }
    else
    {
        dominantAltitude  = _CloudLayer3Altitude;
        dominantWindScale = 2.0;
    }

    float3 normal = CloudNormalFromGradient(viewDir, dominantAltitude);

    float t = dominantAltitude / max(viewDir.y, 0.0001);
    float2 hitXZ = (viewDir * t).xz;
    float selfShadow = SampleCloudSelfShadow(hitXZ, sunDir, dominantWindScale);

    float cosTheta = dot(viewDir, sunDir);

    // Wrap lighting (NOT standard Lambert): clouds are translucent volumes, not
    // opaque surfaces. Map [-1, 1] dot product into [0, 1] linearly.
    float NdotL = saturate(dot(normal, sunDir) * 0.5 + 0.5);

    // Multi-scatter sum stands in for direct + phase contribution. Each octave
    // bakes in NdotL + phase already, so we just scale by self-shadow and the
    // sun color.
    float3 cloudSun = GetCloudSunLight();
    float scatter   = CloudMultiScatter(density, cosTheta, NdotL);
    float3 sunLit   = cloudSun * scatter * selfShadow;

    // Ambient — hemisphere gradient, NOT occluded by self-shadow (represents
    // sky/ground bounce that doesn't pass through the cloud volume) but tinted
    // toward the shadow color in deeper interiors so we still get a moody base.
    float3 hemi      = CloudAmbientHemisphere(viewDir);
    float3 shadowAmb = _CloudShadowColor.rgb;
    float3 ambient   = lerp(shadowAmb, hemi, selfShadow) * _CloudAmbient;

    float3 litColor = (ambient + sunLit) * _CloudColor.rgb;

    result.transmittance  = 1.0 - density;
    result.scatteredLight = litColor * density;

    return result;
}

#endif // SKYBOX_CLOUDS_INCLUDED
