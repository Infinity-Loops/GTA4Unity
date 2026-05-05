using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct CalculateSkinMatrixSystem : ISystem
    {
        ComponentLookup<LocalToWorld> _ltwLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ltwLookup = state.GetComponentLookup<LocalToWorld>(true);
            state.RequireForUpdate<SkinnedMeshRootEntity>();
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ltwLookup.Update(ref state);

            new CalcSkinMatricesJob
            {
                LtwLookup = _ltwLookup,
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct CalcSkinMatricesJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;

            void Execute(
                ref DynamicBuffer<SkinMatrix> skinMatrices,
                in DynamicBuffer<SkinnedMeshBoneRef> bones,
                in DynamicBuffer<SkinnedMeshBindPose> bindPoses,
                in SkinnedMeshRootEntity rootEntity)
            {
                if (!LtwLookup.HasComponent(rootEntity.Value))
                    return;

                float4x4 rootInv = math.inverse(LtwLookup[rootEntity.Value].Value);

                for (int i = 0; i < skinMatrices.Length && i < bones.Length && i < bindPoses.Length; i++)
                {
                    Entity boneEnt = bones[i].BoneEntity;
                    if (boneEnt == Entity.Null || !LtwLookup.HasComponent(boneEnt))
                    {
                        skinMatrices[i] = new SkinMatrix
                        {
                            Value = new float3x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0)
                        };
                        continue;
                    }

                    float4x4 boneWorld = LtwLookup[boneEnt].Value;

                    // First frame: LTW may be uninitialized (all zeros) — use identity
                    if (boneWorld.c3.w == 0f)
                    {
                        skinMatrices[i] = new SkinMatrix
                        {
                            Value = new float3x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0)
                        };
                        continue;
                    }

                    float4x4 skinMat = math.mul(math.mul(rootInv, boneWorld), bindPoses[i].Value);

                    skinMatrices[i] = new SkinMatrix
                    {
                        Value = new float3x4(skinMat.c0.xyz, skinMat.c1.xyz, skinMat.c2.xyz, skinMat.c3.xyz)
                    };
                }
            }
        }
    }
}
