using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using RageLib.FileSystem.Common;
using RageLib.Models;
using RageLib.Textures;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class StaticGeometry : MonoBehaviour
{
    public StaticGeometryData data = new StaticGeometryData();

    private static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    private static Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static Dictionary<string, ModelNode> modelCache = new Dictionary<string, ModelNode>();

    private List<GameObject> childrens = new List<GameObject>();
    private bool loaded;

    public async void LoadModel()
    {
        ModelNode cachedModel = null;

        await Task.Run(() =>
         {
             lock (modelCache)
             {
                 if (data.modelFileName != null)
                 {
                     if (!modelCache.TryGetValue(data.modelFileName, out cachedModel))
                     {

                         try
                         {
                             if (data.collection != null)
                             {
                                 if (data.collection.Length > 0)
                                 {
                                     for (int i = 0; i < data.collection.Length; i++)
                                     {
                                         var textureFile = data.collection[i];
                                         textureFile.Read();
                                     }
                                 }
                             }
                         }
                         catch
                         {
                             data.collection = null;
                         }

                         data.model.Read();
                         var modelNode = data.model.GetModel(data.collection);
                         cachedModel = modelNode;
                         modelCache[data.modelFileName] = cachedModel;
                         WorldComposerMachine.Instance.loadedObjects++;
                     }
                 }

             }
         });

        if (cachedModel != null)
        {
            LoadModelRecursive(gameObject, cachedModel, data.modelFileName);
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
                childrens.Add(children);

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
                                        if (rageMaterial.mainTex != null)
                                        {
                                            var tex2d = rageMaterial.mainTex.GetUnityTexture();
                                            unityMaterial.SetTexture("_MainTex", tex2d);
                                            textureCache.Add(rageMaterial.textureName, tex2d);
                                        }
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
                                        if (!string.IsNullOrEmpty(rageMaterial.textureName) && rageMaterial.mainTex != null)
                                        {
                                            var tex2d = rageMaterial.mainTex.GetUnityTexture();
                                            unityMaterial.SetTexture("_MainTex", tex2d);
                                            textureCache.Add(rageMaterial.textureName, tex2d);
                                        }
                                    }
                                }

                                rendererChildren.sharedMaterial = unityMaterial;
                            }
                        }

                        meshCache.Add($"{originalName}_{i}", filterChildren.sharedMesh);
                    }

                    children.AddComponent<MeshCollider>();
                }

                LoadModelRecursive(children, modelChildren, originalName);
            }
        }
    }
}

public struct StaticGeometryData
{
    public ModelFile model;
    public TextureFile[] collection;
    public Ipl_INST definitions;
    public string modelFileName;
}