using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

public class Compression
{
    private CompressionType type;

    public void setCodec(CompressionType type)
    {
        this.type = type;
    }

    public void compress(Stream destination, byte[] source)
    {
        try
        {
            using (DeflateStream deflater = new DeflateStream(destination, CompressionLevel.Optimal, true))
            {
                deflater.Write(source, 0, source.Length);
            }

            // Mensagem opcional para indicar que a compressão foi concluída
            // Console.WriteLine("Deflate finished");
        }
        catch (IOException ex)
        {
            Debug.LogError("Error during compression: " + ex.Message);
        }
    }

    public byte[] decompress(byte[] source, int totalSize)
    {
        byte[] dataBuffer = new byte[totalSize];
        try
        {
            using (MemoryStream ms = new MemoryStream(source))
            using (DeflateStream inflater = new DeflateStream(ms, CompressionMode.Decompress))
            {
                inflater.Read(dataBuffer, 0, dataBuffer.Length);
            }
        }
        catch (InvalidDataException ex)
        {
            Debug.LogError("Error during decompression: " + ex.Message);
        }

        return dataBuffer;
    }
}
