using System.Collections.Generic;
using System.IO;
using RageLib.Collision;
using UnityEngine;
using UnityEngine.Rendering;

namespace IVUnity
{
    public static class CollisionDebugRenderer
    {
        public static void RenderAll(GTADatLoader loader, Transform parent)
        {
            var wbnFiles = new List<KeyValuePair<string, RageLib.FileSystem.Common.File>>();
            foreach (var kv in loader.gameFiles)
            {
                if (kv.Key.EndsWith(".wbn"))
                    wbnFiles.Add(kv);
            }

            Debug.Log($"[Collision] Found {wbnFiles.Count} .wbn files");

            int totalVerts = 0, totalTris = 0, loaded = 0, failed = 0, skippedEmpty = 0;
            var typeCounts = new Dictionary<int, int>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.8f, 0.3f, 0.5f);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_AlphaClip", 0);
            mat.SetFloat("_Cull", 0); // double-sided
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)RenderQueue.Transparent;

            foreach (var kv in wbnFiles)
            {
                try
                {
                    byte[] data = kv.Value.GetData();
                    var collisionFile = new CollisionFile();
                    using (var stream = new MemoryStream(data))
                    {
                        collisionFile.Open(stream);
                    }

                    if (collisionFile.Geometries == null || collisionFile.Geometries.Length == 0)
                    {
                        skippedEmpty++;
                        int rootType = collisionFile.RootBoundType;
                        typeCounts.TryGetValue(rootType, out int c);
                        typeCounts[rootType] = c + 1;
                        continue;
                    }

                    foreach (var geom in collisionFile.Geometries)
                    {
                        if (geom.Vertices == null || geom.Vertices.Length == 0) continue;
                        if (geom.Polygons == null || geom.Polygons.Length == 0) continue;

                        var mesh = BuildMesh(geom);
                        if (mesh == null) continue;

                        var go = new GameObject($"col_{kv.Key}");
                        go.transform.SetParent(parent, false);
                        var mf = go.AddComponent<MeshFilter>();
                        var mr = go.AddComponent<MeshRenderer>();
                        mf.sharedMesh = mesh;
                        mr.sharedMaterial = mat;

                        totalVerts += geom.NumVertices;
                        totalTris += geom.NumPolygons;
                    }

                    loaded++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    if (failed <= 5)
                        Debug.LogWarning($"[Collision] Failed to load {kv.Key}: {e.Message}");
                }
            }

            string typeBreakdown = "";
            foreach (var kv2 in typeCounts)
                typeBreakdown += $" type{kv2.Key}={kv2.Value}";

            Debug.Log($"[Collision] Loaded {loaded}/{wbnFiles.Count} files ({failed} failed, {skippedEmpty} empty), " +
                      $"{totalVerts} vertices, {totalTris} triangles. Empty types:{typeBreakdown}");
        }

        private static Mesh BuildMesh(PhBoundGeometry geom)
        {
            var vertices = geom.Vertices;
            int numPolys = geom.NumPolygons;

            var indices = new int[numPolys * 6];
            int idx = 0;
            for (int i = 0; i < numPolys; i++)
            {
                var poly = geom.Polygons[i];
                int v0 = poly.GetVertexIndex(0);
                int v1 = poly.GetVertexIndex(1);
                int v2 = poly.GetVertexIndex(2);

                if (v0 >= vertices.Length || v1 >= vertices.Length || v2 >= vertices.Length)
                    continue;

                // RH->LH winding reversal: swap v1/v2
                indices[idx++] = v0;
                indices[idx++] = v2;
                indices[idx++] = v1;

                if (poly.IsQuad)
                {
                    int v3 = poly.GetVertexIndex(3);
                    if (v3 < vertices.Length)
                    {
                        indices[idx++] = v0;
                        indices[idx++] = v3;
                        indices[idx++] = v2;
                    }
                }
            }

            if (idx == 0) return null;

            var mesh = new Mesh();
            if (vertices.Length > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, 0, idx, MeshTopology.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
