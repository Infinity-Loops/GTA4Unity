using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_HIER : IDE_Item
{
    private int gameType;

    public Item_HIER(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}
