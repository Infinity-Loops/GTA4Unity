using System;
using System.Collections.Generic;
using System.IO;
using RageLib.FileSystem;
using RageLib.Textures;
using UnityEngine;
using UnityEngine.Rendering;
using Directory = RageLib.FileSystem.Common.Directory;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity
{
    /// <summary>
    /// Builds the world water mesh as a single GameObject.
    /// Vertices shared by adjacent water planes (matching position AND per-vertex attributes)
    /// are deduplicated, cutting vertex count by ~50-75% on typical grid layouts. Triangle
    /// count is unchanged. UVs are position-based so shared corners agree on UV — and the
    /// water texture flows continuously across the whole surface.
    /// </summary>
    public static class WaterBuilder
    {
        // World-space UV scale. Matches HighPerformanceLoader.CreateWater's value (0.05),
        // chosen so a typical 10m water plane covers half a texture tile.
        private const float UvScale = 0.05f;

        // Vertex equality precision: positions/attributes are quantized to these factors before
        // being keyed. 100f for position = 1cm precision (well below visible float drift between
        // adjacent quad corners); 1000f for attributes = 0.001 unit precision.
        private const float PositionQuantize  = 100f;
        private const float AttributeQuantize = 1000f;

        public static GameObject Build(List<Water> waterPlanes, RealFileSystem fs, Transform parent)
        {
            Debug.Log("Creating Water...");

            GameObject waterModel = new GameObject("Water");
            waterModel.transform.parent = parent;
            waterModel.transform.localScale = Vector3.one;

            var renderer = waterModel.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var filter = waterModel.AddComponent<MeshFilter>();

            Mesh waterMesh = new Mesh();
            waterMesh.indexFormat = IndexFormat.UInt32; // dedup still leaves potentially > 65k verts

            var vertexPoints = new List<Vector3>();
            var triangles    = new List<int>();
            var uvs          = new List<Vector2>();
            var dedup        = new Dictionary<VertexKey, int>(capacity: 16384);

            // Coordinate convention: rotate -90° X (GTA Z-up → Unity Y-up), then negate X to
            // mirror onto the same axis as the rest of the world. Triangle winding flips
            // because of the X negation — emit (0,1,2)+(1,3,2) instead of legacy (0,2,1)+(1,2,3).
            var fix = Quaternion.Euler(-90f, 0f, 0f);

            foreach (Water water in waterPlanes)
            {
                foreach (var plane in water.planes)
                {
                    int i0 = AddOrShareVertex(plane.points[0], fix, vertexPoints, uvs, dedup);
                    int i1 = AddOrShareVertex(plane.points[1], fix, vertexPoints, uvs, dedup);
                    int i2 = AddOrShareVertex(plane.points[2], fix, vertexPoints, uvs, dedup);
                    int i3 = AddOrShareVertex(plane.points[3], fix, vertexPoints, uvs, dedup);

                    triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                    triangles.Add(i1); triangles.Add(i3); triangles.Add(i2);
                }
            }

            waterMesh.SetVertices(vertexPoints);
            waterMesh.SetUVs(0, uvs);
            waterMesh.SetTriangles(triangles, 0);
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();

            filter.sharedMesh = waterMesh;

            int sharedFraction = vertexPoints.Count == 0
                ? 0
                : 100 - (vertexPoints.Count * 100 / Mathf.Max(1, dedup.Count == 0 ? 1 : (triangles.Count / 6) * 4));
            Debug.Log($"[Water] Built mesh: {vertexPoints.Count} unique verts, {triangles.Count / 3} tris " +
                      $"({(triangles.Count / 6) * 4} non-shared corner reads → ~{sharedFraction}% reuse)");

            ApplyWaterMaterial(renderer, fs);
            return waterModel;
        }

        /// <summary>
        /// Apply the world-coord fix to <paramref name="point"/>, then either return the
        /// existing vertex index for an identical (position + attributes) entry, or append
        /// a new vertex/UV entry and remember it.
        /// </summary>
        private static int AddOrShareVertex(
            Water.WaterPoint point,
            Quaternion fix,
            List<Vector3> verts,
            List<Vector2> uvs,
            Dictionary<VertexKey, int> dedup)
        {
            Vector3 p = fix * point.coord;
            p.x = -p.x;

            var key = new VertexKey(p, point.speedX, point.speedY, point.unknown, point.waveHeight);
            if (dedup.TryGetValue(key, out int existing)) return existing;

            int idx = verts.Count;
            verts.Add(p);
            uvs.Add(new Vector2(p.x * UvScale, p.z * UvScale)); // position-based UV (continuous across planes)
            dedup.Add(key, idx);
            return idx;
        }

        private static void ApplyWaterMaterial(MeshRenderer renderer, RealFileSystem fs)
        {
            Debug.Log("Loading Water Textures...");

            try
            {
                Directory pcDirectory = (Directory)fs.RootDirectory.FindByName("pc");
                Directory texturesDirectory = (Directory)pcDirectory.FindByName("textures");
                File waterTextureIndex = (File)texturesDirectory.FindByName("water.wtd");

                byte[] waterTextureData = waterTextureIndex.GetData();
                using MemoryStream waterTextureStream = new MemoryStream(waterTextureData);
                TextureFile waterTexture = new TextureFile();
                waterTexture.Open(waterTextureStream);
                var waterImage = waterTexture.Textures[0].Decode();

                var unityMaterial = new Material(Shader.Find("water"));
                unityMaterial.SetTexture("_MainTex", waterImage.GetUnityTexture());
                renderer.material = unityMaterial;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Water] Texture load failed, using shader-default material: {ex.Message}");
                var fallback = Shader.Find("water");
                if (fallback != null) renderer.material = new Material(fallback);
            }
        }

        /// <summary>
        /// Equality key for vertex deduplication. Two corners merge only if their position
        /// AND every per-vertex water attribute match (after small-tolerance quantization).
        /// Different wave heights / speeds / unknowns mean the planes carry different per-vertex
        /// shader inputs and must stay separate, so we don't silently lose that data.
        /// </summary>
        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            private readonly int px, py, pz;
            private readonly int sx, sy, unk, wh;

            public VertexKey(Vector3 p, float speedX, float speedY, float unknown, float waveHeight)
            {
                px  = Mathf.RoundToInt(p.x * PositionQuantize);
                py  = Mathf.RoundToInt(p.y * PositionQuantize);
                pz  = Mathf.RoundToInt(p.z * PositionQuantize);
                sx  = Mathf.RoundToInt(speedX     * AttributeQuantize);
                sy  = Mathf.RoundToInt(speedY     * AttributeQuantize);
                unk = Mathf.RoundToInt(unknown    * AttributeQuantize);
                wh  = Mathf.RoundToInt(waveHeight * AttributeQuantize);
            }

            public bool Equals(VertexKey o)
                => px == o.px && py == o.py && pz == o.pz
                && sx == o.sx && sy == o.sy && unk == o.unk && wh == o.wh;

            public override bool Equals(object obj) => obj is VertexKey o && Equals(o);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = px;
                    h = h * 397 ^ py;
                    h = h * 397 ^ pz;
                    h = h * 397 ^ sx;
                    h = h * 397 ^ sy;
                    h = h * 397 ^ unk;
                    h = h * 397 ^ wh;
                    return h;
                }
            }
        }
    }
}
