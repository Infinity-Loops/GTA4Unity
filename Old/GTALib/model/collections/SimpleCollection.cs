using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = System.Object;

public class SimpleCollection<T>
{
    public List<T> Values;

    public int Count;
    public int Size;
    public int type;

    public SimpleCollection(ByteReader br, int type)
    {
        this.type = type;
        Read(br);
    }

    public void Read(ByteReader br)
    {
        int offset = br.readOffset();
        // Message.displayMsgLow("Offset: " + offset);

        Count = br.readUInt16();
        Size = br.readUInt16();

        Values = new List<T>(Count);

        int save = br.getCurrentOffset();

        br.setCurrentOffset(offset);

        for (int i = 0; i < Count; i++)
        {
            Values.Add(ReadData(br));
        }

        br.setCurrentOffset(save);
    }

    public T ReadData(ByteReader br)
    {
        switch (type)
        {
            case 0:
                Object data = br.readUInt32();
                // Message.displayMsgLow("Data: " + data);
                return (T)data;
            default:
                Object data2 = br.readUInt32();
                return (T)data2;
        }
    }

}
