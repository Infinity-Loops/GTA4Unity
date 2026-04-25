using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace IVUnity.ECS
{
    /// <summary>
    /// Holds the loading screen up until the initial batch of world entities has finished
    /// streaming in. Polls per frame: counts entities in StreamingState.Loaded, fires the
    /// UI transition (LoadingState.Finish) and publishes GameReadyTag once the count
    /// crosses MinLoadedBeforeReady OR a timeout elapses.
    /// </summary>
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(InstancePromotionSystem))]
    public partial class InitialLoadGateSystem : SystemBase
    {
        // Rough proxy for "world is usable". We can't trivially know the target without
        // a radius pre-scan, so pick a pragmatic default; the timeout catches sparse-area cases.
        private const int   MinLoadedBeforeReady = 400;
        private const float TimeoutSeconds       = 30f;

        private bool fired;
        private float startTime;

        /// <summary>
        /// Called by ECSWorldBootstrap once baking is done. The system then waits for
        /// promotion to populate the Loaded state before firing.
        /// </summary>
        public void Arm()
        {
            startTime = UnityEngine.Time.realtimeSinceStartup;
            fired = false;
            Enabled = true;
        }

        protected override void OnCreate()
        {
            // Dormant until Arm() is called.
            Enabled = false;
        }

        protected override void OnUpdate()
        {
            if (fired) { Enabled = false; return; }

            int loaded = CountLoaded();
            bool timeoutElapsed = UnityEngine.Time.realtimeSinceStartup - startTime > TimeoutSeconds;

            if (loaded >= MinLoadedBeforeReady || timeoutElapsed)
            {
                Debug.Log(timeoutElapsed
                    ? $"[Gate] Timeout — releasing UI with {loaded} loaded"
                    : $"[Gate] Released UI at {loaded} loaded entities");

                LoadingScreen.Finish();
                GameCore.SetReady();
                fired = true;
                Enabled = false;
            }
        }

        private int CountLoaded()
        {
            var q = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, StreamingState>()
                .Build(EntityManager);
            q.SetSharedComponentFilter(new StreamingState { Value = StreamingStateValue.Loaded });
            return q.CalculateEntityCount();
        }
    }
}
