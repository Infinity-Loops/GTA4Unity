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
        // Check if this is a fragment model and apply transforms
        if (modelNode.DataModel is RageLib.Models.Resource.FragTypeModel fragModel)
        {
            ApplyFragmentTransforms(parent, modelNode, fragModel, originalName);
            return;
        }
        
        if (modelNode.Children.Count > 0)
        {
            for (int i = 0; i < modelNode.Children.Count; i++)
            {
                var modelChildren = modelNode.Children[i];
                GameObject children = new GameObject(modelChildren.Name);
                children.transform.parent = parent.transform;
                children.transform.localPosition = Vector3.zero;
                children.transform.localRotation = Quaternion.identity;
                children.transform.localScale = Vector3.one;
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
                                rageMaterial.shaderName, rageMaterial.textureName, embeddedTex,
                                rageMaterial.normalTextureName, rageMaterial.normalTex?.GetUnityTexture(),
                                rageMaterial.specularTextureName, rageMaterial.specularTex?.GetUnityTexture());
                            
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
                                rageMaterial.shaderName, rageMaterial.textureName, embeddedTex,
                                rageMaterial.normalTextureName, rageMaterial.normalTex?.GetUnityTexture(),
                                rageMaterial.specularTextureName, rageMaterial.specularTex?.GetUnityTexture());
                            
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
    
    void ApplyFragmentTransforms(GameObject parent, ModelNode modelNode, RageLib.Models.Resource.FragTypeModel fragModel, string originalName)
    {
        // Process fragment children with their transforms
        if (modelNode.Children.Count > 0)
        {
            // Get positions from drawable centers
            var childPositions = fragModel.GetAllChildPositions();
            
            for (int i = 0; i < modelNode.Children.Count; i++)
            {
                var modelChildren = modelNode.Children[i];
                GameObject children = new GameObject(modelChildren.Name);
                children.transform.parent = parent.transform;
                
                // Apply position from drawable center if available
                if (i < childPositions.Length)
                {
                    var position = childPositions[i];
                    if (position != Vector3.zero)
                    {
                        // Use the drawable center as position
                        children.transform.localPosition = position;
                    }
                    else if (i > 0)
                    {
                        // Fallback: space out fragments if no center data
                        float spacing = 0.5f;
                        children.transform.localPosition = new Vector3(0, i * spacing, 0);
                    }
                    else
                    {
                        // First child at origin if no center data
                        children.transform.localPosition = Vector3.zero;
                    }
                }
                else
                {
                    // No position data for this index - use simple spacing
                    float spacing = 0.5f;
                    children.transform.localPosition = new Vector3(0, i * spacing, 0);
                }
                
                // Set default rotation and scale
                children.transform.localRotation = Quaternion.identity;
                children.transform.localScale = Vector3.one;
                
                childrens.Add(children);

                // Add mesh components if geometry exists
                if (modelChildren.Model3D.geometry != null)
                {
                    var filterChildren = children.AddComponent<MeshFilter>();
                    var rendererChildren = children.AddComponent<MeshRenderer>();

                    string cacheKey = $"{originalName}_frag_{i}";
                    
                    if (meshCache.TryGetValue(cacheKey, out var mesh))
                    {
                        filterChildren.sharedMesh = mesh;
                    }
                    else
                    {
                        filterChildren.sharedMesh = modelChildren.Model3D.geometry.GetUnityMesh();
                        meshCache.Add(cacheKey, filterChildren.sharedMesh);
                    }
                    
                    // Apply material
                    var rageMaterial = modelChildren.Model3D.material;
                    if (rageMaterial != null)
                    {
                        Texture2D embeddedTex = null;
                        if (rageMaterial.mainTex != null)
                        {
                            embeddedTex = rageMaterial.mainTex.GetUnityTexture();
                        }
                        var sharedMaterial = IVUnity.MaterialTextureResolver.GetOrCreateSharedMaterial(
                            rageMaterial.shaderName, rageMaterial.textureName, embeddedTex,
                            rageMaterial.normalTextureName, rageMaterial.normalTex?.GetUnityTexture(),
                            rageMaterial.specularTextureName, rageMaterial.specularTex?.GetUnityTexture());
                        
                        rendererChildren.sharedMaterial = sharedMaterial;
                        if (sharedMaterial != null && sharedMaterial.mainTexture != null)
                        {
                            IVUnity.MaterialTextureResolver.RegisterMaterial(rageMaterial.shaderName, rageMaterial.textureName, sharedMaterial);
                        }
                    }

                    // Add collider for fragment pieces
                    var collider = children.AddComponent<MeshCollider>();
                    collider.convex = true; // Make convex for physics interactions
                    
                    // Tag as fragment for identification
                    children.tag = "Fragment";
                    
                    // Store fragment info for physics/damage system
                    var fragInfo = children.AddComponent<FragmentInfo>();
                    fragInfo.childIndex = i;
                    fragInfo.parentFragModel = fragModel;
                    
                    // Set default values
                    fragInfo.boneId = 0xFFFF; // No bone data available
                    fragInfo.mass = 1.0f;
                    fragInfo.damagedMass = 0.5f;
                    fragInfo.health = 100;
                    fragInfo.minDamageForce = 10;
                }

                // Recursively process children
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