/**********************************************************************\

 RageLib - Models
 Copyright (C) 2009  Arushan/Aru <oneforaru at gmail.com>

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

namespace RageLib.Models.Resource
{
    // gtaFragType
    public class FragTypeModel : IFileAccess, IDataReader, IEmbeddedResourceReader, IDisposable
    {
        public class FragTypeChild : IFileAccess
        {
            public DrawableModel Drawable { get; set; }

            public FragTypeChild(BinaryReader br)
            {
                Read(br);
            }

            #region Implementation of IFileAccess

            public void Read(BinaryReader br)
            {
                br.BaseStream.Seek(0x90, SeekOrigin.Current);

                uint offset = ResourceUtil.ReadOffset(br);
                if (offset != 0)
                {
                    Drawable = new DrawableModel();

                    br.BaseStream.Seek(offset, SeekOrigin.Begin);
                    Drawable.Read(br);
                }
            }

            public void Write(BinaryWriter bw)
            {
                throw new System.NotImplementedException();
            }

            #endregion
        }

        public DrawableModel Drawable { get; set; }
        public FragTypeChild[] Children { get; set; }
        public Skeletons.Skeleton Skeleton { get; set; } // Expose skeleton for fragment positioning

        #region Implementation of IFileAccess

        public void Read(BinaryReader br)
        {
            br.BaseStream.Seek(0xB4, SeekOrigin.Begin);
            uint offset = ResourceUtil.ReadOffset(br);
            if (offset != 0)
            {
                Drawable = new DrawableModel();
                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                Drawable.Read(br);
                
                // Get the skeleton from the drawable if it exists
                Skeleton = Drawable.Skeleton;
            }
            else
            {
                throw new Exception("No model in FragType");
            }

            br.BaseStream.Seek(0x1F3, SeekOrigin.Begin);
            int childCount = br.ReadByte();

            br.BaseStream.Seek(0xD4, SeekOrigin.Begin);
            uint childListOffset = ResourceUtil.ReadOffset(br);

            br.BaseStream.Seek(childListOffset, SeekOrigin.Begin);
            var childOffsets = new SimpleArray<uint>(br, childCount, ResourceUtil.ReadOffset);

            Children = new FragTypeChild[childCount];
            for(int i=0;i<childCount; i++)
            {
                br.BaseStream.Seek(childOffsets[i], SeekOrigin.Begin);
                Children[i] = new FragTypeChild(br);
            }

            foreach (var child in Children)
            {
                if (child.Drawable != null)
                {
                    child.Drawable.ShaderGroup = Drawable.ShaderGroup;
                }
            }
            
        }

        public void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion

        #region Implementation of IDataReader

        public void ReadData(BinaryReader br)
        {
            Drawable.ReadData(br);

            foreach (var child in Children)
            {
                if (child.Drawable != null)
                {
                    child.Drawable.ReadData(br);
                }
            }
        }

        #endregion

        #region Implementation of IEmbeddedResourceReader

        public void ReadEmbeddedResources(Stream systemMemory, Stream graphicsMemory)
        {
            Drawable.ReadEmbeddedResources(systemMemory, graphicsMemory);
        }

        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Gets the position for a specific child fragment using its drawable center
        /// </summary>
        public UnityEngine.Vector3 GetChildPosition(int childIndex)
        {
            if (childIndex < 0 || childIndex >= Children.Length)
                return UnityEngine.Vector3.zero;

            var child = Children[childIndex];
            if (child == null || child.Drawable == null)
                return UnityEngine.Vector3.zero;

            // Use the drawable's center as the fragment position
            var center = child.Drawable.Center;
            return new UnityEngine.Vector3(center.X, center.Y, center.Z);
        }
        
        /// <summary>
        /// Gets all child positions as Unity vectors
        /// </summary>
        public UnityEngine.Vector3[] GetAllChildPositions()
        {
            if (Children == null)
                return new UnityEngine.Vector3[0];

            var positions = new UnityEngine.Vector3[Children.Length];
            for (int i = 0; i < Children.Length; i++)
            {
                positions[i] = GetChildPosition(i);
            }

            return positions;
        }
        
        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (Drawable != null)
            {
                Drawable.Dispose();
            }
        }

        #endregion
    }
}
