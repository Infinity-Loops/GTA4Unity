using Unity.Mathematics;

namespace IVUnity.ECS.Ped.Legs
{
    /// <summary>
    /// Two-bone IK solver (thigh → knee → ankle).
    /// </summary>
    public static class TwoBoneIK
    {
        public static void Solve(
            float3 rootPos, quaternion rootRot,
            float3 midPos, quaternion midRot,
            float3 endPos, quaternion endRot,
            float3 targetPos, float3 hintDir,
            float weight,
            out float3 outRootPos, out quaternion outRootRot,
            out float3 outMidPos, out quaternion outMidRot,
            out float3 outEndPos, out quaternion outEndRot)
        {
            outRootPos = rootPos;
            outRootRot = rootRot;
            outMidPos = midPos;
            outMidRot = midRot;
            outEndPos = endPos;
            outEndRot = endRot;

            if (weight < 0.001f) return;

            float upperLen = math.length(midPos - rootPos);
            float lowerLen = math.length(endPos - midPos);
            float totalLen = upperLen + lowerLen;

            float3 toTarget = targetPos - rootPos;
            float targetDist = math.length(toTarget);

            // Clamp to prevent hyperextension
            if (targetDist > totalLen * 0.9999f)
                targetDist = totalLen * 0.9999f;

            if (targetDist < 0.001f)
            {
                outEndPos = math.lerp(endPos, targetPos, weight);
                return;
            }

            // Law of cosines for knee angle
            float cosAngle = (upperLen * upperLen + lowerLen * lowerLen - targetDist * targetDist)
                             / (2f * upperLen * lowerLen);
            cosAngle = math.clamp(cosAngle, -1f, 1f);

            // Direction from root to target
            float3 targetDir = toTarget / targetDist;

            // Compute plane normal (hint determines which way knee bends)
            float3 rawHint = hintDir - rootPos;
            float3 planeNormal = math.normalizesafe(math.cross(targetDir, math.normalizesafe(rawHint - targetDir * math.dot(rawHint, targetDir))));
            if (math.lengthsq(planeNormal) < 0.001f)
                planeNormal = math.normalizesafe(math.cross(targetDir, new float3(0, 1, 0)));

            // Upper bone angle from law of cosines
            float cosUpper = (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen)
                             / (2f * upperLen * targetDist);
            cosUpper = math.clamp(cosUpper, -1f, 1f);
            float upperAngle = math.acos(cosUpper);

            // Rotate target direction by upper angle around plane normal to get mid position
            quaternion upperRotQ = quaternion.AxisAngle(planeNormal, upperAngle);
            float3 upperDir = math.mul(upperRotQ, targetDir);
            float3 newMid = rootPos + upperDir * upperLen;

            // End position
            float3 newEnd = targetPos;

            // Apply weight
            outMidPos = math.lerp(midPos, newMid, weight);
            outEndPos = math.lerp(endPos, newEnd, weight);
            outRootPos = rootPos;

            // Compute rotations
            float3 origUpperDir = math.normalizesafe(midPos - rootPos);
            float3 newUpperDir = math.normalizesafe(outMidPos - rootPos);
            if (math.lengthsq(origUpperDir) > 0.001f && math.lengthsq(newUpperDir) > 0.001f)
            {
                quaternion upperDelta = FromToRotation(origUpperDir, newUpperDir);
                outRootRot = math.normalize(math.mul(upperDelta, rootRot));
            }

            float3 origLowerDir = math.normalizesafe(endPos - midPos);
            float3 newLowerDir = math.normalizesafe(outEndPos - outMidPos);
            if (math.lengthsq(origLowerDir) > 0.001f && math.lengthsq(newLowerDir) > 0.001f)
            {
                quaternion lowerDelta = FromToRotation(origLowerDir, newLowerDir);
                outMidRot = math.normalize(math.mul(lowerDelta, midRot));
            }

            outEndRot = endRot;
        }

        static quaternion FromToRotation(float3 from, float3 to)
        {
            float3 cross = math.cross(from, to);
            float dot = math.dot(from, to);
            if (dot < -0.9999f)
            {
                float3 perp = math.abs(from.x) < 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
                float3 axis = math.normalizesafe(math.cross(from, perp));
                return new quaternion(axis.x, axis.y, axis.z, 0);
            }
            float w = 1f + dot;
            return math.normalize(new quaternion(cross.x, cross.y, cross.z, w));
        }
    }
}
