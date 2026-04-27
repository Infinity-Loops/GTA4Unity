using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.ECS
{
    #if UNITY_EDITOR
    public struct DebugShaderName : IComponentData
    {
        public FixedString64Bytes Value;
    }
    #endif
    // ---------- Tags ----------

    /// <summary>Marks an entity that represents one Ipl_INST world instance (root).</summary>
    public struct WorldInstanceTag : IComponentData { }

    /// <summary>Marks a sub-mesh child entity spawned during promotion.</summary>
    public struct SubMeshTag : IComponentData { }

    // ---------- Per-root data ----------

    /// <summary>Hash identifying the model this instance should render. Lookup key into ModelCatalog and MeshCache.</summary>
    public struct ModelRef : IComponentData
    {
        public uint ModelHash;
    }

    /// <summary>Stable identifier back to the source Ipl_INST. Debug/logging only.</summary>
    public struct InstanceOrigin : IComponentData
    {
        public ulong SourceId;
    }

    // ---------- Shared components (drive chunking) ----------

    /// <summary>
    /// Cell coordinate in streaming grid. SHARED — entities with the same cell
    /// end up in the same archetype chunk, so activation is a chunk-level op.
    /// </summary>
    public struct CellIndex : ISharedComponentData
    {
        public int2 Cell;
    }

    /// <summary>
    /// Identifies which streaming IPL this entity came from. -1 = gta.dat (always loaded).
    /// >= 0 = streaming WPL index into StreamingIplRegistry. Used by the activation system
    /// to hide/show entire WPLs based on focus proximity to the WPL's spatial bounds.
    /// Engine: DAT_016ec774 pool, FUN_00c77c70 per-frame update.
    /// </summary>
    public struct StreamingIplId : ISharedComponentData
    {
        public int Value;
    }

    public enum StreamingStateValue : byte
    {
        Dormant   = 0,
        Pending   = 1,
        Loaded    = 2,
        Unloading = 3,
    }

    /// <summary>
    /// Streaming lifecycle state. SHARED — same state → same chunk, so we iterate
    /// only the chunks in the relevant state instead of filtering per-entity.
    /// </summary>
    public struct StreamingState : ISharedComponentData
    {
        public StreamingStateValue Value;
    }

    // ---------- Per-entity draw distance ----------

    /// <summary>
    /// IDE-defined draw distance for this instance. Every entity gets one — the streaming
    /// system uses it for per-entity visibility decisions instead of cell-based thresholds.
    /// Matches GTA IV's per-CBaseModelInfo distance check.
    /// </summary>
    public struct DrawDist : IComponentData
    {
        public float Value;
    }

    // ---------- LOD ----------

    /// <summary>
    /// On an HD entity: points to the corresponding LOD entity that replaces it at distance.
    /// Set during baking from Ipl_INST.lod (index → entity). The streaming system uses
    /// the entity's own DrawDist component for the switch threshold.
    /// </summary>
    public struct LodRef : IComponentData
    {
        public Entity LodEntity;
    }

    /// <summary>
    /// Tags an entity as a LOD replacement (lower-detail version of an HD entity).
    /// </summary>
    public struct LodTag : IComponentData { }

    /// <summary>
    /// Number of HD entities that have LodRef pointing to this LOD entity.
    /// Set during baking. The streaming system only suppresses the LOD when
    /// loadedChildCount == LodChildCount (all children visible).
    /// Engine equivalent: CEntity +0x61 (LOD child count byte).
    /// </summary>
    public struct LodChildCount : IComponentData
    {
        public int Value;
    }

    // ---------- Stipple fade (LOD crossfade) ----------

    /// <summary>
    /// Per-instance stipple alpha for LOD crossfade. Written by LodFadeSystem,
    /// read by shaders via DOTS instanced _StippleAlpha property.
    /// 0 = fully invisible (all pixels discarded), 1 = fully opaque.
    /// Engine equivalent: CEntity +0x63 alpha byte + globalScalars.x.
    /// </summary>
    [Unity.Rendering.MaterialProperty("_StippleAlpha")]
    public struct StippleAlpha : IComponentData
    {
        public float Value;
    }

    // ---------- Singletons ----------

    /// <summary>Current focus position in world space, written each frame from the player transform.</summary>
    public struct FocusPointData : IComponentData
    {
        public float3 Position;
    }
}
