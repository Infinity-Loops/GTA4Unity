using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using SphereCollider = Unity.Physics.SphereCollider;

namespace IVUnity.ECS.Ped.Legs
{
    /// <summary>
    /// Leg IK processor adapted from RAGE CLegIkSolver.
    /// Pipeline: ground detect → supporting foot → pelvis delta → foot delta → two-bone IK.
    /// </summary>
    public class PedLegsProcessor
    {
        public PedLegData[] Legs;
        public bool IsInitialized { get; private set; }

        public PedLegsSettings Settings;
        public CollisionFilter GroundFilter;

        // Pelvis state
        float _pelvisDeltaSmoothed;
        float _pelvisDeltaVelocity;
        float _pelvisBlend;

        // Per-leg foot delta state
        float[] _footDeltaSmoothed;
        float[] _footDeltaVelocity;

        // Bind pose reference heights (RAGE: m_afHeightOfBindPoseFootAboveCollision)
        float[] _bindPoseFootHeight;
        float _bindPoseRootHeight;

        // Runtime
        float3 _rootPos;
        quaternion _rootRot;
        float3 _rootUp;
        float _isMovingBlend;
        Vector3[] _currentWorldPos;

        const float PROBE_HYSTERESIS = 0.02f;
        const float NORMAL_VERTICAL_THRESHOLD = 0.7f;
        const float PELVIS_MAX_NEGATIVE_STANDING = -0.35f;
        const float PELVIS_MAX_POSITIVE_STANDING = 0.15f;
        const float PELVIS_MAX_NEGATIVE_MOVING = -0.2f;
        const float PELVIS_MAX_POSITIVE_MOVING = 0.1f;
        const float FOOT_MIN_DIST_TO_ROOT = 0.3f;

        public float PelvisDeltaSmoothed => _pelvisDeltaSmoothed;

        public void Initialize(
            Vector3[] worldPositions, quaternion[] worldRotations,
            int[] parentIndices, float3 rootPos, quaternion rootRot,
            PedLegSetup[] legSetups)
        {
            _rootPos = rootPos;
            _rootRot = rootRot;
            _rootUp = new float3(0, 1, 0);

            Legs = new PedLegData[legSetups.Length];
            _footDeltaSmoothed = new float[legSetups.Length];
            _footDeltaVelocity = new float[legSetups.Length];
            _bindPoseFootHeight = new float[legSetups.Length];

            // Root height in model space (Y in Unity = Z in RAGE = up)
            float rootModelY = worldPositions.Length > 0 ? worldPositions[0].y : 0f;

            // Ground level = lowest point of any foot part (toe or ankle).
            // The toe is near the sole — using it gives the actual ground plane,
            // so _bindPoseFootHeight correctly measures ankle-to-sole distance.
            float groundY = float.MaxValue;
            for (int i = 0; i < legSetups.Length; i++)
            {
                float ankleY = worldPositions[legSetups[i].AnkleBoneIndex].y;
                if (ankleY < groundY) groundY = ankleY;

                if (legSetups[i].ToeBoneIndex >= 0)
                {
                    float toeY = worldPositions[legSetups[i].ToeBoneIndex].y;
                    if (toeY < groundY) groundY = toeY;
                }
            }

            _bindPoseRootHeight = rootModelY - groundY;

            for (int i = 0; i < legSetups.Length; i++)
            {
                var setup = legSetups[i];
                var leg = new PedLegData();
                leg.ThighIndex = setup.ThighBoneIndex;
                leg.KneeIndex = setup.KneeBoneIndex;
                leg.AnkleIndex = setup.AnkleBoneIndex;
                leg.ToeIndex = setup.ToeBoneIndex;

                float3 thighPos = (float3)worldPositions[leg.ThighIndex];
                float3 kneePos = (float3)worldPositions[leg.KneeIndex];
                float3 anklePos = (float3)worldPositions[leg.AnkleIndex];

                leg.UpperLength = math.length(kneePos - thighPos);
                leg.LowerLength = math.length(anklePos - kneePos);
                leg.ProbePosition = anklePos;
                leg.GroundNormal = new float3(0, 1, 0);

                // RAGE: m_afHeightOfBindPoseFootAboveCollision
                _bindPoseFootHeight[i] = anklePos.y - groundY;

                quaternion ankleWorldRot = worldRotations[leg.AnkleIndex];
                leg.FootLocalUp = math.mul(math.inverse(ankleWorldRot), new float3(0, 1, 0));

                Legs[i] = leg;
            }

            _pelvisDeltaSmoothed = 0f;
            _pelvisDeltaVelocity = 0f;
            _pelvisBlend = 0f;
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

            _currentWorldPos = worldPos;
            _rootPos = rootPos;
            _rootRot = rootRot;
            _rootUp = math.mul(rootRot, new float3(0, 1, 0));
            _isMovingBlend = isMoving;

            // ── Phase 1: Ground detection (capsule/sphere probe at foot-toe midpoint) ──
            BlobAssetReference<Collider> sphere = default;
            bool useSphere = Settings.GroundCastRadius > 0f;
            if (useSphere)
                sphere = SphereCollider.Create(new SphereGeometry { Radius = Settings.GroundCastRadius }, GroundFilter);

            for (int i = 0; i < Legs.Length; i++)
                ProbeLeg(ref Legs[i], worldPos, in collisionWorld, useSphere, sphere);

            if (useSphere && sphere.IsCreated)
                sphere.Dispose();

            // ── Phase 2: Supporting foot + pelvis blend ──
            // RAGE uses different modes: LOWEST for standing (pelvis follows lower surface,
            // higher foot pushes UP), HIGHEST for stairs when moving up.
            // We use LOWEST — the higher foot gets a positive delta (up), which works
            // naturally with positive-only foot deltas during movement.
            bool anyGrounded = false;
            int supportIdx = -1;
            float supportGroundY = float.MaxValue;

            for (int i = 0; i < Legs.Length; i++)
            {
                Legs[i].Supporting = false;
                if (!Legs[i].GroundHit) continue;
                anyGrounded = true;

                float gy = math.dot(Legs[i].GroundPoint, _rootUp);
                if (gy < supportGroundY) { supportGroundY = gy; supportIdx = i; }
            }

            if (supportIdx >= 0)
                Legs[supportIdx].Supporting = true;

            // Pelvis blend ramps in when grounded, out when airborne
            float targetBlend = anyGrounded ? 1f : 0f;
            _pelvisBlend = Mathf.MoveTowards(_pelvisBlend, targetBlend, dt * 6f);
            if (_pelvisBlend < 0.001f && !anyGrounded)
            {
                _pelvisDeltaSmoothed = 0f;
                _pelvisDeltaVelocity = 0f;
                return;
            }

            // ── Phase 3: Pelvis delta (RAGE FixupPelvis) ──
            float pelvisDelta = 0f;
            if (supportIdx >= 0)
            {
                // RAGE: targetPelvisWorldZ = supportGroundZ + animZRoot
                float rootY = math.dot(_rootPos, _rootUp);
                float animRootAboveGround = _bindPoseRootHeight;
                float targetPelvisY = supportGroundY + animRootAboveGround;
                pelvisDelta = targetPelvisY - rootY;

                // Clamp
                if (isMoving > 0.1f)
                    pelvisDelta = math.clamp(pelvisDelta, PELVIS_MAX_NEGATIVE_MOVING, PELVIS_MAX_POSITIVE_MOVING);
                else
                    pelvisDelta = math.clamp(pelvisDelta, PELVIS_MAX_NEGATIVE_STANDING, PELVIS_MAX_POSITIVE_STANDING);
            }

            float pelvisDampTime = Mathf.LerpUnclamped(0.2f, 0.01f, Settings.HipsHeightSpeed);
            _pelvisDeltaSmoothed = Mathf.SmoothDamp(_pelvisDeltaSmoothed, pelvisDelta * _pelvisBlend, ref _pelvisDeltaVelocity, pelvisDampTime, 10000f, dt);

            // Apply pelvis offset to ALL bones
            float3 pelvisWorldOffset = _rootUp * _pelvisDeltaSmoothed;
            for (int i = 0; i < worldPos.Length; i++)
                worldPos[i] += (Vector3)pelvisWorldOffset;

            // ── Phase 4: Per-leg foot fixup + IK ──
            for (int i = 0; i < Legs.Length; i++)
                FixupLeg(ref Legs[i], i, worldPos, worldRot, dt);
        }

        // ────────────────────────────────────────────────────────────────
        // Ground probe (RAGE WorkOutLegSituationUsingShapeTests)
        // ────────────────────────────────────────────────────────────────

        void ProbeLeg(ref PedLegData leg, Vector3[] worldPos, in CollisionWorld cw,
            bool useSphere, BlobAssetReference<Collider> sphere)
        {
            float3 anklePos = (float3)worldPos[leg.AnkleIndex];
            leg.AnimatedAnklePosWorld = anklePos;

            // RAGE: probe from midpoint of foot and toe
            float3 toePos = leg.ToeIndex >= 0 ? (float3)worldPos[leg.ToeIndex] : anklePos;
            float3 centre = (anklePos + toePos) * 0.5f;
            float ankleH = math.dot(anklePos, _rootUp);
            centre += _rootUp * (ankleH - math.dot(centre, _rootUp));

            // Probe hysteresis (RAGE: ms_fProbePositionDeltaTolerance)
            float3 flatOld = leg.ProbePosition - _rootUp * math.dot(leg.ProbePosition, _rootUp);
            float3 flatNew = centre - _rootUp * math.dot(centre, _rootUp);
            if (math.length(flatNew - flatOld) > PROBE_HYSTERESIS)
                leg.ProbePosition = centre;
            else
                leg.ProbePosition = flatOld + _rootUp * ankleH;

            float3 origin = leg.ProbePosition + _rootUp * Settings.RaycastOriginUp;
            float3 end = leg.ProbePosition - _rootUp * Settings.RaycastDistance;

            bool hit = false;

            if (useSphere)
            {
                unsafe
                {
                    if (cw.CastCollider(new ColliderCastInput(sphere, origin, end), out ColliderCastHit sHit))
                    {
                        hit = true;
                        leg.GroundPoint = sHit.Position;
                        leg.GroundNormal = sHit.SurfaceNormal;
                        leg.GroundDistance = sHit.Fraction * math.length(end - origin);
                    }
                }
            }

            if (!hit)
            {
                var ray = new RaycastInput { Start = origin, End = end, Filter = GroundFilter };
                if (cw.CastRay(ray, out Unity.Physics.RaycastHit rHit))
                {
                    hit = true;
                    leg.GroundPoint = rHit.Position;
                    leg.GroundNormal = rHit.SurfaceNormal;
                    leg.GroundDistance = math.length(rHit.Position - origin);
                }
            }

            leg.GroundHit = hit;
            if (!hit)
                leg.GroundDistance = Settings.RaycastDistance;
            else if (math.dot(leg.GroundNormal, _rootUp) < NORMAL_VERTICAL_THRESHOLD)
                leg.GroundNormal = _rootUp;
        }

        // ────────────────────────────────────────────────────────────────
        // Per-leg fixup (RAGE FixupLeg → FixupFootHeight + IK)
        // ────────────────────────────────────────────────────────────────

        void FixupLeg(ref PedLegData leg, int legIdx, Vector3[] worldPos, Quaternion[] worldRot, float dt)
        {
            // IK blend ramp
            if (!leg.GroundHit)
            {
                leg.IKBlend = math.max(0f, leg.IKBlend - dt * 6f);
                if (leg.IKBlend < 0.001f)
                {
                    _footDeltaSmoothed[legIdx] = 0f;
                    _footDeltaVelocity[legIdx] = 0f;
                    return;
                }
            }
            else
            {
                leg.IKBlend = math.min(1f, leg.IKBlend + dt * 8f);
            }

            float3 anklePos = (float3)worldPos[leg.AnkleIndex];
            float ankleY = math.dot(anklePos, _rootUp);
            float rootY = math.dot(_rootPos, _rootUp) + _pelvisDeltaSmoothed;

            // ── Foot height fixup (RAGE FixupFootHeight) ──
            float footDelta = 0f;
            if (leg.GroundHit)
            {
                float groundY = math.dot(leg.GroundPoint, _rootUp);
                float targetFootY = groundY + _bindPoseFootHeight[legIdx];

                // Enforce minimum distance from foot to root (prevent over-bend)
                float minFootY = rootY - (leg.UpperLength + leg.LowerLength) * 0.95f;
                targetFootY = math.max(targetFootY, minFootY);

                footDelta = targetFootY - ankleY;

                // RAGE ms_bPositiveFootDeltaZOnly: when moving, only push feet UP
                // to prevent ground penetration. Never pull feet DOWN — that fights
                // the stride animation and causes knee distortion.
                if (_isMovingBlend > 0.1f && footDelta < 0f)
                    footDelta = 0f;

                footDelta *= leg.IKBlend;
            }

            float footDampTime = _isMovingBlend > 0.1f ? 0.06f : 0.12f;
            _footDeltaSmoothed[legIdx] = Mathf.SmoothDamp(
                _footDeltaSmoothed[legIdx], footDelta,
                ref _footDeltaVelocity[legIdx], footDampTime, 10000f, dt);

            // Apply foot height delta
            float3 footOffset = _rootUp * _footDeltaSmoothed[legIdx];
            worldPos[leg.AnkleIndex] = (Vector3)((float3)worldPos[leg.AnkleIndex] + footOffset);
            if (leg.ToeIndex >= 0)
                worldPos[leg.ToeIndex] = (Vector3)((float3)worldPos[leg.ToeIndex] + footOffset);

            // ── Knee solve (RAGE FixupLeg — two-sphere intersection with bind pose lengths) ──
            float3 thighPos = (float3)worldPos[leg.ThighIndex];
            float3 kneePos = (float3)worldPos[leg.KneeIndex];
            float3 footPos = (float3)worldPos[leg.AnkleIndex];
            quaternion thighRot = (quaternion)worldRot[leg.ThighIndex];
            quaternion kneeRot = (quaternion)worldRot[leg.KneeIndex];
            quaternion ankleRot = (quaternion)worldRot[leg.AnkleIndex];

            float thighLen = leg.UpperLength;
            float calfLen = leg.LowerLength;
            float legLen = thighLen + calfLen;

            float3 footToThigh = thighPos - footPos;
            float d = math.length(footToThigh);

            // Clamp foot if over-extended (RAGE: rollback epsilon)
            if (d > legLen)
            {
                footToThigh = math.normalizesafe(footToThigh) * (legLen - 0.001f);
                footPos = thighPos - footToThigh;
                worldPos[leg.AnkleIndex] = (Vector3)footPos;
                d = math.length(footToThigh);
            }

            if (d > 0.001f)
            {
                // Law of cosines: find point P2 on the foot-thigh line, then offset perpendicular by h
                float a = (calfLen * calfLen - thighLen * thighLen + d * d) / (2f * d);
                float hSq = calfLen * calfLen - a * a;
                float h = hSq > 0f ? math.sqrt(hSq) : 0f;

                float3 ftDir = footToThigh / d;
                float3 P2 = footPos + ftDir * a;

                // Knee hint: model forward perpendicular to foot-thigh line.
                // RAGE uses the calf bone's C axis with double-cross; we use simple
                // vector rejection since our hint is character forward, not a bone axis.
                float3 modelFwd = math.mul(_rootRot, new float3(0, 0, -1));
                float3 hVec = modelFwd - ftDir * math.dot(modelFwd, ftDir);
                hVec = math.normalizesafe(hVec, modelFwd);

                float3 newKnee = P2 + hVec * h;
                newKnee = math.lerp(kneePos, newKnee, leg.IKBlend);

                worldPos[leg.KneeIndex] = (Vector3)newKnee;

                // Orient thigh toward knee, knee toward foot (RAGE OrientateBoneATowardBoneB)
                float3 origThighDir = math.normalizesafe(kneePos - thighPos);
                float3 newThighDir = math.normalizesafe((float3)(Vector3)worldPos[leg.KneeIndex] - thighPos);
                if (math.lengthsq(origThighDir) > 0.001f && math.lengthsq(newThighDir) > 0.001f)
                {
                    quaternion delta = FromToRotation(origThighDir, newThighDir);
                    worldRot[leg.ThighIndex] = (Quaternion)math.normalize(math.mul(delta, thighRot));
                }

                float3 origCalfDir = math.normalizesafe(anklePos - kneePos);
                float3 newCalfDir = math.normalizesafe(footPos - (float3)(Vector3)worldPos[leg.KneeIndex]);
                if (math.lengthsq(origCalfDir) > 0.001f && math.lengthsq(newCalfDir) > 0.001f)
                {
                    quaternion calfDelta = FromToRotation(origCalfDir, newCalfDir);
                    worldRot[leg.KneeIndex] = (Quaternion)math.normalize(math.mul(calfDelta, kneeRot));

                    // Propagate calf rotation change to the ankle — without this the foot
                    // keeps its pre-IK orientation (disconnected from the leg chain)
                    worldRot[leg.AnkleIndex] = (Quaternion)math.normalize(math.mul(calfDelta, ankleRot));
                }
            }

            // ── Foot orientation (RAGE FixupFootOrientation) ──
            // Use the post-IK ankle rotation (calf delta already applied above).
            // Scale by howFlat: when the animation pitches the foot (heel-strike / toe-off),
            // alignment is reduced so the natural stride roll is preserved.
            float footBlend = Settings.FootAlignBlend * leg.IKBlend;
            if (footBlend > 0.001f && leg.GroundHit)
            {
                quaternion postIKAnkleRot = (quaternion)worldRot[leg.AnkleIndex];
                float3 currentUp = math.mul(postIKAnkleRot, leg.FootLocalUp);

                // RAGE howFlat: 1 when sole is parallel to ground, 0 when foot is pitched
                float howFlat = math.saturate(math.dot(currentUp, _rootUp));
                footBlend *= howFlat;

                if (footBlend > 0.001f)
                {
                    float3 cr = math.cross(currentUp, leg.GroundNormal);
                    float dt2 = math.dot(currentUp, leg.GroundNormal);
                    if (dt2 > -0.999f)
                    {
                        quaternion correction = math.normalize(new quaternion(cr.x, cr.y, cr.z, 1f + dt2));
                        quaternion aligned = math.mul(correction, postIKAnkleRot);
                        worldRot[leg.AnkleIndex] = (Quaternion)math.slerp(postIKAnkleRot, aligned, footBlend);
                    }
                }
            }

            leg.PreviousIKPos = footPos;
        }

        // Helpers
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
            return math.normalize(new quaternion(cross.x, cross.y, cross.z, 1f + dot));
        }
    }

    public struct PedLegSetup
    {
        public int ThighBoneIndex;
        public int KneeBoneIndex;
        public int AnkleBoneIndex;
        public int ToeBoneIndex;
    }
}
