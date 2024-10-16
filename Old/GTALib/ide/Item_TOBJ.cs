using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Item_TOBJ : IDE_Item
{
    public String modelName; // III, VC, SA, IV
    public String textureName; // III, VC, SA, IV
    public int objectCount; // III, VC, SA
    public float[] drawDistance; // III, VC, SA, IV
    public int flag1; // III, VC, SA, IV
    public int flag2; // IV
    public Vector3 boundsMin = new Vector3(0.0f, 0.0f, 0.0f); // IV
    public Vector3 boundsMax = new Vector3(0.0f, 0.0f, 0.0f); // IV
    public Vector4 boundsSphere = new Vector4(0.0f, 0.0f, 0.0f, 0.0f); // IV
    public String WDD; // IV
    public int timedFlags; // ?

    private int gameType;

    public Item_TOBJ(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(',');

        modelName = split[0];
        textureName = split[1];
        drawDistance = new float[1];
        drawDistance[0] = float.Parse(split[2]);
        flag1 = int.Parse(split[3]);
        flag2 = int.Parse(split[4]);
        boundsMin = new Vector3(float.Parse(split[5]), float.Parse(split[6]), float.Parse(split[7]));
        boundsMax = new Vector3(float.Parse(split[8]), float.Parse(split[9]), float.Parse(split[10]));
        boundsSphere = new Vector4(float.Parse(split[11]), float.Parse(split[12]), float.Parse(split[13]), float.Parse(split[14]));
        WDD = split[15];
        timedFlags = int.Parse(split[16]);
    }
}
