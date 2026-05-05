using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IVUnity.Ped
{
    public enum AnimGroupType
    {
        Movement,
        Weapon,
        Gesture,
        Vehicle,
        VehicleDriveby,
        Bike,
    }

    public struct AnimGroupEntry
    {
        public AnimGroupType GroupType;
        public bool IsResident;
        public string WadName;
    }

    public static class AnimGroupLoader
    {
        public static Dictionary<string, AnimGroupEntry> Load(string path)
        {
            var result = new Dictionary<string, AnimGroupEntry>(System.StringComparer.OrdinalIgnoreCase);

            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[AnimGroup] File not found: {path}");
                return result;
            }

            foreach (string rawLine in System.IO.File.ReadLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                AnimGroupType type;
                if (line.StartsWith("MovementGroup("))
                    type = AnimGroupType.Movement;
                else if (line.StartsWith("WeaponGroup("))
                    type = AnimGroupType.Weapon;
                else if (line.StartsWith("GestureGroup("))
                    type = AnimGroupType.Gesture;
                else if (line.StartsWith("VehicleGroup("))
                    type = AnimGroupType.Vehicle;
                else if (line.StartsWith("VehicleDrivebyGroup("))
                    type = AnimGroupType.VehicleDriveby;
                else if (line.StartsWith("BikeGroup("))
                    type = AnimGroupType.Bike;
                else
                    continue;

                int open = line.IndexOf('(');
                int close = line.IndexOf(')');
                if (open < 0 || close < 0) continue;

                string args = line.Substring(open + 1, close - open - 1);
                string[] parts = args.Split(',');
                if (parts.Length < 2) continue;

                string resType = parts[0].Trim();
                string wadName = parts[1].Trim();

                if (string.IsNullOrEmpty(wadName)) continue;

                bool isResident = resType.Equals("Resident", System.StringComparison.OrdinalIgnoreCase);

                result[wadName] = new AnimGroupEntry
                {
                    GroupType = type,
                    IsResident = isResident,
                    WadName = wadName,
                };
            }

            return result;
        }
    }
}
