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
using System.Runtime.InteropServices;

namespace RageLib.Textures.Encoder
{
    internal class TextureEncoder
    {
        internal static void Encode(Texture texture, Texture2D image, int level)
        {
            var width = texture.GetWidth(level);
            var height = texture.GetHeight(level);
            var data = new byte[width * height * 4];  // R G B A

            Texture2D bitmap = new Texture2D(1, 1, TextureFormat.ARGB32, false);


            if (texture.TextureType == TextureType.A8R8G8B8)
            {

                bitmap = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);

                bitmap.SetPixels(image.GetPixels());
                bitmap.Apply();

                // RGBA (A8R8G8B8)
                var pixels = bitmap.GetPixels32();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = pixels[y * (int)width + x];
                        int offset = (y * (int)width + x) * 4;
                        data[offset + 0] = color.r; // R
                        data[offset + 1] = color.g; // G
                        data[offset + 2] = color.b; // B
                        data[offset + 3] = color.a; // A
                    }
                }
            }
            else if (texture.TextureType == TextureType.L8) // L8
            {
                var newData = new byte[width * height];

                var pixels = image.GetPixels32();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = pixels[y * (int)width + x];
                        int offset = y * (int)width + x;

                        newData[offset] = (byte)((color.r + color.g + color.b) / 3);
                    }
                }

                data = newData;
            }
            else 
            {
                var pixels = image.GetPixels32();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = pixels[y * (int)width + x];
                        int offset = (y * (int)width + x) * 4;
                        data[offset + 0] = color.r; // R
                        data[offset + 1] = color.g; // G
                        data[offset + 2] = color.b; // B
                        data[offset + 3] = color.a; // A
                    }
                }
            }

            bitmap.Apply();


            switch (texture.TextureType)
            {
                case TextureType.DXT1:
                    data = DXTEncoder.EncodeDXT1(data, (int)width, (int)height);
                    break;
                case TextureType.DXT3:
                    data = DXTEncoder.EncodeDXT3(data, (int)width, (int)height);
                    break;
                case TextureType.DXT5:
                    data = DXTEncoder.EncodeDXT5(data, (int)width, (int)height);
                    break;
                case TextureType.A8R8G8B8:
                case TextureType.L8:

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            texture.SetTextureData(level, data);
        }
    }
}
