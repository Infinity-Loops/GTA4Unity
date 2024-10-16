using RageLib.FileSystem;
using RageLib.FileSystem.Common;
using RageLib.Models;
using RageLib.Textures;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Directory = RageLib.FileSystem.Common.Directory;
using File = RageLib.FileSystem.Common.File;

public class WorldComposerMachine : MonoBehaviour
{
    [Header("Settings")]
    //Reimplementation
    private GameObject worldContainer;
    private int loadedObjects;
    //Rage
    private List<Ipl_INST> sceneInstances = new List<Ipl_INST>();
    private List<Item_OBJS> gameObjects = new List<Item_OBJS>();
    private Dictionary<string, Item_OBJS> gameObjectDict = new Dictionary<string, Item_OBJS>();
    private Dictionary<string, ModelNode> modelCache = new Dictionary<string, ModelNode>();
    //Unity
    private Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    public async void ComposeWorld(GTADatLoader loader)
    {
        GenerateContainer();

        ComposeWater(loader.waterPlanes, loader.root);
        //ComposeMap(loader);
        await ComposeMapAccurate(loader);
    }

    void GenerateContainer()
    {
        worldContainer = new GameObject("Container");
        worldContainer.transform.parent = transform;
        worldContainer.transform.localPosition = Vector3.zero;
        worldContainer.transform.localRotation = Quaternion.Euler(0, 0, 0);
    }

    async Task ComposeMapAccurate(GTADatLoader loader)
    {
        Debug.Log("Composing map...");

        Debug.Log("Loading IPL Batch...");
        await Task.Run(() =>
        {
            foreach (var ipl in loader.iplLoader.ipls)
            {
                lock (sceneInstances)
                {
                    sceneInstances.AddRange(ipl.ipl_inst);
                }
            }
        });

        Debug.Log("Loading IDE Batch...");
        await Task.Run(() =>
        {
            foreach (var ide in loader.ideLoader.ides)
            {
                foreach (var obj in ide.items_objs)
                {
                    lock (gameObjectDict)
                    {
                        if (!gameObjectDict.ContainsKey(obj.modelName))
                        {
                            gameObjectDict.Add(obj.modelName, obj);
                        }
                    }
                }
            }
        });

        Debug.Log("Loading Item Definitions");

        await ProcessItemsInBatches(sceneInstances, 128, loader);

        Debug.Log("Map composition finished.");
    }

    async Task ProcessItemsInBatches(List<Ipl_INST> sceneInstances, int batchSize, GTADatLoader loader)
    {
        for (int i = 0; i < sceneInstances.Count; i += batchSize)
        {
            int end = Math.Min(i + batchSize, sceneInstances.Count);
            var batch = sceneInstances.GetRange(i, end - i);

            await Task.Run(() =>
            {
                for (int j = 0; j < batch.Count; j++)
                {
                    var itemInstance = batch[j];

                    if (gameObjectDict.TryGetValue(itemInstance.name, out var equivalentItemGameObject))
                    {
                        File gameItem = null;
                        string modelName = "";

                        if (!string.IsNullOrEmpty(equivalentItemGameObject.wdd) && !equivalentItemGameObject.wdd.Equals("null", StringComparison.OrdinalIgnoreCase))
                        {
                            modelName = equivalentItemGameObject.wdd + ".wdd";
                            gameItem = FindGameFile(loader, modelName);
                        }
                        else
                        {
                            modelName = equivalentItemGameObject.modelName + ".wdr";
                            gameItem = FindGameFile(loader, modelName) ?? FindGameFile(loader, equivalentItemGameObject.modelName + ".wft");
                        }

                        if (gameItem != null)
                        {
                            ProcessGameItem(loader, gameItem, equivalentItemGameObject, itemInstance);
                        }
                    }
                }
            });

            await Task.Yield();
        }
    }


    private void ProcessGameItem(GTADatLoader loader, File gameItem, Item_OBJS equivalentItemGameObject, Ipl_INST itemInstance)
    {
        if (gameItem.Name.EndsWith(".wdr"))
        {
            ModelFile model = new ModelFile();
            TextureFile textureFile = null;
            TextureFile[] textureCollection = new TextureFile[1];

            Debug.Log($"Loading WDR: {gameItem.Name}");

            using (MemoryStream modelStream = new MemoryStream(gameItem.GetData()))
            {
                try
                {
                    model.Open(modelStream);
                }
                catch
                {
                    model = null;
                }
            }

            if (model == null)
            {
                return;
            }

            string texName = equivalentItemGameObject.textureName + ".wtd";
            var textureData = FindGameFile(loader, texName);

            if (textureData != null)
            {
                using (MemoryStream textureStream = new MemoryStream(textureData.GetData()))
                {
                    try
                    {
                        textureFile = new TextureFile();
                        Debug.Log($"Opening Texture: {texName}");
                        textureFile.Open(textureStream);
                        textureCollection[0] = textureFile;
                    }
                    catch
                    {
                        textureCollection = null;
                    }
                }
                MainThreadDispatcher.ExecuteOnMainThread(() =>
                {
                    LoadModel(model, textureCollection, itemInstance, gameItem);
                });
            }
            else
            {
                MainThreadDispatcher.ExecuteOnMainThread(() =>
                {
                    LoadModel(model, null, itemInstance, gameItem);
                });
            }
        }
        else if (gameItem.Name.EndsWith(".wdd"))
        {

        }
        else if (gameItem.Name.EndsWith(".wft"))
        {

        }
    }

    public File FindGameFile(GTADatLoader loader, string filename)
    {
        // Old File Loading

        //File result = null;
        //object locker = new object();

        //Parallel.For(0, loader.imgLoader.imgs.Count, (i, state) =>
        //{
        //    var found = loader.imgLoader.imgs[i].FindItem(filename);
        //    if (found != null)
        //    {
        //        lock (locker)
        //        {
        //            result = found;
        //        }
        //        state.Break();
        //    }
        //});

        File result = null;

        loader.gameFiles.TryGetValue(filename.ToLower(), out result);

        return result;
    }


    #region OldMapComposer
    //void ComposeMap(GTADatLoader loader)
    //{
    //    List<Item_OBJS> ideList = new List<Item_OBJS>();

    //    for (int i = 0; i < loader.iplLoader.ipls.Count; i++)
    //    {
    //        List<Ipl_INST> ipl_Inst = loader.iplLoader.ipls[i].ipl_inst;

    //        for (int j = 0; j < ipl_Inst.Count; j++)
    //        {
    //            int ideNumber = 0;

    //            Item_OBJS ideItem = (Item_OBJS)loader.ideLoader.ides[ideNumber].FindItem(ipl_Inst[j].name);
    //            while (ideItem == null)
    //            {
    //                ideNumber++;
    //                if (ideNumber < loader.ideLoader.ides.Count)
    //                {
    //                    ideItem = (Item_OBJS)loader.ideLoader.ides[ideNumber].FindItem(ipl_Inst[j].name);
    //                }
    //                else
    //                {
    //                    //Debug.Log($"IDE: {ipl_Inst[j].name} Not found");
    //                    break;
    //                }
    //            }
    //            if (ideItem != null)
    //            {
    //                bool found = false;
    //                for (int x = 0; x < ideList.Count; x++)
    //                {
    //                    if (ideList[x].Equals(ideItem))
    //                    {
    //                        found = true;
    //                        ipl_Inst[j].glListID = i + 1;
    //                        ipl_Inst[j].drawDistance = ideItem.drawDistance[0];
    //                    }
    //                }
    //                if (!found)
    //                {
    //                    ipl_Inst[j].glListID = ideList.Count + 1;
    //                    ipl_Inst[j].drawDistance = ideItem.drawDistance[0];
    //                    ideItem.position = ipl_Inst[j].position;
    //                    ideItem.rotation = new Quaternion(-ipl_Inst[j].rotation.x, -ipl_Inst[j].rotation.y, -ipl_Inst[j].rotation.z, ipl_Inst[j].rotation.w);
    //                    ideList.Add(ideItem);
    //                }
    //            }
    //            else
    //            {
    //                ipl_Inst[j].glListID = 0;
    //            }
    //        }

    //        sceneInstances.AddRange(ipl_Inst);
    //    }

    //    Debug.Log($"IDE List: {ideList.Count}");

    //    for (int imgNumber = 0; imgNumber < loader.imgLoader.imgs.Count; imgNumber++)
    //    {
    //        for (int i = 0; i < ideList.Count; i++)
    //        {
    //            string modelName = "";
    //            File item = null;
    //            if (!(ideList[i].wdd.Equals("null") || ideList[i].wdd.Equals("Null")))
    //            {
    //                modelName = ideList[i].wdd + ".wdd";
    //                item = loader.imgLoader.imgs[imgNumber].FindItem(modelName);
    //            }
    //            else
    //            {
    //                modelName = ideList[i].modelName + ".wdr";
    //                item = loader.imgLoader.imgs[imgNumber].FindItem(modelName);
    //                if (item == null)
    //                {
    //                    item = loader.imgLoader.imgs[imgNumber].FindItem(ideList[i].modelName + ".wft");
    //                }
    //            }

    //            if (item != null)
    //            {
    //                if (item.Name.EndsWith(".wdr"))
    //                {
    //                    ModelFile model = new ModelFile();
    //                    TextureFile textureFile = null;
    //                    TextureFile[] textureCollection = new TextureFile[1];

    //                    Debug.Log($"Loading WDR: {item.Name}");
    //                    MemoryStream modelStream = new MemoryStream(item.GetData());
    //                    model.Open(modelStream);


    //                    string texName = ideList[i].textureName + ".wtd";
    //                    var textureData = loader.imgLoader.imgs[imgNumber].FindItem(texName);

    //                    if (textureData != null)
    //                    {
    //                        MemoryStream textureStream = new MemoryStream(textureData.GetData());
    //                        textureFile = new TextureFile();
    //                        textureFile.Open(textureStream);
    //                        textureCollection[0] = textureFile;
    //                        LoadModel(model, textureCollection, ideList[i], item);
    //                    }
    //                    else
    //                    {
    //                        LoadModel(model, null, ideList[i], item);
    //                    }
    //                }
    //                else if (item.Name.EndsWith(".wdd"))
    //                {
    //                    ModelDictionaryFile model = new ModelDictionaryFile();
    //                    TextureFile textureFile = null;
    //                    TextureFile[] textureCollection = new TextureFile[1];

    //                    Debug.Log($"Loading WDD: {item.Name}");
    //                    MemoryStream modelStream = new MemoryStream(item.GetData());
    //                    model.Open(modelStream);

    //                    var hashes = model.File.Data.NameHashes;

    //                    if (hashes.Count > 0)
    //                    {
    //                        string[] wdrNames = new string[hashes.Count];

    //                        for (int xd = 0; xd < hashes.Count; xd++)
    //                        {
    //                            string name = "" + hashes[xd];
    //                            var ini = Hashes.table;
    //                            if (ini.HasOption("Hashes", name))
    //                            {
    //                                wdrNames[xd] = ini.GetValue<string>("Hashes", name); // temp
    //                                Debug.Log($"Discovered WDR {wdrNames[xd]} Model for WDD {item.Name} in IDE: {ideList[i].modelName}");
    //                            }
    //                        }

    //                        for (int xc = 0; xc < wdrNames.Length; xc++)
    //                        {
    //                            string name = wdrNames[xc];

    //                            if (!string.IsNullOrEmpty(name))
    //                            {
    //                                if (ideList[i].modelName == name)
    //                                {
    //                                    var node = model.GetModel(textureCollection);

    //                                    var modelObject = new GameObject(name);
    //                                    modelObject.transform.parent = worldContainer.transform;
    //                                    modelObject.transform.position = ideList[i].position;
    //                                    modelObject.transform.rotation = ideList[i].rotation;

    //                                    foreach (var nodeModel in node.Children[xc].Children)
    //                                    {
    //                                        LoadModelRecursive(modelObject, nodeModel);
    //                                    }


    //                                }
    //                            }
    //                        }
    //                    }


    //                    //LoadModel(model, null, ideList[i], item);
    //                }
    //                else if (item.Name.EndsWith(".wft"))
    //                {

    //                }
    //            }
    //        }
    //    }

    //    worldContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);
    //}
    #endregion

    void LoadModel(IModelFile model, TextureFile[] collection, Ipl_INST definitions, File modelFile)
    {
        ModelNode cachedModel = null;
        RageMaterial cachedTextures = null;

        // Verifica se o modelo já está no cache
        if (!modelCache.TryGetValue(modelFile.Name, out cachedModel))
        {
            // O modelo não está no cache, então precisamos processá-lo
            var modelNode = model.GetModel(collection);
            cachedModel = modelNode;
            modelCache[modelFile.Name] = cachedModel; // Adiciona ao cache

            GameObject modelInstance = new GameObject(modelFile.Name);

            Quaternion rotation = Quaternion.Euler(-90, 0, 0);
            Vector3 newPosition = rotation * definitions.position;

            modelInstance.transform.position = newPosition;
            modelInstance.transform.rotation = rotation * definitions.unityRotation;
            modelInstance.transform.parent = worldContainer.transform;

            LoadModelRecursive(modelInstance, modelNode, modelFile.Name);
            loadedObjects++;
        }
        else
        {
            // Se o modelo está no cache, só instancia a geometria diretamente
            GameObject modelInstance = new GameObject(modelFile.Name);

            Quaternion rotation = Quaternion.Euler(-90, 0, 0);
            Vector3 newPosition = rotation * definitions.position;

            modelInstance.transform.position = newPosition;
            modelInstance.transform.rotation = rotation * definitions.unityRotation;
            modelInstance.transform.parent = worldContainer.transform;


            LoadModelRecursive(modelInstance, cachedModel, modelFile.Name);
        }
    }

    void LoadModelRecursive(GameObject parent, ModelNode modelNode, string originalName)
    {
        if (modelNode.Children.Count > 0)
        {
            for (int i = 0; i < modelNode.Children.Count; i++)
            {
                var modelChildren = modelNode.Children[i];
                GameObject children = new GameObject(modelChildren.Name);
                children.transform.parent = parent.transform;
                children.transform.localPosition = Vector3.zero;
                children.transform.localRotation = Quaternion.identity;

                if (modelChildren.Model3D.geometry != null)
                {
                    var filterChildren = children.AddComponent<MeshFilter>();
                    var rendererChildren = children.AddComponent<MeshRenderer>();

                    if (meshCache.TryGetValue($"{originalName}_{i}", out var mesh))
                    {
                        filterChildren.sharedMesh = mesh;
                        var rageMaterial = modelChildren.Model3D.material;
                        if (rageMaterial != null)
                        {
                            if (materialCache.TryGetValue($"{rageMaterial.shaderName}@{rageMaterial.textureName}", out var cachedMaterial))
                            {
                                rendererChildren.sharedMaterial = cachedMaterial;
                            }
                            else
                            {
                                var unityMaterial = new Material(Shader.Find("gta_default"));
                                unityMaterial.name = $"{rageMaterial.shaderName}@{rageMaterial.textureName}";
                                unityMaterial.enableInstancing = true;

                                if (!string.IsNullOrEmpty(rageMaterial.textureName))
                                {
                                    if (textureCache.TryGetValue(rageMaterial.textureName, out var cachedTex))
                                    {
                                        unityMaterial.SetTexture("_MainTex", cachedTex);
                                    }
                                    else
                                    {
                                        var tex2d = rageMaterial.mainTex.GetUnityTexture();
                                        unityMaterial.SetTexture("_MainTex", tex2d);
                                        textureCache.Add(rageMaterial.textureName, tex2d);
                                    }
                                }


                                rendererChildren.sharedMaterial = unityMaterial;
                            }
                        }
                    }
                    else
                    {
                        filterChildren.sharedMesh = modelChildren.Model3D.geometry.GetUnityMesh();
                        var rageMaterial = modelChildren.Model3D.material;
                        if (rageMaterial != null)
                        {
                            if (materialCache.TryGetValue($"{rageMaterial.shaderName}@{rageMaterial.textureName}", out var cachedMaterial))
                            {
                                rendererChildren.sharedMaterial = cachedMaterial;
                            }
                            else
                            {
                                var unityMaterial = new Material(Shader.Find("gta_default"));
                                unityMaterial.name = $"{rageMaterial.shaderName}@{rageMaterial.textureName}";
                                unityMaterial.enableInstancing = true;

                                if (!string.IsNullOrEmpty(rageMaterial.textureName))
                                {
                                    if (textureCache.TryGetValue(rageMaterial.textureName, out Texture2D cachedTex))
                                    {
                                        unityMaterial.SetTexture("_MainTex", cachedTex);
                                    }
                                    else
                                    {
                                        var tex2d = rageMaterial.mainTex.GetUnityTexture();
                                        unityMaterial.SetTexture("_MainTex", tex2d);
                                        if (!string.IsNullOrEmpty(rageMaterial.textureName))
                                        {
                                            textureCache.Add(rageMaterial.textureName, tex2d);
                                        }
                                    }
                                }

                                rendererChildren.sharedMaterial = unityMaterial;
                            }
                        }

                        meshCache.Add($"{originalName}_{i}", filterChildren.sharedMesh);
                    }


                }

                LoadModelRecursive(children, modelChildren, originalName);
            }
        }
    }

    void ComposeWater(List<Water> waterPlanes, RealFileSystem fs)
    {
        Debug.Log("Creating Water...");

        GameObject waterModel = new GameObject("Water");
        waterModel.transform.parent = transform;
        waterModel.transform.localScale = Vector3.one * 0.1f;
        var renderer = waterModel.AddComponent<MeshRenderer>();
        var filter = waterModel.AddComponent<MeshFilter>();

        Mesh waterMesh = new Mesh();

        List<Vector3> vertexPoints = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int vertexIndex = 0;

        foreach (Water water in waterPlanes)
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

                vertexPoints.Add(p0); // Vertex 1
                vertexPoints.Add(p1); // Vertex 2
                vertexPoints.Add(p2); // Vertex 3
                vertexPoints.Add(p3); // Vertex 4

                triangles.Add(vertexIndex);     // Triangle 1
                triangles.Add(vertexIndex + 2); // Triangle 1
                triangles.Add(vertexIndex + 1); // Triangle 1

                triangles.Add(vertexIndex + 1); // Triangle 2
                triangles.Add(vertexIndex + 2); // Triangle 2
                triangles.Add(vertexIndex + 3); // Triangle 2

                vertexIndex += 4;

                uvs.Add(new Vector2(plane.u, plane.u)); // To p0
                uvs.Add(new Vector2(plane.v, plane.u)); // To p1
                uvs.Add(new Vector2(plane.v, plane.v)); // To p2
                uvs.Add(new Vector2(plane.u, plane.v)); // To p3
            }
        }

        waterMesh.vertices = vertexPoints.ToArray();
        waterMesh.triangles = triangles.ToArray();
        waterMesh.uv = uvs.ToArray();

        waterMesh.RecalculateNormals();
        waterMesh.RecalculateBounds();

        filter.sharedMesh = waterMesh;
        Debug.Log("Loading Water Textures...");

        Directory pcDirectory = (Directory)fs.RootDirectory.FindByName("pc");
        Directory texturesDirectory = (Directory)pcDirectory.FindByName("textures");

        File waterTextureIndex = (File)texturesDirectory.FindByName("water.wtd");

        byte[] waterTextureData = waterTextureIndex.GetData();
        MemoryStream waterTextureStream = new MemoryStream(waterTextureData);
        TextureFile waterTexture = new TextureFile();
        waterTexture.Open(waterTextureStream);
        var waterImage = waterTexture.Textures[0].Decode();
        waterTextureStream.Close();
        var unityMaterial = new Material(Shader.Find("water"));
        unityMaterial.SetTexture("_MainTex", waterImage.GetUnityTexture());
        renderer.material = unityMaterial;
    }


}
