using System;
using System.Collections.Generic;
using System.Linq;
using RageLib.Models.Data;
using RageLib.Models.Resource;
using RageLib.Models.Resource.Shaders;
using RageLib.Textures;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace RageLib.Models
{
    internal static class ModelGenerator
    {
        private static readonly Dictionary<string, RageUnityTexture> textureCache = new Dictionary<string, RageUnityTexture>();

        private static Textures.Texture FindTexture(TextureFile textures, string name)
        {
            if (textures == null)
            {
                return null;
            }
            return textures.FindTextureByName(name);
        }

        private static RageUnityTexture GetTexture(string textureName, TextureFile attachedTexture, TextureFile[] externalTextures)
        {
            lock (textureCache)
            {
                if (string.IsNullOrEmpty(textureName))
                {
                    return null;
                }

                if (textureCache.ContainsKey(textureName))
                    return textureCache[textureName];

                var textureObj = FindTexture(attachedTexture, textureName);
                if (textureObj == null && externalTextures != null)
                {
                    foreach (var file in externalTextures)
                    {
                        textureObj = FindTexture(file, textureName);
                        if (textureObj != null)
                        {
                            break;
                        }
                    }
                }

                if (textureObj != null)
                {
                    var decodedTexture = textureObj.Decode() as RageUnityTexture;
                    textureCache[textureName] = decodedTexture;
                    return decodedTexture;
                }

                return null;
            }
        }

        internal static ModelNode GenerateModel(FragTypeModel fragTypeModel, TextureFile[] textures)
        {
            var fragTypeGroup = new Model3DGroup();
            var fragTypeNode = new ModelNode { DataModel = fragTypeModel, Model3D = fragTypeGroup, Name = "FragType", NoCount = true };

            var parentDrawableNode = GenerateModel(fragTypeModel.Drawable, textures);
            parentDrawableNode.NoCount = false;
            fragTypeGroup.Children.Add(parentDrawableNode.Model3D);
            fragTypeNode.Children.Add(parentDrawableNode);

            foreach (var fragTypeChild in fragTypeModel.Children)
            {
                if (fragTypeChild.Drawable != null && fragTypeChild.Drawable.ModelCollection.Length > 0)
                {
                    var childDrawableNode = GenerateModel(fragTypeChild.Drawable, textures);
                    childDrawableNode.NoCount = false;
                    fragTypeGroup.Children.Add(childDrawableNode.Model3D);
                    fragTypeNode.Children.Add(childDrawableNode);
                }
            }

            return fragTypeNode;
        }

        internal static ModelNode GenerateModel(DrawableModelDictionary drawableModelDictionary, TextureFile[] textures)
        {
            var dictionaryTypeGroup = new Model3DGroup();
            var dictionaryTypeNode = new ModelNode { DataModel = drawableModelDictionary, Model3D = dictionaryTypeGroup, Name = "Dictionary", NoCount = true };
            foreach (var entry in drawableModelDictionary.Entries)
            {
                var drawableNode = GenerateModel(entry, textures);
                drawableNode.NoCount = false;
                dictionaryTypeGroup.Children.Add(drawableNode.Model3D);
                dictionaryTypeNode.Children.Add(drawableNode);
            }
            return dictionaryTypeNode;
        }

        internal static ModelNode GenerateModel(DrawableModel drawableModel, TextureFile[] textures)
        {
            return GenerateModel(new Drawable(drawableModel), textures);
        }

        internal static ModelNode GenerateModel(Drawable drawable, TextureFile[] textures)
        {
            var materials = new RageMaterial[drawable.Materials.Count];
            //Debug.Log($"Material Count: {materials.Length}");

            for (int i = 0; i < materials.Length; i++)
            {
                var drawableMat = drawable.Materials[i];
                string texName = null;
                RageUnityTexture mainTex = new RageUnityTexture(1, 1, TextureFormat.ARGB32, false);

                if (drawableMat.Parameters.ContainsKey((int)ParamNameHash.Texture))
                {
                    var texture = drawableMat.Parameters[(int)ParamNameHash.Texture] as MaterialParamTexture;
                    if (texture != null)
                    {
                        mainTex = GetTexture(texture.TextureName, drawable.AttachedTexture, textures);
                        texName = texture.TextureName;
                    }
                    else
                    {
                        texName = "";
                    }
                }
                else
                {
                    if (drawableMat.Parameters.Count > 0)
                    {
                        var data = drawableMat.Parameters.Values.ToArray();
                        var texture = data[0] as MaterialParamTexture;

                        if (texture != null)
                        {
                            Debug.Log($"Found workaround tex {texture.TextureName}");
                            mainTex = GetTexture(texture.TextureName, drawable.AttachedTexture, textures);
                            texName = texture.TextureName;
                        }
                        else
                        {
                            texName = "";
                        }

                    }
                }

                var material = new RageMaterial(drawableMat.ShaderName, texName, mainTex);
                materials[i] = material;
            }

            var drawableModelGroup = new Model3DGroup();
            var drawableModelNode = new ModelNode { DataModel = drawable, Model3D = drawableModelGroup, Name = "Drawable", NoCount = true };

            foreach (var model in drawable.Models)
            {
                var modelGroup = new Model3DGroup();
                var modelNode = new ModelNode { DataModel = model, Model3D = modelGroup, Name = "Model" };
                drawableModelNode.Children.Add(modelNode);
                foreach (var geometry in model.Geometries)
                {
                    var geometryGroup = new Model3DGroup();
                    var geometryNode = new ModelNode { DataModel = geometry, Model3D = geometryGroup, Name = "Geometry" };
                    modelNode.Children.Add(geometryNode);

                    for (int meshIndex = 0; meshIndex < geometry.Meshes.Count; meshIndex++)
                    {
                        var mesh = geometry.Meshes[meshIndex];
                        var mesh3D = new MeshGeometry3D();

                        var meshNode = new ModelNode { DataModel = mesh, Model3D = null, Name = "Mesh" };
                        geometryNode.Children.Add(meshNode);

                        var jobsVertexData = new NativeArray<CleanVertex>(mesh.DecodeUnityBurstVertexData(), Allocator.TempJob);

                        var decodeJob = new MeshDecodeJob
                        {
                            Positions = new NativeArray<Vector3>(jobsVertexData.Length, Allocator.TempJob),
                            Normals = new NativeArray<Vector3>(jobsVertexData.Length, Allocator.TempJob),
                            TextureCoordinates = new NativeArray<Vector2>(jobsVertexData.Length, Allocator.TempJob),
                            Vertices = jobsVertexData,
                            HasNormals = mesh.VertexHasNormal,
                            HasTextureCoordinates = mesh.VertexHasTexture
                        };

                        var decodeIndexJob = new MeshDecodeIndexJob
                        {
                            Indices = new NativeArray<ushort>(mesh.DecodeIndexData(), Allocator.TempJob),
                            TriangleIndices = new NativeList<int>(Allocator.TempJob),
                            FaceCount = mesh.FaceCount
                        };

                        var handle = decodeJob.Schedule();
                        var indexHandle = decodeIndexJob.Schedule(handle);
                        indexHandle.Complete();

                        mesh3D.positions.AddRange(decodeJob.Positions.ToArray());
                        if (mesh.VertexHasNormal)
                        {
                            mesh3D.normals.AddRange(decodeJob.Normals.ToArray());
                        }
                        if (mesh.VertexHasTexture)
                        {
                            mesh3D.textureCoordinates.AddRange(decodeJob.TextureCoordinates.ToArray());
                        }
                        mesh3D.triangleIndices.AddRange(decodeIndexJob.TriangleIndices.AsArray().ToArray());

                        decodeJob.Vertices.Dispose();
                        decodeJob.Positions.Dispose();
                        decodeJob.Normals.Dispose();
                        decodeJob.TextureCoordinates.Dispose();
                        decodeIndexJob.Indices.Dispose();
                        decodeIndexJob.TriangleIndices.Dispose();

                        var material = materials[geometry.Meshes[meshIndex].MaterialIndex];
                        var model3D = new GeometryModel3D(mesh3D, material);
                        geometryGroup.Children.Add(model3D);
                        meshNode.Model3D = model3D;
                    }

                    modelGroup.Children.Add(geometryGroup);
                }

                drawableModelGroup.Children.Add(modelGroup);
            }

            return drawableModelNode;
        }

        [BurstCompile]
        private struct MeshDecodeJob : IJob
        {
            [ReadOnly] public NativeArray<CleanVertex> Vertices;
            public NativeArray<Vector3> Positions;
            public NativeArray<Vector3> Normals;
            public NativeArray<Vector2> TextureCoordinates;
            public bool HasNormals;
            public bool HasTextureCoordinates;

            public void Execute()
            {
                for (int i = 0; i < Vertices.Length; i++)
                {
                    Positions[i] = Vertices[i].Position;
                    if (HasNormals)
                    {
                        Normals[i] = Vertices[i].Normal;
                    }
                    if (HasTextureCoordinates)
                    {
                        TextureCoordinates[i] = Vertices[i].TextureCoordinates;
                    }
                }
            }
        }

        [BurstCompile]
        private struct MeshDecodeIndexJob : IJob
        {
            [ReadOnly] public NativeArray<ushort> Indices;
            public NativeList<int> TriangleIndices;
            public int FaceCount;

            public void Execute()
            {
                for (int i = 0; i < FaceCount; i++)
                {
                    TriangleIndices.Add(Indices[i * 3 + 0]);
                    TriangleIndices.Add(Indices[i * 3 + 1]);
                    TriangleIndices.Add(Indices[i * 3 + 2]);
                }
            }
        }

    }
}
