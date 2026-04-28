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

        // Renderable + parseable. WDD now supported via hash-based sub-drawable selection
        // (WddSingleEntry in ModelLoader). Collision (.wbn/.wbd) still excluded.
        private static bool IsModelExt(string ext) =>
            ext.Equals(".wdr", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".wdd", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".wft", StringComparison.OrdinalIgnoreCase);

        private static bool ContainsSlod(string name) =>
            !string.IsNullOrEmpty(name) &&
            name.IndexOf("slod", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool ContainsLod(string name) =>
            !string.IsNullOrEmpty(name) &&
            name.IndexOf("lod", StringComparison.OrdinalIgnoreCase) >= 0;

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
                foreach (var anim in ide.items_anim)
                {
                    if (string.IsNullOrEmpty(anim.modelName)) continue;
                    Item_OBJS animObj = anim;
                    if (!objsDict.ContainsKey(anim.modelName)) objsDict[anim.modelName] = animObj;
                    uint h = RageLib.Common.Hasher.Hash(anim.modelName);
                    if (!objsByHash.ContainsKey(h)) objsByHash[h] = animObj;
                    if (!hashToName.ContainsKey(h)) hashToName[h] = anim.modelName;
                }
                foreach (var tobj in ide.items_tobj)
                {
                    if (string.IsNullOrEmpty(tobj.modelName)) continue;
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

                // Skip TOBJ at catalog time — timed objects (emissive windows, night variants).
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
                typeof(DrawDist),
                typeof(InstanceOrigin),
                typeof(CellIndex),
                typeof(StreamingState),
                typeof(StreamingIplId));
            var archetypeNonUniform = em.CreateArchetype(
                typeof(WorldInstanceTag),
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(PostTransformMatrix),
                typeof(ModelRef),
                typeof(DrawDist),
                typeof(InstanceOrigin),
                typeof(CellIndex),
                typeof(StreamingState),
                typeof(StreamingIplId));

            int totalInstances = 0;
            foreach (var ipl in loader.iplLoader.ipls) totalInstances += ipl.ipl_inst.Count;
            LoadingScreen.SetupLoadingTarget(totalInstances);

            var dedupSet = new HashSet<string>(capacity: totalInstances);
            ulong sourceId = 0;
            int created = 0, skippedTobj = 0, skippedUnresolved = 0, skippedDuplicate = 0;

            // LOD linkage: inst.lod is an index within the same IPL. We collect
            // per-IPL (originalIndex → entity) + (originalIndex → inst) so a second
            // pass can wire LodRef on HD entities and LodTag on LOD entities.
            var iplEntityMap = new Dictionary<int, Entity>();        // per-IPL: origIdx → entity
            var iplInstMap   = new Dictionary<int, Ipl_INST>();     // per-IPL: origIdx → inst
            var iplObjsMap   = new Dictionary<int, Item_OBJS>();    // per-IPL: origIdx → IDE def
            int lodLinked = 0, lodTagged = 0;

            // Streaming IPL tracking: gta.dat WPLs = base (-1), others = streaming
            StreamingIplRegistry.Clear();
            foreach (var e in loader.dat.GetEntries("IPL"))
            {
                string arg = e.Args[0];
                if (arg.StartsWith("common", System.StringComparison.OrdinalIgnoreCase)) continue;
                StreamingIplRegistry.BaseWplNames.Add(loader.dat.ResolveWplName(arg).ToLower());
            }

            foreach (var ipl in loader.iplLoader.ipls)
            {
                int batchStart = created;
                iplEntityMap.Clear();
                iplInstMap.Clear();
                iplObjsMap.Clear();

                bool isBase = StreamingIplRegistry.BaseWplNames.Contains(ipl.name?.ToLower() ?? "");
                int streamingIplIdx = isBase ? -1 : 0;
                if (!isBase) StreamingIplRegistry.StreamingWplCount++;

                for (int instIdx = 0; instIdx < ipl.ipl_inst.Count; instIdx++)
                {
                    var inst = ipl.ipl_inst[instIdx];
                    sourceId++;

                    // Resolve name: prefer inst.name, fall back to hash lookup, fall back to "0x{hex}"
                    // The WPL parser sets inst.name to "0x{hash}" when Hashes.table (IDE-only)
                    // can't resolve. We still need to check objsByHash which includes synthesis
                    // entries for model files without IDE entries.
                    string instanceName = inst.name;
                    Item_OBJS objDef = null;
                    bool needsHashLookup = string.IsNullOrEmpty(instanceName)
                        || (instanceName.StartsWith("0x") && inst.hash != 0);

                    if (needsHashLookup && inst.hash != 0)
                    {
                        if (objsByHash.TryGetValue((uint)inst.hash, out objDef))
                        {
                            instanceName = objDef.modelName;
                        }
                        else if (hashToName.TryGetValue((uint)inst.hash, out var resolved))
                        {
                            instanceName = resolved;
                        }
                        else if (string.IsNullOrEmpty(inst.name))
                        {
                            instanceName = $"0x{inst.hash:x8}";
                        }
                    }

                    if (string.IsNullOrEmpty(instanceName))
                    {
                        skippedUnresolved++;
                        continue;
                    }

                    if (objDef == null) objsDict.TryGetValue(instanceName, out objDef);

                    // Skip TOBJ-flagged definitions — timed objects (emissive windows, night variants).
                    if (objDef != null && (objDef.flag2 & unchecked((int)0x80000000)) != 0)
                    {
                        skippedTobj++;
                        continue;
                    }

                    // MLO interiors: if this INST is an MLO building, create interior
                    // prop entities instead of an entity for the building shell itself.
                    // MLO models aren't standard drawables — they're containers for rooms/props.
                    if (loader.ideLoader.mloDict.TryGetValue(instanceName, out var mloDef))
                    {
                        int mloCreated = 0, mloSkipped = 0;

                        // Position: compose in RAGE space with raw quaternion (works for vector rotation),
                        // then let Ipl_INST.unityPosition handle RAGE→Unity via VirtualParentMatrix.
                        Quaternion bRotRaw = new Quaternion(
                            inst.rotation.x, inst.rotation.y, inst.rotation.z, inst.rotation.w);

                        // Rotation: building's unityRotation = ParentRot * mirrorAdjust(buildingStored).
                        // The prop's relative rotation in Unity = mirrorAdjust(propStored) = (x, -y, -z, -w).
                        // Composed: buildingUnityRot * mirrorAdjust(propStored).
                        Quaternion bUnityRot = inst.unityRotation;

                        foreach (var prop in mloDef.Entities)
                        {
                            string propName = prop.ModelName;
                            uint propHash = ModelCatalog.Hash(propName);
                            if (!catalog.TryGet(propHash, out _)) { mloSkipped++; continue; }

                            // Position via fakeInst (RAGE composition → Ipl_INST conversion)
                            Vector3 worldPos = inst.position + bRotRaw * prop.Position;
                            var propInst = new Ipl_INST { position = worldPos };
                            Vector3 propUPos = propInst.unityPosition;

                            // Rotation: mirrorAdjust the prop's stored rotation (same as Ipl_INST line 58)
                            // then compose with building's already-converted Unity rotation.
                            Quaternion propRelMirror = new Quaternion(
                                prop.Rotation.x, -prop.Rotation.y, -prop.Rotation.z, -prop.Rotation.w);
                            Quaternion propURot = bUnityRot * propRelMirror;

                            // Scale from the position conversion (handles VPM mirror)
                            Vector3 propUScale = propInst.unityScale;
                            bool mloUniform = Mathf.Abs(propUScale.x - propUScale.y) < 1e-3f
                                           && Mathf.Abs(propUScale.x - propUScale.z) < 1e-3f;

                            Entity propEntity;
                            if (mloUniform)
                            {
                                float s = propUScale.x != 0f ? propUScale.x : 1f;
                                propEntity = em.CreateEntity(archetypeUniform);
                                em.SetComponentData(propEntity, LocalTransform.FromPositionRotationScale(
                                    new float3(propUPos.x, propUPos.y, propUPos.z),
                                    new quaternion(propURot.x, propURot.y, propURot.z, propURot.w),
                                    s));
                            }
                            else
                            {
                                propEntity = em.CreateEntity(archetypeNonUniform);
                                em.SetComponentData(propEntity, LocalTransform.FromPositionRotationScale(
                                    new float3(propUPos.x, propUPos.y, propUPos.z),
                                    new quaternion(propURot.x, propURot.y, propURot.z, propURot.w),
                                    1f));
                                em.SetComponentData(propEntity, new PostTransformMatrix
                                {
                                    Value = float4x4.Scale(propUScale.x, propUScale.y, propUScale.z),
                                });
                            }
                            em.SetComponentData(propEntity, new ModelRef { ModelHash = propHash });
                            em.SetComponentData(propEntity, new DrawDist { Value = mloDef.drawDistance });
                            em.AddComponentData(propEntity, new BoundRadius { Value = 0f });
                            em.SetComponentData(propEntity, new InstanceOrigin { SourceId = ++sourceId });

                            int2 propCell = CellMath.PositionToCell(new float3(propUPos.x, propUPos.y, propUPos.z), cellSize);
                            em.SetSharedComponent(propEntity, new CellIndex { Cell = propCell });
                            em.SetSharedComponent(propEntity, new StreamingState { Value = StreamingStateValue.Dormant });
                            em.SetSharedComponent(propEntity, new StreamingIplId { Value = streamingIplIdx });
                            em.AddComponentData(propEntity, new LodLevel { Value = LodType.OrphanHD });

                            #if UNITY_EDITOR
                            em.AddComponentData(propEntity, new DebugModelName
                            {
                                Value = new Unity.Collections.FixedString64Bytes(propName.Length > 63 ? propName.Substring(0, 63) : propName)
                            });
                            #endif

                            mloCreated++;
                            created++;
                        }

                        if (mloCreated > 0 || mloSkipped > 0)
                            Debug.Log($"[Baker] MLO '{instanceName}': {mloCreated} props, {mloSkipped} skipped (no catalog)");
                        continue;
                    }

                    // Only create an entity if we have a catalog entry for the model.
                    uint hash = ModelCatalog.Hash(instanceName);
                    if (!catalog.TryGet(hash, out _))
                    {
                        Debug.LogWarning($"[Baker] No catalog: '{instanceName}' (instHash=0x{(uint)inst.hash:X8}) in {ipl.name}");
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

                    // Per-entity draw distance from IDE. Default 300 if no IDE entry.
                    float drawDist = 300f;
                    if (objDef?.drawDistance != null && objDef.drawDistance.Length > 0)
                        drawDist = objDef.drawDistance[0];
                    em.SetComponentData(entity, new DrawDist { Value = drawDist });

                    // XZ bound radius from IDE boundsMin/boundsMax + sphere center offset.
                    // Use XZ extent only (not 3D sphere) for accurate 2D distance checks.
                    // Include sphere center offset so the radius reaches the farthest XZ point
                    // from the entity origin. IDE coords: X=east, Y=north (our XZ plane).
                    float boundRadius = 0f;
                    if (objDef != null)
                    {
                        float xMin = objDef.boundsMin.x;
                        float xMax = objDef.boundsMax.x;
                        float yMin = objDef.boundsMin.y; // RAGE Y = north = Unity Z
                        float yMax = objDef.boundsMax.y;

                        float halfX = (xMax - xMin) * 0.5f;
                        float halfY = (yMax - yMin) * 0.5f;
                        float centerOffX = (xMax + xMin) * 0.5f;
                        float centerOffY = (yMax + yMin) * 0.5f;

                        float maxExtentX = Mathf.Abs(centerOffX) + halfX;
                        float maxExtentY = Mathf.Abs(centerOffY) + halfY;
                        boundRadius = Mathf.Max(maxExtentX, maxExtentY);
                    }
                    em.AddComponentData(entity, new BoundRadius { Value = boundRadius });

                    em.SetComponentData(entity, new InstanceOrigin { SourceId = sourceId });

                    #if UNITY_EDITOR
                    em.AddComponentData(entity, new DebugModelName
                    {
                        Value = new Unity.Collections.FixedString64Bytes(instanceName ?? "unknown")
                    });
                    #endif

                    int2 cell = CellMath.PositionToCell(new float3(uPos.x, uPos.y, uPos.z), cellSize);
                    em.SetSharedComponent(entity, new CellIndex { Cell = cell });
                    em.SetSharedComponent(entity, new StreamingState { Value = StreamingStateValue.Dormant });
                    em.SetSharedComponent(entity, new StreamingIplId { Value = streamingIplIdx });

                    if (streamingIplIdx < 0)
                        em.AddComponentData(entity, new BaseLayerTag());

                    iplEntityMap[instIdx] = entity;
                    iplInstMap[instIdx] = inst;
                    iplObjsMap[instIdx] = objDef;

                    created++;
                }

                // --- LOD linkage pass for this IPL ---
                // inst.lod >= 0: this HD instance's LOD replacement is at that index.
                // inst.lod == -1: this instance IS a LOD (or has no LOD pair).
                // We tag LOD entities and add LodRef on HD entities pointing to their LOD.
                var lodTargets = new HashSet<int>(); // indices that are LOD targets
                foreach (var kv in iplInstMap)
                {
                    if (kv.Value.lod >= 0) lodTargets.Add(kv.Value.lod);
                }

                foreach (var lodIdx in lodTargets)
                {
                    if (iplEntityMap.TryGetValue(lodIdx, out var lodEntity))
                    {
                        em.AddComponentData(lodEntity, new LodTag());
                        em.AddComponentData(lodEntity, new LodChildCount { Value = 0 });
                        lodTagged++;
                    }
                }

                // Count children per LOD, track max child drawDist, add LodRef on HD entities.
                var childCounts = new Dictionary<int, int>();
                var childMaxDist = new Dictionary<int, float>();
                int brokenHd = 0, brokenLod = 0;
                foreach (var kv in iplInstMap)
                {
                    int lodIdx = kv.Value.lod;
                    if (lodIdx < 0) continue;

                    bool hdExists = iplEntityMap.TryGetValue(kv.Key, out var hdEntity);
                    bool lodExists = iplEntityMap.TryGetValue(lodIdx, out var lodEntity);

                    if (!hdExists) { brokenHd++; continue; }
                    if (!lodExists) { brokenLod++; continue; }

                    em.AddComponentData(hdEntity, new LodRef
                    {
                        LodEntity = lodEntity,
                    });
                    lodLinked++;

                    if (!childCounts.ContainsKey(lodIdx))
                        childCounts[lodIdx] = 0;
                    childCounts[lodIdx]++;

                    // Track max child drawDist for ChildLodDist (RAGE: m_childLodDistance)
                    float childDraw = em.GetComponentData<DrawDist>(hdEntity).Value;
                    if (!childMaxDist.TryGetValue(lodIdx, out float existing) || childDraw > existing)
                        childMaxDist[lodIdx] = childDraw;
                }
                if (brokenHd > 0 || brokenLod > 0)
                    Debug.LogWarning($"[Baker] IPL '{ipl.name}': {brokenHd} broken HD links (HD deduped), {brokenLod} broken LOD links (LOD deduped)");

                // Write child counts (engine +0x61) and child lod distance (RAGE: m_childLodDistance)
                foreach (var kv in childCounts)
                {
                    if (iplEntityMap.TryGetValue(kv.Key, out var lodEntity))
                    {
                        em.SetComponentData(lodEntity, new LodChildCount { Value = kv.Value });
                        if (childMaxDist.TryGetValue(kv.Key, out float maxDist))
                            em.AddComponentData(lodEntity, new ChildLodDist { Value = maxDist });
                    }
                }

                // --- LOD draw distance propagation (engine: SetupLodHierarchy) ---
                // Propagate max(children drawDist) upward through the chain, capped at 600.
                // Iterate until stable so deep chains HD→LOD1→LOD2 fully propagate.
                const float LodDrawDistCap = 600f;
                int propPasses = 0, propUpdated = 0;
                for (int pass = 0; pass < 10; pass++)
                {
                    bool changed = false;
                    foreach (var kv in iplInstMap)
                    {
                        int lodIdx = kv.Value.lod;
                        if (lodIdx < 0) continue;
                        if (!iplEntityMap.TryGetValue(kv.Key, out var childEntity)) continue;
                        if (!iplEntityMap.TryGetValue(lodIdx, out var parentEntity)) continue;

                        float childDist = em.GetComponentData<DrawDist>(childEntity).Value;
                        if (childDist > LodDrawDistCap) childDist = LodDrawDistCap;

                        float parentDist = em.GetComponentData<DrawDist>(parentEntity).Value;
                        if (childDist > parentDist)
                        {
                            em.SetComponentData(parentEntity, new DrawDist { Value = childDist });
                            changed = true;
                            propUpdated++;
                        }
                    }
                    propPasses++;
                    if (!changed) break;
                }
                if (propUpdated > 0)
                    Debug.Log($"[Baker] IPL '{ipl.name}': DrawDist propagation: {propUpdated} updates in {propPasses} passes");

                // --- LOD type classification (RAGE: eLodType) ---
                // GTA IV doesn't store lodLevel in map data like GTA V.
                // Classification by model name convention:
                //   "SLOD"/"slod" → SLOD
                //   "LOD"/"lod" prefix/suffix → LOD
                //   everything else → HD or OrphanHD
                foreach (var kv in iplEntityMap)
                {
                    int instIdx = kv.Key;
                    Entity entity = kv.Value;
                    string modelName = iplInstMap.TryGetValue(instIdx, out var inst) ? inst.name : "";

                    LodType type;
                    if (ContainsSlod(modelName))
                        type = LodType.SLOD;
                    else if (ContainsLod(modelName))
                        type = LodType.LOD;
                    else if (em.HasComponent<LodRef>(entity))
                        type = LodType.HD;
                    else
                        type = LodType.OrphanHD;

                    em.AddComponentData(entity, new LodLevel { Value = type });
                }

                LoadingScreen.AdvanceProgress(ipl.name, created - batchStart);
            }

            Debug.Log(
                $"[Baker] Created {created} root entities " +
                $"(skipped {skippedUnresolved} unresolved, {skippedTobj} TOBJ, {skippedDuplicate} duplicate) " +
                $"LOD: {lodLinked} linked, {lodTagged} tagged  " +
                $"Draw distance propagation: multi-pass upward through chains");

            Debug.Log($"[Baker] StreamingIPLs: {StreamingIplRegistry.StreamingWplCount} streaming, " +
                      $"{StreamingIplRegistry.BaseWplNames.Count} base (gta.dat)");

            Debug.Log($"[Baker] MLO: {loader.ideLoader.mloDict.Count} definitions loaded");
        }
    }
}

