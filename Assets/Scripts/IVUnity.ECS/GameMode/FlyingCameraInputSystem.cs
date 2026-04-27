using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class FlyingCameraInputSystem : SystemBase
    {
        private bool captured;

        protected override void OnCreate()
        {
            RequireForUpdate<FlyingCameraTag>();
            RequireForUpdate<Possessed>();
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                captured = !captured;
                Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !captured;
            }

            if (Input.GetMouseButtonDown(1) && !captured)
            {
                captured = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");

            var query = new Unity.Entities.EntityQueryBuilder(Unity.Collections.Allocator.Temp)
                .WithAll<Possessed, FlyingCameraTag, FlyingCameraInput, FlyingCameraSettings>()
                .Build(EntityManager);

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity e = entities[0];
            entities.Dispose();

            var settings = EntityManager.GetComponentData<FlyingCameraSettings>(e);

            if (Mathf.Abs(scroll) > 0.001f)
            {
                settings.MoveSpeed = Mathf.Clamp(settings.MoveSpeed + scroll * 100f, 1f, 500f);
                EntityManager.SetComponentData(e, settings);
            }

            if (!captured)
            {
                EntityManager.SetComponentData(e, default(FlyingCameraInput));
                return;
            }

            float2 move = float2.zero;
            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;

            float vert = 0f;
            if (Input.GetKey(KeyCode.E)) vert += 1f;
            if (Input.GetKey(KeyCode.Q)) vert -= 1f;

            float speedMul = 1f;
            if (Input.GetKey(KeyCode.LeftShift)) speedMul = 3f;
            if (Input.GetKey(KeyCode.LeftControl)) speedMul = 0.25f;

            EntityManager.SetComponentData(e, new FlyingCameraInput
            {
                MoveDelta = move,
                LookDelta = new float2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")),
                VerticalInput = vert,
                SpeedMultiplier = speedMul,
            });
        }
    }
}
