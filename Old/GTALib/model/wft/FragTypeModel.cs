using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FragTypeModel
{
    public int VTable;
    public int blockMapAdress;
    public int offset1;
    public int offset2;

    public List<DrawableModel> drawables = new List<DrawableModel>();

    public void read(ByteReader br)
    {
        Debug.Log("--------------------\nHeader\n--------------------");
        VTable = br.readUInt32();
        Debug.Log("VTable: " + VTable);
        blockMapAdress = br.readOffset();
        Debug.Log("Block: " + Utils.getHexString(blockMapAdress));

        float unkFloat1 = br.readFloat();
        float unkFloat2 = br.readFloat();
        Debug.Log("UNKFloat1: " + unkFloat1);
        Debug.Log("UNKFloat2: " + unkFloat2);

        for (int i = 0; i < 10; i++)
        {
            var vector = br.readVector();
            Debug.Log($"UNK Vec {i}: {vector}");
        }

        offset1 = br.readOffset();
        offset2 = br.readOffset();

        Debug.Log("PackString Offset: " + Utils.getHexString(offset1));
        int save = br.getCurrentOffset();
        br.setCurrentOffset(offset1);
        Debug.Log("PackString: " + br.readNullTerminatedString());
        br.setCurrentOffset(save);

        Debug.Log("Drawable: " + Utils.getHexString(offset2));
        save = br.getCurrentOffset();
        br.setCurrentOffset(offset2);

        DrawableModel drwbl = new DrawableModel();
        drwbl.readSystemMemory(br);
        drawables.Add(drwbl);

        br.setCurrentOffset(save);

        int zero1 = br.readUInt32();
        int zero2 = br.readUInt32();
        int zero3 = br.readUInt32();
        int max1 = br.readUInt32();
        int zero4 = br.readUInt32();
        Debug.Log("Zero1: " + zero1);
        Debug.Log("Zero2: " + zero2);
        Debug.Log("Zero3: " + zero3);
        Debug.Log("Max1: " + max1);
        Debug.Log("Zero4: " + zero4);

        int offset3 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset3));
        save = br.getCurrentOffset();
        br.setCurrentOffset(offset3);
        int off = br.readOffset();
        Debug.Log("Off = " + Utils.getHexString(off));
        while (off != -1)
        {
            int save2 = br.getCurrentOffset();
            br.setCurrentOffset(off);
            String name = br.readNullTerminatedString();
            Debug.Log("Name: " + name);
            br.setCurrentOffset(save2);
            Debug.Log(Utils.getHexString(br.getCurrentOffset()));
            off = br.readOffset();
        }
        br.setCurrentOffset(save);

        int offset4 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset4));
        int childListOffset = br.readOffset();
        Debug.Log("ChildListOffset: " + Utils.getHexString(childListOffset));

        int zero5 = br.readUInt32();
        int zero6 = br.readUInt32();
        int zero7 = br.readUInt32();

        int offset6 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset6));

        int zero8 = br.readUInt32();

        int offset7 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset7));
        int offset8 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset8));
        int offset9 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset9));
        int offset10 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset10));

        int zero9 = br.readUInt32();
        int zero10 = br.readUInt32();
        int zero11 = br.readUInt32();

        int offset11 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset11));
        int zero12 = br.readUInt32();
        int zero13 = br.readUInt32();
        int zero14 = br.readUInt32();
        int zero15 = br.readUInt32();

        int offset12 = br.readOffset();
        Debug.Log("Unk offset: " + Utils.getHexString(offset12));

        Debug.Log("--------------------\nChildList\n--------------------");
        save = br.getCurrentOffset();
        br.setCurrentOffset(childListOffset);
        int childOffset = br.readOffset();
        while (childOffset != -1)
        {
            int save2 = br.getCurrentOffset();
            Debug.Log("ChildOffset: " + Utils.getHexString(childOffset));
            if (childOffset < 0x0F0000)
            {
                br.setCurrentOffset(childOffset);
                FragTypeChild ftc = new FragTypeChild(br);
                if (ftc.drwblOffset != -1)
                {
                    br.setCurrentOffset(ftc.drwblOffset);
                    drwbl = new DrawableModel();
                    drwbl.readSystemMemory(br);
                    drawables.Add(drwbl);
                }
                br.setCurrentOffset(save2);
            }
            childOffset = br.readOffset();
        }
        br.setCurrentOffset(save);
    }
}
