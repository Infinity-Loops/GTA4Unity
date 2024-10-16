using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_LCUL : IPL_Item
{
    private int gameType;

    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;
    public int unk1;
    public long hash1, hash2, hash3, hash4, hash5;
    public long hash6, hash7, hash8, hash9, hash10;
    public String name1, name2, name3, name4, name5;
    public String name6, name7, name8, name9, name10;

    public Item_LCUL(int gameType)
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
        unk1 = rf.readInt();
        hash1 = rf.readUnsignedInt();
        hash2 = rf.readUnsignedInt();
        hash3 = rf.readUnsignedInt();
        hash4 = rf.readUnsignedInt();
        hash5 = rf.readUnsignedInt();
        hash6 = rf.readUnsignedInt();
        hash7 = rf.readUnsignedInt();
        hash8 = rf.readUnsignedInt();
        hash9 = rf.readUnsignedInt();
        hash10 = rf.readUnsignedInt();
        name1 = rf.readString(32);
        name2 = rf.readString(32);
        name3 = rf.readString(32);
        name4 = rf.readString(32);
        name5 = rf.readString(32);
        name6 = rf.readString(32);
        name7 = rf.readString(32);
        name8 = rf.readString(32);
        name9 = rf.readString(32);
        name10 = rf.readString(32);
    }

    public override void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini)
    {
        posLowerLeft = rf.readVector3D();
        posUpperRight = rf.readVector3D();
        unk1 = rf.readInt();
        hash1 = rf.readUnsignedInt();
        hash2 = rf.readUnsignedInt();
        hash3 = rf.readUnsignedInt();
        hash4 = rf.readUnsignedInt();
        hash5 = rf.readUnsignedInt();
        hash6 = rf.readUnsignedInt();
        hash7 = rf.readUnsignedInt();
        hash8 = rf.readUnsignedInt();
        hash9 = rf.readUnsignedInt();
        hash10 = rf.readUnsignedInt();
        name1 = rf.readString(32);
        name2 = rf.readString(32);
        name3 = rf.readString(32);
        name4 = rf.readString(32);
        name5 = rf.readString(32);
        name6 = rf.readString(32);
        name7 = rf.readString(32);
        name8 = rf.readString(32);
        name9 = rf.readString(32);
        name10 = rf.readString(32);
    }
}
