using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RageLib.Collision;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity
{
    public static class CollisionBuilder
    {
        private struct ParsedWbn
        {
            public Vector3[] Vertices;
            public PolygonData[] Polygons;
        }

        public static void StartBuild(MonoBehaviour host, GTADatLoader loader, Transform parent)
        {
            host.StartCoroutine(BuildCoroutine(loader, parent));
        }

        private static IEnumerator BuildCoroutine(GTADatLoader loader, Transform parent)
        {
            var wbnFiles = new List<RageLib.FileSystem.Common.File>();
            foreach (var kv in loader.gameFiles)
            {
                if (kv.Key.EndsWith(".wbn"))
                    wbnFiles.Add(kv.Value);
            }

            Debug.Log($"[CollisionBuilder] Parsing {wbnFiles.Count} .wbn files...");

            // Phase 1: parse all .wbn on a worker thread (managed IO, can't Burst)
            List<ParsedWbn> parsed = null;
            bool parseDone = false;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                parsed = ParseAll(wbnFiles);
                parseDone = true;
            });

            while (!parseDone)
                yield return null;

            Debug.Log($"[CollisionBuilder] Parsed {parsed.Count} collision meshes. Building with Burst...");

            // Phase 2: Burst jobs to build mesh arrays, then bake in parallel
            var go = new GameObject("WorldCollision");
            go.transform.SetParent(parent, false);
            go.isStatic = true;

            var meshes = new List<Mesh>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < parsed.Count; i++)
            {
                var p = parsed[i];
                if (p.Vertices == null || p.Vertices.Length == 0) continue;

                var srcVerts = new NativeArray<float3>(p.Vertices.Length, Allocator.TempJob);
                for (int v = 0; v < p.Vertices.Length; v++)
                    srcVerts[v] = new float3(p.Vertices[v].x, p.Vertices[v].y, p.Vertices[v].z);

                var polyData = new NativeArray<PolygonData>(p.Polygons.Length, Allocator.TempJob);
                int triCount = 0;
                for (int pi = 0; pi < p.Polygons.Length; pi++)
                {
                    polyData[pi] = p.Polygons[pi];
                    triCount += p.Polygons[pi].IsQuad ? 2 : 1;
                }

                var outTris = new NativeList<int3>(triCount, Allocator.TempJob);

                // Schedule Burst job
                var job = new BuildTrianglesJob
                {
                    Polygons = polyData,
                    NumVertices = p.Vertices.Length,
                    Triangles = outTris
                };
                job.Schedule().Complete();

                // Create mesh
                var mesh = new Mesh();
                if (srcVerts.Length > 65535)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.SetVertices(srcVerts);
                mesh.SetIndices(outTris.AsArray().Reinterpret<int>(12), MeshTopology.Triangles, 0);
                meshes.Add(mesh);

                srcVerts.Dispose();
                polyData.Dispose();
                outTris.Dispose();

                if (sw.ElapsedMilliseconds > 8)
                {
                    sw.Restart();
                    yield return null;
                }
            }

            // Free parsed data
            parsed = null;

            Debug.Log($"[CollisionBuilder] Built {meshes.Count} meshes. Baking PhysX BVH...");

            // Phase 3: bake on worker threads in parallel batches
            const int bakeBatch = 32;
            for (int batch = 0; batch < meshes.Count; batch += bakeBatch)
            {
                int end = Mathf.Min(batch + bakeBatch, meshes.Count);
                int remaining = end - batch;
                int done = 0;

                for (int i = batch; i < end; i++)
                {
                    int meshId = meshes[i].GetInstanceID();
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Physics.BakeMesh(meshId, false);
                        Interlocked.Increment(ref done);
                    });
                }

                while (done < remaining)
                    yield return null;
            }

            // Phase 4: attach all MeshColliders to single GameObject
            for (int i = 0; i < meshes.Count; i++)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = meshes[i];
            }

            Debug.Log($"[CollisionBuilder] World collision ready: {meshes.Count} colliders");
        }

        private static List<ParsedWbn> ParseAll(List<RageLib.FileSystem.Common.File> files)
        {
            var result = new List<ParsedWbn>();

            foreach (var file in files)
            {
                try
                {
                    byte[] data = file.GetData();
                    var collisionFile = new CollisionFile();
                    using (var stream = new MemoryStream(data))
                        collisionFile.Open(stream);

                    if (collisionFile.Geometries == null || collisionFile.Geometries.Length == 0)
                        continue;

                    // Merge all geometries in this .wbn into one vertex/polygon set
                    int totalVerts = 0, totalPolys = 0;
                    foreach (var geom in collisionFile.Geometries)
                    {
                        if (geom.Vertices == null || geom.Polygons == null) continue;
                        totalVerts += geom.Vertices.Length;
                        totalPolys += geom.Polygons.Length;
                    }

                    if (totalVerts == 0) continue;

                    var vertices = new Vector3[totalVerts];
                    var polygons = new PolygonData[totalPolys];
                    int vi = 0, pi = 0;

                    foreach (var geom in collisionFile.Geometries)
                    {
                        if (geom.Vertices == null || geom.Polygons == null) continue;
                        int baseVert = vi;

                        System.Array.Copy(geom.Vertices, 0, vertices, vi, geom.Vertices.Length);
                        vi += geom.Vertices.Length;

                        for (int i = 0; i < geom.Polygons.Length; i++)
                        {
                            var poly = geom.Polygons[i];
                            polygons[pi++] = new PolygonData
                            {
                                V0 = baseVert + poly.GetVertexIndex(0),
                                V1 = baseVert + poly.GetVertexIndex(1),
                                V2 = baseVert + poly.GetVertexIndex(2),
                                V3 = baseVert + poly.GetVertexIndex(3),
                                IsQuad = poly.IsQuad
                            };
                        }
                    }

                    result.Add(new ParsedWbn { Vertices = vertices, Polygons = polygons });
                }
                catch { }
            }

            return result;
        }

        private struct PolygonData
        {
            public int V0, V1, V2, V3;
            public bool IsQuad;
        }

        [BurstCompile]
        private struct BuildTrianglesJob : IJob
        {
            [ReadOnly] public NativeArray<PolygonData> Polygons;
            public int NumVertices;
            public NativeList<int3> Triangles;

            public void Execute()
            {
                for (int i = 0; i < Polygons.Length; i++)
                {
                    var p = Polygons[i];

                    if (p.V0 >= NumVertices || p.V1 >= NumVertices || p.V2 >= NumVertices)
                        continue;

                    // RH->LH winding: v0, v2, v1
                    Triangles.Add(new int3(p.V0, p.V2, p.V1));

                    if (p.IsQuad && p.V3 < NumVertices)
                        Triangles.Add(new int3(p.V0, p.V3, p.V2));
                }
            }
        }
    }
}
