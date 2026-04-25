using Unity.Entities;
using UnityEngine;

namespace IVUnity.ECS.UI
{
    /// <summary>
    /// Reads the LoadingProgress ECS singleton each frame and asks LoadingScreenView to
    /// render it. Purely a data-to-view adapter — no direct UI calls in this class.
    /// Runs in PresentationSystemGroup so UI reflects the latest values from the same frame.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadingViewSystem : SystemBase
    {
        private LoadingScreenView cachedView;

        protected override void OnCreate()
        {
            RequireForUpdate<LoadingProgress>();
        }

        protected override void OnUpdate()
        {
            if (cachedView == null)
            {
                cachedView = LoadingScreenView.Instance;
                if (cachedView == null) cachedView = Object.FindFirstObjectByType<LoadingScreenView>();
                if (cachedView == null) return;
            }

            var p = SystemAPI.GetSingleton<LoadingProgress>();
            cachedView.Render(p.Current, p.Total, p.Label.ToString(), p.Finished);
        }
    }
}
