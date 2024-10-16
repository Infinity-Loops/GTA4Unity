using RageLib.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using Unity.VisualScripting;
using UnityEngine;

public class IPL
{
    public List<Ipl_AUZO> ipl_auzo = new();
    public List<Ipl_CARS> ipl_cars = new();
    public List<Ipl_CULL> ipl_cull = new();
    public List<Ipl_ENEX> ipl_enex = new();
    public List<Ipl_GRGE> ipl_grge = new();
    public List<Ipl_INST> ipl_inst = new();
    public List<Ipl_JUMP> ipl_jump = new();
    public List<Ipl_MULT> ipl_mult = new();
    public List<Ipl_OCCL> ipl_occl = new();
    public List<Ipl_PATH> ipl_path = new();
    public List<Ipl_PICK> ipl_pick = new();
    public List<Ipl_TCYC> ipl_tcyc = new();
    public List<Ipl_STRBIG> ipl_strbig = new();
    public List<Ipl_LCUL> ipl_lcul = new();
    public List<Ipl_ZONE> ipl_zone = new();
    public List<Ipl_BLOK> ipl_blok = new();

    public int lodWPL = -1;

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

    BinaryReader reader;
    MemoryStream stream;

    public IPL(byte[] data)
    {
        stream = new MemoryStream(data);
        reader = new BinaryReader(stream);
        ReadHeader(reader);

        var hashes = Hashes.table;

        for (int i = 0; i < inst; i++)
        {
            Ipl_INST item = new Ipl_INST();
            item.Read(reader, hashes);
            ipl_inst.Add(item);
        }
        for (int i = 0; i < grge; i++)
        {
            Ipl_GRGE item = new Ipl_GRGE();
            item.Read(reader, hashes);
            ipl_grge.Add(item);
        }
        for (int i = 0; i < cars; i++)
        {
            Ipl_CARS item = new Ipl_CARS();
            item.Read(reader, hashes);
            ipl_cars.Add(item);
        }
        for (int i = 0; i < cull; i++)
        {
            Ipl_CULL item = new Ipl_CULL();
            item.Read(reader, hashes);
            ipl_cull.Add(item);
        }
        for (int i = 0; i < strbig; i++)
        {
            Ipl_STRBIG item = new Ipl_STRBIG();
            item.Read(reader);
            ipl_strbig.Add(item);
        }
        for (int i = 0; i < lcul; i++)
        {
            Ipl_LCUL item = new Ipl_LCUL();
            item.Read(reader, hashes);
            ipl_lcul.Add(item);
        }
        for (int i = 0; i < zone; i++)
        {
            Ipl_ZONE item = new Ipl_ZONE();
            item.Read(reader, hashes);
            ipl_zone.Add(item);
        }
        for (int i = 0; i < blok; i++)
        {
            Ipl_BLOK item = new Ipl_BLOK();
            item.Read(reader, hashes);
            ipl_blok.Add(item);
        }
    }

    public void ReadHeader(BinaryReader reader)
    {
        version = reader.ReadInt(); // always 3
        Debug.Log($"Header Version: {version}");
        inst = reader.ReadInt(); // Number of instances
        unused1 = reader.ReadInt(); // unused
        grge = reader.ReadInt(); // number of garages
        cars = reader.ReadInt(); // number of cars
        cull = reader.ReadInt(); // number of culls
        unused2 = reader.ReadInt(); // unused
        unused3 = reader.ReadInt(); // unsued
        unused4 = reader.ReadInt(); // unused
        strbig = reader.ReadInt(); // number of strbig
        lcul = reader.ReadInt(); // number of lod cull
        zone = reader.ReadInt(); // number of zones
        unused5 = reader.ReadInt(); // unused
        unused6 = reader.ReadInt(); // unused
        unused7 = reader.ReadInt(); // unused
        unused8 = reader.ReadInt(); // unused
        blok = reader.ReadInt(); // number of bloks
    }
}


public abstract class IPL_Item
{

    public abstract void Read(string line);

    public abstract void Read(BinaryReader reader);

    public abstract void Read(BinaryReader reader, IniJson ini);

}


public class Ipl_BLOK : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_ZONE : IPL_Item
{
    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;

    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        posLowerLeft = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_LCUL : IPL_Item
{
    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;
    public int unk1;
    public long hash1, hash2, hash3, hash4, hash5;
    public long hash6, hash7, hash8, hash9, hash10;
    public String name1, name2, name3, name4, name5;
    public String name6, name7, name8, name9, name10;

    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        posLowerLeft = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1 = reader.ReadInt();
        hash1 = reader.ReadUInt();
        hash2 = reader.ReadUInt();
        hash3 = reader.ReadUInt();
        hash4 = reader.ReadUInt();
        hash5 = reader.ReadUInt();
        hash6 = reader.ReadUInt();
        hash7 = reader.ReadUInt();
        hash8 = reader.ReadUInt();
        hash9 = reader.ReadUInt();
        hash10 = reader.ReadUInt();
        name1 = reader.ReadString(32);
        name2 = reader.ReadString(32);
        name3 = reader.ReadString(32);
        name4 = reader.ReadString(32);
        name5 = reader.ReadString(32);
        name6 = reader.ReadString(32);
        name7 = reader.ReadString(32);
        name8 = reader.ReadString(32);
        name9 = reader.ReadString(32);
        name10 = reader.ReadString(32);
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        posLowerLeft = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1 = reader.ReadInt();
        hash1 = reader.ReadUInt();
        hash2 = reader.ReadUInt();
        hash3 = reader.ReadUInt();
        hash4 = reader.ReadUInt();
        hash5 = reader.ReadUInt();
        hash6 = reader.ReadUInt();
        hash7 = reader.ReadUInt();
        hash8 = reader.ReadUInt();
        hash9 = reader.ReadUInt();
        hash10 = reader.ReadUInt();
        name1 = reader.ReadString(32);
        name2 = reader.ReadString(32);
        name3 = reader.ReadString(32);
        name4 = reader.ReadString(32);
        name5 = reader.ReadString(32);
        name6 = reader.ReadString(32);
        name7 = reader.ReadString(32);
        name8 = reader.ReadString(32);
        name9 = reader.ReadString(32);
        name10 = reader.ReadString(32);
    }
}

public class Ipl_STRBIG : IPL_Item
{
    public string modelName;
    public int unk1, unk2, unk3;
    public Vector3 pos;
    public Vector4 rot;

    public override void Read(string line)
    {

    }

    public override void Read(BinaryReader reader)
    {
        modelName = reader.ReadString(24);
        unk1 = reader.ReadInt();
        unk2 = reader.ReadInt();
        unk3 = reader.ReadInt();
        pos = reader.ReadVector3();
        rot = reader.ReadVector3();
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {

    }
}

public class Ipl_TCYC : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_PICK : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_PATH : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_OCCL : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_MULT : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_JUMP : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_INST : IPL_Item
{
    public int id;
    public string name = "";
    public int hash = 0;
    public int interior;
    public Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 scale = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector4 rotation = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

    public int lod;
    public int unknown1, unknown2;
    public float unknown3;
    public float drawDistance = 300.0f;

    public Vector4 axisAngle = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
    internal int glListID;

    internal Quaternion unityRotation
    {
        get
        {
            return new Quaternion(rotation.x,rotation.y, rotation.z, -rotation.w);
        }
    }

    public override void Read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(",");
        axisAngle = rotation.GetAxisAngle();
    }

    public override void Read(BinaryReader reader)
    {

    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        rotation = new Vector4(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;

        name = ini.GetValue<string>("Hashes", name); // temp
        if (name == null)
        {
            name = "";
            Debug.LogError($"Hash {hash} not found for IPL Instance.");
        }

        unknown1 = reader.ReadInt();

        lod = reader.ReadInt();
        unknown2 = reader.ReadInt();

        unknown3 = reader.ReadFloat();

    }
}

public class Ipl_GRGE : IPL_Item
{
    public Vector3 lowLeftPos;
    public float lineX, lineY;
    public Vector3 topRightPos;
    public int doorType;
    public int garageType;
    public int hash;
    public string name;
    public int unknown;

    public override void Read(string line)
    {

    }

    public override void Read(BinaryReader reader)
    {

    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        lowLeftPos = reader.ReadVector3();
        lineX = reader.ReadFloat();
        lineY = reader.ReadFloat();
        topRightPos = reader.ReadVector3();
        doorType = reader.ReadInt();
        garageType = reader.ReadInt();
        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = ini.GetValue<string>("Hashes", name); // temp
        unknown = reader.ReadInt();
    }
}

public class Ipl_ENEX : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}

public class Ipl_CULL : IPL_Item
{
    public Vector3 posLowerLeft;
    public Vector3 posUpperRight;
    public int unk1, unk2, unk3, unk4;
    public long hash;
    public string name;

    public override void Read(string line)
    {

    }

    public override void Read(BinaryReader reader)
    {
        posLowerLeft = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1 = reader.ReadInt();
        unk2 = reader.ReadInt();
        unk3 = reader.ReadInt();
        unk4 = reader.ReadInt();
        hash = reader.ReadInt();
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        posLowerLeft = reader.ReadVector3();
        posUpperRight = reader.ReadVector3();
        unk1 = reader.ReadInt();
        unk2 = reader.ReadInt();
        unk3 = reader.ReadInt();
        unk4 = reader.ReadInt();
        hash = reader.ReadInt();

        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        name = ini.GetValue<string>("Hashes", name); // temp

        Debug.Log($"Cull Read: {name}");
    }
}

public class Ipl_CARS : IPL_Item
{
    public Vector3 position = new Vector3();
    public Vector3 rotation = new Vector3();
    public int hash;
    public String name;
    public int unknown1, unknown2, unknown3, unknown4, unknown5, unknown6, unknown7;
    public int type = 0;

    public override void Read(string line)
    {

    }

    public override void Read(BinaryReader reader)
    {

    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        position = reader.ReadVector3();
        rotation = reader.ReadVector3();
        long tempHash = reader.ReadUInt();
        name = "" + tempHash;
        hash = (int)tempHash;
        if (ini.HasOption("Cars", name))
        {
            name = ini.GetValue<string>("Cars", name); // temp
        }
        else
        {
            name = "";
        }
        unknown1 = reader.ReadInt();
        unknown2 = reader.ReadInt();
        unknown3 = reader.ReadInt();
        unknown4 = reader.ReadInt();
        unknown5 = reader.ReadInt();
        unknown6 = reader.ReadInt();
        unknown7 = reader.ReadInt();
    }
}

public class Ipl_AUZO : IPL_Item
{
    public override void Read(string line)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }

    public override void Read(BinaryReader reader, IniJson ini)
    {
        Debug.Log($"{GetType().Name} not supported yet.");
    }
}