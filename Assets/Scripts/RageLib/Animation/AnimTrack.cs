using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;

namespace RageLib.Animation
{
    public class AnimTrack
    {
        public byte TrackType { get; private set; }
        public byte ChannelType { get; private set; }
        public ushort BoneId { get; private set; }
        public ushort FramesPerChunk { get; private set; }
        public ushort TrackFlags { get; private set; }
        public AnimChunk Chunk { get; private set; }

        public void Read(BinaryReader br, ushort numFrames)
        {
            TrackType = br.ReadByte();
            ChannelType = br.ReadByte();
            BoneId = br.ReadUInt16();
            FramesPerChunk = br.ReadUInt16();
            TrackFlags = br.ReadUInt16();

            long chunksPtr = ResourceUtil.ReadOffset(br);
            ushort chunksCount = br.ReadUInt16();
            br.ReadUInt16(); // capacity

            if (chunksPtr > 0 && chunksCount > 0)
            {
                using (new StreamContext(br))
                {
                    br.BaseStream.Seek(chunksPtr, SeekOrigin.Begin);
                    long chunkOffset = ResourceUtil.ReadOffset(br);

                    if (chunkOffset > 0)
                    {
                        br.BaseStream.Seek(chunkOffset, SeekOrigin.Begin);
                        Chunk = new AnimChunk();
                        Chunk.Read(br, numFrames);
                    }
                }
            }
        }
    }
}
