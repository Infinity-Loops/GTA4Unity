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

using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;

namespace RageLib.Models.Resource.Models
{
    // rage::grcIndexBufferD3D
    public class IndexBuffer : DATBase, IFileAccess
    {
        public uint IndexCount { get; private set; }
        public uint DataOffset { get; private set; }

        public byte[] RawData { get; private set; }

        public IndexBuffer()
        {
        }

        public IndexBuffer(BinaryReader br)
        {
            Read(br);
        }

        public void ReadData(BinaryReader br)
        {
            int size = (int)(IndexCount * 2);
            RawData = new byte[size];
            br.BaseStream.Seek(DataOffset, SeekOrigin.Begin);
            br.BaseStream.Read(RawData, 0, size);
        }

        public void ReadData(byte[] graphicsMemData)
        {
            int size = (int)(IndexCount * 2);
            RawData = new byte[size];
            System.Buffer.BlockCopy(graphicsMemData, (int)DataOffset, RawData, 0, size);
        }

        #region Implementation of IFileAccess

        public new void Read(BinaryReader br)
        {
            base.Read(br);

            IndexCount = br.ReadUInt32();

            DataOffset = ResourceUtil.ReadDataOffset(br);

            var p1Offset = ResourceUtil.ReadOffset(br);
        }

        public new void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}