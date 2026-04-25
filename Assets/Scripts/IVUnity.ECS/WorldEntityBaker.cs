using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity.ECS
{
    /// <summary>
    /// One-shot. After GTADatLoader completes, translate every Ipl_INST into a root entity
    /// with shared-component-driven streaming state, and build the ModelCatalog of unique
    /// model metadata. Logic mirrors HighPerformanceLoader.BuildObjectIndex:
    ///   - Indexes Item_OBJS by modelName AND by GTA hash (for hash-only instances).
    ///   - Merges items_tobj into the dict with flag2 |= 0x80000000 so they're recognized
    ///     as TOBJ/skipped at instance time.
    ///   - Uses inst.unityPosition / inst.unityRotation directly (already coord-corrected).
    ///   - Dedups instances by position rounded to 0.1m + model name.
    ///   - Dynamic extension search across .wdr / .wdd / .wft / .wbn / .wbd.
    ///   - Hash-only instances fall back to "0x{hash:x8}" name with synthetic Item_OBJS.
    /// </summary>
    public static class WorldEntityBaker
    {
        private static readonly string[] ModelExtensions = { ".wdr", ".wdd", ".wft", ".wbn", ".wbd" };

        // Renderable + parseable only. .wdd holds LOD variants packed together; without
        // proper LOD/sub-drawable selection we'd stack every LOD on the same instance.
        // Re-add .wdd once a WddDrawableSelector is in place.
        private static bool IsModelExt(string ext) =>
            ext.Equals(".wdr", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".wft", StringComparison.OrdinalIgnoreCase);

        public static void Bake(
            EntityManager em,
            GTADatLoader loader,
            ModelCatalog catalog,
            float cellSize)
        {
            // --- 1. Index Item_OBJS and items_tobj by modelName + GTA hash ---
            var objsDict   = new Dictionary<string, Item_OBJS>(System.StringComparer.OrdinalIgnoreCase);
            var objsByHash = new Dictionary<uint, Item_OBJS>();
            var hashToName = new Dictionary<uint, string>();

            foreach (var ide in loader.ideLoader.ides)
            {
                foreach (var obj in ide.items_objs)
                {
                    if (string.IsNullOrEmpty(obj.modelName)) continue;
                    if (!objsDict.ContainsKey(obj.modelName)) objsDict[obj.modelName] = obj;
                    uint h = RageLib.Common.Hasher.Hash(obj.modelName);
                    if (!objsByHash.ContainsKey(h)) objsByHash[h] = obj;
                    if (!hashToName.ContainsKey(h)) hashToName[h] = obj.modelName;
                }
                foreach (var tobj in ide.items_tobj)
                {
                    if (string.IsNullOrEmpty(tobj.modelName)) continue;
                    // Mark as TOBJ (time-object / spawner / non-render) via the same flag
                    // HighPerformanceLoader uses to filter them out downstream.
                    var marked = tobj;
                    marked.flag2 = unchecked((int)0x80000000);
                    if (!objsDict.ContainsKey(tobj.modelName)) objsDict[tobj.modelName] = marked;
                    uint h = RageLib.Common.Hasher.Hash(tobj.modelName);
                    if (!objsByHash.ContainsKey(h)) objsByHash[h] = marked;
                    if (!hashToName.ContainsKey(h)) hashToName[h] = tobj.modelName;
                }
            }
            int ideCount = objsDict.Count;

            // Synthesize entries for every model file in the IMG archives that no IDE entry
            // covers. The engine registers a CBaseModelInfo slot for every drawable in IMG
            // regardless of IDE, so an IPL inst can reference any of them by hash (LODs,
            // dummies, props that only ship as files). Without this pass, hash-only IPL
            // instances pointing to such models hit the "0x..." fallback and get dropped.
            //
            // textureName = basename mirrors the most common IV convention (model.wdr ↔
            // model.wtd same name); the V2 TxdStore lazy-loads it on demand and falls back
            // to the model's embedded TXD if no matching .wtd exists.
            int synthesized = 0;
            foreach (var kvp in loader.gameFiles)
            {
                string fileName = kvp.Key;
                string ext = Path.GetExtension(fileName);
                if (!IsModelExt(ext)) continue;

                string baseName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(baseName)) continue;

                uint h = RageLib.Common.Hasher.Hash(baseName);
                if (objsByHash.ContainsKey(h)) continue; // IDE already covers this model

                var synthetic = new Item_OBJS
                {
                    modelName    = baseName,
                    textureName  = baseName,
                    drawDistance = new[] { 300f },
                    flag1        = 0,
                    flag2        = 0,
                };

                if (!objsDict.ContainsKey(baseName)) objsDict[baseName] = synthetic;
                objsByHash[h] = synthetic;
                if (!hashToName.ContainsKey(h)) hashToName[h] = baseName;
                synthesized++;
            }
            Debug.Log($"[Baker] Indexed {ideCount} IDE entries + {synthesized} synthesized from IMG (TOBJs merged and flagged)");

            // --- 2. Build ModelCatalog from every resolved Item_OBJS that has a model file ---
            int catalogResolved = 0, catalogMissing = 0;
            foreach (var kv in objsDict)
            {
                var def = kv.Value;

                // Skip TOBJ at catalog time — no point loading meshes we'll never render.
                if ((def.flag2 & unchecked((int)0x80000000)) != 0) continue;

                // Prefer the wdd-linked file name if the IDE specifies one; else search by modelName.
                string modelFileName = null;
                File modelFile = null;

                if (!string.IsNullOrEmpty(def.wdd) &&
                    !def.wdd.Equals("null", System.StringComparison.OrdinalIgnoreCase))
                {
                    modelFileName = def.wdd + ".wdd";
                    loader.gameFiles.TryGetValue(modelFileName.ToLowerInvariant(), out modelFile);
                }

                if (modelFile == null)
                {
                    foreach (var ext in ModelExtensions)
                    {
                        string candidate = def.modelName + ext;
                        if (loader.gameFiles.TryGetValue(candidate.ToLowerInvariant(), out modelFile))
                        {
                            modelFileName = candidate;
                            break;
                        }
                    }
                }

                if (modelFile == null)
                {
                    catalogMissing++;
                    continue;
                }

                // Texture resolution: .wtd with same name, else embedded fallback at upload time.
                File textureFile = null;
                string texFileName = null;
                if (!string.IsNullOrEmpty(def.textureName))
                {
                    texFileName = def.textureName + ".wtd";
                    loader.gameFiles.TryGetValue(texFileName.ToLowerInvariant(), out textureFile);
                }

                uint hash = ModelCatalog.Hash(def.modelName);
                catalog.Add(hash, new ModelCatalog.Entry
                {
                    ModelFileName   = modelFileName,
                    TextureFileName = textureFile != null ? texFileName : null,
                    WddName         = def.wdd,
                    ModelFile       = modelFile,
                    TextureFile     = textureFile,
                    Definition      = def,
                });
                catalogResolved++;
            }
            Debug.Log($"[Baker] ModelCatalog: {catalogResolved} resolved, {catalogMissing} missing model files");

            // --- 3. Singleton entities ---
            var configEntity = em.CreateEntity();
            em.SetName(configEntity, "StreamingConfig");
            em.AddComponentData(configEntity, StreamingConfig.Default);

            var focusEntity = em.CreateEntity();
            em.SetName(focusEntity, "FocusPoint");
            em.AddComponentData(focusEntity, new FocusPointData { Position = float3.zero });

            // --- 4. Iterate all Ipl_INST, create one entity each ---
            // Two archetypes: one lean (uniform scale fits in LocalTransform), one with
            // PostTransformMatrix for genuinely non-uniform instances. Choosing at
            // CreateEntity time avoids a second structural change per non-uniform entity.
            var archetypeUniform = em.CreateArchetype(
                typeof(WorldInstanceTag),
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(ModelRef),
                typeof(InstanceOrigin),
                typeof(CellIndex),
                typeof(StreamingState));
            var archetypeNonUniform = em.CreateArchetype(
                typeof(WorldInstanceTag),
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(PostTransformMatrix),
                typeof(ModelRef),
                typeof(InstanceOrigin),
                typeof(CellIndex),
                typeof(StreamingState));

            int totalInstances = 0;
            foreach (var ipl in loader.iplLoader.ipls) totalInstances += ipl.ipl_inst.Count;
            LoadingScreen.SetupLoadingTarget(totalInstances);

            var dedupSet = new HashSet<string>(capacity: totalInstances);
            ulong sourceId = 0;
            int created = 0, skippedTobj = 0, skippedUnresolved = 0, skippedDuplicate = 0;

            foreach (var ipl in loader.iplLoader.ipls)
            {
                int batchStart = created;
                foreach (var inst in ipl.ipl_inst)
                {
                    sourceId++;

                    // Resolve name: prefer inst.name, fall back to hash lookup, fall back to "0x{hex}"
                    string instanceName = inst.name;
                    Item_OBJS objDef = null;

                    if (string.IsNullOrEmpty(instanceName) && inst.hash != 0)
                    {
                        if (objsByHash.TryGetValue((uint)inst.hash, out objDef))
                        {
                            instanceName = objDef.modelName;
                        }
                        else if (hashToName.TryGetValue((uint)inst.hash, out var resolved))
                        {
                            instanceName = resolved;
                        }
                        else
                        {
                            instanceName = $"0x{inst.hash:x8}";
                        }
                    }

                    if (string.IsNullOrEmpty(instanceName)) { skippedUnresolved++; continue; }

                    if (objDef == null) objsDict.TryGetValue(instanceName, out objDef);

                    // Skip TOBJ-flagged definitions — they're non-render spawners.
                    if (objDef != null && (objDef.flag2 & unchecked((int)0x80000000)) != 0)
                    {
                        skippedTobj++;
                        continue;
                    }

                    // Only create an entity if we have a catalog entry for the model.
                    uint hash = ModelCatalog.Hash(instanceName);
                    if (!catalog.TryGet(hash, out _))
                    {
                        skippedUnresolved++;
                        continue;
                    }

                    // Duplicate check: same instance (rounded 0.1m) with the same model name.
                    string dedupKey =
                        $"{Mathf.Round(inst.position.x * 10f) / 10f:F1}," +
                        $"{Mathf.Round(inst.position.y * 10f) / 10f:F1}," +
                        $"{Mathf.Round(inst.position.z * 10f) / 10f:F1}:" +
                        instanceName;
                    if (!dedupSet.Add(dedupKey)) { skippedDuplicate++; continue; }

                    // unityPosition / unityRotation / unityScale — same three inputs
                    // HighPerformanceLoader writes to go.transform.{position,rotation,localScale}.
                    // unityPosition / unityRotation already bake in the VirtualParentMatrix
                    // (−90°X + (−1,1,1) mirror); do NOT re-apply any coord fix here.
                    Vector3 uPos   = inst.unityPosition;
                    Quaternion uRot = inst.unityRotation;
                    Vector3 uScale = inst.unityScale;

                    // ECS LocalTransform.Scale is scalar. Uniform scale fits; non-uniform needs
                    // PostTransformMatrix so the final LocalToWorld matches T * R * S3d — the
                    // composition go.transform.{position,rotation,localScale} produces.
                    // Use 1e-3 epsilon so floating-point drift from the TRS/multiply in
                    // Ipl_INST.unityScale doesn't wrongly classify default instances as non-uniform.
                    const float uniformEps = 1e-3f;
                    bool isUniform = Mathf.Abs(uScale.x - uScale.y) < uniformEps
                                  && Mathf.Abs(uScale.x - uScale.z) < uniformEps;

                    Entity entity;
                    if (isUniform)
                    {
                        float s = uScale.x != 0f ? uScale.x : 1f;
                        entity = em.CreateEntity(archetypeUniform);
                        em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                            new float3(uPos.x, uPos.y, uPos.z),
                            new quaternion(uRot.x, uRot.y, uRot.z, uRot.w),
                            s));
                    }
                    else
                    {
                        entity = em.CreateEntity(archetypeNonUniform);
                        em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                            new float3(uPos.x, uPos.y, uPos.z),
                            new quaternion(uRot.x, uRot.y, uRot.z, uRot.w),
                            1f));
                        em.SetComponentData(entity, new PostTransformMatrix
                        {
                            Value = float4x4.Scale(uScale.x, uScale.y, uScale.z),
                        });
                    }
                    em.SetComponentData(entity, new ModelRef { ModelHash = hash });
                    em.SetComponentData(entity, new InstanceOrigin { SourceId = sourceId });

                    int2 cell = CellMath.PositionToCell(new float3(uPos.x, uPos.y, uPos.z), cellSize);
                    em.SetSharedComponent(entity, new CellIndex { Cell = cell });
                    em.SetSharedComponent(entity, new StreamingState { Value = StreamingStateValue.Dormant });

                    created++;
                }
                LoadingScreen.AdvanceProgress(ipl.name, created - batchStart);
            }

            Debug.Log(
                $"[Baker] Created {created} root entities " +
                $"(skipped {skippedUnresolved} unresolved, {skippedTobj} TOBJ, {skippedDuplicate} duplicate)");
        }
    }
}
