using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace IVUnity.ECS
{
    /// <summary>
    /// Walks Pending entities, groups by ModelHash, dispatches async parse jobs for any
    /// unique model not yet in the MeshCache. Bounded to StreamingConfig.MaxLoadsPerFrame
    /// dispatches per frame — keeps the worker pool from getting flooded.
    /// </summary>
    [UpdateInGroup(typeof(WorldStreamingSystemGroup))]
    [UpdateAfter(typeof(CellActivationSystem))]
    public partial class ModelLoadDispatchSystem : SystemBase
    {
        private ModelCatalog catalog;
        private MeshCache    meshCache;
        private ModelLoader  loader;

        public void Configure(ModelCatalog cat, MeshCache cache, ModelLoader loader)
        {
            this.catalog = cat;
            this.meshCache = cache;
            this.loader = loader;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<StreamingConfig>();
        }

        protected override void OnUpdate()
        {
            if (catalog == null || meshCache == null || loader == null) return;

            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            int budget = cfg.MaxLoadsPerFrame;

            var pending = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, StreamingState>()
                .Build(EntityManager);
            pending.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Pending });

            if (pending.IsEmpty) return;

            using var modelRefs = pending.ToComponentDataArray<ModelRef>(Allocator.Temp);
            var seen = new HashSet<uint>();

            for (int i = 0; i < modelRefs.Length && budget > 0; i++)
            {
                uint hash = modelRefs[i].ModelHash;
                if (!seen.Add(hash)) continue;

                var entry = meshCache.GetOrCreate(hash);
                if (entry.State != ModelLoadState.NotLoaded) continue;

                if (!catalog.TryGet(hash, out var catEntry))
                {
                    entry.State = ModelLoadState.Failed;
                    continue;
                }

                entry.State = ModelLoadState.Loading;
                // Stamp dispatch time so MeshCacheEvictionSystem can detect orphaned
                // Loading entries (worker running long after the player moved on) and
                // reset them, preventing the "stuck on revisit" failure mode.
                entry.LastUsedTime = UnityEngine.Time.realtimeSinceStartup;
                loader.Enqueue(hash, catEntry);
                budget--;
            }
        }
    }
}
