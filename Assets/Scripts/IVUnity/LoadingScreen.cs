using IVUnity.ECS.UI;
using File = RageLib.FileSystem.Common.File;

/// <summary>
/// Static facade over the ECS-driven loading pipeline.
/// Existing callers (GTADatLoader, ECSWorldBootstrap, WorldEntityBaker, etc.) keep their
/// call sites unchanged: LoadingScreen.SetupLoadingTarget, AdvanceProgress, Finish, etc.
///
/// Underneath, progress values are written to LoadingState (thread-safe static), then
/// bridged into the LoadingProgress ECS singleton by LoadingBridgeSystem each frame.
/// LoadingViewSystem reads that singleton on the main thread and drives LoadingScreenView
/// (UI Toolkit renderer). Nothing here touches UnityEngine.UI or blocks on a dispatcher.
/// </summary>
public static class LoadingScreen
{
    public static void SetupLoadingImages(File textureFile)
    {
        var view = LoadingScreenView.Instance;
        if (view != null) view.LoadSplashTextures(textureFile);
    }

    public static void SetupLoadingTarget(int max) => LoadingState.SetTarget(max);
    public static void ResetProgress()              => LoadingState.ResetProgress();
    public static void AdvanceProgress(string text) => LoadingState.Advance(text);
    public static void AdvanceProgress(string text, int count) => LoadingState.Advance(text, count);
    public static void Finish()                     => LoadingState.Finish();
}
