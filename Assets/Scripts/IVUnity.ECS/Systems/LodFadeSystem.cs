using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    /// <summary>
    /// LOD alpha system mirroring RAGE architecture (from OpenRage GTA V source):
    ///
    /// 1. CalcAlphaFade: per-entity alpha from own distance (fade at draw distance edge)
    /// 2. FadeDownRelativeToChildren: parent fades when camera within childLodDist
    ///    - Gated by all children being within their draw distance (RAGE: AllChildrenAttached)
    ///    - Uses ChildLodDist (max of children's drawDist, set during baking)
    /// 3. Force parent visible if child not loaded (Part C safety)
    ///
    /// RAGE refs: CLodMgr::UpdateAlphaPt2_FadeDownRelativeToChildren,
    ///            CLodMgr::CalcCrossFadeAlpha, PostScan.cpp alpha update pass #2
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class LodFadeSystem : SystemBase
    {
        private const float FadeZone = 20f;

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();
            RequireForUpdate<StreamingConfig>();
        }

        protected override void OnUpdate()
        {
            var focus = SystemAPI.GetSingleton<FocusPointData>();
            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            float lodScale = cfg.LodDistanceScale;
            var em = EntityManager;
            float camX = focus.Position.x;
            float camZ = focus.Position.z;

            // Step 1: count children within their draw distance per LOD parent.
            // RAGE: AllChildrenAttached — in engine children beyond range aren't streamed in.
            // We load everything, so gate on draw distance instead.
            var renderingChildCount = new NativeHashMap<Entity, int>(256, Allocator.Temp);

            var loadedHdQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LodRef, LocalTransform, DrawDist, StreamingState>()
                .Build(em);
            loadedHdQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });

            if (!loadedHdQuery.IsEmpty)
            {
                using var lodRefs    = loadedHdQuery.ToComponentDataArray<LodRef>(Allocator.Temp);
                using var childTrans = loadedHdQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                using var childDists = loadedHdQuery.ToComponentDataArray<DrawDist>(Allocator.Temp);

                for (int i = 0; i < lodRefs.Length; i++)
                {
                    Entity lod = lodRefs[i].LodEntity;
                    if (lod == Entity.Null) continue;

                    float cdx = childTrans[i].Position.x - camX;
                    float cdz = childTrans[i].Position.z - camZ;
                    float childDistSq = cdx * cdx + cdz * cdz;
                    float childRange = childDists[i].Value * lodScale;

                    if (childDistSq < childRange * childRange)
                    {
                        renderingChildCount.TryGetValue(lod, out int count);
                        renderingChildCount[lod] = count + 1;
                    }
                }
            }

            // Step 2: compute alpha per Loaded entity
            var loadedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, DrawDist, StreamingState>()
                .Build(em);
            loadedQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            if (loadedQuery.IsEmpty)
            {
                renderingChildCount.Dispose();
                return;
            }

            using var entities   = loadedQuery.ToEntityArray(Allocator.Temp);
            using var transforms = loadedQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var drawDists  = loadedQuery.ToComponentDataArray<DrawDist>(Allocator.Temp);

            var childQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SubMeshTag, Parent, StippleAlpha>()
                .Build(em);
            if (childQuery.IsEmpty)
            {
                renderingChildCount.Dispose();
                return;
            }

            using var childEntities = childQuery.ToEntityArray(Allocator.Temp);
            using var childParents  = childQuery.ToComponentDataArray<Parent>(Allocator.Temp);

            var alphaMap = new NativeHashMap<Entity, float>(entities.Length, Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity e = entities[i];
                float dx = transforms[i].Position.x - camX;
                float dz = transforms[i].Position.z - camZ;
                float dist = math.sqrt(dx * dx + dz * dz);

                // Engine: adds bound radius to effective draw, not subtracts from distance.
                // Large objects stay visible longer because their extent reaches further.
                float radius = em.HasComponent<BoundRadius>(e) ? em.GetComponentData<BoundRadius>(e).Value : 0f;
                float effectiveDraw = drawDists[i].Value * lodScale + radius;

                // Part 1: base alpha from own distance (RAGE: CalcAlphaFade)
                float fadeUp = math.saturate((FadeZone + effectiveDraw - dist) / FadeZone);
                float alpha = math.saturate(fadeUp * 4.0f);
                if (alpha > 0.9f) alpha = 1.0f;

                #if UNITY_EDITOR
                FadeReason reason = alpha < 1.0f ? FadeReason.DistanceEdgeFade : FadeReason.FullyVisible;
                #endif

                // Part 2: FadeDownRelativeToChildren (RAGE: UpdateAlphaPt2)
                // RAGE: only entities with children apply this (!IsHighDetail)
                // HD and OrphanHD are leaves — they never fade from children.
                bool hasChildLodDist = em.HasComponent<ChildLodDist>(e);
                bool hasChildCount = em.HasComponent<LodChildCount>(e);
                bool hasLodLevel = em.HasComponent<LodLevel>(e);
                bool isParentType = hasLodLevel &&
                    (em.GetComponentData<LodLevel>(e).Value == LodType.LOD ||
                     em.GetComponentData<LodLevel>(e).Value == LodType.SLOD);

                if (isParentType)
                {
                    int totalChildren = hasChildCount ? em.GetComponentData<LodChildCount>(e).Value : 0;
                    renderingChildCount.TryGetValue(e, out int attached);

                    float childLodDist;

                    if (totalChildren > 0 && attached >= totalChildren && hasChildLodDist)
                    {
                        // Within-IPL: all children rendering → fade using known childLodDist
                        childLodDist = em.GetComponentData<ChildLodDist>(e).Value * lodScale;
                    }
                    else if (totalChildren == 0 && em.HasComponent<BaseLayerTag>(e))
                    {
                        // Childless LOD in base layer: HD is in streaming WPLs (no LodRef link).
                        // Fade using assumed HD coverage — conservative to avoid gaps.
                        childLodDist = 50f * lodScale;
                    }
                    else
                    {
                        childLodDist = -1f; // don't fade
                    }

                    if (childLodDist >= 0f)
                    {
                        float fadeStart = childLodDist + FadeZone;
                        float fadeStop = childLodDist;

                        if (dist <= fadeStart)
                        {
                            float t = math.saturate((dist - fadeStop) / (fadeStart - fadeStop));
                            alpha = math.min(alpha, t);
                            if (alpha < 0.02f) alpha = 0.0f;

                            #if UNITY_EDITOR
                            reason = totalChildren > 0 ? FadeReason.AllChildrenLoaded : FadeReason.BaseLayerHidden;
                            #endif
                        }
                    }
                }

                // Part 3: Force parent visible if child not loaded (RAGE: PostScan Part C)
                // If this entity has a parent AND this entity is fading/invisible AND not loaded
                // → force parent alpha = 1. We handle this by not hiding entities that aren't loaded,
                // which the Loaded query filter already ensures.

                #if UNITY_EDITOR
                if (isParentType)
                {
                    int dbgTotal = hasChildCount ? em.GetComponentData<LodChildCount>(e).Value : 0;
                    renderingChildCount.TryGetValue(e, out int dbgAttached);
                    float dbgChildDist = hasChildLodDist ? em.GetComponentData<ChildLodDist>(e).Value * lodScale : 0;
                    var dbg = new DebugFadeReason
                    {
                        Value = reason,
                        DistToCamera = dist,
                        ChildLodDistScaled = dbgChildDist,
                        ChildrenLoaded = dbgAttached,
                        ChildrenTotal = dbgTotal,
                    };
                    if (em.HasComponent<DebugFadeReason>(e))
                        em.SetComponentData(e, dbg);
                    else
                        em.AddComponentData(e, dbg);
                }
                #endif

                alphaMap[e] = alpha;
            }

            // Step 3: write alpha to sub-mesh children
            for (int i = 0; i < childEntities.Length; i++)
            {
                Entity parent = childParents[i].Value;
                if (alphaMap.TryGetValue(parent, out float a))
                {
                    em.SetComponentData(childEntities[i], new StippleAlpha { Value = a });
                }
            }

            alphaMap.Dispose();
            renderingChildCount.Dispose();
        }
    }
}
