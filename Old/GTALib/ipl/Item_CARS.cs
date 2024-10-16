using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_CARS_IPL : IPL_Item
{
    private int gameType;

    public bool selected = false;

    public Vector3 position = new Vector3();
    public Vector3 rotation = new Vector3();
    public int hash;
    public String name;
    public int unknown1, unknown2, unknown3, unknown4, unknown5, unknown6, unknown7;
    public int type = 0;
    public Item_CARS_IPL(int gameType)
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
        position = rf.readVector3D();
        rotation = rf.readVector3D();
        long tempHash = rf.readUnsignedInt();
        name = "" + tempHash;
        hash = (int)tempHash;

        if (ini.ContainsKey("Cars"))
        {
            if (ini["Cars"].ContainsKey(name))
            {
                name = (string)ini["Cars"]["Name"];
            }
        }
        else
        {
            name = "";
        }

        unknown1 = rf.readInt();
        unknown2 = rf.readInt();
        unknown3 = rf.readInt();
        unknown4 = rf.readInt();
        unknown5 = rf.readInt();
        unknown6 = rf.readInt();
        unknown7 = rf.readInt();
    }
}
