using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_CULL : IPL_Item
{
    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;
    public int     unk1, unk2, unk3, unk4;
    public long    hash;
    public string  name;

    public override void Read(string line) { }

    public override void Read(BinaryReader reader)
    {
        posLowerLeft  = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1 = reader.ReadInt();
        unk2 = reader.ReadInt();
        unk3 = reader.ReadInt();
        unk4 = reader.ReadInt();
        hash = reader.ReadInt();
    }

    public override void Read(BinaryReader reader, GTAHashTable ini)
    {
        posLowerLeft  = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1 = reader.ReadInt();
        unk2 = reader.ReadInt();
        unk3 = reader.ReadInt();
        unk4 = reader.ReadInt();
        hash = reader.ReadInt();

        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = ini.GetValue<string>("Hashes", name);

        Debug.Log($"Cull Read: {name}");
    }
}
