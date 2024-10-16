using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using static Constants;

public class GTA_DAT
{
    private StreamReader input; // Reader

    private String gameDir; // game dir
    private String fileName; // file name
    private GameType mGameType; // gametype ie: SA

    public bool changed = false; // True when the file needs to be saved

    public List<String> img = new List<String>();
    public List<String> ipl = new List<String>();
    public List<String> ide = new List<String>();
    public List<String> imgList = new List<String>();
    public List<String> water = new List<String>();
    public List<String> colFile = new List<String>();
    public List<String> splash = new List<String>();


    public GTA_DAT(String gameDir, GameType pGameType)
    {
        this.gameDir = gameDir;
        mGameType = pGameType;
        switch (mGameType)
        {
            case GameType.GTA_III:
                this.fileName = gameDir + "data/gta3.dat";
                break;
            case GameType.GTA_VC:
                this.fileName = gameDir + "data/gta_vc.dat";
                break;
            case GameType.GTA_SA:
                this.fileName = gameDir + "data/gta.dat";
                break;
            case GameType.GTA_IV:
                this.fileName = gameDir + "common/data/gta.dat";
                break;
        }
        //		////Message.displayMsgHigh("Filename: " + fileName);
        loadGTA_DAT();
    }

    private bool loadGTA_DAT()
    {
        if (openGTA_DAT())
        {
            try
            {
                String line = null; // not declared within while loop
                while ((line = input.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line.Length < 1)
                    {
                        //						////Message.displayMsgHigh(line);
                    }
                    else
                    {
                        String[] split = line.Split(" ");
                        split[1] = split[1].Replace("platform:", "pc");
                        split[1] = split[1].Replace("common:", "common");
                        if (!split[1].Contains("common"))
                            split[1] = split[1].Replace("IPL", "WPL");
                        if (split[0].Equals("IMG"))
                        {
                            split[1] = split[1].Replace("\\", "/");
                            img.Add(split[1]);
                            ////Message.displayMsgHigh("IMG: " + split[1]);
                        }
                        else if (split[0].Equals("IDE"))
                        {
                            ide.Add(split[1]);
                            ////Message.displayMsgHigh("IDE: " + split[1]);
                        }
                        else if (split[0].Equals("IPL"))
                        {
                            ipl.Add(split[1]);
                            ////Message.displayMsgHigh("IPL: " + split[1]);
                        }
                        else if (split[0].Equals("IMGLIST"))
                        {
                            imgList.Add(split[1]);
                            ////Message.displayMsgHigh("IMGLIST: " + split[1]);
                        }
                        else if (split[0].Equals("WATER"))
                        {
                            water.Add(split[1]);
                            ////Message.displayMsgHigh("WATER: " + split[1]);
                        }
                        else if (split[0].Equals("SPLASH"))
                        {
                            splash.Add(split[1]);
                            ////Message.displayMsgHigh("SPLASH: " + split[1]);
                        }
                        else if (split[0].Equals("COLFILE"))
                        {
                            colFile.Add(split[1]);
                            ////Message.displayMsgHigh("COLFILE: " + split[2]);
                        }
                    }
                }
                closeGTA_DAT();
            }
            catch (IOException ex)
            {
                Debug.LogException(ex);
            }
        }
        loadImagesFromIMGLIST();
        return true;
    }

    public void loadImagesFromIMGLIST()
    {
        for (int imgTexts = 0; imgTexts < imgList.Count; imgTexts++)
        {
            try
            {
                StreamReader inputImgText = null;
                Debug.Log("Loading img text: " + gameDir + imgList[imgTexts]);
                inputImgText = File.OpenText(gameDir + imgList[imgTexts]);
                String line = null; // not declared within while loop
                while ((line = inputImgText.ReadLine()) != null)
                {
                    if (line.StartsWith("platformimg:"))
                    {
                        line = line.Replace("platformimg:", "pc");
                        line = line.Replace("\t", "");
                        img.Add(line);
                        Debug.Log(line);
                    }
                }
                inputImgText.Close();
            }
            catch (IOException ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public bool openGTA_DAT()
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

    public bool closeGTA_DAT()
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
