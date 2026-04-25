using RageLib.Common;
using UnityEngine;

public class Item_TOBJ : IDE_Item
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
    public int      timedFlags;

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
        timedFlags   = p.ReadInt();
        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }

    /// <summary>Implicit conversion to Item_OBJS used by the streaming layer to merge TOBJs
    /// into the main object dictionary (with flag2 set to indicate time-of-day skip).</summary>
    public static implicit operator Item_OBJS(Item_TOBJ item_TOBJ)
    {
        var obj = new Item_OBJS
        {
            id           = item_TOBJ.id,
            modelName    = item_TOBJ.modelName,
            textureName  = item_TOBJ.textureName,
            objectCount  = item_TOBJ.objectCount,
            drawDistance = item_TOBJ.drawDistance,
            flag1        = item_TOBJ.flag1,
            flag2        = item_TOBJ.flag2,
            boundsMin    = item_TOBJ.boundsMin,
            boundsMax    = item_TOBJ.boundsMax,
            boundsSphere = item_TOBJ.boundsSphere,
            wdd          = item_TOBJ.wdd,
        };
        return obj;
    }
}
