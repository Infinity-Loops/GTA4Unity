using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Item_OBJS : IDE_Item
{
    public int id; // III, VC, SA
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

    private int gameType;

    public Item_OBJS(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(',');

        switch (gameType)
        {
            case Constants.gIV:
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
                break;

            case Constants.gSA:
                id = int.Parse(split[0]);
                modelName = split[1];
                textureName = split[2];
                drawDistance = new float[1];
                drawDistance[0] = float.Parse(split[3]);
                flag1 = int.Parse(split[4]);
                break;

            default: // III, VC share the same format
                id = int.Parse(split[0]);
                modelName = split[1];
                textureName = split[2];
                objectCount = int.Parse(split[3]);
                drawDistance = new float[objectCount];
                for (int i = 0; i < objectCount; i++)
                {
                    drawDistance[i] = float.Parse(split[4 + i]);
                }
                flag1 = int.Parse(split[4 + objectCount]);
                break;
        }

        display();
    }

    public void display()
    {
        //if (gameType != Constants.gIV)
        //{
        //    //Message.displayMsgHigh("ID: " + id);
        //}
        ////Message.displayMsgHigh("ModelName: " + modelName);
        ////Message.displayMsgHigh("TextureName: " + textureName);
        //if (gameType == Constants.gIII || gameType == Constants.gVC)
        //{
        //    //Message.displayMsgHigh("ObjectCount: " + objectCount);
        //    for (int i = 0; i < objectCount; i++)
        //    {
        //        //Message.displayMsgHigh("DrawDistance" + i + ": " + drawDistance[i]);
        //    }
        //}
        //if (gameType == Constants.gIV)
        //{
        //    //Message.displayMsgHigh("Flag: " + flag1);
        //    //Message.displayMsgHigh("Flag2: " + flag2);
        //    //Message.displayMsgHigh("BoundsMAX: " + boundsMax.x + ", " + boundsMax.y + ", " + boundsMax.z);
        //    //Message.displayMsgHigh("BoundsMIN: " + boundsMin.x + ", " + boundsMin.y + ", " + boundsMin.z);
        //    //Message.displayMsgHigh("BoundsSphere: " + boundsSphere.x + ", " + boundsSphere.y + ", " + boundsSphere.z +
        //    //", " + boundsSphere.w);
        //    //Message.displayMsgHigh("WDD: " + WDD);
        //}
    }
}
