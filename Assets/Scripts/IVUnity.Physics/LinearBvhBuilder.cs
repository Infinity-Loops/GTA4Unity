using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;

namespace IVUnity.Physics
{
    [BurstCompile]
    internal static unsafe class LinearBvhBuilder
    {
        [BurstCompile]
        public static void Build(
            BoundingVolumeHierarchy.Node* nodes,
            ref NativeArray<Aabb> aabbs,
            int count,
            out int nodeCount)
        {
            nodes[0] = BoundingVolumeHierarchy.Node.Empty;

            if (count == 0)
            {
                nodes[1] = BoundingVolumeHierarchy.Node.Empty;
                nodeCount = 2;
                return;
            }

            // Domain AABB
            Aabb domain = aabbs[0];
            for (int i = 1; i < count; i++)
            {
                domain.Min = math.min(domain.Min, aabbs[i].Min);
                domain.Max = math.max(domain.Max, aabbs[i].Max);
            }

            // Morton codes packed with original index
            float3 extent = domain.Max - domain.Min;
            float3 invExtent = math.select(1f / extent, float3.zero, extent < new float3(1e-10f));

            var mortonPairs = new NativeArray<ulong>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
            {
                float3 norm = (aabbs[i].Center - domain.Min) * invExtent;
                norm = math.clamp(norm, 0f, 1f);
                uint3 q = (uint3)(norm * 1023f);
                uint code = EncodeMorton(q);
                mortonPairs[i] = ((ulong)code << 32) | (uint)i;
            }

            RadixSort(ref mortonPairs, count);

            // Extract sorted primitive indices
            var sortedIndices = new NativeArray<int>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
                sortedIndices[i] = (int)(mortonPairs[i] & 0xFFFFFFFF);

            mortonPairs.Dispose();

            // Build 4-wide tree top-down
            int freeNode = 2;
            var rangeStack = new NativeList<int4>(64, Allocator.Temp);
            rangeStack.Add(new int4(1, 0, count, 0));

            while (rangeStack.Length > 0)
            {
                int4 r = rangeStack[rangeStack.Length - 1];
                rangeStack.RemoveAt(rangeStack.Length - 1);
                int nodeIdx = r.x;
                int start = r.y;
                int len = r.z;

                if (len <= 4)
                {
                    var leaf = BoundingVolumeHierarchy.Node.EmptyLeaf;
                    for (int i = 0; i < len; i++)
                    {
                        int primIdx = sortedIndices[start + i];
                        int4 data = leaf.Data;
                        data[i] = primIdx;
                        leaf.Data = data;
                        leaf.Bounds.SetAabb(i, aabbs[primIdx]);
                    }
                    nodes[nodeIdx] = leaf;
                }
                else
                {
                    var node = BoundingVolumeHierarchy.Node.Empty;
                    int childCount = math.min(4, len);
                    int baseSize = len / childCount;
                    int remainder = len - baseSize * childCount;

                    int offset = start;
                    for (int i = 0; i < childCount; i++)
                    {
                        int childLen = baseSize + (i < remainder ? 1 : 0);
                        int childNode = freeNode++;

                        int4 data = node.Data;
                        data[i] = childNode;
                        node.Data = data;
                        rangeStack.Add(new int4(childNode, offset, childLen, 0));
                        offset += childLen;
                    }

                    nodes[nodeIdx] = node;
                }
            }

            nodeCount = freeNode;

            // Bottom-up AABB refit for internal nodes
            for (int i = nodeCount - 1; i >= 1; i--)
            {
                if (nodes[i].IsLeaf) continue;

                var bounds = FourTransposedAabbs.Empty;
                for (int c = 0; c < 4; c++)
                {
                    int childIdx = nodes[i].Data[c];
                    if (childIdx == 0) continue;
                    bounds.SetAabb(c, nodes[childIdx].Bounds.GetCompoundAabb());
                }
                nodes[i].Bounds = bounds;
            }

            sortedIndices.Dispose();
            rangeStack.Dispose();
        }

        private static uint EncodeMorton(uint3 v)
        {
            uint x = ExpandBits(v.x);
            uint y = ExpandBits(v.y);
            uint z = ExpandBits(v.z);
            return (z << 2) | (y << 1) | x;
        }

        private static uint ExpandBits(uint v)
        {
            v = (v * 0x00010001u) & 0xFF0000FFu;
            v = (v * 0x00000101u) & 0x0F00F00Fu;
            v = (v * 0x00000011u) & 0xC30C30C3u;
            v = (v * 0x00000005u) & 0x49249249u;
            return v;
        }

        private static void RadixSort(ref NativeArray<ulong> pairs, int count)
        {
            var temp = new NativeArray<ulong>(count, Allocator.Temp);
            var counts = new NativeArray<int>(256, Allocator.Temp);

            for (int pass = 0; pass < 4; pass++)
            {
                int shift = 32 + pass * 8;

                for (int i = 0; i < 256; i++) counts[i] = 0;
                for (int i = 0; i < count; i++)
                    counts[(int)((pairs[i] >> shift) & 0xFF)]++;

                int total = 0;
                for (int i = 0; i < 256; i++)
                {
                    int c = counts[i];
                    counts[i] = total;
                    total += c;
                }

                for (int i = 0; i < count; i++)
                {
                    int bucket = (int)((pairs[i] >> shift) & 0xFF);
                    temp[counts[bucket]++] = pairs[i];
                }

                NativeArray<ulong>.Copy(temp, pairs, count);
            }

            counts.Dispose();
            temp.Dispose();
        }
    }
}
