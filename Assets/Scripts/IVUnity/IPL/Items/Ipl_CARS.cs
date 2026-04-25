using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_CARS : IPL_Item
{
    public Vector3 position = new Vector3();
    public Vector3 rotation = new Vector3();
    public int     hash;
    public string  name;
    public int     unknown1, unknown2, unknown3, unknown4, unknown5, unknown6, unknown7;
    public int     type = 0;

    public override void Read(string line) { }
    public override void Read(BinaryReader reader) { }

    public override void Read(BinaryReader reader, GTAHashTable ini)
    {
        position = reader.ReadVector3();
        rotation = reader.ReadVector3();
        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        if (ini.HasOption("Cars", name))
        {
            name = ini.GetValue<string>("Cars", name);
        }
        else
        {
            name = "";
        }
        unknown1 = reader.ReadInt();
        unknown2 = reader.ReadInt();
        unknown3 = reader.ReadInt();
        unknown4 = reader.ReadInt();
        unknown5 = reader.ReadInt();
        unknown6 = reader.ReadInt();
        unknown7 = reader.ReadInt();
    }
}
