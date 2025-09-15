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
using RageLib.Common;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;
using RageLib.Textures;

namespace RageLib.Models.Resource.Shaders
{
    /// grmShaderGroup - Contains all shaders and textures for a drawable
    public class ShaderGroup : DATBase, IFileAccess
    {
        public uint TextureDictionaryOffset { get; private set; }
        public TextureFile TextureDictionary { get; set; }

        public PtrCollection<ShaderFx> Shaders { get; private set; }

        // Shader indices for different render passes
        // Each uint corresponds to a shader index for specific rendering scenarios
        private SimpleArray<uint> ShaderIndices { get; set; }  // 12 shader indices for different passes

        // Vertex declaration usage flags - defines which vertex attributes are used
        private SimpleCollection<uint> VertexDeclarationUsageFlags { get; set; }

        // Shader technique indices - maps to specific rendering techniques
        private SimpleCollection<uint> TechniqueIndices { get; set; }

        public ShaderGroup()
        {
        }

        public ShaderGroup(BinaryReader br)
        {
            Read(br);
        }

        #region Implementation of IFileAccess

        public new void Read(BinaryReader br)
        {
            base.Read(br);

            TextureDictionaryOffset = ResourceUtil.ReadOffset(br);

            // CPtrCollection<T>
            Shaders = new PtrCollection<ShaderFx>(br);

            ShaderIndices = new SimpleArray<uint>(br, 12, r => r.ReadUInt32());

            VertexDeclarationUsageFlags = new SimpleCollection<uint>(br, reader => reader.ReadUInt32());

            TechniqueIndices = new SimpleCollection<uint>(br, reader => reader.ReadUInt32());
        }

        public new void Write(BinaryWriter bw)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}