using System.Collections.Generic;
using UnityEngine;

namespace IVUnity.Ped
{
    public static class MoveBlendLoader
    {
        public static Dictionary<string, string> Load(string path)
        {
            var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[MoveBlend] File not found: {path}");
                return result;
            }

            foreach (string rawLine in System.IO.File.ReadLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (!line.StartsWith("MOVEMENT_GROUP")) continue;

                string[] tokens = line.Split(new[] { '\t', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                // MOVEMENT_GROUP <name> FALLBACK_GROUP <fallback>
                if (tokens.Length < 4) continue;

                int fbIdx = System.Array.IndexOf(tokens, "FALLBACK_GROUP");
                if (fbIdx < 0 || fbIdx + 1 >= tokens.Length) continue;

                string groupName = tokens[1];
                string fallback = tokens[fbIdx + 1];
                result[groupName] = fallback;
            }

            return result;
        }
    }
}
