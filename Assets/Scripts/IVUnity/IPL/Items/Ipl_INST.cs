using System.IO;
using RageLib.Common;
using UnityEngine;

public class Ipl_INST : IPL_Item
{
    // Virtual parent transform matrix for IPL World Math Representation:
    // simulates a parent GameObject with rotation(-90,0,0) and scale(-1,1,1)
    // to convert from GTA coordinate system to Unity coordinate system.
    private static readonly Matrix4x4 VirtualParentMatrix = Matrix4x4.TRS(
        Vector3.zero,
        Quaternion.Euler(-90, 0, 0),
        new Vector3(-1, 1, 1)
    );

    private static readonly Quaternion ParentRotation = Quaternion.Euler(-90, 0, 0);

    public int     id;
    public string  name = "";
    public int     hash = 0;
    public int     interior;
    public Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 scale    = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector4 rotation = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

    public int   lod;
    public int   unknown1, unknown2;
    public float unknown3;
    public float drawDistance = 300.0f;

    public  Vector4 axisAngle = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
    internal int    glListID;

    // Cached results of the unityPosition/Rotation/Scale matrix work. Computed once on first
    // access; subsequent reads are simple field returns. At ECS bake time with ~100K instances
    // × 3 reads this avoids ~300K Matrix4x4.TRS + multiply + lossyScale computations.
    private bool       _worldCached;
    private Vector3    _unityPositionCache;
    private Quaternion _unityRotationCache;
    private Vector3    _unityScaleCache;

    private void EnsureWorldCache()
    {
        if (_worldCached) return;

        Vector3 localScale = (scale.x == 0 && scale.y == 0 && scale.z == 0) ? Vector3.one : scale;
        Quaternion localRotation = new Quaternion(rotation.x, rotation.y, rotation.z, -rotation.w);

        Matrix4x4 localMatrix = Matrix4x4.TRS(position, localRotation, localScale);
        Matrix4x4 worldMatrix = VirtualParentMatrix * localMatrix;

        _unityPositionCache = worldMatrix.GetColumn(3);
        _unityScaleCache    = worldMatrix.lossyScale;

        // unityRotation uses a different quaternion composition than the world matrix above —
        // preserve the original math (y/z component negation to compensate for the -1 x mirror,
        // then ParentRotation × result).
        Quaternion mirrorAdjusted = new Quaternion(rotation.x, -rotation.y, -rotation.z, -rotation.w);
        _unityRotationCache = ParentRotation * mirrorAdjusted;

        _worldCached = true;
    }

    public Vector3   unityPosition { get { EnsureWorldCache(); return _unityPositionCache; } }
    public Vector3   unityScale    { get { EnsureWorldCache(); return _unityScaleCache;    } }
    internal Quaternion unityRotation { get { EnsureWorldCache(); return _unityRotationCache; } }

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

        // Use comprehensive hash resolver
        name = IVUnity.ComprehensiveHashResolver.ResolveHash((uint)tempHash);

        // Fallback to old method if comprehensive resolver not initialized
        if (name.StartsWith("0x"))
        {
            string hashStr = tempHash.ToString();
            string resolved = ini.GetValue<string>("Hashes", hashStr);
            if (resolved != null)
            {
                name = resolved;
            }
        }

        unknown1 = reader.ReadInt();
        lod      = reader.ReadInt();
        unknown2 = reader.ReadInt();
        unknown3 = reader.ReadFloat();
    }
}
