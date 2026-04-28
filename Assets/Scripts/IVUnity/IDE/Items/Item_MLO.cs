using System.Collections.Generic;
using RageLib.Common;
using UnityEngine;

public class Item_MLO : IDE_Item
{
    public string modelName;
    public int    numRooms;
    public int    numPortals;
    public int    numEntities;
    public float  drawDistance;

    public List<MloEntity> Entities = new List<MloEntity>();

    public struct MloEntity
    {
        public string  ModelName;
        public Vector3 Position;
        public Vector4 Rotation;
        public int     LodIndex;
        public int     Flags;
    }

    public void ReadHeader(string line)
    {
        var p = new LineParser(line);
        modelName    = p.ReadString();
        p.ReadInt(); // unknown
        numRooms     = p.ReadInt();
        numPortals   = p.ReadInt();
        numEntities  = p.ReadInt();
        drawDistance  = p.ReadFloat();

        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }

    public void ReadEntity(string line)
    {
        var p = new LineParser(line);
        var ent = new MloEntity
        {
            ModelName = p.ReadString(),
            Position  = p.ReadVector3(),
            Rotation  = p.ReadVector4(),
        };

        // Remaining fields (lodIndex, flags) vary across IDE files — read as floats
        if (p.HasMore) ent.LodIndex = (int)p.ReadFloat();
        if (p.HasMore) ent.Flags    = (int)p.ReadFloat();

        Entities.Add(ent);
        Hashes.table.AddData("hashes", Hasher.Hash(ent.ModelName).ToString(), ent.ModelName);
    }
}
