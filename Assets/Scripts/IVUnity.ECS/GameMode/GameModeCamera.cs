using Unity.Entities;
using UnityEngine;

namespace IVUnity.ECS.GameMode
{
    /// <summary>
    /// Persistent game camera. Reads CameraTarget from the possessed entity.
    /// Each controller type writes CameraTarget differently:
    ///   - FlyingCamera: 1:1 with entity position
    ///   - Future player: third person offset, orbit, etc.
    /// </summary>
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

            var entities = cameraQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var target = world.EntityManager.GetComponentData<CameraTarget>(entities[0]);
            entities.Dispose();

            transform.position = target.Position;
            transform.rotation = target.Rotation;
        }
    }
}
