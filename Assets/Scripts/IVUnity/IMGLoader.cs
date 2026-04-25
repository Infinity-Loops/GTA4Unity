using RageLib.FileSystem;
using RageLib.FileSystem.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class IMGLoader
{
    private string gameDir;
    private StreamReader reader;
    private FileSystem fs;

    public List<string> imgsPath = new List<string>();
    public List<IMGFileSystem> imgs = new List<IMGFileSystem>();
    public IMGLoader(string gameDir)
    {
        this.gameDir = gameDir;
    }

    public void Load(string path)
    {
        reader = System.IO.File.OpenText($"{gameDir}/{path}");

        string line = null;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("platformimg:"))
            {
                line = line.Replace("platformimg:", "pc");
                line = line.Replace("\t", "");
                imgsPath.Add(line);
            }
        }

        reader.Close();
        reader.Dispose();

        GetIMGFiles();
    }

    public void LoadManual(string path)
    {
        Debug.Log($"Loading {path}");

        IMGFileSystem newFS = new IMGFileSystem();
        newFS.Open(path);
        imgs.Add(newFS);
        imgsPath.Add(path);
    }

    private void GetIMGFiles()
    {

        for (int i = 0; i < imgsPath.Count; i++)
        {
            string line = $"{gameDir}/{imgsPath[i]}";
            bool containsProps = line.EndsWith("1");
            line = line.Substring(0, line.Length - 1);
            line = line + ".img";

            Debug.Log($"Loading {line}");

            IMGFileSystem newFS = new IMGFileSystem();
            newFS.Open(line);
            imgs.Add(newFS);
        }

    }
}
