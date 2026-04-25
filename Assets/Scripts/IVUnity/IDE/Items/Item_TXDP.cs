public class Item_TXDP : IDE_Item
{
    public string texDic;
    public string texDicParent;

    public override void Read(ref LineParser p)
    {
        texDic       = p.ReadString();
        texDicParent = p.ReadString();
    }
}
