using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parses images.txt — the engine's IMG load order with priority.
/// Engine: FUN_009d6080, each line = "imgname priority".
/// Higher priority wins when same filename exists in multiple IMGs.
/// Same priority = last in list wins (engine mounts in order, later overwrites).
/// </summary>
public class ImagesListReader
{
    public struct ImgEntry
    {
        public string Path;
        public int Priority;
        public int Order;
    }

    public readonly List<ImgEntry> Entries = new();

    public ImagesListReader(string gameDir, string relativePath)
    {
        string fullPath = $"{gameDir}/{relativePath}";
        if (!System.IO.File.Exists(fullPath)) return;

        int order = 0;
        foreach (string rawLine in System.IO.File.ReadAllLines(fullPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            string[] parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            string imgPath = parts[0]
                .Replace("platformimg:", "pc")
                .Replace("commonimg:", "common")
                .Replace("\\", "/");

            int priority = -1;
            if (parts.Length > 1) int.TryParse(parts[1], out priority);

            Entries.Add(new ImgEntry
            {
                Path = imgPath + ".img",
                Priority = priority,
                Order = order++,
            });
        }

        Debug.Log($"[images.txt] {Entries.Count} IMGs listed");
    }
}
