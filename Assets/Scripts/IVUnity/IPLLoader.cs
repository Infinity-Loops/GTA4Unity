using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IPLLoader
{
    public List<IPL> ipls = new List<IPL>();

    public void LoadIPL(byte[] data)
    {
        IPL ipl = new IPL(data);
        ipls.Add(ipl);
    }
}
