using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class FlyingCameraMovementSystem : SystemBase
    {
        private EntityQuery flyingQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<FlyingCameraTag>();
            RequireForUpdate<Possessed>();

            flyingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Possessed, FlyingCameraTag, FlyingCameraInput, FlyingCameraSettings, LocalTransform>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (flyingQuery.IsEmpty) return;
            Entity e = flyingQuery.GetSingletonEntity();

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

            EntityManager.SetComponentData(e, new PhysicsVelocity { Linear = moveDir * speed, Angular = float3.zero });

            transform.Rotation = rot;
            EntityManager.SetComponentData(e, transform);
            EntityManager.SetComponentData(e, settings);
            EntityManager.SetComponentData(e, new CameraTarget
            {
                Position = transform.Position,
                Rotation = rot,
            });
        }
    }
}
