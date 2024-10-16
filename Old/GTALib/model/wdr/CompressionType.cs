using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompressionType
{
    public const int LZX = (0xF505);
    public const int Deflate = (0xDA78);

    private int type;

    public CompressionType(int type)
    {
        this.type = type;
    }
    public static CompressionType get(int type)
    {
        CompressionType ret = LZX;
        switch (type)
        {
            case 0xF505:
                ret = LZX;
                break;
            case 0xDA78:
                ret = Deflate;
                break;
        }
        return ret;
    }

    public static implicit operator int(CompressionType compressionType)
    {
        return compressionType.type;
    }

    public static implicit operator CompressionType(int type)
    {
        return new CompressionType(type);
    }
}

