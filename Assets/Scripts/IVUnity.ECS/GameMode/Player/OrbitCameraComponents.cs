using System;
using IVUnity.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace IVUnity.ECS.GameMode
{
    [Serializable]
    public struct OrbitCamera : IComponentData
    {
        public float RotationSpeed;
        public float MaxVAngle;
        public float MinVAngle;

        public float MinDistance;
        public float MaxDistance;
        public float DistanceMovementSpeed;
        public float DistanceMovementSharpness;

        public float ObstructionRadius;
        public float ObstructionMinDistance;
        public CollisionFilter ObstructionFilter;

        public float3 FollowOffset;

        public float TargetDistance;
        public float SmoothedTargetDistance;
        public float PitchAngle;
        public float3 PlanarForward;
        public float ObstructedDistance;

        public static OrbitCamera GetDefault() => new()
        {
            RotationSpeed = 0.15f,
            MaxVAngle = 80f,
            MinVAngle = -20f,
            MinDistance = 1.5f,
            MaxDistance = 4.5f,
            DistanceMovementSpeed = 0.1f,
            DistanceMovementSharpness = 10f,
            ObstructionRadius = 0.3f,
            ObstructionMinDistance = 0.3f,
            ObstructionFilter = PhysicsLayers.CameraObstructionFilter,
            FollowOffset = new float3(0, 1.4f, 0),
            TargetDistance = 3.5f,
            SmoothedTargetDistance = 3.5f,
            PitchAngle = 15f,
            PlanarForward = -math.forward(),
            ObstructedDistance = 3.5f,
        };
    }

    [Serializable]
    public struct OrbitCameraControl : IComponentData
    {
        public Entity FollowedCharacterEntity;
        public float2 LookDegreesDelta;
        public float ZoomDelta;
    }

    public struct PedCameraTag : IComponentData { }
}
