using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Message
{   
    // 0 = off
    // 1 = High only
    // 2 = All
    // 3 = Super
    private static int debug_mode = 2;

    public static void displayMsgHigh(String msg)
    {
        if (debug_mode == 1 || debug_mode == 2)
            Debug.Log(msg);
    }

    public static void displayMsgHigh(int msg)
    {
        if (debug_mode == 1 || debug_mode == 2)
            Debug.Log(msg);
    }

    public static void displayMsgLow(String msg)
    {
        if (debug_mode == 2)
            Debug.Log(msg);
    }

    public static void displayMsgSuper(String msg)
    {
        if (debug_mode == 3)
            Debug.Log(msg);
    }

    public static void displayMsgHigh(bool msg)
    {
        if (debug_mode == 1 || debug_mode == 2)
            Debug.Log(msg);
    }
}
