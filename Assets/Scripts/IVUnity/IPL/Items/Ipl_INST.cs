using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_INST : IPL_Item
{
    public int     id;
    public string  name = "";
    public int     hash = 0;
    public int     interior;
    public Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 scale    = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector4 rotation = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

    public int   lod;
    public int   flags, unknown2;
    public float unknown3;
    public float drawDistance = 300.0f;

    public  Vector4 axisAngle = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

    private bool       _worldCached;
    private Vector3    _unityPositionCache;
    private Quaternion _unityRotationCache;

    private void EnsureWorldCache()
    {
        if (_worldCached) return;

        _unityPositionCache = RageCoordinates.Position(position);
        _unityRotationCache = RageCoordinates.Rotation(rotation);

        _worldCached = true;
    }

    public Vector3     unityPosition { get { EnsureWorldCache(); return _unityPositionCache; } }
    public Quaternion  unityRotation { get { EnsureWorldCache(); return _unityRotationCache; } }

    public override void Read(string line)
    {
        line = line.Replace(" ", "");
        string[] split = line.Split(",");
        axisAngle = rotation.GetAxisAngle();
    }

    public override void Read(BinaryReader reader) { }

    public override void Read(BinaryReader reader, GTAHashTable ini)
    {
        position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        rotation = new Vector4(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

        long tempHash = reader.ReadUInt();
        hash = (int)tempHash;

        string resolved = ini.GetValue<string>("Hashes", tempHash.ToString());
        name = resolved ?? $"0x{tempHash:x8}";

        flags = reader.ReadInt();
        lod      = reader.ReadInt();
        unknown2 = reader.ReadInt();
        unknown3 = reader.ReadFloat();
    }
}
