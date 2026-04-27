using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IVUnity.Resolver; // MaterialResolver / TxdStore
using RageLib.Models;
using RageLib.Textures;
using UnityEngine;

namespace IVUnity.ECS
{
    /// <summary>
    /// Worker-thread wrapper around RageLib parsing. Consumers call Enqueue(hash, entry)
    /// from the main thread; results land in the Results queue and are drained by
    /// MainThreadMeshUploadSystem each frame.
    ///
    /// Dispatches on file extension:
    ///   .wdr → ModelFile             (static drawable)
    ///   .wft → ModelFragTypeFile     (fragment — wraps FragTypeModel)
    ///   .wdd → Failed                (dictionary; not handled this phase — matches legacy WorldComposerMachine)
    /// </summary>
    public sealed class ModelLoader : IDisposable
    {
        public readonly ConcurrentQueue<ModelLoadResult> Results = new ConcurrentQueue<ModelLoadResult>();

        private readonly SemaphoreSlim semaphore;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public ModelLoader(int maxParallel)
        {
            semaphore = new SemaphoreSlim(maxParallel);
        }

        public void Enqueue(uint hash, ModelCatalog.Entry entry)
        {
            var token = cts.Token;
            _ = Task.Run(async () =>
            {
                try { await semaphore.WaitAsync(token); }
                catch (OperationCanceledException) { return; }

                try
                {
                    var result = Parse(hash, entry);
                    Results.Enqueue(result);
                }
                catch (Exception ex)
                {
                    Results.Enqueue(new ModelLoadResult
                    {
                        Hash = hash,
                        Failed = true,
                        FailureReason = ex.Message,
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }, token);
        }

        private static ModelLoadResult Parse(uint hash, ModelCatalog.Entry entry)
        {
            var result = new ModelLoadResult { Hash = hash };

            // --- Model ---
            try
            {
                result.ModelFile = OpenModelByExtension(entry, entry.ModelFile.GetData(), out var reason);
                if (result.ModelFile == null)
                {
                    result.Failed = true;
                    result.FailureReason = reason;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Failed = true;
                result.FailureReason = "Model: " + ex.Message;
                return result;
            }

            // --- Textures (optional) ---
            // V2 path: ref-counted Acquire on the TxdStore. The store walks the IDE txdp
            // parent chain top-down, parses each WTD on first request, and bumps RefCount
            // on every slot in the chain. We hand the leaf back as result.TxdLeaf;
            // MainThreadMeshUploadSystem stores ChainSlots() on the cache entry so eviction
            // can Release them. Engine equivalent of CStreaming::AddRef on each slot in the
            // model's dictionary chain.
            if (MaterialResolver.IsActive && MaterialResolver.TxdStore != null)
            {
                string txdName = entry.Definition?.textureName;
                if (!string.IsNullOrEmpty(txdName))
                {
                    try
                    {
                        var leaf = MaterialResolver.TxdStore.Acquire(txdName);
                        if (leaf != null)
                        {
                            result.TxdLeaf = leaf;
                            result.Textures = leaf.ToChainArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ModelLoader] TxdStore.Acquire failed for '{txdName}': {ex.Message}");
                    }
                }
            }
            else if (entry.TextureFile != null)
            {
                try
                {
                    byte[] texBytes = entry.TextureFile.GetData();
                    var tex = new TextureFile();
                    tex.Open(texBytes);
                    tex.Read();
                    result.Textures = new TextureFile[] { tex };
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModelLoader] Texture parse failed for {entry.TextureFileName}: {ex.Message}");
                    result.Textures = null;
                }
            }

            return result;
        }

        private static IModelFile OpenModelByExtension(ModelCatalog.Entry entry, byte[] data, out string reason)
        {
            reason = null;
            string fileName = entry.ModelFileName;

            if (string.IsNullOrEmpty(fileName))
            {
                reason = "ModelFileName is empty";
                return null;
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            switch (ext)
            {
                case ".wdr":
                {
                    var m = new ModelFile();
                    m.Open(data);
                    m.Read();
                    return m;
                }

                case ".wft":
                {
                    var m = new ModelFragTypeFile();
                    using (var stream = new MemoryStream(data, writable: false))
                    {
                        m.Open(stream);
                    }
                    return m;
                }

                case ".wdd":
                {
                    // WDD = pgDictionary<DrawableModel>. Contains multiple drawables keyed
                    // by name hash. The engine selects the right entry via bsearch on the
                    // hash table (sub_6596B0 in IV binary). We do the same: hash the model
                    // name from the IDE entry, find it in NameHashes, return a wrapper that
                    // exposes just that single drawable as an IModelFile.
                    var dict = new ModelDictionaryFile();
                    using (var stream = new MemoryStream(data, writable: false))
                    {
                        dict.Open(stream);
                    }

                    string modelName = entry.Definition?.modelName;
                    if (string.IsNullOrEmpty(modelName))
                    {
                        reason = ".wdd has no model name to select entry";
                        dict.Dispose();
                        return null;
                    }

                    uint targetHash = RageLib.Common.Hasher.Hash(modelName);
                    var hashes = dict.File.Data.NameHashes;
                    int index = -1;
                    for (int i = 0; i < hashes.Count; i++)
                    {
                        if (hashes[i] == targetHash) { index = i; break; }
                    }

                    if (index < 0 || index >= dict.File.Data.Entries.Count)
                    {
                        reason = $".wdd '{fileName}' has no entry for '{modelName}' (hash 0x{targetHash:X8})";
                        dict.Dispose();
                        return null;
                    }

                    return new WddSingleEntry(dict, index);
                }

                case ".wbn":
                case ".wbd":
                    reason = $"{ext} is a collision mesh — out of scope for this phase";
                    return null;

                default:
                    reason = $"Unsupported model extension '{ext}'";
                    return null;
            }
        }

        /// <summary>
        /// Wraps a ModelDictionaryFile exposing only one specific entry as an IModelFile.
        /// Keeps the dictionary alive (owns it) so the drawable's data stays valid.
        /// </summary>
        private sealed class WddSingleEntry : IModelFile
        {
            private readonly ModelDictionaryFile _dict;
            private readonly int _index;

            public WddSingleEntry(ModelDictionaryFile dict, int index)
            {
                _dict = dict;
                _index = index;
            }

            public void Open(string filename) { }
            public void Open(Stream stream) { }

            public ModelNode GetModel(TextureFile[] textures)
            {
                var drawable = new RageLib.Models.Data.Drawable(_dict.File.Data.Entries[_index]);
                return ModelGenerator.GenerateModel(drawable, textures);
            }

            public void Dispose() => _dict.Dispose();
        }

        public void Dispose()
        {
            cts.Cancel();
            semaphore.Dispose();
            cts.Dispose();
        }
    }
}
