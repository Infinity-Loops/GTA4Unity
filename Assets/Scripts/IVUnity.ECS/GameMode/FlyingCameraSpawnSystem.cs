using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using SphereCollider = Unity.Physics.SphereCollider;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(FlyingCameraInputSystem))]
    public partial class FlyingCameraSpawnSystem : SystemBase
    {
        private bool spawned;

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();
        }

        protected override void OnUpdate()
        {
            if (spawned) return;

            var em = EntityManager;

            Entity e = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(FlyingCameraTag),
                typeof(FlyingCameraInput),
                typeof(FlyingCameraSettings),
                typeof(CameraTarget),
                typeof(Possessed),
                typeof(PhysicsCollider),
                typeof(PhysicsVelocity),
                typeof(PhysicsMass),
                typeof(PhysicsDamping),
                typeof(PhysicsGravityFactor),
                typeof(PhysicsWorldIndex));

            float3 startPos = new float3(0, 100, 0);
            float startYaw = 0f;
            float startPitch = 0f;

            var spawnPoint = Object.FindFirstObjectByType<SpawnPoint>();
            if (spawnPoint != null)
            {
                startPos = spawnPoint.transform.position;
                Vector3 euler = spawnPoint.transform.eulerAngles;
                startYaw = euler.y;
                startPitch = euler.x;
                if (startPitch > 180f) startPitch -= 360f;
            }

            em.SetComponentData(e, LocalTransform.FromPositionRotation(
                startPos,
                quaternion.Euler(math.radians(startPitch), math.radians(startYaw), 0f)));

            em.SetComponentData(e, new FlyingCameraSettings
            {
                MoveSpeed = 50f,
                MouseSensitivity = 2f,
                Yaw = startYaw,
                Pitch = startPitch,
            });

            var sphereCollider = SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = 0.5f
            });
            em.SetComponentData(e, new PhysicsCollider { Value = sphereCollider });
            em.SetComponentData(e, PhysicsMass.CreateDynamic(sphereCollider.Value.MassProperties, 1f));
            em.SetComponentData(e, new PhysicsVelocity());
            em.SetComponentData(e, new PhysicsDamping { Linear = 5f, Angular = 5f });
            em.SetComponentData(e, new PhysicsGravityFactor { Value = 0f });
            em.SetSharedComponent(e, new PhysicsWorldIndex { Value = 0 });

            spawned = true;
            Debug.Log($"[GameMode] Flying camera spawned at {startPos}");
        }
    }
}
