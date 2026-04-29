using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace IVUnity.ECS.Physics
{
    /// <summary>
    /// Bridges ECS entities to PhysX GameObjects.
    /// Creates companion Rigidbody+Collider for entities with PhysicsProxy,
    /// syncs velocity/position each frame, cleans up on removal.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PhysicsProxySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Create proxy GameObjects for new entities
            Entities
                .WithAll<PhysicsProxy>()
                .WithNone<PhysicsProxyRef>()
                .WithoutBurst()
                .WithStructuralChanges()
                .ForEach((Entity entity, in PhysicsProxy proxy, in LocalTransform transform) =>
                {
                    var go = new GameObject($"PhysProxy_{entity.Index}");

                    AttachCollider(go, proxy, em.HasComponent<PhysicsProxyMesh>(entity)
                        ? em.GetComponentData<PhysicsProxyMesh>(entity) : null);

                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = proxy.Mass > 0 ? proxy.Mass : 1f;
                    rb.useGravity = proxy.UseGravity;
                    rb.isKinematic = proxy.IsKinematic;
                    rb.freezeRotation = true;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = proxy.IsKinematic
                        ? CollisionDetectionMode.Discrete
                        : CollisionDetectionMode.ContinuousDynamic;
                    rb.linearDamping = proxy.LinearDamping;

                    go.transform.position = new Vector3(
                        transform.Position.x, transform.Position.y, transform.Position.z);

                    em.AddComponentData(entity, new PhysicsProxyRef
                    {
                        GameObject = go,
                        Rigidbody = rb
                    });

                    if (!em.HasComponent<PhysicsInput>(entity))
                        em.AddComponentData(entity, new PhysicsInput());
                }).Run();

            // Sync: push velocity, pull position
            Entities
                .WithAll<PhysicsProxy>()
                .WithoutBurst()
                .ForEach((ref LocalTransform transform, in PhysicsInput input, in PhysicsProxyRef proxyRef) =>
                {
                    if (proxyRef.Rigidbody == null) return;

                    // If ECS entity moved (e.g. teleport/spawn), push to PhysX
                    var rbPos = proxyRef.Rigidbody.position;
                    var ecsPos = transform.Position;
                    float drift = math.lengthsq(new float3(rbPos.x, rbPos.y, rbPos.z) - ecsPos);
                    if (drift > 100f)
                    {
                        proxyRef.Rigidbody.position = new Vector3(ecsPos.x, ecsPos.y, ecsPos.z);
                        proxyRef.Rigidbody.linearVelocity = Vector3.zero;
                        return;
                    }

                    proxyRef.Rigidbody.linearVelocity = new Vector3(
                        input.Velocity.x, input.Velocity.y, input.Velocity.z);

                    transform.Position = new float3(rbPos.x, rbPos.y, rbPos.z);
                }).Run();

            // Cleanup
            Entities
                .WithAll<PhysicsProxyRef>()
                .WithNone<PhysicsProxy>()
                .WithoutBurst()
                .WithStructuralChanges()
                .ForEach((Entity entity, PhysicsProxyRef proxyRef) =>
                {
                    proxyRef.Dispose();
                    em.RemoveComponent<PhysicsProxyRef>(entity);
                }).Run();
        }

        private static void AttachCollider(GameObject go, PhysicsProxy proxy, PhysicsProxyMesh meshData)
        {
            switch (proxy.ColliderType)
            {
                case ProxyColliderType.Sphere:
                {
                    var col = go.AddComponent<SphereCollider>();
                    col.radius = proxy.Size.x > 0 ? proxy.Size.x : 0.5f;
                    break;
                }
                case ProxyColliderType.Capsule:
                {
                    var col = go.AddComponent<CapsuleCollider>();
                    col.radius = proxy.Size.x > 0 ? proxy.Size.x : 0.5f;
                    col.height = proxy.Size.y > 0 ? proxy.Size.y : 2f;
                    break;
                }
                case ProxyColliderType.Box:
                {
                    var col = go.AddComponent<BoxCollider>();
                    col.size = proxy.Size.x > 0
                        ? new Vector3(proxy.Size.x * 2, proxy.Size.y * 2, proxy.Size.z * 2)
                        : Vector3.one;
                    break;
                }
                case ProxyColliderType.Mesh:
                {
                    var col = go.AddComponent<MeshCollider>();
                    if (meshData != null)
                    {
                        col.sharedMesh = meshData.Mesh;
                        col.convex = meshData.Convex;
                    }
                    break;
                }
            }
        }
    }
}
