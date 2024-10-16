using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_WEAP : IDE_Item
{
    private int gameType;

    public Item_WEAP(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}
