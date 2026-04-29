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

using System.Diagnostics;
using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;
using RageLib.Common.ResourceTypes;

namespace RageLib.Models.Resource.Skeletons
{
    // rage::crJointData (0xE0 = 224 bytes per bone)
    // Verified against nightblade.wft hex dump (23 bones, all fields consistent).
    public class Bone : IFileAccess
    {
        public Bone Parent { get; set; }
        public Bone NextSibling { get; set; }
        public Bone FirstChild { get; set; }

        public long Offset { get; private set; }

        // --- Header (32 bytes) ---
        public string Name { get; private set; }
        public short Dofs { get; private set; }                // verified: DOF flags (bodyshell=0x038E, children=0x000F)
        private short Unknown0;                                // verified: always 8 in nightblade.wft

        public uint NextSiblingOffset { get; private set; }
        public uint FirstChildOffset { get; private set; }
        public uint ParentOffset { get; private set; }

        public short BoneIndex { get; private set; }
        public short BoneID { get; private set; }
        private short BoneIndex2;                              // always equals BoneIndex
        private short Unknown1;                                // bodyshell=0x0303, children=0x0300
        private int Unknown2;                                  // always 0 in nightblade.wft

        // --- Local transform (64 bytes, Vec3V+Vec3V+QuatV+Vec3V) ---
        // Verified: Euler matches quaternion (0.6981 rad = sin/cos(0.342/0.940))
        public Vector4 Position { get; private set; }          // verified: local position relative to parent
        public Vector4 RotationEuler { get; private set; }     // verified: local rotation as Euler XYZ radians
        public Vector4 RotationQuaternion { get; private set; }// verified: local rotation as quaternion XYZW
        private Vector4 Unknown3;                              // always zero in nightblade.wft

        // --- Absolute transform (48 bytes) ---
        // Verified: AbsolutePosition matches composed parent chain.
        public Vector4 AbsolutePosition { get; private set; }  // verified: world position relative to skeleton root
        public Vector4 AbsoluteRotationEuler { get; private set; } // verified: world rotation as Euler XYZ radians
        private Vector4 Unknown4;                              // always zero/near-zero in nightblade.wft
        private Vector4 Unknown5;                              // always zero in nightblade.wft
        private Vector4 Unknown6;                              // always zero in nightblade.wft

        // --- Joint limits (48 bytes) ---
        public Vector4 MinRotationLimit { get; private set; }  // verified: consistently (-pi, -pi, -pi) = min Euler limits
        public Vector4 MaxRotationLimit { get; private set; }  // verified: consistently (+pi, +pi, +pi) = max Euler limits
        private Vector4 Padding;                               // always zero in nightblade.wft

        public Bone()
        {
        }

        public Bone(BinaryReader br)
        {
            Read(br);
        }

        #region Implementation of IFileAccess

        public void Read(BinaryReader br)
        {
            Offset = br.BaseStream.Position;

            Name = new PtrString(br).Value;

            Dofs = br.ReadInt16();
            Unknown0 = br.ReadInt16();

            NextSiblingOffset = ResourceUtil.ReadOffset(br);
            FirstChildOffset = ResourceUtil.ReadOffset(br);
            ParentOffset = ResourceUtil.ReadOffset(br);

            BoneIndex = br.ReadInt16();
            BoneID = br.ReadInt16();
            BoneIndex2 = br.ReadInt16();
            Unknown1 = br.ReadInt16();

            Unknown2 = br.ReadInt32();

            Position = new Vector4(br);
            RotationEuler = new Vector4(br);
            RotationQuaternion = new Vector4(br);
            Unknown3 = new Vector4(br);

            AbsolutePosition = new Vector4(br);
            AbsoluteRotationEuler = new Vector4(br);
            Unknown4 = new Vector4(br);
            Unknown5 = new Vector4(br);
            Unknown6 = new Vector4(br);

            MinRotationLimit = new Vector4(br);
            MaxRotationLimit = new Vector4(br);
            Padding = new Vector4(br);

        }

        public void Write(BinaryWriter bw)
        {
            throw new System.NotImplementedException();
        }

        #endregion

        #region Overrides of Object

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}