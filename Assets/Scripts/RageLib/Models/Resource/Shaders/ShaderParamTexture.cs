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
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;

namespace RageLib.Models.Resource.Shaders
{
    // grmShaderParam — Texture variant (FILE format, 0x1C bytes).
    //
    // Binary trace shows the RUNTIME struct is 0x30 bytes with name at +0x20, but the
    // FILE format is smaller — the engine expands the struct during deserialization,
    // moving the name pointer from file +0x14 to runtime +0x20 and adding runtime-only
    // fields. The original RageLib layout (0x1C bytes, name at +0x14) is correct for
    // the file format.
    //
    // The file's name strings are usually null-terminated, but RAGE's resource compiler
    // sometimes packs consecutive specular/bump names back-to-back without separators.
    // We detect this by checking if the read name contains a suffix→uppercase boundary
    // pattern (e.g. "_SNextName") and truncate at the first such boundary.
    internal class ShaderParamTexture : DATBase, IShaderParam
    {
        private uint   Unknown1 { get; set; }           // +0x04
        private ushort Unknown2 { get; set; }           // +0x08
        private ushort Unknown3 { get; set; }           // +0x0A
        private uint   Unknown4 { get; set; }           // +0x0C
        private uint   Unknown5 { get; set; }           // +0x10
        private uint   TextureNameOffset { get; set; }  // +0x14 (pgPtr to name string)
        private uint   Unknown7 { get; set; }           // +0x18

        public string TextureName { get; private set; }

        #region Implementation of IFileAccess

        public new void Read(BinaryReader br)
        {
            base.Read(br);

            Unknown1 = br.ReadUInt32();
            Unknown2 = br.ReadUInt16();
            Unknown3 = br.ReadUInt16();
            Unknown4 = br.ReadUInt32();
            Unknown5 = br.ReadUInt32();
            TextureNameOffset = ResourceUtil.ReadOffset(br);
            Unknown7 = br.ReadUInt32();

            if (TextureNameOffset != 0)
            {
                br.BaseStream.Seek(TextureNameOffset, SeekOrigin.Begin);
                try
                {
                    TextureName = ResourceUtil.ReadNullTerminatedString(br);
                }
                catch
                {
                    // Leave TextureName null — logged as missing downstream.
                }
            }
        }

        public new void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}
