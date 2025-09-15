/**********************************************************************\

 RageLib
 Copyright (C) 2008  Arushan/Aru <oneforaru at gmail.com>

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.

\**********************************************************************/

using System;
using System.Collections.Generic;
using RageLib.FileSystem.Common;
using RageLib.FileSystem.IMG;
using File = RageLib.FileSystem.IMG.File;

namespace RageLib.FileSystem
{
    public class IMGFileSystem : Common.FileSystem
    {
        private File _imgFile;
        
        // LRU cache for recently accessed data blocks
        private class DataCache
        {
            private readonly LinkedList<CacheEntry> _lruList = new LinkedList<CacheEntry>();
            private readonly Dictionary<TOCEntry, LinkedListNode<CacheEntry>> _cacheMap = new Dictionary<TOCEntry, LinkedListNode<CacheEntry>>();
            private const int MaxCacheSize = 32 * 1024 * 1024; // 32MB cache
            private long _currentSize = 0;
            
            private class CacheEntry
            {
                public TOCEntry Entry { get; set; }
                public byte[] Data { get; set; }
                public long Size { get; set; }
            }
            
            public byte[] Get(TOCEntry entry)
            {
                if (_cacheMap.TryGetValue(entry, out var node))
                {
                    // Move to front (most recently used)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Data;
                }
                return null;
            }
            
            public void Add(TOCEntry entry, byte[] data)
            {
                // Remove if already exists
                if (_cacheMap.ContainsKey(entry))
                    Remove(entry);
                    
                // Evict oldest entries if cache is full
                while (_currentSize + data.Length > MaxCacheSize && _lruList.Count > 0)
                {
                    var oldest = _lruList.Last;
                    _cacheMap.Remove(oldest.Value.Entry);
                    _currentSize -= oldest.Value.Size;
                    _lruList.RemoveLast();
                }
                
                // Add new entry
                var cacheEntry = new CacheEntry { Entry = entry, Data = data, Size = data.Length };
                var newNode = _lruList.AddFirst(cacheEntry);
                _cacheMap[entry] = newNode;
                _currentSize += data.Length;
            }
            
            public void Remove(TOCEntry entry)
            {
                if (_cacheMap.TryGetValue(entry, out var node))
                {
                    _currentSize -= node.Value.Size;
                    _lruList.Remove(node);
                    _cacheMap.Remove(entry);
                }
            }
            
            public void Clear()
            {
                _lruList.Clear();
                _cacheMap.Clear();
                _currentSize = 0;
            }
        }
        
        private readonly DataCache _dataCache = new DataCache();

        public override void Open(string filename)
        {
            _imgFile = new File();
            if (!_imgFile.Open(filename))
            {
                throw new Exception($"Could not open IMG file: {filename}");
            }

            BuildFS();
        }

        public override void Save()
        {
            _imgFile.Save();
        }

        public override void Rebuild()
        {
            _imgFile.Rebuild();
        }

        public override void Close()
        {
            // Clear cache before closing to free memory
            _dataCache.Clear();
            _imgFile.Close();
        }

        public override bool SupportsRebuild
        {
            get { return true; }
        }

        public override bool HasDirectoryStructure
        {
            get { return false; }
        }

        private byte[] LoadData(TOCEntry entry)
        {
            if (entry.CustomData != null)
            {
                return entry.CustomData;
            }
            
            // Check cache first
            byte[] cachedData = _dataCache.Get(entry);
            if (cachedData != null)
            {
                return cachedData;
            }
            
            // Load from disk
            byte[] data = _imgFile.ReadData(entry.OffsetBlock * 0x800, entry.Size);
            
            // Add to cache for future access
            _dataCache.Add(entry, data);
            
            return data;
        }

        private void StoreData(TOCEntry entry, byte[] data)
        {
            entry.SetCustomData(data);
        }

        private void BuildFS()
        {
            RootDirectory = new Directory();
            RootDirectory.Name = "/";

            int entryCount = _imgFile.Header.EntryCount;
            for (int i = 0; i < entryCount; i++)
            {
                TOCEntry entry = _imgFile.TOC[i];
                Common.File.DataLoadDelegate load = () => LoadData(entry);
                Common.File.DataStoreDelegate store = data => StoreData(entry, data);
                Common.File.DataIsCustomDelegate isCustom = () => entry.CustomData != null;

                var file = new Common.File(load, store, isCustom)
                {
                    CompressedSize = entry.Size,
                    IsCompressed = false,
                    Name = _imgFile.TOC.GetName(i),
                    Size = entry.Size,
                    IsResource = entry.IsResourceFile,
                    ResourceType = entry.ResourceType,
                    ParentDirectory = RootDirectory
                };

                RootDirectory.AddObject(file);
            }
        }
    }
}