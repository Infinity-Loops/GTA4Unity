using Unity.Mathematics;

namespace IVUnity.ECS.Ped.Legs
{
    /// <summary>
    /// Per-leg runtime state for the procedural legs system.
    /// </summary>
    public struct PedLegData
    {
        // Bone indices into the skeleton
        public int ThighIndex;
        public int KneeIndex;
        public int AnkleIndex;

        // Lengths (computed at init from rest pose)
        public float UpperLength;
        public float LowerLength;

        // IK state
        public float3 IKTargetPos;
        public float3 PreviousIKPos;
        public quaternion IKTargetRot;

        // Raycast results
        public bool GroundHit;
        public float3 GroundPoint;
        public float3 GroundNormal;
        public float GroundDistance;

        // Glue state
        public bool IsGlued;
        public float3 GluePosition;
        public float GlueBlend;
        public float UnglueProgress;

        // Animation reference
        public float3 InitAnklePosRootSpace;
        public float3 AnimatedAnklePosWorld;

        // Hint direction (knee bend direction in world space)
        public float3 KneeHintWorld;
    }
}
