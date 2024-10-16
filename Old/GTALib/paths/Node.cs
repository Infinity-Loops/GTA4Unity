using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node
{
    public Node(ReadFunctions rf)
    {
        int memAdress = rf.readInt();
        int zero = rf.readInt();
        int areaID = rf.readShort();
        int nodeID = rf.readShort();
        int unknown = rf.readInt();
        int always7FFE = rf.readShort();
        int linkID = rf.readShort();
        int posX = rf.readShort();
        int posY = rf.readShort();
        int posZ = rf.readShort();
        byte pathWidth = rf.readByte();
        byte pathType = rf.readByte();
        int flags = rf.readInt();
        Debug.Log("--------------Node---------------");
        Debug.Log("Mem Adress: " + memAdress);
        Debug.Log("Zero: " + zero);
        Debug.Log("AreaID: " + areaID);
        Debug.Log("NodeID: " + nodeID);
        Debug.Log("unknown: " + unknown);
        Debug.Log("Always7FFE: " + always7FFE);
        Debug.Log("LinkID: " + linkID);
        Debug.Log("PosX: " + (float)(posX / 8));
        Debug.Log("PosY: " + (float)(posY / 8));
        Debug.Log("PosZ: " + (float)(posZ / 128));
        Debug.Log("PathWidth: " + pathWidth);
        Debug.Log("PathType: " + pathType);
        Debug.Log("Flags: " + flags);
    }
}
