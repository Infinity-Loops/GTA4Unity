#ifndef GTA_STIPPLE_INCLUDED
#define GTA_STIPPLE_INCLUDED

// ---------------------------------------------------------------------------
// Stipple dithering for LOD crossfade — engine-faithful implementation.
//
// The engine uses globalScalars.x as per-entity alpha (0=invisible, 1=opaque)
// and samples a StippleTexture with screen-space tiling to discard pixels in
// an ordered pattern. We use a 4x4 Bayer matrix (same visual result).
//
// Ref: gta_defaultPS2.asm lines 30-44, globalScalars c39 register.
// ---------------------------------------------------------------------------

static const float BayerMatrix[16] = {
     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
};

void GTA_StippleClip(float2 screenPos, float stippleAlpha)
{
    if (stippleAlpha >= 0.999) return;
    if (stippleAlpha <= 0.001) { clip(-1); return; }
    uint2 pixel = uint2(screenPos) % 4;
    float threshold = BayerMatrix[pixel.y * 4 + pixel.x];
    clip(stippleAlpha - threshold - 0.001);
}

// GTA_ApplyStippleFade removed — call GTA_StippleClip(positionCS.xy, _StippleAlpha)
// directly in each shader's fragment, AFTER the CBUFFER/DOTS block declares _StippleAlpha.

#endif // GTA_STIPPLE_INCLUDED
