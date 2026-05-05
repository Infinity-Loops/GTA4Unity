using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum IDEReader : int
{
    None = -1,
    i2DFX = 0,
    iANIM = 1,
    iCARS = 2,
    iHIER = 3,
    iMLO = 4,
    iOBJS = 5,
    iPATH = 6,
    iPEDS = 7,
    iTANM = 8,
    iTOBJ = 9,
    iTREE = 10,
    iTXDP = 11,
    iWEAP = 12,
    iAGRPS = 13,
}

/// <summary>
/// Base for every IDE item. Items override Read(ref LineParser) and pull typed fields in
/// declaration order — the parser handles culture-invariant numeric parsing and
/// per-field error reporting. The default implementation is a no-op so unsupported
/// item types compile as bare empty classes.
/// </summary>
public abstract class IDE_Item
{
    public virtual void Read(ref LineParser parser)
    {
        // Default: do nothing. Override in items that actually read fields.
    }
}

public class IDE
{
    private StreamReader reader;
    private IDEReader readItem = IDEReader.None;

    public List<Item_OBJS> items_objs = new();
    public List<Item_TOBJ> items_tobj = new();
    public List<Item_TREE> items_tree = new();
    public List<Item_PATH> items_path = new();
    public List<Item_ANIM> items_anim = new();
    public List<Item_TANM> items_tanm = new();
    public List<Item_MLO>  items_mlo  = new();
    public List<Item_2DFX> items_2dfx = new();
    public List<Item_AMAT> items_amat = new();
    public List<Item_TXDP> items_txdp = new();
    public List<Item_CARS> items_cars = new();
    public List<Item_PEDS> items_peds = new();
    public List<Item_AGRPS> items_agrps = new();

    public IDE(string fileName)
    {
        reader = File.OpenText(fileName);

        string line = null;
        while ((line = reader.ReadLine()) != null)
        {
            if (readItem == IDEReader.None)
            {
                if      (line.StartsWith("#"))    { /* Comment */ }
                else if (line.StartsWith("2dfx")) readItem = IDEReader.i2DFX;
                else if (line.StartsWith("anim")) readItem = IDEReader.iANIM;
                else if (line.StartsWith("cars")) readItem = IDEReader.iCARS;
                else if (line.StartsWith("hier")) readItem = IDEReader.iHIER;
                else if (line.StartsWith("mlo"))  readItem = IDEReader.iMLO;
                else if (line.StartsWith("objs")) readItem = IDEReader.iOBJS;
                else if (line.StartsWith("path")) readItem = IDEReader.iPATH;
                else if (line.StartsWith("peds")) readItem = IDEReader.iPEDS;
                else if (line.StartsWith("tanm")) readItem = IDEReader.iTANM;
                else if (line.StartsWith("tobj")) readItem = IDEReader.iTOBJ;
                else if (line.StartsWith("tree")) readItem = IDEReader.iTREE;
                else if (line.StartsWith("txdp")) readItem = IDEReader.iTXDP;
                else if (line.StartsWith("weap")) readItem = IDEReader.iWEAP;
                else if (line.StartsWith("agrps")) readItem = IDEReader.iAGRPS;
                continue;
            }

            if (line.StartsWith("end"))     { readItem = IDEReader.None; continue; }
            if (line.StartsWith("#"))       continue;
            if (string.IsNullOrEmpty(line)) continue;

            if (readItem == IDEReader.iMLO)
            {
                ReadMloBlock(line, fileName);
                continue;
            }

            IDE_Item item = readItem switch
            {
                IDEReader.i2DFX => new Item_2DFX(),
                IDEReader.iANIM => Track(items_anim, new Item_ANIM()),
                IDEReader.iCARS => Track(items_cars, new Item_CARS()),
                IDEReader.iHIER => new Item_HIER(),
                IDEReader.iOBJS => Track(items_objs, new Item_OBJS()),
                IDEReader.iPATH => new Item_PATH(),
                IDEReader.iPEDS => Track(items_peds, new Item_PEDS()),
                IDEReader.iTANM => new Item_TANM(),
                IDEReader.iTOBJ => Track(items_tobj, new Item_TOBJ()),
                IDEReader.iTREE => new Item_TREE(),
                IDEReader.iTXDP => Track(items_txdp, new Item_TXDP()),
                IDEReader.iWEAP => new Item_WEAP(),
                IDEReader.iAGRPS => Track(items_agrps, new Item_AGRPS()),
                _               => null,
            };
            if (item == null) continue;

            try
            {
                var parser = new LineParser(line);
                item.Read(ref parser);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IDE {Path.GetFileName(fileName)}] Skipped line '{line}' — {ex.Message}");
            }
        }

        reader.Close();
        reader.Dispose();
    }

    private void ReadMloBlock(string headerLine, string fileName)
    {
        // headerLine is the MLO header: "ModelName, field, numRooms, numPortals, numEntities, drawDist, ..."
        // Skip mlo sub-section keywords
        if (headerLine.StartsWith("mloroomstart") || headerLine.StartsWith("mloportalstart") ||
            headerLine.StartsWith("mloend") || headerLine.StartsWith("roomend"))
            return;

        // Tab-indented lines are entity entries or room data inside the current block
        if (headerLine.StartsWith("\t") || headerLine.StartsWith("  "))
        {
            // Entity entry for current MLO block
            if (items_mlo.Count == 0) return;
            var current = items_mlo[items_mlo.Count - 1];

            string trimmed = headerLine.Trim().TrimEnd(',');
            if (string.IsNullOrEmpty(trimmed)) return;

            // Skip lines that are just indices (room entity lists: "0, 1, 2, 3, ...")
            // These start with a digit, entity lines start with a model name (letter)
            if (trimmed.Length > 0 && (char.IsDigit(trimmed[0]) || trimmed[0] == '-'))
                return;
            // Skip room definition lines (e.g., "limbo, 5, 7, ...")
            // These appear after mloroomstart — but since we don't track sub-state,
            // check if we've already read enough entities
            if (current.Entities.Count >= current.numEntities)
                return;

            try
            {
                current.ReadEntity(trimmed);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IDE {Path.GetFileName(fileName)}] MLO entity skip: {ex.Message}");
            }
            return;
        }

        // Non-indented, non-keyword: new MLO header
        try
        {
            var mlo = new Item_MLO();
            mlo.ReadHeader(headerLine);
            items_mlo.Add(mlo);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IDE {Path.GetFileName(fileName)}] MLO header skip: {ex.Message}");
        }
    }

    /// <summary>Add to a typed list and return the same item — keeps the switch concise.</summary>
    private static T Track<T>(List<T> list, T item) where T : IDE_Item
    {
        list.Add(item);
        return item;
    }

    public IDE_Item FindItem(string name)
    {
        foreach (var obj in items_objs)
        {
            if (obj.modelName == name) return obj;
        }
        return null;
    }
}
