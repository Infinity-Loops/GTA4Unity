using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VertexDeclaration
{
    public int UsageFlags;
    public int Stride;
    public byte AlterateDecoder;
    public byte Type;
    public long DeclarationTypes;

    public VertexDeclaration(ByteReader br)
    {
        Read(br);
    }

    public void Read(ByteReader br)
    {
        UsageFlags = br.readUInt32();
        Stride = br.readUInt16();
        AlterateDecoder = br.readByte();
        Type = br.readByte();

        br.readUInt32();
        br.readUInt32();
        // DeclarationTypes = br.ReadUInt64();
    }

    public String[] getDataNames()
    {
        List<String> nameList = new List<String>();

        nameList.Add("UsageFlags");
        nameList.Add("Stride");
        nameList.Add("AlterateDecoder");
        nameList.Add("Type");

        String[] names = new String[nameList.Count];
        for (int i = 0; i < nameList.Count; i++)
        {
            names[i] = nameList[i];
        }

        return names;
    }

    public String[] getDataValues()
    {
        List<String> nameList = new List<String>();

        nameList.Add("" + UsageFlags);
        nameList.Add("" + Stride);
        nameList.Add("" + AlterateDecoder);
        nameList.Add("" + Type);

        String[] names = new String[nameList.Count];
        for (int i = 0; i < nameList.Count; i++)
        {
            names[i] = nameList[i];
        }

        return names;
    }
}
