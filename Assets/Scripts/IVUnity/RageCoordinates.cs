using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Coordinate conversion between RAGE (RH, Z-up) and Unity (LH, Y-up).
///
/// RAGE stores quaternions with negated XYZ relative to the mathematical rotation
/// (OpenRage entity.cpp:200: QuatV(-x, -y, -z, w)).
///
/// Position: RAGE(x,y,z) → Unity(-x, z, -y)
/// Rotation: map axis through P then negate for RH→LH handedness flip
/// </summary>
public static class RageCoordinates
{
    /// <summary>Convert a RAGE world/local position to Unity.</summary>
    public static Vector3 Position(Vector3 rage)
    {
        return new Vector3(-rage.x, rage.z, -rage.y);
    }

    /// <summary>Convert a RAGE stored quaternion (x,y,z,w) to a Unity Quaternion.</summary>
    public static Quaternion Rotation(Vector4 stored)
    {
        return new Quaternion(-stored.x, stored.z, -stored.y, stored.w);
    }

    /// <summary>Convert a RAGE stored quaternion (x,y,z,w) to a Unity Quaternion.</summary>
    public static Quaternion Rotation(float sx, float sy, float sz, float sw)
    {
        return new Quaternion(-sx, sz, -sy, sw);
    }

    /// <summary>Convert a RAGE internal/mathematical quaternion to a Unity Quaternion.</summary>
    public static Quaternion RotationInternal(Quaternion rageInternal)
    {
        return new Quaternion(rageInternal.x, -rageInternal.z, rageInternal.y, rageInternal.w);
    }
    
    /// <summary>Convert a RAGE position (3 floats) to a Unity float3.</summary>
    public static float3 Position(float x, float y, float z)
    {
        return new float3(-x, z, -y);
    }

    /// <summary>Convert a Unity position back to RAGE.</summary>
    public static Vector3 InversePosition(Vector3 unity)
    {
        return new Vector3(-unity.x, -unity.z, unity.y);
    }

    /// <summary>Convert a Unity Quaternion back to a RAGE internal quaternion.</summary>
    public static Quaternion InverseRotationInternal(Quaternion unity)
    {
        return new Quaternion(unity.x, unity.z, -unity.y, unity.w);
    }

    /// <summary>
    /// Compose a parent + child transform in RAGE space and return Unity world values.
    /// Both rotations are in RAGE stored format.
    /// </summary>
    public static void Compose(
        Vector3 parentPos, Vector4 parentRot,
        Vector3 childPos, Vector4 childRot,
        out Vector3 unityPos, out Quaternion unityRot)
    {
        Quaternion pI = new Quaternion(parentRot.x, parentRot.y, parentRot.z, parentRot.w);
        Quaternion cI = new Quaternion(childRot.x, childRot.y, childRot.z, childRot.w);

        Vector3 worldPos = parentPos + pI * childPos;
        Quaternion composed = pI * cI;

        unityPos = Position(worldPos);
        unityRot = RotationInternal(composed);
    }
}