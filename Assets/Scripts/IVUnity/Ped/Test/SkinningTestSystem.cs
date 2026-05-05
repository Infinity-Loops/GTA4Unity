using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace IVUnity.Ped
{
    /// <summary>
    /// Temporary test: directly writes to SkinMatrix buffer to verify deformation pipeline.
    /// Disabled by default — enable manually to bypass CalculateSkinMatrixSystem for testing.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CalculateSkinMatrixSystem))]
    public partial class SkinningTestSystem : SystemBase
    {
        protected override void OnCreate()
        {
            Enabled = false;
            RequireForUpdate<SkinnedMeshRootEntity>();
        }

        protected override void OnUpdate()
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            float angle = math.sin(time * 2f) * 0.5f;

            var query = SystemAPI.QueryBuilder().WithAll<SkinnedMeshRootEntity, SkinMatrix>().Build();
            if (query.IsEmpty) return;

            var entity = query.GetSingletonEntity();
            var skinMatrices = EntityManager.GetBuffer<SkinMatrix>(entity);

            float3x4 rotMat = new float3x4(
                math.cos(angle), 0, math.sin(angle), 0,
                0, 1, 0, 0,
                -math.sin(angle), 0, math.cos(angle), 0);

            for (int i = 0; i < skinMatrices.Length; i++)
                skinMatrices[i] = new SkinMatrix { Value = rotMat };
        }
    }
}
