using Unity.Mathematics;

namespace IVUnity.ECS.Ped.Legs
{
    public static class TwoBoneIK
    {
        public const float DefaultMaxStretch = 0.99f;

        public static void Solve(
            float3 rootPos, quaternion rootRot,
            float3 midPos, quaternion midRot,
            float3 endPos, quaternion endRot,
            float3 targetPos, float3 hintPos,
            float weight,
            out float3 outRootPos, out quaternion outRootRot,
            out float3 outMidPos, out quaternion outMidRot,
            out float3 outEndPos, out quaternion outEndRot,
            float maxStretch = DefaultMaxStretch)
        {
            outRootPos = rootPos;
            outRootRot = rootRot;
            outMidPos = midPos;
            outMidRot = midRot;
            outEndPos = endPos;
            outEndRot = endRot;

            if (weight < 0.001f) return;

            float upperLenSq = math.lengthsq(midPos - rootPos);
            float lowerLenSq = math.lengthsq(endPos - midPos);
            float upperLen = math.sqrt(upperLenSq);
            float lowerLen = math.sqrt(lowerLenSq);
            float totalLen = upperLen + lowerLen;

            float3 toTarget = targetPos - rootPos;
            float targetDistSq = math.lengthsq(toTarget);
            float targetDist = math.sqrt(targetDistSq);

            if (targetDist < 0.001f)
            {
                outEndPos = math.lerp(endPos, targetPos, weight);
                return;
            }

            // Stretch clamping — clamp IK target and fade weight when overextended
            float stretch = targetDist / totalLen;
            if (stretch > maxStretch)
            {
                targetDist = totalLen * maxStretch;
                targetDistSq = targetDist * targetDist;
                toTarget = math.normalizesafe(toTarget) * targetDist;
                targetPos = rootPos + toTarget;

                float stretchDiff = math.saturate((stretch - maxStretch) * 3f);
                weight *= (1f - stretchDiff);
                if (weight < 0.001f) return;
            }

            // Direction from root to target
            float3 targetDir = toTarget / targetDist;

            // Bend plane normal from hint position (same as original's CalculateElbowNormalToPosition)
            float3 bendNormal = math.cross(hintPos - rootPos, targetPos - rootPos);
            if (math.lengthsq(bendNormal) < 0.0001f)
                bendNormal = math.cross(midPos - rootPos, targetPos - rootPos);
            if (math.lengthsq(bendNormal) < 0.0001f)
                bendNormal = math.cross(targetDir, new float3(0, 1, 0));
            bendNormal = math.normalizesafe(bendNormal);

            // Perpendicular up within the bend plane
            float3 perpUp = math.normalizesafe(math.cross(targetDir, bendNormal));

            // Law of cosines — direct Cartesian (avoids acos, matches original)
            float forwardLen = (targetDistSq + upperLenSq - lowerLenSq) / (2f * targetDist);
            float upLen = math.sqrt(math.max(upperLenSq - forwardLen * forwardLen, 0f));

            // Mid position from the orientation direction
            float3 newMid = rootPos + targetDir * forwardLen + perpUp * upLen;
            float3 newEnd = targetPos;

            // Apply weight
            outMidPos = math.lerp(midPos, newMid, weight);
            outEndPos = math.lerp(endPos, newEnd, weight);
            outRootPos = rootPos;

            // Rotation deltas
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
