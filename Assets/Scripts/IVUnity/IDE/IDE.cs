using RageLib.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public enum IDEReader : int
{
    None = -1,
    i2DFX = 0,
    iANIM = 1,
    iCARS = 2,
    iHIER = 3,
    iMLO = 4,
    iOBJS = 5,
    iPATH = 6,
    iPEDS = 7,
    iTANM = 8,
    iTOBJ = 9,
    iTREE = 10,
    iTXDP = 11,
    iWEAP = 12,
}

public class IDE
{
    private StreamReader reader;
    private IDEReader readItem = IDEReader.None;

    public List<Item_OBJS> items_objs = new();
    public List<Item_TOBJ> items_tobj = new();
    public List<Item_TREE> items_tree = new();
    public List<Item_PATH> items_path = new();
    public List<Item_ANIM> items_anim = new();
    public List<Item_TANM> items_tanm = new();
    public List<Item_MLO> items_mlo = new();
    public List<Item_2DFX> items_2dfx = new();
    public List<Item_AMAT> items_amat = new();
    public List<Item_TXDP> items_txdp = new();
    public List<Item_CARS> items_cars = new();

    public IDE(string fileName)
    {
        reader = File.OpenText(fileName);

        string line = null;
        while ((line = reader.ReadLine()) != null)
        {
            if (readItem == IDEReader.None)
            {
                if (line.StartsWith("#"))
                {
                    //Comment
                }
                else if (line.StartsWith("2dfx"))
                {
                    readItem = IDEReader.i2DFX;
                }
                else if (line.StartsWith("anim"))
                {
                    readItem = IDEReader.iANIM;
                }
                else if (line.StartsWith("cars"))
                {
                    readItem = IDEReader.iCARS;
                }
                else if (line.StartsWith("hier"))
                {
                    readItem = IDEReader.iHIER;
                }
                else if (line.StartsWith("mlo"))
                {
                    readItem = IDEReader.iMLO;
                }
                else if (line.StartsWith("objs"))
                {
                    readItem = IDEReader.iOBJS;
                }
                else if (line.StartsWith("path"))
                {
                    readItem = IDEReader.iPATH;
                }
                else if (line.StartsWith("peds"))
                {
                    readItem = IDEReader.iPEDS;
                }
                else if (line.StartsWith("tanm"))
                {
                    readItem = IDEReader.iTANM;
                }
                else if (line.StartsWith("tobj"))
                {
                    readItem = IDEReader.iTOBJ;
                }
                else if (line.StartsWith("tree"))
                {
                    readItem = IDEReader.iTREE;
                }
                else if (line.StartsWith("txdp"))
                {
                    readItem = IDEReader.iTXDP;
                }
                else if (line.StartsWith("weap"))
                {
                    readItem = IDEReader.iWEAP;
                }
            }
            else
            {
                if (line.StartsWith("end"))
                {
                    readItem = IDEReader.None;
                }
                else if (line.StartsWith("#"))
                {
                    //Comment
                }
                else if (string.IsNullOrEmpty(line))
                {
                    //Empty line
                }
                else
                {
                    IDE_Item item = null;
                    switch (readItem)
                    {
                        case IDEReader.i2DFX:
                            item = new Item_2DFX();
                            break;
                        case IDEReader.iANIM:
                            item = new Item_ANIM();
                            break;
                        case IDEReader.iCARS:
                            item = new Item_CARS();
                            items_cars.Add((Item_CARS)item);
                            break;
                        case IDEReader.iHIER:
                            item = new Item_HIER();
                            break;
                        case IDEReader.iMLO:
                            item = new Item_MLO();
                            break;
                        case IDEReader.iOBJS:
                            item = new Item_OBJS();
                            items_objs.Add((Item_OBJS)item);
                            break;
                        case IDEReader.iPATH:
                            item = new Item_PATH();
                            break;
                        case IDEReader.iPEDS:
                            item = new Item_PEDS();
                            break;
                        case IDEReader.iTANM:
                            item = new Item_TANM();
                            break;
                        case IDEReader.iTOBJ:
                            item = new Item_TOBJ();
                            items_tobj.Add((Item_TOBJ)item);
                            break;
                        case IDEReader.iTREE:
                            item = new Item_TREE();
                            break;
                        case IDEReader.iTXDP:
                            item = new Item_TXDP();
                            items_txdp.Add((Item_TXDP)item);
                            break;
                        case IDEReader.iWEAP:
                            item = new Item_WEAP();
                            break;
                        default:

                            break;
                    }
                    item.Read(line);
                }
            }
        }

        reader.Close();
        reader.Dispose();
    }

    public IDE_Item FindItem(String name)
    {
        IDE_Item ret = null;
        if (items_objs.Count != 0)
        {
            int i = 0;
            Item_OBJS item = items_objs[i];
            while (!item.modelName.Equals(name))
            {
                if (i < items_objs.Count - 1)
                {
                    i++;
                    item = items_objs[i];
                }
                else
                {
                    break;
                }
            }
            if (item.modelName.Equals(name))
            {
                ret = items_objs[i];
            }
            else
            {
            }
        }
        return ret;
    }
}

public abstract class IDE_Item
{
    public abstract void Read(string line);
}

public class Item_2DFX : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_AMAT : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_ANIM : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_CARS : IDE_Item
{
    public string modelName;
    public string textureName;
    public string type;
    public string handlingID;
    public string gameName;
    public string anims;
    public string anims2;
    public string frequency;
    public string maxNumber;
    public string wheelRadiusFront;
    public string wheelRadiusRear;
    public string defDirtLevel;
    public string swankness;
    public string lodMult;
    public string flags;

    public override void Read(string line)
    {
        line = line.Replace("\t", "");
        line = line.Replace(" ", "");
        Debug.Log($"Car: {line}");
        string[] split = line.Split(",");

        modelName = split[0];
        textureName = split[1];
        type = split[2];
        handlingID = split[3];
        gameName = split[4];
        anims = split[5];
        anims2 = split[6];
        frequency = split[7];
        maxNumber = split[8];
        wheelRadiusFront = split[9];
        wheelRadiusRear = split[10];
        defDirtLevel = split[11];
        swankness = split[12];
        lodMult = split[13];
        flags = split[14];
        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }
}

public class Item_HIER : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_MLO : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_OBJS : IDE_Item
{
    public int id;
    public string modelName;
    public string textureName;
    public int objectCount;
    public float[] drawDistance;
    public int flag1;
    public int flag2;
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public Vector4 boundsSphere;
    public string wdd;

    public override void Read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(",");

        try
        {
            modelName = split[0];
            textureName = split[1];
            drawDistance = new float[1];
            drawDistance[0] = float.Parse(split[2]);
            flag1 = int.Parse(split[3]);
            flag2 = int.Parse(split[4]);
            boundsMin = new Vector3(float.Parse(split[5]), float.Parse(split[6]), float.Parse(split[7]));
            boundsMax = new Vector3(float.Parse(split[8]), float.Parse(split[9]), float.Parse(split[10]));
            boundsSphere = new Vector4(float.Parse(split[11]), float.Parse(split[12]), float.Parse(split[13]), float.Parse(split[14]));
            wdd = split[15];
        }
        catch
        {
            Debug.Log("Couldn't read beyond the end of obj stream");
        }
        //Debug.Log($"Object Found: {modelName}, WDD: {wdd}");
        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }
}

public class Item_PATH : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_PEDS : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_TANM : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_TOBJ : IDE_Item
{
    public int id;
    public string modelName;
    public string textureName;
    public int objectCount;
    public float[] drawDistance;
    public int flag1;
    public int flag2;
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public Vector4 boundsSphere;
    public string wdd;
    public int timedFlags;

    public override void Read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(",");
        modelName = split[0];
        textureName = split[1];
        drawDistance = new float[1];
        drawDistance[0] = float.Parse(split[2]);
        flag1 = int.Parse(split[3]);
        flag2 = int.Parse(split[4]);
        boundsMin = new Vector3(float.Parse(split[5]), float.Parse(split[6]), float.Parse(split[7]));
        boundsMax = new Vector3(float.Parse(split[8]), float.Parse(split[9]), float.Parse(split[10]));
        boundsSphere = new Vector4(float.Parse(split[11]), float.Parse(split[12]), float.Parse(split[13]), float.Parse(split[14]));
        wdd = split[15];
        timedFlags = int.Parse(split[16]);
        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }
}

public class Item_TREE : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Item_TXDP : IDE_Item
{
    public string texDic;
    public string texDicParent;

    public override void Read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(",");
        texDic = split[0];
        texDicParent = split[1];
    }
}

public class Item_WEAP : IDE_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}
