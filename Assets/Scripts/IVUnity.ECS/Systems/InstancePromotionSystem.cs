using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;   // RenderFilterSettings
using Unity.Mathematics;
using Unity.Rendering;           // MaterialMeshInfo, RenderBounds, WorldRenderBounds, WorldToLocal_Tag,
                                 // PerInstanceCullingTag, BlendProbeTag, ChunkWorldRenderBounds, EntitiesGraphicsChunkInfo
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;     // BatchMeshID, BatchMaterialID, ShadowCastingMode, LightProbeUsage, MotionVectorGenerationMode

namespace IVUnity.ECS
{
    /// <summary>
    /// Promotes Pending entities whose model is Loaded into rendered children, creating
    /// one sub-mesh child per SubMeshRef with the full set of BRG components.
    ///
    /// Hot-path note: a pre-built archetype carries every required BRG component, so each
    /// child spawn is one structural change (CreateEntity(archetype)) instead of many
    /// individual AddComponent calls. The previous implementation did ~5 structural
    /// changes per child × many children per frame, which caused editor-wide lag.
    /// </summary>
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(MainThreadMeshUploadSystem))]
    public partial class InstancePromotionSystem : SystemBase
    {
        private MeshCache cache;
        private EntityArchetype childArchetype;
        private RenderFilterSettings filterSettings;
        private RenderFilterSettings filterSettingsNoShadow;
        private EntityQuery pendingQuery;

        public void Configure(MeshCache cache)
        {
            this.cache = cache;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<StreamingConfig>();

            // Component list = exactly what RenderMeshUtility.AddComponents(entity, em, desc, mmi)
            // would add (see com.unity.entities.graphics/.../RenderMeshUtility.cs:143-155), plus
            // our own SubMeshTag + Parent + LocalTransform for the hierarchy hookup.
            childArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<SubMeshTag>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<LocalToWorld>(),
                ComponentType.ReadWrite<Parent>(),
                ComponentType.ReadWrite<MaterialMeshInfo>(),
                ComponentType.ReadWrite<RenderBounds>(),
                ComponentType.ReadWrite<WorldRenderBounds>(),
                ComponentType.ReadWrite<WorldToLocal_Tag>(),
                ComponentType.ReadWrite<PerInstanceCullingTag>(),
                ComponentType.ReadWrite<BlendProbeTag>(),              // from LightProbeUsage.BlendProbes
                ComponentType.ReadWrite<StippleAlpha>(),               // LOD crossfade
                ComponentType.ReadWrite<RenderFilterSettings>(),       // shared
                ComponentType.ChunkComponent<ChunkWorldRenderBounds>(),
                ComponentType.ChunkComponent<EntitiesGraphicsChunkInfo>());

            filterSettings = new RenderFilterSettings
            {
                Layer              = 0,
                RenderingLayerMask = 0xffffffff,
                MotionMode         = MotionVectorGenerationMode.Camera,
                ShadowCastingMode  = ShadowCastingMode.On,
                ReceiveShadows     = true,
                StaticShadowCaster = false,
            };
            filterSettingsNoShadow = new RenderFilterSettings
            {
                Layer              = 0,
                RenderingLayerMask = 0xffffffff,
                MotionMode         = MotionVectorGenerationMode.Camera,
                ShadowCastingMode  = ShadowCastingMode.Off,
                ReceiveShadows     = true,
                StaticShadowCaster = false,
            };

            pendingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, ModelRef, LocalTransform, StreamingState>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            if (cache == null) return;

            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            int budget = cfg.MaxPromotionsPerFrame;

            pendingQuery.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Pending });

            if (pendingQuery.IsEmpty) { pendingQuery.ResetFilter(); return; }

            using var entities = pendingQuery.ToEntityArray(Allocator.Temp);
            using var refs     = pendingQuery.ToComponentDataArray<ModelRef>(Allocator.Temp);
            pendingQuery.ResetFilter();

            for (int i = 0; i < entities.Length && budget > 0; i++)
            {
                var root = entities[i];
                var modelRef = refs[i];

                if (!cache.TryGet(modelRef.ModelHash, out var entry) || entry.State != ModelLoadState.Loaded)
                    continue;

                // Parent's current LocalToWorld — used to pre-seed child LocalToWorld
                // so the first frame doesn't render at origin before transform propagation.
                var parentL2W = EntityManager.GetComponentData<LocalToWorld>(root);

                if (entry.SubMeshes == null || entry.SubMeshes.Length == 0)
                {
                    Debug.LogWarning($"[Promotion] 0x{modelRef.ModelHash:X8} has 0 sub-meshes — invisible entity");
                }

                foreach (var sub in entry.SubMeshes)
                {
                    var child = EntityManager.CreateEntity(childArchetype);

                    // Non-structural setters — fast.
                    EntityManager.SetComponentData(child, sub.LocalTransform);
                    EntityManager.SetComponentData(child, new Parent { Value = root });
                    EntityManager.SetComponentData(child, new MaterialMeshInfo(sub.MaterialId, sub.MeshId, (ushort)0));
                    EntityManager.SetComponentData(child, new RenderBounds { Value = sub.Bounds });
                    EntityManager.SetComponentData(child, new StippleAlpha { Value = 1.0f });
                    EntityManager.SetComponentData(child, new LocalToWorld
                    {
                        Value = math.mul(parentL2W.Value, sub.LocalTransform.ToMatrix()),
                    });

                    // Shared component set moves the entity into the chunk keyed by filterSettings.
                    // All children share the same value, so they all land in one chunk (after the
                    // first one creates it) — subsequent moves are cheap chunk-pointer updates.
                    bool isLod = EntityManager.HasComponent<LodTag>(root);
                    EntityManager.SetSharedComponent(child, isLod ? filterSettingsNoShadow : filterSettings);

                    #if UNITY_EDITOR
                    EntityManager.AddComponentData(child, new DebugShaderName
                    {
                        Value = new Unity.Collections.FixedString64Bytes(sub.ShaderName ?? "unknown")
                    });
                    #endif
                }

                entry.RefCount++;
                entry.LastUsedTime = UnityEngine.Time.realtimeSinceStartup;

                EntityManager.SetSharedComponent(root, new StreamingState { Value = StreamingStateValue.Loaded });

                budget--;
            }
        }
    }
}
