using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;

namespace RageLib.Animation
{
    public class AnimationData : IFileAccess
    {
        public ushort Flags { get; private set; }
        public ushort NumFrames { get; private set; }
        public ushort FramesPerChunk { get; private set; }
        public float Duration { get; private set; }
        public uint Signature { get; private set; }
        public string Name { get; private set; }
        public AnimTrack[] Tracks { get; private set; }

        public void Read(BinaryReader br)
        {
            // +0x00: vftable (already consumed by DATBase)
            // PGBase reads vftable, we start after that
            // Actually IFileAccess.Read starts at the beginning of the struct in PtrCollection
            uint vftable = br.ReadUInt32();

            ushort refCount = br.ReadUInt16();
            Flags = br.ReadUInt16();
            NumFrames = br.ReadUInt16();
            FramesPerChunk = br.ReadUInt16();
            Duration = br.ReadSingle();
            Signature = br.ReadUInt32();

            // atArray for blocks at +0x14
            long blocksOffset = ResourceUtil.ReadOffset(br);
            ushort blocksCount = br.ReadUInt16();
            ushort blocksCap = br.ReadUInt16();

            // Name pointer at +0x1C
            long nameOffset = ResourceUtil.ReadOffset(br);

            // atArray<crAnimTrack*> at +0x20
            long tracksOffset = ResourceUtil.ReadOffset(br);
            ushort trackCount = br.ReadUInt16();
            ushort trackCap = br.ReadUInt16();

            // +0x28: extra field
            br.ReadUInt32();

            // Read name
            if (nameOffset > 0)
            {
                using (new StreamContext(br))
                {
                    br.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
                    Name = ReadNullTermString(br);
                }
            }

            // Read tracks
            Tracks = new AnimTrack[trackCount];
            if (tracksOffset > 0 && trackCount > 0)
            {
                using (new StreamContext(br))
                {
                    br.BaseStream.Seek(tracksOffset, SeekOrigin.Begin);
                    long[] trackPtrs = new long[trackCount];
                    for (int i = 0; i < trackCount; i++)
                        trackPtrs[i] = ResourceUtil.ReadOffset(br);

                    for (int i = 0; i < trackCount; i++)
                    {
                        if (trackPtrs[i] <= 0) continue;
                        br.BaseStream.Seek(trackPtrs[i], SeekOrigin.Begin);
                        Tracks[i] = new AnimTrack();
                        Tracks[i].Read(br, NumFrames);
                    }
                }
            }
        }

        public void Write(BinaryWriter bw) { }

        static string ReadNullTermString(BinaryReader br, int max = 128)
        {
            var chars = new System.Collections.Generic.List<byte>(64);
            for (int i = 0; i < max; i++)
            {
                byte b = br.ReadByte();
                if (b == 0) break;
                chars.Add(b);
            }
            return System.Text.Encoding.ASCII.GetString(chars.ToArray());
        }
    }
}
