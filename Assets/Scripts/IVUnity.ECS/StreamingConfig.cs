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

        /// <summary>
        /// Multiplier applied to every entity's IDE draw distance during visibility checks.
        /// Engine equivalent: *(float*)(renderCtx + 0x934) in FUN_00aebff0.
        /// Engine uses dynamic 1.0-1.5 based on platform/quality; default 1.5 for PC-equivalent.
        /// </summary>
        public float LodDistanceScale;

        public static StreamingConfig Default => new StreamingConfig
        {
            CellSize              = 100f,
            MaxLoadsPerFrame      = 32,
            MaxUploadsPerFrame    = 8,
            MaxPromotionsPerFrame = 64,
            MeshCacheSizeMB       = 2048,
            StreamInDistance      = 300f,
            StreamOutDistance     = 500f,
            LodDistanceScale      = 1.5f,
        };
    }
}
