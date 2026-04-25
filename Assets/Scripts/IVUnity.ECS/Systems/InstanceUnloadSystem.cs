using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace IVUnity.ECS
{
    /// <summary>
    /// Finalizes entities leaving the stream range. Queries all Unloading roots, destroys
    /// their sub-mesh children (matched by Parent), decrements MeshCache refcounts, and
    /// flips the shared state back to Dormant so the root is cheap to iterate.
    /// </summary>
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(InstancePromotionSystem))]
    public partial class InstanceUnloadSystem : SystemBase
    {
        private MeshCache cache;

        public void Configure(MeshCache cache)
        {
            this.cache = cache;
        }

        protected override void OnUpdate()
        {
            // StreamingState must be declared in WithAll for SetSharedComponentFilter to work.
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, StreamingState>()
                .Build(EntityManager);
            query.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Unloading });

            if (query.IsEmpty) return;

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var refs     = query.ToComponentDataArray<ModelRef>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (cache != null)
            {
                // Find all SubMeshTag children whose Parent is one of the unloading roots.
                var childrenQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<SubMeshTag, Parent>()
                    .Build(EntityManager);
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

            EntityManager.SetSharedComponent(query, new StreamingState { Value = StreamingStateValue.Dormant });
        }
    }
}
