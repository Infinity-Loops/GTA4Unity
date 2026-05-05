using RageLib.Common;

public class Item_AGRPS : IDE_Item
{
    public string ModelName;
    public string AudioGroup;
    public int HeadIndex;

    public override void Read(ref LineParser p)
    {
        ModelName = p.ReadString();
        AudioGroup = p.ReadString();
        HeadIndex = p.ReadInt();
    }
}
