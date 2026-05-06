using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    [UpdateInGroup(typeof(WorldStreamingSystemGroup))]
    [UpdateAfter(typeof(FocusPointSyncSystem))]
    public partial class StreamingDecisionSystem : SystemBase
    {
        private const float UnloadMargin = 1.15f;
        private const float MoveThresholdSq = 4f;
        private const int DormantInterval = 3;

        private EntityQuery dormantQuery;
        private EntityQuery loadedQuery;
        private EntityQuery diagCountQuery;
        private EntityQuery diagLodQuery;

        private float3 lastDormantPos;
        private int frameCounter;
        private EntityCommandBuffer pendingDormantEcb;
        private Unity.Jobs.JobHandle pendingDormantJob;
        private bool hasPendingDormant;

        private EntityCommandBuffer pendingLoadedEcb;
        private Unity.Jobs.JobHandle pendingLoadedJob;
        private bool hasPendingLoaded;

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();

            dormantQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, DrawDist>()
                .WithAllRW<StreamingState>()
                .Build(EntityManager);

            loadedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, DrawDist>()
                .WithAllRW<StreamingState>()
                .Build(EntityManager);

            lastDormantPos = new float3(float.MaxValue);

            diagCountQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, StreamingState>()
                .Build(EntityManager);

            diagLodQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LodTag, StreamingState>()
                .Build(EntityManager);
        }

        protected override void OnDestroy()
        {
            if (hasPendingDormant) { pendingDormantJob.Complete(); pendingDormantEcb.Dispose(); }
            if (hasPendingLoaded) { pendingLoadedJob.Complete(); pendingLoadedEcb.Dispose(); }
        }

        private float nextDiagTime;

        protected override void OnUpdate()
        {
            var focus = SystemAPI.GetSingleton<FocusPointData>();
            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            float lodScale = cfg.LodDistanceScale;

            // Flush previous frame's deferred ECBs
            if (hasPendingDormant)
            {
                pendingDormantJob.Complete();
                pendingDormantEcb.Playback(EntityManager);
                pendingDormantEcb.Dispose();
                hasPendingDormant = false;
            }
            if (hasPendingLoaded)
            {
                pendingLoadedJob.Complete();
                pendingLoadedEcb.Playback(EntityManager);
                pendingLoadedEcb.Dispose();
                hasPendingLoaded = false;
            }

            // Dormant check: only when focus moved enough AND every N frames
            frameCounter++;
            float3 delta = focus.Position - lastDormantPos;
            float moveSq = delta.x * delta.x + delta.z * delta.z;

            if (moveSq > MoveThresholdSq || frameCounter >= DormantInterval)
            {
                ProcessDormant(focus, lodScale);
                lastDormantPos = focus.Position;
                frameCounter = 0;
            }

            // Loaded check runs every frame (few entities, fast)
            ProcessLoaded(focus, lodScale);

            if (UnityEngine.Time.realtimeSinceStartup > nextDiagTime)
            {
                nextDiagTime = UnityEngine.Time.realtimeSinceStartup + 5f;
                LogDiagnostics();
            }
        }

        private void ProcessDormant(FocusPointData focus, float lodScale)
        {
            dormantQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Dormant });
            if (dormantQuery.IsEmpty) { dormantQuery.ResetFilter(); return; }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var job = new DormantCheckJob
            {
                CamX = focus.Position.x,
                CamZ = focus.Position.z,
                LodScale = lodScale,
                EntityHandle = GetEntityTypeHandle(),
                TransformHandle = GetComponentTypeHandle<LocalTransform>(true),
                DrawDistHandle = GetComponentTypeHandle<DrawDist>(true),
                BoundRadiusHandle = GetComponentTypeHandle<BoundRadius>(true),
                Ecb = ecb.AsParallelWriter(),
            }.ScheduleParallel(dormantQuery, Dependency);

            // Don't block — store for next frame playback
            pendingDormantEcb = ecb;
            pendingDormantJob = job;
            hasPendingDormant = true;
            Dependency = job;

            dormantQuery.ResetFilter();
        }

        private void ProcessLoaded(FocusPointData focus, float lodScale)
        {
            loadedQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            if (loadedQuery.IsEmpty) { loadedQuery.ResetFilter(); return; }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var job = new LoadedCheckJob
            {
                CamX = focus.Position.x,
                CamZ = focus.Position.z,
                LodScale = lodScale,
                UnloadMargin = UnloadMargin,
                EntityHandle = GetEntityTypeHandle(),
                TransformHandle = GetComponentTypeHandle<LocalTransform>(true),
                DrawDistHandle = GetComponentTypeHandle<DrawDist>(true),
                BoundRadiusHandle = GetComponentTypeHandle<BoundRadius>(true),
                Ecb = ecb.AsParallelWriter(),
            }.ScheduleParallel(loadedQuery, Dependency);

            pendingLoadedEcb = ecb;
            pendingLoadedJob = job;
            hasPendingLoaded = true;
            Dependency = job;

            loadedQuery.ResetFilter();
        }

        [BurstCompile]
        struct DormantCheckJob : IJobChunk
        {
            public float CamX;
            public float CamZ;
            public float LodScale;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<DrawDist> DrawDistHandle;
            [ReadOnly] public ComponentTypeHandle<BoundRadius> BoundRadiusHandle;

            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities   = chunk.GetNativeArray(EntityHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var drawDists  = chunk.GetNativeArray(ref DrawDistHandle);
                var hasBounds  = chunk.Has(ref BoundRadiusHandle);
                var bounds     = hasBounds ? chunk.GetNativeArray(ref BoundRadiusHandle) : default;

                for (int i = 0; i < chunk.Count; i++)
                {
                    float dx = transforms[i].Position.x - CamX;
                    float dz = transforms[i].Position.z - CamZ;
                    float distSq = dx * dx + dz * dz;
                    float radius = hasBounds ? bounds[i].Value : 0f;
                    float t = drawDists[i].Value * LodScale + radius;

                    if (distSq < t * t)
                    {
                        Ecb.SetSharedComponent(unfilteredChunkIndex, entities[i],
                            new StreamingState { Value = StreamingStateValue.Pending });
                    }
                }
            }
        }

        [BurstCompile]
        struct LoadedCheckJob : IJobChunk
        {
            public float CamX;
            public float CamZ;
            public float LodScale;
            public float UnloadMargin;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<DrawDist> DrawDistHandle;
            [ReadOnly] public ComponentTypeHandle<BoundRadius> BoundRadiusHandle;

            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities   = chunk.GetNativeArray(EntityHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var drawDists  = chunk.GetNativeArray(ref DrawDistHandle);
                var hasBounds  = chunk.Has(ref BoundRadiusHandle);
                var bounds     = hasBounds ? chunk.GetNativeArray(ref BoundRadiusHandle) : default;

                for (int i = 0; i < chunk.Count; i++)
                {
                    float dx = transforms[i].Position.x - CamX;
                    float dz = transforms[i].Position.z - CamZ;
                    float distSq = dx * dx + dz * dz;
                    float radius = hasBounds ? bounds[i].Value : 0f;
                    float t = (drawDists[i].Value * LodScale + radius) * UnloadMargin;

                    if (distSq >= t * t)
                    {
                        Ecb.SetSharedComponent(unfilteredChunkIndex, entities[i],
                            new StreamingState { Value = StreamingStateValue.Unloading });
                    }
                }
            }
        }

        private void LogDiagnostics()
        {
            int dormant = 0, pending = 0, loaded = 0, unloading = 0;

            diagCountQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Dormant });
            dormant = diagCountQuery.CalculateEntityCount();
            diagCountQuery.ResetFilter();

            diagCountQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Pending });
            pending = diagCountQuery.CalculateEntityCount();
            diagCountQuery.ResetFilter();

            diagCountQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            loaded = diagCountQuery.CalculateEntityCount();
            diagCountQuery.ResetFilter();

            diagCountQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Unloading });
            unloading = diagCountQuery.CalculateEntityCount();
            diagCountQuery.ResetFilter();

            diagLodQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            int loadedLod = diagLodQuery.CalculateEntityCount();
            diagLodQuery.ResetFilter();

            UnityEngine.Debug.Log(
                $"[Streaming] Dormant={dormant} Pending={pending} " +
                $"Loaded={loaded}(HD={loaded - loadedLod} LOD={loadedLod}) " +
                $"Unloading={unloading}");
        }
    }
}
