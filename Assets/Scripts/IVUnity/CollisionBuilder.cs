using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RageLib.Collision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace IVUnity
{
    public static class CollisionBuilder
    {
        private struct ParsedWbn
        {
            public float3[] Vertices;
            public int3[] Triangles;
            public int4[] Quads;
        }

        public static void StartBuild(MonoBehaviour host, GTADatLoader loader, Transform parent)
        {
            host.StartCoroutine(BuildCoroutine(loader));
        }

        private static IEnumerator BuildCoroutine(GTADatLoader loader)
        {
            var wbnFiles = new List<RageLib.FileSystem.Common.File>();
            foreach (var kv in loader.gameFiles)
            {
                if (kv.Key.EndsWith(".wbn"))
                    wbnFiles.Add(kv.Value);
            }

            Debug.Log($"[CollisionBuilder] Parsing {wbnFiles.Count} .wbn files...");

            // Phase 1: parse all .wbn on worker thread
            List<ParsedWbn> parsed = null;
            bool parseDone = false;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                parsed = ParseAll(wbnFiles);
                parseDone = true;
            });

            while (!parseDone)
                yield return null;

            Debug.Log($"[CollisionBuilder] Parsed {parsed.Count} collision meshes. Building ECS colliders...");

            // Phase 2: create ECS collider entities using RageMeshCollider (fast path)
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) yield break;
            var em = world.EntityManager;

            int created = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < parsed.Count; i++)
            {
                var p = parsed[i];
                if (p.Vertices == null || p.Vertices.Length == 0) continue;

                var verts = new NativeArray<float3>(p.Vertices, Allocator.Temp);
                var tris = new NativeArray<int3>(p.Triangles, Allocator.Temp);
                var quads = new NativeArray<int4>(p.Quads, Allocator.Temp);

                var blob = IVUnity.Physics.RageMeshCollider.Create(verts, tris, quads);

                verts.Dispose();
                tris.Dispose();
                quads.Dispose();

                if (blob.IsCreated)
                {
                    var entity = em.CreateEntity(
                        typeof(LocalTransform),
                        typeof(LocalToWorld),
                        typeof(PhysicsCollider),
                        typeof(PhysicsWorldIndex));

                    em.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
                    em.SetComponentData(entity, new PhysicsCollider { Value = blob });
                    em.SetSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
                    created++;
                }

                if (sw.ElapsedMilliseconds > 12)
                {
                    sw.Restart();
                    yield return null;
                }
            }

            Debug.Log($"[CollisionBuilder] Done: {created} ECS colliders from {parsed.Count} files");
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

                    // Merge all geometries per .wbn, separating tris and quads
                    int totalVerts = 0, totalTris = 0, totalQuads = 0;
                    foreach (var geom in collisionFile.Geometries)
                    {
                        if (geom.Vertices == null || geom.Polygons == null) continue;
                        totalVerts += geom.Vertices.Length;
                        for (int i = 0; i < geom.Polygons.Length; i++)
                        {
                            if (geom.Polygons[i].IsQuad) totalQuads++;
                            else totalTris++;
                        }
                    }

                    if (totalVerts == 0) continue;

                    var vertices = new float3[totalVerts];
                    var triangles = new int3[totalTris];
                    var quads = new int4[totalQuads];
                    int vi = 0, ti = 0, qi = 0;

                    foreach (var geom in collisionFile.Geometries)
                    {
                        if (geom.Vertices == null || geom.Polygons == null) continue;
                        int baseVert = vi;

                        for (int i = 0; i < geom.Vertices.Length; i++)
                            vertices[vi++] = new float3(geom.Vertices[i].x, geom.Vertices[i].y, geom.Vertices[i].z);

                        for (int i = 0; i < geom.Polygons.Length; i++)
                        {
                            var poly = geom.Polygons[i];
                            int v0 = baseVert + poly.GetVertexIndex(0);
                            int v1 = baseVert + poly.GetVertexIndex(1);
                            int v2 = baseVert + poly.GetVertexIndex(2);

                            if (v0 >= totalVerts || v1 >= totalVerts || v2 >= totalVerts) continue;

                            if (poly.IsQuad)
                            {
                                int v3 = baseVert + poly.GetVertexIndex(3);
                                if (v3 >= totalVerts) continue;
                                // Winding: v0, v2, v1, v3 for RH->LH
                                quads[qi++] = new int4(v0, v2, v1, v3);
                            }
                            else
                            {
                                // Winding: v0, v2, v1 for RH->LH
                                triangles[ti++] = new int3(v0, v2, v1);
                            }
                        }
                    }

                    if (ti < totalTris) System.Array.Resize(ref triangles, ti);
                    if (qi < totalQuads) System.Array.Resize(ref quads, qi);

                    result.Add(new ParsedWbn { Vertices = vertices, Triangles = triangles, Quads = quads });
                }
                catch { }
            }

            return result;
        }
    }
}
