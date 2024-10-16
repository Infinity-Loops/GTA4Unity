using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_ZONE : IPL_Item
{
    private int gameType;

    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;

    public Item_ZONE(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void read(ReadFunctions rf)
    {
        posLowerLeft = rf.readVector3D();
        posUpperRight = rf.readVector3D();
    }

    public override void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}
