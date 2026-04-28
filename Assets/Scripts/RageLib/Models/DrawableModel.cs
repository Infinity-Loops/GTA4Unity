using System.Collections;
using System.Collections.Generic;
using RageLib.Textures;
using RageLib.Textures.Decoder;
using RageLib.Textures.Resource;
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
    public Vector3[] positions;
    public Vector3[] normals;
    public Vector2[] textureCoordinates;
    public Color32[] colors;
    public int[] triangleIndices;

    public Mesh GetUnityMesh()
    {
        var mesh = new Mesh();
        if (positions != null) mesh.SetVertices(positions);
        if (normals != null && normals.Length == positions.Length) mesh.SetNormals(normals);
        if (textureCoordinates != null && textureCoordinates.Length == positions.Length) mesh.SetUVs(0, textureCoordinates);
        if (colors != null && colors.Length == positions.Length) mesh.SetColors(colors);
        if (triangleIndices != null) mesh.SetIndices(triangleIndices, MeshTopology.Triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
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
            byte[] pixels = textureFile.GetTextureData(level);
            if (rageTextureType == TextureType.DXT3)
            {
                pixels = TextureDecoder.ConvertDXT3ToDXT5(pixels);
            }
            texture.LoadRawTextureData(pixels);
        }

        texture.Apply(false, true);
        cached = texture;
        return texture;
    }
}
