using Unity.Entities;

namespace IVUnity.ECS
{
    /// <summary>
    /// Main-thread-bound asset operations (mesh upload, entity promotion, cache eviction).
    /// Runs in InitializationSystemGroup so it executes before Transform/rendering in the
    /// frame that the upload happens, making new sub-mesh entities visible the same frame.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class WorldAssetSystemGroup : ComponentSystemGroup { }
}
