using RageLib.Common;

public class Item_PEDS : IDE_Item
{
    public string ModelName;
    public string PhysicsName;
    public string PedType;
    public string MovementGroup;
    public string GestureGroup;
    public string PhoneGestureGroup;
    public string FacialGroup;
    public string VisemeGroup;
    public string Flags;
    public string MovementGroupAlt;

    public override void Read(ref LineParser p)
    {
        ModelName = p.ReadString();
        PhysicsName = p.ReadString();
        PedType = p.ReadString();
        MovementGroup = p.ReadString();
        GestureGroup = p.ReadString();
        PhoneGestureGroup = p.ReadString();
        FacialGroup = p.ReadString();
        VisemeGroup = p.ReadString();
        Flags = p.ReadString();
        MovementGroupAlt = p.ReadString();
    }
}
