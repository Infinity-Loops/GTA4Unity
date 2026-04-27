using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using IVUnity.ECS.GameMode;

namespace IVUnity.ECS
{
    /// <summary>
    /// Writes FocusPointData singleton from the Possessed entity's LocalTransform.
    /// Driven entirely by the GameMode system — no scene Transform references.
    /// </summary>
    [UpdateInGroup(typeof(WorldStreamingSystemGroup), OrderFirst = true)]
    public partial class FocusPointSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<FocusPointData>()) return;

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Possessed>())
            {
                SystemAPI.SetSingleton(new FocusPointData { Position = transform.ValueRO.Position });
                return;
            }
        }
    }
}
