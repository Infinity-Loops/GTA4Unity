using Unity.Burst;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PedPlayerVariableStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PedPlayer, PedPlayerInputs>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (inputs, player) in SystemAPI.Query<RefRO<PedPlayerInputs>, RefRO<PedPlayer>>().WithAll<Simulate>())
            {
                if (SystemAPI.HasComponent<OrbitCameraControl>(player.ValueRO.ControlledCamera))
                {
                    var camCtrl = SystemAPI.GetComponent<OrbitCameraControl>(player.ValueRO.ControlledCamera);
                    camCtrl.FollowedCharacterEntity = player.ValueRO.ControlledCharacter;
                    camCtrl.LookDegreesDelta = inputs.ValueRO.CameraLookInput;
                    camCtrl.ZoomDelta = inputs.ValueRO.CameraZoomInput;
                    SystemAPI.SetComponent(player.ValueRO.ControlledCamera, camCtrl);
                }
            }
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct PedPlayerFixedStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FixedTickSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PedPlayer, PedPlayerInputs>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;

            foreach (var (inputs, player) in SystemAPI.Query<RefRO<PedPlayerInputs>, RefRO<PedPlayer>>().WithAll<Simulate>())
            {
                if (!SystemAPI.HasComponent<PedCharacterControl>(player.ValueRO.ControlledCharacter))
                    continue;

                var charCtrl = SystemAPI.GetComponent<PedCharacterControl>(player.ValueRO.ControlledCharacter);
                float3 charUp = MathUtilities.GetUpFromRotation(
                    SystemAPI.GetComponent<LocalTransform>(player.ValueRO.ControlledCharacter).Rotation);

                quaternion cameraRot = quaternion.identity;
                if (SystemAPI.HasComponent<OrbitCamera>(player.ValueRO.ControlledCamera))
                {
                    var orbitCam = SystemAPI.GetComponent<OrbitCamera>(player.ValueRO.ControlledCamera);
                    cameraRot = OrbitCameraUtilities.CalculateCameraRotation(charUp, orbitCam.PlanarForward, orbitCam.PitchAngle);
                }

                float3 camForward = math.normalizesafe(MathUtilities.ProjectOnPlane(
                    MathUtilities.GetForwardFromRotation(cameraRot), charUp));
                float3 camRight = MathUtilities.GetRightFromRotation(cameraRot);

                charCtrl.MoveVector = (inputs.ValueRO.MoveInput.y * camForward) + (inputs.ValueRO.MoveInput.x * camRight);
                charCtrl.MoveVector = MathUtilities.ClampToMaxLength(charCtrl.MoveVector, 1f);
                charCtrl.MoveSpeed = inputs.ValueRO.MoveSpeed;
                charCtrl.Jump = inputs.ValueRO.JumpPressed.IsSet(tick);

                SystemAPI.SetComponent(player.ValueRO.ControlledCharacter, charCtrl);
            }
        }
    }
}
