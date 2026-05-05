using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity.ECS.Ped
{
    public class PedTwistSolver
    {
        struct TwistBone
        {
            public int BoneIndex;
            public int DriverIndex;
            public float Fraction;
            public Vector3 TwistAxis;
        }

        private readonly List<TwistBone> _twistBones = new List<TwistBone>();
        private quaternion[] _rageRestRot;

        private static readonly (ushort twistId, ushort driverId, Vector3 axis)[] TwistPairs = new[]
        {
            ((ushort)14497, (ushort)1218, new Vector3(0, 0, 1)),  // L_ForeTwist ← L_Forearm (Z axis)
            ((ushort)14496, (ushort)1217, new Vector3(1, 0, 0)),  // L_UpperArmRoll ← L_UpperArm (X axis)
            ((ushort)14753, (ushort)1225, new Vector3(0, 0, 1)),  // R_ForeTwist ← R_Forearm (Z axis)
            ((ushort)14752, (ushort)1224, new Vector3(1, 0, 0)),  // R_UpperArmRoll ← R_UpperArm (X axis)
        };

        public void Configure(Dictionary<ushort, int> boneIdToIndex, quaternion[] rageRestRot)
        {
            _rageRestRot = rageRestRot;
            _twistBones.Clear();

            foreach (var (twistId, driverId, axis) in TwistPairs)
            {
                if (boneIdToIndex.TryGetValue(twistId, out int twistIdx) &&
                    boneIdToIndex.TryGetValue(driverId, out int driverIdx))
                {
                    _twistBones.Add(new TwistBone
                    {
                        BoneIndex = twistIdx,
                        DriverIndex = driverIdx,
                        Fraction = 1f,
                        TwistAxis = axis,
                    });
                }
            }
        }

        private bool _logged;

        public void Solve(Quaternion[] rageLocalRot, Vector3[] rageWorldPos, Quaternion[] rageWorldRot, int[] parentIndices)
        {
            for (int i = 0; i < _twistBones.Count; i++)
            {
                var tb = _twistBones[i];
                var driverRest = new Quaternion(
                    _rageRestRot[tb.DriverIndex].value.x,
                    _rageRestRot[tb.DriverIndex].value.y,
                    _rageRestRot[tb.DriverIndex].value.z,
                    _rageRestRot[tb.DriverIndex].value.w);

                var driverCurrent = rageLocalRot[tb.DriverIndex];

                // Delta from rest: how much the driver bone rotated
                var delta = driverCurrent * Quaternion.Inverse(driverRest);

                // Extract twist component along the bone's length axis
                var projected = ProjectOntoAxis(delta, tb.TwistAxis);

                // Apply fraction of twist
                var twistRot = Quaternion.Slerp(Quaternion.identity, projected, tb.Fraction);

                if (!_logged)
                {
                    Debug.Log($"[TwistSolver] bone[{tb.BoneIndex}] driver[{tb.DriverIndex}] " +
                        $"delta=({delta.x:F3},{delta.y:F3},{delta.z:F3},{delta.w:F3}) " +
                        $"twist=({projected.x:F3},{projected.y:F3},{projected.z:F3},{projected.w:F3}) " +
                        $"applied=({twistRot.x:F3},{twistRot.y:F3},{twistRot.z:F3},{twistRot.w:F3})");
                }

                // Twist bone local rotation = rest * twist fraction
                var restRot = new Quaternion(
                    _rageRestRot[tb.BoneIndex].value.x,
                    _rageRestRot[tb.BoneIndex].value.y,
                    _rageRestRot[tb.BoneIndex].value.z,
                    _rageRestRot[tb.BoneIndex].value.w);
                rageLocalRot[tb.BoneIndex] = restRot * twistRot;

                // Recompose world rotation (position unchanged by twist)
                int pi = parentIndices[tb.BoneIndex];
                if (pi >= 0)
                {
                    rageWorldRot[tb.BoneIndex] = rageWorldRot[pi] * rageLocalRot[tb.BoneIndex];
                }
            }
            _logged = true;
        }

        static Quaternion ProjectOntoAxis(Quaternion q, Vector3 axis)
        {
            // Decompose quaternion into twist (around axis) and swing components
            var ra = new Vector3(q.x, q.y, q.z);
            float dot = Vector3.Dot(ra, axis);
            var projected = axis * dot;
            var twist = new Quaternion(projected.x, projected.y, projected.z, q.w);
            float len = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (len < 0.0001f)
                return Quaternion.identity;
            twist.x /= len;
            twist.y /= len;
            twist.z /= len;
            twist.w /= len;
            if (twist.w < 0)
            {
                twist.x = -twist.x;
                twist.y = -twist.y;
                twist.z = -twist.z;
                twist.w = -twist.w;
            }
            return twist;
        }
    }
}
