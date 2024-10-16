using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IMG_Item 
{
    public int type;
    public int offset;
    public int size;
    public String name;

    public bool resource = false;
    public int flags = 0;

    public String getName()
    {
        return name;
    }

    public void setName(String name)
    {
        this.name = name;
    }

    public int getOffset()
    {
        return offset;
    }

    public void setOffset(int offset)
    {
        this.offset = offset;
    }

    public int getType()
    {
        return type;
    }

    public void setType(int type)
    {
        if (type < 1000)
        {
            resource = true;
        }
        this.type = type;
    }

    public int getSize()
    {
        return size;
    }

    public void setSize(int size)
    {
        this.size = size;
    }

    public int getFlags()
    {
        return flags;
    }

    public void setFlags(int flags)
    {
        this.flags = flags;
    }

    public bool isResource()
    {
        return resource;
    }

    public void setResource(bool resource)
    {
        this.resource = resource;
    }
}
