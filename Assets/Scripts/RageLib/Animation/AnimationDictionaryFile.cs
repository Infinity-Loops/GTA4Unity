using System;
using System.IO;
using RageLib.Common;
using RageLib.Common.ResourceTypes;

namespace RageLib.Animation
{
    public class AnimationDictionary : PGDictionary<AnimationData>, IFileAccess
    {
    }

    public class AnimationDictionaryFile : IDisposable
    {
        public File<AnimationDictionary> File { get; private set; }

        public void Open(Stream stream)
        {
            File = new File<AnimationDictionary>();
            File.Open(stream);
        }

        public void Dispose()
        {
            File?.Dispose();
        }
    }
}
