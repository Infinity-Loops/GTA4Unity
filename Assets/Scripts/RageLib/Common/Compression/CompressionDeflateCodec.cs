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
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace RageLib.Common.Compression
{
    internal class CompressionDeflateCodec : ICompressionCodec
    {
        // Optimized buffer size - larger for better performance
        private const int CopyBufferSize = 64*1024;    // 64kb for better throughput
        
        // Reusable buffers to reduce allocations
        [ThreadStatic] private static byte[] _sharedBuffer;
        
        private static byte[] GetSharedBuffer()
        {
            if (_sharedBuffer == null)
                _sharedBuffer = new byte[CopyBufferSize];
            return _sharedBuffer;
        }

        public void Compress(Stream source, Stream destination)
        {
            try
            {
                var def = new Deflater(Deflater.DEFAULT_COMPRESSION, true);
                
                // Pre-allocate based on stream length for better memory usage
                long inputLength = source.Length - source.Position;
                if (inputLength > int.MaxValue)
                    throw new System.ArgumentException("Input stream too large for compression");
                    
                var inputData = new byte[inputLength];
                int totalRead = 0;
                int bytesRead;
                
                // Ensure we read all data even if Read doesn't return everything at once
                while (totalRead < inputData.Length)
                {
                    bytesRead = source.Read(inputData, totalRead, inputData.Length - totalRead);
                    if (bytesRead == 0)
                        break;
                    totalRead += bytesRead;
                }

                var buffer = GetSharedBuffer();

                def.SetInput(inputData, 0, totalRead);
                def.Finish();

                while (!def.IsFinished)
                {
                    int outputLen = def.Deflate(buffer, 0, buffer.Length);
                    if (outputLen > 0)
                        destination.Write(buffer, 0, outputLen);
                }

                def.Reset();
            }
            catch (System.Exception ex)
            {
                throw new System.IO.IOException($"Deflate compression failed: {ex.Message}", ex);
            }
        }

        public void Decompress(Stream source, Stream destination)
        {
            try
            {
                using (var inflater = new InflaterInputStream(source, new Inflater(true)))
                {
                    inflater.IsStreamOwner = false; // Don't close the source stream
                    
                    var dataBuffer = GetSharedBuffer();
                    StreamUtils.Copy(inflater, destination, dataBuffer);
                }
            }
            catch (ICSharpCode.SharpZipLib.SharpZipBaseException ex)
            {
                throw new System.IO.IOException($"Deflate decompression failed: {ex.Message}", ex);
            }
        }
    }
}