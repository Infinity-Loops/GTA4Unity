using Newtonsoft.Json;


using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Hashes
{
    public static IniJson table = new IniJson();
    //private static StreamReader reader;

    //public static void LoadAllHashes()
    //{
    //    table = new IniJson();
    //    reader = File.OpenText($"{Application.streamingAssetsPath}/Hashes.txt");
    //    string line = null;
    //    while ((line = reader.ReadLine()) != null)
    //    {
    //        if (!string.IsNullOrEmpty(line)) //Skip blank lines
    //        {
    //            var content = line.Split(',');
    //            table.AddData("Hashes", content[1], content[0]);
    //        }
    //    }
    //    reader.Close();
    //    Debug.Log($"Total hash count: {table["hashes"].Count}");
    //}

}
