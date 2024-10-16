using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Water
{

    StreamReader reader;

    public List<WaterPlane> planes = new List<WaterPlane>();

    public Water(string fileName)
    {
        Read(fileName);
    }

    void Read(string fileName)
    {
        reader = File.OpenText(fileName);

        string line;
        while ((line = reader.ReadLine()) != null)
        {

            WaterPlane wp = new WaterPlane(line);
            planes.Add(wp);
        }

        reader.Close();
    }

    public class WaterPlane
    {
        public WaterPoint[] points = new WaterPoint[4];
        public int param;
        public float unknown;
        public bool selected = false;

        public float u = 0.0f;
        public float v = 1.0f;

        public WaterPlane(String line)
        {
            String[] split = line.Split("   ");

            for (int i = 0; i < split.Length; i++)
            {
                points[i] = new WaterPoint(split[i]);
            }
            String[] subSplit = split[3].Split(" ");

            param = int.Parse(subSplit[7]);
            unknown = float.Parse(subSplit[8]);

            v = points[0].coord.x - points[1].coord.x;
            //u = points[0].coord.y - points[1].coord.y;
            if (Mathf.Sign((int)v) == -1) v = -1 * v;
            v = v / 10;
            //if(Mathf.Sign((int) u) == -1) u = -1 * u;
            //u = u/10;

        }
    }
    public class WaterPoint
    {
        public Vector3 coord;
        public float speedX;
        public float speedY;
        public float unknown;
        public float waveHeight;

        public WaterPoint(String line)
        {
            string[] split = line.Split(" ");
            coord = new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            speedX = float.Parse(split[3]);
            speedY = float.Parse(split[4]);
            unknown = float.Parse(split[5]);
            waveHeight = float.Parse(split[6]);
        }
    }
}