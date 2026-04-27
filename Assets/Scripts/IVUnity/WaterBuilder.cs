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
    /// Engine-faithful water mesh generation.
    ///
    /// The engine (FUN_00ad9130) generates a single uniform world-space grid
    /// for the water surface — NOT per-quad meshes. Water.dat defines coverage
    /// areas and heights; the grid is uniform regardless of quad boundaries.
    ///
    /// Engine layout: 12×12 spatial cell grid (500m cells, 6000m total coverage).
    /// Each cell references water quads for height/wave lookups.
    ///
    /// Our approach: compute AABB of all water planes, generate a uniform grid
    /// at fixed spacing, query each vertex against water planes for height/params.
    /// </summary>
    public static class WaterBuilder
    {
        private const float GridSpacing = 10f;
        private const float UvScale = 0.05f;

        public static GameObject Build(List<Water> waterPlanes, RealFileSystem fs, Transform parent)
        {
            Debug.Log("Creating Water...");

            GameObject waterModel = new GameObject("Water");
            waterModel.transform.parent = parent;
            waterModel.transform.localScale = Vector3.one;

            var renderer = waterModel.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var filter = waterModel.AddComponent<MeshFilter>();

            // Collect all transformed plane data for spatial queries
            var fix = Quaternion.Euler(-90f, 0f, 0f);
            var transformedPlanes = new List<TransformedPlane>();

            foreach (Water water in waterPlanes)
            {
                foreach (var plane in water.planes)
                {
                    var tp = new TransformedPlane();
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = fix * plane.points[i].coord;
                        p.x = -p.x;
                        tp.corners[i] = p;
                        tp.points[i] = plane.points[i];
                    }
                    tp.ComputeBounds();
                    transformedPlanes.Add(tp);
                }
            }

            if (transformedPlanes.Count == 0)
            {
                Debug.LogWarning("[Water] No water planes found");
                filter.sharedMesh = new Mesh();
                ApplyWaterMaterial(renderer, fs);
                return waterModel;
            }

            // Compute global AABB
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var tp in transformedPlanes)
            {
                minX = Mathf.Min(minX, tp.minX);
                maxX = Mathf.Max(maxX, tp.maxX);
                minZ = Mathf.Min(minZ, tp.minZ);
                maxZ = Mathf.Max(maxZ, tp.maxZ);
            }

            // Snap bounds to grid
            minX = Mathf.Floor(minX / GridSpacing) * GridSpacing;
            minZ = Mathf.Floor(minZ / GridSpacing) * GridSpacing;
            maxX = Mathf.Ceil(maxX / GridSpacing) * GridSpacing;
            maxZ = Mathf.Ceil(maxZ / GridSpacing) * GridSpacing;

            int gridW = Mathf.RoundToInt((maxX - minX) / GridSpacing) + 1;
            int gridH = Mathf.RoundToInt((maxZ - minZ) / GridSpacing) + 1;

            // Generate uniform grid — query each vertex against water planes
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var vertexMap = new int[gridW, gridH];

            // Initialize to -1 (no vertex)
            for (int x = 0; x < gridW; x++)
                for (int z = 0; z < gridH; z++)
                    vertexMap[x, z] = -1;

            // Create vertices only where water exists
            for (int gz = 0; gz < gridH; gz++)
            {
                for (int gx = 0; gx < gridW; gx++)
                {
                    float wx = minX + gx * GridSpacing;
                    float wz = minZ + gz * GridSpacing;

                    if (FindWaterAt(wx, wz, transformedPlanes, out float height, out Water.WaterPoint wp))
                    {
                        vertexMap[gx, gz] = verts.Count;
                        verts.Add(new Vector3(wx, height, wz));
                        uvs.Add(new Vector2(wx * UvScale, wz * UvScale));
                        colors.Add(new Color(
                            Mathf.Clamp01(wp.waveHeight / 2f),
                            Mathf.Clamp01((wp.speedX + 1f) * 0.5f),
                            Mathf.Clamp01((wp.speedY + 1f) * 0.5f),
                            1f));
                    }
                }
            }

            // Generate triangles for cells where all 4 corners have water
            for (int gz = 0; gz < gridH - 1; gz++)
            {
                for (int gx = 0; gx < gridW - 1; gx++)
                {
                    int a = vertexMap[gx, gz];
                    int b = vertexMap[gx + 1, gz];
                    int c = vertexMap[gx, gz + 1];
                    int d = vertexMap[gx + 1, gz + 1];

                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;

                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            Mesh waterMesh = new Mesh();
            waterMesh.indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            waterMesh.SetVertices(verts);
            waterMesh.SetUVs(0, uvs);
            waterMesh.SetColors(colors);
            waterMesh.SetTriangles(triangles, 0);
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();

            filter.sharedMesh = waterMesh;

            Debug.Log($"[Water] Built uniform grid: {verts.Count} verts, {triangles.Count / 3} tris " +
                      $"(grid {gridW}×{gridH}, spacing {GridSpacing}m, {transformedPlanes.Count} planes)");

            ApplyWaterMaterial(renderer, fs);
            return waterModel;
        }

        private static bool FindWaterAt(float wx, float wz, List<TransformedPlane> planes,
            out float height, out Water.WaterPoint wp)
        {
            height = 0f;
            wp = null;

            foreach (var tp in planes)
            {
                if (wx < tp.minX || wx > tp.maxX || wz < tp.minZ || wz > tp.maxZ)
                    continue;

                // Water quads are axis-aligned rectangles — AABB check is sufficient
                BilinearInterp(wx, wz, tp, out height, out wp);
                return true;
            }
            return false;
        }

        private static bool PointInQuad(float px, float pz, Vector3[] corners)
        {
            // Simple check using cross products for convex quad
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                float cross = (b.x - a.x) * (pz - a.z) - (b.z - a.z) * (px - a.x);
                if (cross < -0.01f) return false;
            }
            return true;
        }

        private static void BilinearInterp(float wx, float wz, TransformedPlane tp,
            out float height, out Water.WaterPoint wp)
        {
            // Compute UV within the quad's AABB (approximate for non-rectangular quads)
            float u = Mathf.InverseLerp(tp.minX, tp.maxX, wx);
            float v = Mathf.InverseLerp(tp.minZ, tp.maxZ, wz);
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            // Bilinear interpolation of corners: 0=topLeft, 1=topRight, 2=bottomLeft, 3=bottomRight
            height = Mathf.Lerp(
                Mathf.Lerp(tp.corners[0].y, tp.corners[1].y, u),
                Mathf.Lerp(tp.corners[2].y, tp.corners[3].y, u), v);

            var p = tp.points;
            wp = new Water.WaterPoint
            {
                coord = new Vector3(wx, height, wz),
                speedX = Mathf.Lerp(Mathf.Lerp(p[0].speedX, p[1].speedX, u),
                                    Mathf.Lerp(p[2].speedX, p[3].speedX, u), v),
                speedY = Mathf.Lerp(Mathf.Lerp(p[0].speedY, p[1].speedY, u),
                                    Mathf.Lerp(p[2].speedY, p[3].speedY, u), v),
                waveHeight = Mathf.Lerp(Mathf.Lerp(p[0].waveHeight, p[1].waveHeight, u),
                                        Mathf.Lerp(p[2].waveHeight, p[3].waveHeight, u), v),
                unknown = 0f,
            };
        }

        private class TransformedPlane
        {
            public Vector3[] corners = new Vector3[4];
            public Water.WaterPoint[] points = new Water.WaterPoint[4];
            public float minX, maxX, minZ, maxZ;

            public void ComputeBounds()
            {
                minX = maxX = corners[0].x;
                minZ = maxZ = corners[0].z;
                for (int i = 1; i < 4; i++)
                {
                    minX = Mathf.Min(minX, corners[i].x);
                    maxX = Mathf.Max(maxX, corners[i].x);
                    minZ = Mathf.Min(minZ, corners[i].z);
                    maxZ = Mathf.Max(maxZ, corners[i].z);
                }
                // Expand by one grid cell so edge vertices/cells aren't clipped
                minX -= GridSpacing;
                maxX += GridSpacing;
                minZ -= GridSpacing;
                maxZ += GridSpacing;
            }
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

                var unityMaterial = new Material(Shader.Find("GTA IV/water"));
                unityMaterial.SetTexture("_MainTex", waterImage.GetUnityTexture());
                renderer.material = unityMaterial;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Water] Texture load failed, using shader-default material: {ex.Message}");
                var fallback = Shader.Find("GTA IV/water");
                if (fallback != null) renderer.material = new Material(fallback);
            }
        }
    }
}
