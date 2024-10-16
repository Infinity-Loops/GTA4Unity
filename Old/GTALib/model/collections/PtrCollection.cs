using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PtrCollection<T>
{
    private const int modelCollection = 1;
    private const int geometry = 2;
    private const int shaderfx = 3;

    private int[] _itemOffsets;
    public List<T> _items;

    private int startOffset;

    public int ptrListOffset;

    public int Count;
    public int Size;

    private int type;
    private ByteReader br;

    public PtrCollection()
    {
    }

    public PtrCollection(ByteReader br, int type)
    {
        this.type = type;
        this.br = br;
        Read();
    }

    private void Read()
    {
        startOffset = br.getCurrentOffset();
        ptrListOffset = br.readOffset();

        Count = br.readUInt16();
        Size = br.readUInt16();

        _itemOffsets = new int[Count];
        _items = new List<T>();

        int save = br.getCurrentOffset();

        br.setCurrentOffset(ptrListOffset);

        for (int i = 0; i < Count; i++)
        {
            _itemOffsets[i] = br.readOffset();
            // Message.displayMsgLow("Item offset " + i + ": " + Utils.getHexString(_itemOffsets[i]));
        }

        for (int i = 0; i < Count; i++)
        {
            br.setCurrentOffset(_itemOffsets[i]);

            T item = getType();

            _items.Add(item);
        }

        br.setCurrentOffset(save);
    }

    private T getType()
    {
        switch (type)
        {
            case modelCollection:
                Model2 model = new Model2();
                model.Read(br);
                return (T)(object)model;
            case geometry:
                Geometry geo = new Geometry();
                geo.Read(br);
                return (T)(object)geo;
            case shaderfx:
                ShaderFx sf = new ShaderFx();
                sf.Read(br);
                return (T)(object)sf;
            default:
                Model2 model2 = new Model2();
                return (T)(object)model2;
        }
    }

    public String[] getDataNames()
    {
        String[] names = new String[4 + Count];
        int i = 0;
        names[i] = "ptrListOffset";
        i++;
        names[i] = "Count";
        i++;
        names[i] = "Size";
        i++;

        names[i] = "[Start PtrList]";
        i++;
        for (int i2 = 0; i2 < Count; i2++)
        {
            names[i] = "  Pointer " + (i2 + 1) + _items;
            i++;
        }

        return names;
    }

    public String[] getDataValues()
    {
        String[] values = new String[4 + Count];
        int i = 0;
        values[i] = Utils.getHexString(ptrListOffset);
        i++;
        values[i] = "" + Count;
        i++;
        values[i] = "" + Size;
        i++;

        values[i] = "";
        i++;
        for (int i2 = 0; i2 < Count; i2++)
        {
            values[i] = Utils.getHexString(_itemOffsets[i2]);
            i++;
        }

        return values;
    }

    public String getStartOffset()
    {
        return Utils.getStartOffset(startOffset);
    }
}
