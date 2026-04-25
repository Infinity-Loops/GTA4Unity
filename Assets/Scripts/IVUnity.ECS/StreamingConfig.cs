using Unity.Entities;

namespace IVUnity.ECS
{
    /// <summary>
    /// Runtime-tunable knobs for the streaming pipeline. Stored as a singleton
    /// component on a dedicated config entity created during bootstrap.
    /// Defaults chosen per the design spec; adjust during Phase 5 tuning.
    /// </summary>
    public struct StreamingConfig : IComponentData
    {
        public float CellSize;
        public int   MaxLoadsPerFrame;
        public int   MaxUploadsPerFrame;
        public int   MaxPromotionsPerFrame;
        public int   MeshCacheSizeMB;
        public float StreamInDistance;
        public float StreamOutDistance;

        public static StreamingConfig Default => new StreamingConfig
        {
            CellSize              = 100f,  // HighPerformanceLoader's SpatialGrid also uses 100m cells
            MaxLoadsPerFrame      = 32,
            MaxUploadsPerFrame    = 8,
            MaxPromotionsPerFrame = 64,
            MeshCacheSizeMB       = 2048,
            StreamInDistance      = 300f,  // == HighPerformanceLoader.streamDistance
            StreamOutDistance     = 500f,  // == HighPerformanceLoader.cullDistance
        };
    }
}
