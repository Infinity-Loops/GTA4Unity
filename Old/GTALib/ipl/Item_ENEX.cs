using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item_ENEX : IPL_Item
{
    private int gameType;

    public Item_ENEX(int gameType)
    {
        this.gameType = gameType;
    }

    public override void read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void read(ReadFunctions rf)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}
