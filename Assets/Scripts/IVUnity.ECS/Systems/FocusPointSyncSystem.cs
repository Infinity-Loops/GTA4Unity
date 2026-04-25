using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity.ECS
{
    /// <summary>
    /// Reads a focus Transform on the main thread and writes to the FocusPointData singleton.
    /// Runs first in WorldStreamingSystemGroup so all downstream streaming decisions see a
    /// fresh position in the same frame.
    ///
    /// Target resolution:
    ///   1. ECSWorldBootstrap.FocusTarget  (inspector-assigned; preferred)
    ///   2. Camera.main.transform          (fallback if FocusTarget is unset)
    ///
    /// The legacy FocusPoint MonoBehaviour is deliberately NOT referenced — it's on the
    /// Phase 7 deletion list. Wire a target via the bootstrap inspector field instead.
    /// </summary>
    [UpdateInGroup(typeof(WorldStreamingSystemGroup), OrderFirst = true)]
    public partial class FocusPointSyncSystem : SystemBase
    {
        private Transform cachedFocus;
        private float3 lastLoggedPos = new float3(float.NaN);

        protected override void OnUpdate()
        {
            if (cachedFocus == null)
            {
                cachedFocus = ResolveFocusTransform();
                if (cachedFocus != null)
                {
                    Debug.Log($"[FocusSync] Tracking '{cachedFocus.gameObject.name}'");
                }
                else
                {
                    return; // nothing to track this frame; retry next frame
                }
            }

            if (!SystemAPI.HasSingleton<FocusPointData>()) return;

            float3 pos = cachedFocus.position;
            SystemAPI.SetSingleton(new FocusPointData { Position = pos });

            // Throttled log — confirms the stream sees movement, without spamming the console.
            if (math.distancesq(pos, lastLoggedPos) > 10000f) // every ~100 m of travel
            {
                Debug.Log($"[FocusSync] Focus moved to {pos}");
                lastLoggedPos = pos;
            }
        }

        private static Transform ResolveFocusTransform()
        {
            var bootstrap = Object.FindFirstObjectByType<ECSWorldBootstrap>();
            if (bootstrap != null && bootstrap.FocusTarget != null)
                return bootstrap.FocusTarget;

            if (Camera.main != null)
                return Camera.main.transform;

            return null;
        }
    }
}
