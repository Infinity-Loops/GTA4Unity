using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_CARS : IDE_Item
{
    private int gameType;
    public String modelName;
    public String textureName;
    public String type;
    public String handlingID;

    public Item_CARS(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        line = line.Replace("\t", "");
        line = line.Replace(" ", "");
        Debug.Log("Car: " + line);
        String[] split = line.Split(",");
        modelName = split[0];
        textureName = split[1];
        type = split[2];
        handlingID = split[3];
        Debug.Log("Model Name: " + split[0]);
        Debug.Log("Texture Name: " + split[1]);
        Debug.Log("Type: " + split[2]);
        Debug.Log("HandLingID: " + split[3]);
        Debug.Log("Game Name: " + split[4]);
        Debug.Log("Anims: " + split[5]);
        Debug.Log("Anims2: " + split[6]);
        Debug.Log("Frq: " + split[7]);
        Debug.Log("MaxNum: " + split[8]);
        Debug.Log("Wheel Radius Front: " + split[9]);
        Debug.Log("Wheel Radius Rear: " + split[10]);
        Debug.Log("DefDirtLevel: " + split[11]);
        Debug.Log("Swankness: " + split[12]);
        Debug.Log("lodMult: " + split[13]);
        Debug.Log("Flags: " + split[14]);
        //System.out.println("Extra stuff?: " + split[15]);
    }
}
