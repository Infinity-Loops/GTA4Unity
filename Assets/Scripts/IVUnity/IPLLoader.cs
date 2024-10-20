using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IPLLoader
{
    public List<IPL> ipls = new List<IPL>();
    public void LoadIPL(string name, byte[] data)
    {
        IPL ipl = new IPL(data, name);
        ipls.Add(ipl);
    }
}
