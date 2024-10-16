using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using System.Text;
using System.Drawing;
using System.IO;

public class IMG_IV_Header
{
    public const uint MagicId = 0xA94E2A52;
    public const int SupportedVersion = 3;

    public IMG_IV_Header(IMG_IV file)
    {
        File = file;
    }

    public uint Identifier { get; set; }
    public int Version { get; set; }
    public int EntryCount { get; set; }
    public int TocSize { get; set; }
    public short TocEntrySize { get; set; }
    private short Unknown2 { get; set; }

    public IMG_IV File { get; private set; }

    public void Read(BinaryReader br)
    {
        Identifier = br.ReadUInt32();
        Version = br.ReadInt32();
        EntryCount = br.ReadInt32();
        TocSize = br.ReadInt32();
        TocEntrySize = br.ReadInt16();
        Unknown2 = br.ReadInt16();
    }
}

internal class IMG_IV_TOCEntry
{
    public const int BlockSize = 0x800;

    public IMG_IV_TOCEntry(TOC toc)
    {
        TOC = toc;
    }

    public int Size { get; set; } // For normal entries, this is the real size, for RSC, this is computed.
    public uint RSCFlags { get; set; } // For RSC entries
    public ResourceType ResourceType { get; set; }
    public int OffsetBlock { get; set; }
    public short UsedBlocks { get; set; }
    public short Flags { get; set; }

    public int PaddingCount
    {
        get
        {
            return Flags & 0x7FF;
        }
        set
        {
            Flags = (short)((Flags & ~0x7FF) | value);
        }
    }

    public bool IsResourceFile { get; set; }

    public TOC TOC { get; set; }

    public byte[] CustomData { get; private set; }

    public void SetCustomData(byte[] data)
    {
        if (data == null)
        {
            CustomData = null;
        }
        else
        {
            Size = data.Length;

            if ((data.Length % BlockSize) != 0)
            {
                int padding = (BlockSize - data.Length % BlockSize);
                int fullDataLength = data.Length + padding;
                var newData = new byte[fullDataLength];
                data.CopyTo(newData, 0);
                data = newData;

                PaddingCount = padding;
            }
            else
            {
                PaddingCount = 0;
            }

            CustomData = data;

            if (IsResourceFile)
            {
                var ms = new MemoryStream(data, false);

                uint flags;
                ResourceType resType;

                ResourceUtil.GetResourceData(ms, out flags, out resType);

                RSCFlags = flags;
                ResourceType = resType;

                ms.Close();
            }
        }
    }

    public static void GetResourceData(Stream stream, out uint flags, out ResourceType type)
    {
        var rh = new ResourceHeader();
        rh.Read(new BinaryReader(stream));
        flags = rh.Flags;
        type = rh.Type;
    }

    #region IFileAccess Members

    public void Read(BinaryReader br)
    {
        uint temp = br.ReadUInt32();
        IsResourceFile = ((temp & 0xc0000000) != 0);

        if (!IsResourceFile)
        {
            Size = (int)temp;
        }
        else
        {
            RSCFlags = temp;
        }

        ResourceType = (ResourceType)br.ReadInt32();
        OffsetBlock = br.ReadInt32();
        UsedBlocks = br.ReadInt16();
        Flags = br.ReadInt16();

        if (IsResourceFile)
        {
            Size = UsedBlocks * 0x800 - PaddingCount;
        }

        // Uses 0x4000 on Flags to determine if its old style resources
        // if its not 0, its old style!

        // Uses 0x2000 on Flags to determine if its a RSC,
        // if its 1, its a RSC!
    }
}

    public class IMG_IV
{
    private byte[] ident = new byte[4];

    public void loadImg(IMG image)
    {
        ReadFunctions rf = new ReadFunctions();
        rf.openFile(image.getFileName());

        ident = new byte[] { rf.readByte(), rf.readByte(), rf.readByte(), rf.readByte() };

        if (ident[0] == 82 && ident[1] == 42 && ident[2] == 78 && ident[3] == -87)
        {
            readUnEncryptedImg(rf, image);
        }
        else
        {
            image.encrypted = true;
            readEncryptedImg(rf, image);
        }

        rf.closeFile();
    }

    private void increaseTypeCounter(string itemName, IMG img)
    {
        itemName = itemName.ToLower();
        string ext = System.IO.Path.GetExtension(itemName);
        switch (ext)
        {
            case ".cut": img.cutCount++; break;
            case ".wad": img.wadCount++; break;
            case ".wbd": img.wbdCount++; break;
            case ".wbn": img.wbnCount++; break;
            case ".wdr": img.wdrCount++; break;
            case ".wdd": img.wddCount++; break;
            case ".wft": img.wftCount++; break;
            case ".wpl": img.wplCount++; break;
            case ".wtd": img.wtdCount++; break;
        }
    }

    public void readUnEncryptedImg(ReadFunctions rf, IMG image)
    {
        List<IMG_Item> items = new List<IMG_Item>();

        int itemCount = rf.readInt();
        image.itemCount = itemCount;

        Debug.Log($"Item Count: {itemCount}");
        Debug.Log($"Table Size: {rf.readInt()}");
        Debug.Log($"Size of table items: {Utils.getHexString(rf.readShort())}");
        Debug.Log($"Unknown: {rf.readShort()}");

        for (int i = 0; i < itemCount; i++)
        {
            IMG_Item item = new IMG_Item
            {
                offset = rf.readInt() * 0x800,
                size = rf.readShort() * 0x800 - (rf.readShort() & 0x7FF),
                type = rf.readInt()
            };

            Debug.Log("-------------------------------");
            Debug.Log($"Offset: {Utils.getHexString(item.offset)}");
            Debug.Log($"Size: {item.size} bytes");
            Debug.Log($"Type: {Utils.getHexString(item.type)}");
            Debug.Log($"Used Blocks: {rf.readShort()}");
            Debug.Log($"Padding: {rf.readShort()}");

            items.Add(item);
        }

        for (int i = 0; i < itemCount; i++)
        {
            string name = rf.readNullTerminatedString();
            items[i].setName(name);
            increaseTypeCounter(name, image);
        }

        image.setItems(items.Count > 0 ? items : null);
    }

    public void readEncryptedImg(ReadFunctions rf, IMG img)
    {
        List<IMG_Item> items = new List<IMG_Item>();

        byte[] key = img.key;
        byte[] data = withIdent(rf, key);

        ByteReader br = new ByteReader(data, 0);

        Debug.Log($"ID: {br.readUInt32()}");
        Debug.Log($"Version: {br.readUInt32()}");
        int itemCount = br.readUInt32();
        int tableSize = br.readUInt32();

        Debug.Log($"Number of items: {itemCount}");
        Debug.Log($"Size of table: {tableSize}");

        int itemSize = rf.readShort();
        int namesSize = tableSize - (itemCount * itemSize);

        Debug.Log($"Item size: {itemSize}");
        Debug.Log($"Names: {namesSize}");

        for (int i = 0; i < itemCount; i++)
        {
            data = decrypt16byteBlock(rf, key);
            br = new ByteReader(data, 0);
            IMG_Item item = new IMG_Item();
            itemSize = br.readUInt32(); // or flags
            int itemType = br.readUInt32();
            int itemOffset = br.readUInt32() * 2048;
            int usedBlocks = br.readUInt16();
            int Padding = (br.readUInt16() & 0x7FF);
            if (itemType <= 0x6E)
            {
                item.setFlags(itemSize);
                itemSize = Utils.getTotalMemSize(itemSize);
            }
            item.setOffset(itemOffset);
            item.setSize(usedBlocks * 0x800 - Padding);
            item.setType(itemType);
            items.Add(item);
        }

        byte[] names = decryptNames(rf, key, namesSize);
        br = new ByteReader(names, 0);
        for (int i = 0; i < itemCount; i++)
        {
            string name = br.readNullTerminatedString();
            items[i].setName(name);
            increaseTypeCounter(name, img);
            Debug.Log($"Name {i}: {name}");
        }

        rf.closeFile();
        img.setItems(items.Count > 0 ? items : null);
    }

    private byte[] decryptNames(ReadFunctions rf, byte[] key, int namesSize)
    {
        byte[] names = new byte[namesSize];
        int i = 0;

        while (i < namesSize)
        {
            byte[] data = decrypt16byteBlock(rf, key);
            int toCopy = Math.Min(16, namesSize - i);
            Array.Copy(data, 0, names, i, toCopy);
            i += 16;
        }

        return names;
    }

    public byte[] withIdent(ReadFunctions rf, byte[] key)
    {
        byte[] data = new byte[16];
        Array.Copy(ident, 0, data, 0, 4);
        for (int i = 4; i < 16; i++)
        {
            data[i] = rf.readByte();
        }
        return decryptAES(key, data);
    }

    public byte[] decrypt16byteBlock(ReadFunctions rf, byte[] key)
    {
        byte[] data = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            data[i] = rf.readByte();
        }
        return decryptAES(key, data);
    }

    public uint SwapEndian(uint v)
    {
        return ((v >> 24) & 0xFF) |
               ((v >> 8) & 0xFF00) |
               ((v & 0xFF00) << 8) |
               ((v & 0xFF) << 24);
    }

    public static byte[] decryptAES(byte[] key, byte[] dataIn)
    {
        byte[] data = new byte[dataIn.Length];
        dataIn.CopyTo(data, 0);

        // Create our Rijndael class
        Rijndael rj = Rijndael.Create();
        rj.BlockSize = 128;
        rj.KeySize = 256;
        rj.Mode = CipherMode.ECB;
        rj.Key = key;
        rj.IV = new byte[16];
        rj.Padding = PaddingMode.None;

        ICryptoTransform transform = rj.CreateDecryptor();

        int dataLen = data.Length & ~0x0F;

        // Decrypt!

        // R* was nice enough to do it 16 times...
        // AES is just as effective doing it 1 time because it has multiple internal rounds

        if (dataLen > 0)
        {
            for (int i = 0; i < 16; i++)
            {
                transform.TransformBlock(data, 0, dataLen, data, 0);
            }
        }

        return data;
    }
}
