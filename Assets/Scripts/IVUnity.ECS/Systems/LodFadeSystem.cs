using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS
{
    /// <summary>
    /// Engine-faithful LOD crossfade via stipple dithering.
    ///
    /// Key insight from binary trace (FUN_00ae6fa0, FUN_00ae7fd0, FUN_00ae25a0):
    ///   - LOD entities with +0x61 > 0 ALWAYS render (in Pass 3)
    ///   - They only become invisible when ALL children set coverage bits (+0x54)
    ///   - The skip at line 1003586 requires: alpha<239 AND +0x4C!=0 AND +0x61!=0
    ///   - Top-level LODs (no parent) are NEVER skipped
    ///   - LODs with 2+ children stay visible during partial child loading
    ///
    /// Our approximation: fade LOD only when ALL its children (LodChildCount) are Loaded.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class LodFadeSystem : SystemBase
    {
        private const float FadeZone = 30f;

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

            // Step 1: count how many Loaded children each LOD entity has.
            var loadedChildCount = new NativeHashMap<Entity, int>(256, Allocator.Temp);

            var loadedHdQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LodRef, StreamingState>()
                .Build(em);
            loadedHdQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });

            if (!loadedHdQuery.IsEmpty)
            {
                using var lodRefs = loadedHdQuery.ToComponentDataArray<LodRef>(Allocator.Temp);
                for (int i = 0; i < lodRefs.Length; i++)
                {
                    Entity lod = lodRefs[i].LodEntity;
                    if (lod == Entity.Null) continue;
                    loadedChildCount.TryGetValue(lod, out int count);
                    loadedChildCount[lod] = count + 1;
                }
            }

            // Step 2: compute alpha per Loaded entity
            var loadedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, LocalTransform, DrawDist, StreamingState>()
                .Build(em);
            loadedQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            if (loadedQuery.IsEmpty)
            {
                loadedChildCount.Dispose();
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
                loadedChildCount.Dispose();
                return;
            }

            using var childEntities = childQuery.ToEntityArray(Allocator.Temp);
            using var childParents  = childQuery.ToComponentDataArray<Parent>(Allocator.Temp);

            var alphaMap = new NativeHashMap<Entity, float>(entities.Length, Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity e = entities[i];
                float dx = transforms[i].Position.x - focus.Position.x;
                float dz = transforms[i].Position.z - focus.Position.z;
                float dist = math.sqrt(dx * dx + dz * dz);
                float effectiveDraw = drawDists[i].Value * lodScale;
                float blend = math.saturate((FadeZone + effectiveDraw - dist) / FadeZone);

                float alpha;

                bool hasChildCount = em.HasComponent<LodChildCount>(e);
                int totalChildren = hasChildCount ? em.GetComponentData<LodChildCount>(e).Value : 0;
                loadedChildCount.TryGetValue(e, out int loadedChildren);

                if (totalChildren > 0 && loadedChildren >= totalChildren)
                {
                    // ALL children loaded → hide LOD parent.
                    // Engine: coverage bits on +0x54/+0x58, parent hidden when all bits set.
                    // Hard cutoff at alpha=239 (0xEF) in engine (line 1003583).
                    alpha = 0.0f;
                }
                else if (totalChildren > 0)
                {
                    // Has children but not all loaded → fully visible
                    alpha = 1.0f;
                }
                else
                {
                    // Leaf/HD/standalone entity: fade at draw distance edge
                    alpha = math.saturate(blend * 4.0f);
                    if (alpha > 0.9f) alpha = 1.0f;
                }

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
            loadedChildCount.Dispose();
        }
    }
}
