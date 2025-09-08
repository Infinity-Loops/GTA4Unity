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
    private static Dictionary<string, ModelNode> modelCache = new Dictionary<string, ModelNode>();

    private List<GameObject> childrens = new List<GameObject>();
    private bool loaded;
    private bool isDestroyed = false;

    public async void LoadModel()
    {
        if (this == null || isDestroyed) return;
        
        ModelNode cachedModel = null;

        await Task.Run(() =>
         {
             if (isDestroyed) return;
             
             lock (modelCache)
             {
                 if (data.modelFileName != null)
                 {
                     if (!modelCache.TryGetValue(data.modelFileName, out cachedModel))
                     {

                         try
                         {
                             if (data.collection != null && !isDestroyed)
                             {
                                 if (data.collection.Length > 0)
                                 {
                                     for (int i = 0; i < data.collection.Length; i++)
                                     {
                                         if (isDestroyed) return;
                                         var textureFile = data.collection[i];
                                         textureFile.Read();
                                         
                                                 foreach (var tex in textureFile.Textures)
                                         {
                                             if (!string.IsNullOrEmpty(tex.Name))
                                             {
                                                 IVUnity.MaterialTextureResolver.RegisterTextureReference(
                                                     tex.Name, tex, data.modelFileName, false);
                                             }
                                         }
                                     }
                                 }
                             }
                         }
                         catch
                         {
                             data.collection = null;
                         }

                         try
                         {
                             if (data.model != null && data.model.File == null && !isDestroyed)
                             {
                                 data.model.Read();
                             }
                         }
                         catch
                         {
                             data.model = null;
                         }


                         if(data.model != null && !isDestroyed)
                         {
                             var modelNode = data.model.GetModel(data.collection);
                             cachedModel = modelNode;
                             modelCache[data.modelFileName] = cachedModel;
                         }
                     }
                 }

             }
         });

        if (this == null || gameObject == null || isDestroyed) return;
        
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
                            Texture2D embeddedTex = null;
                            if (rageMaterial.mainTex != null)
                            {
                                embeddedTex = rageMaterial.mainTex.GetUnityTexture();
                            }
                            var sharedMaterial = IVUnity.MaterialTextureResolver.GetOrCreateSharedMaterial(
                                rageMaterial.shaderName, rageMaterial.textureName, embeddedTex);
                            
                            rendererChildren.sharedMaterial = sharedMaterial;
                            if (sharedMaterial != null && sharedMaterial.mainTexture != null)
                            {
                                IVUnity.MaterialTextureResolver.RegisterMaterial(rageMaterial.shaderName, rageMaterial.textureName, sharedMaterial);
                            }
                        }
                    }
                    else
                    {
                        filterChildren.sharedMesh = modelChildren.Model3D.geometry.GetUnityMesh();
                        var rageMaterial = modelChildren.Model3D.material;
                        if (rageMaterial != null)
                        {
                            Texture2D embeddedTex = null;
                            if (rageMaterial.mainTex != null)
                            {
                                embeddedTex = rageMaterial.mainTex.GetUnityTexture();
                            }
                            var sharedMaterial = IVUnity.MaterialTextureResolver.GetOrCreateSharedMaterial(
                                rageMaterial.shaderName, rageMaterial.textureName, embeddedTex);
                            
                            rendererChildren.sharedMaterial = sharedMaterial;
                            if (sharedMaterial != null && sharedMaterial.mainTexture != null)
                            {
                                IVUnity.MaterialTextureResolver.RegisterMaterial(rageMaterial.shaderName, rageMaterial.textureName, sharedMaterial);
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
    
    void OnDestroy()
    {
        isDestroyed = true;
    }
    
    void OnDisable()
    {
        isDestroyed = true;
    }
}

public struct StaticGeometryData
{
    public ModelFile model;
    public TextureFile[] collection;
    public Ipl_INST definitions;
    public string modelFileName;
}