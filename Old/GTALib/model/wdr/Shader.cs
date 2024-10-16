using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Shader
{
    private int startOffset;

    private int VTable;
    private int BlockMapAdress;

    private int Unknown1;
    private byte Unknown2;
    private byte Unknown3;
    private int Unknown4;
    private int Unknown4_1;
    private int Unknown5;
    private int Unknown6;

    public int shaderParamOffsetsOffset;

    public int ShaderParamCount;
    private int Unknown8;

    public int shaderParamTypesOffset;

    public int shaderParamNameOffset;

    public int Hash;
    private int Unknown9;
    private int Unknown10;
    private int Unknown11;
    private int Unknown12;
    private int Unknown13;

    public SimpleArray<int> ShaderParamOffsets;
    public SimpleArray<Byte> ShaderParamTypes;
    public SimpleArray<int> ShaderParamNames;

    public ArrayList ShaderParams = new ArrayList();

    /* public Dictionary<ParamNameHash, IShaderParam> ShaderParams { get; private set; } public T
	 * GetInfoData<T>(ParamNameHash hash) where T : class, IShaderParam { IShaderParam value;
	 * ShaderParams.TryGetValue(hash, out value); return value as T; } */

    public Shader(ByteReader br)
    {
        startOffset = br.getCurrentOffset();

        VTable = br.readUInt32();
        BlockMapAdress = br.readOffset();

        Unknown1 = br.readUInt16();
        Unknown2 = br.readByte();
        Unknown3 = br.readByte();

        Unknown4 = br.readUInt16();
        Unknown4_1 = br.readUInt16();

        Unknown5 = br.readUInt32();

        shaderParamOffsetsOffset = br.readOffset();

        Unknown6 = br.readUInt32();
        ShaderParamCount = br.readUInt32();
        Unknown8 = br.readUInt32();

        shaderParamTypesOffset = br.readOffset();

        Hash = br.readUInt32();
        Unknown9 = br.readUInt32();
        Unknown10 = br.readUInt32();

        shaderParamNameOffset = br.readOffset();

        Unknown11 = br.readUInt32();
        Unknown12 = br.readUInt32();
        Unknown13 = br.readUInt32();

        int save = br.getCurrentOffset();

        br.setCurrentOffset(shaderParamOffsetsOffset);
        ShaderParamOffsets = new SimpleArray<int>(br, ShaderParamCount, 1);

        br.setCurrentOffset(shaderParamTypesOffset);
        ShaderParamTypes = new SimpleArray<Byte>(br, ShaderParamCount, 2);

        br.setCurrentOffset(shaderParamNameOffset);
        ShaderParamNames = new SimpleArray<int>(br, ShaderParamCount, 0);

        for (int i = 0; i < ShaderParamCount; i++)
        {
            if (ShaderParamOffsets.Values[i] != -1)
            {
                br.setCurrentOffset(ShaderParamOffsets.Values[i]);
                switch (ShaderParamTypes.Values[i])
                {
                    case 0:
                        ShaderParamTexture test = new ShaderParamTexture();
                        test.Read(br);
                        ShaderParams.Add(test);
                        break;
                    case 1:
                        ShaderParamVector test1 = new ShaderParamVector();
                        test1.Read(br);
                        ShaderParams.Add(test1);
                        break;
                    case 4:
                        ShaderParamMatrix test2 = new ShaderParamMatrix();
                        // test2.Read(br);
                        ShaderParams.Add(test2);
                        break;
                }
            }
            else
            {
                // Message.displayMsgLow("WTF Der zit een shader @ -1 " + Utils.getHexString(startOffset));
            }
        }

        // not sure what to do here

        /* ShaderParams = new Dictionary<ParamNameHash, IShaderParam>(ShaderParamCount); for (int i = 0; i <
		 * ShaderParamCount; i++) { try { var obj = ParamObjectFactory.Create((ParamType) ShaderParamTypes[i]);
		 * br.BaseStream.Seek(ShaderParamOffsets[i], SeekOrigin.Begin); obj.Read(br); ShaderParams.Add((ParamNameHash)
		 * ShaderParamNames[i], obj); } catch { ShaderParams.Add((ParamNameHash) ShaderParamNames[i], null); } } */
        br.setCurrentOffset(save);
    }

    public String[] getDataNames()
    {
        List<String> nameList = new List<string>();

        nameList.Add("VTable");
        nameList.Add("BlockMapAdress");
        nameList.Add("Unknown1");
        nameList.Add("Unknown2");
        nameList.Add("Unknown3");
        nameList.Add("Unknown4");
        nameList.Add("Unknown4_1");
        nameList.Add("Unknown5");
        nameList.Add("shaderParamOffsetsOffset");
        nameList.Add("Unknown6");
        nameList.Add("ShaderParamCount");
        nameList.Add("Unknown8");
        nameList.Add("shaderParamTypesOffset");
        nameList.Add("Hash");
        nameList.Add("Unknown9");
        nameList.Add("Unknown10");
        nameList.Add("shaderParamNameOffset");
        nameList.Add("Unknown11");
        nameList.Add("Unknown12");
        nameList.Add("Unknown13");

        nameList.Add("[ShaderParamOffsets]");
        for (int i = 0; i < ShaderParamOffsets.Count; i++)
        {
            nameList.Add("  ShaderParamOffset " + i);
        }

        nameList.Add("[ShaderParamTypes]");
        for (int i = 0; i < ShaderParamTypes.Count; i++)
        {
            nameList.Add("  ShaderParamType " + i);
        }

        nameList.Add("[ShaderParamNames]");
        for (int i = 0; i < ShaderParamNames.Count; i++)
        {
            nameList.Add("  ShaderParamName " + i);
        }

        String[] names = new String[nameList.Count];
        for (int i = 0; i < nameList.Count; i++)
        {
            names[i] = nameList[i];
        }

        return names;
    }

    public String[] getDataValues()
    {
        List<string> valueList = new List<string>();

        valueList.Add("" + VTable);
        valueList.Add("" + BlockMapAdress);
        valueList.Add("" + Unknown1);
        valueList.Add("" + Unknown2);
        valueList.Add("" + Unknown3);
        valueList.Add("" + Unknown4);
        valueList.Add("" + Unknown4_1);
        valueList.Add("" + Unknown5);
        valueList.Add("" + Utils.getHexString(shaderParamOffsetsOffset));
        valueList.Add("" + Unknown6);
        valueList.Add("" + ShaderParamCount);
        valueList.Add("" + Unknown8);
        valueList.Add("" + Utils.getHexString(shaderParamTypesOffset));
        valueList.Add("" + Hash);
        valueList.Add("" + Unknown9);
        valueList.Add("" + Unknown10);
        valueList.Add("" + Utils.getHexString(shaderParamNameOffset));
        valueList.Add("" + Unknown11);
        valueList.Add("" + Unknown12);
        valueList.Add("" + Unknown13);

        valueList.Add("" + ShaderParamOffsets.Count);
        for (int i = 0; i < ShaderParamOffsets.Count; i++)
        {
            valueList.Add("" + Utils.getHexString(ShaderParamOffsets.Values[i]));
        }

        valueList.Add("" + ShaderParamTypes.Count);
        for (int i = 0; i < ShaderParamTypes.Count; i++)
        {
            valueList.Add(Utils.getShaderType(ShaderParamTypes.Values[i]));
        }

        valueList.Add("" + ShaderParamNames.Count);
        for (int i = 0; i < ShaderParamNames.Count; i++)
        {
            valueList.Add(Utils.getShaderName(ShaderParamNames.Values[i]));
        }

        String[] values = new String[valueList.Count];

        for (int i = 0; i < valueList.Count; i++)
        {
            values[i] = (String)valueList[i];
        }

        return values;
    }

    public String getStartOffset()
    {
        return Utils.getStartOffset(startOffset);
    }
}
