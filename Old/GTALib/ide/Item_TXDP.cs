using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_TXDP : IDE_Item
{
    public String texDic;
    public String texDicParent;
    private int gameType;

    public Item_TXDP(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        line = line.Replace(" ", "");
        String[] split = line.Split(",");
        texDic = split[0];
        texDicParent = split[1];
        Debug.Log("<TXDP>" + line);
    }
}
