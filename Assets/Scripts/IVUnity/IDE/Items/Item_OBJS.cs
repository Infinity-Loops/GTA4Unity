using RageLib.Common;
using UnityEngine;

public class Item_OBJS : IDE_Item
{
    public int      id;
    public string   modelName;
    public string   textureName;
    public int      objectCount;
    public float[]  drawDistance;
    public int      flag1;
    public int      flag2;
    public Vector3  boundsMin;
    public Vector3  boundsMax;
    public Vector4  boundsSphere;
    public string   wdd;

    public override void Read(ref LineParser p)
    {
        modelName    = p.ReadString();
        textureName  = p.ReadString();
        drawDistance = new[] { p.ReadFloat() };
        flag1        = p.ReadInt();
        flag2        = p.ReadInt();
        boundsMin    = p.ReadVector3();
        boundsMax    = p.ReadVector3();
        boundsSphere = p.ReadVector4();
        wdd          = p.ReadString();
        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }
}
