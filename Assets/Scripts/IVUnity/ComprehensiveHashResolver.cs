using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RageLib.Common;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity
{
    public static class ComprehensiveHashResolver
    {
        private static Dictionary<uint, string> hashToName = new Dictionary<uint, string>();
        private static Dictionary<string, uint> nameToHash = new Dictionary<string, uint>();
        private static Dictionary<string, File> textureToWTD = new Dictionary<string, File>();
        private static HashSet<uint> unresolvedHashes = new HashSet<uint>();
        private static bool isInitialized = false;
        public static void Initialize(GTADatLoader dataLoader)
        {
            if (isInitialized)
                return;
                
            Debug.Log("Initializing Comprehensive Hash Resolver...");
            hashToName.Clear();
            nameToHash.Clear();
            Hashes.table.Clear();
            ScanGameFiles(dataLoader);
            ProcessIDEFiles(dataLoader);
            ProcessIPLFiles(dataLoader);
            UpdateGlobalHashTable();
            
            Debug.Log($"Hash resolution complete - computed hashes for all {dataLoader.gameFiles.Count} game files");
            
            isInitialized = true;
            Debug.Log($"Hash Resolver initialized with {hashToName.Count} unique hash mappings");
            Debug.Log($"Files scanned: {dataLoader.gameFiles.Count}, Unique names: {nameToHash.Count}");
            int wdrCount = 0, wtdCount = 0, wddCount = 0;
            foreach (var file in dataLoader.gameFiles.Values)
            {
                if (file.Name.EndsWith(".wdr")) wdrCount++;
                else if (file.Name.EndsWith(".wtd")) wtdCount++;
                else if (file.Name.EndsWith(".wdd")) wddCount++;
            }
            Debug.Log($"Model files (.wdr): {wdrCount}, Texture files (.wtd): {wtdCount}, Dictionary files (.wdd): {wddCount}");
        }
        
        private static void ScanGameFiles(GTADatLoader dataLoader)
        {
            if (dataLoader?.gameFiles == null)
                return;
                
            Debug.Log($"Building complete hash table from {dataLoader.gameFiles.Count} game files...");
            
            int wtdScanned = 0;
            int texturesFoundInWTD = 0;
            foreach (var kvp in dataLoader.gameFiles)
            {
                string fileName = kvp.Key;
                File file = kvp.Value;
                string fullName = file.Name;
                string baseName = Path.GetFileNameWithoutExtension(fullName);
                AddHashMapping(fullName);
                AddHashMapping(baseName);
                AddHashMapping(fileName); // The key used in gameFiles
                if (fullName.EndsWith(".wtd", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var textureFile = new RageLib.Textures.TextureFile();
                        textureFile.Open(file.GetData());
                        textureFile.Read();
                        
                        wtdScanned++;
                        foreach (var texture in textureFile.Textures)
                        {
                            if (!string.IsNullOrEmpty(texture.Name))
                            {
                                AddHashMapping(texture.Name);
                                AddHashMapping($"{baseName}/{texture.Name}");
                                string texBaseName = Path.GetFileNameWithoutExtension(texture.Name);
                                if (texBaseName != texture.Name)
                                {
                                    AddHashMapping(texBaseName);
                                    AddHashMapping($"{baseName}/{texBaseName}");
                                }
                                
                                texturesFoundInWTD++;
                                if (!textureToWTD.ContainsKey(texture.Name.ToLower()))
                                {
                                    textureToWTD[texture.Name.ToLower()] = file;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                if (fullName.EndsWith(".wdr", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var modelFile = new RageLib.Models.ModelFile();
                        modelFile.Open(file.GetData());
                        
                        var embeddedTextures = modelFile.EmbeddedTextureFile;
                        if (embeddedTextures != null)
                        {
                            foreach (var texture in embeddedTextures.Textures)
                            {
                                if (!string.IsNullOrEmpty(texture.Name))
                                {
                                    AddHashMapping(texture.Name);
                                    AddHashMapping($"{baseName}/{texture.Name}");
                                    
                                    texturesFoundInWTD++;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                if (fullName.Contains("/") || fullName.Contains("\\"))
                {
                    string nameOnly = Path.GetFileName(fullName);
                    AddHashMapping(nameOnly);
                    AddHashMapping(Path.GetFileNameWithoutExtension(nameOnly));
                }
                if (fullName.EndsWith(".wdr", StringComparison.OrdinalIgnoreCase) || 
                    fullName.EndsWith(".wdd", StringComparison.OrdinalIgnoreCase) ||
                    fullName.EndsWith(".wtd", StringComparison.OrdinalIgnoreCase))
                {
                    string[] commonPrefixes = { "models/", "textures/", "gta/", "" };
                    foreach (var prefix in commonPrefixes)
                    {
                        AddHashMapping(prefix + baseName);
                        AddHashMapping(prefix + Path.GetFileName(fullName));
                    }
                    string[] lodSuffixes = { "_lod", "_lod1", "_lod2", "_lod3", "_hi", "_med", "_low" };
                    foreach (var suffix in lodSuffixes)
                    {
                        if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            string nameWithoutLod = baseName.Substring(0, baseName.Length - suffix.Length);
                            AddHashMapping(nameWithoutLod);
                        }
                    }
                    string[] texSuffixes = { "_diff", "_spec", "_norm", "_detail", "_bump", "_env" };
                    foreach (var suffix in texSuffixes)
                    {
                        if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            string nameWithoutSuffix = baseName.Substring(0, baseName.Length - suffix.Length);
                            AddHashMapping(nameWithoutSuffix);
                        }
                    }
                }
            }
            Debug.Log($"Scanned {wtdScanned} WTD files, found {texturesFoundInWTD} textures inside them");
            Debug.Log($"Texture to WTD mappings created: {textureToWTD.Count}");
        }
        
        private static void ProcessIDEFiles(GTADatLoader dataLoader)
        {
            if (dataLoader?.ideLoader?.ides == null)
                return;
                
            Debug.Log($"Processing {dataLoader.ideLoader.ides.Count} IDE files...");
            
            foreach (var ide in dataLoader.ideLoader.ides)
            {
                foreach (var obj in ide.items_objs)
                {
                    if (!string.IsNullOrEmpty(obj.modelName))
                    {
                        AddHashMapping(obj.modelName);
                        AddHashMapping(obj.modelName + ".wdr");
                        AddHashMapping(obj.modelName + ".wdd");
                    }
                    
                    if (!string.IsNullOrEmpty(obj.textureName))
                    {
                        AddHashMapping(obj.textureName);
                        AddHashMapping(obj.textureName + ".wtd");
                    }
                }
                foreach (var tobj in ide.items_tobj)
                {
                    if (!string.IsNullOrEmpty(tobj.modelName))
                    {
                        AddHashMapping(tobj.modelName);
                        AddHashMapping(tobj.modelName + ".wdr");
                    }
                    
                    if (!string.IsNullOrEmpty(tobj.textureName))
                    {
                        AddHashMapping(tobj.textureName);
                        AddHashMapping(tobj.textureName + ".wtd");
                    }
                }
                foreach (var car in ide.items_cars)
                {
                    if (!string.IsNullOrEmpty(car.modelName))
                    {
                        AddHashMapping(car.modelName);
                        AddHashMapping(car.modelName + ".wdr");
                        AddHashMapping(car.modelName + ".wft");
                    }
                    
                    if (!string.IsNullOrEmpty(car.textureName))
                    {
                        AddHashMapping(car.textureName);
                        AddHashMapping(car.textureName + ".wtd");
                    }
                }
            }
        }
        
        private static void ProcessIPLFiles(GTADatLoader dataLoader)
        {
            if (dataLoader?.iplLoader?.ipls == null)
                return;
                
            Debug.Log($"Processing {dataLoader.iplLoader.ipls.Count} IPL files...");
            
            foreach (var ipl in dataLoader.iplLoader.ipls)
            {
                foreach (var inst in ipl.ipl_inst)
                {
                    if (!string.IsNullOrEmpty(inst.name) && !inst.name.StartsWith("0x"))
                    {
                        AddHashMapping(inst.name);
                        if (inst.hash != 0)
                        {
                            uint hash = (uint)inst.hash;
                            if (!hashToName.ContainsKey(hash))
                            {
                                hashToName[hash] = inst.name;
                            }
                        }
                    }
                }
            }
        }
        
        private static void LoadSupplementaryHashes()
        {
            string hashesJsonPath = Path.Combine(Application.streamingAssetsPath, "Hashes.json");
            if (System.IO.File.Exists(hashesJsonPath))
            {
                Debug.Log("Loading supplementary hashes from Hashes.json");
                try
                {
                    string json = System.IO.File.ReadAllText(hashesJsonPath);
                    json = json.Trim();
                    if (json.StartsWith("{") && json.EndsWith("}"))
                    {
                        json = json.Substring(1, json.Length - 2);
                        string[] pairs = json.Split(',');
                        int loadedCount = 0;
                        
                        foreach (string pair in pairs)
                        {
                            string[] keyValue = pair.Split(':');
                            if (keyValue.Length == 2)
                            {
                                string key = keyValue[0].Trim().Trim('"');
                                string value = keyValue[1].Trim().Trim('"');
                                if (uint.TryParse(key, out uint hash))
                                {
                                    hashToName[hash] = value;
                                    loadedCount++;
                                }
                                else if (key.StartsWith("0x") && uint.TryParse(key.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hash))
                                {
                                    hashToName[hash] = value;
                                    loadedCount++;
                                }
                            }
                        }
                        Debug.Log($"Loaded {loadedCount} hashes from Hashes.json");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load Hashes.json: {ex.Message}");
                }
            }
            string knownFilesPath = Path.Combine(Application.streamingAssetsPath, "KnownFilenames.txt");
            if (System.IO.File.Exists(knownFilesPath))
            {
                Debug.Log("Loading supplementary hashes from KnownFilenames.txt");
                
                using (var reader = System.IO.File.OpenText(knownFilesPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            AddHashMapping(line.Trim());
                        }
                    }
                }
            }
            string hashesPath = Path.Combine(Application.streamingAssetsPath, "Hashes.txt");
            if (System.IO.File.Exists(hashesPath))
            {
                Debug.Log("Loading supplementary hashes from Hashes.txt");
                
                using (var reader = System.IO.File.OpenText(hashesPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && line.Contains(","))
                        {
                            var parts = line.Split(',');
                            if (parts.Length >= 2)
                            {
                                string name = parts[0].Trim();
                                if (uint.TryParse(parts[1].Trim(), out uint hash))
                                {
                                    if (!hashToName.ContainsKey(hash))
                                    {
                                        hashToName[hash] = name;
                                    }
                                    if (!nameToHash.ContainsKey(name))
                                    {
                                        nameToHash[name] = hash;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private static void UpdateGlobalHashTable()
        {
            if (!Hashes.table.ContainsKey("hashes"))
            {
                Hashes.table["hashes"] = new Dictionary<string, object>();
            }
            
            foreach (var kvp in hashToName)
            {
                Hashes.table.AddData("hashes", kvp.Key.ToString(), kvp.Value);
            }
        }
        
        private static void AddHashMapping(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;
            uint hash = Hasher.Hash(name);
            if (!hashToName.ContainsKey(hash) || hashToName[hash].StartsWith("0x"))
            {
                hashToName[hash] = name;
            }
            
            if (!nameToHash.ContainsKey(name))
            {
                nameToHash[name] = hash;
            }
            string lowerName = name.ToLower();
            if (lowerName != name)
            {
                uint lowerHash = Hasher.Hash(lowerName);
                if (!hashToName.ContainsKey(lowerHash))
                {
                    hashToName[lowerHash] = name;
                }
                if (!nameToHash.ContainsKey(lowerName))
                {
                    nameToHash[lowerName] = hash;
                }
            }
        }
        
        public static File GetWTDContainingTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return null;
            if (textureToWTD.TryGetValue(textureName.ToLower(), out File wtdFile))
            {
                return wtdFile;
            }
            string baseName = Path.GetFileNameWithoutExtension(textureName).ToLower();
            if (textureToWTD.TryGetValue(baseName, out wtdFile))
            {
                return wtdFile;
            }
            string[] suffixes = { "_diff", "_spec", "_norm", "_detail" };
            foreach (var suffix in suffixes)
            {
                if (baseName.EndsWith(suffix))
                {
                    string nameWithoutSuffix = baseName.Substring(0, baseName.Length - suffix.Length);
                    if (textureToWTD.TryGetValue(nameWithoutSuffix, out wtdFile))
                    {
                        return wtdFile;
                    }
                }
            }
            
            return null;
        }
        public static string ResolveHash(uint hash)
        {
            if (hashToName.TryGetValue(hash, out string name))
            {
                return name;
            }
            if (!unresolvedHashes.Contains(hash))
            {
                unresolvedHashes.Add(hash);
                if (unresolvedHashes.Count <= 10)
                {
                    Debug.LogWarning($"Hash not found in any game file: {hash} (0x{hash:x8}) - file probably doesn't exist");
                }
                else if (unresolvedHashes.Count == 11)
                {
                    Debug.LogWarning($"Suppressing further unresolved hash warnings. Total: {unresolvedHashes.Count}");
                }
            }
            return $"0x{hash:x8}";
        }
        public static string ResolveHashString(string hashStr)
        {
            if (string.IsNullOrEmpty(hashStr))
                return hashStr;
            if (!hashStr.StartsWith("0x") && !uint.TryParse(hashStr, out _))
                return hashStr;
                
            uint hash;
            if (hashStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(hashStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hash))
                {
                    return ResolveHash(hash);
                }
            }
            else if (uint.TryParse(hashStr, out hash))
            {
                return ResolveHash(hash);
            }
            
            return hashStr;
        }
        public static uint GetHash(string name)
        {
            if (nameToHash.TryGetValue(name, out uint hash))
            {
                return hash;
            }
            hash = Hasher.Hash(name);
            AddHashMapping(name);
            return hash;
        }
        public static bool IsHashKnown(uint hash)
        {
            return hashToName.ContainsKey(hash);
        }
        public static int GetKnownHashCount()
        {
            return hashToName.Count;
        }
    }
}