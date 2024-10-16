using RageLib.Common;
using RageLib.FileSystem;
using RageLib.FileSystem.Common;
using System.Threading.Tasks;
using UnityEngine;

public class Loader : MonoBehaviour
{
    public static Loader Instance;
    public string gameDir;
    public LoadingScreen loadingScreen;
    public WorldComposerMachine composer;

    //GTA IV
    private byte[] key;
    private KeyUtil keyUtil;
    private FileSystem fs;
    private GTADatLoader gameLoader;
    private void Awake()
    {
        Instance = this;
    }

    private async void Start()
    {
        await Init();
    }

    public async Task Init()
    {
        fs = new RealFileSystem();

        keyUtil = new KeyUtilGTAIV();
        key = keyUtil.FindKey(gameDir);
        if (key == null)
        {
            Debug.LogError("GTA IV version couldn't be detected");
        }

        KeyStore.SetKeyLoader(() => key);

        fs.Open(gameDir);

        gameLoader = new GTADatLoader(gameDir, (RealFileSystem)fs);


        await gameLoader.LoadGameFiles(() => { MainThreadDispatcher.ExecuteOnMainThread(() => { composer.ComposeWorld(gameLoader); }); });

    }
}
