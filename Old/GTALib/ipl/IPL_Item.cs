using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class IPL_Item
{

    public abstract void read(String line);

    public abstract void read(ReadFunctions rf);

    public abstract void read(ReadFunctions rf, IDictionary<string, IDictionary<string, object>> ini);

}
