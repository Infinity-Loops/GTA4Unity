using System.Collections.Generic;
using System.IO;
using IVUnity.ECS.GameMode;
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
    public struct PedMoveBlend : IComponentData
    {
        public float DesiredSpeed;    // 0=idle, 1=walk, 2=run, 3=sprint
        public float DirectionAngle;  // degrees relative to facing: 0=fwd, 90=right, 180=back, -90=left
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

        public bool IsConfigured => _clips != null && _boneEntities != null;
        public Vector3[] LastRageWorldPos { get; private set; }
        public Quaternion[] LastRageWorldRot { get; private set; }
        public float3[] RageRestPositions => _rageRestPos;

        public int GetBoneIndexByBoneId(int boneId)
        {
            if (_boneIdToEntityIndex != null && _boneIdToEntityIndex.TryGetValue((ushort)boneId, out int idx))
                return idx;
            return -1;
        }

        private SkeletonDebugGizmo _debugGizmo;
        private PedTwistSolver _twistSolver;
        private PedMoveBlendTree _blendTree;
        private float[] _clipTimes;

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
            _clipTimes = new float[clips.Length];

            _boneIdToEntityIndex = new Dictionary<ushort, int>();
            for (int i = 0; i < boneIds.Length; i++)
                _boneIdToEntityIndex[boneIds[i]] = i;

            _twistSolver = new PedTwistSolver();
            _twistSolver.Configure(_boneIdToEntityIndex, rageRestRot);

            _blendTree = new PedMoveBlendTree();
            _blendTree.Configure(clipsByName);

            var go = new UnityEngine.GameObject("SkeletonDebug");
            _debugGizmo = go.AddComponent<SkeletonDebugGizmo>();
            _debugGizmo.ParentIndices = parentIndices;
            _debugGizmo.BonePositions = new UnityEngine.Vector3[_boneCount];

            Debug.Log($"[PedAnim] Configured: {clips.Length} clips, {_boneIdToEntityIndex.Count} bone mappings");
        }

        public int GetClipIndex(string name)
        {
            if (_clipsByName != null && _clipsByName.TryGetValue(name, out int idx))
                return idx;
            return -1;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<PedMoveBlend>();
        }

        protected override void OnUpdate()
        {
            if (_clips == null || _boneEntities == null) return;

            var query = SystemAPI.QueryBuilder().WithAllRW<PedMoveBlend>().Build();
            if (query.IsEmpty) return;

            var entity = query.GetSingletonEntity();
            var blend = EntityManager.GetComponentData<PedMoveBlend>(entity);

            float dt = SystemAPI.Time.DeltaTime;

            _blendTree.Update(blend.DesiredSpeed, blend.DirectionAngle, 0f, dt);

            // Advance all clip times
            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] != null && _clips[i].Duration > 0)
                {
                    _clipTimes[i] += dt;
                    _clipTimes[i] %= _clips[i].Duration;
                }
            }

            if (_blendTree.OutputCount == 0) return;

            // Initialize local transforms to rest
            var rageLocalPos = new UnityEngine.Vector3[_boneCount];
            var rageLocalRot = new UnityEngine.Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                rageLocalPos[i] = new UnityEngine.Vector3(_rageRestPos[i].x, _rageRestPos[i].y, _rageRestPos[i].z);
                rageLocalRot[i] = new UnityEngine.Quaternion(_rageRestRot[i].value.x, _rageRestRot[i].value.y, _rageRestRot[i].value.z, _rageRestRot[i].value.w);
            }

            // Blend active clips from the tree
            float totalWeight = 0f;
            for (int li = 0; li < _blendTree.OutputCount; li++)
            {
                var entry = _blendTree.GetOutput(li);
                var clip = _clips[entry.ClipIndex];
                if (clip == null || clip.FrameCount <= 1) continue;

                SampleFrame(clip, _clipTimes[entry.ClipIndex], out int f0, out int f1, out float frac);
                float w = entry.Weight;
                totalWeight += w;
                float blendT = totalWeight > 0 ? w / totalWeight : 1f;

                bool isIdleClip = entry.ClipIndex == _blendTree.IdleClipIndex;

                foreach (var bt in clip.BoneTracks)
                {
                    if (!_boneIdToEntityIndex.TryGetValue(bt.BoneId, out int bi)) continue;
                    if (bi >= _boneCount) continue;

                    quaternion q0 = bt.Rotations[f0];
                    quaternion q1 = bt.Rotations[f1];
                    if (math.dot(q0, q1) < 0) q1.value = -q1.value;
                    quaternion rageQ = math.slerp(q0, q1, frac);

                    // Correct idle clip's bone 0 orientation to match locomotion clips
                    if (bi == 0 && isIdleClip)
                        rageQ = math.mul(quaternion.AxisAngle(new float3(0, 0, 1), math.radians(_blendTree.IdleFacingOffset)), rageQ);

                    var q = new UnityEngine.Quaternion(rageQ.value.x, rageQ.value.y, rageQ.value.z, rageQ.value.w);

                    if (UnityEngine.Quaternion.Dot(rageLocalRot[bi], q) < 0)
                        q = new UnityEngine.Quaternion(-q.x, -q.y, -q.z, -q.w);

                    rageLocalRot[bi] = UnityEngine.Quaternion.Slerp(rageLocalRot[bi], q, blendT);
                }

                if (clip.MoverPositions != null && clip.MoverPositions.Length > 0)
                {
                    float3 p0 = clip.MoverPositions[f0];
                    float3 p1 = clip.MoverPositions[math.min(f1, clip.MoverPositions.Length - 1)];
                    float3 moverPos = math.lerp(p0, p1, frac);
                    // Use relative Y (subtract frame 0 baseline) to prevent floating between clips
                    float3 baselinePos = clip.MoverPositions[0];
                    moverPos.y -= baselinePos.y;
                    var swayPos = new UnityEngine.Vector3(moverPos.x, moverPos.y, moverPos.z);
                    var restPos = new UnityEngine.Vector3(_rageRestPos[0].x, _rageRestPos[0].y, _rageRestPos[0].z);
                    rageLocalPos[0] = UnityEngine.Vector3.Lerp(rageLocalPos[0], restPos + swayPos, blendT);
                }
            }


            // Compose RAGE world transforms
            var rageWorldPos = new Vector3[_boneCount];
            var rageWorldRot = new Quaternion[_boneCount];
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

            _twistSolver.Solve(rageLocalRot, rageWorldPos, rageWorldRot, _parentIndices);


            var skinMatrices = EntityManager.GetBuffer<Unity.Deformations.SkinMatrix>(entity);
            var bindPoses = EntityManager.GetBuffer<SkinnedMeshBindPose>(entity);

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
            }

            LastRageWorldPos = rageWorldPos;
            LastRageWorldRot = rageWorldRot;

            if (_debugGizmo != null)
            {
                var rootRef = EntityManager.GetComponentData<SkinnedMeshRootEntity>(entity);
                if (EntityManager.HasComponent<LocalToWorld>(rootRef.Value))
                {
                    var ltw = EntityManager.GetComponentData<LocalToWorld>(rootRef.Value);
                    _debugGizmo.Offset = ltw.Position;
                    _debugGizmo.Rotation = ltw.Rotation;
                }
                for (int i = 0; i < _boneCount; i++)
                    _debugGizmo.BonePositions[i] = RageCoordinates.Position(rageWorldPos[i]);
            }
        }

        static void SampleFrame(AnimationClip clip, float time, out int f0, out int f1, out float frac)
        {
            float frameFloat = time * clip.FrameRate;
            f0 = (int)frameFloat;
            frac = frameFloat - f0;
            f1 = f0 + 1;
            if (f0 >= clip.FrameCount - 1)
            {
                f0 = clip.FrameCount - 2;
                f1 = clip.FrameCount - 1;
                frac = 1f;
            }
            if (f0 < 0) { f0 = 0; f1 = 0; frac = 0; }
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
