using IniReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public static class Utils
{

    public static Vector4 getAxisAngle(this Vector4 vector)
    {
        Quaternion rotation = new Quaternion(vector.x, vector.y, vector.z, vector.w);

        Vector3 axis;
        float angle;

        rotation.ToAngleAxis(out angle, out axis);

        Vector4 result = new Vector4(axis.x, axis.y, axis.z, angle);
        return result;
    }

    public static String getHexString(int value)
    {
        string hex = Convert.ToString(value, 16).ToUpper();
        int size = 4;

        if (hex.Length > 4)
            size = 8;

        while (hex.Length != size)
        {
            hex = "0" + hex;
        }

        hex = "0x" + hex;
        return hex;
    }

    public static String getStartOffset(int offset)
    {
        return " - (" + getHexString(offset) + ")";
    }

    public static String getShaderName(int type)
    {
        String ret = "Unknown";
        switch (type)
        {
            case 0x2b5170fd:
                ret = "Texture";
                break;
            case 0x608799c6:
                ret = "SpecularTexture";
                break;
            case 0x46b7c64f:
                ret = "NormalTexture";
                break;
            case -718597665:
                ret = "DiffuseMap1";
                break;
            case 606121937:
                ret = "DiffuseMap2";
                break;
            case -64241494:
                ret = "Vector";
                break;
            case 376311761:
                ret = "Vector";
                break;
            case 1212833469:
                ret = "Vector";
                break;
            case -15634147:
                ret = "Vector";
                break;
            case -160355455:
                ret = "Vector";
                break;
            case -2078982697:
                ret = "Vector";
                break;
            case -677643234:
                ret = "Vector";
                break;
            case -1168850544:
                ret = "Vector";
                break;
            case 424198508:
                ret = "Vector";
                break;
            case 514782960:
                ret = "Vector";
                break;
            case -260861532:
                ret = "Matrix";
                break;
        }
        ret += " (" + type + ")";
        return ret;
    }

    public static String getShaderNameFromIni(int type)
    {
        String ret = "Unknown";
        try
        {
            var shaderStream = File.OpenText($"{Application.streamingAssetsPath}/shaders.ini");

            var shaders = Ini.Read(shaderStream);

            if (shaders.ContainsKey("names"))
            {
                if (shaders["names"].ContainsKey("" + type))
                {
                    ret = (string)shaders["names"]["" + type];
                }
            }
        }
        catch (IOException ex)
        {
            Debug.LogError("Something went wrong reading the ini " + ex.ToString());
        }
        return ret;
    }

    public static String getShaderType(int type)
    {
        String ret = "Unknown " + type;
        switch (type)
        {
            case 0:
                ret = "Texture";
                break;
            case 4:
                ret = "Matrix";
                break;
            case 1:
                ret = "Vector";
                break;
        }
        return ret;
    }

    public static int getFileType(String fileName, IMG img)
    {
        fileName = fileName.ToLower();
        if (fileName.EndsWith(".dff"))
        {
            return Constants.ftDFF;
        }
        else if (fileName.EndsWith(".txd"))
        {
            return Constants.ftTXD;
        }
        else if (fileName.EndsWith(".col"))
        {
            return Constants.ftCOL;
        }
        else if (fileName.EndsWith(".ipl"))
        {
            return Constants.ftIPL;
        }
        else if (fileName.EndsWith(".ide"))
        {
            return Constants.ftIDE;
        }
        else if (fileName.EndsWith(".wdr"))
        {
            img.wdrCount++;
            return Constants.ftWDR;
        }
        else if (fileName.EndsWith(".wdd"))
        {
            img.wddCount++;
            return Constants.ftWDD;
        }
        else if (fileName.EndsWith(".wbn"))
        {
            img.wbnCount++;
            return Constants.ftWBN;
        }
        else if (fileName.EndsWith(".wbd"))
        {
            img.wbdCount++;
            return Constants.ftWBD;
        }
        else if (fileName.EndsWith(".wtd"))
        {
            img.wtdCount++;
            return Constants.ftWTD;
        }
        else if (fileName.EndsWith(".wft"))
        {
            img.wftCount++;
            return Constants.ftWFT;
        }
        else if (fileName.EndsWith(".wad"))
        {
            img.wadCount++;
            return Constants.ftWAD;
        }
        else if (fileName.EndsWith(".wpl"))
        {
            img.wplCount++;
            return Constants.ftWPL;
        }
        else if (fileName.EndsWith(".ifp"))
        {
            return Constants.ftIFP;
        }
        else
        {
            return -1;
        }
    }

    public static int getResourceType(String fileName)
    {
        fileName = fileName.ToLower();
        if (fileName.EndsWith(".wdr"))
        {
            return Constants.rtWDR;
        }
        else if (fileName.EndsWith(".wdd"))
        {
            return Constants.rtWDD;
        }
        else if (fileName.EndsWith(".wbn"))
        {
            return Constants.rtWBN;
        }
        else if (fileName.EndsWith(".wbd"))
        {
            return Constants.rtWBD;
        }
        else if (fileName.EndsWith(".wtd"))
        {
            return Constants.rtWTD;
        }
        else if (fileName.EndsWith(".wft"))
        {
            return Constants.rtWFT;
        }
        else if (fileName.EndsWith(".wad"))
        {
            return Constants.rtWAD;
        }
        else if (fileName.EndsWith(".wpl"))
        {
            return Constants.rtWPL;
        }
        else
        {
            return -1;
        }
    }

    public static int getTotalMemSize(int Flags)
    {
        return (getSystemMemSize(Flags) + getGraphicsMemSize(Flags));
    }

    public static int getSystemMemSize(int Flags)
    {
        return (int)(Flags & 0x7FF) << (int)(((Flags >> 11) & 0xF) + 8);
    }

    public static int getGraphicsMemSize(int Flags)
    {
        return (int)((Flags >> 15) & 0x7FF) << (int)(((Flags >> 26) & 0xF) + 8);
    }
}
