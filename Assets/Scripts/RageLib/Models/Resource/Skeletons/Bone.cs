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

        public unsafe void Read(BinaryReader br)
        {
            Offset = br.BaseStream.Position;

            // Read the entire 224-byte bone in one shot
            byte[] raw = br.ReadBytes(224);

            fixed (byte* p = raw)
            {
                // Header (32 bytes)
                uint namePtr = *(uint*)p;
                uint nameOff = namePtr == 0 ? 0 : namePtr & 0x0FFFFFFF;
                Dofs = *(short*)(p + 4);
                Unknown0 = *(short*)(p + 6);

                uint nextRaw = *(uint*)(p + 8);
                NextSiblingOffset = nextRaw == 0 ? 0 : (nextRaw >> 28) == 5 ? nextRaw & 0x0FFFFFFF : 0;
                uint childRaw = *(uint*)(p + 12);
                FirstChildOffset = childRaw == 0 ? 0 : (childRaw >> 28) == 5 ? childRaw & 0x0FFFFFFF : 0;
                uint parentRaw = *(uint*)(p + 16);
                ParentOffset = parentRaw == 0 ? 0 : (parentRaw >> 28) == 5 ? parentRaw & 0x0FFFFFFF : 0;

                BoneIndex = *(short*)(p + 20);
                BoneID = *(short*)(p + 22);
                BoneIndex2 = *(short*)(p + 24);
                Unknown1 = *(short*)(p + 26);
                Unknown2 = *(int*)(p + 28);

                // 12 Vector4s (192 bytes at offset 32)
                float* f = (float*)(p + 32);
                Position = new Vector4(f[0], f[1], f[2], f[3]);
                RotationEuler = new Vector4(f[4], f[5], f[6], f[7]);
                RotationQuaternion = new Vector4(f[8], f[9], f[10], f[11]);
                Unknown3 = new Vector4(f[12], f[13], f[14], f[15]);
                AbsolutePosition = new Vector4(f[16], f[17], f[18], f[19]);
                AbsoluteRotationEuler = new Vector4(f[20], f[21], f[22], f[23]);
                Unknown4 = new Vector4(f[24], f[25], f[26], f[27]);
                Unknown5 = new Vector4(f[28], f[29], f[30], f[31]);
                Unknown6 = new Vector4(f[32], f[33], f[34], f[35]);
                MinRotationLimit = new Vector4(f[36], f[37], f[38], f[39]);
                MaxRotationLimit = new Vector4(f[40], f[41], f[42], f[43]);
                Padding = new Vector4(f[44], f[45], f[46], f[47]);

                // Read name string
                if (nameOff != 0)
                {
                    long saved = br.BaseStream.Position;
                    br.BaseStream.Seek(nameOff, SeekOrigin.Begin);
                    Name = ResourceUtil.ReadNullTerminatedString(br);
                    br.BaseStream.Seek(saved, SeekOrigin.Begin);
                }
                else
                {
                    Name = "";
                }
            }
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