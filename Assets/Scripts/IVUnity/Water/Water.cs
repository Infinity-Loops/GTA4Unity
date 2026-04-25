using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Water
{
    public List<WaterPlane> planes = new List<WaterPlane>();

    public Water(string fileName)
    {
        Read(fileName);
    }

    void Read(string fileName)
    {
        using var reader = File.OpenText(fileName);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                planes.Add(new WaterPlane(line));
            }
            catch (FormatException ex)
            {
                Debug.LogWarning($"[Water {Path.GetFileName(fileName)}] Skipped line '{line}' — {ex.Message}");
            }
        }
    }

    public class WaterPlane
    {
        public WaterPoint[] points = new WaterPoint[4];
        public int   param;
        public float unknown;
        public bool  selected = false;

        public float u = 0.0f;
        public float v = 1.0f;

        public WaterPlane(string line)
        {
            // Format: 4 × point (7 floats each: x y z speedX speedY unknown waveHeight)
            //         followed by `param` (int) and `unknown` (float).
            // Whitespace between fields varies (some files use multiple spaces or tabs),
            // so split on either char with RemoveEmptyEntries collapsing runs.
            var p = new LineParser(line, ' ', '\t');

            for (int i = 0; i < 4; i++)
            {
                points[i] = WaterPoint.Read(ref p);
            }

            param   = p.ReadInt();
            unknown = p.ReadFloat();

            // UV scaling derived from the first edge — preserve legacy math verbatim.
            v = points[0].coord.x - points[1].coord.x;
            if (Mathf.Sign((int)v) == -1) v = -1 * v;
            v = v / 10;
        }
    }

    public class WaterPoint
    {
        public Vector3 coord;
        public float   speedX;
        public float   speedY;
        public float   unknown;
        public float   waveHeight;

        public static WaterPoint Read(ref LineParser p)
        {
            return new WaterPoint
            {
                coord      = p.ReadVector3(),
                speedX     = p.ReadFloat(),
                speedY     = p.ReadFloat(),
                unknown    = p.ReadFloat(),
                waveHeight = p.ReadFloat(),
            };
        }
    }
}
