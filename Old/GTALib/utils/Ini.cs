using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace IniReader {
    static class Ini {
        static void AddIniEntryValue(IDictionary<string, object> d, string line, string name, int i, object o) {
            if (o is string) {
                var s = (string)o;
                d.Remove(name);
                var col = new List<string>();
                d.Add(name, col);
                col.Add(s);
                col.Add(line.Substring(i + 1).TrimStart());
            } else {
                ((List<string>)o).Add(line.Substring(i + 1).TrimStart());
            }
        }
        /// <summary>
        /// Reads an INI file as a nested dictionary. The outer dictionary contains a dictionary for each section. The inner dictionary contains a name, and a string or a list of strings when an entry has multiple items.
        /// </summary>
        /// <param name="reader">The text reader</param>
        /// <param name="comparer">The comparer to use for keys. Defaults to culture invariant and case insensitive.</param>
        /// <returns>A nested dictionary</returns>
        public static IDictionary<string, IDictionary<string, object>> Read(TextReader reader, IEqualityComparer<string> comparer = null) {
            if (comparer == null) {
                comparer = StringComparer.InvariantCultureIgnoreCase;
            }
            int lc = 1;
            var result = new Dictionary<string, IDictionary<string, object>>(comparer);
            string section = "";
            string name = null;
            string line;
            while (null != (line = reader.ReadLine())) {
                var i = line.IndexOf(';');
                if (i > -1) {
                    line = line.Substring(0, i);
                }
                line = line.Trim();
                if (!string.IsNullOrEmpty(line)) {
                    IDictionary<string, object> d;
                    if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']') {
                        section = line.Substring(1, line.Length - 2);
                        if (!result.TryGetValue(section, out d)) {
                            d = new Dictionary<string, object>(comparer);
                            result.Add(section, d);
                        }
                    } else {
                        d = result[section];
                        i = line.IndexOf('=');
                        if (i > -1) {
                            name = line.Substring(0, i).TrimEnd();
                            object o;
                            if (d.TryGetValue(name, out o)) {
                                AddIniEntryValue(d, line, name, i, o);
                            } else
                                d.Add(name, line.Substring(i + 1).TrimStart());
                        } else if (null == name) {
                            throw new IOException("Invalid INI file at line " + lc.ToString());
                        } else {
                            var o = d[name];
                            AddIniEntryValue(d, line, name, i, o);
                        }
                    }
                }
                ++lc;
            }

            reader.Close();
            reader.Dispose();

            return result;
        }
        public static string ToString(IDictionary<string,IDictionary<string,object>> ini) {
            var sb = new StringBuilder();
            foreach (var sentry in ini) {
                sb.AppendLine("[" + sentry.Key + "]");
                var d = sentry.Value;
                foreach (var entry in d) {
                    if (entry.Value is IList<string>) {
                        var l = ((IList<string>)entry.Value);
                        sb.AppendLine(string.Format("{0} = {1}", entry.Key, l[0]));
                        for (var i = 1; i < l.Count; ++i) {
                            sb.AppendLine("\t" + l[i]);
                        }
                        sb.AppendLine();
                    } else
                        sb.AppendLine(string.Format("{0} = {1}", entry.Key, entry.Value));
                }
            }
            return sb.ToString();
        }
    }
}
