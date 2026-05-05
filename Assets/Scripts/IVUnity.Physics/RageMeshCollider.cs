using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace IVUnity.Physics
{
    [BurstCompile]
    public static unsafe class RageMeshCollider
    {
        private static readonly int NumKeyBitsOffset;

        static RageMeshCollider()
        {
            var field = typeof(MeshCollider).GetField("<NumColliderKeyBits>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);

            NumKeyBitsOffset = UnsafeUtility.GetFieldOffset(field);
        }

        internal struct IndexedPrimitive
        {
            public float3x4 Vertices;
            public int4 Indices;
            public MeshConnectivityBuilder.PrimitiveFlags Flags;
            public int VertexCount;
        }

        public static BlobAssetReference<Collider> Create(
            NativeArray<float3> vertices,
            NativeArray<int3> triangles,
            NativeArray<int4> quads,
            CollisionFilter filter = default,
            Material material = default)
        {
            if (vertices.Length == 0) return default;

            if (filter.Equals(default)) filter = CollisionFilter.Default;
            if (material.Equals(default)) material = Material.Default;

            int primitiveCount = triangles.Length + quads.Length;
            if (primitiveCount == 0) return default;

            var indexed = new NativeArray<IndexedPrimitive>(primitiveCount, Allocator.Temp);
            BuildIndexedPrimitives(ref vertices, ref triangles, ref quads, ref indexed);

            var aabbs = new NativeArray<Aabb>(primitiveCount, Allocator.Temp);
            ComputeAabbs(ref indexed, ref aabbs, primitiveCount);

            // LBVH build (O(N) via Morton codes + radix sort)
            int nodeCapacity = math.max(primitiveCount * 2 + 1, 2);
            var nodes = new NativeArray<BoundingVolumeHierarchy.Node>(nodeCapacity, Allocator.Temp);
            LinearBvhBuilder.Build(
                (BoundingVolumeHierarchy.Node*)nodes.GetUnsafePtr(),
                ref aabbs, primitiveCount, out int numNodes);

            // Fast section build
            var sectionFlags = new NativeList<Mesh.PrimitiveFlags>(primitiveCount, Allocator.Temp);
            var sectionPrims = new NativeList<Mesh.PrimitiveVertexIndices>(primitiveCount, Allocator.Temp);
            var sectionVerts = new NativeList<float3>(primitiveCount, Allocator.Temp);
            var sectionRanges = new NativeList<MeshBuilder.TempSectionRanges>(16, Allocator.Temp);

            BuildSectionsFast(
                (BoundingVolumeHierarchy.Node*)nodes.GetUnsafePtr(), numNodes,
                ref indexed, ref vertices,
                ref sectionFlags, ref sectionPrims, ref sectionVerts, ref sectionRanges);

            var sections = new MeshBuilder.TempSection
            {
                PrimitivesFlags = sectionFlags,
                Primitives = sectionPrims,
                Vertices = sectionVerts,
                Ranges = sectionRanges
            };

            // Collider blob
            int meshDataSize = Mesh.CalculateMeshDataSize(numNodes, sections.Ranges);
            int headerSize = UnsafeUtility.SizeOf<MeshCollider>();
            int totalSize = ((headerSize + 15) & ~15) + meshDataSize;

            byte* mem = (byte*)UnsafeUtility.Malloc(totalSize, 16, Allocator.Temp);
            UnsafeUtility.MemClear(mem, totalSize);

            var mc = (MeshCollider*)mem;
            mc->m_Header.Type = ColliderType.Mesh;
            mc->m_Header.CollisionType = CollisionType.Composite;
            mc->m_Header.Version = 0;
            mc->m_Header.Magic = 0xff;
            mc->m_Header.ForceUniqueBlobID = ~ColliderConstants.k_SharedBlobID;
            mc->m_Header.Filter = filter;

            ref var mesh = ref mc->Mesh;
            mesh.Init((BoundingVolumeHierarchy.Node*)nodes.GetUnsafePtr(), numNodes, sections, filter, material);
            mesh.UpdateCachedBoundingRadius();

            int aabbOffset = UnsafeUtility.SizeOf<ColliderHeader>();
            Aabb domain = mesh.BoundingVolumeHierarchy.Domain;
            UnsafeUtility.CopyStructureToPtr(ref domain, mem + aabbOffset);

            *(int*)(mem + aabbOffset + UnsafeUtility.SizeOf<Aabb>()) = totalSize;
            *(uint*)(mem + NumKeyBitsOffset) = mesh.NumColliderKeyBits;

            var blob = BlobAssetReference<Collider>.Create(mc, totalSize);

            indexed.Dispose();
            aabbs.Dispose();
            nodes.Dispose();
            UnsafeUtility.Free(mem, Allocator.Temp);

            return blob;
        }

        [BurstCompile]
        private static void BuildIndexedPrimitives(
            ref NativeArray<float3> vertices,
            ref NativeArray<int3> triangles,
            ref NativeArray<int4> quads,
            ref NativeArray<IndexedPrimitive> indexed)
        {
            int pi = 0;
            for (int i = 0; i < triangles.Length; i++)
            {
                var tri = triangles[i];
                indexed[pi++] = new IndexedPrimitive
                {
                    Vertices = new float3x4(vertices[tri.x], vertices[tri.y], vertices[tri.z], vertices[tri.z]),
                    Indices = new int4(tri.x, tri.y, tri.z, tri.z),
                    Flags = MeshConnectivityBuilder.PrimitiveFlags.DefaultTriangleFlags,
                    VertexCount = 3
                };
            }

            for (int i = 0; i < quads.Length; i++)
            {
                var q = quads[i];
                indexed[pi++] = new IndexedPrimitive
                {
                    Vertices = new float3x4(vertices[q.x], vertices[q.y], vertices[q.z], vertices[q.w]),
                    Indices = new int4(q.x, q.y, q.z, q.w),
                    Flags = MeshConnectivityBuilder.PrimitiveFlags.IsFlatConvexQuad,
                    VertexCount = 4
                };
            }
        }

        [BurstCompile]
        private static void ComputeAabbs(
            ref NativeArray<IndexedPrimitive> indexed,
            ref NativeArray<Aabb> aabbs,
            int count)
        {
            for (int i = 0; i < count; i++)
                aabbs[i] = Aabb.CreateFromPoints(indexed[i].Vertices);
        }

        [BurstCompile]
        private static void BuildSectionsFast(
            BoundingVolumeHierarchy.Node* nodes, int nodeCount,
            ref NativeArray<IndexedPrimitive> primitives,
            ref NativeArray<float3> allVertices,
            ref NativeList<Mesh.PrimitiveFlags> outFlags,
            ref NativeList<Mesh.PrimitiveVertexIndices> outPrims,
            ref NativeList<float3> outVerts,
            ref NativeList<MeshBuilder.TempSectionRanges> outRanges)
        {
            if (primitives.Length == 0) return;

            // Collect leaf primitives in BVH traversal order
            var leafPrimIndices = new NativeList<int>(primitives.Length, Allocator.Temp);
            var leafNodeSlots = new NativeList<int2>(primitives.Length, Allocator.Temp);

            var stack = new NativeArray<int>(BoundingVolumeHierarchy.Constants.UnaryStackSize, Allocator.Temp);
            int stackSize = 1;
            stack[0] = 1;

            while (stackSize > 0)
            {
                int ni = stack[--stackSize];
                var node = nodes[ni];

                if (node.IsLeaf)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (node.IsChildValid(i))
                        {
                            leafPrimIndices.Add(node.Data[i]);
                            leafNodeSlots.Add(new int2(ni, i));
                        }
                    }
                }
                else
                {
                    for (int i = 3; i >= 0; i--)
                    {
                        if (node.IsChildValid(i))
                            stack[stackSize++] = node.Data[i];
                    }
                }
            }

            stack.Dispose();

            // Group into sections (max 256 unique verts per section)
            var vertRemap = new NativeHashMap<int, byte>(256, Allocator.Temp);
            int cursor = 0;
            int sectionIdx = 0;

            while (cursor < leafPrimIndices.Length)
            {
                vertRemap.Clear();
                int nextByte = 0;

                int rangeVMin = outVerts.Length;
                int rangePMin = outPrims.Length;
                int rangeFMin = outFlags.Length;

                int end = cursor;
                while (end < leafPrimIndices.Length)
                {
                    var prim = primitives[leafPrimIndices[end]];

                    int newVerts = 0;
                    for (int v = 0; v < prim.VertexCount; v++)
                    {
                        if (!vertRemap.ContainsKey(prim.Indices[v]))
                            newVerts++;
                    }

                    if (nextByte + newVerts > 256) break;

                    var vi = new Mesh.PrimitiveVertexIndices();
                    byte* viPtr = &vi.A;

                    for (int v = 0; v < prim.VertexCount; v++)
                    {
                        int origIdx = prim.Indices[v];
                        if (!vertRemap.TryGetValue(origIdx, out byte bIdx))
                        {
                            bIdx = (byte)nextByte++;
                            vertRemap.Add(origIdx, bIdx);
                            outVerts.Add(allVertices[origIdx]);
                        }
                        viPtr[v] = bIdx;
                    }
                    if (prim.VertexCount == 3) vi.D = vi.C;

                    Mesh.PrimitiveFlags mf = (prim.Flags & MeshConnectivityBuilder.PrimitiveFlags.IsTrianglePair) != 0
                        ? Mesh.PrimitiveFlags.IsTrianglePair
                        : Mesh.PrimitiveFlags.IsTriangle;
                    if ((prim.Flags & MeshConnectivityBuilder.PrimitiveFlags.IsFlatConvexQuad) == MeshConnectivityBuilder.PrimitiveFlags.IsFlatConvexQuad)
                        mf |= Mesh.PrimitiveFlags.IsQuad;

                    outFlags.Add(mf);
                    outPrims.Add(vi);

                    int primInSection = outPrims.Length - rangePMin - 1;
                    var slot = leafNodeSlots[end];
                    int* dataPtr = (int*)&nodes[slot.x].Data;
                    dataPtr[slot.y] = (sectionIdx << 8) | primInSection;

                    end++;
                }

                outRanges.Add(new MeshBuilder.TempSectionRanges
                {
                    VerticesMin = rangeVMin,
                    VerticesLength = outVerts.Length - rangeVMin,
                    PrimitivesMin = rangePMin,
                    PrimitivesLength = outPrims.Length - rangePMin,
                    PrimitivesFlagsMin = rangeFMin,
                    PrimitivesFlagsLength = outFlags.Length - rangeFMin,
                });

                sectionIdx++;
                cursor = end;
            }

            leafPrimIndices.Dispose();
            leafNodeSlots.Dispose();
            vertRemap.Dispose();
        }
    }
}
