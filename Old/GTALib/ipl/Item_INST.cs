using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_INST : IPL_Item
{
    public int id; // III, VC, SA
    public String name = ""; // III, VC, SA, IV(Hash)
    public int hash = 0;
    public int interior; // VC, SA
    public Vector3 position = new Vector3(0.0f, 0.0f, 0.0f); // III, VC, SA,
                                                               // IV
    public Vector3 scale = new Vector3(0.0f, 0.0f, 0.0f); // III, VC
    public Vector4 rotation = new Vector4(0.0f, 0.0f, 0.0f, 1.0f); // III, VC,
                                                                     // SA,
                                                                     // IV
    public int lod; // SA, IV
    public int unknown1, unknown2; // IV
    public float unknown3; // IV
    public float drawDistance = 300.0f; // default value

    public Vector4 axisAngle = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
    private bool hidden = false;
    public bool selected = false;
    public int glListID;

    private int gameType = Constants.gIV;

    public Item_INST(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(',');

        switch (gameType)
        {
            case Constants.gSA:
                id = int.Parse(split[0]);
                name = split[1];
                interior = int.Parse(split[2]);
                position = new Vector3(float.Parse(split[3]), float.Parse(split[4]), float.Parse(split[5]));
                rotation = new Vector4(float.Parse(split[6]), float.Parse(split[7]), float.Parse(split[8]), float.Parse(split[9]));
                lod = int.Parse(split[10]);
                break;

            case Constants.gVC:
                id = int.Parse(split[0]);
                name = split[1];
                interior = int.Parse(split[2]);
                position = new Vector3(float.Parse(split[3]), float.Parse(split[4]), float.Parse(split[5]));
                scale = new Vector3(float.Parse(split[6]), float.Parse(split[7]), float.Parse(split[8]));
                rotation = new Vector4(float.Parse(split[9]), float.Parse(split[10]), float.Parse(split[11]), float.Parse(split[12]));
                break;

            case Constants.gIII:
                id = int.Parse(split[0]);
                name = split[1];
                position = new Vector3(float.Parse(split[2]), float.Parse(split[3]), float.Parse(split[4]));
                scale = new Vector3(float.Parse(split[5]), float.Parse(split[6]), float.Parse(split[7]));
                rotation = new Vector4(float.Parse(split[8]), float.Parse(split[9]), float.Parse(split[10]), float.Parse(split[11]));
                break;
        }

        axisAngle = rotation.getAxisAngle();
    }

    public override void read(ReadFunctions rf)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini)
    {
        position = new Vector3(rf.readFloat(), rf.readFloat(), rf.readFloat());
        rotation = new Vector4(rf.readFloat(), rf.readFloat(), rf.readFloat(), rf.readFloat());

        long tempHash = rf.readUnsignedInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = (string)ini["Hashes"][name]; // temp
        if (name == null)
        {
            name = "";
            Debug.LogError("ERROR Hash bestaat niet");
        }

        // Message.displayMsgLow("iName: " + name);

        unknown1 = rf.readInt();
        // System.out.println("Unknown1: " + unknown1);
        lod = rf.readInt();
        unknown2 = rf.readInt();
        // System.out.println("Unknown2: " + unknown2);
        unknown3 = rf.readFloat();
        // System.out.println("Unknown3: " + unknown3);
        // this.display();
    }
}
