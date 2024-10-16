using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HashUtils
{
    public static long genHash(string key)
    {
        int hash = 0;

        for (int i = 0; i < key.Length; i++)
        {
            hash += (key[i] & 0xFF);
            hash += (hash << 10);
            hash ^= (hash >> 6);
        }

        hash += (hash << 3);
        hash ^= (hash >> 11);
        hash += (hash << 15);

        long ret = (long)(hash & 0xFFFFFFFFL);
        return ret;
    }

}
