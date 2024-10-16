using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArchetypeDamp 
{
    public void read(ByteReader br)
    {
        int VTable = br.readUInt32();
        Debug.Log("VTable: " + VTable);
    }
}
