using RageLib.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using UnityEngine;

public static class Utils
{
    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        float x = reader.ReadFloat();
        float y = reader.ReadFloat();
        float z = reader.ReadFloat();
        return new Vector3(x, y, z);
    }

    public static bool CanReadMoreData(this BinaryReader reader)
    {
        long value = (reader.BaseStream.Length - reader.BaseStream.Position);
        return value > 0;
    }

    public static float ReadFloat(this BinaryReader reader)
    {
        return reader.ReadSingle();
    }

    public static int ReadInt(this BinaryReader reader)
    {
        return reader.ReadInt32();
    }

    public static long ReadUInt(this BinaryReader reader)
    {
        return reader.ReadUInt32();
    }

    public static string ReadString(this BinaryReader reader, int size)
    {
        char letter = 'n';
        String woord = "";
        for (int i = 0; i < size; i++)
        {
            letter = reader.ReadCharByte();
            woord += letter;
        }
        return woord;
    }

    public static char ReadCharByte(this BinaryReader reader)
    {
        char letter = '\0';
        try
        {
            letter = (char)reader.ReadByte();
        }
        catch (IOException ex)
        {

        }
        return letter;
    }

    public static Vector4 GetAxisAngle(this Vector4 vector)
    {
        float scale = -1.0f;
        Vector4 rot = new Vector4();
        rot.x = vector.x / scale;
        rot.y = vector.y / scale;
        rot.z = vector.z / scale;
        rot.w = (float)(Math.Acos(vector.w) * 2.0f);
        rot.w = (float)(rot.w * (180 / 3.14159265));
        return rot;
    }
}
