using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class IDELoader
{
    public List<IDE> ides = new List<IDE>();

    public void LoadIDE(string fileName)
    {
        IDE ide = new IDE(fileName);
        ides.Add(ide);
    }
}
