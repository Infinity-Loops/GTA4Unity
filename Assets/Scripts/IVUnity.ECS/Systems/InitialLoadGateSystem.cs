using IVUnity.ECS.UI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace IVUnity.ECS
{
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(InstancePromotionSystem))]
    public partial class InitialLoadGateSystem : SystemBase
    {
        private const float TimeoutSeconds = 30f;

        private bool fired;
        private bool switchedToGeometry;
        private float startTime;
        private EntityQuery streamingQuery;
        private EntityQuery collisionReadyQuery;

        public void Arm()
        {
            startTime = UnityEngine.Time.realtimeSinceStartup;
            fired = false;
            switchedToGeometry = false;
            Enabled = true;
        }

        protected override void OnCreate()
        {
            Enabled = false;
            streamingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WorldInstanceTag, StreamingState>()
                .Build(EntityManager);
            collisionReadyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CollisionReadyTag>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            if (fired) { Enabled = false; return; }

            bool collisionReady = !collisionReadyQuery.IsEmpty;
            int pending = CountState(StreamingStateValue.Pending);
            int loaded = CountState(StreamingStateValue.Loaded);
            bool geometryReady = loaded > 0 && pending == 0;
            bool timeoutElapsed = UnityEngine.Time.realtimeSinceStartup - startTime > TimeoutSeconds;

            if (collisionReady && !switchedToGeometry)
            {
                switchedToGeometry = true;
                LoadingState.SetTarget(0);
                LoadingState.Advance("world geometry...", 0);
            }

            if ((collisionReady && geometryReady) || timeoutElapsed)
            {
                Debug.Log(timeoutElapsed
                    ? $"[Gate] Timeout - releasing UI with {loaded} loaded, {pending} pending, collision={collisionReady}"
                    : $"[Gate] Released UI at {loaded} loaded, 0 pending, collision ready");

                LoadingScreen.Finish();
                GameCore.SetReady();
                fired = true;
                Enabled = false;
            }
        }

        private int CountState(StreamingStateValue state)
        {
            streamingQuery.SetSharedComponentFilter(new StreamingState { Value = state });
            int count = streamingQuery.CalculateEntityCount();
            streamingQuery.ResetFilter();
            return count;
        }
    }
}
