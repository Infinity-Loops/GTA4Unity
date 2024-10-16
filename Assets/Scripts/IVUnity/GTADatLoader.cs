using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using RageLib.FileSystem.Common;
using File = RageLib.FileSystem.Common.File;
using RageLib.FileSystem;
using System.Threading.Tasks;

[System.Serializable]
public class GTADatLoader
{
    private StreamReader reader;

    private string gameDir;
    private string path = "common/data/gta.dat";

    public List<string> img = new();
    public List<string> ipl = new();
    public List<string> ide = new();
    public List<string> imgList = new();
    public List<string> water = new();
    public List<string> colFile = new();
    public List<string> splash = new();

    public Dictionary<string, File> gameFiles = new Dictionary<string, File>();

    internal IMGLoader imgLoader;
    internal IDELoader ideLoader;
    internal IPLLoader iplLoader;
    internal List<Water> waterPlanes = new List<Water>();
    internal RealFileSystem root;

    public GTADatLoader(string gameDir, RealFileSystem fs)
    {
        this.gameDir = gameDir;
        root = fs;
    }

    public async Task LoadGameFiles(Action OnFinishLoad)
    {
        ReadDatFile();

        await Task.Run(() =>
        {
            imgLoader = new IMGLoader(gameDir);

            List<string> imgFiles = new List<string>();

            SearchFilesRecursively(gameDir, ".img", imgFiles);

            imgList = imgFiles;

            for (int i = 0; i < imgList.Count; i++)
            {
                Debug.Log($"Loading IMG container: {gameDir}/{imgList[i].Replace(gameDir, "")}");
                imgLoader.LoadManual(imgList[i]);
            }

            //for (int i = 0; i < imgList.Count; i++)
            //{
            //    Debug.Log($"Loading IMG container: {gameDir}/{imgList[i]}");
            //    imgLoader.Load(imgList[i]);
            //}

            img = imgLoader.imgsPath;

        });

        await Task.Run(() =>
        {

            ideLoader = new IDELoader();

            //Load Vehicles
            //ideLoader.LoadIDE($"{gameDir}/common/data/vehicles.ide");

            List<string> ideFiles = new List<string>();

            SearchFilesRecursively(gameDir, ".ide", ideFiles);

            ide = ideFiles;

            for (int i = 0; i < ide.Count; i++)
            {
                ideLoader.LoadIDE($"{gameDir}/{ide[i].Replace(gameDir, "")}");
            }

            Debug.Log($"Total ide:{ideFiles.Count}");

        });

        Debug.Log($"Total hashes: {Hashes.table["hashes"].Count}");


        await Task.Run(() =>
        {

            List<FSObject> wplFiles = new List<FSObject>();

            foreach (var image in imgLoader.imgs)
            {
                wplFiles.AddRange(image.RootDirectory.CollectByExtension("wpl"));
            }


            iplLoader = new IPLLoader();

            //This only loads lods (not useful for now)

            //List<string> actualWPLFiles = new List<string>();

            //foreach (var wplFile in ipl)
            //{
            //    if (wplFile.EndsWith(".WPL"))
            //    {
            //        actualWPLFiles.Add(wplFile);
            //        byte[] stream = System.IO.File.ReadAllBytes($"{gameDir}/{wplFile}");
            //        iplLoader.LoadIPL(stream);
            //    }
            //}

            //ipl = actualWPLFiles;

            //This actually loads the streaming WPL files

            ipl.Clear();

            foreach (File wplFile in wplFiles)
            {
                //if (wplFile.Name.Contains("bronx")) Load Only Bronx Area
                // {
                ipl.Add(wplFile.Name);
                iplLoader.LoadIPL(wplFile.Name, wplFile.GetData());
                // }
            }

            //iplLoader = new IPLLoader();


            Debug.Log($"Loaded {ipl.Count} WPL files...");

        });


        await Task.Run(() =>
        {

            for (int i = 0; i < water.Count; i++)
            {
                Debug.Log($"Loading {gameDir}/{water[i]}");
                var waterData = new Water($"{gameDir}/{water[i]}");
                waterPlanes.Add(waterData);
            }

        });

        await Task.Run(() =>
        {
            Debug.Log("Caching game files...");

            for (int i = 0; i < imgLoader.imgs.Count; i++)
            {
                List<File> files = imgLoader.imgs[i].GetAllFiles();

                for (int j = 0; j < files.Count; j++)
                {
                    var file = files[j];

                    gameFiles[file.Name.ToLower()] = file;
                }

                Debug.Log($"Cached files for: {imgLoader.imgs[i].ToString()}");
            }
        });

        Debug.Log("Finished loading.");
        OnFinishLoad.Invoke();
    }

    private void ReadDatFile()
    {
        reader = System.IO.File.OpenText($"{gameDir}/{path}");

        string line = null;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("#") || line.Length < 1)
            {
                //Comment Section
            }
            else
            {
                string[] split = line.Split(" ");
                split[1] = split[1].Replace("platform:", "pc");
                split[1] = split[1].Replace("common:", "common");
                if (!split[1].Contains("common"))
                {
                    split[1] = split[1].Replace("IPL", "WPL");
                }
                if (split[0].Equals("IMG"))
                {
                    split[1] = split[1].Replace("\\", "/");
                    img.Add(split[1]);
                }
                else if (split[0].Equals("IDE"))
                {
                    ide.Add(split[1]);
                    Debug.Log($"Adding IDE: {split[1]}");
                }
                else if (split[0].Equals("IPL"))
                {
                    ipl.Add(split[1]);
                }
                else if (split[0].Equals("IMGLIST"))
                {
                    imgList.Add(split[1]);
                }
                else if (split[0].Equals("WATER"))
                {
                    water.Add(split[1]);
                }
                else if (split[0].Equals("SPLASH"))
                {
                    splash.Add(split[1]);
                }
                else if (split[0].Equals("COLFILE"))
                {
                    colFile.Add(split[1]);
                }
            }
        }

        reader.Close();
        reader.Dispose();
    }

    void SearchFilesRecursively(string directory, string extension, List<string> filePaths)
    {
        foreach (string file in System.IO.Directory.GetFiles(directory, $"*{extension}"))
        {
            filePaths.Add(file);
        }

        foreach (string subDirectory in System.IO.Directory.GetDirectories(directory))
        {
            SearchFilesRecursively(subDirectory, extension, filePaths);
        }
    }
}
