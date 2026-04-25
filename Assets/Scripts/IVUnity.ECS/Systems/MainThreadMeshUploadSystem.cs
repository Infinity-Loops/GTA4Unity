using System.Collections.Generic;
using IVUnity; // MaterialTextureResolver
using IVUnity.Resolver; // MaterialTextureResolverV2
using RageLib.Models;
using RageLib.Textures;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;           // EntitiesGraphicsSystem
using UnityEngine;
using UnityEngine.Rendering;     // BatchMeshID, BatchMaterialID

namespace IVUnity.ECS
{
    /// <summary>
    /// Main-thread-only: pulls parsed ModelLoadResult payloads, builds Unity.Mesh and Material
    /// objects, registers them with EntitiesGraphicsSystem. Bounded by MaxUploadsPerFrame.
    /// Re-entry-safe — state per model is owned by MeshCache.
    /// </summary>
    [UpdateInGroup(typeof(WorldAssetSystemGroup))]
    public partial class MainThreadMeshUploadSystem : SystemBase
    {
        private ModelLoader loader;
        private MeshCache   cache;
        private EntitiesGraphicsSystem graphics;
        private readonly List<FlatSubMesh> flatBuffer = new List<FlatSubMesh>(32);
        // Throttle the noisy upload-failure logs: log full stack trace for the first N
        // distinct hashes; after that just bump the FailureCount so the user can still
        // see counts via the eviction log without drowning the console.
        private const int FailureLogBudget = 25;
        private static readonly System.Collections.Generic.HashSet<uint> failureLogged = new System.Collections.Generic.HashSet<uint>();
        private static int failureLogCount;

        public void Configure(ModelLoader loader, MeshCache cache)
        {
            this.loader = loader;
            this.cache = cache;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<StreamingConfig>();
        }

        protected override void OnUpdate()
        {
            if (loader == null || cache == null) return;

            if (graphics == null)
            {
                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (graphics == null) return;
            }

            var cfg = SystemAPI.GetSingleton<StreamingConfig>();
            int budget = cfg.MaxUploadsPerFrame;

            FlushMissingTextureReport();

            while (budget > 0 && loader.Results.TryDequeue(out var result))
            {
                budget--;
                var entry = cache.GetOrCreate(result.Hash);

                if (result.Failed)
                {
                    entry.State = ModelLoadState.Failed;
                    entry.FailureCount++;
                    // The worker may have acquired a TXD chain before failing on the model
                    // parse — release it here so the slots don't leak refs.
                    ReleaseLeaf(result.TxdLeaf);
                    LogFailure(result.Hash, "worker", result.FailureReason, null);
                    continue;
                }

                try
                {
                    flatBuffer.Clear();

                    // Embedded-texture fallback: if no .wtd was resolved at bake time, use the
                    // model's own ShaderGroup.TextureDictionary. Matches the fallback in
                    // HighPerformanceLoader.ProcessLoadTask:382-410.
                    TextureFile[] textures = result.Textures;
                    if (textures == null)
                    {
                        TextureFile embedded = null;
                        if      (result.ModelFile is ModelFile mf)        embedded = mf.EmbeddedTextureFile;
                        else if (result.ModelFile is ModelFragTypeFile ff) embedded = ff.EmbeddedTextureFile;
                        if (embedded != null) textures = new[] { embedded };
                    }

                    var modelNode = result.ModelFile.GetModel(textures);
                    ModelFlatten.Flatten(modelNode, flatBuffer);

                    var subs = new SubMeshRef[flatBuffer.Count];
                    for (int i = 0; i < flatBuffer.Count; i++)
                    {
                        var f = flatBuffer[i];
                        var mesh = f.Geometry.GetUnityMesh();

                        Material mat = null;
                        if (f.Material == null)
                        {
                            // No shader/material at all on the submesh. BRG with default material
                            // id (0) renders this whitish. Catch it explicitly — V1 vs V2 makes
                            // no difference here, but the count tells us if this is a real cause.
                            LogMissingTexture(result.Hash, "(null Material)", "no-material");
                        }
                        else
                        {
                            // mainTex is never null here — ModelGenerator initialises it to a
                            // 1×1 placeholder RageUnityTexture before lookup. The real signal
                            // is whether Decode() actually populated `pixels`. A placeholder
                            // (lookup failed or shader has no texture param) leaves pixels=null,
                            // which renders as the GPU default (white) once GetUnityTexture runs.
                            bool diffuseResolved  = HasRealPixels(f.Material.mainTex);
                            bool normalResolved   = HasRealPixels(f.Material.normalTex);
                            bool specularResolved = HasRealPixels(f.Material.specularTex);

                            Texture2D embedded = diffuseResolved  ? f.Material.mainTex.GetUnityTexture()     : null;
                            Texture2D normal   = normalResolved   ? f.Material.normalTex.GetUnityTexture()   : null;
                            Texture2D specular = specularResolved ? f.Material.specularTex.GetUnityTexture() : null;

                            if (!diffuseResolved)
                            {
                                string label = string.IsNullOrEmpty(f.Material.textureName)
                                    ? "(no-name|shader=" + (f.Material.shaderName ?? "?") + ")"
                                    : f.Material.textureName;
                                LogMissingTexture(result.Hash, label, "diffuse");
                            }
                            if (!normalResolved && !string.IsNullOrEmpty(f.Material.normalTextureName))
                                LogMissingTexture(result.Hash, f.Material.normalTextureName, "normal");
                            if (!specularResolved && !string.IsNullOrEmpty(f.Material.specularTextureName))
                                LogMissingTexture(result.Hash, f.Material.specularTextureName, "specular");

                            if (MaterialTextureResolverV2.IsActive)
                            {
                                // Engine-faithful: one fresh Material per submesh, populated
                                // with the textures already resolved through this model's
                                // TxdSlot chain. No global material cache → no order-dependent
                                // material pollution between cells.
                                if (f.Material.layerTextures != null && f.Material.layerTextures.Length > 1)
                                {
                                    // Multi-layer terrain — collect resolved Texture2Ds for each
                                    // layer and route to the matching gta_terrain_va_3lyr/4lyr
                                    // shader. ModelGenerator already populated layerTextures only
                                    // for shaders that need them.
                                    var layers = new Texture2D[f.Material.layerTextures.Length];
                                    for (int li = 0; li < layers.Length; li++)
                                    {
                                        var lt = f.Material.layerTextures[li];
                                        layers[li] = HasRealPixels(lt) ? lt.GetUnityTexture() : null;
                                    }
                                    mat = MaterialTextureResolverV2.BuildTerrain(f.Material.shaderName, layers);
                                }
                                else
                                {
                                    mat = MaterialTextureResolverV2.Build(f.Material.shaderName, embedded, normal, specular);
                                }
                            }
                            else
                            {
                                mat = MaterialTextureResolver.GetOrCreateSharedMaterial(
                                    f.Material.shaderName,
                                    f.Material.textureName,
                                    embedded,
                                    f.Material.normalTextureName, normal,
                                    f.Material.specularTextureName, specular);
                            }
                        }

                        var meshId = graphics.RegisterMesh(mesh);
                        var matId  = mat != null ? graphics.RegisterMaterial(mat) : default;

                        subs[i] = new SubMeshRef
                        {
                            MeshId = meshId,
                            MaterialId = matId,
                            LocalTransform = f.LocalTransform,
                            Bounds = new AABB
                            {
                                Center  = mesh.bounds.center,
                                Extents = mesh.bounds.extents,
                            },
                        };
                    }

                    entry.SubMeshes = subs;
                    // Hand the acquired chain to the cache entry so eviction releases it.
                    // Capturing here (not earlier) means a thrown upload still routes through
                    // the catch path and the leaf gets released without a cache leak.
                    entry.TxdChain = result.TxdLeaf?.ChainSlots();
                    entry.State = ModelLoadState.Loaded;
                    entry.LastUsedTime = UnityEngine.Time.realtimeSinceStartup;
                }
                catch (System.Exception ex)
                {
                    entry.State = ModelLoadState.Failed;
                    entry.FailureCount++;
                    ReleaseLeaf(result.TxdLeaf);
                    LogFailure(result.Hash, "upload", ex.Message, ex);
                }
            }
        }

        // A "real" texture has Decode()'d pixel bytes. The 1×1 placeholder ModelGenerator
        // assigns when lookup fails has pixels=null, which renders as the GPU default
        // (white). This is the actual indicator of "we got nothing useful".
        private static bool HasRealPixels(global::RageUnityTexture rut)
        {
            return rut != null && rut.pixels != null && rut.pixels.Length > 0;
        }

        // Diagnostic: collect (texture-name, slot) misses with per-key occurrence counts so
        // a single wide-spread case (e.g. many shaders without standard texture parameters
        // collapsing to "(no-name)") doesn't look like one isolated incident. Periodic flush
        // every 5s prints the counters; final report on first quiet tick.
        private const int MissingLogBudget = 80;
        private static readonly System.Collections.Generic.Dictionary<string, int> missingCounts =
            new System.Collections.Generic.Dictionary<string, int>();
        private static readonly System.Collections.Generic.Dictionary<string, uint> missingFirstHash =
            new System.Collections.Generic.Dictionary<string, uint>();
        private static float lastMissingFlush;

        private static void LogMissingTexture(uint hash, string textureName, string slot)
        {
            string key = textureName + "|" + slot;
            if (missingCounts.TryGetValue(key, out int prev))
            {
                missingCounts[key] = prev + 1;
            }
            else
            {
                missingCounts[key] = 1;
                missingFirstHash[key] = hash;
            }
        }

        private static void FlushMissingTextureReport()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - lastMissingFlush < 5f) return;
            lastMissingFlush = now;
            if (missingCounts.Count == 0) return;

            int printed = 0;
            foreach (var kv in missingCounts)
            {
                if (printed++ >= MissingLogBudget) break;
                int sep = kv.Key.LastIndexOf('|');
                string name = kv.Key.Substring(0, sep);
                string slot = kv.Key.Substring(sep + 1);
                uint exampleHash = missingFirstHash[kv.Key];
                Debug.LogWarning($"[MissingTex] {slot}='{name}' x{kv.Value} (e.g. model 0x{exampleHash:X8})");
            }
            if (missingCounts.Count > MissingLogBudget)
                Debug.LogWarning($"[MissingTex] {missingCounts.Count - MissingLogBudget} more unique entries suppressed.");
            missingCounts.Clear();
            missingFirstHash.Clear();
        }

        private static void ReleaseLeaf(IVUnity.Resolver.TxdSlot leaf)
        {
            if (leaf == null) return;
            var store = MaterialTextureResolverV2.TxdStore;
            if (store != null) store.Release(leaf.ChainSlots());
        }

        private static void LogFailure(uint hash, string phase, string message, System.Exception ex)
        {
            if (failureLogged.Add(hash) && failureLogCount < FailureLogBudget)
            {
                failureLogCount++;
                if (ex != null)
                    Debug.LogWarning($"[Upload:{phase}] hash {hash:X8} — {ex.GetType().Name}: {message}\n{ex}");
                else
                    Debug.LogWarning($"[Upload:{phase}] hash {hash:X8} — {message}");

                if (failureLogCount == FailureLogBudget)
                    Debug.LogWarning($"[Upload] Suppressing further per-hash failure traces (budget {FailureLogBudget} hit). Eviction log still reports counts.");
            }
        }
    }
}
