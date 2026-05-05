using System.Collections.Generic;
using System.IO;
using IVUnity.Ped;
using RageLib.Animation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using AnimationClip = RageLib.Animation.AnimationClip;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity.ECS.Ped
{
    public struct PedAnimState : IComponentData
    {
        public int CurrentClipIndex;
        public float Time;
        public float Speed;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class PedAnimationSystem : SystemBase
    {
        private AnimationClip[] _clips;
        private Dictionary<string, int> _clipsByName;
        private Dictionary<ushort, int> _boneIdToEntityIndex;
        private int[] _parentIndices;
        private float3[] _rageRestPos;
        private quaternion[] _rageRestRot;
        private int _boneCount;
        private Entity[] _boneEntities;
        private SkeletonDebugGizmo _debugGizmo;
        private int _debugFrame;

        public void Configure(AnimationClip[] clips, Dictionary<string, int> clipsByName,
            Entity[] boneEntities, ushort[] boneIds, int[] parentIndices,
            Unity.Transforms.LocalTransform[] restPose,
            float3[] rageRestPos, quaternion[] rageRestRot)
        {
            _clips = clips;
            _clipsByName = clipsByName;
            _boneCount = boneIds.Length;
            _parentIndices = parentIndices;
            _rageRestPos = rageRestPos;
            _rageRestRot = rageRestRot;
            _boneEntities = boneEntities;

            _boneIdToEntityIndex = new Dictionary<ushort, int>();
            for (int i = 0; i < boneIds.Length; i++)
                _boneIdToEntityIndex[boneIds[i]] = i;

            // Recompute bind poses using the same RAGE-space composition + Unity conversion
            // as the animation loop, to guarantee exact match at rest pose
            _bindPoseOverride = new float4x4[_boneCount];
            var rp = new UnityEngine.Vector3[_boneCount];
            var rr = new UnityEngine.Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                var lp = new UnityEngine.Vector3(rageRestPos[i].x, rageRestPos[i].y, rageRestPos[i].z);
                var lr = new UnityEngine.Quaternion(rageRestRot[i].value.x, rageRestRot[i].value.y, rageRestRot[i].value.z, rageRestRot[i].value.w);
                int pi = parentIndices[i];
                if (pi >= 0 && pi < i)
                {
                    rp[i] = rp[pi] + rr[pi] * lp;
                    rr[i] = rr[pi] * lr;
                }
                else
                {
                    rp[i] = lp;
                    rr[i] = lr;
                }
                var uPos = RageCoordinates.Position(rp[i]);
                var uRot = RageCoordinates.RotationInternal(rr[i]);
                float4x4 worldMat = float4x4.TRS(
                    new float3(uPos.x, uPos.y, uPos.z),
                    new quaternion(uRot.x, uRot.y, uRot.z, uRot.w), 1f);
                _bindPoseOverride[i] = math.inverse(worldMat);
            }

            var go = new UnityEngine.GameObject("SkeletonDebug");
            _debugGizmo = go.AddComponent<SkeletonDebugGizmo>();
            _debugGizmo.ParentIndices = parentIndices;
            _debugGizmo.BonePositions = new UnityEngine.Vector3[_boneCount];

            Debug.Log($"[PedAnim] Configured: {clips.Length} clips, {_boneIdToEntityIndex.Count} bone mappings");
        }

        private float4x4[] _bindPoseOverride;

        public int GetClipIndex(string name)
        {
            if (_clipsByName != null && _clipsByName.TryGetValue(name, out int idx))
                return idx;
            return -1;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<PedAnimState>();
        }

        private bool _loggedOnce;

        protected override void OnUpdate()
        {
            if (_clips == null || _boneEntities == null) return;

            var query = SystemAPI.QueryBuilder().WithAllRW<PedAnimState>().Build();
            if (query.IsEmpty) return;

            var entity = query.GetSingletonEntity();
            var state = EntityManager.GetComponentData<PedAnimState>(entity);

            if (state.CurrentClipIndex < 0 || state.CurrentClipIndex >= _clips.Length) return;
            var clip = _clips[state.CurrentClipIndex];
            if (clip == null || clip.FrameCount <= 1) return;

            if (!_loggedOnce)
            {
                _loggedOnce = true;
                int matched = 0;
                foreach (var bt in clip.BoneTracks)
                    if (_boneIdToEntityIndex.ContainsKey(bt.BoneId)) matched++;
                Debug.Log($"[PedAnim] Playing clip '{clip.Name}' idx={state.CurrentClipIndex} frames={clip.FrameCount} boneTracks={clip.BoneTracks.Length} matched={matched}/{_boneIdToEntityIndex.Count}");

                // Log rest vs animation for first few animated bones
                foreach (var bt in clip.BoneTracks)
                {
                    if (!_boneIdToEntityIndex.TryGetValue(bt.BoneId, out int bi)) continue;
                    if (bi >= 5) continue;
                    var restRage = _rageRestRot[bi];
                    var animRage = bt.Rotations[0];
                    var restU = new quaternion(restRage.value.x, -restRage.value.z, restRage.value.y, restRage.value.w);
                    var animU = new quaternion(animRage.value.x, -animRage.value.z, animRage.value.y, animRage.value.w);
                    Debug.Log($"[PedAnim] bone[{bi}] id={bt.BoneId} restRAGE=({restRage.value.x:F4},{restRage.value.y:F4},{restRage.value.z:F4},{restRage.value.w:F4}) animRAGE=({animRage.value.x:F4},{animRage.value.y:F4},{animRage.value.z:F4},{animRage.value.w:F4}) restUnity=({restU.value.x:F4},{restU.value.y:F4},{restU.value.z:F4},{restU.value.w:F4}) animUnity=({animU.value.x:F4},{animU.value.y:F4},{animU.value.z:F4},{animU.value.w:F4})");
                }
            }

            float dt = SystemAPI.Time.DeltaTime;
            state.Time += dt * state.Speed;
            if (clip.Duration > 0)
                state.Time %= clip.Duration;

            float frameFloat = state.Time * clip.FrameRate;
            int frame0 = (int)frameFloat;
            float frac = frameFloat - frame0;
            int frame1 = frame0 + 1;
            if (frame0 >= clip.FrameCount - 1)
            {
                frame0 = clip.FrameCount - 2;
                frame1 = clip.FrameCount - 1;
                frac = 1f;
            }
            if (frame0 < 0) { frame0 = 0; frame1 = 0; frac = 0; }

            var skinMatrices = EntityManager.GetBuffer<Unity.Deformations.SkinMatrix>(entity);
            var bindPoses = EntityManager.GetBuffer<SkinnedMeshBindPose>(entity);

            // Compose in RAGE space using UnityEngine.Quaternion (same as ModelFlatten)
            var rageLocalPos = new UnityEngine.Vector3[_boneCount];
            var rageLocalRot = new UnityEngine.Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                rageLocalPos[i] = new UnityEngine.Vector3(_rageRestPos[i].x, _rageRestPos[i].y, _rageRestPos[i].z);
                rageLocalRot[i] = new UnityEngine.Quaternion(_rageRestRot[i].value.x, _rageRestRot[i].value.y, _rageRestRot[i].value.z, _rageRestRot[i].value.w);
            }

            foreach (var boneTrack in clip.BoneTracks)
            {
                if (!_boneIdToEntityIndex.TryGetValue(boneTrack.BoneId, out int boneIdx))
                    continue;
                if (boneIdx >= _boneCount) continue;
                // Bone 0 is "*skeleton" root node — a coordinate frame, not a real bone.
                // Its rest rotation differs from animation by 90deg, causing facing issues.
                if (boneTrack.BoneId == 0) continue;

                quaternion q0 = boneTrack.Rotations[frame0];
                quaternion q1 = boneTrack.Rotations[frame1];
                quaternion rageQ = math.slerp(q0, q1, frac);
                rageLocalRot[boneIdx] = new UnityEngine.Quaternion(rageQ.value.x, rageQ.value.y, rageQ.value.z, rageQ.value.w);
            }

            // Compose RAGE world transforms
            var rageWorldPos = new UnityEngine.Vector3[_boneCount];
            var rageWorldRot = new UnityEngine.Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                int pi = _parentIndices[i];
                if (pi >= 0 && pi < i)
                {
                    rageWorldPos[i] = rageWorldPos[pi] + rageWorldRot[pi] * rageLocalPos[i];
                    rageWorldRot[i] = rageWorldRot[pi] * rageLocalRot[i];
                }
                else
                {
                    rageWorldPos[i] = rageLocalPos[i];
                    rageWorldRot[i] = rageLocalRot[i];
                }
            }

            // Convert to Unity space using RageCoordinates, compute skin matrices
            _debugFrame++;
            for (int i = 0; i < _boneCount && i < skinMatrices.Length && i < bindPoses.Length; i++)
            {
                var uPos = RageCoordinates.Position(rageWorldPos[i]);
                var uRot = RageCoordinates.RotationInternal(rageWorldRot[i]);
                float4x4 worldMat = float4x4.TRS(
                    new float3(uPos.x, uPos.y, uPos.z),
                    new quaternion(uRot.x, uRot.y, uRot.z, uRot.w), 1f);

                float4x4 skinMat = math.mul(worldMat, bindPoses[i].Value);
                skinMatrices[i] = new Unity.Deformations.SkinMatrix
                {
                    Value = new float3x4(skinMat.c0.xyz, skinMat.c1.xyz, skinMat.c2.xyz, skinMat.c3.xyz)
                };

                if (_debugFrame == 2 && i < 5)
                {
                    Debug.Log($"[SkinDbg] bone[{i}] skinMat diag=({skinMat.c0.x:F4},{skinMat.c1.y:F4},{skinMat.c2.z:F4}) trans=({skinMat.c3.x:F4},{skinMat.c3.y:F4},{skinMat.c3.z:F4})");
                }
            }

            // Update gizmo at ped world position
            if (_debugGizmo != null)
            {
                var rootLtw = EntityManager.GetComponentData<SkinnedMeshRootEntity>(entity);
                if (EntityManager.HasComponent<LocalToWorld>(rootLtw.Value))
                {
                    var ltw = EntityManager.GetComponentData<LocalToWorld>(rootLtw.Value);
                    _debugGizmo.Offset = ltw.Position;
                    _debugGizmo.Rotation = ltw.Rotation;
                }
                for (int i = 0; i < _boneCount; i++)
                {
                    var p = RageCoordinates.Position(rageWorldPos[i]);
                    _debugGizmo.BonePositions[i] = p;
                }
            }

            EntityManager.SetComponentData(entity, state);
        }

        /// <summary>
        /// Convert a matrix from RAGE coordinate space to Unity coordinate space.
        /// RAGE: right-handed Z-up. Unity: left-handed Y-up.
        /// Transform: P * M * P_inv where P maps RAGE(x,y,z)->Unity(-x,z,-y)
        /// </summary>
        static float4x4 CoordConvertMatrix(float4x4 m)
        {
            // P maps RAGE(x,y,z) -> Unity(-x,z,-y)
            // P = [[-1,0,0,0],[0,0,1,0],[0,-1,0,0],[0,0,0,1]]
            // Result = P * M * P^T  (P^T = P^-1 for this orthogonal P)
            // Derived column by column:
            return new float4x4(
                new float4( m.c0.x, -m.c0.z,  m.c0.y, 0),  // col 0
                new float4(-m.c2.x,  m.c2.z, -m.c2.y, 0),  // col 1
                new float4( m.c1.x, -m.c1.z,  m.c1.y, 0),  // col 2
                new float4(-m.c3.x,  m.c3.z, -m.c3.y, 1)   // col 3
            );
        }

        public static LoadResult LoadFromWad(Dictionary<string, File> gameFiles, string wadName)
        {
            string key = wadName.ToLower() + ".wad";
            if (!gameFiles.TryGetValue(key, out var wadFile))
            {
                Debug.LogWarning($"[PedAnim] WAD not found: {key}");
                return null;
            }

            AnimationDictionaryFile dictFile;
            try
            {
                dictFile = new AnimationDictionaryFile();
                using (var stream = new MemoryStream(wadFile.GetData()))
                    dictFile.Open(stream);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PedAnim] Failed to parse {key}: {ex.Message}");
                return null;
            }

            var entries = dictFile.File.Data.Entries;
            var clips = new AnimationClip[entries.Count];
            var clipsByName = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                var animData = entries[i];
                clips[i] = AnimationClip.FromAnimationData(animData);
                if (clips[i].Name != null)
                {
                    string shortName = clips[i].Name.Replace("pack:/", "").Replace(".anim", "");
                    clipsByName[shortName] = i;
                }
            }

            dictFile.Dispose();

            Debug.Log($"[PedAnim] Loaded {clips.Length} clips from {wadName}");
            return new LoadResult { Clips = clips, ClipsByName = clipsByName };
        }

        public class LoadResult
        {
            public AnimationClip[] Clips;
            public Dictionary<string, int> ClipsByName;
        }
    }
}
