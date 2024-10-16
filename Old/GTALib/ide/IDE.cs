using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;

public class IDE
{
    private StreamReader input; //Reader

    private String fileName; //name of the file
    private int gameType; //gametype ie: SA

    public bool changed = false; //True when the file needs to be saved

    private int readItem = -1; //used to identify what type of section we are reading

    public List<Item_OBJS> items_objs = new List<Item_OBJS>();
    public List<Item_TOBJ> items_tobj = new List<Item_TOBJ>();
    public List<Item_TREE> items_tree = new List<Item_TREE>();
    public List<Item_PATH> items_path = new List<Item_PATH>();
    public List<Item_ANIM> items_anim = new List<Item_ANIM>();
    public List<Item_TANM> items_tanm = new List<Item_TANM>();
    public List<Item_MLO> items_mlo = new List<Item_MLO>();
    public List<Item_2DFX> items_2dfx = new List<Item_2DFX>();
    public List<Item_AMAT> items_amat = new List<Item_AMAT>();
    public List<Item_TXDP> items_txdp = new List<Item_TXDP>();
    public List<Item_CARS> items_cars = new List<Item_CARS>();

    public IDE(String fileName, int gameType, bool autoLoad)
    {
        this.fileName = fileName;
        this.gameType = gameType;
        if (autoLoad) loadIDE(); //Load the wanted IDE
    }

    private bool loadIDE()
    {
        if (openIDE())
        {
            try
            {
                string line;
                while ((line = input.ReadLine()) != null)
                {
                    if (readItem == -1)
                    {
                        if (line.StartsWith("#"))
                        {
                            Console.WriteLine("Comment: " + line);
                        }
                        else if (line.StartsWith("2dfx")) { readItem = Constants.i2DFX; }
                        else if (line.StartsWith("anim")) { readItem = Constants.iANIM; }
                        else if (line.StartsWith("cars")) { readItem = Constants.iCARS; }
                        else if (line.StartsWith("hier")) { readItem = Constants.iHIER; }
                        else if (line.StartsWith("mlo")) { readItem = Constants.iMLO; }
                        else if (line.StartsWith("objs")) { readItem = Constants.iOBJS; }
                        else if (line.StartsWith("path")) { readItem = Constants.iPATH; }
                        else if (line.StartsWith("peds")) { readItem = Constants.iPEDS; }
                        else if (line.StartsWith("tanm")) { readItem = Constants.iTANM; }
                        else if (line.StartsWith("tobj")) { readItem = Constants.iTOBJ; }
                        else if (line.StartsWith("tree")) { readItem = Constants.iTREE; }
                        else if (line.StartsWith("txdp")) { readItem = Constants.iTXDP; }
                        else if (line.StartsWith("weap")) { readItem = Constants.iWEAP; }
                    }
                    else
                    {
                        if (line.StartsWith("end"))
                        {
                            readItem = -1;
                        }
                        else if (line.StartsWith("#"))
                        {
                            Console.WriteLine("Comment: " + line);
                        }
                        else if (string.IsNullOrEmpty(line))
                        {
                            Console.WriteLine("Empty line");
                        }
                        else
                        {
                            IDE_Item item = null;
                            switch (readItem)
                            {
                                case Constants.i2DFX: item = new Item_2DFX(gameType); break;
                                case Constants.iANIM: item = new Item_ANIM(gameType); break;
                                case Constants.iCARS: item = new Item_CARS(gameType); items_cars.Add((Item_CARS)item); break;
                                case Constants.iHIER: item = new Item_HIER(gameType); break;
                                case Constants.iMLO: item = new Item_MLO(gameType); break;
                                case Constants.iOBJS: item = new Item_OBJS(gameType); items_objs.Add((Item_OBJS)item); break;
                                case Constants.iPATH: item = new Item_PATH(gameType); break;
                                case Constants.iPEDS: item = new Item_PEDS(gameType); break;
                                case Constants.iTANM: item = new Item_TANM(gameType); break;
                                case Constants.iTOBJ: item = new Item_TOBJ(gameType); items_tobj.Add((Item_TOBJ)item); break;
                                case Constants.iTREE: item = new Item_TREE(gameType); break;
                                case Constants.iTXDP: item = new Item_TXDP(gameType); items_txdp.Add((Item_TXDP)item); break;
                                case Constants.iWEAP: item = new Item_WEAP(gameType); break;
                                default: Console.WriteLine("Unknown item: " + line); break;
                            }
                            item.read(line);
                        }
                    }
                }
                closeIDE();
            }
            catch (IOException ex)
            {
               Debug.LogException(ex);
            }
        }
        else
        {
           Debug.LogError("Unable to open file: " + fileName);
        }
        return true;
    }

    public IDE_Item findItem(string name)
    {
        IDE_Item ret = null;
        if (items_objs.Count != 0)
        {
            int i = 0;
            Item_OBJS item = items_objs[i];
            while (!item.modelName.Equals(name))
            {
                if (i < items_objs.Count - 1)
                {
                    i++;
                    item = items_objs[i];
                }
                else
                {
                    break;
                }
            }
            if (item.modelName.Equals(name))
            {
                ret = items_objs[i];
            }
        }
        return ret;
    }

    public bool openIDE()
    {
        try
        {
            input = new StreamReader(fileName);
        }
        catch (IOException)
        {
            return false;
        }
        return true;
    }

    public bool closeIDE()
    {
        try
        {
            input.Close();
        }
        catch (IOException)
        {
            return false;
        }
        return true;
    }

    public string getFileName()
    {
        return fileName;
    }

    public void setFileName(string fileName)
    {
        this.fileName = fileName;
    }

    public int getGameType()
    {
        return gameType;
    }

    public void setGameType(int gameType)
    {
        this.gameType = gameType;
    }
}
