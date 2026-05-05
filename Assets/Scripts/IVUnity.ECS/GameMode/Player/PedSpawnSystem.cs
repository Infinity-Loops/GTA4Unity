using System.Collections.Generic;
using System.IO;
using IVUnity.ECS.Ped;
using IVUnity.Ped;
using IVUnity.Resolver;
using RageLib.Models;
using RageLib.Models.Data;
using RageLib.Textures;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using CapsuleCollider = Unity.Physics.CapsuleCollider;
using File = RageLib.FileSystem.Common.File;
using Material = UnityEngine.Material;

namespace IVUnity.ECS.GameMode
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PedSpawnSystem : SystemBase
    {
        const float CapsuleRadius = 0.3f;

        private bool spawned;
        private Dictionary<string, File> gameFiles;
        private Dictionary<string, PedVariation> pedVariations;
        private Dictionary<string, Item_PEDS> pedsIde;
        private Dictionary<string, string> moveBlendFallbacks;
        private EntitiesGraphicsSystem graphics;

        public void Configure(Dictionary<string, File> gameFiles, Dictionary<string, PedVariation> pedVariations,
            Dictionary<string, Item_PEDS> pedsIde, Dictionary<string, string> moveBlendFallbacks)
        {
            this.gameFiles = gameFiles;
            this.pedVariations = pedVariations;
            this.pedsIde = pedsIde;
            this.moveBlendFallbacks = moveBlendFallbacks;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<FocusPointData>();
            RequireForUpdate<CollisionReadyTag>();
        }

        protected override void OnUpdate()
        {
            if (spawned || gameFiles == null) return;
            spawned = true;

            graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null) return;

            var em = EntityManager;
            float3 startPos = new float3(0, 100, 0);

            var spawnPoint = Object.FindAnyObjectByType<SpawnPoint>();
            if (spawnPoint != null)
                startPos = (float3)spawnPoint.transform.position;

            string pedName = FindFirstPedName();
            if (pedName == null)
            {
                Debug.LogError("[PedSpawn] No ped model found in componentpeds.img");
                return;
            }

            Debug.Log($"[PedSpawn] Spawning ped: {pedName}");

            Entity charEntity = CreateCharacterEntity(em, startPos, 1.8f);

            // Mesh parent sits between character and submeshes, offset to align feet with capsule base
            Entity meshParent = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent));
            em.SetComponentData(meshParent, new Parent { Value = charEntity });

            // Build skeleton from .wft
            var skeletonData = LoadSkeleton(em, meshParent, pedName);

            float meshOffsetY = LoadPedMeshes(em, meshParent, pedName, skeletonData);
            em.SetComponentData(meshParent, LocalTransform.FromPosition(new float3(0, meshOffsetY, 0)));

            Entity camEntity = CreateCameraEntity(em, startPos, charEntity, 1.8f);

            em.AddComponentData(charEntity, new Possessed());

            Entity playerEntity = em.CreateEntity(
                typeof(PedPlayer),
                typeof(PedPlayerInputs),
                typeof(Simulate));

            em.SetComponentData(playerEntity, new PedPlayer
            {
                ControlledCharacter = charEntity,
                ControlledCamera = camEntity,
            });

            // Load animation WAD and configure playback
            SetupAnimation(em, meshParent, pedName, skeletonData);

            // BuildSkinnedMeshTest(pedName, skeletonData, startPos);

            Debug.Log($"[PedSpawn] Ped spawned at {startPos}");
        }

        private Entity CreateCharacterEntity(EntityManager em, float3 position, float pedHeight)
        {
            Entity e = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(PedTag),
                typeof(PedCharacterComponent),
                typeof(PedCharacterControl),
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex),
                typeof(Simulate));

            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, PedCharacterComponent.GetDefault());

            var capsule = CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(0, CapsuleRadius, 0),
                Vertex1 = new float3(0, pedHeight - CapsuleRadius, 0),
                Radius = CapsuleRadius,
            });
            em.SetComponentData(e, new PhysicsCollider { Value = capsule });
            em.SetSharedComponent(e, new PhysicsWorldIndex { Value = 0 });

            KinematicCharacterUtilities.CreateCharacter(em, e,
                AuthoringKinematicCharacterProperties.GetDefault());

            return e;
        }

        private Entity CreateCameraEntity(EntityManager em, float3 position, Entity followTarget, float pedHeight)
        {
            Entity e = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(OrbitCamera),
                typeof(OrbitCameraControl),
                typeof(PedCameraTag),
                typeof(Simulate));

            em.SetComponentData(e, LocalTransform.FromPosition(position + new float3(0, 2, -5)));
            var cam = OrbitCamera.GetDefault();
            cam.FollowOffset = new float3(0, pedHeight * 0.65f, 0);
            em.SetComponentData(e, cam);
            em.SetComponentData(e, new OrbitCameraControl { FollowedCharacterEntity = followTarget });

            return e;
        }

        private PedSkeletonBuilder.SkeletonData LoadSkeleton(EntityManager em, Entity meshParent, string pedName)
        {
            string lowerName = pedName.ToLower();
            if (!gameFiles.TryGetValue(lowerName + ".wft", out var wftFile))
            {
                Debug.LogWarning($"[PedSpawn] Missing {pedName}.wft - no skeleton");
                return default;
            }

            try
            {
                var fragFile = new ModelFragTypeFile();
                using (var stream = new MemoryStream(wftFile.GetData()))
                    fragFile.Open(stream);

                var resourceSkeleton = fragFile.File.Data.Drawable.Skeleton;
                if (resourceSkeleton == null)
                {
                    Debug.LogWarning($"[PedSpawn] {pedName}.wft has no skeleton");
                    fragFile.Dispose();
                    return default;
                }

                var result = PedSkeletonBuilder.Build(em, meshParent, resourceSkeleton);
                fragFile.Dispose();
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PedSpawn] Failed to load {pedName}.wft skeleton: {ex.Message}");
                return default;
            }
        }

        private void BuildSkinnedMeshTest(string pedName, PedSkeletonBuilder.SkeletonData skeletonData, float3 pos)
        {
            string lowerName = pedName.ToLower();
            if (!gameFiles.TryGetValue(lowerName + ".wft", out var wftFile)) return;
            if (!gameFiles.TryGetValue(lowerName + ".wdd", out var wddFile)) return;

            try
            {
                // Load skeleton
                var fragFile = new ModelFragTypeFile();
                using (var stream = new MemoryStream(wftFile.GetData()))
                    fragFile.Open(stream);
                var resourceSkeleton = fragFile.File.Data.Drawable.Skeleton;
                if (resourceSkeleton == null) { fragFile.Dispose(); return; }

                // Load meshes
                TextureFile wtd = null;
                if (gameFiles.TryGetValue(lowerName + ".wtd", out var wtdFile))
                {
                    wtd = new TextureFile();
                    using (var stream = new MemoryStream(wtdFile.GetData()))
                        wtd.Open(stream);
                }

                var dict = new ModelDictionaryFile();
                using (var stream = new MemoryStream(wddFile.GetData()))
                    dict.Open(stream);

                if (!pedVariations.TryGetValue(pedName, out var variation)) return;
                int variant = UnityEngine.Random.Range(0, variation.VariantCount);
                var slots = variation.GetSlotsForVariant(variant);

                var hashes = dict.File.Data.NameHashes;
                var entries = dict.File.Data.Entries;
                var hashToIndex = new Dictionary<uint, int>();
                for (int i = 0; i < hashes.Count && i < entries.Count; i++)
                    hashToIndex[hashes[i]] = i;

                TextureFile[] texArray = wtd != null ? new[] { wtd } : null;
                var flatBuffer = new System.Collections.Generic.List<FlatSubMesh>(16);

                foreach (var slot in slots)
                {
                    var candidates = PedVariation.GetDrawableCandidates(slot.Slot, slot.GeometryIndex);
                    foreach (string candidate in candidates)
                    {
                        uint h = RageLib.Common.Hasher.Hash(candidate);
                        if (hashToIndex.TryGetValue(h, out int entryIdx))
                        {
                            var drawable = new Drawable(entries[entryIdx]);
                            while (drawable.Models.Count > 1)
                                drawable.Models.RemoveAt(drawable.Models.Count - 1);
                            var modelNode = ModelGenerator.GenerateModel(drawable, texArray);
                            ModelFlatten.Flatten(modelNode, flatBuffer);
                            break;
                        }
                    }
                }

                // Load animation
                string wadName = "move_player";
                var loadResult = PedAnimationSystem.LoadFromWad(gameFiles, wadName);
                RageLib.Animation.AnimationClip rageClip = null;
                if (loadResult != null && loadResult.ClipsByName.TryGetValue("idle", out int idleIdx))
                    rageClip = loadResult.Clips[idleIdx];

                var testGO = SkinnedMeshTest.Build(resourceSkeleton, flatBuffer, rageClip);
                testGO.transform.position = new Vector3(pos.x + 2f, pos.y, pos.z);

                fragFile.Dispose();
                dict.Dispose();
                wtd?.Dispose();

                Debug.Log($"[SkinnedMeshTest] Built test GO with {flatBuffer.Count} meshes");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SkinnedMeshTest] Failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private PedAnimationSystem.LoadResult LoadWadWithFallbacks(string wadName)
        {
            // Load the primary WAD
            var primary = PedAnimationSystem.LoadFromWad(gameFiles, wadName);

            // Walk the fallback chain from moveblend.dat, merging missing clips
            string current = wadName;
            int depth = 0;
            while (depth < 5 && moveBlendFallbacks != null && moveBlendFallbacks.TryGetValue(current, out string fallback))
            {
                var fbResult = PedAnimationSystem.LoadFromWad(gameFiles, fallback);
                if (fbResult != null)
                {
                    if (primary == null)
                    {
                        primary = fbResult;
                    }
                    else
                    {
                        // Merge: add any clips from fallback that primary doesn't have
                        int added = 0;
                        foreach (var kv in fbResult.ClipsByName)
                        {
                            if (!primary.ClipsByName.ContainsKey(kv.Key))
                            {
                                int newIdx = primary.Clips.Length;
                                System.Array.Resize(ref primary.Clips, newIdx + 1);
                                primary.Clips[newIdx] = fbResult.Clips[kv.Value];
                                primary.ClipsByName[kv.Key] = newIdx;
                                added++;
                            }
                        }
                        if (added > 0)
                            Debug.Log($"[PedAnim] Merged {added} clips from fallback '{fallback}'");
                    }
                }
                current = fallback;
                depth++;
            }

            return primary;
        }

        private void SetupAnimation(EntityManager em, Entity meshParent, string pedName, PedSkeletonBuilder.SkeletonData skeletonData)
        {
            if (skeletonData.BoneEntities == null || skeletonData.BoneIds == null) return;

            // Use ped's own movement group from peds.ide, with fallback chain from moveblend.dat
            string wadName = "move_player";
            if (pedsIde != null && pedsIde.TryGetValue(pedName, out var pedInfo)
                && !string.IsNullOrEmpty(pedInfo.MovementGroup) && pedInfo.MovementGroup != "null")
                wadName = pedInfo.MovementGroup;

            var loadResult = LoadWadWithFallbacks(wadName);
            if (loadResult == null) return;

            var animSystem = World.GetExistingSystemManaged<PedAnimationSystem>();
            if (animSystem == null) return;

            animSystem.Configure(loadResult.Clips, loadResult.ClipsByName,
                skeletonData.BoneEntities, skeletonData.BoneIds,
                skeletonData.ParentIndices, skeletonData.RestPose,
                skeletonData.RageRestPositions, skeletonData.RageRestRotations);

            int idleClip = animSystem.GetClipIndex("idle");
            if (idleClip < 0) idleClip = animSystem.GetClipIndex("idle_a");
            int walkClip = animSystem.GetClipIndex("walk");
            int runClip = animSystem.GetClipIndex("run");
            if (runClip < 0) runClip = animSystem.GetClipIndex("run");
            int sprintClip = animSystem.GetClipIndex("sprint");
            if (sprintClip < 0) sprintClip = runClip;

            em.AddComponentData(meshParent, new PedMoveBlend
            {
                DesiredSpeed = 0f,
                IdleClip = idleClip,
                WalkClip = walkClip,
                RunClip = runClip,
                SprintClip = sprintClip,
            });
        }

        private string FindFirstPedName()
        {
            if (pedsIde == null || pedsIde.Count == 0) return null;

            var keys = new System.Collections.Generic.List<string>(pedsIde.Keys);
            keys.Sort(System.StringComparer.OrdinalIgnoreCase);

            foreach (string name in keys)
            {
                if (gameFiles.ContainsKey(name.ToLower() + ".wdd") && pedVariations.ContainsKey(name))
                    return name;
            }

            return null;
        }

        private float LoadPedMeshes(EntityManager em, Entity meshParent, string pedName, PedSkeletonBuilder.SkeletonData skeletonData)
        {
            string lowerName = pedName.ToLower();
            if (!gameFiles.TryGetValue(lowerName + ".wdd", out var wddFile))
            {
                Debug.LogWarning($"[PedSpawn] Missing {pedName}.wdd");
                return 0f;
            }

            if (!pedVariations.TryGetValue(pedName, out var variation))
            {
                Debug.LogWarning($"[PedSpawn] No pedVariations entry for {pedName}");
                return 0f;
            }


            int variant = UnityEngine.Random.Range(0, variation.VariantCount);
            var defaultSlots = variation.GetSlotsForVariant(variant);
            Debug.Log($"[PedSpawn] {pedName} variant {variant}/{variation.VariantCount}, {defaultSlots.Count} slots");

            TextureFile wtd = null;
            if (gameFiles.TryGetValue(lowerName + ".wtd", out var wtdFile))
            {
                try
                {
                    wtd = new TextureFile();
                    using (var stream = new MemoryStream(wtdFile.GetData()))
                        wtd.Open(stream);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PedSpawn] Failed to parse {pedName}.wtd: {ex.Message}");
                    wtd = null;
                }
            }

            TextureFile[] texArray = wtd != null ? new[] { wtd } : null;

            ModelDictionaryFile dict = null;
            try
            {
                dict = new ModelDictionaryFile();
                using (var stream = new MemoryStream(wddFile.GetData()))
                    dict.Open(stream);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PedSpawn] Failed to parse {pedName}.wdd: {ex.Message}");
                dict?.Dispose();
                wtd?.Dispose();
                return 0f;
            }

            var hashes = dict.File.Data.NameHashes;
            var entries = dict.File.Data.Entries;

            var hashToIndex = new Dictionary<uint, int>();
            for (int i = 0; i < hashes.Count && i < entries.Count; i++)
                hashToIndex[hashes[i]] = i;

            int meshCount = 0;
            float minY = 0f;
            var flatBuffer = new List<FlatSubMesh>(16);

            // Set up skinning on mesh parent if skeleton is available
            bool hasSkinning = skeletonData.BoneEntities != null && skeletonData.BoneEntities.Length > 0;
            int boneCount = hasSkinning ? skeletonData.BoneEntities.Length : 0;
            Matrix4x4[] bindposes = null;

            if (hasSkinning)
            {
                DeformationHelper.SetupDeformedEntity(em, meshParent, boneCount);
                em.AddComponentData(meshParent, new SkinnedMeshRootEntity { Value = meshParent });
                em.AddBuffer<SkinnedMeshBoneRef>(meshParent).ResizeUninitialized(boneCount);
                em.AddBuffer<SkinnedMeshBindPose>(meshParent).ResizeUninitialized(boneCount);

                bindposes = new Matrix4x4[boneCount];
                for (int i = 0; i < boneCount; i++)
                {
                    var m = skeletonData.InverseBindPoses[i];
                    bindposes[i] = new Matrix4x4(m.c0, m.c1, m.c2, m.c3);
                }
            }

            foreach (var slot in defaultSlots)
            {
                var candidates = PedVariation.GetDrawableCandidates(slot.Slot, slot.GeometryIndex);
                int entryIdx = -1;
                string matchedName = null;

                foreach (string candidate in candidates)
                {
                    uint h = RageLib.Common.Hasher.Hash(candidate);
                    if (hashToIndex.TryGetValue(h, out int idx))
                    {
                        entryIdx = idx;
                        matchedName = candidate;
                        break;
                    }
                }

                if (entryIdx < 0)
                    continue;

                try
                {
                    var drawable = new Drawable(entries[entryIdx]);
                    // Keep only LOD 0 (highest detail) — ModelCollection[0..3] = high/med/low/vlow
                    while (drawable.Models.Count > 1)
                        drawable.Models.RemoveAt(drawable.Models.Count - 1);
                    var modelNode = ModelGenerator.GenerateModel(drawable, texArray);

                    flatBuffer.Clear();
                    ModelFlatten.Flatten(modelNode, flatBuffer);

                    Debug.Log($"[PedSpawn] Matched '{matchedName}' → {flatBuffer.Count} sub-mesh(es)");

                    foreach (var sub in flatBuffer)
                    {
                        var mesh = sub.Geometry.GetUnityMesh();

                        bool meshIsSkinned = hasSkinning && bindposes != null
                            && mesh.boneWeights != null && mesh.boneWeights.Length > 0;

                        if (!meshIsSkinned && hasSkinning)
                            Debug.LogWarning($"[PedSpawn] Mesh has no bone weights (boneWeights={mesh.boneWeights?.Length ?? 0}, hasBlend={sub.Geometry.boneWeights != null})");

                        if (meshIsSkinned)
                            mesh.bindposes = bindposes;

                        Material mat = null;
                        string shaderName = sub.Material?.shaderName;

                        if (sub.Material != null)
                        {
                            Texture2D diffuse = sub.Material.mainTex?.textureFile != null
                                ? sub.Material.mainTex.GetUnityTexture() : null;
                            Texture2D normal = sub.Material.normalTex?.textureFile != null
                                ? sub.Material.normalTex.GetUnityTexture() : null;
                            Texture2D specular = sub.Material.specularTex?.textureFile != null
                                ? sub.Material.specularTex.GetUnityTexture() : null;

                            if (meshIsSkinned)
                                mat = MaterialResolver.Build("gta_default_skinned", diffuse, normal, specular);
                            else
                                mat = MaterialResolver.Build(shaderName, diffuse, normal, specular);
                        }

                        var meshId = graphics.RegisterMesh(mesh);
                        var matId = mat != null ? graphics.RegisterMaterial(mat) : default;

                        var child = em.CreateEntity(
                            typeof(LocalTransform),
                            typeof(LocalToWorld),
                            typeof(Parent),
                            typeof(MaterialMeshInfo),
                            typeof(RenderBounds),
                            typeof(WorldRenderBounds),
                            typeof(WorldToLocal_Tag),
                            typeof(PerInstanceCullingTag),
                            typeof(BlendProbeTag),
                            typeof(StippleAlpha),
                            typeof(RenderFilterSettings));
                        em.AddChunkComponentData<ChunkWorldRenderBounds>(child);
                        em.AddChunkComponentData<EntitiesGraphicsChunkInfo>(child);

                        if (mesh.bounds.min.y < minY) minY = mesh.bounds.min.y;

                        em.SetComponentData(child, sub.LocalTransform);
                        em.SetComponentData(child, new Parent { Value = meshParent });
                        em.SetComponentData(child, new MaterialMeshInfo(matId, meshId, 0));
                        em.SetComponentData(child, new StippleAlpha { Value = 1.0f });
                        em.SetComponentData(child, new RenderBounds
                        {
                            Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents }
                        });
                        em.SetSharedComponent(child, new RenderFilterSettings
                        {
                            RenderingLayerMask = 1,
                            Layer = 0,
                            MotionMode = MotionVectorGenerationMode.Camera,
                            ShadowCastingMode = ShadowCastingMode.On,
                            ReceiveShadows = true,
                            StaticShadowCaster = false,
                        });

                        if (meshIsSkinned)
                            DeformationHelper.SetupRenderEntity(em, child, meshParent);

                        meshCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PedSpawn] Failed '{matchedName}': {ex.Message}");
                }
            }

            // Write bone/bindpose buffer data after all structural changes are done
            if (hasSkinning)
            {
                var boneRefs = em.GetBuffer<SkinnedMeshBoneRef>(meshParent);
                var bindPoseBuf = em.GetBuffer<SkinnedMeshBindPose>(meshParent);
                for (int i = 0; i < boneCount; i++)
                {
                    var boneEntity = i < skeletonData.BoneEntities.Length ? skeletonData.BoneEntities[i] : Entity.Null;
                    boneRefs[i] = new SkinnedMeshBoneRef { BoneEntity = boneEntity };
                    bindPoseBuf[i] = new SkinnedMeshBindPose { Value = skeletonData.InverseBindPoses[i] };
                }
            }

            dict.Dispose();
            wtd?.Dispose();
            Debug.Log($"[PedSpawn] Loaded {meshCount} mesh(es) for {pedName}, minY={minY:F2}");
            return -minY;
        }
    }
}
