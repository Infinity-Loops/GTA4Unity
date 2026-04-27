using RageLib.FileSystem;
using RageLib.FileSystem.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class IMGLoader
{
    public List<string> imgsPath = new List<string>();
    public List<IMGFileSystem> imgs = new List<IMGFileSystem>();
    
    public void Load(string path)
    {
        //Debug.Log($"Loading {path}");
        IMGFileSystem newFS = new IMGFileSystem();
        newFS.Open(path);
        imgs.Add(newFS);
        imgsPath.Add(path);
    }
}