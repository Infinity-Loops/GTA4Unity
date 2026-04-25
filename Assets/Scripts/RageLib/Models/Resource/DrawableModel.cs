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
using System.Diagnostics;
using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;
using RageLib.Models.Resource.Models;
using RageLib.Models.Resource.Shaders;
using RageLib.Models.Resource.Skeletons;
using RageLib.Textures;

namespace RageLib.Models.Resource
{
    // gtaDrawable : rmcDrawable : rmcDrawableBase
    public class DrawableModel : PGBase, IFileAccess, IDataReader, IEmbeddedResourceReader, IDisposable
    {
        public ShaderGroup ShaderGroup { get; set; }
        public Skeleton Skeleton { get; private set; }

        public Vector4 Center { get; private set; }
        public Vector4 BoundsMin { get; private set; }
        public Vector4 BoundsMax { get; private set; }

        public PtrCollection<Model>[] ModelCollection { get; private set; }

        public Vector4 AbsoluteMax { get; private set; }

        // LOD group and model properties
        private uint LodGroupFlags { get; set; }     // LOD flags, often 1 or 9
        
        // These are typically -1 (0xFFFFFFFF) when unused
        private uint JointDataOffset { get; set; }    // Joint constraint data pointer (usually -1)
        private uint Reserved1 { get; set; }           // Reserved/padding field (usually -1)
        private uint Reserved2 { get; set; }           // Reserved/padding field (usually -1)

        private float LodDistanceHigh { get; set; }    // LOD switching distance threshold

        // Handle and container information
        private uint HandleIndex { get; set; }         // Resource handle index
        private uint ContainerSize { get; set; }       // Container size in quadwords
        private uint ContainerType { get; set; }       // Container type identifier

        // Light attributes collection (CSimpleCollection<LightAttrs>)
        private uint LightAttrsPointer { get; set; }   // Pointer to light attributes
        private uint LightAttrsCount { get; set; }     // Number of light attributes

        public void ReadData(BinaryReader br)
        {
            foreach (var geometryInfo in ModelCollection)
            {
                foreach (var info in geometryInfo)
                {
                    foreach (var dataInfo in info.Geometries)
                    {
                        dataInfo.VertexBuffer.ReadData(br);
                        dataInfo.IndexBuffer.ReadData(br);
                    }                    
                }
            }
        }

        #region IFileAccess Members

        public new void Read(BinaryReader br)
        {
            base.Read(br);

            // rage::rmcDrawableBase
            //    rage::rmcDrawable
            //        gtaDrawable

            var shaderGroupOffset = ResourceUtil.ReadOffset(br);
            var skeletonOffset = ResourceUtil.ReadOffset(br);

            Center = new Vector4(br);
            BoundsMin = new Vector4(br);
            BoundsMax = new Vector4(br);

            int levelOfDetailCount = 0;
            var modelOffsets = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                modelOffsets[i] = ResourceUtil.ReadOffset(br);
                if (modelOffsets[i] != 0)
                {
                    levelOfDetailCount++;
                }
            }

            AbsoluteMax = new Vector4(br);

            LodGroupFlags = br.ReadUInt32();

            JointDataOffset = br.ReadUInt32();
            Reserved1 = br.ReadUInt32();
            Reserved2 = br.ReadUInt32();

            LodDistanceHigh = br.ReadSingle();

            HandleIndex = br.ReadUInt32();
            ContainerSize = br.ReadUInt32();
            ContainerType = br.ReadUInt32();

            // Collection<LightAttrs>
            LightAttrsPointer = br.ReadUInt32();
            LightAttrsCount = br.ReadUInt32();

            // The data follows:

            if (shaderGroupOffset != 0)
            {
                br.BaseStream.Seek(shaderGroupOffset, SeekOrigin.Begin);
                ShaderGroup = new ShaderGroup(br);
            }

            if (skeletonOffset != 0)
            {
                br.BaseStream.Seek(skeletonOffset, SeekOrigin.Begin);
                Skeleton = new Skeleton(br);
            }

            ModelCollection = new PtrCollection<Model>[levelOfDetailCount];
            for (int i = 0; i < levelOfDetailCount; i++)
            {
                br.BaseStream.Seek(modelOffsets[i], SeekOrigin.Begin);
                ModelCollection[i] = new PtrCollection<Model>(br);
            }
        }

        public new void Write(BinaryWriter bw)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Implementation of IEmbeddedResourceReader

        public void ReadEmbeddedResources(Stream systemMemory, Stream graphicsMemory)
        {
            if (ShaderGroup.TextureDictionaryOffset != 0)
            {
                systemMemory.Seek(ShaderGroup.TextureDictionaryOffset, SeekOrigin.Begin);

                ShaderGroup.TextureDictionary = new TextureFile();
                ShaderGroup.TextureDictionary.Open(systemMemory, graphicsMemory);
            }
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (ShaderGroup != null)
            {
                if (ShaderGroup.TextureDictionary != null)
                {
                    ShaderGroup.TextureDictionary.Dispose();
                    ShaderGroup.TextureDictionary = null;
                }
            }
        }

        #endregion
    }
}
