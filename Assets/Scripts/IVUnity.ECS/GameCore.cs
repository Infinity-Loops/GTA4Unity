using Unity.Entities;

namespace IVUnity.ECS
{
    /// <summary>
    /// ECS-side game lifecycle hooks. Currently exposes a single SetReady() that publishes
    /// the GameReadyTag singleton. Deliberately does NOT touch any UI — the legacy
    /// GameCore MonoBehaviour activated a "gameUI" GameObject on SetReady; we don't want
    /// that. Future gameplay logic (input, player spawning, AI, mission state) gets added
    /// here as additional ECS components / systems.
    /// </summary>
    public static class GameCore
    {
        /// <summary>
        /// Idempotent — creates the GameReadyTag singleton entity if it doesn't already exist.
        /// Safe to call from the main thread of any system or MonoBehaviour.
        /// </summary>
        public static void SetReady()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            using var q = em.CreateEntityQuery(typeof(GameReadyTag));
            if (!q.IsEmpty) return;

            var e = em.CreateEntity(typeof(GameReadyTag));
            em.SetName(e, "GameReady");
        }

        public static bool IsReady()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return false;
            using var q = world.EntityManager.CreateEntityQuery(typeof(GameReadyTag));
            return !q.IsEmpty;
        }
    }
}
