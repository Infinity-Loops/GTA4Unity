using System;
using System.IO;
using UnityEngine;
using RageLib.FileSystem.IMG;
using RageLib.FileSystem.Common;
using RageLib.Models;

namespace IVUnity
{
    /// <summary>
    /// Debug helper to understand IMG resource loading issues
    /// </summary>
    public static class IMGResourceDebugger
    {
        public static void DebugIMGResource(string imgPath, string resourceName)
        {
            try
            {
                Debug.Log($"=== Debugging IMG Resource: {resourceName} in {imgPath} ===");
                
                // Open the IMG file
                var imgFile = new RageLib.FileSystem.IMG.File();
                imgFile.Open(imgPath);
                
                // Find the resource
                TOCEntry foundEntry = null;
                int entryIndex = -1;
                
                for (int i = 0; i < imgFile.Header.EntryCount; i++)
                {
                    var name = imgFile.TOC.GetName(i);
                    if (name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundEntry = imgFile.TOC[i];
                        entryIndex = i;
                        Debug.Log($"Found {resourceName} at index {i}");
                        break;
                    }
                }
                
                if (foundEntry == null)
                {
                    Debug.LogError($"Resource {resourceName} not found in IMG");
                    imgFile.Close();
                    return;
                }
                
                // Log entry details
                Debug.Log($"Entry details:");
                Debug.Log($"  - IsResourceFile: {foundEntry.IsResourceFile}");
                Debug.Log($"  - ResourceType: {foundEntry.ResourceType}");
                Debug.Log($"  - Size: {foundEntry.Size}");
                Debug.Log($"  - OffsetBlock: {foundEntry.OffsetBlock}");
                Debug.Log($"  - UsedBlocks: {foundEntry.UsedBlocks}");
                Debug.Log($"  - Flags: 0x{foundEntry.Flags:X4}");
                Debug.Log($"  - RSCFlags: 0x{foundEntry.RSCFlags:X8}");
                
                // Check for old-style resource
                bool isOldStyle = (foundEntry.Flags & 0x4000) != 0;
                bool isRSC = (foundEntry.Flags & 0x2000) != 0;
                Debug.Log($"  - Is old-style resource: {isOldStyle}");
                Debug.Log($"  - Is RSC: {isRSC}");
                
                // Read the data
                int offset = foundEntry.OffsetBlock * 0x800;
                Debug.Log($"  - Reading from offset: 0x{offset:X8} ({offset})");
                
                byte[] data = imgFile.ReadData(offset, foundEntry.Size);
                Debug.Log($"  - Read {data.Length} bytes");
                
                // Check the first bytes
                if (data.Length >= 32)
                {
                    string hexDump = "";
                    for (int i = 0; i < 32; i++)
                    {
                        hexDump += $"{data[i]:X2} ";
                        if ((i + 1) % 16 == 0) hexDump += "\n";
                    }
                    Debug.Log($"First 32 bytes:\n{hexDump}");
                    
                    // Check for RSC magic
                    uint magic = BitConverter.ToUInt32(data, 0);
                    Debug.Log($"Magic value: 0x{magic:X8}");
                    
                    // Check if it looks like RSC05
                    if ((magic & 0xFFFFFF00) == 0x52534300)
                    {
                        byte version = (byte)(magic & 0xFF);
                        Debug.Log($"Looks like RSC with version: {version}");
                    }
                    
                    // Try to load as model
                    try
                    {
                        var model = new ModelFile();
                        model.Open(data);
                        model.Read();
                        Debug.Log("Successfully loaded as ModelFile with Read()");
                    }
                    catch (Exception e1)
                    {
                        Debug.Log($"Failed to load with Open+Read: {e1.Message}");
                        
                        // Try stream approach
                        try
                        {
                            var model = new ModelFile();
                            using (var stream = new MemoryStream(data))
                            {
                                model.Open(stream);
                            }
                            Debug.Log("Successfully loaded as ModelFile with stream");
                        }
                        catch (Exception e2)
                        {
                            Debug.Log($"Failed to load with stream: {e2.Message}");
                        }
                    }
                }
                
                imgFile.Close();
                Debug.Log("=== Debug complete ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error debugging IMG resource: {ex}");
            }
        }
        
        /// <summary>
        /// Debug a specific problematic file
        /// </summary>
        public static void DebugProblematicFile()
        {
            string imgPath = Path.Combine(Application.dataPath, "../pc/data/maps/props/roadside/lamppost.img");
            if (System.IO.File.Exists(imgPath))
            {
                DebugIMGResource(imgPath, "bm_wall_light_07.wdr");
            }
            else
            {
                Debug.LogError($"IMG file not found: {imgPath}");
            }
        }
    }
}