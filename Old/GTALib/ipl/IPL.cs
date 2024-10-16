
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IPL
{
    public ReadFunctions rf = null;
    private String fileName = "";
    private int gameType;
    public bool changed = false; // True when the file needs to be saved

    public bool loaded = false;
    public bool selected = false;
    public bool itemsLoaded = false;

    public bool stream = false; // if it's a stream wpl
    public IMG img = null; // the img it's in
    public IMG_Item imgItem = null; // img item

    public int lodWPL = -1;

    public List<Item_AUZO> items_auzo = new List<Item_AUZO>();
    public List<Item_CARS_IPL> items_cars = new List<Item_CARS_IPL>();
    public List<Item_CULL> items_cull = new List<Item_CULL>();
    public List<Item_ENEX> items_enex = new List<Item_ENEX>();
    public List<Item_GRGE> items_grge = new List<Item_GRGE>();
    public List<Item_INST> items_inst = new List<Item_INST>();
    public List<Item_JUMP> items_jump = new List<Item_JUMP>();
    public List<Item_MULT> items_mult = new List<Item_MULT>();
    public List<Item_OCCL> items_occl = new List<Item_OCCL>();
    public List<Item_PATH_IPL> items_path = new List<Item_PATH_IPL>();
    public List<Item_PICK> items_pick = new List<Item_PICK>();
    public List<Item_TCYC> items_tcyc = new List<Item_TCYC>();
    public List<Item_STRBIG> items_strbig = new List<Item_STRBIG>();
    public List<Item_LCUL> items_lcul = new List<Item_LCUL>();
    public List<Item_ZONE> items_zone = new List<Item_ZONE>();
    public List<Item_BLOK> items_blok = new List<Item_BLOK>();

    public IPL(String fileName, int gameType, bool autoLoad)
    {
        this.fileName = fileName;
        this.gameType = gameType;
        Debug.Log ("Started loading: " + this.fileName);
        if (autoLoad)
            loadPlacement();
    }

    public IPL(ReadFunctions rf, int gameType, bool autoLoad, IMG img, IMG_Item imgItem)
    {
        this.gameType = gameType;
        this.rf = rf;
        this.img = img;
        this.imgItem = imgItem;
        if (autoLoad)
            loadPlacement();
    }

    private bool loadPlacement()
    {
        switch (gameType)
        {
            case Constants.gIV:
                if (fileName.Contains("common"))
                    new IPL_III_ERA().loadPlacement(this);
                else
                    new IPL_IV().loadPlacement(this);
                break;
            default:
                new IPL_III_ERA().loadPlacement(this);
                break;
        }
        return true;
    }


    public String getFileName()
    {
        return fileName;
    }

    public void setFileName(String fileName)
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
