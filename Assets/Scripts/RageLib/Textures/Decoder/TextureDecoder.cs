using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace RageLib.Textures.Decoder
{
    public class TextureDecoder
    {
        public static RageUnityTexture Decode(Texture texture, int level = 0)
        {
            var texture2D = new RageUnityTexture(
                (int)texture.GetWidth(level),
                (int)texture.GetHeight(level),
                UnityEngine.TextureFormat.DXT1,
                level > 0);

            switch (texture.TextureType)
            {
                case TextureType.DXT1:
                    texture2D.format = UnityEngine.TextureFormat.DXT1;
                    break;
                case TextureType.DXT3:
                    texture2D.format = UnityEngine.TextureFormat.DXT5;
                    break;
                case TextureType.DXT5:
                    texture2D.format = UnityEngine.TextureFormat.DXT5;
                    break;
                case TextureType.A8R8G8B8:
                    texture2D.format = UnityEngine.TextureFormat.RGBA32;
                    break;
                case TextureType.L8:
                    texture2D.format = UnityEngine.TextureFormat.RGBA32;
                    break;
            }

            texture2D.textureFile = texture;
            texture2D.rageTextureType = texture.TextureType;
            texture2D.level = level;
            texture2D.name = $"{texture.Name}@{texture.TextureType.ToString()}";

            return texture2D;
        }

        public static byte[] ConvertDXT3ToDXT5(byte[] data)
        {
            var native = new NativeArray<byte>(data, Allocator.TempJob);

            new DXT3ToDXT5Job
            {
                Data = native,
            }.Schedule(data.Length / 16, 64).Complete();

            native.CopyTo(data);
            native.Dispose();

            return data;
        }

        [BurstCompile]
        struct DXT3ToDXT5Job : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<byte> Data;

            public void Execute(int blockIndex)
            {
                int i = blockIndex * 16;
                ulong packed = 0;

                for (int j = 0; j < 16; ++j)
                {
                    int s = 1 | ((j & 1) << 2);
                    int c = (Data[i + (j >> 1)] >> s) & 0x7;

                    if (c == 0) c = 1;
                    else if (c == 7) c = 0;
                    else c = 8 - c;

                    packed |= ((ulong)c << (3 * j));
                }

                Data[i + 0] = 0xff;
                Data[i + 1] = 0x00;

                for (int j = 0; j < 6; ++j)
                {
                    Data[i + 2 + j] = (byte)((packed >> (j << 3)) & 0xff);
                }
            }
        }
    }
}
