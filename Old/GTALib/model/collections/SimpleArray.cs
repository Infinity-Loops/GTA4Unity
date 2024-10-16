using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = System.Object;
public class SimpleArray<T>
{
    public List<T> Values;
    public int Count;
    public int type;

    private const int ReadUInt32 = 0;
    private const int ReadOffset = 1;
    private const int ReadByte = 2;
    private const int ReadShort = 3;
    private const int ReadVector4 = 4;

    public SimpleArray(ByteReader br, int count, int type)
    {
        this.Count = count;
        this.type = type;
        Read(br);
    }

    public void Read(ByteReader br)
    {
        Values = new List<T>(Count);

        for (int i = 0; i < Count; i++)
        {
            Values.Add(ReadData(br));
        }
    }

    public T ReadData(ByteReader br)
    {

        switch (type)
        {
            case ReadUInt32:
                Object data = br.readUInt32();
                // Message.displayMsgLow("Data: " + data);
                return (T)data;
            case ReadOffset:
                Object offset = br.readOffset();
                // Message.displayMsgLow("Offset: " + offset);
                return (T)offset;
            case ReadByte:
                Object Byte = br.readByte();
                // Message.displayMsgLow("Byte: " + Byte);
                return (T)Byte;
            case ReadShort:
                Object Short = br.readUInt16();
                // Message.displayMsgLow("Short: " + Short);
                return (T)Short;
            case ReadVector4:
                Vector4 vec = br.readVector();
                Debug.Log($"Simple Array: {vec}");
                return (T)(System.Object)vec;
            default:
                Object data2 = br.readUInt32();
                return (T)data2;
        }
    }
}
