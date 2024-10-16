using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Model2
{
    public int startOffset;

    public int VTable;
    public PtrCollection<Geometry> Geometries;
    public int unknownVectorOffsets;
    public int materialMappingOffset;
    private int Unknown1; // the four following really should be bytes
    private int Unknown2;
    private int Unknown3;
    private int GeoCount;
    public SimpleArray<Vector4> UnknownVectors;
    public SimpleArray<int> ShaderMappings;

    public void Read(ByteReader br)
    {
        startOffset = br.getCurrentOffset();

        VTable = br.readUInt32();

        Geometries = new PtrCollection<Geometry>(br, 2);

        unknownVectorOffsets = br.readOffset();
        materialMappingOffset = br.readOffset();

        // Message.displayMsgHigh("UnkownVectorOffsets: " + unknownVectorOffsets);
        // Message.displayMsgHigh("MaterialMappingOffset: " + materialMappingOffset);

        Unknown1 = br.readUInt16();
        Unknown2 = br.readUInt16();

        Unknown3 = br.readUInt16();
        GeoCount = br.readUInt16();

        //

        br.setCurrentOffset(unknownVectorOffsets);
        UnknownVectors = new SimpleArray<Vector4>(br, 4, 4);

        br.setCurrentOffset(materialMappingOffset);
        ShaderMappings = new SimpleArray<int>(br, Geometries.Count, 3);
    }

    public String toString()
    {
        return "Model";
    }

    public String[] getDataNames()
    {
        String[] names = new String[9 + UnknownVectors.Count + ShaderMappings.Count];
        int i = 0;
        names[i] = "VTable";
        i++;
        names[i] = "unknownVectorOffsets";
        i++;
        names[i] = "materialMappingOffset";
        i++;
        names[i] = "Unknown1";
        i++; // the four following really should be bytes
        names[i] = "Unknown2";
        i++;
        names[i] = "Unknown3";
        i++;
        names[i] = "GeoCount";
        i++;
        names[i] = "[UnknownVectors]";
        i++;
        for (int i2 = 0; i2 < UnknownVectors.Count; i2++)
        {
            names[i] = "  Vector " + (i2 + 1);
            i++;
        }
        names[i] = "[ShaderMappings]";
        i++;
        for (int i2 = 0; i2 < ShaderMappings.Count; i2++)
        {
            names[i] = "  ShaderMapping " + (i2 + 1);
            i++;
        }

        // for(int i2 = 0; i2 < Count; i2++){
        // names[i] = "  Pointer " + (i2+1) + _items; i++;
        // }

        return names;
    }

    public String[] getDataValues()
    {
        String[] values = new String[9 + UnknownVectors.Count + ShaderMappings.Count];
        int i = 0;
        values[i] = "" + VTable;
        i++;
        values[i] = Utils.getHexString(unknownVectorOffsets);
        i++;
        values[i] = Utils.getHexString(materialMappingOffset);
        i++;
        values[i] = "" + Unknown1;
        i++; // the four following really should be bytes
        values[i] = "" + Unknown2;
        i++;
        values[i] = "" + Unknown3;
        i++;
        values[i] = "" + GeoCount;
        i++;
        values[i] = "";
        i++;
        for (int i2 = 0; i2 < UnknownVectors.Count; i2++)
        {
            values[i] = "" + UnknownVectors.Values[i2];
            i++;
        }
        values[i] = "";
        i++;
        for (int i2 = 0; i2 < ShaderMappings.Count; i2++)
        {
            values[i] = "" + ShaderMappings.Values[i2];
            i++;
        }

        // for(int i2 = 0; i2 < Count; i2++){
        // names[i] = "  Pointer " + (i2+1) + _items; i++;
        // }

        return values;
    }

    public String getStartOffset()
    {
        return Utils.getStartOffset(startOffset);
    }
}
