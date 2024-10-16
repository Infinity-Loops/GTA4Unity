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
                    data = DXTDecoder.DecodeDXT1(data, (int)width, (int)height);
                    break;
                case TextureType.DXT3:
                    data = DXTDecoder.DecodeDXT3(data, (int)width, (int)height);
                    break;
                case TextureType.DXT5:
                    data = DXTDecoder.DecodeDXT5(data, (int)width, (int)height);
                    break;
                case TextureType.A8R8G8B8:
                    //Nothing to do
                    break;
                case TextureType.L8:
                    {
                        // L8 to RGBA32
                        var newData = new byte[width * height * 4];
                        for (int i = 0; i < data.Length; i++)
                        {
                            newData[i * 4 + 0] = data[i];  // R
                            newData[i * 4 + 1] = data[i];  // G
                            newData[i * 4 + 2] = data[i];  // B
                            newData[i * 4 + 3] = 255;      // A (opaque)
                        }
                        data = newData;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                int offset = i * 4;
                pixels[i] = new Color32(data[offset + 0], data[offset + 1], data[offset + 2], data[offset + 3]);
            }
            texture2D.name = $"{texture.Name}@{texture.TextureType.ToString()}";
            texture2D.pixels = pixels;

            return texture2D;
        }
    }
}
