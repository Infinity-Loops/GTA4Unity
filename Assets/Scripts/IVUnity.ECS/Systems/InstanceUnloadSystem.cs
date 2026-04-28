using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace IVUnity.ECS
{
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(InstancePromotionSystem))]
    public partial class InstanceUnloadSystem : SystemBase
    {
        private MeshCache cache;
        private EntityQuery unloadingQuery;

        public void Configure(MeshCache cache)
        {
            this.cache = cache;
        }

        protected override void OnCreate()
        {
            unloadingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, StreamingState>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            unloadingQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Unloading });

            if (unloadingQuery.IsEmpty) { unloadingQuery.ResetFilter(); return; }

            using var entities = unloadingQuery.ToEntityArray(Allocator.Temp);
            using var refs     = unloadingQuery.ToComponentDataArray<ModelRef>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (cache != null)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (EntityManager.HasBuffer<Child>(entities[i]))
                    {
                        var children = EntityManager.GetBuffer<Child>(entities[i]);
                        for (int c = 0; c < children.Length; c++)
                            ecb.DestroyEntity(children[c].Value);
                    }

                    if (cache.TryGet(refs[i].ModelHash, out var entry))
                        entry.RefCount = System.Math.Max(0, entry.RefCount - 1);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            EntityManager.SetSharedComponent(unloadingQuery, new StreamingState { Value = StreamingStateValue.Dormant });
            unloadingQuery.ResetFilter();
        }
    }
}
