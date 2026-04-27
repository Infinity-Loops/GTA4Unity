using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    /// <summary>
    /// Pure distance-based streaming. No LOD suppression, no cull groups.
    /// Matches engine behavior: FUN_00aebff0 renders if distance &lt; drawDist * lodMultiplier.
    /// LOD visibility is handled entirely by LodFadeSystem via StippleAlpha.
    /// </summary>
    [UpdateInGroup(typeof(WorldStreamingSystemGroup))]
    [UpdateAfter(typeof(FocusPointSyncSystem))]
    public partial class StreamingDecisionSystem : SystemBase
    {
        private const float UnloadMargin = 1.15f;

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();
        }

        private float nextDiagTime;

        protected override void OnUpdate()
        {
            var focus = SystemAPI.GetSingleton<FocusPointData>();
            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            float lodScale = cfg.LodDistanceScale;

            ProcessDormant(focus, lodScale);
            ProcessLoaded(focus, lodScale);

            if (UnityEngine.Time.realtimeSinceStartup > nextDiagTime)
            {
                nextDiagTime = UnityEngine.Time.realtimeSinceStartup + 5f;
                LogDiagnostics();
            }
        }

        private void ProcessDormant(FocusPointData focus, float lodScale)
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, StreamingState, DrawDist>()
                .Build(EntityManager);
            query.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Dormant });
            if (query.IsEmpty) return;

            using var entities   = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var drawDists  = query.ToComponentDataArray<DrawDist>(Allocator.Temp);

            float3 cam = focus.Position;

            for (int i = 0; i < entities.Length; i++)
            {
                float3 pos = transforms[i].Position;
                float dx = pos.x - cam.x;
                float dz = pos.z - cam.z;
                float dist = math.sqrt(dx * dx + dz * dz);

                if (dist < drawDists[i].Value * lodScale)
                {
                    EntityManager.SetSharedComponent(entities[i],
                        new StreamingState { Value = StreamingStateValue.Pending });
                }
            }
        }

        private void ProcessLoaded(FocusPointData focus, float lodScale)
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, StreamingState, DrawDist>()
                .Build(EntityManager);
            query.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            if (query.IsEmpty) return;

            using var entities   = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var drawDists  = query.ToComponentDataArray<DrawDist>(Allocator.Temp);

            float3 cam = focus.Position;

            for (int i = 0; i < entities.Length; i++)
            {
                float3 pos = transforms[i].Position;
                float dx = pos.x - cam.x;
                float dz = pos.z - cam.z;
                float dist = math.sqrt(dx * dx + dz * dz);

                if (dist >= drawDists[i].Value * lodScale * UnloadMargin)
                {
                    EntityManager.SetSharedComponent(entities[i],
                        new StreamingState { Value = StreamingStateValue.Unloading });
                }
            }
        }

        private void LogDiagnostics()
        {
            var em = EntityManager;
            int dormant = 0, pending = 0, loaded = 0, unloading = 0;

            foreach (var sv in new[] {
                StreamingStateValue.Dormant, StreamingStateValue.Pending,
                StreamingStateValue.Loaded, StreamingStateValue.Unloading })
            {
                var q = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<WorldInstanceTag, StreamingState>()
                    .Build(em);
                q.SetSharedComponentFilter(new StreamingState { Value = sv });
                int c = q.CalculateEntityCount();
                switch (sv)
                {
                    case StreamingStateValue.Dormant:   dormant   = c; break;
                    case StreamingStateValue.Pending:    pending   = c; break;
                    case StreamingStateValue.Loaded:     loaded    = c; break;
                    case StreamingStateValue.Unloading:  unloading = c; break;
                }
            }

            var loadedLodQ = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LodTag, StreamingState>()
                .Build(em);
            loadedLodQ.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            int loadedLod = loadedLodQ.CalculateEntityCount();

            UnityEngine.Debug.Log(
                $"[Streaming] Dormant={dormant} Pending={pending} " +
                $"Loaded={loaded}(HD={loaded - loadedLod} LOD={loadedLod}) " +
                $"Unloading={unloading}");
        }
    }
}
