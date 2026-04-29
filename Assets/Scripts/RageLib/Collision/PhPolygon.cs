using System;
using System.IO;

namespace RageLib.Collision
{
    // rage::phPolygon (0x20 = 32 bytes in GTA IV)
    // Verified against GTAIV (line 471729, 505027) and bronx_e_6.wbn extraction
    public struct PhPolygon
    {
        public float NormalX, NormalY, NormalZ;
        public float Area;
        public ushort VertexIndex0, VertexIndex1, VertexIndex2;
        public ushort VertexIndex3;
        public ushort Neighbor0, Neighbor1, Neighbor2, Neighbor3;

        public byte MaterialIndex => (byte)(BitConverter.SingleToInt32Bits(Area) & 0xFF);

        public bool IsQuad => (VertexIndex3 & 0x7FFF) != 0;

        public int GetVertexIndex(int i)
        {
            return i switch
            {
                0 => VertexIndex0 & 0x7FFF,
                1 => VertexIndex1 & 0x7FFF,
                2 => VertexIndex2 & 0x7FFF,
                3 => VertexIndex3 & 0x7FFF,
                _ => 0
            };
        }

        public static PhPolygon Read(BinaryReader br)
        {
            var p = new PhPolygon();
            p.NormalX = br.ReadSingle();
            p.NormalY = br.ReadSingle();
            p.NormalZ = br.ReadSingle();
            p.Area = br.ReadSingle();
            p.VertexIndex0 = br.ReadUInt16();
            p.VertexIndex1 = br.ReadUInt16();
            p.VertexIndex2 = br.ReadUInt16();
            p.VertexIndex3 = br.ReadUInt16();
            p.Neighbor0 = br.ReadUInt16();
            p.Neighbor1 = br.ReadUInt16();
            p.Neighbor2 = br.ReadUInt16();
            p.Neighbor3 = br.ReadUInt16();
            return p;
        }
    }
}
