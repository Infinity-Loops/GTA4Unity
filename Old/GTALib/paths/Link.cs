using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Link
{
    public Link(ReadFunctions rf)
    {
        int areaID = rf.readShort();
        int nodeID = rf.readShort();
        byte unknown = rf.readByte();
        byte linkLength = rf.readByte();
        int flags = rf.readShort();
        Debug.Log("-----------Link-------------");
        Debug.Log("AreaID: " + areaID);
        Debug.Log("NodeID: " + nodeID);
        Debug.Log("unknown: " + unknown);
        Debug.Log("LinkLength: " + linkLength);
        Debug.Log("Flags: " + flags);
    }
}
