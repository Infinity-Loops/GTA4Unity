using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderParamTexture
{
    public int startOffset;
    private int VTable;
    private int Unknown1;
    private int Unknown2;
    private int Unknown3;
    private int Unknown4;
    private int Unknown5;
    private int TextureNameOffset;
    private int Unknown7;

    public String TextureName = "";

    public void Read(ByteReader br)
    {
        startOffset = br.getCurrentOffset();
        VTable = br.readUInt32();

        Unknown1 = br.readUInt32();
        Unknown2 = br.readUInt16();
        Unknown3 = br.readUInt16();
        Unknown4 = br.readUInt32();
        Unknown5 = br.readUInt32();
        TextureNameOffset = br.readOffset();
        Unknown7 = br.readUInt32();

        br.setCurrentOffset(TextureNameOffset);
        TextureName = br.readNullTerminatedString();
        // Message.displayMsgHigh("Texture name: " + TextureName);
    }

    public String[] getDataNames()
    {
        List<String> nameList = new List<string>();

        nameList.Add("VTable");
        nameList.Add("Unknown1");
        nameList.Add("Unknown2");
        nameList.Add("Unknown3");
        nameList.Add("Unknown4");
        nameList.Add("Unknown5");
        nameList.Add("TextureNameOffset");
        nameList.Add("Unknown7");

        nameList.Add("TextureName");

        String[] names = new String[nameList.Count];
        for (int i = 0; i < nameList.Count; i++)
        {
            names[i] = nameList[i];
        }

        return names;
    }

    public String[] getDataValues()
    {
        List<String> valueList = new List<string>();

        valueList.Add("" + VTable);
        valueList.Add("" + Unknown1);
        valueList.Add("" + Unknown2);
        valueList.Add("" + Unknown3);
        valueList.Add("" + Unknown4);
        valueList.Add("" + Unknown5);
        valueList.Add("" + Utils.getHexString(TextureNameOffset));
        valueList.Add("" + Unknown7);

        valueList.Add("" + TextureName);

        String[] values = new String[valueList.Count];
        for (int i = 0; i < valueList.Count; i++)
        {
            values[i] = valueList[i];
        }

        return values;
    }

    public String getStartOffset()
    {
        return Utils.getStartOffset(startOffset);
    }
}
