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
using System.IO;
using System.Security.Cryptography;

namespace RageLib.Common
{
    public abstract class KeyUtil
    {
        public static string dir { get; set;}

        public static class StringExtensions
        {
            public static bool IsNullOrWhiteSpace(string value)
            {
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        if (!char.IsWhiteSpace(value[i]))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
        }
        public abstract string ExecutableName { get; }
        protected abstract string[] PathRegistryKeys { get; }
        protected abstract uint[] SearchOffsets { get; }

        public string FindGameDirectory()
        {
            dir = Loader.Instance.gameDir;
            return dir;
        }

        public byte[] FindKey(string gamePath)
        {
            var gameExe = Path.Combine(gamePath, ExecutableName);

            const string validHash = "DEA375EF1E6EF2223A1221C2C575C47BF17EFA5E";
            byte[] key = null;

            var fs = new FileStream(gameExe, FileMode.Open, FileAccess.Read);

            bool ReadKeyFromOffset(uint offset)
            {
                if (offset <= fs.Length - 32)
                {
                    var tempKey = new byte[32];
                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Read(tempKey, 0, 32);

                    var hash = BitConverter.ToString(SHA1.Create().ComputeHash(tempKey)).Replace("-", "");
                    if (hash == validHash)
                    {
                        key = tempKey;
                        return true;
                    }
                }

                return false;
            }

            uint LookupOffset()
            {
                uint num = (uint)Math.Floor((double)(fs.Length / 32));

                for (uint i = 0; i < num; i++)
                {
                    if (ReadKeyFromOffset(i * 32))
                        return i * 32;
                }

                return (uint)0xFFFFFFFF;
            }

            foreach (var u in SearchOffsets)
            {
                if (ReadKeyFromOffset(u))
                    break;
            }

            if (key == null)
            {
                if (File.Exists($"{ExecutableName}.keyOffset"))
                {
                    bool res = uint.TryParse(File.ReadAllText($"{ExecutableName}.keyOffset"), out uint offset);

                    if (res)
                    {
                        res = ReadKeyFromOffset(offset);

                        if (!res)
                        {
                            offset = LookupOffset();
                            
                            if (offset != (uint)0xFFFFFFFF)
                                File.WriteAllText($"{ExecutableName}.keyOffset", offset.ToString());
                        }
                    }
                    else
                    {
                        offset = LookupOffset();
                            
                        if (offset != (uint)0xFFFFFFFF)
                            File.WriteAllText($"{ExecutableName}.keyOffset", offset.ToString());
                    }
                }
                else
                {
                    uint offset = LookupOffset();
                            
                    if (offset != (uint)0xFFFFFFFF)
                        File.WriteAllText($"{ExecutableName}.keyOffset", offset.ToString());
                }
            }

            fs.Close();

            return key;
        }

        public class GetDir
        {
            public static string Get()
            {
                return KeyUtil.dir;
            }
        }
    }
}
