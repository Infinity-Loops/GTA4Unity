using System;
using System.IO;
using RageLib.Models.Resource;

namespace RageLib.Models.Data
{
    public class Mesh
    {
        public PrimitiveType PrimitiveType { get; private set; }
        public int FaceCount { get; private set; }

        public int VertexCount { get; private set; }
        public byte[] VertexData { get; private set; }
        public VertexDeclaration VertexDeclaration { get; private set; }
        public bool VertexHasNormal { get; set; }
        public bool VertexHasTexture { get; set; }
        public bool VertexHasColor { get; set; }
        public bool VertexHasBlendInfo { get; set; }
        public int VertexStride { get; private set; }

        public int IndexCount { get; private set; }
        public byte[] IndexData { get; private set; }

        public int MaterialIndex { get; set; }

        private int posOffset = -1, normalOffset = -1, uvOffset = -1, colorOffset = -1;

        internal Mesh(Resource.Models.Geometry info)
        {
            PrimitiveType = (PrimitiveType) info.PrimitiveType;

            FaceCount = (int) info.FaceCount;

            VertexCount = info.VertexCount;
            VertexStride = info.VertexStride;
            VertexData = info.VertexBuffer.RawData;

            IndexCount = (int) info.IndexCount;
            IndexData = info.IndexBuffer.RawData;

            VertexDeclaration = new VertexDeclaration(info.VertexBuffer.VertexDeclaration);
            foreach (var element in VertexDeclaration.Elements)
            {
                if (element.Stream == -1) break;

                switch (element.Usage)
                {
                    case VertexElementUsage.Position:
                        posOffset = element.Offset;
                        break;
                    case VertexElementUsage.Normal:
                        VertexHasNormal = true;
                        normalOffset = element.Offset;
                        break;
                    case VertexElementUsage.TextureCoordinate when element.UsageIndex == 0:
                        VertexHasTexture = true;
                        uvOffset = element.Offset;
                        break;
                    case VertexElementUsage.Color when element.UsageIndex == 0:
                        VertexHasColor = true;
                        colorOffset = element.Offset;
                        break;
                    case VertexElementUsage.BlendIndices:
                        VertexHasBlendInfo = true;
                        break;
                }
            }
        }

        public ushort[] DecodeIndexData()
        {
            var indices = new ushort[IndexCount];
            Buffer.BlockCopy(IndexData, 0, indices, 0, IndexCount * 2);
            return indices;
        }

        public Vertex[] DecodeVertexData()
        {
            byte[] vertexData = VertexData;
            Vertex[] vertices = new Vertex[VertexCount];

            using(MemoryStream ms = new MemoryStream(vertexData))
            {
                BinaryReader br = new BinaryReader(ms);
                for (int i = 0; i < VertexCount; i++)
                {
                    ms.Seek(i*VertexStride, SeekOrigin.Begin);
                    vertices[i] = new Vertex(br, this);
                }
            }

            return vertices;
        }

        public unsafe CleanVertex[] DecodeUnityBurstVertexData()
        {
            byte[] data = VertexData;
            int stride = VertexStride;
            int count = VertexCount;
            var vertices = new CleanVertex[count];

            bool hasPos = posOffset >= 0;
            bool hasNormal = normalOffset >= 0;
            bool hasUV = uvOffset >= 0;
            bool hasColor = colorOffset >= 0;

            fixed (byte* basePtr = data)
            {
                for (int i = 0; i < count; i++)
                {
                    byte* b = basePtr + i * stride;
                    CleanVertex v = default;

                    if (hasPos)
                    {
                        float* p = (float*)(b + posOffset);
                        v.Position = new UnityEngine.Vector3(p[0], p[1], p[2]);
                    }

                    if (hasNormal)
                    {
                        float* n = (float*)(b + normalOffset);
                        v.Normal = new UnityEngine.Vector3(n[0], n[1], n[2]);
                    }

                    if (hasUV)
                    {
                        float* t = (float*)(b + uvOffset);
                        v.TextureCoordinates = new UnityEngine.Vector2(t[0], t[1]);
                    }

                    if (hasColor)
                    {
                        v.DiffuseColor = *(uint*)(b + colorOffset);
                    }

                    vertices[i] = v;
                }
            }

            return vertices;
        }
    }
}
