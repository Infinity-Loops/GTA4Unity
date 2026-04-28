using Unity.Entities;
using UnityEngine;

namespace IVUnity.ECS.GameMode
{
    [RequireComponent(typeof(Camera))]
    public class GameModeCamera : MonoBehaviour
    {
        private EntityQuery cameraQuery;
        private World world;

        private void LateUpdate()
        {
            if (world == null || !world.IsCreated)
            {
                world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return;

                cameraQuery = world.EntityManager.CreateEntityQuery(
                    typeof(Possessed),
                    typeof(CameraTarget));
            }

            if (cameraQuery.IsEmpty) return;

            var target = cameraQuery.GetSingleton<CameraTarget>();
            transform.position = target.Position;
            transform.rotation = target.Rotation;
        }
    }
}
