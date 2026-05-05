using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial class PedPlayerInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<FixedTickSystem.Singleton>();
            RequireForUpdate<PedPlayer>();
        }

        protected override void OnUpdate()
        {
            uint tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;

            foreach (var (inputs, _) in SystemAPI.Query<RefRW<PedPlayerInputs>, RefRO<PedPlayer>>())
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                inputs.ValueRW.MoveInput = new float2(h, v);

                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                inputs.ValueRW.CameraLookInput = new float2(mouseX, mouseY) * 10f;

                inputs.ValueRW.CameraZoomInput = -Input.GetAxis("Mouse ScrollWheel") * 100f;

                if (Input.GetKeyDown(KeyCode.Space))
                    inputs.ValueRW.JumpPressed.Set(tick);
            }
        }
    }
}
