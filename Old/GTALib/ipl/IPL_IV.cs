using IniReader;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class IPL_IV
{
    private int version; // always 3
    private int inst; // Number of instances
    private int unused1; // unused
    private int grge; // number of garages
    private int cars; // number of cars
    private int cull; // number of culls
    private int unused2; // unused
    private int unused3; // unsued
    private int unused4; // unused
    private int strbig; // number of strbig
    private int lcul; // number of lod cull
    private int zone; // number of zones
    private int unused5; // unused
    private int unused6; // unused
    private int unused7; // unused
    private int unused8; // unused
    private int blok; // number of bloks

    public void loadPlacement(IPL wpl)
    {
        Debug.Log("Loading.. bin wpl");
        ReadFunctions rf;
        if (wpl.rf == null)
        {
            rf = new ReadFunctions();
            rf.openFile(wpl.getFileName());
        }
        else
        {
            wpl.stream = true;
            rf = wpl.rf;
        }
        readHeader(rf);

        IDictionary<string, IDictionary<string, object>> ini = null;

        try
        {
            var stream = File.OpenText($"{Application.streamingAssetsPath}/hashes.ini");
            ini = Ini.Read(stream);
        }
        catch (IOException ex)
        {
            Debug.LogError("Unable to open INI");
        }

        for (int i = 0; i < inst; i++)
        {
            Item_INST item = new Item_INST(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_inst.Add(item);
        }
        for (int i = 0; i < grge; i++)
        {
            Item_GRGE item = new Item_GRGE(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_grge.Add(item);
        }
        for (int i = 0; i < cars; i++)
        {
            Item_CARS_IPL item = new Item_CARS_IPL(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_cars.Add(item);
        }
        for (int i = 0; i < cull; i++)
        {
            Item_CULL item = new Item_CULL(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_cull.Add(item);
        }
        for (int i = 0; i < strbig; i++)
        {
            Item_STRBIG item = new Item_STRBIG(wpl.getGameType());
            item.read(rf);
            wpl.items_strbig.Add(item);
        }
        for (int i = 0; i < lcul; i++)
        {
            Item_LCUL item = new Item_LCUL(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_lcul.Add(item);
        }
        for (int i = 0; i < zone; i++)
        {
            Item_ZONE item = new Item_ZONE(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_zone.Add(item);
        }
        for (int i = 0; i < blok; i++)
        {
            Item_BLOK item = new Item_BLOK(wpl.getGameType());
            item.read(rf, ini);
            wpl.items_blok.Add(item);
        }

        ini = null;

        if (wpl.rf == null)
        {
            rf.closeFile();
        }
        wpl.loaded = true;
    }

    public void readHeader(ReadFunctions rf)
    {
        version = rf.readInt(); // always 3
        inst = rf.readInt(); // Number of instances
        unused1 = rf.readInt(); // unused
        grge = rf.readInt(); // number of garages
        cars = rf.readInt(); // number of cars
        cull = rf.readInt(); // number of culls
        unused2 = rf.readInt(); // unused
        unused3 = rf.readInt(); // unsued
        unused4 = rf.readInt(); // unused
        strbig = rf.readInt(); // number of strbig
        lcul = rf.readInt(); // number of lod cull
        zone = rf.readInt(); // number of zones
        unused5 = rf.readInt(); // unused
        unused6 = rf.readInt(); // unused
        unused7 = rf.readInt(); // unused
        unused8 = rf.readInt(); // unused
        blok = rf.readInt(); // number of bloks
                             // System.out.println(version);
                             // System.out.println(inst);
        /* System.out.println(unused1); System.out.println(grge); System.out.println(cars); System.out.println(cull);
		 * System.out.println(unused2); System.out.println(unused3); System.out.println(unused4);
		 * System.out.println(strbig); System.out.println(lcul); System.out.println(zone); System.out.println(unused5);
		 * System.out.println(unused6); System.out.println(unused7); System.out.println(unused8);
		 * System.out.println(blok); */
        // //Message.displayMsgHigh
    }
}
