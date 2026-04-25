using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVUnity.Resolver
{
    /// <summary>
    /// Engine-leaning material construction with content-keyed deduplication.
    ///
    /// Why not pure per-submesh fresh materials (the strict engine analogue): Unity's
    /// BatchRendererGroup material pool is finite. A world with thousands of submeshes
    /// across hundreds of models would exhaust the pool and RegisterMaterial would start
    /// failing — taking down whole mesh uploads with it.
    ///
    /// Why not the V1 string-name cache: keying by shader+textureName let two models that
    /// resolved the same texture name to *different* TXD chains share a single Material.
    /// First-arrival wins set the material's _MainTex once and later models inherited it,
    /// producing the "spawn at A, walk to B, lose textures" symptom.
    ///
    /// Compromise here: key the cache by the actual <see cref="Texture2D"/> reference tuple.
    /// Same texture instances → same Material. Two models that resolved the same name to
    /// different textures (e.g. one had the chain, one didn't) get *different* materials —
    /// no cross-contamination. Same models that resolved the same names to the same
    /// textures share a Material and a BatchMaterialID — pool stays small.
    ///
    /// The texture refs themselves come from <c>ModelGenerator.textureCache</c>, which is
    /// process-static and name-keyed. Same texture name decoded once → same Texture2D
    /// returned for every subsequent lookup, so dedupe collapses naturally.
    ///
    /// Materials are never evicted: textures are stable for session lifetime
    /// (ModelGenerator's cache is never cleared) so the material set is bounded by the
    /// world's unique shader+texture-ref combinations. <see cref="MeshCacheEvictionSystem"/>
    /// must NOT UnregisterMaterial in the V2 path — other live models share the slot.
    /// </summary>
    public static class MaterialTextureResolverV2
    {
        public static bool     IsActive { get; private set; }
        public static TxdStore TxdStore { get; private set; }

        private static readonly Dictionary<MaterialKey, Material> cache =
            new Dictionary<MaterialKey, Material>(MaterialKey.Comparer);
        private static readonly object cacheLock = new object();

        public static void Configure(GTADatLoader dataLoader)
        {
            if (dataLoader == null) throw new ArgumentNullException(nameof(dataLoader));
            var txdParents = dataLoader.ideLoader?.txdParents;
            TxdStore = new TxdStore(dataLoader.gameFiles, txdParents);
            IsActive = true;

            lock (cacheLock) cache.Clear();
        }

        public static void Reset()
        {
            IsActive = false;
            TxdStore = null;
            lock (cacheLock) cache.Clear();
        }

        public static Material Build(string shaderName, Texture2D mainTex, Texture2D normalTex, Texture2D specularTex)
        {
            var key = new MaterialKey(shaderName, mainTex, normalTex, specularTex);

            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out var existing) && existing != null) return existing;

                var mat = new Material(Shader.Find("gta_default"));
                mat.enableInstancing = true;
                if (mainTex     != null) mat.SetTexture("_MainTex",      mainTex);
                if (normalTex   != null) mat.SetTexture("_BumpMap",      normalTex);
                if (specularTex != null) mat.SetTexture("_SpecGlossMap", specularTex);
                cache[key] = mat;
                return mat;
            }
        }

        /// <summary>
        /// Build a multi-layer terrain material. <paramref name="shaderName"/> is the RAGE
        /// shader name (e.g. "gta_terrain_va_3lyr"); the matching Unity shader is loaded
        /// directly. Layers 0..3 are passed to _MainTex / _Layer1 / _Layer2 / _Layer3 in the
        /// HLSL shaders. Caller fills only the layers it has — anything past the shader's
        /// layer count is ignored.
        /// </summary>
        public static Material BuildTerrain(string shaderName, Texture2D[] layers)
        {
            var key = new MaterialKey(
                shaderName,
                layers != null && layers.Length > 0 ? layers[0] : null,
                layers != null && layers.Length > 1 ? layers[1] : null,
                layers != null && layers.Length > 2 ? layers[2] : null,
                layers != null && layers.Length > 3 ? layers[3] : null);

            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out var existing) && existing != null) return existing;

                var shader = Shader.Find(shaderName) ?? Shader.Find("gta_default");
                var mat = new Material(shader);
                mat.enableInstancing = true;
                if (layers != null)
                {
                    if (layers.Length > 0 && layers[0] != null) mat.SetTexture("_MainTex", layers[0]);
                    if (layers.Length > 1 && layers[1] != null) mat.SetTexture("_Layer1",  layers[1]);
                    if (layers.Length > 2 && layers[2] != null) mat.SetTexture("_Layer2",  layers[2]);
                    if (layers.Length > 3 && layers[3] != null) mat.SetTexture("_Layer3",  layers[3]);
                }
                cache[key] = mat;
                return mat;
            }
        }

        public static int CachedMaterialCount { get { lock (cacheLock) return cache.Count; } }

        // Reference-equality on textures + ordinal-equality on shader. Generic 4-slot tuple
        // so it works for both gta_default (Main + Normal + Specular, slot4 unused) and
        // terrain shaders (Main + Layer1..3, normal/spec re-purposed). Shader name is part
        // of the key so identical texture sets under different shaders don't collide.
        private readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            public readonly string Shader;
            public readonly Texture2D Slot0;   // _MainTex / Layer 0
            public readonly Texture2D Slot1;   // _BumpMap or _Layer1
            public readonly Texture2D Slot2;   // _SpecGlossMap or _Layer2
            public readonly Texture2D Slot3;   // _Layer3 (terrain-4lyr only; null otherwise)

            public MaterialKey(string shader, Texture2D s0, Texture2D s1, Texture2D s2, Texture2D s3 = null)
            { Shader = shader ?? string.Empty; Slot0 = s0; Slot1 = s1; Slot2 = s2; Slot3 = s3; }

            public bool Equals(MaterialKey other) =>
                string.Equals(Shader, other.Shader, StringComparison.Ordinal) &&
                ReferenceEquals(Slot0, other.Slot0) &&
                ReferenceEquals(Slot1, other.Slot1) &&
                ReferenceEquals(Slot2, other.Slot2) &&
                ReferenceEquals(Slot3, other.Slot3);

            public override bool Equals(object obj) => obj is MaterialKey k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Shader.GetHashCode();
                    h = (h * 397) ^ (Slot0 != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Slot0) : 0);
                    h = (h * 397) ^ (Slot1 != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Slot1) : 0);
                    h = (h * 397) ^ (Slot2 != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Slot2) : 0);
                    h = (h * 397) ^ (Slot3 != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Slot3) : 0);
                    return h;
                }
            }

            public static readonly IEqualityComparer<MaterialKey> Comparer = EqualityComparer<MaterialKey>.Default;
        }
    }
}
