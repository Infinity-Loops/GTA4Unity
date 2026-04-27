using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FlyingCameraMovementSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<FlyingCameraTag>();
            RequireForUpdate<Possessed>();
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            var query = new Unity.Entities.EntityQueryBuilder(Unity.Collections.Allocator.Temp)
                .WithAll<Possessed, FlyingCameraTag, FlyingCameraInput, FlyingCameraSettings, LocalTransform>()
                .Build(EntityManager);

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity e = entities[0];
            entities.Dispose();

            var transform = EntityManager.GetComponentData<LocalTransform>(e);
            var input = EntityManager.GetComponentData<FlyingCameraInput>(e);
            var settings = EntityManager.GetComponentData<FlyingCameraSettings>(e);

            settings.Yaw += input.LookDelta.x * settings.MouseSensitivity;
            settings.Pitch = math.clamp(
                settings.Pitch - input.LookDelta.y * settings.MouseSensitivity,
                -89f, 89f);

            quaternion rot = quaternion.Euler(
                math.radians(settings.Pitch),
                math.radians(settings.Yaw),
                0f);

            float3 forward = math.forward(rot);
            float3 right = math.mul(rot, new float3(1, 0, 0));

            float3 moveDir = float3.zero;
            moveDir += forward * input.MoveDelta.y;
            moveDir += right * input.MoveDelta.x;
            moveDir.y += input.VerticalInput;

            if (math.lengthsq(moveDir) > 0.001f)
                moveDir = math.normalize(moveDir);

            float speed = settings.MoveSpeed * input.SpeedMultiplier;
            float3 newPos = transform.Position + moveDir * speed * dt;

            EntityManager.SetComponentData(e, LocalTransform.FromPositionRotation(newPos, rot));
            EntityManager.SetComponentData(e, settings);

            // Flying camera: camera = entity (1:1)
            // A player controller would write offset/orbit here instead
            EntityManager.SetComponentData(e, new CameraTarget
            {
                Position = newPos,
                Rotation = rot,
            });
        }
    }
}
