using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_GRGE : IPL_Item
{
    public Vector3 lowLeftPos;
    public float   lineX, lineY;
    public Vector3 topRightPos;
    public int     doorType;
    public int     garageType;
    public int     hash;
    public string  name;
    public int     unknown;

    public override void Read(string line) { }
    public override void Read(BinaryReader reader) { }

    public override void Read(BinaryReader reader, GTAHashTable ini)
    {
        lowLeftPos  = reader.ReadVector3();
        lineX       = reader.ReadFloat();
        lineY       = reader.ReadFloat();
        topRightPos = reader.ReadVector3();
        doorType    = reader.ReadInt();
        garageType  = reader.ReadInt();
        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = ini.GetValue<string>("Hashes", name);
        unknown = reader.ReadInt();
    }
}
