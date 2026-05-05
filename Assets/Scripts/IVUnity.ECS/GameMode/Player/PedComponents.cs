using System;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.ECS.GameMode
{
    public struct FixedInputEvent
    {
        byte _wasEverSet;
        uint _lastSetTick;

        public void Set(uint tick) { _lastSetTick = tick; _wasEverSet = 1; }
        public bool IsSet(uint tick) => _wasEverSet == 1 && tick == _lastSetTick;
    }

    public struct PedTag : IComponentData { }

    [Serializable]
    public struct PedCharacterComponent : IComponentData
    {
        public float RotationSharpness;
        public float GroundMaxSpeed;
        public float GroundedMovementSharpness;
        public float AirAcceleration;
        public float AirMaxSpeed;
        public float AirDrag;
        public float JumpSpeed;
        public float3 Gravity;
        public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

        public static PedCharacterComponent GetDefault() => new()
        {
            RotationSharpness = 10f,
            GroundMaxSpeed = 5f,
            GroundedMovementSharpness = 15f,
            AirAcceleration = 8f,
            AirMaxSpeed = 5f,
            AirDrag = 0f,
            JumpSpeed = 6f,
            Gravity = new float3(0, -20f, 0),
            StepAndSlopeHandling = new BasicStepAndSlopeHandlingParameters
            {
                StepHandling = true,
                MaxStepHeight = 0.4f,
                CharacterWidthForStepGroundingCheck = 1f,
                PreventGroundingWhenMovingTowardsNoGrounding = true,
                HasMaxDownwardSlopeChangeAngle = false,
                MaxDownwardSlopeChangeAngle = 90f,
                ConstrainVelocityToGroundPlane = true,
            },
        };
    }

    [Serializable]
    public struct PedCharacterControl : IComponentData
    {
        public float3 MoveVector;
        public float MoveSpeed;
        public bool Jump;
    }

    [Serializable]
    public struct PedPlayer : IComponentData
    {
        public Entity ControlledCharacter;
        public Entity ControlledCamera;
    }

    [Serializable]
    public struct PedPlayerInputs : IComponentData
    {
        public float2 MoveInput;
        public float2 CameraLookInput;
        public float CameraZoomInput;
        public float MoveSpeed;
        public FixedInputEvent JumpPressed;
    }
}
