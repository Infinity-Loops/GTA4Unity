using System.IO;
using RageLib.Common.ResourceTypes;

namespace RageLib.Collision
{
    // rage::phBound base class (0x80 = 128 bytes in GTA IV)
    // Verified against GTAIV constructor at line 443747
    public class PhBound
    {
        public BoundType Type { get; private set; }
        public byte Flags { get; private set; }
        public ushort PartIndex { get; private set; }
        public float RadiusAroundCentroid { get; private set; }
        public float Unknown0C { get; private set; }

        public Vector3 BoundingBoxMax { get; private set; }
        public Vector3 BoundingBoxMin { get; private set; }
        public Vector3 CentroidOffset { get; private set; }
        public Vector3 CGOffset { get; private set; }
        public Vector3 VolumeDistribution { get; private set; }

        public void Read(BinaryReader br)
        {
            br.ReadUInt32(); // vtable
            Type = (BoundType)br.ReadByte();
            Flags = br.ReadByte();
            PartIndex = br.ReadUInt16();
            RadiusAroundCentroid = br.ReadSingle();
            Unknown0C = br.ReadSingle();

            BoundingBoxMax = ReadVec3Aligned(br);
            BoundingBoxMin = ReadVec3Aligned(br);
            CentroidOffset = ReadVec3Aligned(br);
            CGOffset = ReadVec3Aligned(br);
            VolumeDistribution = ReadVec3Aligned(br);

            // +0x60: sentinel Vec4V, +0x70: margin Vec3 + refcount
            br.BaseStream.Seek(32, SeekOrigin.Current);
        }

        private static Vector3 ReadVec3Aligned(BinaryReader br)
        {
            float x = br.ReadSingle();
            float y = br.ReadSingle();
            float z = br.ReadSingle();
            br.ReadSingle(); // W padding (NaN)
            return new Vector3(x, y, z);
        }
    }
}
