using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class IDELoader
{
    public List<IDE> ides = new List<IDE>();
    public Dictionary<string, Item_OBJS> objsDict = new Dictionary<string, Item_OBJS>();
    public Dictionary<string, Item_TOBJ> tobjDict = new Dictionary<string, Item_TOBJ>();

    public void LoadIDE(string fileName)
    {
        IDE ide = new IDE(fileName);
        ides.Add(ide);
        
        // Populate dictionaries for fast lookup
        foreach (var obj in ide.items_objs)
        {
            if (!string.IsNullOrEmpty(obj.modelName) && !objsDict.ContainsKey(obj.modelName))
            {
                objsDict[obj.modelName] = obj;
            }
        }
        
        foreach (var tobj in ide.items_tobj)
        {
            if (!string.IsNullOrEmpty(tobj.modelName) && !tobjDict.ContainsKey(tobj.modelName))
            {
                tobjDict[tobj.modelName] = tobj;
            }
        }
    }
}
