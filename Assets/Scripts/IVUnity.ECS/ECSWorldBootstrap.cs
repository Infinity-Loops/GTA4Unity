using System.Threading.Tasks;
using RageLib.Common;
using RageLib.FileSystem;
using Unity.Entities;
using UnityEngine;
using Directory = RageLib.FileSystem.Common.Directory;
using File = RageLib.FileSystem.Common.File;

namespace IVUnity.ECS
{
    /// <summary>
    /// Self-contained scene entry point for the ECS world. Owns the entire bootstrap chain:
    ///   1. Set up RageLib filesystem + GTA IV encryption key (replaces Loader.Init).
    ///   2. Load loading-screen textures via GTADatLoader + LoadingScreen.SetupLoadingImages.
    ///   3. GTADatLoader.LoadGameFiles — parses IMG/IDE/IPL/water.
    ///   4. Resolver init — V1 pre-indexes textures globally; V2 wires a lazy TxdStore.
    ///   5. WorldEntityBaker.Bake — one entity per Ipl_INST.
    ///   6. WaterBuilder.Build — water GameObject.
    ///   7. Configure ECS systems with MeshCache / ModelLoader / ModelCatalog.
    ///   8. GameCore.SetReady — UI handoff.
    ///
    /// Deliberately does NOT reference or depend on Loader.cs. The legacy scene still uses
    /// Loader.cs + HighPerformanceLoader; the ECS scene uses this class instead. They are
    /// independent entry points into the same parsing stack.
    /// </summary>
    public class ECSWorldBootstrap : MonoBehaviour
    {
        [Header("Game install")]
        [Tooltip("Path to the GTA IV install directory (same value Loader.cs uses on the legacy scene).")]
        public string gameDir;

        [Header("Resolver")]

        public ModelCatalog Catalog     { get; private set; }
        public MeshCache    MeshCache   { get; private set; }
        public ModelLoader  ModelLoader { get; private set; }

        private GTADatLoader gameLoader;
        private RealFileSystem fs;

        private async void Start()
        {
            try
            {
                await BootstrapAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ECSWorldBootstrap] Bootstrap failed: {e}");
            }
        }

        private async Task BootstrapAsync()
        {
            if (string.IsNullOrEmpty(gameDir))
            {
                Debug.LogError("[ECSWorldBootstrap] gameDir is empty - set it in the Inspector");
                return;
            }

            fs = new RealFileSystem();
            var keyUtil = new KeyUtilGTAIV();
            byte[] key = keyUtil.FindKey(gameDir);
            if (key == null)
            {
                Debug.LogError("[ECSWorldBootstrap] GTA IV version couldn't be detected at " + gameDir);
                return;
            }
            KeyStore.SetKeyLoader(() => key);
            fs.Open(gameDir);

            SetupLoadingScreenImages(fs);

            await Awaitable.NextFrameAsync();
            
            // --- 3. Parse gta.dat + IMGs + IDEs + IPLs + water (managed, awaitable) ---
            gameLoader = new GTADatLoader(gameDir, fs);
            await gameLoader.LoadGameFiles(() => { /* legacy callback; ECS finalizes below */ });
            
            // --- 4. Resolver init ---
            LoadingScreen.AdvanceProgress("Configuring material resolver...", 0);
            IVUnity.Resolver.MaterialResolver.Configure(gameLoader);
            
            // --- 5..8 on main thread ---
            BakeAndFinalize();
        }

        private void BakeAndFinalize()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            Catalog     = new ModelCatalog();
            MeshCache   = new MeshCache();
            ModelLoader = new ModelLoader(maxParallel: 8);

            CollisionBuilder.StartBuild(this, gameLoader, transform);
            WorldEntityBaker.Bake(em, gameLoader, Catalog, StreamingConfig.Default.CellSize);
            IVUnity.WaterBuilder.Build(gameLoader.waterPlanes, gameLoader.root, transform);
            // IVUnity.CollisionDebugRenderer.RenderAll(gameLoader, transform);

            // Wire per-system managed dependencies. GetExistingSystemManaged is null-safe:
            // if a system type hasn't been created yet for this World, we skip gracefully.
            TryConfigureDispatch(world);
            TryConfigureUpload(world);
            TryConfigurePromotion(world);
            TryConfigureUnload(world);
            TryConfigureEviction(world);

            // Hand the UI transition to InitialLoadGateSystem — it keeps the loading screen
            // up until the initial batch of entities has actually promoted to Loaded, then
            // calls LoadingScreen.Finish + GameCore.SetReady (publishes GameReadyTag).
            var gate = world.GetExistingSystemManaged<InitialLoadGateSystem>();
            if (gate != null) gate.Arm();
            else
            {
                // Fallback if the gate system isn't in this world: release immediately.
                LoadingScreen.Finish();
                GameCore.SetReady();
            }
            Debug.Log("[ECSWorldBootstrap] Bake finished, awaiting initial load for UI handoff");
        }

        private void SetupLoadingScreenImages(RealFileSystem fileSystem)
        {
            try
            {
                Directory pcDirectory = (Directory)fileSystem.RootDirectory.FindByName("pc");
                Directory texturesDirectory = (Directory)pcDirectory.FindByName("textures");
                File loadingScreenFile = (File)texturesDirectory.FindByName("loadingscreens.wtd");
                LoadingScreen.SetupLoadingImages(loadingScreenFile);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ECSWorldBootstrap] Loading-screen textures unavailable: " + e.Message);
            }
        }

        private void TryConfigureDispatch(World world)
        {
            var s = world.GetExistingSystemManaged<ModelLoadDispatchSystem>();
            if (s != null) s.Configure(Catalog, MeshCache, ModelLoader);
        }
        private void TryConfigureUpload(World world)
        {
            var s = world.GetExistingSystemManaged<MainThreadMeshUploadSystem>();
            if (s != null) s.Configure(ModelLoader, MeshCache);
        }
        private void TryConfigurePromotion(World world)
        {
            var s = world.GetExistingSystemManaged<InstancePromotionSystem>();
            if (s != null) s.Configure(MeshCache);
        }
        private void TryConfigureUnload(World world)
        {
            var s = world.GetExistingSystemManaged<InstanceUnloadSystem>();
            if (s != null) s.Configure(MeshCache);
        }
        private void TryConfigureEviction(World world)
        {
            var s = world.GetExistingSystemManaged<MeshCacheEvictionSystem>();
            if (s != null) s.Configure(MeshCache);
        }

        private void OnDestroy()
        {
            ModelLoader?.Dispose();
        }
    }
}
