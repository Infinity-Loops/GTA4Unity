using System.Collections.Generic;
using IVUnity.Resolver;        // TxdSlot
using RageLib.Models;
using RageLib.Textures;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;  // BatchMeshID, BatchMaterialID

namespace IVUnity.ECS
{
    public enum ModelLoadState : byte
    {
        NotLoaded = 0,
        Loading   = 1,
        Loaded    = 2,
        Failed    = 3,
    }

    public struct SubMeshRef
    {
        public BatchMeshID     MeshId;
        public BatchMaterialID MaterialId;
        public LocalTransform  LocalTransform; // identity for non-fragment; offset for fragment pieces
        public AABB            Bounds;         // canonical Unity.Entities bounds (Center/Extents)
    }

    public sealed class ModelCacheEntry
    {
        public ModelLoadState State;
        public SubMeshRef[] SubMeshes;
        public int RefCount;
        public float LastUsedTime;
        // Number of consecutive Failed states we've evicted-and-retried. Capped by
        // MeshCacheEvictionSystem so a permanently broken model (corrupt resource,
        // unsupported variant, etc.) doesn't loop the system forever.
        public int FailureCount;
        // Every TxdSlot in the model's TXD chain (self → parent → …) that we ref-counted
        // when the model loaded. MeshCacheEvictionSystem releases these on eviction so the
        // store can drop dictionaries no model references anymore. Engine equivalent of the
        // CStreaming::RemoveRef calls a model issues against its TXD slot at unload.
        public TxdSlot[] TxdChain;
    }

    /// <summary>
    /// Managed per-unique-model cache. Mutated only on the main thread.
    /// Key is ModelCatalog.Hash(modelName).
    /// </summary>
    public sealed class MeshCache
    {
        private readonly Dictionary<uint, ModelCacheEntry> entries = new Dictionary<uint, ModelCacheEntry>();

        public int Count => entries.Count;

        public bool TryGet(uint hash, out ModelCacheEntry entry) => entries.TryGetValue(hash, out entry);

        public ModelCacheEntry GetOrCreate(uint hash)
        {
            if (!entries.TryGetValue(hash, out var e))
            {
                e = new ModelCacheEntry { State = ModelLoadState.NotLoaded };
                entries[hash] = e;
            }
            return e;
        }

        public IEnumerable<KeyValuePair<uint, ModelCacheEntry>> All => entries;

        public void Remove(uint hash) => entries.Remove(hash);
    }

    /// <summary>Payload handed from worker thread → main thread for upload.</summary>
    public sealed class ModelLoadResult
    {
        public uint Hash;
        public IModelFile ModelFile;       // ModelFile for .wdr, ModelFragTypeFile for .wft
        public TextureFile[] Textures;     // null if no texture (legacy V1 path)
        public bool Failed;
        public string FailureReason;
        // V2 path: the leaf slot the worker acquired. ChainSlots() gives the array we
        // need to release later; ToChainArray() gives the TextureFile[] for ModelGenerator.
        public TxdSlot TxdLeaf;
    }
}
