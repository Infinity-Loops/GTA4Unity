using System.IO;

namespace RageLib.Common
{
    public static class BinaryReaderExtensions
    {
        public static bool CanReadMoreData(this BinaryReader reader)
        {
            return reader.BaseStream.Position < reader.BaseStream.Length;
        }
    }
}