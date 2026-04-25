using RageLib.Common;

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

    public override void Read(ref LineParser p)
    {
        modelName        = p.ReadString();
        textureName      = p.ReadString();
        type             = p.ReadString();
        handlingID       = p.ReadString();
        gameName         = p.ReadString();
        anims            = p.ReadString();
        anims2           = p.ReadString();
        frequency        = p.ReadString();
        maxNumber        = p.ReadString();
        wheelRadiusFront = p.ReadString();
        wheelRadiusRear  = p.ReadString();
        defDirtLevel     = p.ReadString();
        swankness        = p.ReadString();
        lodMult          = p.ReadString();
        flags            = p.ReadString();
        Hashes.table.AddData("hashes", Hasher.Hash(modelName).ToString(), modelName);
    }
}
