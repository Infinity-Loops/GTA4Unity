using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    /// <summary>
    /// Priority-based dispatch of async model parse jobs. Two passes per frame:
    ///   1. LOD models first (LodTag) — they're small, fast, and provide the initial
    ///      world view (skyline, distant islands). GTA IV loads these before anything else.
    ///   2. HD models second, sorted by distance to camera (closest first).
    ///
    /// Within each pass, unique model hashes are dispatched up to MaxLoadsPerFrame total.
    /// Models already in MeshCache (Loading/Loaded/Failed) are skipped.
    /// </summary>
    [UpdateInGroup(typeof(WorldStreamingSystemGroup))]
    [UpdateAfter(typeof(StreamingDecisionSystem))]
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
            var seen = new HashSet<uint>();

            // --- Pass 1: LOD models (highest priority) ---
            budget = DispatchPending(
                withLodTag: true,
                budget, seen);

            // --- Pass 2: everything else (HD + standalone), closest first ---
            if (budget > 0)
            {
                budget = DispatchPending(
                    withLodTag: false,
                    budget, seen);
            }
        }

        private int DispatchPending(bool withLodTag, int budget, HashSet<uint> seen)
        {
            EntityQuery query;
            if (withLodTag)
            {
                query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<WorldInstanceTag, ModelRef, StreamingState, LodTag, LocalTransform>()
                    .Build(EntityManager);
            }
            else
            {
                query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<WorldInstanceTag, ModelRef, StreamingState, LocalTransform>()
                    .WithNone<LodTag>()
                    .Build(EntityManager);
            }
            query.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Pending });

            if (query.IsEmpty) return budget;

            using var modelRefs  = query.ToComponentDataArray<ModelRef>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Both passes sort by distance — closest first. For LODs this means nearby
            // islands/buildings get their LOD before distant skyline. For HD this means
            // objects around the player load before distant ones.
            var focus = SystemAPI.GetSingleton<FocusPointData>();
            var order = SortByDistance(transforms, focus.Position);

            int count = modelRefs.Length;
            for (int idx = 0; idx < count && budget > 0; idx++)
            {
                int i = order[idx];
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
                entry.LastUsedTime = UnityEngine.Time.realtimeSinceStartup;
                loader.Enqueue(hash, catEntry);
                budget--;
            }

            return budget;
        }

        private static int[] SortByDistance(NativeArray<LocalTransform> transforms, float3 focus)
        {
            int n = transforms.Length;
            var indices = new int[n];
            var dists   = new float[n];
            for (int i = 0; i < n; i++)
            {
                indices[i] = i;
                float dx = transforms[i].Position.x - focus.x;
                float dz = transforms[i].Position.z - focus.z;
                dists[i] = dx * dx + dz * dz;
            }
            System.Array.Sort(dists, indices);
            return indices;
        }
    }
}
