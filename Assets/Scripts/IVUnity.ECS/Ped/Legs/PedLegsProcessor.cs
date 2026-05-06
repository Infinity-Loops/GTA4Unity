using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace IVUnity.ECS.Ped.Legs
{
    public class PedLegsProcessor
    {
        public PedLegData[] Legs;
        public bool IsInitialized { get; private set; }

        float _hipsOffset;
        float _hipsOffsetVelocity;
        float3 _hipsStabilityOffset;
        float3 _hipsStabilityVelocity;

        public PedLegsSettings Settings;
        public CollisionFilter GroundFilter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = ~0u };

        float3 _rootPos;
        quaternion _rootRot;
        float3 _rootUp;
        float _scaleRef;
        float _isMovingBlend;

        public float HipsOffsetResult => _hipsOffset;

        public void Initialize(
            Vector3[] worldPositions, quaternion[] worldRotations,
            int[] parentIndices, float3 rootPos, quaternion rootRot,
            PedLegSetup[] legSetups)
        {
            _rootPos = rootPos;
            _rootRot = rootRot;
            _rootUp = math.mul(rootRot, new float3(0, 1, 0));
            _scaleRef = 1f;

            Legs = new PedLegData[legSetups.Length];
            for (int i = 0; i < legSetups.Length; i++)
            {
                var setup = legSetups[i];
                var leg = new PedLegData();
                leg.ThighIndex = setup.ThighBoneIndex;
                leg.KneeIndex = setup.KneeBoneIndex;
                leg.AnkleIndex = setup.AnkleBoneIndex;

                float3 thighPos = IsValidPos(worldPositions, leg.ThighIndex);
                float3 kneePos = IsValidPos(worldPositions, leg.KneeIndex);
                float3 anklePos = IsValidPos(worldPositions, leg.AnkleIndex);

                leg.UpperLength = math.length(kneePos - thighPos);
                leg.LowerLength = math.length(anklePos - kneePos);
                leg.InitAnklePosRootSpace = InverseTransformPoint(rootPos, rootRot, anklePos);
                leg.IKTargetPos = anklePos;
                leg.PreviousIKPos = anklePos;
                leg.GroundNormal = new float3(0, 1, 0);
                leg.KneeHintWorld = kneePos + math.mul(rootRot, new float3(0, 0, 1)) * 0.3f;
                leg.GlueBlend = 0f;
                leg.IsGlued = false;

                Legs[i] = leg;
            }

            _hipsOffset = 0f;
            _hipsOffsetVelocity = 0f;
            _hipsStabilityOffset = float3.zero;
            _hipsStabilityVelocity = float3.zero;
            Settings = PedLegsSettings.Default;
            IsInitialized = true;
        }

        public void Update(
            Vector3[] worldPos, Quaternion[] worldRot,
            float3 rootPos, quaternion rootRot,
            float dt, float isMoving,
            in CollisionWorld collisionWorld)
        {
            if (!IsInitialized || Legs == null) return;
            if (dt <= 0f) return;

            _rootPos = rootPos;
            _rootRot = rootRot;
            _rootUp = math.mul(rootRot, new float3(0, 1, 0));
            _isMovingBlend = isMoving;

            // Phase 1: Raycast under each foot
            for (int i = 0; i < Legs.Length; i++)
                RaycastLeg(ref Legs[i], worldPos, in collisionWorld);

            // Phase 2: Compute hips height offset (lower body to reach ground)
            float targetHipsOffset = ComputeHipsHeightOffset();
            float dampTime = Mathf.LerpUnclamped(0.2f, 0.01f, Settings.HipsHeightSpeed);
            _hipsOffset = Mathf.SmoothDamp(_hipsOffset, targetHipsOffset * Settings.HipsHeightBlend, ref _hipsOffsetVelocity, dampTime, 10000f, dt);

            // Phase 3: Compute hips stability (center of mass)
            float3 stabilityOffset = ComputeStabilityOffset();
            float stabDamp = Mathf.LerpUnclamped(0.4f, 0.01f, Settings.HipsStabilitySpeed);
            float stabMult = math.lerp(1f, 0.5f, _isMovingBlend);
            float3 targetStab = stabilityOffset * Settings.HipsStabilityBlend * stabMult;
            SmoothDamp3(ref _hipsStabilityOffset, targetStab, ref _hipsStabilityVelocity, stabDamp, dt);

            // Phase 4: Apply hips offset to all bone positions
            float3 hipsWorldOffset = _rootUp * _hipsOffset + TransformVector(_rootRot, _hipsStabilityOffset);
            for (int i = 0; i < worldPos.Length; i++)
                worldPos[i] += (Vector3)hipsWorldOffset;

            // Phase 5: Foot IK + alignment + gluing
            for (int i = 0; i < Legs.Length; i++)
                ProcessLegIK(ref Legs[i], worldPos, worldRot, dt);
        }

        void RaycastLeg(ref PedLegData leg, Vector3[] worldPos, in CollisionWorld collisionWorld)
        {
            float3 anklePos = IsValidPos(worldPos, leg.AnkleIndex);
            leg.AnimatedAnklePosWorld = anklePos;

            float3 origin = anklePos + _rootUp * Settings.RaycastOriginUp;
            float3 end = anklePos - _rootUp * Settings.RaycastDistance;

            var rayInput = new RaycastInput
            {
                Start = origin,
                End = end,
                Filter = GroundFilter
            };

            if (collisionWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
            {
                leg.GroundHit = true;
                leg.GroundPoint = hit.Position;
                leg.GroundNormal = hit.SurfaceNormal;
                leg.GroundDistance = math.length(hit.Position - origin);
            }
            else
            {
                leg.GroundHit = false;
                leg.GroundDistance = Settings.RaycastDistance;
            }
        }

        float ComputeHipsHeightOffset()
        {
            float lowestOffset = 0f;
            for (int i = 0; i < Legs.Length; i++)
            {
                if (!Legs[i].GroundHit) continue;

                // How much the foot needs to move down to reach ground
                float ankleY = math.dot((float3)(Vector3)Legs[i].AnimatedAnklePosWorld - _rootPos, _rootUp);
                float groundY = math.dot(Legs[i].GroundPoint - _rootPos, _rootUp);
                float diff = groundY - ankleY;

                if (diff < lowestOffset)
                    lowestOffset = diff;
            }

            // Also apply stretch prevention
            if (Settings.HipsStretchPreventer > 0f)
            {
                for (int i = 0; i < Legs.Length; i++)
                {
                    float totalLen = Legs[i].UpperLength + Legs[i].LowerLength;
                    float3 thighPos = IsValidPos(null, -1); // dummy, we use target
                    // Check if IK target would overstretch
                    if (Legs[i].GroundHit)
                    {
                        float3 thigh = IsValidPosFromLeg(Legs[i].ThighIndex);
                        float dist = math.length(Legs[i].GroundPoint - thigh);
                        if (dist > totalLen * 0.95f)
                        {
                            float overstretch = dist - totalLen * 0.95f;
                            float correction = -overstretch * Settings.HipsStretchPreventer;
                            if (correction < lowestOffset)
                                lowestOffset = correction;
                        }
                    }
                }
            }

            return lowestOffset;
        }

        float3 ComputeStabilityOffset()
        {
            if (Settings.HipsStabilityBlend <= 0f) return float3.zero;

            float3 offset = float3.zero;
            float count = Legs.Length;

            for (int i = 0; i < Legs.Length; i++)
            {
                float3 initPos = TransformPoint(_rootPos, _rootRot, Legs[i].InitAnklePosRootSpace);
                float3 currentPos = Legs[i].PreviousIKPos;
                float3 diff = currentPos - initPos;

                // Only XZ stability (don't push up)
                float3 localDiff = InverseTransformVector(_rootRot, diff);
                localDiff.y *= 0.25f;
                if (localDiff.y > 0f) localDiff.y = 0f;

                offset += localDiff / count;
            }

            return offset;
        }

        void ProcessLegIK(ref PedLegData leg, Vector3[] worldPos, Quaternion[] worldRot, float dt)
        {
            if (!leg.GroundHit)
            {
                // No ground — release glue, use animated position
                leg.IsGlued = false;
                leg.GlueBlend = math.max(0f, leg.GlueBlend - dt * Settings.GlueReleaseSpeed);
                leg.PreviousIKPos = leg.AnimatedAnklePosWorld;
                return;
            }

            float3 targetIKPos = leg.GroundPoint;

            // Foot alignment: is foot below or at ground level?
            float footHeightAboveGround = math.dot(leg.AnimatedAnklePosWorld - leg.GroundPoint, _rootUp);

            if (footHeightAboveGround <= 0.02f * _scaleRef)
            {
                // Foot is at or below ground → align to ground
                targetIKPos = leg.GroundPoint;
            }
            else
            {
                // Foot is above ground → blend between animated and ground
                float alignT = math.saturate(1f - footHeightAboveGround / (0.1f * _scaleRef));
                targetIKPos = math.lerp(leg.AnimatedAnklePosWorld, leg.GroundPoint, alignT * Settings.FootAlignBlend);
            }

            // Gluing: stick foot to ground when idle
            float glueTarget = (1f - _isMovingBlend) * Settings.GlueBlend;
            if (glueTarget > 0.01f && !leg.IsGlued)
            {
                // Attach
                leg.IsGlued = true;
                leg.GluePosition = targetIKPos;
            }

            if (leg.IsGlued)
            {
                float distFromGlue = math.length(targetIKPos - leg.GluePosition);
                if (distFromGlue > Settings.GlueThreshold || _isMovingBlend > 0.5f)
                {
                    // Release glue
                    leg.IsGlued = false;
                    leg.GlueBlend = math.max(0f, leg.GlueBlend - dt * Settings.GlueReleaseSpeed);
                }
                else
                {
                    leg.GlueBlend = math.min(1f, leg.GlueBlend + dt * 6f);
                    targetIKPos = math.lerp(targetIKPos, leg.GluePosition, leg.GlueBlend * glueTarget);
                }
            }
            else
            {
                leg.GlueBlend = math.max(0f, leg.GlueBlend - dt * Settings.GlueReleaseSpeed);
            }

            leg.IKTargetPos = targetIKPos;
            leg.PreviousIKPos = targetIKPos;

            // Solve two-bone IK
            float3 thighPos = IsValidPos(worldPos, leg.ThighIndex);
            float3 kneePos = IsValidPos(worldPos, leg.KneeIndex);
            float3 anklePos = IsValidPos(worldPos, leg.AnkleIndex);
            quaternion thighRot = IsValidRot(worldRot, leg.ThighIndex);
            quaternion kneeRot = IsValidRot(worldRot, leg.KneeIndex);
            quaternion ankleRot = IsValidRot(worldRot, leg.AnkleIndex);

            // Knee hint: forward of the animated knee
            leg.KneeHintWorld = kneePos + math.mul(_rootRot, new float3(0, 0, 0.3f));

            TwoBoneIK.Solve(
                thighPos, thighRot,
                kneePos, kneeRot,
                anklePos, ankleRot,
                targetIKPos, leg.KneeHintWorld, 1f,
                out float3 newThigh, out quaternion newThighRot,
                out float3 newKnee, out quaternion newKneeRot,
                out float3 newAnkle, out quaternion newAnkleRot);

            worldPos[leg.ThighIndex] = (Vector3)newThigh;
            worldPos[leg.KneeIndex] = (Vector3)newKnee;
            worldPos[leg.AnkleIndex] = (Vector3)newAnkle;
            worldRot[leg.ThighIndex] = (Quaternion)newThighRot;
            worldRot[leg.KneeIndex] = (Quaternion)newKneeRot;

            // Foot rotation alignment to ground normal
            if (Settings.FootAlignBlend > 0f && leg.GroundHit)
            {
                quaternion footAlignRot = AlignFootToGround(ankleRot, leg.GroundNormal);
                worldRot[leg.AnkleIndex] = (Quaternion)math.slerp(ankleRot, footAlignRot, Settings.FootAlignBlend);
            }
        }

        quaternion AlignFootToGround(quaternion currentRot, float3 groundNormal)
        {
            float3 currentUp = math.mul(currentRot, new float3(0, 1, 0));
            quaternion correction = TwoBoneIK_FromToRotation(currentUp, groundNormal);
            return math.mul(correction, currentRot);
        }

        static quaternion TwoBoneIK_FromToRotation(float3 from, float3 to)
        {
            float3 cross = math.cross(from, to);
            float dot = math.dot(from, to);
            float w = 1f + dot;
            if (w < 0.0001f)
            {
                float3 perp = math.abs(from.x) < 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
                float3 axis = math.normalizesafe(math.cross(from, perp));
                return new quaternion(axis.x, axis.y, axis.z, 0);
            }
            return math.normalize(new quaternion(cross.x, cross.y, cross.z, w));
        }

        // Helpers
        float3 IsValidPos(Vector3[] arr, int idx)
        {
            if (arr == null || idx < 0 || idx >= arr.Length) return float3.zero;
            return (float3)arr[idx];
        }

        float3 IsValidPosFromLeg(int idx)
        {
            return float3.zero; // Placeholder for stretch check — uses current worldPos
        }

        quaternion IsValidRot(Quaternion[] arr, int idx)
        {
            if (arr == null || idx < 0 || idx >= arr.Length) return quaternion.identity;
            return (quaternion)arr[idx];
        }

        float3 TransformPoint(float3 origin, quaternion rot, float3 localPos)
        {
            return origin + math.mul(rot, localPos);
        }

        float3 TransformVector(quaternion rot, float3 localVec)
        {
            return math.mul(rot, localVec);
        }

        float3 InverseTransformPoint(float3 origin, quaternion rot, float3 worldPos)
        {
            return math.mul(math.inverse(rot), worldPos - origin);
        }

        float3 InverseTransformVector(quaternion rot, float3 worldVec)
        {
            return math.mul(math.inverse(rot), worldVec);
        }

        static void SmoothDamp3(ref float3 current, float3 target, ref float3 velocity, float smoothTime, float dt)
        {
            current.x = Mathf.SmoothDamp(current.x, target.x, ref velocity.x, smoothTime, 10000f, dt);
            current.y = Mathf.SmoothDamp(current.y, target.y, ref velocity.y, smoothTime, 10000f, dt);
            current.z = Mathf.SmoothDamp(current.z, target.z, ref velocity.z, smoothTime, 10000f, dt);
        }
    }

    public struct PedLegSetup
    {
        public int ThighBoneIndex;
        public int KneeBoneIndex;
        public int AnkleBoneIndex;
    }
}
