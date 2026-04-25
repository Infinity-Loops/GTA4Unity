using System.Collections.Generic;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity.ECS
{
    /// <summary>
    /// Immutable-after-bake metadata for each unique model. Populated once during
    /// WorldEntityBaker; read by ModelLoadDispatchSystem and MainThreadMeshUploadSystem.
    /// </summary>
    public sealed class ModelCatalog
    {
        public struct Entry
        {
            public string ModelFileName;   // e.g. "cj_bench.wdr"
            public string TextureFileName; // e.g. "cj_bench.wtd" (may be null)
            public string WddName;         // if non-null/non-"null", use this instead of ModelFileName with .wdd suffix
            public File ModelFile;         // resolved File reference inside IMG
            public File TextureFile;       // resolved File reference; may be null
            public Item_OBJS Definition;   // IDE metadata
        }

        private readonly Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();

        public int Count => entries.Count;

        public void Add(uint hash, Entry entry)
        {
            entries[hash] = entry;
        }

        public bool TryGet(uint hash, out Entry entry)
        {
            return entries.TryGetValue(hash, out entry);
        }

        /// <summary>Deterministic hash of a lowercased model name. Use for ModelRef.ModelHash.</summary>
        public static uint Hash(string modelName)
        {
            // Stable FNV-1a. Matches nothing in the codebase — it's the ECS-side internal key,
            // NOT the in-game GTA jenkins hash (which remains Ipl_INST.hash, used elsewhere).
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime  = 16777619;
                uint h = offset;
                for (int i = 0; i < modelName.Length; i++)
                {
                    h ^= char.ToLowerInvariant(modelName[i]);
                    h *= prime;
                }
                return h;
            }
        }
    }
}
