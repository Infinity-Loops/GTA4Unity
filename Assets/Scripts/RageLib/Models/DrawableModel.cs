using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RageLib.Textures;
using RageLib.Textures.Decoder;
using RageLib.Textures.Resource;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Model3DGroup : GeometryModel3D
{
    public List<GeometryModel3D> Children = new List<GeometryModel3D>();
}

public class MeshGeometry3D
{
    public Vector3[] positions;
    public Vector3[] normals;
    public Vector2[] textureCoordinates;
    public Color32[] colors;
    public int[] triangleIndices;
    public BoneWeight[] boneWeights;

    [StructLayout(LayoutKind.Sequential)]
    struct SkinnedStream0
    {
        public Vector3 pos;
        public Vector3 nrm;
        public Vector4 tan;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SkinnedStream1
    {
        public Vector2 uv;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SkinnedStream2
    {
        public Color32 color;
    }

    public Mesh GetUnityMesh()
    {
        var mesh = new Mesh();
        if (boneWeights != null && boneWeights.Length == positions.Length)
            return BuildSkinnedMesh(mesh);

        if (positions != null) mesh.SetVertices(positions);
        if (normals != null && normals.Length == positions.Length) mesh.SetNormals(normals);
        if (textureCoordinates != null && textureCoordinates.Length == positions.Length) mesh.SetUVs(0, textureCoordinates);
        if (colors != null && colors.Length == positions.Length) mesh.SetColors(colors);
        if (triangleIndices != null) mesh.SetIndices(triangleIndices, MeshTopology.Triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }

    Mesh BuildSkinnedMesh(Mesh mesh)
    {
        int vertexCount = positions.Length;

        // Compute tangents on a temp mesh to avoid polluting our final mesh state
        Vector4[] tangents;
        if (normals != null && normals.Length == vertexCount && textureCoordinates != null)
        {
            var tmp = new Mesh();
            tmp.SetVertices(positions);
            tmp.SetNormals(normals);
            tmp.SetUVs(0, textureCoordinates);
            if (triangleIndices != null) tmp.SetIndices(triangleIndices, MeshTopology.Triangles, 0);
            tmp.RecalculateTangents();
            tangents = tmp.tangents;
            Object.Destroy(tmp);
        }
        else
        {
            tangents = new Vector4[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                tangents[i] = new Vector4(1, 0, 0, 1);
        }

        // Compute deformation requires:
        //   Stream 0: Position(Float32x3) + Normal(Float32x3) + Tangent(Float32x4) ONLY
        //   Stream 1+: everything else (UVs, colors, etc.)
        mesh.SetVertexBufferParams(vertexCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2));

        var s0 = new NativeArray<SkinnedStream0>(vertexCount, Allocator.Temp);
        var s1 = new NativeArray<SkinnedStream1>(vertexCount, Allocator.Temp);
        var s2 = new NativeArray<SkinnedStream2>(vertexCount, Allocator.Temp);

        for (int i = 0; i < vertexCount; i++)
        {
            s0[i] = new SkinnedStream0
            {
                pos = positions[i],
                nrm = normals != null ? normals[i] : Vector3.up,
                tan = tangents[i],
            };
            s1[i] = new SkinnedStream1
            {
                uv = textureCoordinates != null ? textureCoordinates[i] : Vector2.zero,
            };
            s2[i] = new SkinnedStream2
            {
                color = colors != null && i < colors.Length ? colors[i] : new Color32(255, 255, 255, 255),
            };
        }

        mesh.SetVertexBufferData(s0, 0, 0, vertexCount, 0);
        mesh.SetVertexBufferData(s1, 0, 0, vertexCount, 1);
        mesh.SetVertexBufferData(s2, 0, 0, vertexCount, 2);
        s0.Dispose();
        s1.Dispose();
        s2.Dispose();

        if (triangleIndices != null)
            mesh.SetIndices(triangleIndices, MeshTopology.Triangles, 0);

        // Use modern SetBoneWeights API — stored in separate buffer, won't touch vertex streams
        var bonesPerVertex = new NativeArray<byte>(vertexCount, Allocator.Temp);
        var weights = new NativeArray<BoneWeight1>(vertexCount * 4, Allocator.Temp);
        int wi = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            var bw = boneWeights[i];
            byte count = 0;
            if (bw.weight0 > 0) { weights[wi++] = new BoneWeight1 { boneIndex = bw.boneIndex0, weight = bw.weight0 }; count++; }
            if (bw.weight1 > 0) { weights[wi++] = new BoneWeight1 { boneIndex = bw.boneIndex1, weight = bw.weight1 }; count++; }
            if (bw.weight2 > 0) { weights[wi++] = new BoneWeight1 { boneIndex = bw.boneIndex2, weight = bw.weight2 }; count++; }
            if (bw.weight3 > 0) { weights[wi++] = new BoneWeight1 { boneIndex = bw.boneIndex3, weight = bw.weight3 }; count++; }
            bonesPerVertex[i] = count;
        }
        mesh.SetBoneWeights(bonesPerVertex, weights.GetSubArray(0, wi));
        bonesPerVertex.Dispose();
        weights.Dispose();

        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return mesh;
    }
}

public class GeometryModel3D
{
    public MeshGeometry3D geometry;
    public RageMaterial material;
    public GeometryModel3D() { }
    public GeometryModel3D(MeshGeometry3D geometry, RageMaterial material)
    {
        this.geometry = geometry;
        this.material = material;
    }
}

[System.Serializable]
public class RageMaterial
{
    public RageMaterial(string shaderName, string textureName, RageUnityTexture mainTex)
    {
        this.shaderName = shaderName;
        this.textureName = textureName;
        this.mainTex = mainTex;
    }

    public RageUnityTexture mainTex;
    public RageUnityTexture normalTex;
    public RageUnityTexture specularTex;
    public string shaderName;
    public string textureName;
    public string normalTextureName;
    public string specularTextureName;

    // Multi-layer terrain shaders (gta_terrain_va_3lyr / 4lyr) carry their additional
    // diffuse layers here. Index 0 mirrors mainTex / textureName for the base layer; the
    // terrain Unity shaders read indices 1..3 directly.
    public RageUnityTexture[] layerTextures;
    public string[]           layerTextureNames;
}

[System.Serializable]
public class RageUnityTexture
{
    public RageUnityTexture(int width, int height, TextureFormat format, bool mipChain)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        this.mipChain = mipChain;
    }

    public string name;
    public int level;
    public int width;
    public int height;
    public TextureFormat format;
    public TextureType rageTextureType;
    public bool mipChain;
    public RageLib.Textures.Texture textureFile;

    // Cache the materialized Unity Texture2D so subsequent callers receive the SAME
    // instance. Without this, every GetUnityTexture() call allocated a fresh Texture2D
    // and uploaded pixels to the GPU again — which broke any caller that deduped
    // materials by Texture2D reference (each submesh got a unique Texture2D → unique
    // MaterialKey → unique Material → unique BatchMaterialID, exhausting Unity's BRG
    // material pool and silently dropping meshes from the render).
    private Texture2D cached;

    public Texture2D GetUnityTexture()
    {
        if (cached != null) return cached;

        var texture = new Texture2D(width, height, format, mipChain);
        texture.name = name;

        if (textureFile != null)
        {
            var native = textureFile.GetTextureDataNative(level, Allocator.TempJob);

            if (rageTextureType == TextureType.DXT3)
                TextureDecoder.ConvertDXT3ToDXT5(native);

            texture.LoadRawTextureData(native);
            native.Dispose();
        }

        texture.Apply(false, true);
        cached = texture;
        return texture;
    }
}
