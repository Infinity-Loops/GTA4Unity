using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class Model3DGroup : GeometryModel3D
{
    public List<GeometryModel3D> Children = new List<GeometryModel3D>();
}

public class MeshGeometry3D
{
    public List<Vector3> positions = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Vector2> textureCoordinates = new List<Vector2>();
    // Per-vertex diffuse color from the WDR. Empty for meshes without a Color element in
    // their VertexDeclaration. Read by terrain shaders as layer-blend weights.
    public List<Color32> colors = new List<Color32>();
    public List<int> triangleIndices = new List<int>();

    public Mesh GetUnityMesh()
    {
        var mesh = new Mesh();

        NativeArray<Vector3> nativePositions = new NativeArray<Vector3>(positions.ToArray(), Allocator.TempJob);
        NativeArray<Vector3> nativeNormals = new NativeArray<Vector3>(normals.ToArray(), Allocator.TempJob);
        NativeArray<Vector2> nativeUVs = new NativeArray<Vector2>(textureCoordinates.ToArray(), Allocator.TempJob);

        mesh.SetVertices(nativePositions);
        mesh.SetNormals(nativeNormals);
        mesh.SetUVs(0, nativeUVs);
        if (colors.Count == positions.Count) mesh.SetColors(colors);
        mesh.triangles = triangleIndices.ToArray();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);

        nativePositions.Dispose();
        nativeNormals.Dispose();
        nativeUVs.Dispose();

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
    public int width;
    public int height;
    public TextureFormat format;
    public bool mipChain;
    public byte[] pixels;

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

        if (pixels != null && pixels.Length > 0)
        {
            texture.LoadRawTextureData(pixels);
        }

        texture.Apply(false, false);
        cached = texture;
        return texture;
    }
}
