using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using RageLib.Textures;
using RageLib.Models;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity
{
    public static class MaterialTextureResolver
    {
        private static ConcurrentDictionary<string, Material> sharedMaterials = new ConcurrentDictionary<string, Material>();
        private static ConcurrentDictionary<string, TextureReference> textureReferences = new ConcurrentDictionary<string, TextureReference>();
        private static ConcurrentDictionary<string, Texture2D> loadedTextures = new ConcurrentDictionary<string, Texture2D>();
        private static readonly object materialCreationLock = new object();
        private class MaterialReference
        {
            public string ShaderName { get; set; }
            public string TextureName { get; set; }
            public string SourceFile { get; set; }
            public bool HasEmbeddedTexture { get; set; }
            public TextureReference LinkedTexture { get; set; }
        }
        
        private static ConcurrentDictionary<string, MaterialDefinition> materialIndex = new ConcurrentDictionary<string, MaterialDefinition>();
        
        private class MaterialDefinition
        {
            public string ShaderName { get; set; }
            public string TextureName { get; set; }
            public string SourceModel { get; set; }
            public bool HasTexture { get; set; }
            public TextureReference ResolvedTexture { get; set; }
        }
        private static ConcurrentDictionary<string, MaterialReference> materialReferences = new ConcurrentDictionary<string, MaterialReference>();
        private static HashSet<string> scannedModels = new HashSet<string>();
        
        private class TextureReference 
        {
            public string TextureName { get; set; }
            public string SourceFile { get; set; }
            public RageLib.Textures.Texture RageTexture { get; set; }
            public byte[] RawData { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsEmbedded { get; set; }
        }
        
        private struct FileEntry
        {
            public string Name { get; set; }
            public byte[] Data { get; set; }
        }
        
        public static async Task Initialize(GTADatLoader dataLoader)
        {
            Debug.Log("Initializing Material/Texture Resolver - Optimized parallel caching...");
            Debug.Log($"Using {Environment.ProcessorCount} CPU cores with enhanced parallelization");
            
            var startTime = System.DateTime.Now;
            int workerThreads = Environment.ProcessorCount * 4;
            int ioThreads = Environment.ProcessorCount * 4;
            System.Threading.ThreadPool.SetMinThreads(workerThreads, ioThreads);
            System.Threading.ThreadPool.SetMaxThreads(workerThreads * 2, ioThreads * 2);
            var allFiles = dataLoader.gameFiles.ToList();
            var wtdFiles = new List<KeyValuePair<string, File>>();
            
            foreach (var kvp in allFiles)
            {
                if (kvp.Key.EndsWith(".wtd", StringComparison.OrdinalIgnoreCase))
                {
                    wtdFiles.Add(kvp);
                }
            }
            
            Debug.Log($"Found {wtdFiles.Count} texture dictionary files to index");
            Debug.Log("Phase 1: Loading ALL texture references into memory...");
            await Task.Run(() => ProcessTexturesOptimized(wtdFiles));
            Debug.Log("Phase 2: Building complete material index with resolved textures...");
            await BuildCompleteMaterialIndex(allFiles);
            
            var elapsed = (System.DateTime.Now - startTime).TotalSeconds;
            int embeddedCount = textureReferences.Values.Count(t => t.IsEmbedded);
            int externalCount = textureReferences.Count - embeddedCount;
            
            Debug.Log($"Texture Resolver initialized in {elapsed:F2} seconds");
            Debug.Log($"  - Indexed {textureReferences.Count} total texture references");
            Debug.Log($"    - {externalCount} from .wtd files");
            Debug.Log($"    - {embeddedCount} embedded in models");
            Debug.Log($"  - Resolved {materialIndex.Count} complete material definitions");
            Debug.Log($"  - Ready for on-demand material creation with proper sharing");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        
        
        
        private static async Task BuildCompleteMaterialIndex(List<KeyValuePair<string, File>> allFiles)
        {
            var modelFiles = new List<KeyValuePair<string, File>>();
            
            foreach (var kvp in allFiles)
            {
                string name = kvp.Key.ToLower();
                if (name.EndsWith(".wdr") || name.EndsWith(".wdd") || name.EndsWith(".wft"))
                {
                    modelFiles.Add(kvp);
                }
            }
            
            Debug.Log($"Scanning {modelFiles.Count} model files for material-texture mappings...");
            
            var processedCount = 0;
            var materialCount = 0;
            
            await Task.Run(() =>
            {
                var partitioner = Partitioner.Create(modelFiles, true);
                
                Parallel.ForEach(partitioner, new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = Environment.ProcessorCount * 2
                },
                kvp =>
                {
                    try
                    {
                        var data = kvp.Value.GetData();
                        if (data == null || data.Length == 0) return;
                        
                        string ext = Path.GetExtension(kvp.Key).ToLower();
                        IModelFile model = null;
                        
                        if (ext == ".wft")
                        {
                            model = new ModelFragTypeFile();
                        }
                        else
                        {
                            model = new ModelFile();
                        }
                        
                        using (var ms = new MemoryStream(data))
                        {
                            model.Open(ms);
                        }
                        
                        bool hasEmbeddedTextures = false;
                        if (model is ModelFile modelFile && modelFile.EmbeddedTextureFile != null)
                        {
                            var embeddedTextures = modelFile.EmbeddedTextureFile;
                            foreach (var texture in embeddedTextures.Textures)
                            {
                                if (!string.IsNullOrEmpty(texture.Name))
                                {
                                    try
                                    {
                                        var reference = new TextureReference
                                        {
                                            TextureName = texture.Name,
                                            SourceFile = kvp.Key,
                                            RageTexture = texture,
                                            RawData = null,
                                            IsEmbedded = true
                                        };
                                            
                                        string texKey = texture.Name.ToLower();
                                        if (textureReferences.TryAdd(texKey, reference))
                                        {
                                            hasEmbeddedTextures = true;
                                            Debug.Log($"Cached embedded texture reference '{texture.Name}' from {kvp.Key}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"Failed to cache embedded texture '{texture.Name}': {ex.Message}");
                                    }
                                }
                            }
                        }
                        else if (model is ModelFragTypeFile fragModel && fragModel.EmbeddedTextureFile != null)
                        {
                            var embeddedTextures = fragModel.EmbeddedTextureFile;
                            foreach (var texture in embeddedTextures.Textures)
                            {
                                if (!string.IsNullOrEmpty(texture.Name))
                                {
                                    try
                                    {
                                        var reference = new TextureReference
                                        {
                                            TextureName = texture.Name,
                                            SourceFile = kvp.Key,
                                            RageTexture = texture,
                                            RawData = null,
                                            IsEmbedded = true
                                        };
                                            
                                        string texKey = texture.Name.ToLower();
                                        if (textureReferences.TryAdd(texKey, reference))
                                        {
                                            hasEmbeddedTextures = true;
                                            Debug.Log($"Cached embedded texture reference '{texture.Name}' from fragment {kvp.Key}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"Failed to cache embedded texture '{texture.Name}': {ex.Message}");
                                    }
                                }
                            }
                        }
                        var modelNode = model.GetModel(null);
                        if (modelNode != null)
                        {
                            ExtractAndResolveMaterials(modelNode, kvp.Key);
                        }
                        if (!hasEmbeddedTextures)
                        {
                            model.Dispose();
                        }
                        else
                        {
                            Debug.Log($"Keeping model {kvp.Key} in memory due to embedded textures");
                        }
                        
                        var current = System.Threading.Interlocked.Increment(ref processedCount);
                        if (current % 500 == 0)
                        {
                            Debug.Log($"Processed {current}/{modelFiles.Count} models...");
                        }
                    }
                    catch
                    {

                    }
                });
            });
            
            Debug.Log($"Material index complete: {processedCount} models processed, {materialIndex.Count} resolved materials");
        }
        
        private static void ExtractAndResolveMaterials(ModelNode node, string modelName)
        {
            if (node == null) return;
            
            if (node.Model3D?.material != null)
            {
                var mat = node.Model3D.material;
                if (!string.IsNullOrEmpty(mat.shaderName))
                {
                    string materialKey = GetMaterialKey(mat.shaderName, mat.textureName);
                    
                    var def = new MaterialDefinition
                    {
                        ShaderName = mat.shaderName,
                        TextureName = mat.textureName,
                        SourceModel = modelName,
                        HasTexture = !string.IsNullOrEmpty(mat.textureName),
                        ResolvedTexture = TryResolveTexture(mat.textureName)
                    };
                    
                    materialIndex.AddOrUpdate(materialKey, def, (key, existing) =>
                    {
                        
                        bool newHasResolvedTexture = def.ResolvedTexture != null;
                        bool existingHasResolvedTexture = existing.ResolvedTexture != null;
                        
                        if (newHasResolvedTexture && !existingHasResolvedTexture)
                        {
                            Debug.Log($"Updated material {materialKey} with resolved texture from {modelName} (replacing {existing.SourceModel})");
                            return def;
                        }
                        else if (!newHasResolvedTexture && existingHasResolvedTexture)
                        {
                            return existing;
                        }
                        else if (newHasResolvedTexture && existingHasResolvedTexture)
                        {
                            return existing;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(def.TextureName) && string.IsNullOrEmpty(existing.TextureName))
                            {
                                Debug.Log($"Updated material {materialKey} with texture name from {modelName}");
                                return def;
                            }
                            return existing;
                        }
                    });
                }
            }
            foreach (var child in node.Children)
            {
                ExtractAndResolveMaterials(child, modelName);
            }
        }
        
        private static TextureReference TryResolveTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;
            
            string key = textureName.ToLower();
            if (textureReferences.TryGetValue(key, out var reference))
            {
                if (reference != null && reference.RageTexture != null)
                {
                    return reference;
                }
                else
                {
                    Debug.LogWarning($"Found texture reference for '{textureName}' but RageTexture is null");
                }
            }
            string[] variations = { key + "_diff", key.Replace("_diff", ""), key + "_d", key.Replace("_d", "") };
            foreach (var variation in variations)
            {
                if (textureReferences.TryGetValue(variation, out var varRef))
                {
                    if (varRef != null && varRef.RageTexture != null)
                    {
                        Debug.Log($"Found texture '{textureName}' as variation '{variation}'");
                        return varRef;
                    }
                }
            }
            Debug.LogWarning($"Could not resolve texture '{textureName}' - not found in any texture dictionary");
            return null;
        }
        
        private static void ProcessTexturesOptimized(List<KeyValuePair<string, File>> wtdFiles)
        {
            Debug.Log($"Processing {wtdFiles.Count} texture files with optimized batching...");
            
            var processedCount = 0;
            var lastReportTime = DateTime.Now;
            
            var partitioner = Partitioner.Create(wtdFiles, true);
            
            Parallel.ForEach(partitioner, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
            },
            kvp =>
            {
                try
                {
                    var data = kvp.Value.GetData();
                    if (data == null || data.Length == 0) return;
                    
                    var textureFile = new TextureFile();
                    textureFile.Open(data);
                    textureFile.Read();
                    var texturesToAdd = new List<KeyValuePair<string, TextureReference>>();
                    
                    foreach (var texture in textureFile.Textures)
                    {
                        if (!string.IsNullOrEmpty(texture.Name))
                        {
                            var reference = new TextureReference
                            {
                                TextureName = texture.Name,
                                SourceFile = kvp.Key,
                                RageTexture = texture,
                                IsEmbedded = false
                            };
                            
                            texturesToAdd.Add(new KeyValuePair<string, TextureReference>(
                                texture.Name.ToLower(), reference));
                        }
                    }
                    foreach (var tex in texturesToAdd)
                    {
                        textureReferences.TryAdd(tex.Key, tex.Value);
                    }
                    
                    var current = System.Threading.Interlocked.Increment(ref processedCount);
                    if (current % 100 == 0)
                    {
                        Debug.Log($"Processed {current}/{wtdFiles.Count} texture files...");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to process {kvp.Key}: {ex.Message}");
                }
            });
            
            Debug.Log($"Completed processing {processedCount} texture files");
        }
        
        
        private static void ProcessFileInBackground(List<KeyValuePair<string, File>> modelFiles)
        {
            Debug.Log($"Processing {modelFiles.Count} model files with optimized batching...");
            
            var processedCount = 0;
            
            var partitioner = Partitioner.Create(modelFiles, true);
            
            Parallel.ForEach(partitioner, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
            },
            kvp =>
            {
                try
                {
                    var data = kvp.Value.GetData();
                    if (data == null || data.Length == 0) return;
                    
                    string ext = Path.GetExtension(kvp.Key).ToLower();
                    IModelFile model = null;
                    
                    if (ext == ".wft")
                    {
                        model = new ModelFragTypeFile();
                    }
                    else
                    {
                        model = new ModelFile();
                    }
                    
                    using (var ms = new MemoryStream(data))
                    {
                        model.Open(ms);
                    }
                    
                    if (model is ModelFile modelFile && modelFile.EmbeddedTextureFile != null)
                    {
                        var embeddedTextures = modelFile.EmbeddedTextureFile;
                        foreach (var texture in embeddedTextures.Textures)
                        {
                            if (!string.IsNullOrEmpty(texture.Name))
                            {
                                var reference = new TextureReference
                                {
                                    TextureName = texture.Name,
                                    SourceFile = kvp.Key,
                                    RageTexture = texture,
                                    IsEmbedded = true
                                };
                                
                                textureReferences.TryAdd(texture.Name.ToLower(), reference);
                            }
                        }
                    }
                    else if (model is ModelFragTypeFile fragModel && fragModel.EmbeddedTextureFile != null)
                    {
                        var embeddedTextures = fragModel.EmbeddedTextureFile;
                        foreach (var texture in embeddedTextures.Textures)
                        {
                            if (!string.IsNullOrEmpty(texture.Name))
                            {
                                var reference = new TextureReference
                                {
                                    TextureName = texture.Name,
                                    SourceFile = kvp.Key,
                                    RageTexture = texture,
                                    IsEmbedded = true
                                };
                                
                                textureReferences.TryAdd(texture.Name.ToLower(), reference);
                            }
                        }
                    }
                    
                    model.Dispose();
                    
                    var current = System.Threading.Interlocked.Increment(ref processedCount);
                    if (current % 500 == 0)
                    {
                        Debug.Log($"Processed {current}/{modelFiles.Count} model files...");
                    }
                }
                catch (Exception ex)
                {
                }
            });
            
            Debug.Log($"Completed processing {processedCount} model files");
        }
        
  
        
        private static void RegisterMaterialReferencesFromModel(string modelName, ModelFile model)
        {
            if (model == null) return;
            
            try
            {
                var modelNode = model.GetModel(null);
                if (modelNode != null)
                {
                    ScanModelNodeForMaterials(modelNode, modelName);
                }
            }
            catch
            {
                // Silent fail
            }
        }
        
        private static void ScanModelNodeForMaterials(ModelNode node, string modelName)
        {
            if (node == null) return;
            
            if (node.Model3D?.material != null)
            {
                var material = node.Model3D.material;
                
                if (!string.IsNullOrEmpty(material.shaderName))
                {
                    // Store material reference
                    var reference = new MaterialReference
                    {
                        ShaderName = material.shaderName,
                        TextureName = material.textureName,
                        SourceFile = modelName,
                        HasEmbeddedTexture = material.mainTex != null
                    };
                    
                    string materialKey = GetMaterialKey(material.shaderName, material.textureName);
                    materialReferences.TryAdd(materialKey, reference);
                }
            }
            
            foreach (var child in node.Children)
            {
                ScanModelNodeForMaterials(child, modelName);
            }
        }

        private static Texture2D GetOrCreateTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return null;
                
            string key = textureName.ToLower();
            if (loadedTextures.TryGetValue(key, out Texture2D loaded))
            {
                Debug.Log($"Using pre-loaded texture: {textureName}");
                return loaded;
            }
            if (textureReferences.TryGetValue(key, out TextureReference reference))
            {
                try
                {
                    if (reference.RageTexture != null)
                    {
                        var rageUnityTexture = reference.RageTexture.Decode();
                        var unityTexture = rageUnityTexture?.GetUnityTexture();
                        if (unityTexture != null)
                        {
                            loadedTextures.TryAdd(key, unityTexture);
                            Debug.Log($"Loaded texture on-demand: {textureName} from {reference.SourceFile} (embedded: {reference.IsEmbedded})");
                            return unityTexture;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid texture reference for '{textureName}' - RageTexture is null");
                        textureReferences.TryRemove(key, out _);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to create texture {textureName}: {ex.Message}");
                }
            }
            else
            {
                string[] variations = {
                    key,
                    key + "_diff",
                    key.Replace("_diff", ""),
                    key + "_d",
                    key.Replace("_d", "")
                };
                
                foreach (var variation in variations)
                {
                    if (textureReferences.TryGetValue(variation, out TextureReference varRef))
                    {
                        try
                        {
                            var rageUnityTexture = varRef.RageTexture.Decode();
                            var unityTexture = rageUnityTexture?.GetUnityTexture();
                            if (unityTexture != null)
                            {
                                loadedTextures.TryAdd(key, unityTexture);
                                Debug.Log($"Loaded texture with variation: {textureName} -> {variation} from {varRef.SourceFile}");
                                return unityTexture;
                            }
                        }
                        catch { }
                    }
                }
                Debug.LogWarning($"Texture not found in any form: {textureName} (Checked {textureReferences.Count} references)");
            }
            
            return null;
        }
        
        public static void RegisterMaterialFromModel(string modelName, ModelFile model)
        {
            if (model == null || scannedModels.Contains(modelName))
                return;
                
            scannedModels.Add(modelName);
            
            try
            {
                RegisterMaterialReferencesFromModel(modelName, model);
                
                var embeddedTextures = model.EmbeddedTextureFile;
                if (embeddedTextures != null)
                {
                    foreach (var texture in embeddedTextures.Textures)
                    {
                        if (!string.IsNullOrEmpty(texture.Name))
                        {
                            RegisterTextureReference(texture.Name, texture, modelName, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to scan model {modelName} for materials: {ex.Message}");
            }
        }
        
        public static void RegisterTextureReference(string textureName, RageLib.Textures.Texture rageTexture, string source, bool isEmbedded)
        {
            if (string.IsNullOrEmpty(textureName) || rageTexture == null)
                return;
                
            try
            {
                var reference = new TextureReference
                {
                    TextureName = textureName,
                    SourceFile = source,
                    RageTexture = rageTexture,
                    IsEmbedded = isEmbedded
                };
                
                textureReferences.TryAdd(textureName.ToLower(), reference);
                string[] suffixes = { "_diff", "_spec", "_norm", "_detail" };
                foreach (var suffix in suffixes)
                {
                    if (textureName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        string baseName = textureName.Substring(0, textureName.Length - suffix.Length);
                        textureReferences.TryAdd(baseName.ToLower(), reference);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to register texture reference {textureName}: {ex.Message}");
            }
        }
        
        private static string GetMaterialKey(string shaderName, string textureName)
        {
            // Use texture name as the primary key for material sharing
            // This ensures all materials using the same texture share the same Unity material
            // IMPORTANT: Normalize to lowercase to prevent case-sensitivity issues
            
            // If no texture name, use shader name as fallback
            if (string.IsNullOrEmpty(textureName))
            {
                // For untextured materials, group by shader
                return $"notex_{shaderName?.ToLower() ?? "default"}";
            }
            
            // Use only texture name as key - all materials with same texture should share
            // This fixes the issue where different shaders with same texture weren't sharing
            return textureName.ToLower();
        }

        
        /// <summary>
        /// Get or create a shared Unity Material using the pre-resolved material index
        /// </summary>
        public static Material GetOrCreateSharedMaterial(string shaderName, string textureName, Texture2D embeddedTexture = null)
        {
            string materialKey = GetMaterialKey(shaderName, textureName);
            
            if (sharedMaterials.TryGetValue(materialKey, out Material existingMaterial))
            {
                return existingMaterial;
            }
            
            lock (materialCreationLock)
            {
                if (sharedMaterials.TryGetValue(materialKey, out existingMaterial))
                {
                    return existingMaterial;
                }
                
                MaterialDefinition materialDef = null;
                if (materialIndex.TryGetValue(materialKey, out materialDef))
                {
                    Debug.Log($"Using indexed material: {materialKey} from {materialDef.SourceModel}");
                    
                    if (materialDef.ResolvedTexture == null && materialDef.HasTexture)
                    {
                        Debug.LogWarning($"Material {materialKey} expects texture '{materialDef.TextureName}' but ResolvedTexture is null");
                        materialDef.ResolvedTexture = TryResolveTexture(materialDef.TextureName);
                        if (materialDef.ResolvedTexture != null)
                        {
                            Debug.Log($"Successfully resolved texture '{materialDef.TextureName}' on second attempt");
                        }
                    }
                }
                else
                {
                    materialDef = new MaterialDefinition
                    {
                        ShaderName = shaderName,
                        TextureName = textureName,
                        SourceModel = "runtime",
                        HasTexture = embeddedTexture != null || !string.IsNullOrEmpty(textureName),
                        ResolvedTexture = TryResolveTexture(textureName)
                    };
                    Debug.Log($"Created runtime material definition for {materialKey}, HasTexture: {materialDef.HasTexture}, Resolved: {materialDef.ResolvedTexture != null}");
                }
                
                Material unityMaterial;
                try
                {
                    unityMaterial = new Material(Shader.Find("gta_default"));
                    unityMaterial.name = materialKey;
                    unityMaterial.enableInstancing = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to create material '{materialKey}': {ex.Message}");
                    return null;
                }
                
                Texture2D finalTexture = null;
                
                if (embeddedTexture != null)
                {
                    finalTexture = embeddedTexture;
                }
                else if (materialDef.ResolvedTexture != null)
                {
                    finalTexture = GetOrCreateTexture(materialDef.ResolvedTexture.TextureName);
                }
                else if (!string.IsNullOrEmpty(textureName))
                {
                    finalTexture = GetOrCreateTexture(textureName);
                }
                
                if (finalTexture != null)
                {
                    unityMaterial.SetTexture("_MainTex", finalTexture);
                    Debug.Log($"Material '{materialKey}' created with texture");
                }
                else if (materialDef.HasTexture)
                {
                    Debug.LogWarning($"Material '{materialKey}' expects texture '{materialDef.TextureName}' but none could be loaded. ResolvedTexture exists: {materialDef.ResolvedTexture != null}");
                    
                    var similarKeys = textureReferences.Keys.Where(k => k.Contains(materialKey.Replace("_", ""))).Take(5).ToList();
                    if (similarKeys.Any())
                    {
                        Debug.Log($"Similar textures found: {string.Join(", ", similarKeys)}");
                    }
                }
                
                sharedMaterials.TryAdd(materialKey, unityMaterial);
                
                return unityMaterial;
            }
        }
        
        public static void RegisterMaterial(string shaderName, string textureName, Material material)
        {
            if (material == null) return;
            
            string materialKey = GetMaterialKey(shaderName, textureName);
            if (!sharedMaterials.ContainsKey(materialKey))
            {
                sharedMaterials.TryAdd(materialKey, material);
                Debug.Log($"Registered shared material: {materialKey}");
            }
            else if (material.mainTexture != null)
            {
                sharedMaterials[materialKey] = material;
                Debug.Log($"Updated shared material with texture: {materialKey}");
            }
        }
    }
}