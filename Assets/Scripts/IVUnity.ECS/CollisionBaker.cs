using System.Collections.Concurrent;
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
using MeshCollider = Unity.Physics.MeshCollider;
using SphereCollider = Unity.Physics.SphereCollider;
using CapsuleCollider = Unity.Physics.CapsuleCollider;

namespace IVUnity.ECS
{
    public struct CollisionEntry
    {
        public string Name;
        public RageLib.FileSystem.Common.File File;
        public float3 Center;
        public float Radius;
        public enum State { Idle, Loading, Ready, Active }
        public State CurrentState;
        public Entity Entity;
    }

    public class CollisionBaker : MonoBehaviour
    {
        private static List<CollisionEntry> entries;
        private static readonly ConcurrentQueue<CollisionResult> results = new();
        private static float3 lastLoadPos;
        private static bool initialized;

        private struct CollisionResult
        {
            public int Index;
            public NativeArray<float3> Vertices;
            public NativeArray<int3> Triangles;
            public bool Failed;
        }

        public static void Initialize(GTADatLoader loader)
        {
            entries = new List<CollisionEntry>();

            foreach (var kv in loader.gameFiles)
            {
                if (!kv.Key.EndsWith(".wbn")) continue;

                entries.Add(new CollisionEntry
                {
                    Name = kv.Key,
                    File = kv.Value,
                    CurrentState = CollisionEntry.State.Idle,
                });
            }

            lastLoadPos = new float3(float.MaxValue);
            initialized = true;

            Debug.Log($"[CollisionBaker] Indexed {entries.Count} .wbn files for streaming");
        }

        public static void Update(EntityManager em, float3 focusPos)
        {
            if (!initialized) return;

            // Drain completed results from worker threads
            int uploaded = 0;
            while (results.TryDequeue(out var result))
            {
                if (result.Failed || !result.Vertices.IsCreated)
                {
                    if (result.Vertices.IsCreated) result.Vertices.Dispose();
                    if (result.Triangles.IsCreated) result.Triangles.Dispose();
                    var e = entries[result.Index];
                    e.CurrentState = CollisionEntry.State.Idle;
                    entries[result.Index] = e;
                    continue;
                }

                var blob = MeshCollider.Create(result.Vertices, result.Triangles);
                result.Vertices.Dispose();
                result.Triangles.Dispose();

                if (blob.IsCreated)
                {
                    var entry = entries[result.Index];
                    entry.Entity = CreateStaticColliderEntity(em, blob);
                    entry.CurrentState = CollisionEntry.State.Active;
                    entries[result.Index] = entry;
                    uploaded++;
                }

                if (uploaded >= 2) break; // limit per frame
            }

            // Check distance - only scan for new loads periodically
            float moveSq = math.lengthsq(focusPos - lastLoadPos);
            if (moveSq < 100f) return; // moved less than 10 units
            lastLoadPos = focusPos;

            const float loadRadius = 500f;
            const float unloadRadius = 800f;
            const int maxLoading = 4;

            int loading = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].CurrentState == CollisionEntry.State.Loading)
                    loading++;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.CurrentState == CollisionEntry.State.Active)
                {
                    float dist = math.distance(focusPos, entry.Center);
                    if (dist > unloadRadius)
                    {
                        if (entry.Entity != Entity.Null && em.Exists(entry.Entity))
                            em.DestroyEntity(entry.Entity);
                        entry.Entity = Entity.Null;
                        entry.CurrentState = CollisionEntry.State.Idle;
                        entries[i] = entry;
                    }
                }
                else if (entry.CurrentState == CollisionEntry.State.Idle && loading < maxLoading)
                {
                    // For files without known center yet, always try loading
                    // Once loaded, we'll know the center from the bound's bbox
                    entry.CurrentState = CollisionEntry.State.Loading;
                    entries[i] = entry;
                    loading++;

                    int idx = i;
                    var file = entry.File;
                    ThreadPool.QueueUserWorkItem(_ => LoadOnWorker(idx, file));
                }
            }
        }

        private static void LoadOnWorker(int index, RageLib.FileSystem.Common.File file)
        {
            try
            {
                byte[] data = file.GetData();
                var collisionFile = new CollisionFile();
                using (var stream = new MemoryStream(data))
                    collisionFile.Open(stream);

                if (collisionFile.Geometries == null || collisionFile.Geometries.Length == 0)
                {
                    results.Enqueue(new CollisionResult { Index = index, Failed = true });
                    return;
                }

                // Merge all geometries into one mesh
                int totalVerts = 0, totalTris = 0;
                foreach (var geom in collisionFile.Geometries)
                {
                    if (geom.Vertices == null || geom.Polygons == null) continue;
                    totalVerts += geom.Vertices.Length;
                    for (int i = 0; i < geom.Polygons.Length; i++)
                        totalTris += geom.Polygons[i].IsQuad ? 2 : 1;
                }

                if (totalVerts == 0 || totalTris == 0)
                {
                    results.Enqueue(new CollisionResult { Index = index, Failed = true });
                    return;
                }

                var vertices = new NativeArray<float3>(totalVerts, Allocator.Persistent);
                var triangles = new NativeArray<int3>(totalTris, Allocator.Persistent);

                int vi = 0, ti = 0;
                float3 bboxMin = new float3(float.MaxValue);
                float3 bboxMax = new float3(float.MinValue);

                foreach (var geom in collisionFile.Geometries)
                {
                    if (geom.Vertices == null || geom.Polygons == null) continue;
                    int baseVert = vi;

                    for (int i = 0; i < geom.Vertices.Length; i++)
                    {
                        var v = new float3(geom.Vertices[i].x, geom.Vertices[i].y, geom.Vertices[i].z);
                        vertices[vi++] = v;
                        bboxMin = math.min(bboxMin, v);
                        bboxMax = math.max(bboxMax, v);
                    }

                    for (int i = 0; i < geom.Polygons.Length; i++)
                    {
                        int v0 = geom.Polygons[i].GetVertexIndex(0);
                        int v1 = geom.Polygons[i].GetVertexIndex(1);
                        int v2 = geom.Polygons[i].GetVertexIndex(2);
                        if (v0 >= geom.Vertices.Length || v1 >= geom.Vertices.Length || v2 >= geom.Vertices.Length) continue;

                        triangles[ti++] = new int3(baseVert + v0, baseVert + v1, baseVert + v2);

                        if (geom.Polygons[i].IsQuad)
                        {
                            int v3 = geom.Polygons[i].GetVertexIndex(3);
                            if (v3 < geom.Vertices.Length)
                                triangles[ti++] = new int3(baseVert + v0, baseVert + v2, baseVert + v3);
                        }
                    }
                }

                // Store center for distance checks
                var entry = entries[index];
                entry.Center = (bboxMin + bboxMax) * 0.5f;
                entry.Radius = math.length(bboxMax - bboxMin) * 0.5f;
                entries[index] = entry;

                if (ti < totalTris)
                {
                    var trimmed = new NativeArray<int3>(ti, Allocator.Persistent);
                    NativeArray<int3>.Copy(triangles, trimmed, ti);
                    triangles.Dispose();
                    triangles = trimmed;
                }

                results.Enqueue(new CollisionResult
                {
                    Index = index,
                    Vertices = vertices,
                    Triangles = triangles,
                });
            }
            catch
            {
                results.Enqueue(new CollisionResult { Index = index, Failed = true });
            }
        }

        private static Entity CreateStaticColliderEntity(EntityManager em, BlobAssetReference<Collider> collider)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex));

            em.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(entity, new PhysicsCollider { Value = collider });
            em.SetSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
            return entity;
        }
    }
}
