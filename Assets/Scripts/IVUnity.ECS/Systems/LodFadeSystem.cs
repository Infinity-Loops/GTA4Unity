using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class LodFadeSystem : SystemBase
    {
        private const float FadeZone = 20f;

        private EntityQuery hdChildQuery;
        private EntityQuery loadedRootQuery;
        private EntityQuery subMeshQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();
            RequireForUpdate<StreamingConfig>();

            hdChildQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LodRef, LocalTransform, DrawDist, StreamingState>()
                .Build(EntityManager);

            loadedRootQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, DrawDist, StreamingState>()
                .Build(EntityManager);

            subMeshQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SubMeshTag, Parent>()
                .WithAllRW<StippleAlpha>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            var focus = SystemAPI.GetSingleton<FocusPointData>();
            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            float lodScale = cfg.LodDistanceScale;
            float camX = focus.Position.x;
            float camZ = focus.Position.z;

            var loadedFilter = new StreamingState { Value = StreamingStateValue.Loaded };

            // Step 1: count HD children within draw distance per LOD parent
            hdChildQuery.SetSharedComponentFilter(loadedFilter);
            int hdCount = hdChildQuery.CalculateEntityCount();
            var renderingChildCount = new NativeParallelHashMap<Entity, int>(
                math.max(hdCount / 4, 64), Allocator.TempJob);

            var dep = Dependency;

            if (hdCount > 0)
            {
                dep = new CountChildrenJob
                {
                    CamX = camX,
                    CamZ = camZ,
                    LodScale = lodScale,
                    LodRefHandle = GetComponentTypeHandle<LodRef>(true),
                    TransformHandle = GetComponentTypeHandle<LocalTransform>(true),
                    DrawDistHandle = GetComponentTypeHandle<DrawDist>(true),
                    ChildCount = renderingChildCount,
                }.Schedule(hdChildQuery, dep);
            }
            hdChildQuery.ResetFilter();

            // Step 2: compute alpha — ONLY entities with alpha != 1.0 go into the map
            loadedRootQuery.SetSharedComponentFilter(loadedFilter);
            int rootCount = loadedRootQuery.CalculateEntityCount();
            if (rootCount == 0)
            {
                dep.Complete();
                renderingChildCount.Dispose();
                loadedRootQuery.ResetFilter();
                return;
            }

            var fadingMap = new NativeParallelHashMap<Entity, float>(rootCount, Allocator.TempJob);

            dep = new ComputeAlphaJob
            {
                CamX = camX,
                CamZ = camZ,
                LodScale = lodScale,
                FadeZone = FadeZone,
                EntityHandle = GetEntityTypeHandle(),
                TransformHandle = GetComponentTypeHandle<LocalTransform>(true),
                DrawDistHandle = GetComponentTypeHandle<DrawDist>(true),
                BoundRadiusHandle = GetComponentTypeHandle<BoundRadius>(true),
                LodLevelHandle = GetComponentTypeHandle<LodLevel>(true),
                ChildLodDistHandle = GetComponentTypeHandle<ChildLodDist>(true),
                LodChildCountHandle = GetComponentTypeHandle<LodChildCount>(true),
                BaseLayerHandle = GetComponentTypeHandle<BaseLayerTag>(true),
                RenderingChildCount = renderingChildCount,
                FadingMap = fadingMap.AsParallelWriter(),
            }.Schedule(loadedRootQuery, dep);

            loadedRootQuery.ResetFilter();

            // Step 3: write alpha to sub-mesh children
            // Only fading entities are in the map. Children whose parent is NOT
            // in the map get alpha=1.0 (skip write if already 1.0 → no dirty chunk).
            if (!subMeshQuery.IsEmpty)
            {
                dep = new WriteAlphaJob
                {
                    ParentHandle = GetComponentTypeHandle<Parent>(true),
                    AlphaHandle = GetComponentTypeHandle<StippleAlpha>(false),
                    FadingMap = fadingMap,
                }.ScheduleParallel(subMeshQuery, dep);
            }

            dep.Complete();
            fadingMap.Dispose();
            renderingChildCount.Dispose();

            #if UNITY_EDITOR
            WriteDebugData(camX, camZ, lodScale);
            #endif
        }

        [BurstCompile]
        struct CountChildrenJob : IJobChunk
        {
            public float CamX, CamZ, LodScale;

            [ReadOnly] public ComponentTypeHandle<LodRef> LodRefHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<DrawDist> DrawDistHandle;

            public NativeParallelHashMap<Entity, int> ChildCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var lodRefs = chunk.GetNativeArray(ref LodRefHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var drawDists = chunk.GetNativeArray(ref DrawDistHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity lod = lodRefs[i].LodEntity;
                    if (lod == Entity.Null) continue;

                    float dx = transforms[i].Position.x - CamX;
                    float dz = transforms[i].Position.z - CamZ;
                    float range = drawDists[i].Value * LodScale;

                    if (dx * dx + dz * dz < range * range)
                    {
                        ChildCount.TryGetValue(lod, out int count);
                        ChildCount[lod] = count + 1;
                    }
                }
            }
        }

        [BurstCompile]
        struct ComputeAlphaJob : IJobChunk
        {
            public float CamX, CamZ, LodScale, FadeZone;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<DrawDist> DrawDistHandle;
            [ReadOnly] public ComponentTypeHandle<BoundRadius> BoundRadiusHandle;
            [ReadOnly] public ComponentTypeHandle<LodLevel> LodLevelHandle;
            [ReadOnly] public ComponentTypeHandle<ChildLodDist> ChildLodDistHandle;
            [ReadOnly] public ComponentTypeHandle<LodChildCount> LodChildCountHandle;
            [ReadOnly] public ComponentTypeHandle<BaseLayerTag> BaseLayerHandle;

            [ReadOnly] public NativeParallelHashMap<Entity, int> RenderingChildCount;
            public NativeParallelHashMap<Entity, float>.ParallelWriter FadingMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var drawDists = chunk.GetNativeArray(ref DrawDistHandle);

                bool hasBounds = chunk.Has(ref BoundRadiusHandle);
                var bounds = hasBounds ? chunk.GetNativeArray(ref BoundRadiusHandle) : default;

                bool hasLodLevel = chunk.Has(ref LodLevelHandle);
                var lodLevels = hasLodLevel ? chunk.GetNativeArray(ref LodLevelHandle) : default;

                bool hasChildLodDist = chunk.Has(ref ChildLodDistHandle);
                var childLodDists = hasChildLodDist ? chunk.GetNativeArray(ref ChildLodDistHandle) : default;

                bool hasChildCount = chunk.Has(ref LodChildCountHandle);
                var childCounts = hasChildCount ? chunk.GetNativeArray(ref LodChildCountHandle) : default;

                bool hasBaseLayer = chunk.Has(ref BaseLayerHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    float ddx = transforms[i].Position.x - CamX;
                    float ddz = transforms[i].Position.z - CamZ;
                    float distSq = ddx * ddx + ddz * ddz;

                    float radius = hasBounds ? bounds[i].Value : 0f;
                    float effectiveDraw = drawDists[i].Value * LodScale + radius;

                    // Fast path: entity well inside draw distance → alpha is 1.0
                    // Only entities within FadeZone of the edge can have alpha < 1.0
                    float safeRange = effectiveDraw - FadeZone;
                    bool needsDistanceFade = safeRange <= 0f || distSq >= safeRange * safeRange;

                    LodType lodType = hasLodLevel ? lodLevels[i].Value : LodType.OrphanHD;
                    bool isParentType = lodType == LodType.LOD || lodType == LodType.SLOD;
                    bool isBaseOrphanLod = lodType == LodType.OrphanHD && hasBaseLayer;
                    bool needsChildFade = isParentType || isBaseOrphanLod;

                    // Skip sqrt and fade math for the vast majority of entities
                    if (!needsDistanceFade && !needsChildFade)
                        continue; // alpha=1.0, not added to map

                    float dist = math.sqrt(distSq);

                    // Part 1: distance edge fade
                    float fadeUp = math.saturate((FadeZone + effectiveDraw - dist) / FadeZone);
                    float alpha = math.saturate(fadeUp * 4.0f);
                    if (alpha > 0.9f) alpha = 1.0f;

                    // Part 2: fade down relative to children
                    if (needsChildFade)
                    {
                        Entity e = entities[i];
                        int totalChildren = hasChildCount ? childCounts[i].Value : 0;
                        RenderingChildCount.TryGetValue(e, out int attached);

                        float childLodDist;

                        if (totalChildren > 0 && attached >= totalChildren && hasChildLodDist)
                            childLodDist = childLodDists[i].Value * LodScale;
                        else if (totalChildren == 0 && hasBaseLayer)
                            childLodDist = 50f * LodScale;
                        else
                            childLodDist = -1f;

                        if (childLodDist >= 0f)
                        {
                            float fadeStart = childLodDist + FadeZone;
                            if (dist <= fadeStart)
                            {
                                float t = math.saturate((dist - childLodDist) / FadeZone);
                                alpha = math.min(alpha, t);
                                if (alpha < 0.02f) alpha = 0.0f;
                            }
                        }
                    }

                    // Only add if NOT fully opaque — keeps the map tiny
                    if (alpha < 1.0f)
                        FadingMap.TryAdd(entities[i], alpha);
                }
            }
        }

        [BurstCompile]
        struct WriteAlphaJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Parent> ParentHandle;
            public ComponentTypeHandle<StippleAlpha> AlphaHandle;
            [ReadOnly] public NativeParallelHashMap<Entity, float> FadingMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var parents = chunk.GetNativeArray(ref ParentHandle);
                var alphas = chunk.GetNativeArray(ref AlphaHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (FadingMap.TryGetValue(parents[i].Value, out float a))
                    {
                        // Parent is fading — write the fade value
                        if (alphas[i].Value != a)
                            alphas[i] = new StippleAlpha { Value = a };
                    }
                    else
                    {
                        // Parent not fading — should be fully visible
                        if (alphas[i].Value != 1.0f)
                            alphas[i] = new StippleAlpha { Value = 1.0f };
                    }
                }
            }
        }

        #if UNITY_EDITOR
        private void WriteDebugData(float camX, float camZ, float lodScale)
        {
            var em = EntityManager;
            loadedRootQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            if (loadedRootQuery.IsEmpty) { loadedRootQuery.ResetFilter(); return; }

            using var entities = loadedRootQuery.ToEntityArray(Allocator.Temp);
            using var transforms = loadedRootQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity e = entities[i];
                float dx = transforms[i].Position.x - camX;
                float dz = transforms[i].Position.z - camZ;
                float dist = math.sqrt(dx * dx + dz * dz);

                bool hasChildLodDist = em.HasComponent<ChildLodDist>(e);
                bool hasChildCount = em.HasComponent<LodChildCount>(e);

                int total = hasChildCount ? em.GetComponentData<LodChildCount>(e).Value : 0;
                float childDist = hasChildLodDist ? em.GetComponentData<ChildLodDist>(e).Value * lodScale : 0;

                var dbg = new DebugFadeReason
                {
                    Value = FadeReason.None,
                    DistToCamera = dist,
                    ChildLodDistScaled = childDist,
                    ChildrenLoaded = 0,
                    ChildrenTotal = total,
                };
                if (em.HasComponent<DebugFadeReason>(e))
                    em.SetComponentData(e, dbg);
                else
                    em.AddComponentData(e, dbg);
            }
            loadedRootQuery.ResetFilter();
        }
        #endif
    }
}
