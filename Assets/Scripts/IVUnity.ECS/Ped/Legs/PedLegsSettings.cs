using System;
using Unity.Entities;
using Unity.Physics;

namespace IVUnity.ECS.Ped.Legs
{
    [Serializable]
    public struct PedLegsSettings : IComponentData
    {
        public float HipsHeightBlend;
        public float HipsHeightSpeed;
        public float HipsStabilityBlend;
        public float HipsStabilitySpeed;
        public float HipsStretchPreventer;
        public float FootAlignBlend;
        public float GlueBlend;
        public float GlueThreshold;
        public float GlueReleaseSpeed;
        public float RaycastDistance;
        public float RaycastOriginUp;

        public static PedLegsSettings Default => new()
        {
            HipsHeightBlend = 0f,
            HipsHeightSpeed = 0.7f,
            HipsStabilityBlend = 0f,
            HipsStabilitySpeed = 0.375f,
            HipsStretchPreventer = 0f,
            FootAlignBlend = 1f,
            GlueBlend = 0.8f,
            GlueThreshold = 0.15f,
            GlueReleaseSpeed = 4f,
            RaycastDistance = 1.5f,
            RaycastOriginUp = 0.5f,
        };
    }
}
