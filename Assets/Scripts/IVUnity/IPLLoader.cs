using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IPLLoader
{
    public List<IPL> ipls = new List<IPL>();
    public string name;

    public void LoadIPL(string name, byte[] data)
    {
        this.name = name;

        IPL ipl = new IPL(data);
        ipls.Add(ipl);
    }
}
