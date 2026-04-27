using UnityEngine;

namespace IVUnity.ECS.GameMode
{
    public class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 2f);
            Gizmos.DrawRay(transform.position, transform.forward * 5f);
        }
    }
}
