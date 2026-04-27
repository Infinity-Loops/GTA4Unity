using IVUnity.Resolver;
using Unity.Entities;
using UnityEngine;

namespace IVUnity.ECS
{
    /// <summary>
    /// Per-tick reaper for the TxdStore. Drops the parsed TextureFile from any TxdSlot
    /// whose RefCount has been zero for longer than <see cref="LruKeepAliveSeconds"/>,
    /// freeing the parsed dictionary back to GC. The slot record itself is kept so a
    /// future Acquire reuses the same identity (avoids re-resolving parent links).
    ///
    /// Mirrors the engine's deferred eviction: a TXD orphaned when a model unloads is
    /// kept around briefly in case the camera turns back into the same area.
    /// </summary>
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(MeshCacheEvictionSystem))]
    public partial class TxdEvictionSystem : SystemBase
    {
        private const float TickIntervalSeconds  = 1f;
        private const float LruKeepAliveSeconds  = 30f;

        private float lastTick;

        protected override void OnUpdate()
        {
            if (!MaterialResolver.IsActive) return;
            var store = MaterialResolver.TxdStore;
            if (store == null) return;

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - lastTick < TickIntervalSeconds) return;
            lastTick = now;

            int evicted = store.Evict(now, LruKeepAliveSeconds);
            if (evicted > 0)
            {
                Debug.Log($"[TxdEviction] Released {evicted} dictionaries (live={store.LiveSlotCount} loaded={store.LoadedSlotCount})");
            }
        }
    }
}
