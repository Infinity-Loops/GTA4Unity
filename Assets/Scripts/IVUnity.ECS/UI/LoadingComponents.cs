using Unity.Collections;
using Unity.Entities;

namespace IVUnity.ECS.UI
{
    /// <summary>
    /// Snapshot of current loading progress as an ECS singleton. Written once per frame
    /// on the main thread by LoadingBridgeSystem (which pulls from thread-safe LoadingState).
    /// Read on the main thread by LoadingViewSystem (which drives the LoadingScreenView).
    /// Decouples writers (any thread) from the renderer.
    /// </summary>
    public struct LoadingProgress : IComponentData
    {
        public int Current;
        public int Total;
        public FixedString128Bytes Label;
        public bool Finished;
    }
}
