using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.ECS.GameMode
{
    /// <summary>Tag on the currently possessed entity. Only one entity should have this.</summary>
    public struct Possessed : IComponentData { }

    /// <summary>
    /// Where the camera should be. Written by the active controller system,
    /// read by GameModeCamera. Decoupled so each game mode can position
    /// the camera differently (first person, third person, orbit, etc).
    /// </summary>
    public struct CameraTarget : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }

    /// <summary>Tag marking an entity as a flying camera controller.</summary>
    public struct FlyingCameraTag : IComponentData { }

    /// <summary>Input state for the flying camera, written by the input system.</summary>
    public struct FlyingCameraInput : IComponentData
    {
        public float2 MoveDelta;
        public float2 LookDelta;
        public float VerticalInput;
        public float SpeedMultiplier;
    }

    /// <summary>Flying camera settings.</summary>
    public struct FlyingCameraSettings : IComponentData
    {
        public float MoveSpeed;
        public float MouseSensitivity;
        public float Yaw;
        public float Pitch;

        public static FlyingCameraSettings Default => new()
        {
            MoveSpeed = 50f,
            MouseSensitivity = 2f,
            Yaw = 0f,
            Pitch = 0f,
        };
    }
}
