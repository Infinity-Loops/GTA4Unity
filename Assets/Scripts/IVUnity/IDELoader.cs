using System;
using System.Collections.Generic;

public class IDELoader
{
    public List<IDE> ides = new List<IDE>();
    public Dictionary<string, Item_OBJS> objsDict = new Dictionary<string, Item_OBJS>();
    public Dictionary<string, Item_TOBJ> tobjDict = new Dictionary<string, Item_TOBJ>();
    public Dictionary<string, Item_MLO> mloDict = new Dictionary<string, Item_MLO>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parent-TXD chain from every IDE file's <c>txdp</c> section, keyed by the lowercase child
    /// TXD name. This is the IV equivalent of SA's txdrelations.dat — engine equivalent: the
    /// parent slot index stored on each CTxdStore slot (see FUN_008e07a0 in GTAIV.exe.c).
    /// Used by <see cref="IVUnity.Resolver.TxdStore"/> to auto-link the chain on lazy load.
    /// </summary>
    public Dictionary<string, string> txdParents =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void LoadIDE(string fileName)
    {
        IDE ide = new IDE(fileName);
        ides.Add(ide);

        foreach (var obj in ide.items_objs)
        {
            if (!string.IsNullOrEmpty(obj.modelName) && !objsDict.ContainsKey(obj.modelName))
            {
                objsDict[obj.modelName] = obj;
            }
        }

        foreach (var anim in ide.items_anim)
        {
            if (!string.IsNullOrEmpty(anim.modelName) && !objsDict.ContainsKey(anim.modelName))
            {
                objsDict[anim.modelName] = anim;
            }
        }

        foreach (var tobj in ide.items_tobj)
        {
            if (!string.IsNullOrEmpty(tobj.modelName) && !tobjDict.ContainsKey(tobj.modelName))
            {
                tobjDict[tobj.modelName] = tobj;
            }
        }

        foreach (var mlo in ide.items_mlo)
        {
            if (!string.IsNullOrEmpty(mlo.modelName) && !mloDict.ContainsKey(mlo.modelName))
            {
                mloDict[mlo.modelName] = mlo;
            }
        }

        foreach (var txdp in ide.items_txdp)
        {
            if (string.IsNullOrEmpty(txdp.texDic) || string.IsNullOrEmpty(txdp.texDicParent)) continue;
            // First write wins — same convention as the OBJS/TOBJ dicts above. Conflicting
            // parents across IDE files are rare in practice.
            if (!txdParents.ContainsKey(txdp.texDic))
            {
                txdParents[txdp.texDic] = txdp.texDicParent;
            }
        }
    }
}
