using System.Collections.Generic;
using UnityEngine;

namespace IVUnity.Ped
{
    public struct MeleeAnimEntry
    {
        public string Name;
        public bool UpperBody;
        public bool HeadNeckAndArms;
        public bool RotatesRoot;
    }

    public struct MeleeAnimGroup
    {
        public string LogicalName;
        public string DictionaryName;
        public string Residency;
        public string NetResidency;
        public List<MeleeAnimEntry> Anims;
    }

    public static class MeleeAnimsLoader
    {
        public static Dictionary<string, MeleeAnimGroup> Load(string path)
        {
            var result = new Dictionary<string, MeleeAnimGroup>(System.StringComparer.OrdinalIgnoreCase);

            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[MeleeAnims] File not found: {path}");
                return result;
            }

            var lines = System.IO.File.ReadAllLines(path);
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i].Trim();
                i++;

                if (line != "MELEE_ANIM_GROUP:") continue;

                if (i >= lines.Length) break;
                string header = lines[i].Trim();
                i++;

                string[] tokens = header.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4) continue;

                var group = new MeleeAnimGroup
                {
                    LogicalName = tokens[0],
                    DictionaryName = tokens[1],
                    Residency = tokens[2],
                    NetResidency = tokens[3],
                    Anims = new List<MeleeAnimEntry>(),
                };

                while (i < lines.Length)
                {
                    string animLine = lines[i].Trim();
                    i++;

                    if (animLine == "END_ANIM_GROUP") break;
                    if (animLine.Length == 0 || animLine[0] == '#') continue;

                    string[] parts = animLine.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    var entry = new MeleeAnimEntry
                    {
                        Name = parts[0],
                    };

                    for (int p = 1; p < parts.Length; p++)
                    {
                        if (parts[p] == "-UpperBody") entry.UpperBody = true;
                        else if (parts[p] == "-HeadNeckAndArms") entry.HeadNeckAndArms = true;
                        else if (parts[p] == "-RotatesRoot") entry.RotatesRoot = true;
                    }

                    group.Anims.Add(entry);
                }

                result[group.LogicalName] = group;
            }

            return result;
        }
    }
}
