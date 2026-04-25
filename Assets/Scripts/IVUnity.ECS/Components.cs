using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.ECS
{
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

    // ---------- Singletons ----------

    /// <summary>Current focus position in world space, written each frame from the player transform.</summary>
    public struct FocusPointData : IComponentData
    {
        public float3 Position;
    }
}
