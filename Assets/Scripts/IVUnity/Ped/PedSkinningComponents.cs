using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.Ped
{
    public struct SkinnedMeshBoneRef : IBufferElementData
    {
        public Entity BoneEntity;
    }

    public struct SkinnedMeshBindPose : IBufferElementData
    {
        public float4x4 Value;
    }

    public struct SkinnedMeshRootEntity : IComponentData
    {
        public Entity Value;
    }
}
