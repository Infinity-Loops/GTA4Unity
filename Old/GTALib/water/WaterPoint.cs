using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WaterPoint
{
    public Vector3 coord;
    public float speedX;
    public float speedY;
    public float unknown;
    public float waveHeight;

    public WaterPoint(String line)
    {
        String[] split = line.Split(" ");
        // System.out.println("WaterPoint " + split.length);
        coord = new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
        speedX = float.Parse(split[3]);
        speedY = float.Parse(split[4]);
        unknown = float.Parse(split[5]);
        waveHeight = float.Parse(split[6]);
    }
}
