using Unity.Entities;

namespace IVUnity.ECS
{
    /// <summary>
    /// Singleton tag indicating the world has finished initial loading and gameplay
    /// systems may begin running. Published by GameCore.SetReady (called by
    /// InitialLoadGateSystem once enough world entities are loaded). Currently a
    /// pure marker — no behavior attached. Future gameplay systems can RequireForUpdate
    /// on it to defer execution until the world is usable.
    /// </summary>
    public struct GameReadyTag : IComponentData { }
}
