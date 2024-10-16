using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GTAIVUtils
{
    private static String LOG_TAG = "GTAIVUtils";

    /** Hash of the encryption key */
    private static String ENCRYPTION_KEY_HASH = "1ab56fed7ec3ff01227b691533975dce47d769653ff775426a96cd6d5307565d";

    /** Length of the encryption key */
    private static int ENCRYPTION_KEY_LENGTH = 32;

    /** file name that contains the settings */
    private static String VERSION_FILE_NAME = "versions.json";

    public static byte[] findKey(String pGameDir)
    {
        ReadFunctions readFunctions = new ReadFunctions();
        readFunctions.openFile(pGameDir + "GTAIV.exe");

        byte[] key = new byte[ENCRYPTION_KEY_LENGTH];
        Version[] versions = getVersions();

        // There was a problem loading the versions return
        if (versions == null)
        {
            Debug.Log("Versions not found");
            return null;
        }

        // Loop through all version to retrieve the encryption key
        foreach (Version version in versions)
        {
            readFunctions.seek(version.getVersionOffset());
            key = readFunctions.readArray(ENCRYPTION_KEY_LENGTH);

            // Check if the key is valid, if it's not continue
            if (isEncryptionKeyValid(key))
            {
                Debug.Log("Found version: " + version.getVersionName());
                break;
            }
            key = null;
        }

        readFunctions.closeFile();
        return key;
    }

    private static Version[] getVersions()
    {
        string versionFilePath = $"{Application.streamingAssetsPath}/{VERSION_FILE_NAME}";

        if (File.Exists(versionFilePath))
        {
            try
            {
                using (StreamReader reader = new StreamReader(versionFilePath))
                {
                    string json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<Version[]>(json);
                }
            }
            catch (FileNotFoundException e)
            {
                Debug.Log(e.Message);
            }
        }
        return null;
    }


    private static bool isEncryptionKeyValid(byte[] pEncryptionKey)
    {
        return asHex(pEncryptionKey).Equals(ENCRYPTION_KEY_HASH);
    }

    private static string asHex(byte[] bytes)
    {
        string result = "";
        foreach (byte b in bytes)
        {
            result += ((b & 0xff) + 0x100).ToString("x2").Substring(1);
        }
        return result;
    }


    [System.Serializable]
    private class Version
    {

        public string mVersionName;


        public string mVersionOffset;

        public String getVersionName()
        {
            return mVersionName;
        }

        public long getVersionOffset()
        {
            if (string.IsNullOrEmpty(mVersionOffset))
                throw new ArgumentNullException(nameof(mVersionOffset));

            // Check for hexadecimal
            if (mVersionOffset.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || mVersionOffset.StartsWith("#"))
            {
                return Convert.ToInt64(mVersionOffset.Substring(2), 16);  // Hexadecimal
            }
            // Check for octal (starting with '0')
            else if (mVersionOffset.StartsWith("0") && mVersionOffset.Length > 1)
            {
                return Convert.ToInt64(mVersionOffset, 8);  // Octal
            }
            else
            {
                return Convert.ToInt64(mVersionOffset);  // Decimal
            }
        }
    }
}
