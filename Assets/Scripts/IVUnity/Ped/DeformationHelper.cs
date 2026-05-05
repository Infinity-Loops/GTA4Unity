using System;
using System.Reflection;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace IVUnity.Ped
{
    public static class DeformationHelper
    {
        private static Type _tDeformedEntity;
        private static ComponentType _deformedEntityComponentType;
        private static FieldInfo _deformedEntityValueField;
        private static MethodInfo _setComponentGeneric;
        private static bool _resolved;

        static void Resolve()
        {
            if (_resolved) return;

            var asm = typeof(Unity.Rendering.EntitiesGraphicsSystem).Assembly;
            _tDeformedEntity = asm.GetType("Unity.Rendering.DeformedEntity");
            _deformedEntityComponentType = ComponentType.ReadWrite(_tDeformedEntity);
            _deformedEntityValueField = _tDeformedEntity.GetField("Value");

            foreach (var m in typeof(EntityManager).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name == "SetComponentData" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2)
                {
                    _setComponentGeneric = m;
                    break;
                }
            }

            _resolved = true;
        }

        public static void SetupDeformedEntity(EntityManager em, Entity deformedEntity, int boneCount)
        {
            var skinMatrices = em.AddBuffer<SkinMatrix>(deformedEntity);
            skinMatrices.ResizeUninitialized(boneCount);
            for (int i = 0; i < boneCount; i++)
            {
                skinMatrices[i] = new SkinMatrix
                {
                    Value = new float3x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0)
                };
            }
        }

        /// <summary>
        /// Adds DeformedEntity to the render entity pointing to the deformed entity (which holds SkinMatrix).
        /// PushMeshDataSystem will auto-add SharedMeshTracker, DeformedMeshIndex, SkinMatrixBufferIndex
        /// if the mesh has valid Position/Normal/Tangent layout and bone weights.
        /// </summary>
        public static void SetupRenderEntity(EntityManager em, Entity renderEntity, Entity deformedEntity)
        {
            Resolve();

            try
            {
                em.AddComponent(renderEntity, _deformedEntityComponentType);

                var instance = Activator.CreateInstance(_tDeformedEntity);
                _deformedEntityValueField.SetValue(instance, deformedEntity);

                var closed = _setComponentGeneric.MakeGenericMethod(_tDeformedEntity);
                closed.Invoke(em, new object[] { renderEntity, instance });

                UnityEngine.Debug.Log($"[Deform] SetupRenderEntity OK: render={renderEntity}, deformed={deformedEntity}, type={_tDeformedEntity?.Name}, setMethod={_setComponentGeneric?.Name}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[Deform] SetupRenderEntity FAILED: {ex}");
            }
        }
    }
}
