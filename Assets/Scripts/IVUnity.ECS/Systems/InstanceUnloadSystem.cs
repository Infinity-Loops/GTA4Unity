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
        private EntityQuery childrenQuery;

        public void Configure(MeshCache cache)
        {
            this.cache = cache;
        }

        protected override void OnCreate()
        {
            unloadingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, StreamingState>()
                .Build(EntityManager);

            childrenQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SubMeshTag, Parent>()
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
                using var childEntities = childrenQuery.ToEntityArray(Allocator.Temp);
                using var childParents  = childrenQuery.ToComponentDataArray<Parent>(Allocator.Temp);

                var unloadingSet = new NativeHashSet<Entity>(entities.Length, Allocator.Temp);
                for (int i = 0; i < entities.Length; i++) unloadingSet.Add(entities[i]);

                for (int i = 0; i < childEntities.Length; i++)
                {
                    if (unloadingSet.Contains(childParents[i].Value))
                    {
                        ecb.DestroyEntity(childEntities[i]);
                    }
                }
                unloadingSet.Dispose();

                for (int i = 0; i < entities.Length; i++)
                {
                    if (cache.TryGet(refs[i].ModelHash, out var entry))
                    {
                        entry.RefCount = System.Math.Max(0, entry.RefCount - 1);
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            EntityManager.SetSharedComponent(unloadingQuery, new StreamingState { Value = StreamingStateValue.Dormant });
            unloadingQuery.ResetFilter();
        }
    }
}
