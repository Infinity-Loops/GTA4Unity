using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace IVUnity.ECS.Physics
{
    public enum ProxyColliderType : byte
    {
        Sphere = 0,
        Capsule = 1,
        Box = 2,
        Mesh = 3
    }

    /// <summary>
    /// Add to any ECS entity that needs PhysX collision.
    /// PhysicsProxySystem creates a companion GameObject with Rigidbody + Collider
    /// and keeps transforms in sync each frame.
    /// </summary>
    public struct PhysicsProxy : IComponentData
    {
        public ProxyColliderType ColliderType;
        public float3 Size;         // Sphere: x=radius, Capsule: x=radius y=height, Box: xyz=halfExtents
        public float Mass;
        public float LinearDamping;
        public bool UseGravity;
        public bool IsKinematic;
    }

    /// <summary>
    /// Optional: attach a pre-built Mesh for mesh collider proxies.
    /// </summary>
    public class PhysicsProxyMesh : IComponentData
    {
        public Mesh Mesh;
        public bool Convex;
    }

    /// <summary>
    /// Written by gameplay systems to drive the physics body.
    /// </summary>
    public struct PhysicsInput : IComponentData
    {
        public float3 Velocity;
    }

    /// <summary>
    /// Managed component holding the companion GameObject.
    /// Created and destroyed automatically by PhysicsProxySystem.
    /// </summary>
    public class PhysicsProxyRef : IComponentData, System.IDisposable
    {
        public GameObject GameObject;
        public Rigidbody Rigidbody;

        public void Dispose()
        {
            if (GameObject != null)
            {
                Object.Destroy(GameObject);
                GameObject = null;
                Rigidbody = null;
            }
        }
    }
}
