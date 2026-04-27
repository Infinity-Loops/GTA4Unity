using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using RageLib.FileSystem.Common;
using File = RageLib.FileSystem.Common.File;
using RageLib.FileSystem;
using System.Threading.Tasks;

[Serializable]
public class GTADatLoader
{
    internal string gameDir;

    public Dictionary<string, File> gameFiles = new();

    internal IMGLoader imgLoader;
    public IDELoader ideLoader;
    internal IPLLoader iplLoader;
    internal List<Water> waterPlanes = new();
    internal RealFileSystem root;

    internal DatFileReader dat;

    public GTADatLoader(string gameDir, RealFileSystem fs)
    {
        this.gameDir = gameDir;
        root = fs;
    }

    public async Task LoadGameFiles(Action OnFinishLoad)
    {
        dat = new DatFileReader(gameDir, "common/data/gta.dat");
        var imgList = new ImagesListReader(gameDir, "common/data/images.txt");

        await MountImgsAndCacheFiles(imgList);
        await LoadIdes();
        await LoadWpls();
        LoadWater();

        LoadingScreen.ResetProgress();
        Debug.Log("Finished loading.");
        OnFinishLoad.Invoke();
    }

    private async Task MountImgsAndCacheFiles(ImagesListReader imgList)
    {
        await Task.Run(() =>
        {
            imgLoader = new IMGLoader();

            var imgPriority = new Dictionary<string, (int pri, int ord)>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in imgList.Entries)
                imgPriority[$"{gameDir}/{e.Path}"] = (e.Priority, e.Order);

            var allImgFiles = new List<string>();
            SearchFilesRecursively(gameDir, ".img", allImgFiles);

            allImgFiles.Sort((a, b) =>
            {
                bool aKnown = imgPriority.TryGetValue(a, out var aPri);
                bool bKnown = imgPriority.TryGetValue(b, out var bPri);
                if (aKnown && bKnown)
                {
                    if (aPri.pri != bPri.pri) return aPri.pri.CompareTo(bPri.pri);
                    return aPri.ord.CompareTo(bPri.ord);
                }
                if (aKnown) return -1;
                if (bKnown) return 1;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            LoadingScreen.SetupLoadingTarget(allImgFiles.Count);
            for (int i = 0; i < allImgFiles.Count; i++)
            {
                LoadingScreen.AdvanceProgress(allImgFiles[i]);
                imgLoader.Load(allImgFiles[i]);
            }

            var filePriority = new Dictionary<string, (int pri, int ord)>(StringComparer.OrdinalIgnoreCase);

            LoadingScreen.ResetProgress();
            LoadingScreen.SetupLoadingTarget(imgLoader.imgs.Count);

            for (int i = 0; i < imgLoader.imgs.Count; i++)
            {
                string imgPath = i < allImgFiles.Count ? allImgFiles[i] : "";
                imgPriority.TryGetValue(imgPath, out var thisPri);

                var files = imgLoader.imgs[i].GetAllFiles();
                for (int j = 0; j < files.Count; j++)
                {
                    string key = files[j].Name.ToLower();

                    if (filePriority.TryGetValue(key, out var existingPri))
                    {
                        if (thisPri.pri < existingPri.pri) continue;
                        if (thisPri.pri == existingPri.pri && thisPri.ord <= existingPri.ord) continue;
                    }

                    gameFiles[key] = files[j];
                    filePriority[key] = thisPri;
                }

                LoadingScreen.AdvanceProgress(imgLoader.imgs[i].ToString());
            }

            LoadingScreen.ResetProgress();
            Debug.Log($"Cached {gameFiles.Count} files from {imgLoader.imgs.Count} IMGs");
        });
    }

    private async Task LoadIdes()
    {
        await Task.Run(() =>
        {
            ideLoader = new IDELoader();

            var ideFiles = new List<string>();
            SearchFilesRecursively(gameDir, ".ide", ideFiles);

            LoadingScreen.SetupLoadingTarget(ideFiles.Count);
            for (int i = 0; i < ideFiles.Count; i++)
            {
                LoadingScreen.AdvanceProgress(ideFiles[i]);
                ideLoader.LoadIDE($"{gameDir}/{ideFiles[i].Replace(gameDir, "")}");
            }
            LoadingScreen.ResetProgress();

            Debug.Log($"Total ide: {ideFiles.Count}");
        });
    }

    private async Task LoadWpls()
    {
        // Engine two-tier IPL loading:
        //   1. gta.dat IPLs → base world with LOD chains (inst.lod links within each WPL)
        //   2. Streaming IPLs → HD detail from IMGs, loaded by proximity
        // We load gta.dat WPLs FIRST so their LOD chains survive dedup in WorldEntityBaker,
        // then all remaining WPLs from IMGs for HD geometry.
        await Task.Run(() =>
        {
            iplLoader = new IPLLoader();
            var loadedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pass 1: gta.dat WPLs (base world with LOD hierarchy)
            foreach (var e in dat.GetEntries("IPL"))
            {
                string arg = e.Args[0];
                if (arg.StartsWith("common", StringComparison.OrdinalIgnoreCase)) continue;

                string wplName = dat.ResolveWplName(arg);

                if (gameFiles.TryGetValue(wplName.ToLower(), out File wpl))
                {
                    iplLoader.LoadIPL(wpl.Name, wpl.GetData());
                    loadedNames.Add(wplName.ToLower());
                }
                else
                {
                    string wplPath = dat.ResolveWplPath(arg);
                    if (System.IO.File.Exists(wplPath))
                    {
                        byte[] data = System.IO.File.ReadAllBytes(wplPath);
                        iplLoader.LoadIPL(wplName, data);
                        loadedNames.Add(wplName.ToLower());
                    }
                }
            }

            int baseCount = loadedNames.Count;

            // Pass 2: all remaining WPLs from IMGs (streaming HD content)
            foreach (var kvp in gameFiles)
            {
                if (!kvp.Key.EndsWith(".wpl")) continue;
                if (loadedNames.Contains(kvp.Key)) continue;

                iplLoader.LoadIPL(kvp.Value.Name, kvp.Value.GetData());
                loadedNames.Add(kvp.Key);
            }

            Debug.Log($"Loaded {loadedNames.Count} WPL files ({baseCount} base from gta.dat, " +
                      $"{loadedNames.Count - baseCount} streaming from IMGs)");
        });
    }

    private void LoadWater()
    {
        foreach (var e in dat.GetEntries("WATER"))
        {
            for (int i = 0; i < e.Args.Length; i++)
            {
                string path = dat.ResolveToFilesystem(e.Args[i]);
                if (!System.IO.File.Exists(path)) continue;
                Debug.Log($"Loading water: {path}");
                waterPlanes.Add(new Water(path));
            }
        }
    }

    static void SearchFilesRecursively(string directory, string extension, List<string> results)
    {
        foreach (string file in System.IO.Directory.GetFiles(directory, $"*{extension}"))
            results.Add(file);
        foreach (string sub in System.IO.Directory.GetDirectories(directory))
            SearchFilesRecursively(sub, extension, results);
    }
}
