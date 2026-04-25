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
    public class Model : DATBase, IFileAccess
    {
        public PtrCollection<Geometry> Geometries { get; private set; }
        
        // grmModel fields
        // These should actually be bytes but are read as ushorts (pairs)
        private byte MatrixCount { get; set; }      // Number of bone matrices for skinning
        private byte Flags { get; set; }            // Model flags (RELATIVE, RESOURCED, etc.)
        private byte Type { get; set; }             // Model type identifier
        private byte MatrixIndex { get; set; }      // Matrix index for hierarchical models
        
        private byte RenderMask { get; set; }       // Render bucket mask
        private byte SkinFlag { get; set; }         // Skinning enabled flag
        private ushort GeometryCount { get; set; }  // Number of geometries (matches Geometries.Count)
        
        // Bounding boxes: one per geometry + one for the whole model
        public SimpleArray<Vector4> BoundingBoxes { get; private set; }
        
        // Maps each geometry to a shader index in the ShaderGroup
        public SimpleArray<ushort> ShaderMappings { get; private set; }

        #region Implementation of IFileAccess

        public new void Read(BinaryReader br)
        {
            base.Read(br);

            Geometries = new PtrCollection<Geometry>(br);

            var boundingBoxesOffset = ResourceUtil.ReadOffset(br);
            var shaderMappingOffset = ResourceUtil.ReadOffset(br);

            // Read as bytes instead of ushorts for proper field mapping
            MatrixCount = br.ReadByte();
            Flags = br.ReadByte();
            Type = br.ReadByte();
            MatrixIndex = br.ReadByte();

            RenderMask = br.ReadByte();
            SkinFlag = br.ReadByte();
            GeometryCount = br.ReadUInt16();

            //

            br.BaseStream.Seek(boundingBoxesOffset, SeekOrigin.Begin);
            // Bounding boxes: one per geometry + one for the whole model
            int boundingBoxCount = Geometries.Count + 1;
            BoundingBoxes = new SimpleArray<Vector4>(br, boundingBoxCount * 2, reader => new Vector4(reader));

            br.BaseStream.Seek(shaderMappingOffset, SeekOrigin.Begin);
            ShaderMappings = new SimpleArray<ushort>(br, Geometries.Count, reader => reader.ReadUInt16());
        }

        public new void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}