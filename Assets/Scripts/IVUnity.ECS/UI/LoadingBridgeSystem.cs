using Unity.Collections;
using Unity.Entities;

namespace IVUnity.ECS.UI
{
    /// <summary>
    /// Snapshots the thread-safe LoadingState into the LoadingProgress ECS singleton once
    /// per frame on the main thread. Runs first in InitializationSystemGroup so downstream
    /// readers (LoadingViewSystem, InitialLoadGateSystem, any other ECS-side consumer) see
    /// up-to-date values within the same frame.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class LoadingBridgeSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Singleton entity — created once; subsequent systems use SystemAPI.GetSingleton.
            var e = EntityManager.CreateEntity(typeof(LoadingProgress));
            EntityManager.SetName(e, "LoadingProgress");
        }

        protected override void OnUpdate()
        {
            var label = default(FixedString128Bytes);
            var src = LoadingState.Label;
            if (!string.IsNullOrEmpty(src))
            {
                // Truncate safely if longer than 128 bytes (rare; file names are short).
                int max = System.Math.Min(src.Length, label.Capacity);
                for (int i = 0; i < max; i++) label.Append(src[i]);
            }

            SystemAPI.SetSingleton(new LoadingProgress
            {
                Current  = LoadingState.Current,
                Total    = LoadingState.Target,
                Label    = label,
                Finished = LoadingState.Finished,
            });
        }
    }
}
