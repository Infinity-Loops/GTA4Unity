using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_STRBIG : IPL_Item
{
    public string  modelName;
    public int     unk1, unk2, unk3;
    public Vector3 pos;
    public Vector4 rot;

    public override void Read(string line) { }

    public override void Read(BinaryReader reader)
    {
        modelName = reader.ReadString(24);
        unk1 = reader.ReadInt();
        unk2 = reader.ReadInt();
        unk3 = reader.ReadInt();
        pos  = reader.ReadVector3();
        rot  = reader.ReadVector3();
    }

    public override void Read(BinaryReader reader, GTAHashTable ini) { }
}
