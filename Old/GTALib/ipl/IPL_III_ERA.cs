using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Windows;
using File = System.IO.File;

public class IPL_III_ERA 
{
    private StreamReader input;

    private int readItem = -1;

    public void loadPlacement(IPL ipl)
    {
        if (openIPL(ipl.getFileName()))
        {
            try
            {
                String line = null; //not declared within while loop
                while ((line = input.ReadLine()) != null)
                {
                    if (readItem == -1)
                    {
                        if (line.StartsWith("#"))
                        {
                            //Message.displayMsgHigh("Commentaar: " + line);
                        }
                        else if (line.StartsWith("inst"))
                        {
                            readItem = Constants.pINST;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("cull"))
                        {
                            readItem = Constants.pCULL;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("path"))
                        {
                            readItem = Constants.pPATH;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("grge"))
                        {
                            readItem = Constants.pGRGE;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("enex"))
                        {
                            readItem = Constants.pENEX;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("pick"))
                        {
                            readItem = Constants.pPICK;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("jump"))
                        {
                            readItem = Constants.pJUMP;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("tcyc"))
                        {
                            readItem = Constants.pTCYC;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("auzo"))
                        {
                            readItem = Constants.pAUZO;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("mult"))
                        {
                            readItem = Constants.pMULT;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("cars"))
                        {
                            readItem = Constants.pCARS;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("occl"))
                        {
                            readItem = Constants.pOCCL;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                        else if (line.StartsWith("zone"))
                        {
                            readItem = Constants.pZONE;
                            //Message.displayMsgHigh("Started reading item " + readItem);
                        }
                    }
                    else
                    {
                        if (line.StartsWith("#"))
                        {
                            //Message.displayMsgHigh("Commentaar: " + line);
                        }
                        else if (line.StartsWith("end"))
                        {
                            //Message.displayMsgHigh("Item " + readItem + " ended");
                            readItem = -1;
                        }
                        else
                        {
                            IPL_Item item = null;
                            switch (readItem)
                            {
                                case Constants.pINST:
                                    item = new Item_INST(ipl.getGameType());
                                    ipl.items_inst.Add((Item_INST)item);
                                    break;
                                case Constants.pAUZO:
                                    item = new Item_AUZO(ipl.getGameType());
                                    break;
                                case Constants.pCARS:
                                    item = new Item_CARS_IPL(ipl.getGameType());
                                    break;
                                case Constants.pCULL:
                                    item = new Item_CULL(ipl.getGameType());
                                    break;
                                case Constants.pENEX:
                                    item = new Item_ENEX(ipl.getGameType());
                                    break;
                                case Constants.pJUMP:
                                    item = new Item_JUMP(ipl.getGameType());
                                    break;
                                case Constants.pGRGE:
                                    item = new Item_GRGE(ipl.getGameType());
                                    break;
                                case Constants.pMULT:
                                    item = new Item_MULT(ipl.getGameType());
                                    break;
                                case Constants.pOCCL:
                                    item = new Item_OCCL(ipl.getGameType());
                                    break;
                                case Constants.pPATH:
                                    item = new Item_PATH_IPL(ipl.getGameType());
                                    break;
                                case Constants.pPICK:
                                    item = new Item_PICK(ipl.getGameType());
                                    break;
                                case Constants.pTCYC:
                                    item = new Item_TCYC(ipl.getGameType());
                                    break;
                                case Constants.pZONE:
                                    item = new Item_ZONE(ipl.getGameType());
                                    break;
                                default:
                                    break;
                                    //Message.displayMsgHigh("Unknown item " + line);
                            }
                            item.read(line);
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.LogException(ex);
            }
            closeIPL();
        }
        else
        {
            //Message.displayMsgHigh("Error: Can't open file");
        }
        ipl.loaded = true;
    }

    public bool openIPL(String fileName)
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

    public bool closeIPL()
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