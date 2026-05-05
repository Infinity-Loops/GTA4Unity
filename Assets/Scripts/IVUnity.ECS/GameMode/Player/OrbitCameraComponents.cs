using System;
using Unity.Entities;
using Unity.Mathematics;

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

        public float3 FollowOffset;

        public float TargetDistance;
        public float SmoothedTargetDistance;
        public float PitchAngle;
        public float3 PlanarForward;

        public static OrbitCamera GetDefault() => new()
        {
            RotationSpeed = 0.15f,
            MaxVAngle = 80f,
            MinVAngle = -20f,
            MinDistance = 1.5f,
            MaxDistance = 10f,
            DistanceMovementSpeed = 0.1f,
            DistanceMovementSharpness = 10f,
            FollowOffset = new float3(0, 1.2f, 0),
            TargetDistance = 5f,
            SmoothedTargetDistance = 5f,
            PitchAngle = 15f,
            PlanarForward = -math.forward(),
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
