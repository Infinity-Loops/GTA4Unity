using System.Collections.Generic;
using RageLib.Common.ResourceTypes;           // SimpleArray<T>
using RageLib.Models;
using RageLib.Models.Resource;                // FragTypeModel
using RageLib.Models.Resource.Skeletons;      // Bone
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace IVUnity.ECS
{
    /// <summary>A flattened sub-mesh ready for Unity.Mesh/Material upload on the main thread.</summary>
    public struct FlatSubMesh
    {
        public MeshGeometry3D Geometry;   // global-namespace type from RageLib/Models/DrawableModel.cs
        public RageMaterial   Material;   // global-namespace type from RageLib/Models/DrawableModel.cs
        public LocalTransform LocalTransform;
    }

    /// <summary>
    /// Flattens a ModelNode tree into renderable leaves with correct per-leaf transforms.
    ///
    /// Regular DrawableModel: walk tree, emit leaves at identity LocalTransform.
    ///
    /// FragTypeModel (.wft):
    ///   - Parent drawable subtree (FragmentChildIndex == -1) emits at identity — it's
    ///     the fragment's base shape anchored at the fragment origin.
    ///   - Each fragment-piece subtree uses its FragTypeChild.BoneIndex to look up the
    ///     matching bone in the skeleton; the bone's Position + RotationQuaternion is
    ///     the piece's transform relative to the fragment origin.
    ///
    /// BoneIndex is parsed from byte offset 0x0e of the on-disk FragTypeChild header
    /// (see FragTypeModel.cs — determined from the GTA IV Ghidra decompilation).
    /// If BoneIndex is out of range or skeleton is missing, the piece falls back to
    /// identity so it's at least visible (stacked at fragment origin).
    /// </summary>
    public static class ModelFlatten
    {
        public static void Flatten(ModelNode root, List<FlatSubMesh> output)
        {
            if (root == null) return;

            if (root.DataModel is FragTypeModel fragModel && root.Children != null)
            {
                FlattenFragmentRoot(root, fragModel, output);
                return;
            }

            Walk(root, output, LocalTransform.Identity);
        }

        private static void FlattenFragmentRoot(
            ModelNode root,
            FragTypeModel fragModel,
            List<FlatSubMesh> output)
        {
            SimpleArray<Bone> bones = fragModel.Skeleton?.Bones;
            int boneCount = bones?.Count ?? 0;

            for (int i = 0; i < root.Children.Count; i++)
            {
                var child = root.Children[i];

                // Parent drawable (FragmentChildIndex == -1) and children with no FragTypeChild
                // reference render at identity.
                LocalTransform local = LocalTransform.Identity;

                if (child.FragmentChildIndex >= 0 && child.FragmentChild != null && boneCount > 0)
                {
                    int boneIndex = child.FragmentChild.BoneIndex;
                    if (boneIndex >= 0 && boneIndex < boneCount)
                    {
                        var bone = bones[boneIndex];

                        var absPos = bone.AbsolutePosition;
                        var p = RageCoordinates.Position(new UnityEngine.Vector3(absPos.X, absPos.Y, absPos.Z));
                        var pos = new float3(p.x, p.y, p.z);

                        var eulerRad = bone.AbsoluteRotationEuler;
                        var rageQuat = quaternion.EulerXYZ(eulerRad.X, eulerRad.Y, eulerRad.Z);
                        var r = RageCoordinates.RotationInternal(new Quaternion(rageQuat.value.x, rageQuat.value.y, rageQuat.value.z, rageQuat.value.w));
                        var rot = new quaternion(r.x, r.y, r.z, r.w);

                        local = LocalTransform.FromPositionRotation(pos, rot);
                    }
                }

                Walk(child, output, local);
            }
        }

        private static void Walk(ModelNode node, List<FlatSubMesh> output, LocalTransform parentTransform)
        {
            if (node == null) return;

            if (node.Model3D != null && node.Model3D.geometry != null)
            {
                output.Add(new FlatSubMesh
                {
                    Geometry = node.Model3D.geometry,
                    Material = node.Model3D.material,
                    LocalTransform = parentTransform,
                });
            }

            if (node.Children == null) return;
            for (int i = 0; i < node.Children.Count; i++)
            {
                Walk(node.Children[i], output, parentTransform);
            }
        }
    }
}
