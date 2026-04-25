using Unity.Entities;

namespace IVUnity.ECS
{
    /// <summary>
    /// Contains streaming-decision systems that run per frame during simulation.
    /// Order: FocusPointSyncSystem → CellActivationSystem → ModelLoadDispatchSystem.
    /// Asset systems live in WorldAssetSystemGroup instead.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class WorldStreamingSystemGroup : ComponentSystemGroup { }
}
