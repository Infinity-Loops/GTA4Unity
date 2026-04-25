#ifndef SKYBOX_NOISE_INCLUDED
#define SKYBOX_NOISE_INCLUDED

// Noise primitives — standalone, no engine dependencies.
//
// Mix of techniques used by the cloud renderer:
//   - Value-noise FBM is the smooth scalar potential we differentiate to get curl.
//   - Worley (cellular) noise gives the puffy billows that read as cumulus.
//   - Perlin-Worley hybrid (Guerrilla "Nubis" R-channel) is the base cumulus shape.
//   - A very-low-frequency FBM is sampled as a 2D weather map controlling coverage
//     across the sky, producing cloud clusters and clear gaps instead of uniform
//     sprinkle.

// ============================================================================
//  Hashes
// ============================================================================

float SkyHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// 2D → 2D hash, used for jittering Worley feature points.
float2 SkyHash22(float2 p)
{
    float n = sin(dot(p, float2(127.1, 311.7)));
    return frac(float2(262144.0, 32768.0) * n);
}

// ============================================================================
//  Value noise + FBM (smooth, used as Perlin-substitute and curl potential)
// ============================================================================

float SkyValueNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = SkyHash21(i + float2(0, 0));
    float b = SkyHash21(i + float2(1, 0));
    float c = SkyHash21(i + float2(0, 1));
    float d = SkyHash21(i + float2(1, 1));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 5-octave FBM. Output ~[0, 1].
float SkyFBM2D(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        v += a * SkyValueNoise2D(p);
        p  = p * 2.03;
        a *= 0.5;
    }
    return v;
}

// ============================================================================
//  Worley (cellular) noise — F1 distance to nearest jittered feature point
// ============================================================================

// Returns ~[0, 1] where 0 = at a feature point, 1 = far from any. Inverting (1 - x)
// gives the classic puffy "bubble" look.
float SkyWorley2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float minDist = 1.0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y);
            float2 o = SkyHash22(i + g);
            float2 r = g + o - f;
            float d = dot(r, r);          // squared distance, cheaper
            minDist = min(minDist, d);
        }
    }
    return saturate(sqrt(minDist));
}

// 3-octave Worley FBM. Output ~[0, 1].
float SkyWorleyFBM2D(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        v += a * SkyWorley2D(p);
        p  = p * 2.07;
        a *= 0.5;
    }
    return v;
}

// ============================================================================
//  Perlin-Worley hybrid — base cumulus shape
// ============================================================================
//
// Guerrilla pack this into the R channel of the 3D shape texture: a Perlin field
// remapped by an inverted-Worley to bias high values into puffy billowy regions.
// Implemented here in 2D using value-noise FBM as the Perlin substitute.

float SkyPerlinWorley2D(float2 p, float mix01)
{
    float perlin = SkyFBM2D(p);
    float worley = 1.0 - SkyWorleyFBM2D(p * 1.13);   // invert: 1 at billow center
    // Smooth blend toward the puffier shape as mix01 increases.
    return saturate(lerp(perlin, worley, saturate(mix01)));
}

// ============================================================================
//  Curl noise — divergence-free 2D vector field for UV warping
// ============================================================================
//
// The 2D curl of a scalar potential phi is (dPhi/dy, -dPhi/dx). Sampling at low
// frequency gives broad swirls that wind-shear the cloud edges instead of leaving
// them axis-aligned. Divergence-free means it doesn't pump density into / out of
// regions on average — pure stirring.

float2 SkyCurl2D(float2 p)
{
    const float h = 0.1;
    float n0 = SkyValueNoise2D(p + float2( h, 0.0));
    float n1 = SkyValueNoise2D(p - float2( h, 0.0));
    float n2 = SkyValueNoise2D(p + float2(0.0,  h));
    float n3 = SkyValueNoise2D(p - float2(0.0,  h));
    float dPhiDx = (n0 - n1) / (2.0 * h);
    float dPhiDy = (n2 - n3) / (2.0 * h);
    return float2(dPhiDy, -dPhiDx);
}

// ============================================================================
//  Weather map — very-low-frequency coverage modulation
// ============================================================================
//
// 3-octave fBm sampled at the weather scale. The coverage threshold per pixel is
// modulated by this value so dense regions cluster and clear sky opens between
// them, rather than the homogeneous sprinkle a single global threshold produces.

float SkyWeatherMap2D(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        v += a * SkyValueNoise2D(p);
        p  = p * 2.11;
        a *= 0.5;
    }
    return v;
}

#endif // SKYBOX_NOISE_INCLUDED
