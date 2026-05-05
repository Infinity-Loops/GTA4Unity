#ifndef GTA_SKINNING_INCLUDED
#define GTA_SKINNING_INCLUDED

// Compute Deformation: the Entities Graphics compute shader reads SkinMatrix +
// original mesh data, writes deformed vertices to _DeformedMeshData. The vertex
// shader just reads the result by vertexID + _ComputeMeshIndex offset.

#ifdef UNITY_DOTS_INSTANCING_ENABLED

struct DeformedVertexData
{
    float3 Position;
    float3 Normal;
    float3 Tangent;
};

uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void GTA_ApplyComputeDeformation(uint vertexID, uint computeMeshIndex, inout float3 positionOS, inout float3 normalOS, inout float3 tangentOS)
{
    if (computeMeshIndex == 0) return;

    DeformedVertexData v = _DeformedMeshData[computeMeshIndex + vertexID];
    positionOS = v.Position;
    normalOS = v.Normal;
    tangentOS = v.Tangent;
}

#endif // UNITY_DOTS_INSTANCING_ENABLED
#endif // GTA_SKINNING_INCLUDED
