using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;

namespace RageLib.Animation
{
    public class AnimChunk
    {
        public byte TrackType { get; private set; }
        public byte Reconstruct { get; private set; }
        public ushort BoneId { get; private set; }
        public AnimChannel[] Channels { get; private set; }

        public void Read(BinaryReader br, ushort numFrames)
        {
            TrackType = br.ReadByte();
            Reconstruct = br.ReadByte();
            BoneId = br.ReadUInt16();

            // 4 channel pointers
            long[] channelPtrs = new long[4];
            for (int i = 0; i < 4; i++)
                channelPtrs[i] = ResourceUtil.ReadOffset(br);

            uint channelCount = br.ReadUInt32();

            Channels = new AnimChannel[channelCount];
            for (int i = 0; i < channelCount && i < 4; i++)
            {
                if (channelPtrs[i] <= 0) continue;
                using (new StreamContext(br))
                {
                    br.BaseStream.Seek(channelPtrs[i], SeekOrigin.Begin);
                    Channels[i] = AnimChannel.ReadChannel(br, numFrames);
                }
            }
        }
    }
}
