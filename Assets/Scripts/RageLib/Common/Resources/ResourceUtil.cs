/**********************************************************************\

 RageLib
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
using System.IO;
using System.Text;
using RageLib.Common;

namespace RageLib.Common.Resources
{
    public static class ResourceUtil
    {
        public static bool IsResource(Stream stream)
        {
            var rh = new ResourceHeader();
            rh.Read(new BinaryReader(stream));
            return rh.IsValidRSC();
        }

        public static void GetResourceData(Stream stream, out uint flags, out ResourceType type)
        {
            var rh = new ResourceHeader();
            rh.Read(new BinaryReader(stream));
            flags = rh.Flags;
            type = rh.Type;
        }

        public static uint ReadOffset(BinaryReader br)
        {
            if (!br.CanReadMoreData())
                return 0;

            uint offset = br.ReadUInt32();
            
            if (offset == 0)
                return 0;
            
            // Virtual offset marker for RSC5 is 0x5 in upper nibble
            uint marker = offset >> 28;
            if (marker != 5)
            {
                // Log warning for debugging but don't throw - maintain compatibility
                System.Diagnostics.Debug.WriteLine($"Warning: Expected virtual offset marker 0x5, got 0x{marker:X}");
                return 0;
            }
            
            // Mask out the marker bits to get actual offset
            return offset & 0x0FFFFFFF;
        }

        public static uint ReadDataOffset(BinaryReader br)
        {
            uint offset = br.ReadUInt32();

            if (offset == 0)
                return 0;

            // Physical/data offset marker for RSC5 is 0x6 in upper nibble
            uint marker = offset >> 28;
            if (marker != 6)
            {
                throw new Exception($"Expected a data offset marker 0x6, got 0x{marker:X} at position {br.BaseStream.Position - 4}");
            }
            
            return offset & 0x0FFFFFFF;
        }

        public static uint ReadDataOffset(BinaryReader br, uint mask, out uint lowerBits)
        {
            uint value;
            uint offset = br.ReadUInt32();

            if (offset == 0)
            {
                lowerBits = 0;
                value = 0;
            }
            else
            {
                if (offset >> 28 != 6)
                {
                    throw new Exception("Expected a data offset.");
                }
                value = offset & mask;
                lowerBits = offset & (~mask & 0xff);
            }

            return value;
        }

        public static string ReadNullTerminatedString(BinaryReader br)
        {
            var sb = new StringBuilder();

            var c = (char) br.ReadByte();
            while (c != 0)
            {
                sb.Append(c);
                c = (char) br.ReadByte();
            }

            return sb.ToString();
        }
        
        // Optimized string reading with length hint
        public static string ReadNullTerminatedString(BinaryReader br, int maxLength)
        {
            var sb = new StringBuilder(Math.Min(maxLength, 256));
            int count = 0;
            
            while (count < maxLength)
            {
                byte b = br.ReadByte();
                if (b == 0)
                    break;
                    
                sb.Append((char)b);
                count++;
            }
            
            return sb.ToString();
        }
        
        // Fast offset validation without exceptions for performance
        public static bool IsValidOffset(uint offset, uint expectedMarker)
        {
            if (offset == 0)
                return true;
                
            uint marker = offset >> 28;
            return marker == expectedMarker;
        }
        
        // Batch read optimization for multiple offsets
        public static uint[] ReadOffsets(BinaryReader br, int count)
        {
            uint[] offsets = new uint[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = ReadOffset(br);
            }
            return offsets;
        }

    }
}