using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Generic delimited-text reader for GTA-format lines (IDE, water, etc.). Built once per
/// line, then individual fields are pulled in declaration order via Read* methods.
///
/// All numeric parsing uses <see cref="CultureInfo.InvariantCulture"/> with explicit
/// <see cref="NumberStyles"/>, so locales with `,` decimal separators don't break `1.5`
/// (and so we don't conflict with the comma field separator in IDE files).
///
/// Construction:
///   - <c>new LineParser(line)</c>                 — comma-separated (IDE default)
///   - <c>new LineParser(line, ' ', '\t')</c>      — whitespace-separated, multiple
///                                                    consecutive separators collapse
///                                                    via RemoveEmptyEntries
///
/// Parse failures throw <see cref="FormatException"/> with the field index and the
/// offending value, e.g. <c>"Field 4 ('1,5') is not a valid float"</c>.
/// </summary>
public struct LineParser
{
    private readonly string[] tokens;
    private int index;

    /// <summary>Comma-separated (default for IDE files).</summary>
    public LineParser(string line) : this(line, ',') { }

    /// <summary>
    /// Split <paramref name="line"/> by any of <paramref name="separators"/>, dropping
    /// empty tokens (so multiple consecutive spaces or tabs are treated as one).
    /// Each token is then trimmed of leading/trailing whitespace.
    /// </summary>
    public LineParser(string line, params char[] separators)
    {
        var raw = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        tokens = new string[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            tokens[i] = raw[i].Trim();
        }
        index = 0;
    }

    public int  Remaining => tokens.Length - index;
    public bool HasMore   => index < tokens.Length;

    public string ReadString()
    {
        EnsureAvailable("string");
        return tokens[index++];
    }

    public int ReadInt()
    {
        EnsureAvailable("int");
        var raw = tokens[index];
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Field {index} ('{raw}') is not a valid int");
        index++;
        return value;
    }

    public float ReadFloat()
    {
        EnsureAvailable("float");
        var raw = tokens[index];
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Field {index} ('{raw}') is not a valid float");
        index++;
        return value;
    }

    public Vector3 ReadVector3() => new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
    public Vector4 ReadVector4() => new Vector4(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    private void EnsureAvailable(string expected)
    {
        if (index >= tokens.Length)
        {
            throw new FormatException(
                $"Unexpected end of line: expected {expected} at field {index}, line had {tokens.Length} tokens");
        }
    }
}
