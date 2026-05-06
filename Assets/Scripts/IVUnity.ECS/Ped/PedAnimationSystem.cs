using System.Collections.Generic;
using System.IO;
using IVUnity.ECS.GameMode;
using IVUnity.ECS.Ped.Legs;
using IVUnity.Ped;
using RageLib.Animation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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
        private PedLegsProcessor _legsProcessor;
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

            var legSetups = new List<PedLegSetup>();
            TryAddLeg(legSetups, (ushort)PedBoneId.L_Thigh, (ushort)PedBoneId.L_Calf, (ushort)PedBoneId.L_Foot, (ushort)PedBoneId.L_Toe0);
            TryAddLeg(legSetups, (ushort)PedBoneId.R_Thigh, (ushort)PedBoneId.R_Calf, (ushort)PedBoneId.R_Foot, (ushort)PedBoneId.R_Toe0);
            if (legSetups.Count > 0)
            {
                _legsProcessor = new PedLegsProcessor();
                // Compose RAGE hierarchy first, then convert to Unity —
                // rageRestPos/Rot are per-bone LOCAL transforms, not world
                var rageWP = new UnityEngine.Vector3[_boneCount];
                var rageWR = new UnityEngine.Quaternion[_boneCount];
                for (int i = 0; i < _boneCount; i++)
                {
                    var lp = new UnityEngine.Vector3(rageRestPos[i].x, rageRestPos[i].y, rageRestPos[i].z);
                    var rq = rageRestRot[i];
                    var lr = new UnityEngine.Quaternion(rq.value.x, rq.value.y, rq.value.z, rq.value.w);
                    int pi = parentIndices[i];
                    if (pi >= 0 && pi < i)
                    {
                        rageWP[i] = rageWP[pi] + rageWR[pi] * lp;
                        rageWR[i] = rageWR[pi] * lr;
                    }
                    else
                    {
                        rageWP[i] = lp;
                        rageWR[i] = lr;
                    }
                }
                var initWorldPos = new Vector3[_boneCount];
                var initWorldRot = new quaternion[_boneCount];
                for (int i = 0; i < _boneCount; i++)
                {
                    initWorldPos[i] = RageCoordinates.Position(rageWP[i]);
                    initWorldRot[i] = (quaternion)RageCoordinates.RotationInternal(rageWR[i]);
                }
                _legsProcessor.Initialize(initWorldPos, initWorldRot, parentIndices,
                    float3.zero, quaternion.identity, legSetups.ToArray());
                _legsProcessor.GroundFilter = new Unity.Physics.CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = PhysicsLayers.Environment,
                };
                Debug.Log($"[PedAnim] Legs processor initialized with {legSetups.Count} legs");
            }

            var go = new UnityEngine.GameObject("SkeletonDebug");
            _debugGizmo = go.AddComponent<SkeletonDebugGizmo>();
            _debugGizmo.ParentIndices = parentIndices;
            _debugGizmo.BonePositions = new UnityEngine.Vector3[_boneCount];

            // Log CalfRoll parent relationships for debugging
            if (_boneIdToEntityIndex.TryGetValue((ushort)PedBoneId.L_CalfRoll, out int lcrIdx))
                Debug.Log($"[PedAnim] L_CalfRoll idx={lcrIdx} parent={parentIndices[lcrIdx]}, L_Calf idx={(_boneIdToEntityIndex.TryGetValue((ushort)PedBoneId.L_Calf, out int lcIdx) ? lcIdx : -1)}");
            else
                Debug.Log("[PedAnim] L_CalfRoll not found in skeleton");

            if (_boneIdToEntityIndex.TryGetValue((ushort)PedBoneId.R_CalfRoll, out int rcrIdx))
                Debug.Log($"[PedAnim] R_CalfRoll idx={rcrIdx} parent={parentIndices[rcrIdx]}, R_Calf idx={(_boneIdToEntityIndex.TryGetValue((ushort)PedBoneId.R_Calf, out int rcIdx) ? rcIdx : -1)}");
            else
                Debug.Log("[PedAnim] R_CalfRoll not found in skeleton");

            Debug.Log($"[PedAnim] Configured: {clips.Length} clips, {_boneIdToEntityIndex.Count} bone mappings");
        }

        private void TryAddLeg(List<PedLegSetup> setups, ushort thighId, ushort calfId, ushort footId, ushort toeId)
        {
            if (_boneIdToEntityIndex.TryGetValue(thighId, out int thighIdx) &&
                _boneIdToEntityIndex.TryGetValue(calfId, out int calfIdx) &&
                _boneIdToEntityIndex.TryGetValue(footId, out int footIdx))
            {
                _boneIdToEntityIndex.TryGetValue(toeId, out int toeIdx);
                setups.Add(new PedLegSetup
                {
                    ThighBoneIndex = thighIdx,
                    KneeBoneIndex = calfIdx,
                    AnkleBoneIndex = footIdx,
                    ToeBoneIndex = toeIdx
                });
            }
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
            //RequireForUpdate<PhysicsWorldSingleton>();
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
                    // Subtract frame 0 vertical baseline (Z is up in RAGE space)
                    float3 baselinePos = clip.MoverPositions[0];
                    moverPos.z -= baselinePos.z;
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

            // Convert to Unity local space (relative to mesh root, no world transform yet)
            var unityLocalPos = new Vector3[_boneCount];
            var unityLocalRot = new Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                unityLocalPos[i] = RageCoordinates.Position(rageWorldPos[i]);
                unityLocalRot[i] = RageCoordinates.RotationInternal(rageWorldRot[i]);
            }

            // Get mesh root world transform
            var rootRef2 = EntityManager.GetComponentData<SkinnedMeshRootEntity>(entity);
            float3 rootWorldPos = float3.zero;
            quaternion rootWorldRot = quaternion.identity;
            if (EntityManager.HasComponent<LocalToWorld>(rootRef2.Value))
            {
                var ltw = EntityManager.GetComponentData<LocalToWorld>(rootRef2.Value);
                rootWorldPos = ltw.Position;
                rootWorldRot = ltw.Rotation;
            }

            // Transform to Unity world space
            var unityWorldPos = new Vector3[_boneCount];
            var unityWorldRot = new Quaternion[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                unityWorldPos[i] = (Vector3)rootWorldPos + (Quaternion)rootWorldRot * unityLocalPos[i];
                unityWorldRot[i] = (Quaternion)rootWorldRot * unityLocalRot[i];
            }

            // Legs IK — operates in Unity world space, results converted back to local for skin matrices
            if (_legsProcessor != null && _legsProcessor.IsInitialized)
            {
                bool hasPhysics = SystemAPI.HasSingleton<PhysicsWorldSingleton>();
                if (hasPhysics)
                {
                    var physWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                    var collWorld = physWorld.CollisionWorld;

                    if (EntityManager.HasComponent<PedLegsSettings>(entity))
                        _legsProcessor.Settings = EntityManager.GetComponentData<PedLegsSettings>(entity);

                    float isMoving = math.saturate(_blendTree.CurrentMBR);

                    _legsProcessor.Update(
                        unityWorldPos, unityWorldRot,
                        rootWorldPos, rootWorldRot,
                        dt, isMoving,
                        in collWorld);

                    // Recompose children of IK-modified bones (CalfRoll, Toe0, etc.)
                    // IK changed thigh/knee/ankle transforms but their children are stale
                    bool[] modified = new bool[_boneCount];
                    for (int li = 0; li < _legsProcessor.Legs.Length; li++)
                    {
                        var leg = _legsProcessor.Legs[li];
                        modified[leg.ThighIndex] = true;
                        modified[leg.KneeIndex] = true;
                        modified[leg.AnkleIndex] = true;
                    }
                    for (int i = 0; i < _boneCount; i++)
                    {
                        int pi = _parentIndices[i];
                        if (pi >= 0 && modified[pi] && !modified[i])
                        {
                            var childLocalPos = RageCoordinates.Position(rageLocalPos[i]);
                            var childLocalRot = RageCoordinates.RotationInternal(rageLocalRot[i]);
                            unityWorldPos[i] = unityWorldPos[pi] + (Quaternion)unityWorldRot[pi] * childLocalPos;
                            unityWorldRot[i] = (Quaternion)unityWorldRot[pi] * childLocalRot;
                            modified[i] = true;
                        }
                    }

                    // Convert everything back to model-space for skin matrices
                    var invRootRot = math.inverse(rootWorldRot);
                    for (int i = 0; i < _boneCount; i++)
                    {
                        unityLocalPos[i] = (Vector3)math.mul(invRootRot, (float3)unityWorldPos[i] - rootWorldPos);
                        unityLocalRot[i] = (Quaternion)math.mul(invRootRot, (quaternion)unityWorldRot[i]);
                    }
                }
            }

            // Compute skin matrices from Unity local-space (relative to mesh root)
            var skinMatrices = EntityManager.GetBuffer<Unity.Deformations.SkinMatrix>(entity);
            var bindPoses = EntityManager.GetBuffer<SkinnedMeshBindPose>(entity);

            for (int i = 0; i < _boneCount && i < skinMatrices.Length && i < bindPoses.Length; i++)
            {
                float4x4 boneMat = float4x4.TRS(
                    (float3)unityLocalPos[i],
                    (quaternion)unityLocalRot[i], 1f);

                float4x4 skinMat = math.mul(boneMat, bindPoses[i].Value);
                skinMatrices[i] = new Unity.Deformations.SkinMatrix
                {
                    Value = new float3x4(skinMat.c0.xyz, skinMat.c1.xyz, skinMat.c2.xyz, skinMat.c3.xyz)
                };
            }

            LastRageWorldPos = rageWorldPos;
            LastRageWorldRot = rageWorldRot;

            if (_debugGizmo != null)
            {
                _debugGizmo.Offset = rootWorldPos;
                _debugGizmo.Rotation = rootWorldRot;
                for (int i = 0; i < _boneCount; i++)
                    _debugGizmo.BonePositions[i] = unityLocalPos[i];
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
