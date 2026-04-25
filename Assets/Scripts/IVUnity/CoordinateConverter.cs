using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;

namespace IVUnity
{
    /// <summary>
    /// Handles conversion between GTA IV (DirectX 9 left-handed) and Unity (right-handed) coordinate systems
    /// </summary>
    public static class CoordinateConverter
    {
        // DirectX uses left-handed coordinate system: +X right, +Y up, +Z forward
        // Unity uses right-handed coordinate system: +X right, +Y up, +Z forward (but flipped)
        
        /// <summary>
        /// Converts a position from DirectX to Unity coordinate system
        /// </summary>
        public static Vector3 ConvertPosition(Vector3 dxPosition)
        {
            // Flip Z axis to convert from left-handed to right-handed
            return new Vector3(dxPosition.x, dxPosition.z, -dxPosition.y);
        }
        
        /// <summary>
        /// Converts a rotation from DirectX to Unity coordinate system
        /// </summary>
        public static Quaternion ConvertRotation(Quaternion dxRotation)
        {
            // Convert quaternion from left-handed to right-handed system
            // Flip W and Y components
            return new Quaternion(-dxRotation.x, dxRotation.z, -dxRotation.y, dxRotation.w);
        }
        
        /// <summary>
        /// Converts Euler angles from DirectX to Unity coordinate system
        /// </summary>
        public static Vector3 ConvertEulerAngles(Vector3 dxEuler)
        {
            // Swap Y and Z, negate Z
            return new Vector3(dxEuler.x, dxEuler.z, -dxEuler.y);
        }
        
        /// <summary>
        /// Converts a normal vector from DirectX to Unity coordinate system
        /// </summary>
        public static Vector3 ConvertNormal(Vector3 dxNormal)
        {
            // Same as position conversion for normals
            return new Vector3(dxNormal.x, dxNormal.z, -dxNormal.y);
        }
        
        /// <summary>
        /// Converts texture coordinates (usually no change needed)
        /// </summary>
        public static Vector2 ConvertUV(Vector2 dxUV)
        {
            // GTA IV uses standard UV mapping, no conversion needed
            return dxUV;
        }
        
        /// <summary>
        /// Reverses the winding order of triangles (for mirroring fix)
        /// </summary>
        public static void ReverseWindingOrder(int[] triangles)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Swap second and third vertex to reverse winding
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }
        }
        
        /// <summary>
        /// Batch converts an array of positions using Burst
        /// </summary>
        [BurstCompile]
        public static void ConvertPositionsBatch(ref Vector3[] positions)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];
                positions[i] = new Vector3(pos.x, pos.z, -pos.y);
            }
        }
        
        /// <summary>
        /// Batch converts an array of normals using Burst
        /// </summary>
        [BurstCompile]
        public static void ConvertNormalsBatch(ref Vector3[] normals)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                var normal = normals[i];
                normals[i] = new Vector3(normal.x, normal.z, -normal.y);
            }
        }
    }
}