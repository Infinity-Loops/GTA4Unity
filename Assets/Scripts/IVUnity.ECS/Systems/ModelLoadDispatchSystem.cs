using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    [UpdateInGroup(typeof(WorldStreamingSystemGroup))]
    [UpdateAfter(typeof(StreamingDecisionSystem))]
    public partial class ModelLoadDispatchSystem : SystemBase
    {
        private ModelCatalog catalog;
        private MeshCache    meshCache;
        private ModelLoader  loader;

        private EntityQuery lodPendingQuery;
        private EntityQuery hdPendingQuery;
        private readonly HashSet<uint> seen = new HashSet<uint>();

        public void Configure(ModelCatalog cat, MeshCache cache, ModelLoader loader)
        {
            this.catalog = cat;
            this.meshCache = cache;
            this.loader = loader;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<StreamingConfig>();

            lodPendingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, StreamingState, LodTag, LocalTransform>()
                .Build(EntityManager);

            hdPendingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, StreamingState, LocalTransform>()
                .WithNone<LodTag>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            if (catalog == null || meshCache == null || loader == null) return;

            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            int budget = cfg.MaxLoadsPerFrame;
            seen.Clear();

            budget = DispatchPending(lodPendingQuery, budget);
            if (budget > 0)
                DispatchPending(hdPendingQuery, budget);
        }

        private int DispatchPending(EntityQuery query, int budget)
        {
            query.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Pending });

            if (query.IsEmpty) { query.ResetFilter(); return budget; }

            using var modelRefs  = query.ToComponentDataArray<ModelRef>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            query.ResetFilter();

            var focus = SystemAPI.GetSingleton<FocusPointData>();
            int count = modelRefs.Length;

            using var order = SortByDistance(transforms, focus.Position);

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

        private static NativeArray<int> SortByDistance(NativeArray<LocalTransform> transforms, float3 focus)
        {
            int n = transforms.Length;
            var pairs = new NativeArray<float2>(n, Allocator.Temp);
            for (int i = 0; i < n; i++)
            {
                float dx = transforms[i].Position.x - focus.x;
                float dz = transforms[i].Position.z - focus.z;
                pairs[i] = new float2(dx * dx + dz * dz, math.asfloat(i));
            }
            pairs.Sort(new DistComparer());

            var indices = new NativeArray<int>(n, Allocator.Temp);
            for (int i = 0; i < n; i++)
                indices[i] = math.asint(pairs[i].y);
            pairs.Dispose();
            return indices;
        }

        private struct DistComparer : IComparer<float2>
        {
            public int Compare(float2 a, float2 b) => a.x.CompareTo(b.x);
        }
    }
}
