using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Parses gta.dat / default.dat — the engine's resource registration file.
/// Engine: FUN_009d61a0, commands: IMG, IMGLIST, IDE, WATER, IPL (catch-all).
/// </summary>
public class DatFileReader
{
    public struct Entry
    {
        public string Command;
        public string[] Args;
    }

    private readonly List<Entry> entries = new();
    private readonly string gameDir;

    public DatFileReader(string gameDir, string relativePath)
    {
        this.gameDir = gameDir;
        string fullPath = $"{gameDir}/{relativePath}";

        foreach (string rawLine in File.ReadAllLines(fullPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int sep = line.IndexOf(' ');
            if (sep < 0) continue;

            string command = line.Substring(0, sep).ToUpperInvariant();
            string rest = line.Substring(sep + 1).Trim();

            string[] rawArgs = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rawArgs.Length; i++)
            {
                rawArgs[i] = rawArgs[i]
                    .Replace("platform:", "pc")
                    .Replace("common:", "common")
                    .Replace("\\", "/");
            }

            entries.Add(new Entry { Command = command, Args = rawArgs });
        }
    }

    public IEnumerable<Entry> GetEntries(string command)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Command == command)
                yield return entries[i];
    }

    public string ResolveToFilesystem(string arg)
    {
        return $"{gameDir}/{arg}";
    }

    public string ResolveWplName(string iplArg)
    {
        string filename = Path.GetFileName(iplArg);
        return Path.ChangeExtension(filename, ".wpl");
    }

    public string ResolveWplPath(string iplArg)
    {
        string dir = Path.GetDirectoryName(iplArg)?.Replace("\\", "/") ?? "";
        string wplName = ResolveWplName(iplArg);
        return $"{gameDir}/{dir}/{wplName}";
    }
}
