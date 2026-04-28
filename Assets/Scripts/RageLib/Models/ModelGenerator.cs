using System;
using System.Collections.Generic;
using RageLib.Models.Data;
using RageLib.Models.Resource;
using RageLib.Models.Resource.Shaders;
using RageLib.Textures;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace RageLib.Models
{
    public static class ModelGenerator
    {
        private static readonly Dictionary<string, RageUnityTexture> textureCache = new Dictionary<string, RageUnityTexture>();

        private static Textures.Texture FindTexture(TextureFile textures, string name)
        {
            if (textures == null) return null;
            return textures.FindTextureByName(name);
        }

        private static RageUnityTexture GetTexture(string textureName, TextureFile attachedTexture, TextureFile[] externalTextures)
        {
            lock (textureCache)
            {
                if (string.IsNullOrEmpty(textureName)) return null;

                if (textureCache.TryGetValue(textureName, out var cached))
                    return cached;

                var textureObj = FindTexture(attachedTexture, textureName);
                if (textureObj == null && externalTextures != null)
                {
                    for (int i = 0; i < externalTextures.Length; i++)
                    {
                        textureObj = FindTexture(externalTextures[i], textureName);
                        if (textureObj != null) break;
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
            parentDrawableNode.Name = "FragmentParent";
            fragTypeGroup.Children.Add(parentDrawableNode.Model3D);
            fragTypeNode.Children.Add(parentDrawableNode);

            for (int i = 0; i < fragTypeModel.Children.Length; i++)
            {
                var fragTypeChild = fragTypeModel.Children[i];
                if (fragTypeChild.Drawable != null && fragTypeChild.Drawable.ModelCollection.Length > 0)
                {
                    var childDrawableNode = GenerateModel(fragTypeChild.Drawable, textures);
                    childDrawableNode.NoCount = false;
                    childDrawableNode.Name = $"FragmentChild_{i}";
                    childDrawableNode.FragmentChild = fragTypeChild;
                    childDrawableNode.FragmentChildIndex = i;

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

        public static ModelNode GenerateModel(Drawable drawable, TextureFile[] textures)
        {
            var materials = ResolveMaterials(drawable, textures);

            // Collect all meshes for batched job scheduling
            var meshWorkItems = new List<MeshWorkItem>();
            CollectMeshes(drawable, materials, meshWorkItems);

            // Schedule all decode jobs, then Complete once
            var jobHandles = new NativeArray<JobHandle>(meshWorkItems.Count, Allocator.Temp);
            for (int m = 0; m < meshWorkItems.Count; m++)
            {
                var work = meshWorkItems[m];
                var vertexHandle = work.DecodeJob.Schedule();
                jobHandles[m] = work.IndexJob.Schedule(vertexHandle);
            }
            JobHandle.CompleteAll(jobHandles);
            jobHandles.Dispose();

            // Copy results to MeshGeometry3D arrays and dispose NativeArrays
            for (int m = 0; m < meshWorkItems.Count; m++)
            {
                var work = meshWorkItems[m];
                var mesh3D = work.Mesh3D;

                mesh3D.positions = work.DecodeJob.Positions.ToArray();
                mesh3D.normals = work.HasNormals ? work.DecodeJob.Normals.ToArray() : null;
                mesh3D.textureCoordinates = work.HasTexCoords ? work.DecodeJob.TextureCoordinates.ToArray() : null;
                mesh3D.colors = work.HasColors ? work.DecodeJob.Colors.ToArray() : null;
                mesh3D.triangleIndices = work.IndexJob.TriangleIndices.AsArray().ToArray();

                work.DecodeJob.Vertices.Dispose();
                work.DecodeJob.Positions.Dispose();
                work.DecodeJob.Normals.Dispose();
                work.DecodeJob.TextureCoordinates.Dispose();
                work.DecodeJob.Colors.Dispose();
                work.IndexJob.Indices.Dispose();
                work.IndexJob.TriangleIndices.Dispose();
            }

            // Build the node tree (lightweight — no data copies)
            return BuildNodeTree(drawable, materials, meshWorkItems);
        }

        private struct MeshWorkItem
        {
            public MeshGeometry3D Mesh3D;
            public MeshDecodeJob DecodeJob;
            public MeshDecodeIndexJob IndexJob;
            public RageMaterial Material;
            public bool HasNormals;
            public bool HasTexCoords;
            public bool HasColors;
            // Tree location
            public int ModelIndex;
            public int GeometryIndex;
            public int MeshIndex;
        }

        private static void CollectMeshes(Drawable drawable, RageMaterial[] materials, List<MeshWorkItem> workItems)
        {
            for (int mi = 0; mi < drawable.Models.Count; mi++)
            {
                var model = drawable.Models[mi];
                for (int gi = 0; gi < model.Geometries.Count; gi++)
                {
                    var geometry = model.Geometries[gi];
                    for (int meshIdx = 0; meshIdx < geometry.Meshes.Count; meshIdx++)
                    {
                        var mesh = geometry.Meshes[meshIdx];
                        var decoded = mesh.DecodeUnityBurstVertexData();
                        var vertexData = new NativeArray<CleanVertex>(decoded, Allocator.TempJob);

                        var decodeJob = new MeshDecodeJob
                        {
                            Vertices = vertexData,
                            Positions = new NativeArray<Vector3>(vertexData.Length, Allocator.TempJob),
                            Normals = new NativeArray<Vector3>(vertexData.Length, Allocator.TempJob),
                            TextureCoordinates = new NativeArray<Vector2>(vertexData.Length, Allocator.TempJob),
                            Colors = new NativeArray<Color32>(vertexData.Length, Allocator.TempJob),
                            HasNormals = mesh.VertexHasNormal,
                            HasTextureCoordinates = mesh.VertexHasTexture,
                            HasColors = mesh.VertexHasColor,
                        };

                        var indexJob = new MeshDecodeIndexJob
                        {
                            Indices = new NativeArray<ushort>(mesh.DecodeIndexData(), Allocator.TempJob),
                            TriangleIndices = new NativeList<int>(mesh.FaceCount * 3, Allocator.TempJob),
                            FaceCount = mesh.FaceCount,
                        };

                        workItems.Add(new MeshWorkItem
                        {
                            Mesh3D = new MeshGeometry3D(),
                            DecodeJob = decodeJob,
                            IndexJob = indexJob,
                            Material = materials[mesh.MaterialIndex],
                            HasNormals = mesh.VertexHasNormal,
                            HasTexCoords = mesh.VertexHasTexture,
                            HasColors = mesh.VertexHasColor,
                            ModelIndex = mi,
                            GeometryIndex = gi,
                            MeshIndex = meshIdx,
                        });
                    }
                }
            }
        }

        private static ModelNode BuildNodeTree(Drawable drawable, RageMaterial[] materials, List<MeshWorkItem> workItems)
        {
            var drawableModelGroup = new Model3DGroup();
            var drawableModelNode = new ModelNode { DataModel = drawable, Model3D = drawableModelGroup, Name = "Drawable", NoCount = true };

            int workIdx = 0;
            for (int mi = 0; mi < drawable.Models.Count; mi++)
            {
                var model = drawable.Models[mi];
                var modelGroup = new Model3DGroup();
                var modelNode = new ModelNode { DataModel = model, Model3D = modelGroup, Name = "Model" };
                drawableModelNode.Children.Add(modelNode);

                for (int gi = 0; gi < model.Geometries.Count; gi++)
                {
                    var geometry = model.Geometries[gi];
                    var geometryGroup = new Model3DGroup();
                    var geometryNode = new ModelNode { DataModel = geometry, Model3D = geometryGroup, Name = "Geometry" };
                    modelNode.Children.Add(geometryNode);

                    for (int meshIdx = 0; meshIdx < geometry.Meshes.Count; meshIdx++)
                    {
                        var work = workItems[workIdx++];
                        var model3D = new GeometryModel3D(work.Mesh3D, work.Material);
                        geometryGroup.Children.Add(model3D);
                        geometryNode.Children.Add(new ModelNode { DataModel = geometry.Meshes[meshIdx], Model3D = model3D, Name = "Mesh" });
                    }

                    modelGroup.Children.Add(geometryGroup);
                }

                drawableModelGroup.Children.Add(modelGroup);
            }

            return drawableModelNode;
        }

        private static RageMaterial[] ResolveMaterials(Drawable drawable, TextureFile[] textures)
        {
            var materials = new RageMaterial[drawable.Materials.Count];

            for (int i = 0; i < materials.Length; i++)
            {
                var drawableMat = drawable.Materials[i];
                string texName = null;
                string normalTexName = null;
                string specularTexName = null;
                RageUnityTexture mainTex = new RageUnityTexture(1, 1, TextureFormat.ARGB32, false);
                RageUnityTexture normalTex = null;
                RageUnityTexture specularTex = null;

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
                    foreach (var param in drawableMat.Parameters.Values)
                    {
                        if (param is MaterialParamTexture tex)
                        {
                            mainTex = GetTexture(tex.TextureName, drawable.AttachedTexture, textures);
                            texName = tex.TextureName;
                            break;
                        }
                    }
                    if (texName == null) texName = "";
                }

                if (drawableMat.Parameters.ContainsKey((int)ParamNameHash.NormalTexture))
                {
                    var normalTexture = drawableMat.Parameters[(int)ParamNameHash.NormalTexture] as MaterialParamTexture;
                    if (normalTexture != null)
                    {
                        normalTex = GetTexture(normalTexture.TextureName, drawable.AttachedTexture, textures);
                        normalTexName = normalTexture.TextureName;
                    }
                }

                if (drawableMat.Parameters.ContainsKey((int)ParamNameHash.SpecularTexture))
                {
                    var specularTexture = drawableMat.Parameters[(int)ParamNameHash.SpecularTexture] as MaterialParamTexture;
                    if (specularTexture != null)
                    {
                        specularTex = GetTexture(specularTexture.TextureName, drawable.AttachedTexture, textures);
                        specularTexName = specularTexture.TextureName;
                    }
                }

                var material = new RageMaterial(drawableMat.ShaderName, texName, mainTex);
                material.normalTex = normalTex;
                material.normalTextureName = normalTexName;
                material.specularTex = specularTex;
                material.specularTextureName = specularTexName;

                if (drawableMat.ShaderName != null &&
                    drawableMat.ShaderName.IndexOf("terrain_va", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var layerTexes = new List<RageUnityTexture>(4);
                    var layerNames = new List<string>(4);
                    foreach (var param in drawableMat.Parameters.Values)
                    {
                        if (param is MaterialParamTexture tex)
                        {
                            layerTexes.Add(GetTexture(tex.TextureName, drawable.AttachedTexture, textures));
                            layerNames.Add(tex.TextureName);
                            if (layerTexes.Count >= 4) break;
                        }
                    }
                    material.layerTextures     = layerTexes.ToArray();
                    material.layerTextureNames = layerNames.ToArray();
                }

                materials[i] = material;
            }

            return materials;
        }

        [BurstCompile]
        private struct MeshDecodeJob : IJob
        {
            [ReadOnly] public NativeArray<CleanVertex> Vertices;
            public NativeArray<Vector3> Positions;
            public NativeArray<Vector3> Normals;
            public NativeArray<Vector2> TextureCoordinates;
            public NativeArray<Color32> Colors;
            public bool HasNormals;
            public bool HasTextureCoordinates;
            public bool HasColors;

            public void Execute()
            {
                for (int i = 0; i < Vertices.Length; i++)
                {
                    Positions[i] = Vertices[i].Position;

                    if (HasNormals)
                        Normals[i] = Vertices[i].Normal;
                    if (HasTextureCoordinates)
                        TextureCoordinates[i] = Vertices[i].TextureCoordinates;
                    if (HasColors)
                    {
                        uint argb = Vertices[i].DiffuseColor;
                        byte b = (byte)(argb        & 0xFF);
                        byte g = (byte)((argb >> 8)  & 0xFF);
                        byte r = (byte)((argb >> 16) & 0xFF);
                        byte a = (byte)((argb >> 24) & 0xFF);
                        Colors[i] = new Color32(r, g, b, a);
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
