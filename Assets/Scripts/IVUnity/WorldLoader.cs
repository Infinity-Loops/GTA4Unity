using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using RageLib.FileSystem.Common;
using RageLib.Models;
using RageLib.Textures;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Jobs;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity
{
    /// <summary>
    /// Improved world loading system with efficient streaming and caching
    /// </summary>
    public class WorldLoader : MonoBehaviour
    {
        public static WorldLoader Instance;
        
        [Header("Loading Configuration")]
        public int maxConcurrentLoads = 4;
        public int maxModelsInMemory = 500;
        public float loadDistance = 500f;
        public float unloadDistance = 600f;
        public float updateInterval = 0.5f;
        
        [Header("Performance")]
        public bool useObjectPooling = true;
        public int initialPoolSize = 100;
        public bool enableLOD = true;
        public float[] lodDistances = { 50f, 100f, 200f, 400f };
        
        // Caching systems
        private readonly Dictionary<string, ModelFile> modelCache = new Dictionary<string, ModelFile>();
        private readonly Dictionary<string, TextureFile> textureCache = new Dictionary<string, TextureFile>();
        private readonly Dictionary<uint, GameObject> loadedObjects = new Dictionary<uint, GameObject>();
        private readonly ConcurrentQueue<LoadRequest> loadQueue = new ConcurrentQueue<LoadRequest>();
        private readonly ConcurrentQueue<uint> unloadQueue = new ConcurrentQueue<uint>();
        
        // Object pooling
        private Queue<GameObject> objectPool;
        private GameObject poolContainer;
        
        // Threading
        private CancellationTokenSource cancellationToken;
        private SemaphoreSlim loadSemaphore;
        private float lastUpdateTime;
        
        // Statistics
        public int ModelsLoaded => modelCache.Count;
        public int TexturesLoaded => textureCache.Count;
        public int ObjectsLoaded => loadedObjects.Count;
        public int QueuedLoads => loadQueue.Count;
        
        private GTADatLoader dataLoader;
        private Transform playerTransform;
        
        private struct LoadRequest
        {
            public uint InstanceId;
            public string ModelName;
            public string TextureName;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Priority;
            public Ipl_INST Definition;
        }
        
        private void Awake()
        {
            Instance = this;
            InitializeObjectPool();
            loadSemaphore = new SemaphoreSlim(maxConcurrentLoads);
            cancellationToken = new CancellationTokenSource();
        }
        
        private void OnDestroy()
        {
            cancellationToken?.Cancel();
            cancellationToken?.Dispose();
            loadSemaphore?.Dispose();
            CleanupCaches();
        }
        
        public void Initialize(GTADatLoader loader, Transform player)
        {
            dataLoader = loader;
            playerTransform = player;
            StartLoadingSystem();
        }
        
        private void InitializeObjectPool()
        {
            if (!useObjectPooling) return;
            
            objectPool = new Queue<GameObject>(initialPoolSize);
            poolContainer = new GameObject("ObjectPool");
            poolContainer.SetActive(false);
            
            for (int i = 0; i < initialPoolSize; i++)
            {
                var pooledObject = new GameObject($"PooledObject_{i}");
                pooledObject.transform.parent = poolContainer.transform;
                pooledObject.AddComponent<MeshFilter>();
                pooledObject.AddComponent<MeshRenderer>();
                pooledObject.AddComponent<StaticGeometry>();
                objectPool.Enqueue(pooledObject);
            }
        }
        
        private GameObject GetPooledObject()
        {
            if (!useObjectPooling || objectPool.Count == 0)
            {
                var newObject = new GameObject();
                newObject.AddComponent<MeshFilter>();
                newObject.AddComponent<MeshRenderer>();
                newObject.AddComponent<StaticGeometry>();
                return newObject;
            }
            
            return objectPool.Dequeue();
        }
        
        private void ReturnToPool(GameObject obj)
        {
            if (!useObjectPooling)
            {
                Destroy(obj);
                return;
            }
            
            obj.SetActive(false);
            obj.transform.parent = poolContainer.transform;
            obj.name = "PooledObject";
            
            // Clear components
            var meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter) meshFilter.sharedMesh = null;
            
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            if (meshRenderer) meshRenderer.sharedMaterials = new Material[0];
            
            objectPool.Enqueue(obj);
        }
        
        private void StartLoadingSystem()
        {
            Task.Run(async () => await ProcessLoadQueue(), cancellationToken.Token);
            Task.Run(async () => await ProcessUnloadQueue(), cancellationToken.Token);
        }
        
        private async Task ProcessLoadQueue()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                if (loadQueue.TryDequeue(out LoadRequest request))
                {
                    await loadSemaphore.WaitAsync();
                    try
                    {
                        await LoadObjectAsync(request);
                    }
                    finally
                    {
                        loadSemaphore.Release();
                    }
                }
                else
                {
                    await Task.Delay(10);
                }
            }
        }
        
        private async Task ProcessUnloadQueue()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                if (unloadQueue.TryDequeue(out uint instanceId))
                {
                    UnloadObject(instanceId);
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        
        private async Task LoadObjectAsync(LoadRequest request)
        {
            try
            {
                // Check if already loaded
                if (loadedObjects.ContainsKey(request.InstanceId))
                    return;
                
                // Load model
                ModelFile model = await GetOrLoadModel(request.ModelName);
                if (model == null) return;
                
                // Load texture
                TextureFile texture = null;
                if (!string.IsNullOrEmpty(request.TextureName))
                {
                    texture = await GetOrLoadTexture(request.TextureName);
                }
                
                // Create game object on main thread
                await MainThreadDispatcher.ExecuteOnMainThreadAsync(() =>
                {
                    CreateGameObject(request, model, texture);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load object {request.ModelName}: {e.Message}");
            }
        }
        
        private async Task<ModelFile> GetOrLoadModel(string modelName)
        {
            if (modelCache.TryGetValue(modelName, out ModelFile cachedModel))
                return cachedModel;
            
            File modelFile = null;
            dataLoader.gameFiles.TryGetValue(modelName.ToLower(), out modelFile);
            if (modelFile == null) return null;
            
            return await Task.Run(() =>
            {
                try
                {
                    var model = new ModelFile();
                    model.Open(modelFile.GetData());
                    model.Read();
                    
                    lock (modelCache)
                    {
                        if (!modelCache.ContainsKey(modelName))
                            modelCache[modelName] = model;
                    }
                    
                    return model;
                }
                catch
                {
                    return null;
                }
            });
        }
        
        private async Task<TextureFile> GetOrLoadTexture(string textureName)
        {
            if (textureCache.TryGetValue(textureName, out TextureFile cachedTexture))
                return cachedTexture;
            
            File textureFile = null;
            dataLoader.gameFiles.TryGetValue(textureName.ToLower(), out textureFile);
            if (textureFile == null) return null;
            
            return await Task.Run(() =>
            {
                try
                {
                    var texture = new TextureFile();
                    texture.Open(textureFile.GetData());
                    texture.Read();
                    
                    lock (textureCache)
                    {
                        if (!textureCache.ContainsKey(textureName))
                            textureCache[textureName] = texture;
                    }
                    
                    return texture;
                }
                catch
                {
                    return null;
                }
            });
        }
        
        private void CreateGameObject(LoadRequest request, ModelFile model, TextureFile texture)
        {
            var gameObject = GetPooledObject();
            gameObject.name = request.ModelName;
            gameObject.transform.position = request.Position;
            gameObject.transform.rotation = request.Rotation;
            gameObject.SetActive(true);
            
            var geometry = gameObject.GetComponent<StaticGeometry>();
            geometry.data.model = model;
            geometry.data.collection = texture != null ? new[] { texture } : null;
            geometry.data.modelFileName = request.ModelName;
            geometry.data.definitions = request.Definition;
            geometry.LoadModel();
            
            if (enableLOD)
            {
                SetupLOD(gameObject);
            }
            
            loadedObjects[request.InstanceId] = gameObject;
        }
        
        private void SetupLOD(GameObject obj)
        {
            var lodGroup = obj.AddComponent<LODGroup>();
            var renderers = obj.GetComponentsInChildren<Renderer>();
            
            LOD[] lods = new LOD[lodDistances.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                float screenRelativeHeight = 1.0f - (lodDistances[i] / loadDistance);
                lods[i] = new LOD(screenRelativeHeight, renderers);
            }
            
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }
        
        private void UnloadObject(uint instanceId)
        {
            if (!loadedObjects.TryGetValue(instanceId, out GameObject obj))
                return;
            
            MainThreadDispatcher.ExecuteOnMainThread(() =>
            {
                ReturnToPool(obj);
                loadedObjects.Remove(instanceId);
            });
        }
        
        public void QueueLoad(Ipl_INST instance, Item_OBJS definition)
        {
            float distance = Vector3.Distance(instance.position, playerTransform.position);
            if (distance > loadDistance) return;
            
            var request = new LoadRequest
            {
                InstanceId = (uint)(instance.GetHashCode() + definition.GetHashCode()),
                ModelName = definition.modelName + ".wdr",
                TextureName = definition.textureName + ".wtd",
                Position = instance.position,
                Rotation = instance.unityRotation,
                Priority = 1f / distance, // Closer objects have higher priority
                Definition = instance
            };
            
            loadQueue.Enqueue(request);
        }
        
        public void UpdateStreaming()
        {
            if (Time.time - lastUpdateTime < updateInterval)
                return;
            
            lastUpdateTime = Time.time;
            
            // Check for objects to unload
            var toUnload = new List<uint>();
            foreach (var kvp in loadedObjects)
            {
                float distance = Vector3.Distance(kvp.Value.transform.position, playerTransform.position);
                if (distance > unloadDistance)
                {
                    toUnload.Add(kvp.Key);
                }
            }
            
            foreach (var id in toUnload)
            {
                unloadQueue.Enqueue(id);
            }
            
            // Manage cache size
            if (modelCache.Count > maxModelsInMemory)
            {
                TrimCaches();
            }
        }
        
        private void TrimCaches()
        {
            // Remove least recently used models
            var modelsToRemove = new List<string>();
            int removeCount = modelCache.Count - (maxModelsInMemory * 3 / 4); // Keep 75% when trimming
            
            foreach (var kvp in modelCache)
            {
                bool inUse = false;
                foreach (var obj in loadedObjects.Values)
                {
                    var geometry = obj.GetComponent<StaticGeometry>();
                    if (geometry && geometry.data.modelFileName == kvp.Key)
                    {
                        inUse = true;
                        break;
                    }
                }
                
                if (!inUse)
                {
                    modelsToRemove.Add(kvp.Key);
                    if (modelsToRemove.Count >= removeCount)
                        break;
                }
            }
            
            foreach (var key in modelsToRemove)
            {
                modelCache[key]?.Dispose();
                modelCache.Remove(key);
            }
        }
        
        private void CleanupCaches()
        {
            foreach (var model in modelCache.Values)
                model?.Dispose();
            modelCache.Clear();
            
            foreach (var texture in textureCache.Values)
                texture?.Dispose();
            textureCache.Clear();
            
            foreach (var obj in loadedObjects.Values)
                Destroy(obj);
            loadedObjects.Clear();
        }
        
        private void Update()
        {
            UpdateStreaming();
        }
    }
}