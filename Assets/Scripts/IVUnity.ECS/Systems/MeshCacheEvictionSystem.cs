using System.Collections.Generic;
using IVUnity.Resolver;           // MaterialResolver / TxdStore
using Unity.Entities;
using Unity.Rendering;           // EntitiesGraphicsSystem
using UnityEngine;
using UnityEngine.Rendering;     // BatchMeshID, BatchMaterialID

namespace IVUnity.ECS
{
    /// <summary>
    /// Periodically releases MeshCache entries whose RefCount is 0 under three conditions:
    ///   • Loaded + LRU expired → free BRG resources (the original LRU path).
    ///   • Failed + grace expired → drop the dead entry so the next dispatch retries it.
    ///   • Loading + watchdog timeout → orphaned worker (player fast-travelled before it
    ///     finished and never came back near it). Drop the entry so the next visit re-dispatches;
    ///     when the in-flight worker eventually returns, MainThreadMeshUploadSystem just
    ///     creates a fresh entry and stores it as Loaded — same outcome.
    ///
    /// Without the latter two paths a single transient parse failure or a worker that gets
    /// orphaned by fast travel can leave a model permanently stuck (state Failed/Loading,
    /// dispatch refuses to retry), so revisiting the area never re-loads the model.
    ///
    /// Materials: V2 dedupes by texture-reference tuple in MaterialResolver — many
    /// models can hold the same BatchMaterialID, so we never unregister here. Same for V1.
    ///
    /// TXD slots: V2 cache entries hold a TxdChain ref-counted at load time; we Release it
    /// here so the TxdStore can drop unreferenced dictionaries.
    /// </summary>
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    [UpdateAfter(typeof(InstanceUnloadSystem))]
    public partial class MeshCacheEvictionSystem : SystemBase
    {
        private const float TickIntervalSeconds   = 1f;
        private const float LruKeepAliveSeconds   = 30f;
        private const float FailedRetrySeconds    = 5f;
        private const float LoadingTimeoutSeconds = 60f;
        private const int   MaxFailureRetries     = 3;

        private MeshCache cache;
        private EntitiesGraphicsSystem graphics;
        private float lastTick;

        public void Configure(MeshCache cache) { this.cache = cache; }

        protected override void OnUpdate()
        {
            if (cache == null) return;

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - lastTick < TickIntervalSeconds) return;
            lastTick = now;

            if (graphics == null)
            {
                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (graphics == null) return;
            }

            var toRemove = new List<uint>();
            var toReset  = new List<uint>(); // keep entry, FailureCount preserved
            int loadedEvictions = 0, failedRetries = 0, loadingResets = 0, failedGiveUp = 0;

            foreach (var kv in cache.All)
            {
                var e = kv.Value;
                if (e.RefCount > 0) continue;
                float age = now - e.LastUsedTime;

                switch (e.State)
                {
                    case ModelLoadState.Loaded when age >= LruKeepAliveSeconds:
                        toRemove.Add(kv.Key); loadedEvictions++; break;

                    // Capped retry: reset state in place (don't remove the entry — that
                    // would zero FailureCount via GetOrCreate, defeating the cap). After
                    // MaxFailureRetries the entry stays Failed and dispatch leaves it alone.
                    case ModelLoadState.Failed when age >= FailedRetrySeconds:
                        if (e.FailureCount < MaxFailureRetries)
                        {
                            toReset.Add(kv.Key);
                            failedRetries++;
                        }
                        else
                        {
                            failedGiveUp++;
                        }
                        break;

                    case ModelLoadState.Loading when age >= LoadingTimeoutSeconds:
                        toRemove.Add(kv.Key); loadingResets++; break;
                }
            }

            var v2Store = MaterialResolver.IsActive ? MaterialResolver.TxdStore : null;

            foreach (var hash in toRemove)
            {
                if (!cache.TryGet(hash, out var entry)) continue;
                if (entry.SubMeshes != null)
                {
                    foreach (var sub in entry.SubMeshes)
                    {
                        if (sub.MeshId.value != 0) graphics.UnregisterMesh(sub.MeshId);
                        // Materials are intentionally kept registered — both V1 (string-name
                        // shared) and V2 (texture-ref deduped) reuse them across models.
                    }
                }
                if (entry.TxdChain != null && v2Store != null)
                {
                    v2Store.Release(entry.TxdChain);
                    entry.TxdChain = null;
                }
                cache.Remove(hash);
            }

            foreach (var hash in toReset)
            {
                if (!cache.TryGet(hash, out var entry)) continue;
                if (entry.TxdChain != null && v2Store != null)
                {
                    v2Store.Release(entry.TxdChain);
                    entry.TxdChain = null;
                }
                entry.State        = ModelLoadState.NotLoaded;
                entry.SubMeshes    = null;
                entry.LastUsedTime = now;
                // FailureCount preserved — already incremented on the failure itself.
            }

            int total = toRemove.Count + toReset.Count;
            if (total > 0 || failedGiveUp > 0)
            {
                Debug.Log($"[Eviction] {total} cycled (loaded-evict={loadedEvictions} failed-retry={failedRetries} loading-reset={loadingResets}) | give-up={failedGiveUp}");
            }
        }
    }
}
