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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RageLib.Textures.Resource;
using File = RageLib.Textures.Resource.File;

namespace RageLib.Textures
{
    public class TextureFile : IEnumerable<Texture>, IDisposable
    {
        private File _file;
        public List<Texture> Textures { get; private set; }
        private byte[] _data;

        public int Count
        {
            get { return Textures.Count; }
        }

        public void Open(string filename)
        {
            _file = new File();
            _file.Open(filename);
            BuildTextures();
        }

        public void Open(Stream stream)
        {
            _file = new File();
            _file.Open(stream);
            BuildTextures();
        }

        public void Open(byte[] data)
        {
            _file = new File();
            _data = data;
        }

        public void Read()
        {
            using (MemoryStream stream = new MemoryStream(_data))
            {
                _file.Open(stream);
                BuildTextures();
            }
            _data = null;
        }

        public void Open(Stream systemMemory, Stream graphicsMemory)
        {
            _file = new File();
            _file.Open(systemMemory, graphicsMemory);
            BuildTextures();
        }

        public void Save(Stream stream)
        {
            _file.Save(stream);
        }

        public void Save(Stream systemMemory, Stream graphicsMemory)
        {
            _file.Save(systemMemory, graphicsMemory);
        }

        internal void DumpTextureInfosToDebug()
        {
            var infos = new SortedList<uint, TextureInfo>();
            foreach (var info in _file.Textures)
            {
                infos.Add(info.RawDataOffset, info);
            }

            foreach (var infoData in infos)
            {
                var info = infoData.Value;
                Debug.WriteLine(string.Format("{0}\t{1}x{2}x{3} {4}\t{5:x}\t{6:x}\t{7:x}", info.Name, info.Width,
                  info.Height, info.Levels, info.Format, info.RawDataOffset,
                  info.GetTotalDataSize(), info.RawDataOffset + info.GetTotalDataSize()));

            }
        }

        private void BuildTextures()
        {
            Textures = new List<Texture>(_file.Header.TextureCount);

            foreach (TextureInfo info in _file.Textures)
            {
                Textures.Add(new Texture(info));
            }
        }

        public Texture FindTextureByName(string name)
        {
            // Engine-faithful: hash the name and look up in the pgDictionary's hash table,
            // exactly like FUN_0040ba60 + pgDictionary::Get in GTAIV.exe. Falls back to
            // string comparison only if hash lookup misses (shouldn't happen with correct
            // hash function, but guards against edge cases).
            if (_file != null && _file.TexturesByHash != null)
            {
                uint hash = Common.Hasher.Hash(name);
                if (_file.TexturesByHash.TryGetValue(hash, out var info))
                {
                    // Find the matching Texture wrapper in our Textures list
                    int idx = _file.Textures.IndexOf(info);
                    if (idx >= 0 && idx < Textures.Count)
                    {
                        return Textures[idx];
                    }
                }
            }

            // Fallback: linear string comparison (original behavior)
            string lower = name.ToLower();
            foreach (var texture in this)
            {
                if (texture.Name.ToLower() == lower)
                    return texture;
                if (texture.TitleName.ToLower() == lower)
                    return texture;
            }
            return null;
        }

        #region Implementation of IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Implementation of IEnumerable<Texture>

        public IEnumerator<Texture> GetEnumerator()
        {
            return Textures.GetEnumerator();
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (_file != null)
            {
                _file.Dispose();
            }

            foreach (var texture in this)
            {
                texture.Dispose();
            }

            Textures.Clear();
        }

        #endregion
    }
}