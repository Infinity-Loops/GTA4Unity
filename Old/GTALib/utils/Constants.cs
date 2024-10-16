using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    /**
	 * Enum of GameType
	 */
    public enum GameType
    {
        GTA_III, // --
        GTA_VC, // --
        GTA_SA, // --
        GTA_IV, // --
        GTA_V
    }

    // file types
    public const int ftDFF = 0;
    public const int ftTXD = 1;
    public const int ftCOL = 2;
    public const int ftIPL = 3;
    public const int ftIDE = 4;
    public const int ftWDR = 5;
    public const int ftWDD = 6;
    public const int ftWFT = 7;
    public const int ftWBN = 8;
    public const int ftWBD = 9;
    public const int ftWTD = 10;
    public const int ftWPL = 11;
    public const int ftWAD = 12;
    public const int ftIFP = 13;

    // Resource types
    public const int rtWDR = 110;
    public const int rtWDD = 110;
    public const int rtWFT = 112;
    public const int rtWBN = 32;
    public const int rtWBD = 32;
    public const int rtWTD = 8;
    public const int rtWPL = 1919251285;
    public const int rtWAD = 1;
    public const int rtCUT = 1162696003;

    // Game versions OUTDATED

    public const int gIII = 0;

    public const int gVC = 1;

    public const int gSA = 2;

    public const int gIV = 3;

    // Placement
    public const int pINST = 0;
    public const int pCULL = 1;
    public const int pPATH = 2;
    public const int pGRGE = 3;
    public const int pENEX = 4;
    public const int pPICK = 5;
    public const int pJUMP = 6;
    public const int pTCYC = 7;
    public const int pAUZO = 8;
    public const int pMULT = 9;
    public const int pCARS = 10;
    public const int pOCCL = 11;
    public const int pZONE = 12;

    // IDE
    public const int i2DFX = 0;
    public const int iANIM = 1;
    public const int iCARS = 2;
    public const int iHIER = 3;
    public const int iMLO = 4;
    public const int iOBJS = 5;
    public const int iPATH = 6;
    public const int iPEDS = 7;
    public const int iTANM = 8;
    public const int iTOBJ = 9;
    public const int iTREE = 10;
    public const int iTXDP = 11;
    public const int iWEAP = 12;

    public const int fileOpen = 0;
    public const int fileSave = 1;
}
