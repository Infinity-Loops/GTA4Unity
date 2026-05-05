using System.Collections.Generic;
using IVUnity.ECS;
using RageLib.Animation;
using RageLib.Models.Resource.Skeletons;
using Unity.Mathematics;
using UnityEngine;
using AnimationClip = UnityEngine.AnimationClip;

namespace IVUnity.Ped
{
    public static class SkinnedMeshTest
    {
        public static GameObject Build(
            Skeleton skeleton,
            List<FlatSubMesh> submeshes,
            RageLib.Animation.AnimationClip rageClip)
        {
            int boneCount = skeleton.Bones.Count;

            // Create root GameObject
            var root = new GameObject("SkinnedMeshTest");

            var boneGOs = new Transform[boneCount];

            // Compose hierarchy in RAGE space (same as ECS gizmo — proven correct)
            var rageWorldPos = new Vector3[boneCount];
            var rageWorldRot = new Quaternion[boneCount];

            for (int i = 0; i < boneCount; i++)
            {
                var bone = skeleton.Bones[i];
                string boneName = !string.IsNullOrEmpty(bone.Name) ? bone.Name : $"bone_{bone.BoneID}";
                var go = new GameObject($"[{i}] {boneName} (id:{bone.BoneID})");
                boneGOs[i] = go.transform;

                var localPos = new Vector3(bone.Position.X, bone.Position.Y, bone.Position.Z);
                var rq = bone.RotationQuaternion;
                var localRot = new Quaternion(rq.X, rq.Y, rq.Z, rq.W);

                int parentIdx = skeleton.ParentIndices[i];
                if (parentIdx >= 0 && parentIdx < i)
                {
                    rageWorldPos[i] = rageWorldPos[parentIdx] + rageWorldRot[parentIdx] * localPos;
                    rageWorldRot[i] = rageWorldRot[parentIdx] * localRot;
                    go.transform.SetParent(boneGOs[parentIdx], false);
                }
                else
                {
                    rageWorldPos[i] = localPos;
                    rageWorldRot[i] = localRot;
                    go.transform.SetParent(root.transform, false);
                }

                // Convert RAGE world to Unity world and set on Transform
                var uWorldPos = RageCoordinates.Position(rageWorldPos[i]);
                var uWorldRot = RageCoordinates.RotationInternal(rageWorldRot[i]);
                go.transform.position = root.transform.position + uWorldPos;
                go.transform.rotation = uWorldRot;
            }

            // Build bind poses (inverse of bone world transforms relative to root)
            var bindposes = new Matrix4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
                bindposes[i] = boneGOs[i].worldToLocalMatrix * root.transform.localToWorldMatrix;

            // DEBUG: compare our bone rotation matrix vs skeleton's DefaultTransforms
            if (skeleton.DefaultTransforms != null && skeleton.DefaultTransforms.Count >= boneCount)
            {
                int mismatches = 0;
                for (int i = 0; i < System.Math.Min(boneCount, 10); i++)
                {
                    // Our bone's local rotation matrix (3x3 from Unity Transform)
                    var ourMat = Matrix4x4.Rotate(boneGOs[i].localRotation);

                    // Game's DefaultTransform (read as row-major Matrix44)
                    var dt = skeleton.DefaultTransforms[i];

                    // Convert game matrix from RAGE space to Unity space: P * M * P^T
                    // P maps RAGE(x,y,z)->Unity(-x,z,-y)
                    // P = [[-1,0,0],[0,0,1],[0,-1,0]]
                    var gm = new Matrix4x4();
                    gm.SetColumn(0, new Vector4(dt[0,0], dt[1,0], dt[2,0], 0));
                    gm.SetColumn(1, new Vector4(dt[0,1], dt[1,1], dt[2,1], 0));
                    gm.SetColumn(2, new Vector4(dt[0,2], dt[1,2], dt[2,2], 0));
                    gm.SetColumn(3, new Vector4(0, 0, 0, 1));
                    // P*M*P^T manually for 3x3:
                    // P swaps and negates rows/cols based on (-x,z,-y) mapping
                    var converted = new float[3,3];
                    int[] map = {0, 2, 1}; // RAGE x→0, y→2, z→1 in Unity
                    float[] sign = {-1, 1, -1}; // negate x and y(→z)
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                            converted[r, c] = sign[r] * sign[c] * gm[map[r], map[c]];

                    float diffConverted = 0;
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                            diffConverted += Mathf.Abs(ourMat[r, c] - converted[r, c]);

                    float diffRaw = 0;
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                            diffRaw += Mathf.Abs(ourMat[r, c] - gm[r, c]);

                    string match;
                    if (diffConverted < 0.1f) match = "CONVERTED_MATCH";
                    else if (diffRaw < 0.1f) match = "RAW_MATCH";
                    else match = $"MISMATCH(conv={diffConverted:F2},raw={diffRaw:F2})";

                    if (diffConverted > 0.1f && diffRaw > 0.1f) mismatches++;

                    var bone = skeleton.Bones[i];
                    Debug.Log($"[SMR RotCheck] bone[{i}] {bone.Name}: {match}" +
                        $"\n  Our:       [{ourMat[0,0]:F4},{ourMat[0,1]:F4},{ourMat[0,2]:F4}] [{ourMat[1,0]:F4},{ourMat[1,1]:F4},{ourMat[1,2]:F4}] [{ourMat[2,0]:F4},{ourMat[2,1]:F4},{ourMat[2,2]:F4}]" +
                        $"\n  Game(raw): [{gm[0,0]:F4},{gm[0,1]:F4},{gm[0,2]:F4}] [{gm[1,0]:F4},{gm[1,1]:F4},{gm[1,2]:F4}] [{gm[2,0]:F4},{gm[2,1]:F4},{gm[2,2]:F4}]" +
                        $"\n  Game(conv):[{converted[0,0]:F4},{converted[0,1]:F4},{converted[0,2]:F4}] [{converted[1,0]:F4},{converted[1,1]:F4},{converted[1,2]:F4}] [{converted[2,0]:F4},{converted[2,1]:F4},{converted[2,2]:F4}]");
                }
                Debug.Log($"[SMR RotCheck] {mismatches}/10 bones have rotation matrix mismatch");
            }

            // Add blend weight debug visualization
            root.AddComponent<BlendWeightDebug>();

            // Add gizmo to visualize bone hierarchy
            var gizmo = root.AddComponent<SkeletonDebugGizmo>();
            gizmo.BonePositions = new Vector3[boneCount];
            gizmo.ParentIndices = new int[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                gizmo.ParentIndices[i] = skeleton.ParentIndices[i];
                gizmo.BonePositions[i] = boneGOs[i].position;
            }
            // Live-update bone positions each frame
            var updater = root.AddComponent<SMRGizmoUpdater>();
            updater.Gizmo = gizmo;
            updater.BoneTransforms = boneGOs;

            // Create SkinnedMeshRenderer for each submesh
            foreach (var sub in submeshes)
            {
                var mesh = sub.Geometry.GetUnityMesh();
                if (mesh.boneWeights == null || mesh.boneWeights.Length == 0) continue;

                mesh.bindposes = bindposes;

                var meshGO = new GameObject("mesh");
                meshGO.transform.SetParent(root.transform, false);
                meshGO.transform.localPosition = Vector3.zero;

                var smr = meshGO.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = boneGOs;
                smr.rootBone = root.transform;

                if (sub.Material != null)
                {
                    var diffuse = sub.Material.mainTex?.GetUnityTexture();
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (diffuse != null) mat.mainTexture = diffuse;
                    smr.sharedMaterial = mat;
                }
            }

            // Build legacy AnimationClip if we have animation data
            if (false && rageClip != null && rageClip.BoneTracks.Length > 0)
            {
                var boneIdToIndex = new Dictionary<ushort, int>();
                for (int i = 0; i < boneCount; i++)
                    boneIdToIndex[(ushort)skeleton.Bones[i].BoneID] = i;

                var unityClip = new AnimationClip();
                unityClip.legacy = true;
                unityClip.wrapMode = WrapMode.Loop;

                float fps = rageClip.FrameCount > 1 ? (rageClip.FrameCount - 1) / rageClip.Duration : 30f;

                foreach (var bt in rageClip.BoneTracks)
                {
                    if (!boneIdToIndex.TryGetValue(bt.BoneId, out int boneIdx)) continue;
                    // Build hierarchical path from root to this bone
                    string path = GetBonePath(boneGOs[boneIdx], root.transform);

                    var curveX = new Keyframe[rageClip.FrameCount];
                    var curveY = new Keyframe[rageClip.FrameCount];
                    var curveZ = new Keyframe[rageClip.FrameCount];
                    var curveW = new Keyframe[rageClip.FrameCount];

                    for (int f = 0; f < rageClip.FrameCount; f++)
                    {
                        float time = f / fps;
                        var rageQ = bt.Rotations[f];
                        // Convert RAGE mathematical quat to Unity
                        var uRot = RageCoordinates.RotationInternal(
                            new Quaternion(rageQ.value.x, rageQ.value.y, rageQ.value.z, rageQ.value.w));

                        curveX[f] = new Keyframe(time, uRot.x);
                        curveY[f] = new Keyframe(time, uRot.y);
                        curveZ[f] = new Keyframe(time, uRot.z);
                        curveW[f] = new Keyframe(time, uRot.w);
                    }

                    unityClip.SetCurve(path, typeof(Transform), "localRotation.x", new UnityEngine.AnimationCurve(curveX));
                    unityClip.SetCurve(path, typeof(Transform), "localRotation.y", new UnityEngine.AnimationCurve(curveY));
                    unityClip.SetCurve(path, typeof(Transform), "localRotation.z", new UnityEngine.AnimationCurve(curveZ));
                    unityClip.SetCurve(path, typeof(Transform), "localRotation.w", new UnityEngine.AnimationCurve(curveW));
                }

                unityClip.EnsureQuaternionContinuity();

                var anim = root.AddComponent<Animation>();
                anim.AddClip(unityClip, "idle");
                anim.clip = unityClip;
                anim.Play("idle");
            }

            return root;
        }

        static string GetBonePath(Transform bone, Transform root)
        {
            var parts = new List<string>();
            var current = bone;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
