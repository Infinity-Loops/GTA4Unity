using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class IniJson : Dictionary<string, Dictionary<string, object>>
{
    public bool ColumnExists(string column)
    {
        return this.ContainsKey(column.ToLower());
    }

    public bool HasOption(string column, string row)
    {
        if (ColumnExists(column.ToLower()))
        {
            if (this[column.ToLower()].ContainsKey(row.ToLower()))
            {
                return true;
            }
        }

        return false;
    }

    public void AddData(string column, string row, object value)
    {
        if (!ColumnExists(column))
        {
            this.Add(column.ToLower(), new Dictionary<string, object>());
            this[column.ToLower()].Add(row.ToLower(), value);
        }

        this[column.ToLower()][row.ToLower()] = value;
    }

    public T GetValue<T>(string columm, string row)
    {

        if (HasOption(columm, row))
        {
            //Debug.Log($"Hash found {columm}|{row}");
            return (T)this[columm.ToLower()][row.ToLower()];
        }
        else
        {
            // Only log for Hashes that look important (not numeric hashes that are likely unresolved)
            if (columm != "Hashes" || (!row.All(char.IsDigit) && row.Length < 10))
            {
                Debug.Log($"Couldn't find {columm}|{row}");
            }
            return default(T);
        }
    }
}