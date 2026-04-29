using System;
using System.Collections.Generic;
using System.IO;

namespace RageLib.Collision
{
    // rage::phBoundComposite (type 12 in GTA IV)
    // Verified against GTAIV resource constructor at line 463130
    //
    // Layout after phBound base (+0x80):
    //   +0x80 [0x20]: m_Bounds (ptr, relocated)
    //   +0x84 [0x21]: m_CurrentMatrices (ptr, relocated)
    //   +0x88 [0x22]: m_LastMatrices (ptr, relocated)
    //   +0x8C [0x23]: m_LocalBoxMinMaxs (ptr, relocated)
    //   +0x90 [0x24]: m_MaxNumBounds (u16) - used as loop limit in rsc ctor
    //   +0x92:        m_NumBounds (u16)
    public class PhBoundComposite : PhBound
    {
        public PhBoundGeometry[] Children { get; private set; }
        public PhBound[] Primitives { get; private set; }

        public void ReadComposite(byte[] systemData, long baseOffset)
        {
            uint boundsArrayRaw = BitConverter.ToUInt32(systemData, (int)baseOffset + 0x80);
            ushort maxBounds = BitConverter.ToUInt16(systemData, (int)baseOffset + 0x90);
            ushort numBounds = BitConverter.ToUInt16(systemData, (int)baseOffset + 0x92);

            int count = maxBounds > 0 ? maxBounds : numBounds;

            if (boundsArrayRaw == 0 || count == 0)
            {
                Children = Array.Empty<PhBoundGeometry>();
                return;
            }

            uint boundsArrayOffset = boundsArrayRaw & 0x0FFFFFFF;
            if (boundsArrayOffset + count * 4 > systemData.Length)
            {
                Children = Array.Empty<PhBoundGeometry>();
                return;
            }

            var children = new List<PhBoundGeometry>();
            var primitives = new List<PhBound>();

            for (int i = 0; i < count; i++)
            {
                uint childPtrRaw = BitConverter.ToUInt32(systemData, (int)boundsArrayOffset + i * 4);
                if (childPtrRaw == 0) continue;

                uint childOffset = childPtrRaw & 0x0FFFFFFF;
                if (childOffset + 0x80 > systemData.Length) continue;

                byte childType = systemData[childOffset + 4];

                if (childType == (byte)BoundType.Geometry ||
                    childType == (byte)BoundType.BVH ||
                    childType == (byte)BoundType.Box)
                {
                    if (childOffset + 0xD0 > systemData.Length) continue;

                    using var ms = new MemoryStream(systemData);
                    ms.Seek(childOffset, SeekOrigin.Begin);
                    using var childBr = new BinaryReader(ms);

                    var geom = new PhBoundGeometry();
                    geom.Read(childBr);
                    geom.ReadGeometry(childBr, systemData);

                    if (geom.NumVertices > 0 && geom.NumPolygons > 0)
                        children.Add(geom);
                }
                else if (childType == (byte)BoundType.Sphere ||
                         childType == (byte)BoundType.Capsule)
                {
                    using var ms = new MemoryStream(systemData);
                    ms.Seek(childOffset, SeekOrigin.Begin);
                    using var childBr = new BinaryReader(ms);

                    var prim = new PhBound();
                    prim.Read(childBr);
                    primitives.Add(prim);
                }
            }

            Children = children.ToArray();
            Primitives = primitives.ToArray();
        }
    }
}
