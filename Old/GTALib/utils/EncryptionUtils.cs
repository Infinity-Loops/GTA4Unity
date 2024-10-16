using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

public class EncryptionUtils
{
    //Check if the current system supports crypto keys larger than 128 bits
    public static bool IsUnlimitedStrengthInstalled()
    {
        try
        {
            using (Aes aes = Aes.Create())
            {
                return aes.KeySize > 128;
            }
        }
        catch (CryptographicException e)
        {
            Debug.LogException(e);
        }

        return false;
    }
}
