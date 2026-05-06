using Unity.Mathematics;

namespace IVUnity.ECS.Ped.Legs
{
    public struct PedLegData
    {
        public int ThighIndex;
        public int KneeIndex;
        public int AnkleIndex;
        public int ToeIndex;

        public float UpperLength;
        public float LowerLength;

        // Ground probe
        public bool GroundHit;
        public float3 GroundPoint;
        public float3 GroundNormal;
        public float GroundDistance;
        public float3 ProbePosition;
        public bool Supporting;

        public float3 AnimatedAnklePosWorld;
        public float3 PreviousIKPos;

        public float3 FootLocalUp;
        public float IKBlend;
    }
}
