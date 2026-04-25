using System.Collections.Generic;
using System.IO;
using RageLib.Common;
using UnityEngine;

public class IPL
{
    public string name;
    public List<Ipl_AUZO>   ipl_auzo   = new();
    public List<Ipl_CARS>   ipl_cars   = new();
    public List<Ipl_CULL>   ipl_cull   = new();
    public List<Ipl_ENEX>   ipl_enex   = new();
    public List<Ipl_GRGE>   ipl_grge   = new();
    public List<Ipl_INST>   ipl_inst   = new();
    public List<Ipl_JUMP>   ipl_jump   = new();
    public List<Ipl_MULT>   ipl_mult   = new();
    public List<Ipl_OCCL>   ipl_occl   = new();
    public List<Ipl_PATH>   ipl_path   = new();
    public List<Ipl_PICK>   ipl_pick   = new();
    public List<Ipl_TCYC>   ipl_tcyc   = new();
    public List<Ipl_STRBIG> ipl_strbig = new();
    public List<Ipl_LCUL>   ipl_lcul   = new();
    public List<Ipl_ZONE>   ipl_zone   = new();
    public List<Ipl_BLOK>   ipl_blok   = new();

    public int lodWPL = -1;

    private int version; // always 3
    private int inst;    // Number of instances
    private int unused1; // unused
    private int grge;    // number of garages
    private int cars;    // number of cars
    private int cull;    // number of culls
    private int unused2; // unused
    private int unused3; // unsued
    private int unused4; // unused
    private int strbig;  // number of strbig
    private int lcul;    // number of lod cull
    private int zone;    // number of zones
    private int unused5; // unused
    private int unused6; // unused
    private int unused7; // unused
    private int unused8; // unused
    private int blok;    // number of bloks

    private BinaryReader reader;
    private MemoryStream stream;

    public IPL(byte[] data, string name)
    {
        this.name = name;

        stream = new MemoryStream(data);
        reader = new BinaryReader(stream);
        ReadHeader(reader);

        var hashes = Hashes.table;

        for (int i = 0; i < inst;   i++) { var item = new Ipl_INST();   item.Read(reader, hashes); ipl_inst.Add(item); }
        for (int i = 0; i < grge;   i++) { var item = new Ipl_GRGE();   item.Read(reader, hashes); ipl_grge.Add(item); }
        for (int i = 0; i < cars;   i++) { var item = new Ipl_CARS();   item.Read(reader, hashes); ipl_cars.Add(item); }
        for (int i = 0; i < cull;   i++) { var item = new Ipl_CULL();   item.Read(reader, hashes); ipl_cull.Add(item); }
        for (int i = 0; i < strbig; i++) { var item = new Ipl_STRBIG(); item.Read(reader);          ipl_strbig.Add(item); }
        for (int i = 0; i < lcul;   i++) { var item = new Ipl_LCUL();   item.Read(reader, hashes); ipl_lcul.Add(item); }
        for (int i = 0; i < zone;   i++) { var item = new Ipl_ZONE();   item.Read(reader, hashes); ipl_zone.Add(item); }
        for (int i = 0; i < blok;   i++) { var item = new Ipl_BLOK();   item.Read(reader, hashes); ipl_blok.Add(item); }
    }

    public void ReadHeader(BinaryReader reader)
    {
        version = reader.ReadInt();
        Debug.Log($"Header Version: {version}");
        inst    = reader.ReadInt();
        unused1 = reader.ReadInt();
        grge    = reader.ReadInt();
        cars    = reader.ReadInt();
        cull    = reader.ReadInt();
        unused2 = reader.ReadInt();
        unused3 = reader.ReadInt();
        unused4 = reader.ReadInt();
        strbig  = reader.ReadInt();
        lcul    = reader.ReadInt();
        zone    = reader.ReadInt();
        unused5 = reader.ReadInt();
        unused6 = reader.ReadInt();
        unused7 = reader.ReadInt();
        unused8 = reader.ReadInt();
        blok    = reader.ReadInt();
    }
}
