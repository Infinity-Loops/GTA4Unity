using System;
using System.IO;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;

namespace RageLib.Collision
{
    // rage::phBoundGeometry / rage::phBoundBVH (extends phBoundPolyhedron)
    // Verified against GTAIV and bronx_e_6.wbn (6875 verts, 5952 polys, 100% valid)
    public class PhBoundGeometry : PhBound
    {
        public Vector3 UnQuantizeFactor { get; private set; }
        public Vector3 BoundingBoxCenter { get; private set; }
        public int NumVertices { get; private set; }
        public int NumPolygons { get; private set; }

        public UnityEngine.Vector3[] Vertices { get; private set; }
        public PhPolygon[] Polygons { get; private set; }

        public void ReadGeometry(BinaryReader br, byte[] systemData)
        {
            // Polyhedron fields start at +0x80 (after PhBound base read)
            br.ReadUInt32(); // +0x80: unknown
            br.ReadUInt32(); // +0x84: m_ShrunkVertices ptr
            br.ReadUInt32(); // +0x88: unknown ptr

            uint polygonsOffset = ResourceUtil.ReadOffset(br); // +0x8C

            float uqX = br.ReadSingle(); // +0x90
            float uqY = br.ReadSingle();
            float uqZ = br.ReadSingle();
            br.ReadSingle(); // W pad
            UnQuantizeFactor = new Vector3(uqX, uqY, uqZ);

            float cX = br.ReadSingle(); // +0xA0
            float cY = br.ReadSingle();
            float cZ = br.ReadSingle();
            br.ReadSingle(); // W pad
            BoundingBoxCenter = new Vector3(cX, cY, cZ);

            uint verticesOffset = ResourceUtil.ReadOffset(br); // +0xB0
            br.ReadUInt32(); // +0xB4: unknown ptr
            br.ReadUInt32(); // +0xB8: m_NumPerVertexAttribs + padding
            br.ReadUInt32(); // +0xBC: 0xFFFFFFFF marker
            br.ReadUInt32(); // +0xC0: unknown
            br.ReadUInt32(); // +0xC4: unknown

            NumVertices = br.ReadInt32();  // +0xC8
            NumPolygons = br.ReadInt32();  // +0xCC

            DecompressVertices(systemData, verticesOffset);
            ReadPolygons(systemData, polygonsOffset);
        }

        private unsafe void DecompressVertices(byte[] data, uint offset)
        {
            if (offset == 0 || NumVertices <= 0)
            {
                Vertices = Array.Empty<UnityEngine.Vector3>();
                return;
            }

            Vertices = new UnityEngine.Vector3[NumVertices];
            float uqX = UnQuantizeFactor.X, uqY = UnQuantizeFactor.Y, uqZ = UnQuantizeFactor.Z;
            float cX = BoundingBoxCenter.X, cY = BoundingBoxCenter.Y, cZ = BoundingBoxCenter.Z;

            fixed (byte* basePtr = &data[offset])
            {
                short* src = (short*)basePtr;
                for (int i = 0; i < NumVertices; i++)
                {
                    float rx = src[0] * uqX + cX;
                    float ry = src[1] * uqY + cY;
                    float rz = src[2] * uqZ + cZ;
                    src += 3;

                    // Inline RageCoordinates.Position: (-x, z, -y)
                    Vertices[i] = new UnityEngine.Vector3(-rx, rz, -ry);
                }
            }
        }

        private unsafe void ReadPolygons(byte[] data, uint offset)
        {
            if (offset == 0 || NumPolygons <= 0)
            {
                Polygons = Array.Empty<PhPolygon>();
                return;
            }

            Polygons = new PhPolygon[NumPolygons];
            fixed (byte* basePtr = &data[offset])
            {
                byte* src = basePtr;
                for (int i = 0; i < NumPolygons; i++)
                {
                    float* f = (float*)src;
                    ushort* u = (ushort*)(src + 16);

                    Polygons[i] = new PhPolygon
                    {
                        NormalX = f[0], NormalY = f[1], NormalZ = f[2],
                        Area = f[3],
                        VertexIndex0 = u[0], VertexIndex1 = u[1],
                        VertexIndex2 = u[2], VertexIndex3 = u[3],
                        Neighbor0 = u[4], Neighbor1 = u[5],
                        Neighbor2 = u[6], Neighbor3 = u[7]
                    };
                    src += 32;
                }
            }
        }
    }
}
