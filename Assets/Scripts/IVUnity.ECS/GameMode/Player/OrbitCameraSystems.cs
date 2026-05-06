using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PedPlayerVariableStepControlSystem))]
    [UpdateAfter(typeof(PedCharacterVariableUpdateSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct OrbitCameraSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<OrbitCamera, OrbitCameraControl>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new OrbitCameraSimulationJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                KinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct OrbitCameraSimulationJob : IJobEntity
        {
            public float DeltaTime;
            public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup;

            void Execute(Entity entity, ref OrbitCamera cam, in OrbitCameraControl ctrl)
            {
                if (!LocalTransformLookup.TryGetComponent(ctrl.FollowedCharacterEntity, out var charTransform))
                    return;

                float3 targetUp = math.up();

                quaternion tmpPlanarRot = MathUtilities.CreateRotationWithUpPriority(targetUp, cam.PlanarForward);
                if (KinematicCharacterBodyLookup.TryGetComponent(ctrl.FollowedCharacterEntity, out var charBody))
                {
                    KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(
                        ref tmpPlanarRot, charBody.RotationFromParent,
                        DeltaTime, charBody.LastPhysicsUpdateDeltaTime);
                }
                cam.PlanarForward = MathUtilities.GetForwardFromRotation(tmpPlanarRot);

                float yaw = ctrl.LookDegreesDelta.x * cam.RotationSpeed;
                quaternion yawRot = quaternion.Euler(targetUp * math.radians(yaw));
                cam.PlanarForward = math.rotate(yawRot, cam.PlanarForward);

                cam.PitchAngle += -ctrl.LookDegreesDelta.y * cam.RotationSpeed;
                cam.PitchAngle = math.clamp(cam.PitchAngle, cam.MinVAngle, cam.MaxVAngle);

                cam.TargetDistance = math.clamp(
                    cam.TargetDistance + ctrl.ZoomDelta * cam.DistanceMovementSpeed,
                    cam.MinDistance, cam.MaxDistance);

                cam.SmoothedTargetDistance = math.lerp(cam.SmoothedTargetDistance, cam.TargetDistance,
                    MathUtilities.GetSharpnessInterpolant(cam.DistanceMovementSharpness, DeltaTime));

                // Write a preliminary position from simulation transform (TransformSystemGroup will interpolate the character)
                float3 targetPos = charTransform.Position + cam.FollowOffset;
                quaternion camRot = CalculateCameraRotation(targetUp, cam.PlanarForward, cam.PitchAngle);
                float3 camPos = targetPos + (-MathUtilities.GetForwardFromRotation(camRot) * cam.SmoothedTargetDistance);
                LocalTransformLookup[entity] = LocalTransform.FromPositionRotation(camPos, camRot);
            }
        }

        public static quaternion CalculateCameraRotation(float3 targetUp, float3 planarForward, float pitchAngle)
        {
            quaternion pitch = quaternion.Euler(math.right() * math.radians(pitchAngle));
            quaternion rot = MathUtilities.CreateRotationWithUpPriority(targetUp, planarForward);
            return math.mul(rot, pitch);
        }
    }

    /// <summary>
    /// Runs after TransformSystemGroup so the character's LocalToWorld is interpolated.
    /// Repositions the camera using the interpolated target position for jitter-free rendering.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct OrbitCameraLateUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<OrbitCamera, OrbitCameraControl>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hasPhysics = SystemAPI.HasSingleton<PhysicsWorldSingleton>();
            var collWorld = hasPhysics
                ? SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld
                : default;

            new OrbitCameraLateUpdateJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
                CollisionWorld = collWorld,
                HasPhysics = hasPhysics,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct OrbitCameraLateUpdateJob : IJobEntity
        {
            public float DeltaTime;
            public bool HasPhysics;
            public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public CollisionWorld CollisionWorld;

            void Execute(Entity entity, ref OrbitCamera cam, in OrbitCameraControl ctrl)
            {
                if (!LocalToWorldLookup.TryGetComponent(ctrl.FollowedCharacterEntity, out var charLtw))
                    return;

                float3 targetUp = math.up();
                float3 targetPos = charLtw.Position + cam.FollowOffset;

                quaternion camRot = OrbitCameraSimulationSystem.CalculateCameraRotation(targetUp, cam.PlanarForward, cam.PitchAngle);
                float3 camDir = -MathUtilities.GetForwardFromRotation(camRot);
                float desiredDist = cam.SmoothedTargetDistance;

                // Sphere cast from target toward camera to detect obstruction
                float obstructedDist = desiredDist;
                if (HasPhysics && cam.ObstructionRadius > 0f)
                {
                    var sphere = SphereCollider.Create(
                        new SphereGeometry { Radius = cam.ObstructionRadius },
                        cam.ObstructionFilter);

                    unsafe
                    {
                        var castInput = new ColliderCastInput(
                            sphere, targetPos,
                            targetPos + camDir * desiredDist);

                        if (CollisionWorld.CastCollider(castInput, out var hit))
                        {
                            obstructedDist = math.max(hit.Fraction * desiredDist, cam.ObstructionMinDistance);
                        }
                    }

                    sphere.Dispose();
                }

                // Snap in when obstructed, smooth out when clear
                if (obstructedDist < cam.ObstructedDistance)
                    cam.ObstructedDistance = obstructedDist;
                else
                    cam.ObstructedDistance = math.lerp(cam.ObstructedDistance, obstructedDist,
                        MathUtilities.GetSharpnessInterpolant(cam.DistanceMovementSharpness, DeltaTime));

                float3 camPos = targetPos + camDir * cam.ObstructedDistance;
                LocalToWorldLookup[entity] = new LocalToWorld { Value = new float4x4(camRot, camPos) };
            }
        }
    }

    public static class OrbitCameraUtilities
    {
        public static quaternion CalculateCameraRotation(float3 targetUp, float3 planarForward, float pitchAngle)
        {
            return OrbitCameraSimulationSystem.CalculateCameraRotation(targetUp, planarForward, pitchAngle);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PedCameraSyncSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PedCameraTag>();
        }

        protected override void OnUpdate()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<PedCameraTag>())
            {
                cam.transform.SetPositionAndRotation(ltw.ValueRO.Position, ltw.ValueRO.Rotation);
            }
        }
    }
}
