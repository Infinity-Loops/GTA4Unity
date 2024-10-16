using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ByteReader
{
    private byte[] stream;
    private int currentOffset;
    private bool system = true;
    private int sysSize = 0;

    public ByteReader(byte[] stream, int startOffset)
    {
        this.stream = stream;
        this.currentOffset = startOffset;
    }

    public int readUInt32()
    {
        byte[] tmp = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            tmp[i] = stream[currentOffset + i];
        }

        long accum = 0;
        for (int i = 0; i < 4; i++)
        {
            accum |= (long)(tmp[i] & 0xFF) << (i * 8);
        }

        currentOffset += 4;
        return (int)accum;
    }

    public int readUInt16()
    {
        int low = stream[currentOffset] & 0xFF;
        int high = stream[currentOffset + 1] & 0xFF;
        currentOffset += 2;
        return (high << 8) | low;
    }

    public short ReadInt16()
    {
        short ret = (short)(((stream[currentOffset + 1] << 8) | (stream[currentOffset] & 0xFF)));
        currentOffset += 2;
        return ret;
    }

    public float readFloat()
    {
        byte[] tmp = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            tmp[i] = stream[currentOffset + i];
        }

        currentOffset += 4;
        int accum = (tmp[0] & 0xFF) | ((tmp[1] & 0xFF) << 8) | ((tmp[2] & 0xFF) << 16) | ((tmp[3] & 0xFF) << 24);
        return BitConverter.ToSingle(BitConverter.GetBytes(accum), 0);
    }

    public Vector4 readVector()
    {
        float x = readFloat();
        float y = readFloat();
        float z = readFloat();
        float w = readFloat();
        return new Vector4(x, y, z, w);
    }

    // Função readOffset corrigida
    public int readOffset()
    {
        int offset = readUInt32();
        if (offset == 0 || (offset >> 28) != 5)
        {
            return -1;
        }
        return offset & 0x0FFFFFFF;
    }

    // Função ReadDataOffset corrigida
    public int ReadDataOffset()
    {
        int offset = readUInt32();
        if (offset == 0)
        {
            return 0;
        }
        if ((offset >> 28) != 6)
        {
            // Comportamento não especificado, então fazemos nada aqui
        }
        return offset & 0x0FFFFFFF;
    }

    public string readNullTerminatedString(int size)
    {
        StringBuilder sb = new StringBuilder();
        bool gotNull = false;
        for (int i = 0; i < size; i++)
        {
            byte b = readByte();
            if (!gotNull)
            {
                if (b != 0)
                {
                    sb.Append((char)b);
                }
                else
                {
                    gotNull = true;
                }
            }
        }
        return sb.ToString();
    }

    public string readNullTerminatedString()
    {
        StringBuilder sb = new StringBuilder();
        char c = (char)stream[currentOffset];
        while (c != 0)
        {
            sb.Append(c);
            currentOffset++;
            c = (char)stream[currentOffset];
        }
        return sb.ToString();
    }

    public string readString(int length)
    {
        StringBuilder sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append((char)stream[currentOffset]);
            currentOffset++;
        }
        return sb.ToString();
    }

    public byte[] toArray(int bytes)
    {
        byte[] arr = new byte[bytes];
        for (int i = 0; i < bytes; i++)
        {
            arr[i] = stream[currentOffset];
            currentOffset++;
        }
        return arr;
    }

    public byte[] toArray(int start, int end)
    {
        int length = end - start;
        byte[] retStream = new byte[length];
        currentOffset = start;
        for (int i = 0; i < length; i++)
        {
            retStream[i] = stream[currentOffset];
            currentOffset++;
        }
        return retStream;
    }

    public byte readByte()
    {
        return stream[currentOffset++];
    }

    public void setCurrentOffset(int offset)
    {
        currentOffset = system ? offset : offset + sysSize;
    }

    public int getCurrentOffset()
    {
        return currentOffset;
    }

    public void setSysSize(int size)
    {
        sysSize = size;
    }

    public void setSystemMemory(bool system)
    {
        this.system = system;
    }

    public byte[] readBytes(int pCount)
    {
        byte[] buffer = new byte[pCount];
        for (int i = 0; i < pCount; i++)
        {
            buffer[i] = readByte();
        }
        return buffer;
    }

    public void skipBytes(int bytes)
    {
        currentOffset += bytes;
    }

    public int moreToRead()
    {
        return stream.Length - currentOffset;
    }

    public long unsignedInt()
    {
        int i = readUInt32();
        return i & 0xFFFFFFFFL;
    }

    // Função para verificar flag
    public bool hasFlag(int flags, int flag)
    {
        return (flags & flag) == flag;
    }
}
