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

    public struct PedMoveBlend : IComponentData
    {
        public float DesiredSpeed;
        public float IdleTime;
        public float WalkTime;
        public float RunTime;
        public float SprintTime;
        public int IdleClip;
        public int WalkClip;
        public int RunClip;
        public int SprintClip;
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
        private HashSet<int> _bonesWithTracks;
        private PedTwistSolver _twistSolver;

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

            // Collect all bone indices that have at least one track across all clips
            _bonesWithTracks = new HashSet<int>();
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                foreach (var bt in clip.BoneTracks)
                {
                    if (_boneIdToEntityIndex.TryGetValue(bt.BoneId, out int bi))
                        _bonesWithTracks.Add(bi);
                }
            }

            _twistSolver = new PedTwistSolver();
            _twistSolver.Configure(_boneIdToEntityIndex, rageRestRot);

            var go = new UnityEngine.GameObject("SkeletonDebug");
            _debugGizmo = go.AddComponent<SkeletonDebugGizmo>();
            _debugGizmo.ParentIndices = parentIndices;
            _debugGizmo.BonePositions = new UnityEngine.Vector3[_boneCount];

            Debug.Log($"[PedAnim] Configured: {clips.Length} clips, {_boneIdToEntityIndex.Count} bone mappings, {_bonesWithTracks.Count} bones with tracks");
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

            float speed = math.clamp(blend.DesiredSpeed, 0f, 4f);
            ComputeBlendWeights(speed, out float wIdle, out float wWalk, out float wRun, out float wSprint);

            var layers = new BlendLayer[4];
            int layerCount = 0;
            if (wIdle > 0.001f && blend.IdleClip >= 0)
                layers[layerCount++] = new BlendLayer { ClipIndex = blend.IdleClip, Weight = wIdle, Time = blend.IdleTime };
            if (wWalk > 0.001f && blend.WalkClip >= 0)
                layers[layerCount++] = new BlendLayer { ClipIndex = blend.WalkClip, Weight = wWalk, Time = blend.WalkTime };
            if (wRun > 0.001f && blend.RunClip >= 0)
                layers[layerCount++] = new BlendLayer { ClipIndex = blend.RunClip, Weight = wRun, Time = blend.RunTime };
            if (wSprint > 0.001f && blend.SprintClip >= 0)
                layers[layerCount++] = new BlendLayer { ClipIndex = blend.SprintClip, Weight = wSprint, Time = blend.SprintTime };

            if (layerCount == 0) return;

            var rageLocalPos = new UnityEngine.Vector3[_boneCount];
            var rageLocalRot = new UnityEngine.Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                rageLocalPos[i] = new UnityEngine.Vector3(_rageRestPos[i].x, _rageRestPos[i].y, _rageRestPos[i].z);
                rageLocalRot[i] = new UnityEngine.Quaternion(_rageRestRot[i].value.x, _rageRestRot[i].value.y, _rageRestRot[i].value.z, _rageRestRot[i].value.w);
            }

            float totalWeight = 0f;

            for (int li = 0; li < layerCount; li++)
            {
                var layer = layers[li];
                var clip = _clips[layer.ClipIndex];
                if (clip == null || clip.FrameCount <= 1) continue;

                SampleFrame(clip, layer.Time, out int f0, out int f1, out float frac);
                float w = layer.Weight;
                totalWeight += w;
                float blendT = totalWeight > 0 ? w / totalWeight : 1f;

                foreach (var bt in clip.BoneTracks)
                {
                    if (bt.BoneId == 0) continue;
                    if (!_boneIdToEntityIndex.TryGetValue(bt.BoneId, out int bi)) continue;
                    if (bi >= _boneCount) continue;

                    quaternion q0 = bt.Rotations[f0];
                    quaternion q1 = bt.Rotations[f1];
                    if (math.dot(q0, q1) < 0) q1.value = -q1.value;
                    quaternion rageQ = math.slerp(q0, q1, frac);

                    var q = new UnityEngine.Quaternion(rageQ.value.x, rageQ.value.y, rageQ.value.z, rageQ.value.w);

                    if (UnityEngine.Quaternion.Dot(rageLocalRot[bi], q) < 0)
                        q = new UnityEngine.Quaternion(-q.x, -q.y, -q.z, -q.w);

                    rageLocalRot[bi] = UnityEngine.Quaternion.Slerp(rageLocalRot[bi], q, blendT);
                }

                // Apply bone 0 position from mover track (hip sway in RAGE space)
                if (clip.MoverPositions != null && clip.MoverPositions.Length > 0)
                {
                    float3 p0 = clip.MoverPositions[f0];
                    float3 p1 = clip.MoverPositions[math.min(f1, clip.MoverPositions.Length - 1)];
                    float3 moverPos = math.lerp(p0, p1, frac);
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

            // Advance clip times
            if (blend.IdleClip >= 0 && _clips[blend.IdleClip] != null)
                blend.IdleTime = AdvanceClipTime(blend.IdleTime, _clips[blend.IdleClip], dt);
            if (blend.WalkClip >= 0 && _clips[blend.WalkClip] != null)
                blend.WalkTime = AdvanceClipTime(blend.WalkTime, _clips[blend.WalkClip], dt);
            if (blend.RunClip >= 0 && _clips[blend.RunClip] != null)
                blend.RunTime = AdvanceClipTime(blend.RunTime, _clips[blend.RunClip], dt);
            if (blend.SprintClip >= 0 && _clips[blend.SprintClip] != null)
                blend.SprintTime = AdvanceClipTime(blend.SprintTime, _clips[blend.SprintClip], dt);

            LastRageWorldPos = rageWorldPos;
            LastRageWorldRot = rageWorldRot;

            EntityManager.SetComponentData(entity, blend);

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
                    _debugGizmo.BonePositions[i] = RageCoordinates.Position(rageWorldPos[i]);
            }
        }

        struct BlendLayer
        {
            public int ClipIndex;
            public float Weight;
            public float Time;
        }

        static void ComputeBlendWeights(float speed, out float idle, out float walk, out float run, out float sprint)
        {
            if (speed < 1f)
            {
                idle = 1f - speed;
                walk = speed;
                run = 0f;
                sprint = 0f;
            }
            else if (speed < 2f)
            {
                float t = speed - 1f;
                idle = 0f;
                walk = 1f - t;
                run = t;
                sprint = 0f;
            }
            else if (speed < 3f)
            {
                float t = speed - 2f;
                idle = 0f;
                walk = 0f;
                run = 1f - t;
                sprint = t;
            }
            else
            {
                idle = 0f;
                walk = 0f;
                run = 0f;
                sprint = 1f;
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

        static float AdvanceClipTime(float time, AnimationClip clip, float dt)
        {
            time += dt;
            if (clip.Duration > 0) time %= clip.Duration;
            return time;
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
