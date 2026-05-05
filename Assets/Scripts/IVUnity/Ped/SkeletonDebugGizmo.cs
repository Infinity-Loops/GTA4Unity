using UnityEngine;

namespace IVUnity.Ped
{
    public class SkeletonDebugGizmo : MonoBehaviour
    {
        public Vector3[] BonePositions;
        public int[] ParentIndices;
        public Vector3 Offset;
        public Quaternion Rotation = Quaternion.identity;

        private void OnDrawGizmos()
        {
            if (BonePositions == null || ParentIndices == null) return;

            for (int i = 0; i < BonePositions.Length && i < ParentIndices.Length; i++)
            {
                var pos = Rotation * BonePositions[i] + Offset;
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(pos, 0.01f);

                int pi = ParentIndices[i];
                if (pi >= 0 && pi < BonePositions.Length)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(pos, Rotation * BonePositions[pi] + Offset);
                }
            }
        }
    }
}
