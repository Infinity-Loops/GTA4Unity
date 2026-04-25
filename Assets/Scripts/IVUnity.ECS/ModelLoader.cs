using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IVUnity.Resolver; // MaterialTextureResolverV2 / TxdStore
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
                result.ModelFile = OpenModelByExtension(entry.ModelFileName, entry.ModelFile.GetData(), out var reason);
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
            if (MaterialTextureResolverV2.IsActive && MaterialTextureResolverV2.TxdStore != null)
            {
                string txdName = entry.Definition?.textureName;
                if (!string.IsNullOrEmpty(txdName))
                {
                    try
                    {
                        var leaf = MaterialTextureResolverV2.TxdStore.Acquire(txdName);
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

        /// <summary>
        /// Dispatch by file extension and return a fully parsed IModelFile, or null if the
        /// extension is unsupported (reason written out). Mirrors the extension handling in
        /// HighPerformanceLoader.GetOrLoadModelData:
        ///   .wdr → ModelFile          (standard drawable)
        ///   .wft → ModelFragTypeFile  (fragment)
        ///   .wdd → reject             (dictionary; HighPerformanceLoader also doesn't pick from it)
        ///   .wbn/.wbd → reject        (collision meshes; out of scope per design spec)
        /// </summary>
        private static IModelFile OpenModelByExtension(string fileName, byte[] data, out string reason)
        {
            reason = null;

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
                    // Drawable dictionaries hold LOD variants packed together. Until we wire
                    // a real LOD selector (pick the right sub-drawable based on distance and
                    // the IPL hash), rendering the whole dictionary just stacks every LOD on
                    // top of itself. Disabled — re-enable alongside a WddDrawableSelector.
                    reason = ".wdd (LOD dictionary) disabled — needs proper sub-drawable selection";
                    return null;

                case ".wbn":
                case ".wbd":
                    reason = $"{ext} is a collision mesh — out of scope for this phase";
                    return null;

                default:
                    reason = $"Unsupported model extension '{ext}'";
                    return null;
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            semaphore.Dispose();
            cts.Dispose();
        }
    }
}
