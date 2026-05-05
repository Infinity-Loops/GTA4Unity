using IVUnity.ECS.Ped;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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

            foreach (var (inputs, player) in SystemAPI.Query<RefRW<PedPlayerInputs>, RefRO<PedPlayer>>())
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

                float inputMag = math.length(inputs.ValueRO.MoveInput);
                float moveSpeed = 0f;
                if (inputMag > 0.1f)
                {
                    moveSpeed = 1.5f;
                    if (Input.GetKey(KeyCode.LeftShift))
                        moveSpeed = 3.5f;
                    if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
                        moveSpeed = 5.5f;
                }
                inputs.ValueRW.MoveSpeed = moveSpeed;

                Entity charEntity = player.ValueRO.ControlledCharacter;
                if (charEntity != Entity.Null)
                    SetMoveBlendSpeed(charEntity, moveSpeed);
            }
        }

        private void SetMoveBlendSpeed(Entity charEntity, float moveSpeed)
        {
            if (!EntityManager.HasBuffer<Child>(charEntity)) return;
            var children = EntityManager.GetBuffer<Child>(charEntity);
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i].Value;
                if (EntityManager.HasComponent<PedMoveBlend>(child))
                {
                    var blend = EntityManager.GetComponentData<PedMoveBlend>(child);
                    float animSpeed = moveSpeed <= 0f ? 0f :
                                      moveSpeed <= 2f ? 1f :
                                      moveSpeed <= 4f ? 2f : 3f;
                    blend.DesiredSpeed = math.lerp(blend.DesiredSpeed, animSpeed, SystemAPI.Time.DeltaTime * 5f);
                    EntityManager.SetComponentData(child, blend);
                    break;
                }
            }
        }
    }
}
