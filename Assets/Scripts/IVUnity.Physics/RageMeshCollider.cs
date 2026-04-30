using System;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace IVUnity.Physics
{
    public static unsafe class RageMeshCollider
    {
        private static readonly int NumKeyBitsOffset;

        static RageMeshCollider()
        {
            var field = typeof(MeshCollider).GetField("<NumColliderKeyBits>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);

            NumKeyBitsOffset = UnsafeUtility.GetFieldOffset(field);
        }

        public static BlobAssetReference<Collider> Create(
            NativeArray<float3> vertices,
            NativeArray<int3> triangles,
            NativeArray<int4> quads,
            CollisionFilter filter = default,
            Material material = default)
        {
            if (vertices.Length == 0)
                return default;

            if (filter.Equals(default)) filter = CollisionFilter.Default;
            if (material.Equals(default)) material = Material.Default;

            int primitiveCount = triangles.Length + quads.Length;
            if (primitiveCount == 0)
                return default;

            var primitives = new NativeArray<MeshConnectivityBuilder.Primitive>(primitiveCount, Allocator.Temp);
            int primIndex = 0;

            // TRIANGLES
            for (int i = 0; i < triangles.Length; i++)
            {
                var tri = triangles[i];

                primitives[primIndex++] = new MeshConnectivityBuilder.Primitive
                {
                    Vertices = new float3x4(
                        vertices[tri.x],
                        vertices[tri.y],
                        vertices[tri.z],
                        vertices[tri.z]),
                    Flags = MeshConnectivityBuilder.PrimitiveFlags.DefaultTriangleFlags
                };
            }

            // QUADS
            for (int i = 0; i < quads.Length; i++)
            {
                var q = quads[i];

                primitives[primIndex++] = new MeshConnectivityBuilder.Primitive
                {
                    Vertices = new float3x4(
                        vertices[q.x],
                        vertices[q.y],
                        vertices[q.z],
                        vertices[q.w]),
                    Flags = MeshConnectivityBuilder.PrimitiveFlags.IsFlatConvexQuad
                };
            }

            int finalCount = primIndex;
            if (finalCount == 0)
            {
                primitives.Dispose();
                return default;
            }

            // AABB + POINTS (single pass)
            var aabbs = new NativeArray<Aabb>(finalCount, Allocator.Temp);
            var points = new NativeArray<BoundingVolumeHierarchy.PointAndIndex>(finalCount, Allocator.Temp);

            for (int i = 0; i < finalCount; i++)
            {
                var aabb = Aabb.CreateFromPoints(primitives[i].Vertices);
                aabbs[i] = aabb;

                points[i] = new BoundingVolumeHierarchy.PointAndIndex
                {
                    Position = aabb.Center,
                    Index = i
                };
            }

            // BVH (SEM SAH)
            int nodeCapacity = math.max(finalCount * 2 + 1, 2);
            var nodes = new NativeArray<BoundingVolumeHierarchy.Node>(nodeCapacity, Allocator.Temp);

            var bvh = new BoundingVolumeHierarchy(nodes);
            bvh.Build(points, aabbs, out int numNodes, useSah: false);

            // SECTIONS
            var primitiveList = new NativeList<MeshConnectivityBuilder.Primitive>(finalCount, Allocator.Temp);
            primitiveList.AddRange(primitives.GetSubArray(0, finalCount));

            MeshBuilder.TempSection sections = MeshBuilder.BuildSections(
                (BoundingVolumeHierarchy.Node*)nodes.GetUnsafePtr(),
                numNodes,
                primitiveList);

            // COLLIDER MEMORY
            int meshDataSize = Mesh.CalculateMeshDataSize(numNodes, sections.Ranges);
            int headerSize = UnsafeUtility.SizeOf<MeshCollider>();
            int alignedHeaderSize = (headerSize + 15) & ~15;
            int totalSize = alignedHeaderSize + meshDataSize;

            byte* mem = (byte*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Temp);
            UnsafeUtility.MemClear(mem, totalSize);

            var mc = (MeshCollider*)mem;

            // HEADER
            mc->m_Header.Type = ColliderType.Mesh;
            mc->m_Header.CollisionType = CollisionType.Composite;
            mc->m_Header.Version = 0;
            mc->m_Header.Magic = 0xff;
            mc->m_Header.ForceUniqueBlobID = ~ColliderConstants.k_SharedBlobID;

            // INIT MESH
            ref var mesh = ref mc->Mesh;
            mesh.Init((BoundingVolumeHierarchy.Node*)nodes.GetUnsafePtr(), numNodes, sections, filter, material);
            mesh.UpdateCachedBoundingRadius();

            // AABB
            int aabbOffset = UnsafeUtility.SizeOf<ColliderHeader>();
            Aabb domain = mesh.BoundingVolumeHierarchy.Domain;
            UnsafeUtility.CopyStructureToPtr(ref domain, mem + aabbOffset);

            // MEMORY SIZE
            int memorySizeOffset = aabbOffset + UnsafeUtility.SizeOf<Aabb>();
            *(int*)(mem + memorySizeOffset) = totalSize;

            // FILTER
            mc->m_Header.Filter = mesh.Sections[0].Filters[0];
            for (int i = 0; i < mesh.Sections.Length; i++)
            {
                var filters = mesh.Sections[i].Filters;
                for (int j = 0; j < filters.Length; j++)
                {
                    mc->m_Header.Filter = CollisionFilter.CreateUnion(mc->m_Header.Filter, filters[j]);
                }
            }

            // KEY BITS
            *(uint*)(mem + NumKeyBitsOffset) = mesh.NumColliderKeyBits;

            var blob = BlobAssetReference<Collider>.Create(mc, totalSize);

            // DISPOSE
            primitives.Dispose();
            aabbs.Dispose();
            points.Dispose();
            nodes.Dispose();

            UnsafeUtility.Free(mem, Allocator.Temp);

            return blob;
        }
    }
}