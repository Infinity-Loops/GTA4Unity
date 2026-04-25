using System.IO;

public abstract class IPL_Item
{
    public abstract void Read(string line);
    public abstract void Read(BinaryReader reader);
    public abstract void Read(BinaryReader reader, GTAHashTable ini);
}
