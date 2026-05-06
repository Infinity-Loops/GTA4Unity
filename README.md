# GTA4Unity

Loads GTA IV's world into Unity 6 using the Entity Component System. Reads the game's original files directly (IMG archives, WDR/WFT models, WTD textures, WPL/IPL placements, IDE definitions) and renders them through Entities Graphics (BatchRendererGroup).

Requires a licensed GTA IV installation. No game assets are included.

## Community

Join the Discord server: https://discord.gg/AArdEqjuY

![](Screenshots/Screenshot.png)
![](Screenshots/Screenshot2.png)
![](Screenshots/Screenshot3.png)
![](Screenshots/Screenshot4.png)
![](Screenshots/Screenshot5.png)

## Architecture

### Boot sequence

`ECSWorldBootstrap` drives the full load pipeline:

1. Initialize RageLib filesystem with GTA IV encryption key
2. `GTADatLoader` reads `gta.dat` - opens IMG archives, parses all IDE and IPL/WPL files, loads water definitions
3. Texture resolver initializes - wires the lazy TxdStore with IDE-driven parent chains
4. `WorldEntityBaker` creates one ECS entity per world placement (Ipl_INST)
5. `WaterBuilder` generates water meshes
6. ECS systems start streaming

### File loading order

GTA IV's world data is spread across several interdependent file types. The load order matters:

1. **IMG archives** - Container files (`.img`) holding models, textures, and placements. Opened first so everything inside is accessible by path.
2. **IDE files** - Item definitions. Each IDE declares object types (`objs`, `tobj`, `anim`), MLO interiors (`mlo`), texture dictionary parents (`txdp`), and more. Loaded second to build the full model/texture catalog before placements reference them.
3. **IPL / WPL files** - Item placements. Text IPL files and binary WPL files define where every object sits in the world (position, rotation, scale, LOD linkage). Loaded after IDE so each placement can resolve its model name to a definition.

### IDE item types

- **OBJS** - Static objects. Each entry names a model, texture dictionary, draw distances, and flags. The most common type - buildings, props, barriers, signs.
- **TOBJ** - Timed objects. Identical to OBJS but with an additional time-of-day flag controlling visibility (day-only, night-only).
- **ANIM** - Animated objects. Same fields as OBJS plus an animation dictionary name. Parsed identically for placement purposes.
- **MLO** - Multi-part interiors. A multi-line block defining a building shell, its rooms, portals, and interior prop entities. Each prop has its own position and rotation relative to the building origin.
- **TXDP** - Texture dictionary parents. Maps a child TXD name to a parent TXD name, forming a lookup chain for texture resolution.

### IPL / WPL placements

Each placement (`Ipl_INST`) carries:

| Field | Description |
|-------|-------------|
| `name` / `hash` | Model name or Jenkins hash, resolved against the IDE catalog |
| `position` | RAGE world coordinates (x, y, z) |
| `rotation` | Stored quaternion (x, y, z, w) - XYZ are negated relative to the mathematical quaternion |
| `scale` | scaleXY / scaleZ |
| `lod` | Index of this entity's LOD parent (-1 if none) |
| `drawDistance` | Maximum visibility range in game units |

Binary WPL files store the same data in a packed binary format read from IMG archives. Text IPL files are referenced by `gta.dat` and parsed line-by-line.

### Coordinate conversion

RAGE uses a right-handed coordinate system (X-right, Y-forward, Z-up) with row-vector matrix convention (DX9). Unity uses left-handed (X-right, Y-up, Z-forward) with column-vector convention.

All conversions go through `RageCoordinates`, the single source of truth:

**Positions**: `RAGE(x, y, z)` → `Unity(-x, z, -y)`

**Rotations** (stored quaternion): `RAGE(sx, sy, sz, sw)` → `Unity(-sx, sz, -sy, sw)`

The rotation formula is derived from the similarity transformation `P R P⁻¹` where P is the position mapping matrix. Since P has determinant -1 (handedness flip), the quaternion axis gets mapped through P then negated - which produces the same `(-x, z, -y)` swizzle pattern applied to the imaginary components.

Mesh vertices and normals are converted during model loading in the `MeshDecodeJob`, with triangle winding reversed to correct face orientation after the handedness change.

RAGE stores entity quaternions with negated XYZ relative to the mathematical rotation. The conversion formula accounts for this.

For MLO interior props, `RageCoordinates.Compose()` composes parent (building) and child (prop) transforms in RAGE space before converting the result to Unity. The `RotationInternal()` variant handles quaternions already in mathematical form (from Euler angles or composition results).

**Ped forward direction**: RAGE Y+ (forward) maps to Unity `(0, 0, -1)` via `Position(-x, z, -y)`. The ped model's visual forward in Unity space is -Z. The entity's +Z is the character controller's facing, but the skeleton's forward is the opposite. This matters for procedural systems like knee IK hints that need the model's facing direction rather than the entity's.

### ECS streaming pipeline

The world contains 200K+ entities. The streaming pipeline manages which are visible:

```
Dormant → Pending → Loading → Loaded → Unloading → Dormant
```

| System | Group | Frequency | Role |
|--------|-------|-----------|------|
| **FocusPointSyncSystem** | Streaming | Every frame | Writes camera position to singleton |
| **StreamingDecisionSystem** | Streaming | Every frame (throttled) | Distance check: Dormant→Pending if in range, Loaded→Unloading if out of range. Deferred ECB playback. |
| **ModelLoadDispatchSystem** | Asset | Every frame | Picks Pending entities, sorts by distance, dispatches model loads to worker threads (budget-limited) |
| **MainThreadMeshUploadSystem** | Asset | Every frame | Drains worker results, builds Unity Meshes, registers with Entities Graphics |
| **InstancePromotionSystem** | Asset | Every frame | Creates rendered sub-mesh children for Pending entities whose model is ready (budget-limited) |
| **InstanceUnloadSystem** | Asset | Every frame | Destroys sub-mesh children of Unloading entities via Child buffer, decrements cache ref counts |
| **MeshCacheEvictionSystem** | Asset | Every 1s | LRU eviction of unused mesh/material registrations. Retries failed loads, times out stuck workers. |
| **TxdEvictionSystem** | Asset | Every 1s | Drops parsed TXD dictionaries whose ref count hit zero (LRU keepalive) |

### LOD fading system

`LodFadeSystem` runs every frame in the PresentationSystemGroup with three Burst-compiled `IJobChunk` passes:

1. **CountChildrenJob** - For HD entities that have a LOD parent, counts how many are within draw distance per LOD entity. Written to a persistent `NativeParallelHashMap`.
2. **ComputeAlphaJob** - For every loaded root entity, computes a stipple alpha value. Fast-path skips entities well inside draw distance. Entities near the draw distance edge get a distance fade. LOD/SLOD parents fade out as their HD children fade in. Only entities with alpha < 1.0 are written to the fading map (keeps the map small).
3. **WriteAlphaJob** - Parallel over all sub-mesh entities. Looks up the parent in the fading map. Compare-before-write avoids dirtying ECS chunks when alpha hasn't changed.

### Texture resolution

Each model's IDE entry names a texture dictionary (TXD). The `txdp` IDE section defines TXD-to-TXD parent relationships, forming a chain. When a shader references a texture name, the resolver walks the chain:

1. Look up the texture in the model's own TXD
2. If not found, walk to the parent TXD and repeat
3. Continue until found or the chain ends

This mirrors the engine's `CTxdStore` slot system. The `TxdStore` is ref-counted and lazily loaded - a TXD is only parsed when first needed, and evicted when no model references it.

### Fragment models

Fragment type models (`.wft`) contain a parent drawable plus child pieces positioned by skeleton bones. `ModelFlatten` walks the fragment tree: the parent drawable renders at identity, each child piece looks up its `BoneIndex` in the skeleton and uses that bone's absolute position and rotation (converted through `RageCoordinates`) as a local offset.

### Shaders

Custom shaders matching RAGE's original shader names handle the game's material types:

- `gta_default`, `gta_normal`, `gta_spec`, `gta_reflect` and their combinations
- `gta_emissive` / `gta_emissivenight` - emissive with optional night-only variants
- `gta_glass` variants - transparency with optional reflection, specular, emission
- `gta_cutout_fence` - alpha-tested cutout
- `gta_terrain_va_2lyr` / `3lyr` / `4lyr` - terrain blending with vertex color weights
- `gta_trees` / `gta_grass` - vegetation with wind animation
- `gta_decal` variants - decal projection
- `gta_parallax` - parallax occlusion mapping
- `water` / `waterTex` - water surface rendering
- `ProceduralSky` - atmosphere scattering with layered clouds

## Setup

1. Unity 6 (6000.4.0f1+), URP, Forward+
2. Clone and open in Unity Hub
3. Open the `ECSWorld` scene
4. Set `gameDir` on the `ECSWorldBootstrap` component to your GTA IV install path
5. Play

## Status

World geometry, textures, LOD streaming, MLO interiors, and fragment models load and render correctly. Not a complete game - no physics, vehicles, peds, or gameplay yet.

## Requirements

- Windows
- GTA IV installed (Complete Edition, Steam, or EFLC)
- 8GB+ RAM
