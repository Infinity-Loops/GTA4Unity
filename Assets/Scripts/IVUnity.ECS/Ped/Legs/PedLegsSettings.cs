using System;
using Unity.Entities;

namespace IVUnity.ECS.Ped.Legs
{
    [Serializable]
    public struct PedLegsSettings : IComponentData
    {
        public float HipsHeightSpeed;
        public float FootAlignBlend;
        public float RaycastDistance;
        public float RaycastOriginUp;
        public float GroundCastRadius;

        public static PedLegsSettings Default => new()
        {
            HipsHeightSpeed = 0.7f,
            FootAlignBlend = 1f,
            RaycastDistance = 1.5f,
            RaycastOriginUp = 0.5f,
            GroundCastRadius = 0.1f,
        };
    }
}
