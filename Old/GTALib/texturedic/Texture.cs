using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Texture
{
    public int width;
    public int height;
    public int compression;
    public int offset;
    public int conversionOffset;
    public int dataSize;
    public String difTexName;
    public String alphaTexName;
    public byte[] data;

    public Texture2D GetUnityTexture()
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
        tex.name = difTexName;
        tex.LoadImage(data, false);
        tex.Apply();

        return tex;
    }
}
