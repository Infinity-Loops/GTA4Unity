using System;
using System.Collections.Generic;
using System.IO;

namespace IVUnity.Ped
{
    public class PedSlotEntry
    {
        public string Slot;
        public int GeometryIndex;
    }

    public class PedVariation
    {
        public string Name;
        public List<PedSlotEntry> Slots = new();

        public static readonly string[] DrawableSuffixes = { "_u", "_r", "_m" };

        public static string[] GetDrawableCandidates(string slot, int geometryIndex)
        {
            string baseName = $"{slot}_{geometryIndex:D3}";
            return new[]
            {
                baseName + "_u",
                baseName + "_r",
                baseName + "_m",
            };
        }

        public int VariantCount
        {
            get
            {
                int max = 0;
                foreach (var entry in Slots)
                {
                    if (entry.GeometryIndex + 1 > max)
                        max = entry.GeometryIndex + 1;
                }
                return max;
            }
        }

        public List<PedSlotEntry> GetSlotsForVariant(int variantIndex)
        {
            var result = new List<PedSlotEntry>();
            var usedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First pass: pick the requested variant for each slot
            foreach (var entry in Slots)
            {
                if (entry.GeometryIndex == variantIndex && usedSlots.Add(entry.Slot))
                    result.Add(entry);
            }

            // Second pass: fill slots that don't have this variant with geometry 0
            foreach (var entry in Slots)
            {
                if (entry.GeometryIndex == 0 && usedSlots.Add(entry.Slot))
                    result.Add(entry);
            }

            return result;
        }
    }

    public static class PedVariationsLoader
    {
        public static Dictionary<string, PedVariation> Load(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return new Dictionary<string, PedVariation>(StringComparer.OrdinalIgnoreCase);

            using var reader = new StreamReader(filePath);
            return Parse(reader);
        }

        public static Dictionary<string, PedVariation> Parse(TextReader reader)
        {
            var result = new Dictionary<string, PedVariation>(StringComparer.OrdinalIgnoreCase);
            PedVariation current = null;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null)
                    {
                        result[current.Name] = current;
                        current = null;
                    }
                    continue;
                }

                var parts = line.Split(new[] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                for (int i = 0; i < parts.Length; i++)
                    parts[i] = parts[i].Trim();

                if (current == null)
                {
                    current = new PedVariation { Name = parts[0] };
                }
                else
                {
                    if (int.TryParse(parts[1], out int geomIdx))
                    {
                        current.Slots.Add(new PedSlotEntry
                        {
                            Slot = parts[0],
                            GeometryIndex = geomIdx,
                        });
                    }
                }
            }

            if (current != null)
                result[current.Name] = current;

            return result;
        }
    }
}
