using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//public enum ResourceType
//{
//    TextureXBOX = (0x7), // xtd
//    ModelXBOX = (0x6D), // xdr
//    Generic = (0x01), // xhm / xad (Generic files as rsc?)
//    Bounds = (0x20), // xbd, wbd
//    Particles = (0x24), // xpfl
//    Particles2 = (0x1B), // xpfl

//    Texture = (0x8), // wtd
//    Model = (0x6E), // wdr
//    ModelFrag = (0x70) //wft*/
//}

public class ResourceType
{
    public const int TextureXBOX = 0x7; // xtd
    public const int ModelXBOX = 0x6D; // xdr
    public const int Generic = 0x01; // xhm / xad (Generic files as rsc?)
    public const int Bounds = 0x20; // xbd, wbd
    public const int Particles = 0x24; // xpfl
    public const int Particles2 = 0x1B; // xpfl
    public const int Texture = 0x8; // wtd
    public const int Model = 0x6E; // wdr
    public const int ModelFrag = 0x70; // wft

    private int type;

    public ResourceType(int type)
    {
        this.type = type;
    }
    public static ResourceType get(int type)
    {
        //return new ResourceType(type);
        return new ResourceType(Model);
    }

    public static implicit operator int(ResourceType resourceType)
    {
        return resourceType.type;
    }

    public static implicit operator ResourceType(int type)
    {
        return new ResourceType(type);
    }
}


