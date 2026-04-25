using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_ZONE : IPL_Item
{
    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;

    public override void Read(string line) => Debug.Log($"{GetType().Name} not supported yet.");

    public override void Read(BinaryReader reader)
    {
        posLowerLeft  = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
    }

    public override void Read(BinaryReader reader, GTAHashTable ini)
        => Debug.Log($"{GetType().Name} not supported yet.");
}
