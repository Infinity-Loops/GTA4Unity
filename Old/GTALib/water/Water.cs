using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Windows;
using File = System.IO.File;

public class Water
{
    public String fileName;
    public int gameType;

    public List<WaterPlane> planes = new List<WaterPlane>();

    private StreamReader input;

    public Water(String fileName, int gameType)
    {
        this.fileName = fileName;
        this.gameType = gameType;
        Read();
    }

    public void Read()
    {
        if (openWater())
        {
            try
            {
                //System.out.println("Opened water.dat");
                String line;
                while ((line = input.ReadLine()) != null)
                {
                    Debug.Log(line);
                    WaterPlane wp = new WaterPlane(line);
                    planes.Add(wp);
                }
            }
            catch (IOException ex)
            {
                Debug.LogException(ex);
            }
        }
        closeWater();
    }

    public void Write()
    {

    }

    public bool openWater()
    {
        try
        {
            input = File.OpenText(fileName);
        }
        catch (IOException ex)
        {
            return false;
        }
        return true;
    }

    public bool closeWater()
    {
        try
        {
            input.Close();
        }
        catch (IOException ex)
        {
            return false;
        }
        return true;
    }
}
