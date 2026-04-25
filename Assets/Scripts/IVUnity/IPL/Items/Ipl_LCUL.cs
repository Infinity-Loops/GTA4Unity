using System;
using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_LCUL : IPL_Item
{
    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;
    public int     unk1;
    public long    hash1, hash2, hash3, hash4, hash5;
    public long    hash6, hash7, hash8, hash9, hash10;
    public string  name1, name2, name3, name4, name5;
    public string  name6, name7, name8, name9, name10;

    public override void Read(string line) => Debug.Log($"{GetType().Name} not supported yet.");

    public override void Read(BinaryReader reader)
    {
        ReadCommon(reader);
    }

    public override void Read(BinaryReader reader, GTAHashTable ini)
    {
        ReadCommon(reader);
    }

    private void ReadCommon(BinaryReader reader)
    {
        posLowerLeft  = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1   = reader.ReadInt();
        hash1  = reader.ReadUInt();
        hash2  = reader.ReadUInt();
        hash3  = reader.ReadUInt();
        hash4  = reader.ReadUInt();
        hash5  = reader.ReadUInt();
        hash6  = reader.ReadUInt();
        hash7  = reader.ReadUInt();
        hash8  = reader.ReadUInt();
        hash9  = reader.ReadUInt();
        hash10 = reader.ReadUInt();
        name1  = reader.ReadString(32);
        name2  = reader.ReadString(32);
        name3  = reader.ReadString(32);
        name4  = reader.ReadString(32);
        name5  = reader.ReadString(32);
        name6  = reader.ReadString(32);
        name7  = reader.ReadString(32);
        name8  = reader.ReadString(32);
        name9  = reader.ReadString(32);
        name10 = reader.ReadString(32);
    }
}
