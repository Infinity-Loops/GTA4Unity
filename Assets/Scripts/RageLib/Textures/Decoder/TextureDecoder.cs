/**********************************************************************\

 RageLib - Textures
 Copyright (C) 2008  Arushan/Aru <oneforaru at gmail.com>

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.

\**********************************************************************/

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace RageLib.Textures.Decoder
{
    internal class TextureDecoder
    {
        internal static RageUnityTexture Decode(Texture texture, int level)
        {
            var width = texture.GetWidth(level);
            var height = texture.GetHeight(level);
            var data = texture.GetTextureData(level);

            RageUnityTexture texture2D = new RageUnityTexture((int)width, (int)height, TextureFormat.RGBA32, false);

            switch (texture.TextureType)
            {
                case TextureType.DXT1:
                    texture2D.format = TextureFormat.DXT1;
                    break;
                case TextureType.DXT3:
                    texture2D.format = TextureFormat.DXT5;
                    data = ConvertDXT3ToDXT5(data);
                    break;
                case TextureType.DXT5:
                    texture2D.format = TextureFormat.DXT5;
                    break;
                case TextureType.A8R8G8B8:
                    texture2D.format = TextureFormat.RGBA32;
                    break;
                case TextureType.L8:
                    texture2D.format = TextureFormat.RGBA32;
                    break;
            }

            texture2D.pixels = data;
            texture2D.name = $"{texture.Name}@{texture.TextureType.ToString()}";

            return texture2D;
        }

        private static byte[] ConvertDXT3ToDXT5(byte[] data)
        {
            for (var i = 0; i < data.Length; i += 16)
            {
                ulong packed = 0;

                for (var j = 0; j < 16; ++j)
                {
                    var s = 1 | ((j & 1) << 2);
                    var c = (data[i + (j >> 1)] >> s) & 0x7;

                    switch (c)
                    {
                        case 0:
                            c = 1;
                            break;

                        case 7:
                            c = 0;
                            break;

                        default:
                            c = 8 - c;
                            break;
                    }

                    packed |= ((ulong)c << (3 * j));
                }

                data[i + 0] = 0xff;
                data[i + 1] = 0x00;

                for (var j = 0; j < 6; ++j)
                {
                    data[i + 2 + j] = (byte)((packed >> (j << 3)) & 0xff);
                }
            }

            return data;
        }
    }
}
