using IniReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class Dummy
{
    private String name;

    private int index;
    private int parent;
    private int child;

    private Vector3 rotation1;
    private Vector3 rotation2;
    private Vector3 rotation3;

    private Vector3 translation;

    private int flags;
    private int type = 0;

    public Dummy(String name)
    {
        this.name = name;
    }

    public Dummy()
    {

    }

    public int getType()
    {
        return type;
    }

    public void setType(int type)
    {
        this.type = type;
    }

    public int getChild()
    {
        return child;
    }

    public void setChild(int child)
    {
        this.child = child;
    }

    public int getFlags()
    {
        return flags;
    }

    public void setFlags(int flags)
    {
        this.flags = flags;
    }

    public int getIndex()
    {
        return index;
    }

    public void setIndex(int index)
    {
        this.index = index;
    }

    public int getParent()
    {
        return parent;
    }

    public void setParent(int parent)
    {
        this.parent = parent;
    }

    public Vector3 getRotation1()
    {
        return rotation1;
    }

    public void setRotation1(Vector3 rotation1)
    {
        this.rotation1 = rotation1;
    }

    public Vector3 getRotation2()
    {
        return rotation2;
    }

    public void setRotation2(Vector3 rotation2)
    {
        this.rotation2 = rotation2;
    }

    public Vector3 getRotation3()
    {
        return rotation3;
    }

    public void setRotation3(Vector3 rotation3)
    {
        this.rotation3 = rotation3;
    }

    public Vector3 getTranslation()
    {
        return translation;
    }

    public void setTranslation(Vector3 translation)
    {
        this.translation = translation;
    }

    public String getName()
    {
        return name;
    }

    public void setName(String name)
    {
        this.name = name;
    }

    public void setTypeByName()
    {
        try
        {
            
            IDictionary<string, IDictionary<string, object>> ini = Ini.Read(File.OpenText($"{Application.streamingAssetsPath}/dummys.ini"));
            if (ini.ContainsKey("dummys"))
            {
                if (ini["dummys"].ContainsKey("name"))
                {
                    Debug.Log("DummyName Exists");
                    this.type = int.Parse((string)ini["dummys"][name]);
                }
            }
            else
            {
                Debug.Log("Dummyname doesn't exist");
            }
        }
        catch (IOException ex)
        {
            Debug.LogException(ex);
        }
    }
}
