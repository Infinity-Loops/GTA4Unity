using Unity.Physics;

namespace IVUnity.ECS
{
    public static class PhysicsLayers
    {
        // Layer bits
        public const uint Environment = 1u << 0;
        public const uint Character = 1u << 1;

        // Filters
        public static readonly CollisionFilter EnvironmentFilter = new CollisionFilter
        {
            BelongsTo = Environment,
            CollidesWith = Environment | Character,
        };

        public static readonly CollisionFilter CharacterFilter = new CollisionFilter
        {
            BelongsTo = Character,
            CollidesWith = Environment | Character,
        };

        // Camera ray: hits environment only, ignores characters
        public static readonly CollisionFilter CameraObstructionFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = Environment,
        };
    }
}
