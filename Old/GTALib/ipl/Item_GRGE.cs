using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Item_GRGE : IPL_Item
{
    private int gameType;

    public Vector3 lowLeftPos;
    public float lineX, lineY;
    public Vector3 topRightPos;
    public int doorType;
    public int garageType;
    public int hash;
    public String name;
    public int unknown;

    public Item_GRGE(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void read(ReadFunctions rf)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini)
    {
        lowLeftPos = rf.readVector3D();
        lineX = rf.readFloat();
        lineY = rf.readFloat();
        topRightPos = rf.readVector3D();
        doorType = rf.readInt();
        garageType = rf.readInt();
        long tempHash = rf.readUnsignedInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = (string)ini["Hashes"][name]; // temp
        unknown = rf.readInt();
    }
}
