using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureDic
{
    private String fileName;
    private int gameType;

    public int[] textureId;
    public String[] texName;
    public int textureCount = 0;
    public int flags = 0;
    public int size = 0;
    public List<Texture> textures = new List<Texture>();

    public ByteReader br = null;

    public int fileSize = -1;
    private bool compressed = true;

    public TextureDic(String fileName, ByteReader br, int gameType, int fileSize)
    {
        this.fileName = fileName;
        this.gameType = gameType;
        this.br = br;
        this.fileSize = fileSize;
        loadTextureDic();
    }

    public TextureDic(String fileName)
    {
        this.fileName = fileName;
        gameType = 2;
        loadTextureDic();
    }

    public TextureDic(String fileName, ByteReader br, int gameType, bool compressed, int sysSize)
    {
        this.fileName = fileName;
        this.gameType = gameType;
        this.br = br;
        this.fileSize = sysSize;
        this.compressed = compressed;
        loadTextureDic();
    }

    private bool loadTextureDic()
    {
        switch (gameType)
        {
            case 3:
                if (compressed)
                    textureId = new TextureDic_IV().loadTextureDic(this, compressed, 0);
                else
                    textureId = new TextureDic_IV().loadTextureDic(this, compressed, fileSize);
                break;
            default:
                textureId = new TextureDic_III_ERA().loadTextureDic(this);
                break;
        }
        return true;
    }

    public String getFileName()
    {
        return fileName;
    }

    public void setFileName(String fileName)
    {
        this.fileName = fileName;
    }

    public void displayTextures()
    {
        for (int i = 0; i < textures.Count; i++)
        {
            Debug.Log("Texture" + i + " size " + Utils.getHexString(textures[i].dataSize) + " offset "
                    + Utils.getHexString(textures[i].conversionOffset));
        }
    }

    public void sortTextures()
    {
        Texture temp = new Texture();

        for (int position = textures.Count - 1; position >= 0; position--)
        {
            for (int scan = 0; scan <= position - 1; scan++)
            {
                if (textures[scan].dataSize > textures[scan + 1].dataSize)
                {
                    temp = textures[scan];
                    textures[scan] = textures[scan + 1];
                    textures[scan + 1] = temp;
                }
            }
        }
    }

    public int getFlags(int sysSegSize, int gpuSegSize)
    {
        int result = (getCompactSize(sysSegSize) & 0x7FFF) | (getCompactSize(gpuSegSize) & 0x7FFF) << 15 | 3 << 30;
        return result;
    }

    public int getCompactSize(int size)
    {
        int i;
        // sizes must be multiples of 256!
        if ((size % 256) != 0)
        {
            Debug.Log("Size klopt niet");
        }
        size = size >> 8;

        i = 0;
        while ((size % 2) == 0 && size >= 32 && i < 15)
        {
            i++;
            size = size >> 1;
        }

        return ((i & 0xF) << 11) | (size & 0x7FF);
    }

    public void addTexture(Texture tex)
    {
        textures.Add(tex);
    }
}
