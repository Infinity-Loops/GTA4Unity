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

using System.Collections.Generic;
using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;

namespace RageLib.Models.Resource.Skeletons
{
    // rage::crJointDataFile (GTA V equivalent: rage::crSkeletonData)
    public class Skeleton : IFileAccess
    {
        // Inner struct at the end of the header. Not positionally verified against GTA V.
        class SubStruct : DATBase, IFileAccess
        {
            private int Unknown0 { get; set; }
            private int Unknown1 { get; set; }

            public SubStruct(BinaryReader br) { Read(br); }

            public new void Read(BinaryReader br)
            {
                base.Read(br);
                Unknown0 = br.ReadInt32();
                Unknown1 = br.ReadInt32();
            }

            public new void Write(BinaryWriter bw) { throw new System.NotImplementedException(); }
        }

        // --- Header fields ---
        // Verified against nightblade.wft hex dump.
        public ushort BoneCount { get; private set; }          // verified: 23 in nightblade.wft
        private short Unknown0;                                // value=3 in nightblade.wft
        private int Unknown1;                                  // value=69 (0x45) in nightblade.wft
        private int Unknown2;                                  // value=15 (0x0F) in nightblade.wft

        // m_BoneIdTable: maps BoneID (short) -> BoneIndex (short) for animation compatibility.
        // Most bones have BoneID == BoneIndex; special bones (wheels, lights) use hashed name IDs
        // (e.g. wheel_lf=15298->3, headlight_l=15302->8). Sorted by BoneID.
        // Verified: 23 entries in nightblade.wft, all mappings match bone data.
        public SimpleCollection<BoneIDMapping> BoneIDMappings { get; private set; }

        private int Unknown3;                                  // value=3 in nightblade.wft
        private uint Unknown4;                                 // value=0x938C5F9F - likely signature hash
        private int Unknown5;                                  // value=0 in nightblade.wft

        private SubStruct Unknown6;

        public Skeleton()
        {
        }

        public Skeleton(BinaryReader br)
        {
            Read(br);
        }

        // --- Data arrays (read from offsets in header) ---
        // All verified against nightblade.wft hex dump.
        public SimpleArray<Bone> Bones { get; private set; }                    // verified: crJointData array (224 bytes each)
        public SimpleArray<int> ParentIndices { get; private set; }             // verified: parent bone index per bone (matches hierarchy)
        public SimpleArray<Matrix44> DefaultTransforms { get; private set; }    // verified: local rotation matrices (match bone RotationEuler)
        public SimpleArray<Matrix44> InverseTransforms { get; private set; }    // verified: inverse of DefaultTransforms
        public SimpleArray<Matrix44> GlobalTransforms { get; private set; }     // verified: absolute transform (rotation + translation)

        #region Implementation of IFileAccess

        public void Read(BinaryReader br)
        {
            uint bonesOffset = ResourceUtil.ReadOffset(br);
            uint parentIndicesOffset = ResourceUtil.ReadOffset(br);
            uint defaultTransformsOffset = ResourceUtil.ReadOffset(br);
            uint inverseTransformsOffset = ResourceUtil.ReadOffset(br);
            uint globalTransformsOffset = ResourceUtil.ReadOffset(br);

            BoneCount = br.ReadUInt16();
            Unknown0 = br.ReadInt16();
            Unknown1 = br.ReadInt32();
            Unknown2 = br.ReadInt32();

            BoneIDMappings = new SimpleCollection<BoneIDMapping>(br, r => new BoneIDMapping(r));

            Unknown3 = br.ReadInt32();
            Unknown4 = br.ReadUInt32();
            Unknown5 = br.ReadInt32();

            Unknown6 = new SubStruct(br);

            br.BaseStream.Seek(bonesOffset, SeekOrigin.Begin);
            Bones = new SimpleArray<Bone>(br, BoneCount, r => new Bone(r));

            br.BaseStream.Seek(parentIndicesOffset, SeekOrigin.Begin);
            ParentIndices = new SimpleArray<int>(br, BoneCount, r => r.ReadInt32());

            br.BaseStream.Seek(defaultTransformsOffset, SeekOrigin.Begin);
            DefaultTransforms = new SimpleArray<Matrix44>(br, BoneCount, r => new Matrix44(r));

            br.BaseStream.Seek(inverseTransformsOffset, SeekOrigin.Begin);
            InverseTransforms = new SimpleArray<Matrix44>(br, BoneCount, r => new Matrix44(r));

            br.BaseStream.Seek(globalTransformsOffset, SeekOrigin.Begin);
            GlobalTransforms = new SimpleArray<Matrix44>(br, BoneCount, r => new Matrix44(r));

            // Fun stuff...
            // Build a mapping of Offset -> Bone
            var boneOffsetMapping = new Dictionary<uint, Bone>();
            boneOffsetMapping.Add(0, null);
            foreach (var bone in Bones)
            {
                boneOffsetMapping.Add((uint)bone.Offset, bone);
            }

            // Now resolve all the bone offsets to the real bones
            foreach (var bone in Bones)
            {
                bone.Parent = boneOffsetMapping[bone.ParentOffset];
                bone.FirstChild = boneOffsetMapping[bone.FirstChildOffset];
                bone.NextSibling = boneOffsetMapping[bone.NextSiblingOffset];
            }
        }

        public void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}
