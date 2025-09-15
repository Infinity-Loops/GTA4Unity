using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using RageLib.Models;
using RageLib.Textures;
using RageLib.Models.Data;
using RageLib.FileSystem.Common;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity
{
    public class HighPerformanceLoader : MonoBehaviour
    {
        public static HighPerformanceLoader Instance;
        
        [Header("Performance Settings")]
        [SerializeField] private int maxParallelFileLoads = 8;
        [SerializeField] private int maxParallelMeshJobs = 16;
        [SerializeField] private int batchSize = 50;
        [SerializeField] private float maxLoadTimePerFrame = 8.0f;
        
        [Header("Streaming Settings")]
        [SerializeField] private float streamDistance = 300f;
        [SerializeField] private float cullDistance = 500f;
        [SerializeField] private float lodSwitchDistance = 150f;
        [SerializeField] private int maxActiveObjects = 2000;
        
        [Header("Memory Management")]
        [SerializeField] private int modelCacheSize = 500;
        [SerializeField] private int textureCacheSize = 300;
        [SerializeField] private bool aggressiveCaching = true;
        private SpatialGrid spatialGrid;
        private Transform playerTransform;
        private readonly ConcurrentDictionary<string, ModelData> modelCache = new();
        private readonly ConcurrentDictionary<string, TextureData> textureCache = new();
        private readonly ConcurrentDictionary<uint, LoadedObject> activeObjects = new();
        private readonly ConcurrentBag<uint> recyclePool = new();
        private PriorityQueue<LoadTask> highPriorityQueue = new();
        private PriorityQueue<LoadTask> normalPriorityQueue = new();
        private PriorityQueue<LoadTask> lowPriorityQueue = new();
        private List<JobHandle> activeJobs = new();
        private CancellationTokenSource cancellationToken;
        private float frameStartTime;
        private int objectsLoadedThisFrame;
        private PerformanceStats stats = new();
        private struct SpawnData
        {
            public LoadTask Task;
            public ModelData Model;
            public TextureData Texture;
        }
        private readonly Queue<SpawnData> spawnQueue = new Queue<SpawnData>();
        private float lastBatchSpawnTime;
        private const float batchSpawnInterval = 0.033f;
        private const int maxSpawnsPerBatch = 10;
        private readonly Queue<GameObject> objectPool = new Queue<GameObject>();
        private const int poolSize = 100;
        private const float loadDistance = 400f;
        private const float unloadDistance = 600f;
        private readonly ConcurrentDictionary<uint, bool> pendingLoads = new ConcurrentDictionary<uint, bool>();
        private readonly ConcurrentDictionary<uint, bool> pendingUnloads = new ConcurrentDictionary<uint, bool>();
        
        private GTADatLoader dataLoader;
        private int totalObjectsToLoad = 0;
        private int objectsProcessed = 0;
        private bool initialLoadComplete = false;
        private bool isInitializing = true;
        private float initStartTime;
        private Dictionary<string, List<File>> assetsByName = new Dictionary<string, List<File>>();
        private Dictionary<uint, string> hashToName = new Dictionary<uint, string>();
        private HashSet<string> missingAssetsLog = new HashSet<string>();
        
        
        private class ObjectInstance
        {
            public uint Id;
            public string ModelName;
            public string TextureName;
            public Vector3 Position;
            public Vector3 UnityPosition;
            public Quaternion Rotation;
            public Ipl_INST Instance;
            public Item_OBJS Definition;
            public bool IsLoaded;
        }
        private Dictionary<uint, ObjectInstance> allGameObjects = new Dictionary<uint, ObjectInstance>();
        private SpatialGrid objectIndex; 
        
        private class LoadTask
        {
            public uint Id;
            public string ModelName;
            public string TextureName;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public float Priority;
            public int LODLevel;
            public Ipl_INST Instance;
            public Item_OBJS Definition;
        }
        
        private class LoadedObject
        {
            public GameObject GameObject;
            public int LODLevel;
            public DateTime LastAccessTime;
            public Bounds Bounds;
            public bool IsVisible;
        }
        
        private class ModelData
        {
            public IModelFile Model; 
            public UnityEngine.Mesh[] Meshes;
            public UnityEngine.Material[] Materials;
            public DateTime LastUsed;
            public int RefCount;
        }
        
        private class TextureData
        {
            public TextureFile File;
            public Texture2D[] Textures;
            public DateTime LastUsed;
            public int RefCount;
        }
        
        private class PerformanceStats
        {
            public int TotalLoaded;
            public int TotalUnloaded;
            public float AverageLoadTime;
            public float PeakMemoryUsage;
            public int CacheHits;
            public int CacheMisses;
        }
        
        private void Awake()
        {
            Instance = this;
            cancellationToken = new CancellationTokenSource();
            spatialGrid = new SpatialGrid(6000f, 100f); 
            objectIndex = new SpatialGrid(6000f, 100f); 
        }
        
        private void OnDestroy()
        {
            cancellationToken?.Cancel();
            CompleteAllJobs();
            CleanupResources();
        }
        
        public async void Initialize(GTADatLoader loader, Transform player)
        {
            dataLoader = loader;
            playerTransform = player;
            initStartTime = Time.time;
            
            
            LoadingScreen.AdvanceProgress("Indexing game assets (this is one-time only)...", 0);
            
            
            var cacheStart = System.DateTime.Now;
            await IVUnity.MaterialTextureResolver.Initialize(dataLoader);
            var cacheTime = (System.DateTime.Now - cacheStart).TotalSeconds;
            
            LoadingScreen.AdvanceProgress($"Indexed in {cacheTime:F1}s. Setting up world streaming...", 0);
            
            
            if (FocusPoint.Instance == null)
            {
                GameObject focusPointGO = new GameObject("FocusPoint");
                focusPointGO.AddComponent<FocusPoint>();
            }
            
            
            Vector3 initialPosition = Vector3.zero;
            if (playerTransform != null)
            {
                FocusPoint.SetTarget(playerTransform);
                initialPosition = playerTransform.position;
                Debug.Log($"Using player position for initial load: {initialPosition}");
            }
            else if (Camera.main != null)
            {
                FocusPoint.SetTarget(Camera.main.transform);
                initialPosition = Camera.main.transform.position;
                Debug.Log($"Using camera position for initial load: {initialPosition}");
            }
            else
            {
                
                initialPosition = new Vector3(1000, 50, 1000);
                Debug.LogWarning($"No player or camera found, using default position: {initialPosition}");
            }
            
            
            FocusPoint.SetPosition(initialPosition);
            
            
            CreateWater();
            
            
            BuildAssetLookupTables();
            
            
            BuildObjectIndex();
            
            
            QueueObjectsAroundPosition(initialPosition, cullDistance);
            
            
            
            
            if (totalObjectsToLoad > 0)
            {
                LoadingScreen.SetupLoadingTarget(totalObjectsToLoad);
                LoadingScreen.ResetProgress();
                LoadingScreen.AdvanceProgress("Loading world objects...", 0);
                Debug.Log($"Starting to load {totalObjectsToLoad} objects around FocusPoint");
            }
            
            
            StartBackgroundWorkers();
            
            
            _ = ReportMissingAssetsAfterDelay();
        }
        
        private async Task ReportMissingAssetsAfterDelay()
        {
            
            await Task.Delay(10000); 
            
            if (missingAssetsLog.Count > 0)
            {
                Debug.LogWarning($"===== MISSING ASSETS REPORT =====");
                Debug.LogWarning($"Total missing models: {missingAssetsLog.Count}");
                
                
                var missingByExtension = new Dictionary<string, List<string>>();
                foreach (var asset in missingAssetsLog)
                {
                    string ext = Path.GetExtension(asset);
                    if (string.IsNullOrEmpty(ext)) ext = "no_extension";
                    
                    if (!missingByExtension.ContainsKey(ext))
                        missingByExtension[ext] = new List<string>();
                    missingByExtension[ext].Add(asset);
                }
                
                foreach (var kvp in missingByExtension)
                {
                    Debug.LogWarning($"{kvp.Key}: {kvp.Value.Count} missing");
                    
                    for (int i = 0; i < Math.Min(10, kvp.Value.Count); i++)
                    {
                        Debug.LogWarning($"  - {kvp.Value[i]}");
                    }
                    if (kvp.Value.Count > 10)
                    {
                        Debug.LogWarning($"  ... and {kvp.Value.Count - 10} more");
                    }
                }
                Debug.LogWarning($"=================================");
            }
        }
        
        private void StartBackgroundWorkers()
        {
            
            for (int i = 0; i < maxParallelFileLoads; i++)
            {
                Task.Run(FileLoadWorker, cancellationToken.Token);
            }
            
            
            for (int i = 0; i < 2; i++)
            {
                Task.Run(MeshGenerationWorker, cancellationToken.Token);
            }
            
            
            Task.Run(CacheManagementWorker, cancellationToken.Token);
        }
        
        private async Task FileLoadWorker()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                LoadTask task = null;
                
                
                if (!highPriorityQueue.TryDequeue(out task))
                {
                    if (!normalPriorityQueue.TryDequeue(out task))
                    {
                        lowPriorityQueue.TryDequeue(out task);
                    }
                }
                
                if (task != null)
                {
                    await ProcessLoadTask(task);
                }
                else
                {
                    await Task.Delay(10);
                }
            }
        }
        
        private async Task ProcessLoadTask(LoadTask task)
        {
            try
            {
                
                if (activeObjects.ContainsKey(task.Id))
                    return;
                
                
                if (!initialLoadComplete)
                {
                    
                    string loadingText = Path.GetFileNameWithoutExtension(task.ModelName);
                    if (!string.IsNullOrEmpty(task.Definition?.modelName))
                    {
                        loadingText = task.Definition.modelName;
                    }
                    
                    LoadingScreen.AdvanceProgress(loadingText);
                    objectsProcessed++;
                    
                    
                    if (!initialLoadComplete && objectsProcessed >= totalObjectsToLoad * 0.9f) 
                    {
                        initialLoadComplete = true;
                        isInitializing = false; 
                        LoadingScreen.Finish();
                        Debug.Log($"Initial load complete ({objectsProcessed}/{totalObjectsToLoad}), streaming enabled, isInitializing={isInitializing}");
                        
                        
                        UpdateStreaming();
                        
                        
                        if (missingAssetsLog.Count > 0)
                        {
                            Debug.LogWarning($"Total missing assets: {missingAssetsLog.Count}");
                            Debug.LogWarning($"First 20 missing assets: {string.Join(", ", missingAssetsLog.Take(20))}");
                            
                            
                            var byExtension = missingAssetsLog.GroupBy(x => Path.GetExtension(x))
                                .OrderByDescending(g => g.Count())
                                .Select(g => $"{g.Key}: {g.Count()}");
                            Debug.LogWarning($"Missing by type: {string.Join(", ", byExtension)}");
                        }
                    }
                }
                
                
                var modelData = await GetOrLoadModelData(task.ModelName);
                if (modelData == null) return;
                
                
                TextureData textureData = null;
                if (!string.IsNullOrEmpty(task.TextureName))
                {
                    textureData = await GetOrLoadTextureData(task.TextureName);
                }
                
                
                if (textureData == null && modelData != null && modelData.Model != null)
                {
                    try
                    {
                        TextureFile embeddedTexture = null;
                        if (modelData.Model is ModelFile modelFile)
                        {
                            embeddedTexture = modelFile.EmbeddedTextureFile;
                        }
                        else if (modelData.Model is ModelFragTypeFile fragModel)
                        {
                            embeddedTexture = fragModel.EmbeddedTextureFile;
                        }
                        if (embeddedTexture != null)
                        {
                            textureData = new TextureData
                            {
                                File = embeddedTexture,
                                LastUsed = DateTime.Now,
                                RefCount = 1
                            };
                            Debug.Log($"Using embedded texture for {task.ModelName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to get embedded texture for {task.ModelName}: {ex.Message}");
                    }
                }
                
                
                lock (spawnQueue)
                {
                    spawnQueue.Enqueue(new SpawnData 
                    { 
                        Task = task, 
                        Model = modelData, 
                        Texture = textureData 
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load {task.ModelName}: {e.Message}");
            }
        }
        
        private async Task<ModelData> GetOrLoadModelData(string modelName)
        {
            
            if (modelCache.TryGetValue(modelName, out ModelData cached))
            {
                cached.LastUsed = DateTime.Now;
                cached.RefCount++;
                stats.CacheHits++;
                return cached;
            }
            
            stats.CacheMisses++;
            
            
            string baseName = Path.GetFileNameWithoutExtension(modelName);
            string extension = Path.GetExtension(modelName);
            
            
            File file = null;
            string actualModelName = modelName; 
            
            if (string.IsNullOrEmpty(extension))
            {
                
                string[] modelExtensions = { ".wdr", ".wdd", ".wft", ".wbn", ".wbd" };
                foreach (var ext in modelExtensions)
                {
                    file = FindAssetFile(baseName, ext);
                    if (file != null)
                    {
                        extension = ext;
                        actualModelName = baseName + ext; 
                        Debug.Log($"Found {actualModelName} - Size: {file.Size}, IsResource: {file.IsResource}, IsCompressed: {file.IsCompressed}");
                        break;
                    }
                }
            }
            else
            {
                file = FindAssetFile(baseName, extension);
                if (file != null)
                {
                    Debug.Log($"Found {modelName} - Size: {file.Size}, IsResource: {file.IsResource}, IsCompressed: {file.IsCompressed}");
                }
            }
            
            if (file == null)
            {
                if (!missingAssetsLog.Contains(modelName))
                {
                    missingAssetsLog.Add(modelName);
                    Debug.LogWarning($"Model not found: {modelName} (searched as {baseName} with various extensions)");
                }
                stats.CacheMisses++;
                return null;
            }
            
            string finalModelName = actualModelName; 
            
            return await Task.Run(() =>
            {
                try
                {
                    var data = file.GetData();
                    
                    
                    if (data == null || data.Length == 0)
                    {
                        Debug.LogError($"Model file {finalModelName} has no data or is empty");
                        return null;
                    }
                    
                    
                    string ext = Path.GetExtension(finalModelName).ToLower();
                    IModelFile model = null;
                    
                    
                    if (ext == ".wft")
                    {
                        
                        model = new ModelFragTypeFile();
                        Debug.Log($"Loading WFT fragment model: {finalModelName}");
                    }
                    else
                    {
                        
                        model = new ModelFile();
                    }
                    
                    
                    try
                    {
                        
                        using (var stream = new MemoryStream(data))
                        {
                            model.Open(stream);
                        }
                    }
                    catch (EndOfStreamException eosEx)
                    {
                        Debug.LogError($"File {modelName} appears truncated or corrupted. Size: {data.Length} bytes. Error: {eosEx.Message}");
                        
                        
                        if (data.Length > 0)
                        {
                            string firstBytes = BitConverter.ToString(data, 0, Math.Min(32, data.Length));
                            Debug.LogError($"First 32 bytes: {firstBytes}");
                            
                            if (data.Length > 32)
                            {
                                int lastStart = Math.Max(0, data.Length - 32);
                                string lastBytes = BitConverter.ToString(data, lastStart, Math.Min(32, data.Length - lastStart));
                                Debug.LogError($"Last 32 bytes: {lastBytes}");
                            }
                        }
                        return null;
                    }
                    
                    
                    
                    if (model is ModelFile standardModel)
                    {
                        IVUnity.MaterialTextureResolver.RegisterMaterialFromModel(modelName, standardModel);
                    }
                    
                    var modelData = new ModelData
                    {
                        Model = model,
                        LastUsed = DateTime.Now,
                        RefCount = 1
                    };
                    
                    
                    if (modelCache.Count < modelCacheSize)
                    {
                        modelCache.TryAdd(modelName, modelData);
                    }
                    
                    return modelData;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load model {modelName}: {e.Message}");
                    
                    
                    if (e.Message.Contains("Not a valid resource"))
                    {
                        Debug.LogError($"File {modelName} may be corrupted or in an unsupported format. IsResource: {file.IsResource}, Size: {file.Size}, Compressed: {file.IsCompressed}, ResourceType: {file.ResourceType}");
                        
                        
                        var data = file.GetData();
                        if (data != null && data.Length >= 16)
                        {
                            uint magic = System.BitConverter.ToUInt32(data, 0);
                            Debug.LogError($"First 4 bytes (magic): 0x{magic:X8}, Expected: 0x05435352 (RSC\\x05)");
                            
                            
                            string hexBytes = "";
                            for (int i = 0; i < Math.Min(32, data.Length); i++)
                            {
                                hexBytes += $"{data[i]:X2} ";
                                if ((i + 1) % 16 == 0) hexBytes += "\n";
                            }
                            Debug.LogError($"First 32 bytes:\n{hexBytes}");
                            
                            
                            bool looksLikeRandom = true;
                            for (int i = 1; i < Math.Min(16, data.Length); i++)
                            {
                                if (data[i] == data[0])
                                {
                                    looksLikeRandom = false;
                                    break;
                                }
                            }
                            
                            if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x00)
                            {
                                Debug.LogError("Data starts with null bytes - may be reading wrong offset");
                            }
                            else if (looksLikeRandom)
                            {
                                Debug.LogError("Data looks random - may be encrypted or compressed");
                            }
                            
                            
                            if (file.ParentDirectory != null)
                            {
                                Debug.LogError($"File source: {file.ParentDirectory.Name}/{file.Name}");
                            }
                            
                            
                            if (modelName.Contains("BM_wall_light_07"))
                            {
                                IMGResourceDebugger.DebugProblematicFile();
                            }
                        }
                    }
                    
                    return null;
                }
            });
        }
        
        private async Task<TextureData> GetOrLoadTextureData(string textureName)
        {
            
            if (textureCache.TryGetValue(textureName, out TextureData cached))
            {
                cached.LastUsed = DateTime.Now;
                cached.RefCount++;
                return cached;
            }
            
            
            string baseName = Path.GetFileNameWithoutExtension(textureName);
            string extension = Path.GetExtension(textureName);
            if (string.IsNullOrEmpty(extension)) extension = ".wtd";
            
            File file = FindAssetFile(baseName, extension);
            if (file == null)
            {
                Debug.LogWarning($"Texture not found: {textureName}");
                return null;
            }
            
            return await Task.Run(() =>
            {
                try
                {
                    var texture = new TextureFile();
                    texture.Open(file.GetData());
                    texture.Read();
                    
                    
                    
                    int registeredCount = 0;
                    foreach (var tex in texture.Textures)
                    {
                        if (!string.IsNullOrEmpty(tex.Name))
                        {
                            IVUnity.MaterialTextureResolver.RegisterTextureReference(
                                tex.Name, tex, textureName, false);
                            registeredCount++;
                        }
                    }
                    
                    if (registeredCount > 0)
                    {
                        Debug.Log($"Registered {registeredCount} textures from {textureName} to MaterialTextureResolver");
                    }
                    
                    var textureData = new TextureData
                    {
                        File = texture,
                        LastUsed = DateTime.Now,
                        RefCount = 1
                    };
                    
                    
                    if (textureCache.Count < textureCacheSize)
                    {
                        textureCache.TryAdd(textureName, textureData);
                    }
                    
                    return textureData;
                }
                catch
                {
                    return null;
                }
            });
        }
        
        private async Task MeshGenerationWorker()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                
                await Task.Delay(16); 
            }
        }
        
        private async Task CacheManagementWorker()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                await Task.Delay(5000); 
                
                
                CleanupCache();
            }
        }
        
        private void CleanupCache()
        {
            DateTime currentTime = DateTime.Now;
            TimeSpan cacheTimeout = TimeSpan.FromSeconds(30); 
            
            
            var modelsToRemove = new List<string>();
            foreach (var kvp in modelCache)
            {
                if (kvp.Value.RefCount == 0 && 
                    currentTime - kvp.Value.LastUsed > cacheTimeout)
                {
                    modelsToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in modelsToRemove)
            {
                if (modelCache.TryRemove(key, out ModelData data))
                {
                    data.Model?.Dispose();
                }
            }
            
            
            var texturesToRemove = new List<string>();
            foreach (var kvp in textureCache)
            {
                if (kvp.Value.RefCount == 0 && 
                    currentTime - kvp.Value.LastUsed > cacheTimeout)
                {
                    texturesToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in texturesToRemove)
            {
                if (textureCache.TryRemove(key, out TextureData data))
                {
                    data.File?.Dispose();
                    if (data.Textures != null)
                    {
                        foreach (var tex in data.Textures)
                        {
                            if (tex != null) Destroy(tex);
                        }
                    }
                }
            }
        }
        
        private void LoadModelNodeDirectly(GameObject parent, ModelNode node)
        {
            if (node == null) return;
            
            
            if (node.Model3D?.geometry != null)
            {
                var meshFilter = parent.AddComponent<MeshFilter>();
                var meshRenderer = parent.AddComponent<MeshRenderer>();
                
                meshFilter.sharedMesh = node.Model3D.geometry.GetUnityMesh();
                
                if (node.Model3D.material != null)
                {
                    
                    var rageMaterial = node.Model3D.material;
                    Texture2D embeddedTex = null;
                    
                    if (rageMaterial.mainTex != null)
                    {
                        embeddedTex = rageMaterial.mainTex.GetUnityTexture();
                    }
                    
                    
                    var sharedMaterial = IVUnity.MaterialTextureResolver.GetOrCreateSharedMaterial(
                        rageMaterial.shaderName, rageMaterial.textureName, embeddedTex);
                    
                    meshRenderer.sharedMaterial = sharedMaterial;
                    
                    
                    if (sharedMaterial != null && sharedMaterial.mainTexture != null)
                    {
                        IVUnity.MaterialTextureResolver.RegisterMaterial(
                            rageMaterial.shaderName, rageMaterial.textureName, sharedMaterial);
                    }
                }
            }
            
            
            foreach (var child in node.Children)
            {
                var childGO = new GameObject(child.Name ?? "Child");
                childGO.transform.parent = parent.transform;
                childGO.transform.localPosition = Vector3.zero;
                childGO.transform.localRotation = Quaternion.identity;
                LoadModelNodeDirectly(childGO, child);
            }
        }
        
        private void CreateObjectFromDataPooled(LoadTask task, ModelData modelData, TextureData textureData)
        {
            
            GameObject go;
            if (objectPool.Count > 0)
            {
                go = objectPool.Dequeue();
                go.name = task.ModelName;
                go.SetActive(true);
            }
            else
            {
                go = new GameObject(task.ModelName);
            }
            
            CreateObjectFromDataInternal(go, task, modelData, textureData);
        }
        
        private void CreateObjectFromData(LoadTask task, ModelData modelData, TextureData textureData)
        {
            var go = new GameObject(task.ModelName);
            CreateObjectFromDataInternal(go, task, modelData, textureData);
        }
        
        private void CreateObjectFromDataInternal(GameObject go, LoadTask task, ModelData modelData, TextureData textureData)
        {
            go.transform.position =  task.Position;
            go.transform.rotation = task.Rotation;
            go.transform.localScale = task.Scale != Vector3.zero ? task.Scale : Vector3.one;
            go.transform.parent = transform;
            
            
            if (objectsLoadedThisFrame < 5) 
            {
                Debug.Log($"Created {task.ModelName} at position {task.Position}");
            }
            
            
            spatialGrid.Add(task.Id, go.transform.position);
            
            
            var geometry = go.AddComponent<StaticGeometry>();
            
            
            if (modelData.Model is ModelFile modelFile)
            {
                geometry.data.model = modelFile;
                geometry.data.collection = textureData?.File != null ? new[] { textureData.File } : null;
                geometry.data.modelFileName = task.ModelName;
                geometry.data.definitions = task.Instance;
                
                
                geometry.LoadModel();
            }
            else if (modelData.Model is ModelFragTypeFile fragModel)
            {
                
                
                try
                {
                    var modelNode = fragModel.GetModel(textureData?.File != null ? new[] { textureData.File } : null);
                    if (modelNode != null)
                    {
                        
                        LoadModelNodeDirectly(go, modelNode);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load fragment model {task.ModelName}: {ex.Message}");
                }
            }
            
            
            activeObjects[task.Id] = new LoadedObject
            {
                GameObject = go,
                LODLevel = task.LODLevel,
                LastAccessTime = DateTime.Now,
                Bounds = new Bounds(go.transform.position, Vector3.one * 10f),
                IsVisible = true
            };
            
            
            if (allGameObjects.TryGetValue(task.Id, out ObjectInstance objInst))
            {
                objInst.IsLoaded = true;
            }
            
            
            pendingLoads.TryRemove(task.Id, out _);
            
            stats.TotalLoaded++;
            objectsLoadedThisFrame++;
        }
        
        private void BuildObjectIndex()
        {
            if (dataLoader?.iplLoader?.ipls == null) return;
            
            Debug.Log("Building index of all game objects...");
            allGameObjects.Clear();
            objectIndex = new SpatialGrid(6000f, 100f);
            
            
            var objsDict = new Dictionary<string, Item_OBJS>();
            var objsByHash = new Dictionary<uint, Item_OBJS>();
            
            foreach (var ide in dataLoader.ideLoader.ides)
            {
                foreach (var obj in ide.items_objs)
                {
                    if (!string.IsNullOrEmpty(obj.modelName))
                    {
                        
                        if (!objsDict.ContainsKey(obj.modelName))
                        {
                            objsDict[obj.modelName] = obj;
                        }
                        
                        
                        uint hash = RageLib.Common.Hasher.Hash(obj.modelName);
                        if (!objsByHash.ContainsKey(hash))
                        {
                            objsByHash[hash] = obj;
                        }
                        
                        
                        if (!hashToName.ContainsKey(hash))
                        {
                            hashToName[hash] = obj.modelName;
                        }
                    }
                }
                
                
                
                foreach (var tobj in ide.items_tobj)
                {
                    if (!string.IsNullOrEmpty(tobj.modelName))
                    {
                        
                        Item_OBJS objFromTobj = tobj;
                        
                        
                        objFromTobj.flag2 = unchecked((int)0x80000000); 
                        
                        
                        if (!objsDict.ContainsKey(tobj.modelName))
                        {
                            objsDict[tobj.modelName] = objFromTobj;
                        }
                        
                        
                        uint hash = RageLib.Common.Hasher.Hash(tobj.modelName);
                        if (!objsByHash.ContainsKey(hash))
                        {
                            objsByHash[hash] = objFromTobj;
                        }
                        
                        
                        if (!hashToName.ContainsKey(hash))
                        {
                            hashToName[hash] = tobj.modelName;
                        }
                        
                        Debug.Log($"Added TOBJ to dictionary: {tobj.modelName} (will skip loading)");
                    }
                }
            }
            
            
            var duplicateTracker = new HashSet<string>();
            
            
            foreach (var ipl in dataLoader.iplLoader.ipls)
            {
                foreach (var inst in ipl.ipl_inst)
                {
                    
                    string instanceName = inst.name;
                    Item_OBJS objDef = null;
                    
                    if (string.IsNullOrEmpty(instanceName) && inst.hash != 0)
                    {
                        
                        if (objsByHash.TryGetValue((uint)inst.hash, out objDef))
                        {
                            instanceName = objDef.modelName;
                        }
                        else if (hashToName.TryGetValue((uint)inst.hash, out string resolvedName))
                        {
                            instanceName = resolvedName;
                        }
                        else
                        {
                            
                            instanceName = $"0x{inst.hash:x8}";
                        }
                    }
                    
                    
                    if (string.IsNullOrEmpty(instanceName)) continue;
                    
                    
                    if (objDef == null && !objsDict.TryGetValue(instanceName, out objDef))
                    {
                        
                        if (!objsDict.TryGetValue(instanceName.ToLower(), out objDef))
                        {
                            
                            if (instanceName.StartsWith("0x"))
                            {
                                
                                string[] modelExtensions = { ".wdr", ".wdd", ".wft", ".wbn", ".wbd" };
                                bool foundModel = false;
                                
                                foreach (var ext in modelExtensions)
                                {
                                    if (dataLoader.gameFiles.ContainsKey(instanceName.ToLower() + ext))
                                    {
                                        
                                        objDef = new Item_OBJS
                                        {
                                            modelName = instanceName,
                                            textureName = instanceName, 
                                            drawDistance = new float[] { 300f }
                                        };
                                        foundModel = true;
                                        Debug.Log($"Found model file for hash: {instanceName}{ext}");
                                        break;
                                    }
                                }
                                
                                if (!foundModel)
                                {
                                    
                                    if (!missingAssetsLog.Contains(instanceName))
                                    {
                                        missingAssetsLog.Add(instanceName);
                                    }
                                    continue;
                                }
                            }
                            else
                            {
                                
                                
                                objDef = new Item_OBJS
                                {
                                    modelName = instanceName,
                                    textureName = instanceName, 
                                    drawDistance = new float[] { 300f },
                                    flag1 = 0,
                                    flag2 = 0
                                };
                                
                                
                                if (!missingAssetsLog.Contains(instanceName))
                                {
                                    Debug.Log($"No IDE definition for {instanceName}, will try to load model directly");
                                }
                            }
                        }
                    }
                    
                    
                    if ((objDef.flag2 & 0x80000000) != 0)
                    {
                        continue; 
                    }
                    
                    
                    string modelFileName = objDef.modelName;
                    if (!modelFileName.Contains("."))
                    {
                        
                        
                        string[] modelExtensions = { ".wdr", ".wdd", ".wft", ".wbn", ".wbd" };
                        bool found = false;
                        foreach (var ext in modelExtensions)
                        {
                            if (dataLoader.gameFiles.ContainsKey(modelFileName.ToLower() + ext))
                            {
                                modelFileName = modelFileName + ext;
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            
                            if (modelFileName.StartsWith("0x"))
                            {
                                Debug.LogWarning($"Could not find model file for hash {modelFileName}. This hash likely represents a model name we couldn't resolve from IDE files.");
                                continue; 
                            }
                            modelFileName = modelFileName + ".wdr"; 
                        }
                    }
                    
                    
                    string positionKey = $"{Mathf.Round(inst.position.x * 10f) / 10f}," +
                                       $"{Mathf.Round(inst.position.y * 10f) / 10f}," +
                                       $"{Mathf.Round(inst.position.z * 10f) / 10f}:" +
                                       $"{objDef.modelName}";
                    
                    
                    if (duplicateTracker.Contains(positionKey))
                    {
                        continue;
                    }
                    duplicateTracker.Add(positionKey);
                    
                    
                    uint objId = (uint)(inst.GetHashCode() + objDef.GetHashCode());
                    var objInstance = new ObjectInstance
                    {
                        Id = objId,
                        ModelName = modelFileName,
                        TextureName = objDef.textureName + (objDef.textureName.Contains(".") ? "" : ".wtd"),
                        Position = inst.unityPosition,
                        UnityPosition = inst.unityPosition,
                        Rotation = inst.unityRotation,
                        Instance = inst,
                        Definition = objDef,
                        IsLoaded = false
                    };
                    
                    
                    allGameObjects[objId] = objInstance;
                    objectIndex.Add(objId, inst.unityPosition);
                }
            }
            
            Debug.Log($"Indexed {allGameObjects.Count} unique game objects");
            
            if (missingAssetsLog.Count > 0)
            {
                Debug.LogWarning($"Total missing IDE definitions: {missingAssetsLog.Count}");
            }
        }
        
        private void QueueObjectsAroundPosition(Vector3 position, float radius)
        {
            Debug.Log($"Queueing objects within {radius}m of position {position}");
            
            
            highPriorityQueue = new PriorityQueue<LoadTask>();
            normalPriorityQueue = new PriorityQueue<LoadTask>();
            lowPriorityQueue = new PriorityQueue<LoadTask>();
            
            var loadTasks = new List<LoadTask>();
            
            
            var nearbyCells = objectIndex.GetCellsInRadius(position, radius);
            var nearbyIds = new HashSet<uint>();
            foreach (int cellIndex in nearbyCells)
            {
                var objectsInCell = objectIndex.GetObjectsInCell(cellIndex);
                if (objectsInCell != null)
                {
                    nearbyIds.UnionWith(objectsInCell);
                }
            }
            
            foreach (uint id in nearbyIds)
            {
                if (!allGameObjects.TryGetValue(id, out ObjectInstance obj))
                    continue;
                    
                
                if (obj.IsLoaded || activeObjects.ContainsKey(id))
                    continue;
                    
                float distance = Vector3.Distance(obj.UnityPosition, position);
                if (distance > radius) continue;
                
                
                float clampedDistance = Mathf.Max(distance, 0.1f);
                
                var task = new LoadTask
                {
                    Id = obj.Id,
                    ModelName = obj.ModelName,
                    TextureName = obj.TextureName,
                    Position = obj.Position,
                    Rotation = obj.Rotation,
                    Scale = obj.Instance != null ? obj.Instance.unityScale : Vector3.one,
                    Priority = 1f / clampedDistance,
                    LODLevel = GetLODLevel(distance),
                    Instance = obj.Instance,
                    Definition = obj.Definition
                };
                
                loadTasks.Add(task);
            }
            
            
            loadTasks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            
            
            if (loadTasks.Count > 0)
            {
                var closest = loadTasks.Take(5);
                foreach (var task in closest)
                {
                    float dist = Vector3.Distance(task.Position, position);
                    Debug.Log($"Queuing nearby object: {task.ModelName} at distance {dist:F1}m, priority {task.Priority:F2}");
                }
            }
            
            foreach (var task in loadTasks)
            {
                if (task.Priority > 0.01f) 
                    highPriorityQueue.Enqueue(task, task.Priority);
                else if (task.Priority > 0.005f) 
                    normalPriorityQueue.Enqueue(task, task.Priority);
                else 
                    lowPriorityQueue.Enqueue(task, task.Priority);
            }
            
            totalObjectsToLoad = loadTasks.Count;
            Debug.Log($"Queued {totalObjectsToLoad} objects for loading");
            
            
            if (totalObjectsToLoad == 0 && isInitializing)
            {
                Debug.LogWarning("No objects found at initial position!");
                isInitializing = false;
                initialLoadComplete = true;
            }
        }
        
        private int GetLODLevel(float distance)
        {
            if (distance < 50f) return 0;
            if (distance < 150f) return 1;
            if (distance < 300f) return 2;
            return 3;
        }
        
        private void Update()
        {
            frameStartTime = Time.realtimeSinceStartup * 1000f;
            objectsLoadedThisFrame = 0;
            
            
            if (isInitializing && !initialLoadComplete && Time.time - initStartTime > 30f) 
            {
                Debug.LogWarning($"Force completing initialization after 30s timeout (processed {objectsProcessed}/{totalObjectsToLoad})");
                initialLoadComplete = true;
                isInitializing = false;
                LoadingScreen.Finish();
            }
            
            
            ProcessBatchedSpawns();
            
            
            if (FocusPoint.Instance != null)
            {
                UpdateStreaming();
            }
            
            CompleteJobs();
        }
        
        private void ProcessBatchedSpawns()
        {
            
            if (Time.time - lastBatchSpawnTime < batchSpawnInterval)
                return;
                
            lastBatchSpawnTime = Time.time;
            
            int spawned = 0;
            while (spawnQueue.Count > 0 && spawned < maxSpawnsPerBatch)
            {
                SpawnData spawn;
                lock (spawnQueue)
                {
                    if (spawnQueue.Count == 0) break;
                    spawn = spawnQueue.Dequeue();
                }
                
                CreateObjectFromDataPooled(spawn.Task, spawn.Model, spawn.Texture);
                spawned++;
            }
        }
        
        
        
        
        public void SetFocusPosition(Vector3 position)
        {
            FocusPoint.SetPosition(position);
            
            
            UpdateStreaming();
        }
        
        
        
        
        public void LoadAreaAroundPosition(Vector3 position, float radius)
        {
            SetFocusPosition(position);
            
            
            float originalStreamDistance = streamDistance;
            streamDistance = radius;
            
            
            PopulateQueueForArea(position, radius);
            
            
            streamDistance = originalStreamDistance;
        }
        
        private void PopulateQueueForArea(Vector3 position, float radius)
        {
            if (dataLoader?.iplLoader?.ipls == null) return;
            
            var loadTasks = new List<LoadTask>();
            
            
            var objsDict = new Dictionary<string, Item_OBJS>();
            foreach (var ide in dataLoader.ideLoader.ides)
            {
                foreach (var obj in ide.items_objs)
                {
                    if (!string.IsNullOrEmpty(obj.modelName) && !objsDict.ContainsKey(obj.modelName))
                    {
                        objsDict[obj.modelName] = obj;
                    }
                }
            }
            
            
            var loadedInstances = new HashSet<string>();
            
            
            foreach (var ipl in dataLoader.iplLoader.ipls)
            {
                foreach (var inst in ipl.ipl_inst)
                {
                    if (string.IsNullOrEmpty(inst.name)) continue;
                    
                    if (!objsDict.TryGetValue(inst.name, out Item_OBJS objDef))
                        continue;
                    
                    float distance = Vector3.Distance(inst.unityPosition, position);
                    if (distance > radius) continue;
                    
                    
                    uint id = (uint)(inst.GetHashCode() + objDef.GetHashCode());
                    if (activeObjects.ContainsKey(id)) continue;
                    
                    
                    string positionKey = $"{Mathf.Round(inst.position.x * 10f) / 10f}," +
                                       $"{Mathf.Round(inst.position.y * 10f) / 10f}," +
                                       $"{Mathf.Round(inst.position.z * 10f) / 10f}:" +
                                       $"{objDef.modelName}";
                    
                    if (loadedInstances.Contains(positionKey))
                    {
                        Debug.Log($"Skipping duplicate instance of {objDef.modelName} at position {inst.position} in LoadArea");
                        continue;
                    }
                    loadedInstances.Add(positionKey);
                    
                    
                    float clampedDistance = Mathf.Max(distance, 0.1f);
                    
                    var task = new LoadTask
                    {
                        Id = id,
                        ModelName = objDef.modelName + ".wdr",
                        TextureName = objDef.textureName + ".wtd",
                        Position = inst.unityPosition,
                        Rotation = inst.unityRotation,
                        Scale = inst.unityScale,
                        Priority = 1f / clampedDistance,
                        LODLevel = GetLODLevel(distance),
                        Instance = inst,
                        Definition = objDef
                    };
                    
                    loadTasks.Add(task);
                }
            }
            
            
            foreach (var task in loadTasks)
            {
                highPriorityQueue.Enqueue(task, task.Priority);
            }
            
            Debug.Log($"Queued {loadTasks.Count} objects for area load at {position} with radius {radius}");
        }
        
        private void CreateWater()
        {
            if (dataLoader?.waterPlanes == null || dataLoader.waterPlanes.Count == 0)
            {
                Debug.Log("No water planes to create");
                return;
            }
            
            Debug.Log($"Creating water with {dataLoader.waterPlanes.Count} water definitions");
            
            GameObject waterModel = new GameObject("Water");
            waterModel.transform.parent = transform;
            waterModel.transform.localScale = new Vector3(-0.1f, 0.1f, 0.1f);
            
            var renderer = waterModel.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var filter = waterModel.AddComponent<MeshFilter>();
            
            UnityEngine.Mesh waterMesh = new UnityEngine.Mesh();
            waterMesh.name = "WaterMesh";
            
            List<Vector3> vertexPoints = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            
            int vertexIndex = 0;
            
            foreach (Water water in dataLoader.waterPlanes)
            {
                foreach (var plane in water.planes)
                {
                    
                    Vector3 p0 = plane.points[0].coord;
                    Vector3 p1 = plane.points[1].coord;
                    Vector3 p2 = plane.points[2].coord;
                    Vector3 p3 = plane.points[3].coord;
                    
                    
                    p0 = Quaternion.Euler(-90, 0, 0) * p0;
                    p1 = Quaternion.Euler(-90, 0, 0) * p1;
                    p2 = Quaternion.Euler(-90, 0, 0) * p2;
                    p3 = Quaternion.Euler(-90, 0, 0) * p3;
                    
                    
                    vertexPoints.Add(p0);
                    vertexPoints.Add(p1);
                    vertexPoints.Add(p2);
                    vertexPoints.Add(p3);
                    
                    
                    // Calculate UVs based on world position for proper tiling
                    // Use a tiling factor to control texture scale
                    float uvScale = 0.05f; // Adjust this value to control texture tiling
                    
                    uvs.Add(new Vector2(p0.x * uvScale, p0.z * uvScale));
                    uvs.Add(new Vector2(p1.x * uvScale, p1.z * uvScale));
                    uvs.Add(new Vector2(p2.x * uvScale, p2.z * uvScale));
                    uvs.Add(new Vector2(p3.x * uvScale, p3.z * uvScale));
                    
                    
                    triangles.Add(vertexIndex + 0);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);

                    
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 3);
                    
                    vertexIndex += 4;
                }
            }
            
            
            waterMesh.vertices = vertexPoints.ToArray();
            waterMesh.triangles = triangles.ToArray();
            waterMesh.uv = uvs.ToArray();
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();
            
            filter.sharedMesh = waterMesh;
            
            
            Debug.Log("Loading Water Textures...");
            UnityEngine.Material waterMaterial = null;
            
            try
            {
                
                if (dataLoader?.root != null)
                {
                    var pcDirectory = dataLoader.root.RootDirectory.FindByName("pc") as RageLib.FileSystem.Common.Directory;
                    if (pcDirectory != null)
                    {
                        var texturesDirectory = pcDirectory.FindByName("textures") as RageLib.FileSystem.Common.Directory;
                        if (texturesDirectory != null)
                        {
                            var waterTextureIndex = texturesDirectory.FindByName("water.wtd") as RageLib.FileSystem.Common.File;
                            if (waterTextureIndex != null)
                            {
                                byte[] waterTextureData = waterTextureIndex.GetData();
                                using (MemoryStream waterTextureStream = new MemoryStream(waterTextureData))
                                {
                                    TextureFile waterTexture = new TextureFile();
                                    waterTexture.Open(waterTextureStream);
                                    if (waterTexture.Textures.Count > 0)
                                    {
                                        var waterImage = waterTexture.Textures[0].Decode();
                                        
                                        
                                        var waterShader = Shader.Find("water");
                                        if (waterShader != null)
                                        {
                                            waterMaterial = new UnityEngine.Material(waterShader);
                                            waterMaterial.SetTexture("_MainTex", waterImage.GetUnityTexture());
                                            Debug.Log("Successfully loaded water texture and created material with water shader");
                                        }
                                        else
                                        {
                                            Debug.LogError("Could not find 'water' shader");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load water texture: {e.Message}");
            }
            
            
            if (waterMaterial == null)
            {
                var waterShader = Shader.Find("water");
                if (waterShader == null)
                {
                    Debug.LogError("Could not find 'water' shader for water material");
                    return;
                }
                
                waterMaterial = new UnityEngine.Material(waterShader);
                waterMaterial.name = "WaterMaterial";
                Debug.Log("Created water material with water shader (no texture)");
            }
            
            renderer.material = waterMaterial;
            
            Debug.Log($"Created water mesh with {vertexPoints.Count} vertices and {triangles.Count / 3} triangles");
        }
        
        private void BuildAssetLookupTables()
        {
            Debug.Log("Building comprehensive asset lookup tables from actual GTA files...");
            
            
            IVUnity.ComprehensiveHashResolver.Initialize(dataLoader);
            
            
            foreach (var kvp in dataLoader.gameFiles)
            {
                string fileName = kvp.Key;
                File file = kvp.Value;
                
                
                string baseName = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                
                
                AddAssetVariations(baseName, file);
                
                
                AddAssetVariations(fileName, file);
                
                
                if (extension.Equals(".wtd", StringComparison.OrdinalIgnoreCase))
                {
                    string[] suffixes = { "_diff", "_spec", "_norm", "_detail" };
                    foreach (var suffix in suffixes)
                    {
                        if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            string nameWithoutSuffix = baseName.Substring(0, baseName.Length - suffix.Length);
                            AddAssetVariations(nameWithoutSuffix, file);
                        }
                    }
                }
                
                
                if (fileName.StartsWith("0x"))
                {
                    string resolved = IVUnity.ComprehensiveHashResolver.ResolveHashString(baseName);
                    if (!resolved.StartsWith("0x"))
                    {
                        AddAssetVariations(resolved, file);
                    }
                }
            }
            
            
            foreach (var ide in dataLoader.ideLoader.ides)
            {
                foreach (var obj in ide.items_objs)
                {
                    if (!string.IsNullOrEmpty(obj.modelName))
                    {
                        
                        uint hash = RageLib.Common.Hasher.Hash(obj.modelName);
                        hashToName[hash] = obj.modelName;
                        
                        
                        uint dotNetHash = (uint)obj.modelName.GetHashCode();
                        hashToName[dotNetHash] = obj.modelName;
                        
                        
                        if (!string.IsNullOrEmpty(obj.textureName))
                        {
                            uint texHash = RageLib.Common.Hasher.Hash(obj.textureName);
                            hashToName[texHash] = obj.textureName;
                        }
                    }
                }
            }
            
            
            foreach (var ipl in dataLoader.iplLoader.ipls)
            {
                foreach (var inst in ipl.ipl_inst)
                {
                    if (!string.IsNullOrEmpty(inst.name))
                    {
                        
                        if (inst.hash != 0)
                        {
                            hashToName[(uint)inst.hash] = inst.name;
                        }
                        
                        
                        uint gtaHash = RageLib.Common.Hasher.Hash(inst.name);
                        hashToName[gtaHash] = inst.name;
                    }
                }
            }
            
            Debug.Log($"Built comprehensive lookup tables: {assetsByName.Count} asset variations, {hashToName.Count} hash mappings");
        }
        
        private void AddAssetVariations(string name, File file)
        {
            
            if (!assetsByName.ContainsKey(name))
                assetsByName[name] = new List<File>();
            if (!assetsByName[name].Contains(file))
                assetsByName[name].Add(file);
            
            
            string lowerName = name.ToLower();
            if (!assetsByName.ContainsKey(lowerName))
                assetsByName[lowerName] = new List<File>();
            if (!assetsByName[lowerName].Contains(file))
                assetsByName[lowerName].Add(file);
            
            
            string upperName = name.ToUpper();
            if (!assetsByName.ContainsKey(upperName))
                assetsByName[upperName] = new List<File>();
            if (!assetsByName[upperName].Contains(file))
                assetsByName[upperName].Add(file);
        }
        
        private File FindAssetFile(string name, string extension)
        {
            
            if (string.IsNullOrEmpty(name) || name == "null")
                return null;
            
            
            string fullName = name + extension;
            if (dataLoader.gameFiles.TryGetValue(fullName.ToLower(), out File file))
            {
                return file;
            }
            
            
            if (extension.Equals(".wtd", StringComparison.OrdinalIgnoreCase))
            {
                File wtdFile = IVUnity.ComprehensiveHashResolver.GetWTDContainingTexture(name);
                if (wtdFile != null)
                {
                    Debug.Log($"Found texture '{name}' inside WTD file: {wtdFile.Name}");
                    return wtdFile;
                }
            }
            
            
            if (assetsByName.TryGetValue(name, out List<File> files))
            {
                
                foreach (var f in files)
                {
                    if (f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                }
            }
            
            
            string nameWithoutExt = Path.GetFileNameWithoutExtension(name);
            if (nameWithoutExt != name && assetsByName.TryGetValue(nameWithoutExt, out files))
            {
                foreach (var f in files)
                {
                    if (f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                }
            }
            
            
            if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string resolved = IVUnity.ComprehensiveHashResolver.ResolveHashString(name);
                if (resolved != name && !resolved.StartsWith("0x"))
                {
                    Debug.Log($"Resolved hash {name} to {resolved}");
                    return FindAssetFile(resolved, extension);
                }
            }
            
            
            if (uint.TryParse(name, out uint plainHash))
            {
                string resolved = IVUnity.ComprehensiveHashResolver.ResolveHash(plainHash);
                if (!resolved.StartsWith("0x"))
                {
                    return FindAssetFile(resolved, extension);
                }
            }
            
            
            uint nameHash = RageLib.Common.Hasher.Hash(name);
            if (hashToName.TryGetValue(nameHash, out string hashResolved))
            {
                if (hashResolved != name) 
                {
                    return FindAssetFile(hashResolved, extension);
                }
            }
            
            
            if (extension.Equals(".wtd", StringComparison.OrdinalIgnoreCase))
            {
                
                string[] variants = { name + "_diff", name + "_spec", name + "_norm", name + "_detail", name + "_vehshare" };
                foreach (var variant in variants)
                {
                    if (assetsByName.TryGetValue(variant, out files))
                    {
                        foreach (var f in files)
                        {
                            if (f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                            {
                                return f;
                            }
                        }
                    }
                }
                
                
                string nameWithoutNumbers = System.Text.RegularExpressions.Regex.Replace(name, @"\d+$", "");
                if (nameWithoutNumbers != name && assetsByName.TryGetValue(nameWithoutNumbers, out files))
                {
                    foreach (var f in files)
                    {
                        if (f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        {
                            return f;
                        }
                    }
                }
                
                
                if (name.Contains("building") || name.Contains("street") || name.Contains("wall"))
                {
                    string[] commonTextures = { "generic", "vehshare", "shared", "common" };
                    foreach (var common in commonTextures)
                    {
                        if (assetsByName.TryGetValue(common, out files))
                        {
                            foreach (var f in files)
                            {
                                if (f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.Log($"Using fallback texture {f.Name} for {name}");
                                    return f;
                                }
                            }
                        }
                    }
                }
            }
            
            
            if (!name.StartsWith("0x") && !string.IsNullOrEmpty(name))
            {
                missingAssetsLog.Add($"{name}{extension}");
                if (missingAssetsLog.Count <= 20) 
                {
                    Debug.LogWarning($"Could not find asset: {name}{extension}. Total game files: {dataLoader.gameFiles.Count}, AssetsByName entries: {assetsByName.Count}");
                }
            }
            
            return null;
        }
        
        private void UpdateStreaming()
        {
            
            if (isInitializing) 
            {
                if (Time.frameCount % 300 == 0)
                    Debug.LogWarning($"UpdateStreaming skipped - still initializing (processed {objectsProcessed}/{totalObjectsToLoad})");
                return;
            }
            
            
            var focusPos = FocusPoint.GetPosition();
            
            
            var toUnload = new List<uint>();
            int checkedCount = 0;
            int activeCount = activeObjects.Count;
            
            foreach (var kvp in activeObjects)
            {
                var obj = kvp.Value;
                if (obj.GameObject == null) 
                {
                    
                    toUnload.Add(kvp.Key);
                    continue;
                }
                
                float distance = Vector3.Distance(obj.GameObject.transform.position, focusPos);
                checkedCount++;
                
                
                int newLOD = GetLODLevel(distance);
                if (newLOD != obj.LODLevel)
                {
                    UpdateObjectLOD(kvp.Key, obj, newLOD);
                }
                
                
                
                if ((distance > unloadDistance && !pendingLoads.ContainsKey(kvp.Key)) || 
                    distance > unloadDistance * 2f)
                {
                    toUnload.Add(kvp.Key);
                }
            }
            
            
            if (Time.frameCount % 300 == 0) 
            {
                Debug.Log($"Streaming: Checked {checkedCount} active objects, found {toUnload.Count} to unload (Focus: {focusPos}, UnloadDist: {unloadDistance}m)");
                if (checkedCount > 0 && toUnload.Count == 0)
                {
                    
                    var samples = activeObjects.Take(3);
                    foreach (var sample in samples)
                    {
                        if (sample.Value.GameObject != null)
                        {
                            float dist = Vector3.Distance(sample.Value.GameObject.transform.position, focusPos);
                            Debug.Log($"  Sample object {sample.Key}: distance {dist:F1}m (unload at {unloadDistance}m)");
                        }
                    }
                }
            }
            
            
            foreach (var id in toUnload)
            {
                if (!pendingUnloads.ContainsKey(id))
                {
                    pendingUnloads.TryAdd(id, true);
                    UnloadObjectPooled(id);
                }
            }
            
            
            CheckForNewLoadsWithHysteresis(focusPos);
        }
        
        private void UnloadObjectPooled(uint id)
        {
            if (activeObjects.TryRemove(id, out LoadedObject obj))
            {
                if (obj.GameObject != null)
                {
                    
                    if (objectPool.Count < poolSize)
                    {
                        
                        obj.GameObject.SetActive(false);
                        
                        
                        foreach (var component in obj.GameObject.GetComponents<Component>())
                        {
                            if (!(component is Transform))
                            {
                                Destroy(component);
                            }
                        }
                        
                        
                        foreach (Transform child in obj.GameObject.transform)
                        {
                            Destroy(child.gameObject);
                        }
                        
                        objectPool.Enqueue(obj.GameObject);
                    }
                    else
                    {
                        Destroy(obj.GameObject);
                    }
                }
                
                spatialGrid.Remove(id);
                
                
                if (allGameObjects.TryGetValue(id, out ObjectInstance objInst))
                {
                    objInst.IsLoaded = false;
                }
                
                stats.TotalUnloaded++;
                pendingUnloads.TryRemove(id, out _);
            }
        }
        
        private void UpdateObjectLOD(uint id, LoadedObject obj, int newLOD)
        {
            obj.LODLevel = newLOD;
            
        }
        
        private void UnloadObject(uint id)
        {
            if (activeObjects.TryRemove(id, out LoadedObject obj))
            {
                if (obj.GameObject != null)
                {
                    Destroy(obj.GameObject);
                }
                
                spatialGrid.Remove(id);
                stats.TotalUnloaded++;
            }
        }
        
        private void CheckForNewLoadsWithHysteresis(Vector3 focusPos)
        {
            
            var nearbyCells = objectIndex.GetCellsInRadius(focusPos, loadDistance);
            var nearbyIds = new HashSet<uint>();
            foreach (int cellIndex in nearbyCells)
            {
                var objectsInCell = objectIndex.GetObjectsInCell(cellIndex);
                if (objectsInCell != null)
                {
                    nearbyIds.UnionWith(objectsInCell);
                }
            }
            
            int queued = 0;
            foreach (uint id in nearbyIds)
            {
                
                if (activeObjects.ContainsKey(id) || pendingLoads.ContainsKey(id))
                    continue;
                    
                if (!allGameObjects.TryGetValue(id, out ObjectInstance obj))
                    continue;
                    
                
                if (obj.IsLoaded)
                    continue;
                    
                float distance = Vector3.Distance(obj.UnityPosition, focusPos);
                if (distance > loadDistance)
                    continue;
                
                
                float clampedDistance = Mathf.Max(distance, 0.1f);
                
                
                var task = new LoadTask
                {
                    Id = obj.Id,
                    ModelName = obj.ModelName,
                    TextureName = obj.TextureName,
                    Position = obj.Position,
                    Rotation = obj.Rotation,
                    Scale = obj.Instance != null ? obj.Instance.unityScale : Vector3.one,
                    Priority = 1f / clampedDistance,
                    LODLevel = GetLODLevel(distance),
                    Instance = obj.Instance,
                    Definition = obj.Definition
                };
                
                
                if (task.Priority > 0.01f) 
                    highPriorityQueue.Enqueue(task, task.Priority);
                else if (task.Priority > 0.005f) 
                    normalPriorityQueue.Enqueue(task, task.Priority);
                else 
                    lowPriorityQueue.Enqueue(task, task.Priority);
                    
                pendingLoads.TryAdd(id, true);
                queued++;
            }
            
            if (queued > 0)
            {
                Debug.Log($"Queued {queued} new objects for loading as player moved");
            }
        }
        
        private void CheckForNewLoads(Vector3 focusPos)
        {
            
            CheckForNewLoadsWithHysteresis(focusPos);
        }
        
        private void CompleteJobs()
        {
            
            for (int i = activeJobs.Count - 1; i >= 0; i--)
            {
                if (activeJobs[i].IsCompleted)
                {
                    activeJobs[i].Complete();
                    activeJobs.RemoveAt(i);
                }
            }
        }
        
        private void CompleteAllJobs()
        {
            foreach (var job in activeJobs)
            {
                job.Complete();
            }
            activeJobs.Clear();
        }
        
        private void CleanupResources()
        {
            foreach (var model in modelCache.Values)
            {
                model.Model?.Dispose();
            }
            modelCache.Clear();
            
            foreach (var texture in textureCache.Values)
            {
                texture.File?.Dispose();
            }
            textureCache.Clear();
            
            foreach (var obj in activeObjects.Values)
            {
                if (obj.GameObject != null)
                    Destroy(obj.GameObject);
            }
            activeObjects.Clear();
        }
    }
    
    
    
    
    public class PriorityQueue<T> where T : class
    {
        private readonly SortedDictionary<float, Queue<T>> dict = new();
        private readonly object lockObj = new object();
        private int totalCount = 0;
        
        public int Count => totalCount;
        
        public void Enqueue(T item, float priority = 0)
        {
            lock (lockObj)
            {
                if (!dict.ContainsKey(priority))
                    dict[priority] = new Queue<T>();
                dict[priority].Enqueue(item);
                totalCount++;
            }
        }
        
        public bool TryDequeue(out T item)
        {
            lock (lockObj)
            {
                foreach (var kvp in dict.Reverse())
                {
                    if (kvp.Value.Count > 0)
                    {
                        item = kvp.Value.Dequeue();
                        totalCount--;
                        if (kvp.Value.Count == 0)
                            dict.Remove(kvp.Key);
                        return true;
                    }
                }
                item = null;
                return false;
            }
        }
    }
    
    
    
    
    public class SpatialGrid
    {
        private readonly float worldSize;
        private readonly float cellSize;
        private readonly int gridSize;
        private readonly Dictionary<int, HashSet<uint>> grid = new();
        private readonly Dictionary<uint, int> objectCells = new();
        
        public SpatialGrid(float worldSize, float cellSize)
        {
            this.worldSize = worldSize;
            this.cellSize = cellSize;
            this.gridSize = Mathf.CeilToInt(worldSize / cellSize);
        }
        
        public void Add(uint id, Vector3 position)
        {
            int cellIndex = GetCellIndex(position);
            
            if (!grid.ContainsKey(cellIndex))
                grid[cellIndex] = new HashSet<uint>();
            
            grid[cellIndex].Add(id);
            objectCells[id] = cellIndex;
        }
        
        public void Remove(uint id)
        {
            if (objectCells.TryGetValue(id, out int cellIndex))
            {
                if (grid.ContainsKey(cellIndex))
                {
                    grid[cellIndex].Remove(id);
                    if (grid[cellIndex].Count == 0)
                        grid.Remove(cellIndex);
                }
                objectCells.Remove(id);
            }
        }
        
        public List<int> GetCellsInRadius(Vector3 center, float radius)
        {
            var cells = new List<int>();
            int cellRadius = Mathf.CeilToInt(radius / cellSize);
            int centerCell = GetCellIndex(center);
            
            int centerX = centerCell % gridSize;
            int centerZ = centerCell / gridSize;
            
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    int cellX = centerX + x;
                    int cellZ = centerZ + z;
                    
                    if (cellX >= 0 && cellX < gridSize && 
                        cellZ >= 0 && cellZ < gridSize)
                    {
                        cells.Add(cellZ * gridSize + cellX);
                    }
                }
            }
            
            return cells;
        }
        
        public HashSet<uint> GetObjectsInCell(int cellIndex)
        {
            if (grid.TryGetValue(cellIndex, out HashSet<uint> objects))
            {
                return objects;
            }
            return null;
        }
        
        private int GetCellIndex(Vector3 position)
        {
            int x = Mathf.Clamp((int)((position.x + worldSize / 2) / cellSize), 0, gridSize - 1);
            int z = Mathf.Clamp((int)((position.z + worldSize / 2) / cellSize), 0, gridSize - 1);
            return z * gridSize + x;
        }
    }
}
