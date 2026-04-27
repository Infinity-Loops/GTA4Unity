using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVUnity.Resolver
{
    /// <summary>
    /// Material construction with content-keyed deduplication and per-shader resolution.
    ///
    /// Cache key: actual <see cref="Texture2D"/> reference tuple + shader name.
    /// Same texture instances → same Material → shared BatchMaterialID.
    /// Different texture resolutions (e.g. different TXD chains) → different Materials.
    ///
    /// Shader resolution: RAGE shader name (e.g. "gta_normal_spec") → "GTA IV/gta_normal_spec".
    /// Missing shaders are logged once and fall back to gta_default.
    /// </summary>
    public static class MaterialResolver
    {
        public static bool     IsActive { get; private set; }
        public static TxdStore TxdStore { get; private set; }

        private static readonly Dictionary<MaterialKey, Material> cache =
            new Dictionary<MaterialKey, Material>(MaterialKey.Comparer);
        private static readonly object cacheLock = new object();

        private static readonly HashSet<string> loggedMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            loggedMissing.Clear();
        }

        public static Material Build(string shaderName, Texture2D mainTex, Texture2D normalTex, Texture2D specularTex)
        {
            var key = new MaterialKey(shaderName, mainTex, normalTex, specularTex);

            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out var existing) && existing != null) return existing;

                var shader = ResolveShader(shaderName);
                var mat = new Material(shader);
                mat.enableInstancing = true;
                if (mainTex     != null) mat.SetTexture("_MainTex", mainTex);
                if (normalTex   != null) mat.SetTexture("_BumpMap", normalTex);
                if (specularTex != null) mat.SetTexture("_SpecTex", specularTex);
                cache[key] = mat;
                return mat;
            }
        }

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

                var shader = ResolveShader(shaderName);
                var mat = new Material(shader);
                mat.enableInstancing = true;
                if (layers != null)
                {
                    if (layers.Length > 0 && layers[0] != null) mat.SetTexture("_Layer0Tex", layers[0]);
                    if (layers.Length > 1 && layers[1] != null) mat.SetTexture("_Layer1Tex", layers[1]);
                    if (layers.Length > 2 && layers[2] != null) mat.SetTexture("_Layer2Tex", layers[2]);
                    if (layers.Length > 3 && layers[3] != null) mat.SetTexture("_Layer3Tex", layers[3]);
                }
                cache[key] = mat;
                return mat;
            }
        }

        public static int CachedMaterialCount { get { lock (cacheLock) return cache.Count; } }

        private static Shader ResolveShader(string rageShaderName)
        {
            if (string.IsNullOrEmpty(rageShaderName))
                rageShaderName = "gta_default";

            var shader = Shader.Find("GTA IV/" + rageShaderName);
            if (shader != null) return shader;

            shader = Shader.Find(rageShaderName);
            if (shader != null) return shader;

            if (loggedMissing.Add(rageShaderName))
                Debug.LogWarning($"[MaterialResolver] Missing shader: '{rageShaderName}' — falling back to gta_default");

            return Shader.Find("GTA IV/gta_default");
        }

        private readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            public readonly string Shader;
            public readonly Texture2D Slot0;
            public readonly Texture2D Slot1;
            public readonly Texture2D Slot2;
            public readonly Texture2D Slot3;

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
