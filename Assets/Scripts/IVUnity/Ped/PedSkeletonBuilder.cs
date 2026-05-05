using RageLib.Models.Resource.Skeletons;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace IVUnity.Ped
{
    public struct BoneTag : IComponentData { }

    public struct BoneIndex : IComponentData
    {
        public int Value;
    }

    public static class PedSkeletonBuilder
    {
        public struct SkeletonData
        {
            public Entity RootBone;
            public Entity[] BoneEntities;
            public float4x4[] InverseBindPoses;
            public ushort[] BoneIds;
            public int[] ParentIndices;
            public LocalTransform[] RestPose;
            public float3[] RageRestPositions;
            public quaternion[] RageRestRotations;
        }

        public static SkeletonData Build(EntityManager em, Entity meshParent, Skeleton resourceSkeleton)
        {
            int boneCount = resourceSkeleton.Bones.Count;
            var boneEntities = new Entity[boneCount];
            var localTransforms = new LocalTransform[boneCount];
            var parentIndices = new int[boneCount];

            for (int i = 0; i < boneCount; i++)
            {
                boneEntities[i] = em.CreateEntity(
                    typeof(LocalTransform),
                    typeof(LocalToWorld),
                    typeof(Parent),
                    typeof(BoneTag),
                    typeof(BoneIndex));

                var bone = resourceSkeleton.Bones[i];
                parentIndices[i] = resourceSkeleton.ParentIndices[i];

                float3 localPos = RageCoordinates.Position(bone.Position.X, bone.Position.Y, bone.Position.Z);
                var q = bone.RotationQuaternion;
                quaternion localRot = new quaternion(q.X, -q.Z, q.Y, q.W);
                localRot = math.normalizesafe(localRot, quaternion.identity);

                var lt = LocalTransform.FromPositionRotation(localPos, localRot);
                localTransforms[i] = lt;

                em.SetComponentData(boneEntities[i], lt);
                em.SetComponentData(boneEntities[i], new BoneIndex { Value = i });

                Entity parentEntity = parentIndices[i] >= 0 ? boneEntities[parentIndices[i]] : meshParent;
                em.SetComponentData(boneEntities[i], new Parent { Value = parentEntity });
            }

            // Compose bind poses by multiplying local transforms down the chain
            // This matches exactly what TransformSystemGroup produces for LocalToWorld
            var worldMatrices = new float4x4[boneCount];
            var inverseBindPoses = new float4x4[boneCount];

            for (int i = 0; i < boneCount; i++)
            {
                float4x4 localMat = float4x4.TRS(localTransforms[i].Position, localTransforms[i].Rotation, 1f);

                if (parentIndices[i] >= 0 && parentIndices[i] < i)
                    worldMatrices[i] = math.mul(worldMatrices[parentIndices[i]], localMat);
                else
                    worldMatrices[i] = localMat;

                inverseBindPoses[i] = math.inverse(worldMatrices[i]);
            }

            var boneIds = new ushort[boneCount];
            var ragePositions = new float3[boneCount];
            var rageRotations = new quaternion[boneCount];

            for (int i = 0; i < boneCount; i++)
            {
                var bone = resourceSkeleton.Bones[i];
                boneIds[i] = (ushort)bone.BoneID;
                ragePositions[i] = new float3(bone.Position.X, bone.Position.Y, bone.Position.Z);
                var rq = bone.RotationQuaternion;
                rageRotations[i] = new quaternion(rq.X, rq.Y, rq.Z, rq.W);
            }

            // Compose bind poses in RAGE space then convert to Unity
            // Skeleton RotationQuaternion is CONJUGATED relative to mathematical rotation
            // (confirmed: DefaultTransforms matrix = TRANSPOSE of quat-derived matrix for bone 0)
            // Use conjugate quaternion for bind pose: negate XYZ, then convert with RotationInternal
            // which is equivalent to using Rotation() (stored format conversion)
            var rageWP = new UnityEngine.Vector3[boneCount];
            var rageWR = new UnityEngine.Quaternion[boneCount];
            var gameInverseBindPoses = new float4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                var lp = new UnityEngine.Vector3(ragePositions[i].x, ragePositions[i].y, ragePositions[i].z);
                var rq = rageRotations[i];
                var lr = new UnityEngine.Quaternion(rq.value.x, rq.value.y, rq.value.z, rq.value.w);
                if (parentIndices[i] >= 0 && parentIndices[i] < i)
                {
                    rageWP[i] = rageWP[parentIndices[i]] + rageWR[parentIndices[i]] * lp;
                    rageWR[i] = rageWR[parentIndices[i]] * lr;
                }
                else
                {
                    rageWP[i] = lp;
                    rageWR[i] = lr;
                }
                var uPos = RageCoordinates.Position(rageWP[i]);
                var uRot = RageCoordinates.RotationInternal(rageWR[i]);
                var unityWorld = float4x4.TRS(
                    new float3(uPos.x, uPos.y, uPos.z),
                    new quaternion(uRot.x, uRot.y, uRot.z, uRot.w), 1f);
                gameInverseBindPoses[i] = math.inverse(unityWorld);
            }
            bool hasGameBindPoses = true;

            Debug.Log($"[PedSkeleton] Created {boneCount} bone entities");

            return new SkeletonData
            {
                RootBone = boneEntities[0],
                BoneEntities = boneEntities,
                InverseBindPoses = hasGameBindPoses ? gameInverseBindPoses : inverseBindPoses,
                BoneIds = boneIds,
                ParentIndices = parentIndices,
                RestPose = localTransforms,
                RageRestPositions = ragePositions,
                RageRestRotations = rageRotations,
            };
        }
    }
}
