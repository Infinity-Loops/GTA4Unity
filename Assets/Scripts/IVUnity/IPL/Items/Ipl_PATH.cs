using System.IO;
using UnityEngine;

public class Ipl_PATH : IPL_Item
{
    public override void Read(string line) => Debug.Log($"{GetType().Name} not supported yet.");
    public override void Read(BinaryReader reader) => Debug.Log($"{GetType().Name} not supported yet.");
    public override void Read(BinaryReader reader, GTAHashTable ini) => Debug.Log($"{GetType().Name} not supported yet.");
}
