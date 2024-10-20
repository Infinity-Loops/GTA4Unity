using RageLib.FileSystem;
using RageLib.FileSystem.Common;
using RageLib.Models;
using RageLib.Models.Data;
using RageLib.Textures;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEngine;
using Directory = RageLib.FileSystem.Common.Directory;
using File = RageLib.FileSystem.Common.File;
using Material = UnityEngine.Material;

public class Player : MonoBehaviour
{
    private GTADatLoader loader;
    private RPFFileSystem playerped;
    private List<File> playerFiles;
    private Dictionary<string, File> playerPedFiles = new Dictionary<string, File>();
    public void Load(GTADatLoader loader)
    {
        this.loader = loader;
        GetPlayerRPF();
        LoadMeshes();
    }

    public void GetPlayerRPF()
    {
        Directory pcDirectory = (Directory)loader.root.RootDirectory.FindByName("pc");
        Directory modelsDirectory = (Directory)pcDirectory.FindByName("models");
        Directory cdimagesDirectory = (Directory)modelsDirectory.FindByName("cdimages");
        File playerPed = (File)cdimagesDirectory.FindByName("playerped.rpf");
        Debug.Log($"Player RPF: {System.IO.Path.GetDirectoryName(loader.gameDir)}/{playerPed.FullName}");

        playerped = new RPFFileSystem();
        playerped.Open($"{System.IO.Path.GetDirectoryName(loader.gameDir)}/{playerPed.FullName}");


        playerFiles = playerped.GetAllFiles();

        foreach (var item in playerFiles)
        {
            playerPedFiles[item.Name] = item;
            Debug.Log($"Adding {item.Name} to playerPedFiles");
        }
    }

    public void LoadMeshes()
    {
        GetModel("head_000_r.wdr", "head_diff_000_a_whi.wtd");
    }

    public void GetModel(string modelName, string textureName)
    {
        var item = playerPedFiles[modelName];
        ModelFile model = new ModelFile();

        using (MemoryStream modelStream = new MemoryStream(item.GetData()))
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

        TextureFile textureFile = null;
        TextureFile[] textureCollection = new TextureFile[1];
        var textureData = playerPedFiles[textureName];

        if (textureData != null)
        {
            using (MemoryStream textureStream = new MemoryStream(textureData.GetData()))
            {
                try
                {
                    textureFile = new TextureFile();
                    textureFile.Open(textureStream);
                    textureCollection[0] = textureFile;
                }
                catch
                {
                    textureCollection = null;
                }
            }
            LoadModel(modelName, gameObject, model, textureCollection);
        }
        else
        {
            LoadModel(modelName, gameObject, model, null);
        }
    }

    public void LoadModel(string name, GameObject parent, ModelFile model, TextureFile[] collection)
    {
        var modelNode = model.GetModel(collection);
        var data = model.GetDataModel();

        GameObject root = new GameObject($"{name}-root");
        root.transform.parent = transform;
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.gameObject.AddComponent<SkeletonDrawer>();
        Transform[] bones = null;

        if (data.Skeleton != null)
        {
            BuildSkeleton(root, data.Skeleton.RootBone);
            bones = root.transform.GetComponentsInChildren<Transform>();
        }

        LoadModelRecursive(root, modelNode, name, bones);
    }

    void LoadModelRecursive(GameObject parent, ModelNode modelNode, string originalName,Transform[] bones)
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
                    var renderer = children.AddComponent<SkinnedMeshRenderer>();

                    renderer.sharedMesh = modelChildren.Model3D.geometry.GetUnityMesh();

                    if (bones != null)
                    {
                        if (bones.Length > 0)
                        {
                            renderer.rootBone = bones[0];
                            renderer.bones = bones;
                        }
                    }

                    var rageMaterial = modelChildren.Model3D.material;
                    if (rageMaterial != null)
                    {
                        var unityMaterial = new Material(Shader.Find("gta_default"));
                        unityMaterial.name = $"{rageMaterial.shaderName}@{rageMaterial.textureName}";
                        unityMaterial.enableInstancing = true;

                        if (!string.IsNullOrEmpty(rageMaterial.textureName) && rageMaterial.mainTex != null)
                        {
                            var tex2d = rageMaterial.mainTex.GetUnityTexture();
                            unityMaterial.SetTexture("_MainTex", tex2d);
                        }

                        renderer.sharedMaterial = unityMaterial;
                    }
                }

                LoadModelRecursive(children, modelChildren, originalName, bones);
            }
        }
    }

    public void BuildSkeleton(GameObject parent, Bone bone)
    {
        GameObject objBone = new GameObject(bone.Name);

        objBone.transform.parent = parent.transform;
        objBone.transform.localPosition = bone.GetUnityPosition;
        objBone.transform.localRotation = bone.GetUnityRotation;

        if (bone.Children != null)
        {
            if (bone.Children.Count > 0)
            {
                foreach (var child in bone.Children)
                {
                    BuildSkeleton(objBone, child);
                }
            }
        }
    }
}

public class SkeletonDrawer : MonoBehaviour
{
    private void Update()
    {
        DrawRecursive(transform);
    }

    // Desenha recursivamente linhas conectando o pai aos filhos
    public void DrawRecursive(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            Debug.DrawLine(parent.position, child.position, Color.green);

            DrawRecursive(child);
        }
    }
}