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
    public class Geometry : DATBase, IFileAccess
    {
        // grmGeometry/grmGeometryQB fields
        private uint Flags { get; set; }                   // Geometry flags
        private uint BoneIds { get; set; }                 // Packed bone IDs for skinning
        
        // Vertex buffer info (follows pointer)
        private uint VertexBufferUnk1 { get; set; }        // Vertex buffer related field
        private uint VertexBufferUnk2 { get; set; }        // Vertex buffer related field
        private uint VertexBufferUnk3 { get; set; }        // Vertex buffer related field
        
        // Index buffer info (follows pointer)
        private uint IndexBufferUnk1 { get; set; }         // Index buffer related field
        private uint IndexBufferUnk2 { get; set; }         // Index buffer related field
        private uint IndexBufferUnk3 { get; set; }         // Index buffer related field
        
        public uint IndexCount { get; private set; }       // Total number of indices
        public uint FaceCount { get; private set; }        // Number of triangles/faces
        public ushort VertexCount { get; private set; }    // Number of vertices
        public ushort PrimitiveType { get; private set; }  // RAGE_PRIMITIVE_TYPE (triangles, strips, etc.)
        
        private uint VertexDeclaration { get; set; }       // Vertex format declaration
        public ushort VertexStride { get; private set; }   // Size of each vertex in bytes
        private ushort BoneCount { get; set; }             // Number of bones affecting this geometry
        
        private uint MaterialId { get; set; }              // Material/shader ID
        private uint DrawBucket { get; set; }              // Render bucket for sorting
        private uint Reserved { get; set; }                // Reserved/padding

        public VertexBuffer VertexBuffer { get; set; }
        public IndexBuffer IndexBuffer { get; set; }

        #region Implementation of IFileAccess

        public new void Read(BinaryReader br)
        {
            base.Read(br);

            Flags = br.ReadUInt32();
            BoneIds = br.ReadUInt32();

            var vertexBuffersOffset = ResourceUtil.ReadOffset(br);
            VertexBufferUnk1 = br.ReadUInt32();
            VertexBufferUnk2 = br.ReadUInt32();
            VertexBufferUnk3 = br.ReadUInt32();

            var indexBuffersOffset = ResourceUtil.ReadOffset(br);
            IndexBufferUnk1 = br.ReadUInt32();
            IndexBufferUnk2 = br.ReadUInt32();
            IndexBufferUnk3 = br.ReadUInt32();

            IndexCount = br.ReadUInt32();
            FaceCount = br.ReadUInt32();
            VertexCount = br.ReadUInt16();
            PrimitiveType = br.ReadUInt16();

            VertexDeclaration = br.ReadUInt32();

            VertexStride = br.ReadUInt16();
            BoneCount = br.ReadUInt16();

            MaterialId = br.ReadUInt32();
            DrawBucket = br.ReadUInt32();
            Reserved = br.ReadUInt32();

            // Data

            br.BaseStream.Seek(vertexBuffersOffset, SeekOrigin.Begin);
            VertexBuffer = new VertexBuffer(br);

            br.BaseStream.Seek(indexBuffersOffset, SeekOrigin.Begin);
            IndexBuffer = new IndexBuffer(br);
        }

        public new void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}