using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class ReadFunctions
{
    private FileStream data_in;

    public bool hasFlag(int flags, int flag)
    {
        Debug.Log("Total flags: " + Convert.ToString(flags, 2) + " " + Convert.ToString(flag, 2) + " " + flag);
        bool hasFlag = false;
        bool finished = false;
        int waarde = 2048;
        int newFlag = flags;
        while (!finished)
        {
            Debug.Log("Waarde: " + waarde + " newFlag " + newFlag);
            newFlag -= waarde;
            if (waarde < flag)
            {
                finished = true;
            }
            else
            {
                if (newFlag <= 0)
                {
                    if (waarde == 1)
                    {
                        finished = true;
                    }
                    newFlag = flags;
                    waarde /= 2;
                }
                else if (flag == newFlag)
                {
                    hasFlag = true;
                    finished = true;
                }
                else
                {
                    flags = newFlag;
                    if (waarde == flag)
                    {
                        hasFlag = true;
                        finished = true;
                    }
                    else
                    {
                        waarde /= 2;
                    }
                }
            }
        }
        Debug.Log(hasFlag);
        return hasFlag;
    }

    public bool openFile(String name)
    {
        bool ret = true;
        try
        {
            data_in = File.Open(name, FileMode.Open);
        }
        catch (FileNotFoundException ex)
        {
            ret = false;
        }
        return ret;
    }

    public bool closeFile()
    {
        bool ret = true;
        try
        {
            data_in.Close();
        }
        catch (IOException ex)
        {
            Debug.Log("Unable to close file");
            ret = false;
        }
        return ret;
    }

    public MemoryStream getByteBuffer(int size)
    {
        byte[] buffer = new byte[size];
        MemoryStream bbuf = new MemoryStream(size);
        for (int i = 0; i < size; i++)
        {
            buffer[i] = readByte();
        }
        bbuf.Write(buffer, 0, size);
        bbuf.Position = 0;
        return bbuf;
    }

    public void skipBytes(int aantal)
    {
        try
        {
            data_in.Position += aantal;
        }
        catch (IOException ex)
        {
            Debug.LogException(ex);
        }
    }

    public int readByteAsInt()
    {
        int waarde = -1;
        try
        {
            waarde = data_in.ReadByte();
        }
        catch (IOException ex)
        {
            waarde = -1;
        }
        return waarde & 0xFF;
    }

    public byte readByte()
    {
        int waarde = -1;
        try
        {
            waarde = data_in.ReadByte();
        }
        catch (IOException ex)
        {
            waarde = -1;
        }
        return (byte)waarde;
    }

    public int readInt()
    {
        int waarde = -1;
        try
        {
            byte[] buffer = new byte[4];

            // Lê 4 bytes diretamente
            int bytesRead = data_in.Read(buffer, 0, 4);
            if (bytesRead < 4)
            {
                throw new IOException("Unable to retrieve int32 from bytes...");
            }

            // Se os dados forem big-endian, você precisa inverter a ordem dos bytes
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);  // Inverte para big-endian
            }

            // Converte para int
            waarde = swapInt(BitConverter.ToInt32(buffer, 0));
        }
        catch (IOException ex)
        {
            waarde = -1;
        }
        return waarde;
    }


    public int readShort()
    {
        int waarde = -1;
        try
        {
            byte[] buffer = new byte[2];

            // Lê 2 bytes diretamente
            int bytesRead = data_in.Read(buffer, 0, 2);
            if (bytesRead < 2)
            {
                throw new IOException("Unable to retrieve uint16 from bytes...");
            }

            // Se os dados forem big-endian, você precisa inverter a ordem dos bytes
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);  // Inverte para big-endian
            }

            // Converte para short
            waarde = swapShort(BitConverter.ToInt16(buffer, 0));
        }
        catch (IOException ex)
        {
            waarde = -1;
        }
        return waarde;
    }


    public float readFloat()
    {
        float waarde = -1;
        byte[] bytes = new byte[4];
        for (int i = 3; i >= 0; i--)
        {
            bytes[i] = readByte();
        }
        waarde = BitConverter.ToSingle(bytes);
        return waarde;
    }

    public String readString(int size)
    {
        char letter = 'n';
        String woord = "";
        //letter = readChar(data_in);
        for (int i = 0; i < size; i++)
        {
            letter = readChar();
            woord += letter;
        }
        return woord;
    }

    public String readNullTerminatedString(int size)
    {
        String woord = "";
        bool gotNull = false;
        for (int i = 0; i < size; i++)
        {
            byte b = readByte();
            if (!gotNull)
            {
                if (b != 0) woord += (char)b;
                else gotNull = true;
            }
        }
        return woord;
    }

    public String readNullTerminatedString()
    {
        String woord = "";
        byte b = readByte();
        while (b != 0)
        {
            woord += (char)b;
            b = readByte();
        }
        return woord;
    }

    public char readChar()
    {
        char letter = '\0';
        try
        {
            letter = (char)((byte)data_in.ReadByte());
        }
        catch (IOException ex)
        {
            //Logger.getLogger(loadSAFiles.class.getName()).log(Level.SEVERE, null, ex);
        }
        return letter;
    }

    public int swapInt(int v)
    {
        uint uv = (uint)v;
        return (int)((uv >> 24) | (uv << 24) | ((uv << 8) & 0x00FF0000) | ((uv >> 8) & 0x0000FF00));
    }

    public int swapShort(short i)
    {
        return ((i >> 8) & 0xff) + ((i << 8) & 0xff00);
    }

    public float swapFloat(float f)
    {
        int intValue = BitConverter.SingleToInt32Bits(f);
        intValue = swapInt(intValue);
        return BitConverter.Int32BitsToSingle(intValue);
    }

    public String readString()
    {
        char letter = 'n';
        String woord = "";
        letter = readChar();
        while (letter != '\0')
        {
            woord = woord + letter;
            letter = readChar();
        }
        return woord;
    }

    public Vector3 readVector3D()
    {
        return new Vector3(readFloat(), readFloat(), readFloat());
    }

    public Vector4 readVector4D()
    {
        return new Vector4(readFloat(), readFloat(), readFloat(), readFloat());
    }

    public int moreToRead()
    {
        try
        {
            return (int)(data_in.Length - data_in.Position);
        }
        catch (IOException ex)
        {
            Debug.LogException(ex);
            return 0;
        }
    }



    public ByteReader getByteReader()
    {
        try
        {
            Message.displayMsgLow("Data in size: " + data_in.Length);
            byte[] stream = new byte[(int)data_in.Length];
            data_in.Read(stream, 0, (int)data_in.Length);
            Message.displayMsgLow("Done");
            return new ByteReader(stream, 0);
        }
        catch (IOException ex)
        {
            return null;
        }
    }

    public ByteReader getByteReader(int size)
    {
        try
        {
            byte[] stream = new byte[size];
            data_in.Read(stream, 0, size);
            return new ByteReader(stream, 0);
        }
        catch (IOException ex)
        {
            Debug.LogError("Error in getByteReader");
            return null;
        }
    }

    public void seek(int offset)
    {
        try
        {
            data_in.Seek(offset, SeekOrigin.Begin);
        }
        catch (IOException ex)
        {
            Debug.LogException(ex);
        }
    }

    public void seek(long pOffset)
    {
        try
        {
            data_in.Seek(pOffset, SeekOrigin.Begin);
        }
        catch (IOException ex)
        {
            Debug.LogException(ex);
        }
    }

    public long readUnsignedInt()
    {
        int i = readInt();
        long l = i & 0xffffffffL;

        return l;
    }

    public byte[] readArray(int size)
    {
        byte[] array = new byte[size];
        try
        {
            int bytesRead = 0;
            while (bytesRead < size)
            {
                int read = data_in.Read(array, bytesRead, size - bytesRead);
                if (read == 0)
                {
                    throw new EndOfStreamException("Could not read the desired number of bytes.");
                }
                bytesRead += read;
            }
        }
        catch (IOException ex)
        {
            Debug.LogException(ex);
        }
        return array;
    }
}
