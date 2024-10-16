using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WaterPlane
{
    public WaterPoint[] points = new WaterPoint[4];
    public int param;
    public float unknown;
    public bool selected = false;

    public float u = 0.0f; //used for rendering only
    public float v = 1.0f; //used for rendering only

    public WaterPlane(String line)
    {
        String[] split = line.Split("   ");
        //System.out.println("Waterplane " + split.length);
        for (int i = 0; i < split.Length; i++)
        {
            points[i] = new WaterPoint(split[i]);
        }
        String[] subSplit = split[3].Split(" ");
        //System.out.println("Sub: " + subSplit.length);
        param = int.Parse(subSplit[7]);
        unknown = float.Parse(subSplit[8]);
        //System.out.println("Param: " + param);
        //System.out.println("Unknown: " + unknown);
        v = points[0].coord.x - points[1].coord.x;
        //u = points[0].coord.y - points[1].coord.y;
        if (Mathf.Sign((int)v) == -1) v = -1 * v;
        v = v / 10;
        //if(Integer.signum((int) u) == -1) u = -1 * u;
        //u = u/10;
        //System.out.println("V: " + v + " U: " + u);
    }
}
