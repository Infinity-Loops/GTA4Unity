using System;
using System.Collections.Generic;
using RageLib.Textures;
using UnityEngine;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity.Resolver
{
    /// <summary>
    /// Engine-faithful TXD slot table. Mirrors CTxdStore: every TXD ever requested has a
    /// <see cref="TxdSlot"/>; ref counts are bumped via <see cref="Acquire"/> and dropped via
    /// <see cref="Release"/>; slots whose ref count hits zero get evicted by
    /// <see cref="Evict"/> after an LRU window.
    ///
    /// Differences vs. the previous lazy/cache-the-null implementation:
    ///   • Failures are not cached as null — a transient parse error doesn't pin a TXD as
    ///     permanently missing, so the order-dependent "spawn at A, walk to B, B's textures
    ///     missing" failure mode goes away. Capped retry via <see cref="MaxFailureRetries"/>
    ///     prevents infinite spinning on a genuinely broken WTD.
    ///   • The chain is built top-down at acquire time (parent acquired first), so a single
    ///     <see cref="Acquire"/> hands back a slot whose entire parent chain is already loaded
    ///     and ref-counted on the caller's behalf.
    ///   • Every slot in the chain holds a ref. <see cref="TxdSlot.ChainSlots"/> + matching
    ///     <see cref="Release"/> calls keep parent dictionaries alive as long as any descendant
    ///     model is still resident — same shape as the engine's per-slot ref counting.
    /// </summary>
    public sealed class TxdStore
    {
        private const int MaxFailureRetries = 3;

        private readonly Dictionary<string, File>   gameFiles;
        private readonly Dictionary<string, string> txdParents;
        private readonly Dictionary<string, TxdSlot> slots = new Dictionary<string, TxdSlot>(StringComparer.OrdinalIgnoreCase);
        private readonly object loadLock = new object();

        public TxdStore(Dictionary<string, File> gameFiles, Dictionary<string, string> txdParents = null)
        {
            this.gameFiles  = gameFiles  ?? throw new ArgumentNullException(nameof(gameFiles));
            this.txdParents = txdParents ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a Loaded slot for the named TXD with RefCount bumped on it AND on every
        /// parent in the chain. Returns null only if the WTD is genuinely missing or has
        /// failed parsing more than <see cref="MaxFailureRetries"/> times. Thread-safe;
        /// blocks on <see cref="loadLock"/> for the duration of the load (parses are fast).
        /// </summary>
        public TxdSlot Acquire(string txdName)
        {
            if (string.IsNullOrEmpty(txdName)) return null;

            lock (loadLock)
            {
                if (!slots.TryGetValue(txdName, out var slot))
                {
                    string lower = txdName.ToLowerInvariant();
                    slot = new TxdSlot(lower);
                    slots[lower] = slot;
                }

                EnsureLoaded(slot);

                if (slot.State != TxdLoadState.Loaded) return null;

                // Bump ref counts top-down across the whole chain. The engine ref-counts
                // each slot in CStreaming::AddRef terms (FUN_005b6110); we do the equivalent
                // at the granularity that matters: any slot reachable from this one stays
                // alive as long as we're holding it. Do NOT touch UnityEngine.Time here —
                // Acquire runs on worker threads and Time accessors are not always thread-safe
                // (the `get_isPlaying ... can only be called from the main thread` exception
                // earlier in the session is the same family). LastUsedTime only matters once
                // RefCount reaches 0; Release stamps it from the main thread.
                for (var s = slot; s != null; s = s.Parent) s.RefCount++;
                return slot;
            }
        }

        /// <summary>
        /// Drops one ref from every slot in the chain. Caller passes the same array
        /// <see cref="TxdSlot.ChainSlots"/> returned (or kept) at acquire time.
        /// </summary>
        public void Release(TxdSlot[] chain)
        {
            if (chain == null) return;
            lock (loadLock)
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                foreach (var s in chain)
                {
                    if (s == null) continue;
                    s.RefCount = Math.Max(0, s.RefCount - 1);
                    if (s.RefCount == 0) s.LastUsedTime = now;
                }
            }
        }

        /// <summary>
        /// Per-tick eviction. Drops slots whose RefCount has been zero for more than
        /// <paramref name="lruSeconds"/> — releases the parsed TextureFile back to GC.
        /// Mirrors the engine's deferred eviction: a slot freshly orphaned (refcount → 0)
        /// is kept around briefly in case a nearby model brings it right back.
        /// </summary>
        public int Evict(float now, float lruSeconds)
        {
            int evicted = 0;
            lock (loadLock)
            {
                List<string> toRemove = null;
                foreach (var kv in slots)
                {
                    var s = kv.Value;
                    if (s.RefCount > 0) continue;
                    if (s.State != TxdLoadState.Loaded) continue;
                    if (now - s.LastUsedTime < lruSeconds) continue;
                    (toRemove ?? (toRemove = new List<string>())).Add(kv.Key);
                }
                if (toRemove != null)
                {
                    foreach (var key in toRemove)
                    {
                        if (!slots.TryGetValue(key, out var s)) continue;
                        // Drop the dictionary; keep the slot record so a future Acquire reuses
                        // the same identity (avoids re-creating parent links on every cycle).
                        s.Dictionary = null;
                        s.State = TxdLoadState.NotLoaded;
                        s.Parent = null;
                        evicted++;
                    }
                }
            }
            return evicted;
        }

        public int LiveSlotCount    { get { lock (loadLock) return slots.Count; } }
        public int LoadedSlotCount  { get { lock (loadLock) { int c = 0; foreach (var kv in slots) if (kv.Value.State == TxdLoadState.Loaded) c++; return c; } } }

        // ---- internals ----

        // Parses the WTD into the slot, recursively acquiring (without ref bump — that's
        // the caller's job in Acquire) the parent slot from the txdp map.
        // Caller must hold loadLock.
        private void EnsureLoaded(TxdSlot slot)
        {
            if (slot.State == TxdLoadState.Loaded || slot.State == TxdLoadState.Loading) return;
            if (slot.State == TxdLoadState.Failed && slot.FailureCount >= MaxFailureRetries) return;

            slot.State = TxdLoadState.Loading;

            string fileName = slot.Name + ".wtd";
            if (!gameFiles.TryGetValue(fileName, out var file) || file == null)
            {
                // Missing file is a permanent fact about the dataset — record it as Failed
                // at max retries so subsequent Acquire calls early-out without re-trying.
                slot.State = TxdLoadState.Failed;
                slot.FailureCount = MaxFailureRetries;
                return;
            }

            try
            {
                byte[] data = file.GetData();
                if (data == null || data.Length == 0)
                {
                    slot.State = TxdLoadState.Failed;
                    slot.FailureCount++;
                    return;
                }

                var tf = new TextureFile();
                tf.Open(data);
                tf.Read();
                slot.Dictionary = tf;
            }
            catch (Exception ex)
            {
                slot.State = TxdLoadState.Failed;
                slot.FailureCount++;
                // Log full stack only on first failure per slot — keeps the log readable
                // while still telling us exactly where the NRE / parser error originated.
                if (slot.FailureCount == 1)
                    Debug.LogWarning($"[TxdStore] Parse failed '{fileName}' ({ex.GetType().Name}): {ex.Message}\n{ex}");
                else
                    Debug.LogWarning($"[TxdStore] Parse failed '{fileName}' (attempt {slot.FailureCount}/{MaxFailureRetries}): {ex.Message}");
                return;
            }

            // Engine equivalent of FUN_008e07a0 — link parent at load completion. We resolve
            // the parent slot recursively (no ref bump here; Acquire bumps the whole chain
            // once it has the leaf).
            if (txdParents.TryGetValue(slot.Name, out var parentName))
            {
                if (!slots.TryGetValue(parentName, out var parentSlot))
                {
                    string lower = parentName.ToLowerInvariant();
                    parentSlot = new TxdSlot(lower);
                    slots[lower] = parentSlot;
                }
                EnsureLoaded(parentSlot);
                if (parentSlot.State == TxdLoadState.Loaded)
                {
                    slot.Parent = parentSlot;
                }
                // If the parent failed, leave Parent null — the chain just stops at this slot.
                // The model will still resolve textures present in this dictionary.
            }

            slot.State = TxdLoadState.Loaded;
        }
    }
}
