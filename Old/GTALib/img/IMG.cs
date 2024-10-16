using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Constants;
using static UnityEditor.Experimental.GraphView.GraphView;

public class IMG
{
    private String fileName;
    private GameType mGameType;
    public bool changed = false; // True when the file needs to be saved

    public bool encrypted = false;
    public bool containsProps = false;

    public byte[] key = new byte[32];

    public int itemCount = 0;
    public int cutCount = 0;
    public int wtdCount = 0;
    public int wbdCount = 0;
    public int wbnCount = 0;
    public int wplCount = 0;
    public int wddCount = 0;
    public int wdrCount = 0;
    public int wadCount = 0;
    public int wftCount = 0;
    public int unknownCount = 0;

    public List<IMG_Item> Items = new List<IMG_Item>(); // All items

    public IMG(String fileName, GameType pGameType, byte[] key, bool autoLoad, bool containsProps)
    {
        this.key = key;
        // Message.displayMsgSuper("Loading IMG: " + fileName);
        this.fileName = fileName;
        mGameType = pGameType;
        this.containsProps = containsProps;
        if (autoLoad)
            loadImg();
    }

    private bool loadImg()
    {
        switch (mGameType)
        {
            case GameType.GTA_III:
                new IMG_III().loadImg(this);
                break;
            case GameType.GTA_VC:
                new IMG_VC().loadImg(this);
                break;
            case GameType.GTA_SA:
                new IMG_SA().loadImg(this);
                break;
            case GameType.GTA_IV:
                new IMG_IV().loadImg(this);
                break;
        }
        if (Items == null)
            return false;
        else
            return true;
    }

    public int getItemIndex(String name)
    {
        int i = 0;
        while (!Items[i].getName().ToLower().Equals(name.ToLower()))
        {
            if (i < Items.Count - 1)
            {
                i++;
            }
            else
            {
                break;
            }
        }
        return i;
    }

    public IMG_Item findItem(String name)
    {
        IMG_Item ret = null;
        int i = 0;
        while (!Items[i].getName().ToLower().Equals(name.ToLower()))
        {
            if (i < Items.Count - 1)
            {
                i++;
            }
            else
            {
                break;
            }
        }
        if (Items[i].getName().ToLower().Equals(name.ToLower()))
        {
            // Message.displayMsgSuper("<IMG " + fileName + ">Found file " + name + " at " + i + " offset " +
            // Items.get(i).getOffset());
            ret = Items[i];
        }
        else
        {
            // Message.displayMsgSuper("<IMG " + fileName + ">Unable to find file " + name);
        }
        return ret;
    }

    public List<IMG_Item> getItems()
    {
        return Items;
    }

    public void setItems(List<IMG_Item> Items)
    {
        this.Items = Items;
    }

    public String getFileName()
    {
        return fileName;
    }

    public void setFileName(String fileName)
    {
        this.fileName = fileName;
    }

    public GameType getGameType()
    {
        return mGameType;
    }

    public void setGameType(GameType gameType)
    {
        mGameType = gameType;
    }
}
