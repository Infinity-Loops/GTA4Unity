using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawableModel
{
    public int startOffset;

    public ShaderGroup shaderGroup;
    public Skeleton skeleton;

    public int shaderGroupOffset;
    public int skeletonOffset;

    public Vector4 Center;
    public Vector4 BoundsMin;
    public Vector4 BoundsMax;

    int[] modelOffsets;
    public int levelOfDetailCount;

    public PtrCollection<Model2>[] mModelCollection;

    public Vector4 AbsoluteMax;

    private int Unk1;     // either 1 or 9

    private int Neg1;
    private int Neg2;
    private int Neg3;

    private float Unk2;

    private int Unk3;
    private int Unk4;
    private int Unk5;

    private int Unk6;  // This should be a CSimpleCollection
    private int Unk7;

    public void readSystemMemory(ByteReader br)
    {

        startOffset = br.getCurrentOffset();

        // Message.displayMsgHigh("VTable: " + br.ReadUInt32());
        // Message.displayMsgHigh("BlockMapAdress: " + br.ReadOffset());

        shaderGroupOffset = br.readOffset();
        skeletonOffset = br.readOffset();

        // Message.displayMsgHigh("ShaderGroupOffset: " + shaderGroupOffset);
        // Message.displayMsgHigh("skeletonOffset: " + skeletonOffset);

        Center = br.readVector();
        Debug.Log($"Center: {Center}");
        BoundsMin = br.readVector();
        Debug.Log($"BoundsMin: {BoundsMin}");
        BoundsMax = br.readVector();
        Debug.Log($"BoundsMax: {BoundsMax}");

        levelOfDetailCount = 0;
        modelOffsets = new int[4];
        for (int i = 0; i < 4; i++)
        {
            modelOffsets[i] = br.readOffset();
            if (modelOffsets[i] != -1)
            {
                // Message.displayMsgHigh("Level " + i + " at offset " + modelOffsets[i]);
                levelOfDetailCount++;
            }
        }
        // Message.displayMsgHigh("Level of detail: " + levelOfDetailCount);

        AbsoluteMax = br.readVector();
        Debug.Log($"AbsoluteMax: {AbsoluteMax}");

        Unk1 = br.readUInt32();

        Neg1 = br.readUInt32();
        Neg2 = br.readUInt32();
        Neg3 = br.readUInt32();

        Unk2 = br.readFloat();

        Unk3 = br.readUInt32();
        Unk4 = br.readUInt32();
        Unk5 = br.readUInt32();

        // Collection<LightAttrs>
        Unk6 = br.readUInt32();
        Unk7 = br.readUInt32();

        // Message.displayMsgHigh("Unknown: " + Unk1);

        // Message.displayMsgHigh("Neg: " + Neg1 + ", " + Neg2 + ", " + Neg3);

        // Message.displayMsgHigh("Unknown float: " + Unk2);

        // Message.displayMsgHigh("Unknown: " + Unk3 + ", " + Unk4 + ", " + Unk5);

        // Message.displayMsgHigh("Unknown: " + Unk6 + ", " + Unk7);

        if (shaderGroupOffset != -1)
        {
            // Message.displayMsgHigh("Setting shader offset " + Utils.getHexString(shaderGroupOffset));
            br.setCurrentOffset(shaderGroupOffset);
            shaderGroup = new ShaderGroup(br);
        }

        if (skeletonOffset != -1)
        {
            br.setCurrentOffset(skeletonOffset);
            skeleton = new Skeleton(/* br */);
        }

        // Message.displayMsgHigh("Created new PtrCollection");
        mModelCollection = new PtrCollection<Model2>[levelOfDetailCount];
        for (int i = 0; i < levelOfDetailCount; i++)
        {
            Debug.Log($"PointerCollectionOffset: {modelOffsets[i]}");
            br.setCurrentOffset(modelOffsets[i]);
            mModelCollection[i] = new PtrCollection<Model2>(br, 1);
        }

    }

    public String[] getDataNames()
    {
        String[] names = new String[(17 + levelOfDetailCount)];
        int i = 0;
        names[i] = "shaderGroupOffset";
        i++;
        names[i] = "skeletonOffset";
        i++;
        names[i] = "Center";
        i++;
        names[i] = "BoundsMin";
        i++;
        names[i] = "BoundsMax";
        i++;

        names[i] = "levelOfDetailCount";
        i++;

        for (int i2 = 0; i2 < levelOfDetailCount; i2++)
        {
            names[i] = "  DetailOffset " + (i2 + 1);
            i++;
        }

        names[i] = "AbsoluteMax";
        i++;

        names[i] = "Unk1";     // either 1 or 9
        i++;

        names[i] = "Neg1";
        i++;
        names[i] = "Neg2";
        i++;
        names[i] = "Neg3";
        i++;

        names[i] = "Unk2";
        i++;

        names[i] = "Unk3";
        i++;
        names[i] = "Unk4";
        i++;
        names[i] = "Unk5";
        i++;

        names[i] = "Unk6";  // This should be a CSimpleCollection
        i++;
        names[i] = "Unk7";

        return names;
    }

    public String[] getDataValues()
    {
        String[] values = new String[(17 + levelOfDetailCount)];
        int i = 0;
        values[i] = Utils.getHexString(shaderGroupOffset);
        i++;
        values[i] = Utils.getHexString(skeletonOffset);
        i++;

        values[i] = "" + Center;
        i++;
        values[i] = "" + BoundsMin;
        i++;
        values[i] = "" + BoundsMax;
        i++;

        values[i] = "" + levelOfDetailCount;
        i++;

        for (int i2 = 0; i2 < levelOfDetailCount; i2++)
        {
            values[i] = Utils.getHexString(modelOffsets[i2]);
            i++;
        }

        values[i] = "" + AbsoluteMax;
        i++;

        values[i] = "" + Unk1;     // either 1 or 9
        i++;

        values[i] = "" + Neg1;
        i++;
        values[i] = "" + Neg2;
        i++;
        values[i] = "" + Neg3;
        i++;

        values[i] = "" + Unk2;
        i++;

        values[i] = "" + Unk3;
        i++;
        values[i] = "" + Unk4;
        i++;
        values[i] = "" + Unk5;
        i++;

        values[i] = "" + Unk6;  // This should be a CSimpleCollection
        i++;
        values[i] = "" + Unk7;

        return values;
    }
}
