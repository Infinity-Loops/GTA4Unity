using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderParamVector
{
    public int startOffset;
    public Vector4 Data;

    public void Read(ByteReader br)
    {
        startOffset = br.getCurrentOffset();
        Data = br.readVector();
        Debug.Log($"Shader Vector: {Data}");
    }

    public String[] getDataNames()
    {
        List<String> nameList = new List<string>();

        nameList.Add("Unkown Vector");

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

        valueList.Add(Data.x + ", " + Data.y + ", " + Data.z + ", " + Data.w);

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
