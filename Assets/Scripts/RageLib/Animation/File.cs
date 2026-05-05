using System;
using System.IO;
using RageLib.Common;
using RageLib.Common.Resources;

namespace RageLib.Animation
{
    public class File<T> : IDisposable where T : IFileAccess, new()
    {
        public T Data { get; private set; }

        public void Open(Stream stream)
        {
            var res = new ResourceFile();
            res.Read(stream);

            if (res.Type != ResourceType.Generic)
            {
                throw new Exception($"Not an animation resource (type=0x{(int)res.Type:X}, expected Generic=0x1).");
            }

            var systemMemory = new MemoryStream(res.SystemMemData);
            var br = new BinaryReader(systemMemory);

            Data = new T();
            Data.Read(br);

            systemMemory.Close();
        }

        public void Dispose() { }
    }
}
