using System;
using System.IO;
using RageLib.Common.Resources;

namespace RageLib.Collision
{
    // Parses .wbn (world bound) RSC5 resource files.
    // Root is pgDictionary<phBound>, actual bound ptr at +0x08.
    public class CollisionFile
    {
        public PhBoundGeometry[] Geometries { get; private set; }
        public PhBound[] Primitives { get; private set; }
        public int RootBoundType { get; private set; }

        public void Open(string filename)
        {
            using var stream = System.IO.File.OpenRead(filename);
            Open(stream);
        }

        public void Open(Stream stream)
        {
            var res = new ResourceFile();
            res.Read(stream);

            if (res.Type != ResourceType.Bounds)
                throw new Exception("Not a bounds resource file");

            Parse(res.SystemMemData);
        }

        private void Parse(byte[] data)
        {
            // Root pgDictionary: bound ptr at +0x08
            uint rootPtrRaw = BitConverter.ToUInt32(data, 8);
            uint boundOffset = rootPtrRaw & 0x0FFFFFFF;
            if (boundOffset + 0x80 > data.Length)
                throw new Exception($"Root bound offset {boundOffset:#x} out of range");

            byte boundType = data[boundOffset + 4];
            RootBoundType = boundType;

            if (boundType == (byte)BoundType.BVH || boundType == (byte)BoundType.Geometry ||
                boundType == (byte)BoundType.Box)
            {
                var geom = ReadGeometry(data, boundOffset);
                Geometries = new[] { geom };
                Primitives = Array.Empty<PhBound>();
            }
            else if (boundType == (byte)BoundType.Sphere || boundType == (byte)BoundType.Capsule)
            {
                Geometries = Array.Empty<PhBoundGeometry>();
                Primitives = new[] { ReadPrimitive(data, boundOffset) };
            }
            else if (boundType == (byte)BoundType.Composite)
            {
                using var ms = new MemoryStream(data);
                ms.Seek(boundOffset, SeekOrigin.Begin);
                using var br = new BinaryReader(ms);

                var composite = new PhBoundComposite();
                composite.Read(br);
                composite.ReadComposite(data, boundOffset);
                Geometries = composite.Children;
                Primitives = composite.Primitives;
            }
            else
            {
                Geometries = Array.Empty<PhBoundGeometry>();
                Primitives = Array.Empty<PhBound>();
            }
        }

        private static PhBound ReadPrimitive(byte[] data, uint offset)
        {
            using var ms = new MemoryStream(data);
            ms.Seek(offset, SeekOrigin.Begin);
            using var br = new BinaryReader(ms);

            var bound = new PhBound();
            bound.Read(br);
            return bound;
        }

        private static PhBoundGeometry ReadGeometry(byte[] data, uint offset)
        {
            using var ms = new MemoryStream(data);
            ms.Seek(offset, SeekOrigin.Begin);
            using var br = new BinaryReader(ms);

            var geom = new PhBoundGeometry();
            geom.Read(br);
            geom.ReadGeometry(br, data);
            return geom;
        }

        public int TotalVertices
        {
            get
            {
                int total = 0;
                if (Geometries != null)
                    foreach (var g in Geometries)
                        total += g.NumVertices;
                return total;
            }
        }

        public int TotalTriangles
        {
            get
            {
                int total = 0;
                if (Geometries != null)
                    foreach (var g in Geometries)
                        total += g.NumPolygons;
                return total;
            }
        }
    }
}
