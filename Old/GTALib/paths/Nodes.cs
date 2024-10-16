using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Nodes
{
    private int nodeCount;
    private int carNodeCount;
    private int intersectionNodeCount;
    private int linkCount;

    public Nodes()
    {
        ReadFunctions rf = new ReadFunctions();
        rf.openFile("E:/Nodes/nodes29.nod");

        readHeader(rf);
        for (int i = 0; i < nodeCount; i++)
        {
            Node node = new Node(rf);
        }
        for (int i = 0; i < linkCount; i++)
        {
            Link link = new Link(rf);
        }
    }

    private void readHeader(ReadFunctions rf)
    {
        nodeCount = rf.readInt();
        carNodeCount = rf.readInt();
        intersectionNodeCount = rf.readInt();
        linkCount = rf.readInt();
        Debug.Log("Node Count: " + nodeCount);
        Debug.Log("Car Node Count: " + carNodeCount);
        Debug.Log("intersectionNode Count: " + intersectionNodeCount);
        Debug.Log("Link Count: " + linkCount);
    }
}
