using UnityEngine;

namespace IVUnity.Ped
{
    public class SMRGizmoUpdater : MonoBehaviour
    {
        public SkeletonDebugGizmo Gizmo;
        public Transform[] BoneTransforms;

        void LateUpdate()
        {
            if (Gizmo == null || BoneTransforms == null) return;
            for (int i = 0; i < BoneTransforms.Length && i < Gizmo.BonePositions.Length; i++)
            {
                if (BoneTransforms[i] != null)
                    Gizmo.BonePositions[i] = BoneTransforms[i].position;
            }
        }
    }
}
