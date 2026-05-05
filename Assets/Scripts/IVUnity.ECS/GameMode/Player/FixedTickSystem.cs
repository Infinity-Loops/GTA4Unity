using Unity.Burst;
using Unity.Entities;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct FixedTickSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public uint Tick;
        }

        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<Singleton>())
            {
                var e = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(e, new Singleton());
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            singleton.Tick++;
        }
    }
}
