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

namespace RageLib.Textures.Filter
{
    class ChannelFilter : IFilter
    {
        private ImageChannel _channel;

        public ChannelFilter(ImageChannel channel)
        {
            _channel = channel;
        }

        public void Apply(Texture2D image)
        {
            if (_channel != ImageChannel.All)
            {
                uint mask;
                int shift;

                switch (_channel)
                {
                    case ImageChannel.Red:
                        mask = 0x00FF0000;
                        shift = 16;
                        break;
                    case ImageChannel.Green:
                        mask = 0x0000FF00;
                        shift = 8;
                        break;
                    case ImageChannel.Blue:
                        mask = 0x000000FF;
                        shift = 0;
                        break;
                    case ImageChannel.Alpha:
                        mask = 0xFF000000;
                        shift = 24;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Color32[] pixels = image.GetPixels32();

                for (int y = 0; y < image.height; y++)
                {
                    for (int x = 0; x < image.width; x++)
                    {
                        int offset = y * image.width + x;

                        Color32 color = pixels[offset];

                        uint colorValue = (uint)((color.r << 16) | (color.g << 8) | color.b | (color.a << 24));

                        byte data = (byte)((colorValue & mask) >> shift);

                        pixels[offset] = new Color32(data, data, data, 255);
                    }
                }

                image.SetPixels32(pixels);
                image.Apply();
            }
        }
    }
}
