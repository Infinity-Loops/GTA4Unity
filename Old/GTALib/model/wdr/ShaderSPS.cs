using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderSPS
{
    private int startOffset;

    private int Unknown14;
    private int Unknown15;
    private int Unknown16;
    private int Unknown17;

    public String ShaderName;
    public String shaderSPS;

    public void Read(ByteReader br)
    {
        startOffset = br.getCurrentOffset();

        Shader shader = new Shader(br);

        int shaderNamePtr = br.readOffset();
        int shaderSpsPtr = br.readOffset();

        // Message.displayMsgHigh("ShaderNamePtr: " + shaderNamePtr);
        // Message.displayMsgHigh("ShaderSpsPtr: " + shaderSpsPtr);

        Unknown14 = br.readUInt32();
        Unknown15 = br.readUInt32();
        Unknown16 = br.readUInt32();
        Unknown17 = br.readUInt32();

        // Data:

        br.setCurrentOffset(shaderNamePtr);
        ShaderName = br.readNullTerminatedString();

        // Message.displayMsgHigh("Shadername: " + ShaderName);

        br.setCurrentOffset(shaderSpsPtr);
        shaderSPS = br.readNullTerminatedString();

        // Message.displayMsgHigh("ShaderSPS: " + ShaderSPS);
    }

    public String getStartOffset()
    {
        return Utils.getStartOffset(startOffset);
    }
}
