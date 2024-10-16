using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_CULL : IPL_Item
{
    private int gameType;

    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;
    public int unk1, unk2, unk3, unk4;
    public long hash;
    public String name;

    public Item_CULL(int gameType)
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
        unk2 = rf.readInt();
        unk3 = rf.readInt();
        unk4 = rf.readInt();
        hash = rf.readInt();
    }
    private void display()
    {
        Debug.Log("Position: " + posLowerLeft.x + ", " + posLowerLeft.y + ", " + posLowerLeft.z);
        Debug.Log("Rotation: " + posUpperRight.x + ", " + posUpperRight.y + ", " + posUpperRight.z);
        Debug.Log("Hash: " + hash);// + " name: " + name);
        Debug.Log("Unknown1: " + unk1);
        Debug.Log("Unknown2: " + unk2);
        Debug.Log("Unknown3: " + unk3);
        Debug.Log("Unknown4: " + unk4);
        Debug.Log("Name: " + name);
    }
    public override void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini)
    {
        posLowerLeft = rf.readVector3D();
        posUpperRight = rf.readVector3D();
        unk1 = rf.readInt();
        unk2 = rf.readInt();
        unk3 = rf.readInt();
        unk4 = rf.readInt();

        long tempHash = rf.readUnsignedInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = (string)ini["Hashes"][name]; // temp

        display();
    }
}
