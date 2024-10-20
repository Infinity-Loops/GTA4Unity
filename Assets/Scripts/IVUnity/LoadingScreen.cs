using RageLib.FileSystem.Common;
using RageLib.Textures;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using File = RageLib.FileSystem.Common.File;

public class LoadingScreen : MonoBehaviour
{
    private static LoadingScreen instance;
    public GameObject loadingScreen;
    public Slider progressBar;
    public TMP_Text loadingText;
    public RawImage background;
    public RawImage character;
    public float characterMoveSpeed;
    public float switchLoadingScreenTime = 1f;
    private TextureFile loadingScreenTextures;
    private bool screenTexturesLoaded;
    private bool moveCharacterOnLoadScreen;
    private int loadingTypesCount = 0;
    private List<Texture2D> backgroundTextures = new List<Texture2D>();
    private List<Texture2D> characterTextures = new List<Texture2D>();
    private float loadingScreenSwitchTimer;

    private void Awake()
    {
        instance = this;
    }


    private void SetupScreenLoadingImages()
    {


        if (screenTexturesLoaded)
        {
            if (loadingScreenTextures.Count > 0)
            {
                var orderedTextures = loadingScreenTextures.Textures.OrderBy(x => x.Name).ToList();
                int maxCount = (orderedTextures.Count / 2) - 2;

                for (int i = 0; i < maxCount; i++)
                {
                    var backgroundIndex = i * 2;
                    var characterIndex = (i * 2) + 1;

                    if (backgroundIndex < orderedTextures.Count && characterIndex < orderedTextures.Count)
                    {
                        backgroundTextures.Add(orderedTextures[backgroundIndex].Decode().GetUnityTexture());
                        characterTextures.Add(orderedTextures[characterIndex].Decode().GetUnityTexture());
                    }
                }

                loadingTypesCount = maxCount;
            }
        }

        int randomScreenSelected = Random.Range(0, loadingTypesCount);
        var backgroundTex = backgroundTextures[randomScreenSelected];
        var characterTex = characterTextures[randomScreenSelected];

        background.texture = backgroundTex;
        character.texture = characterTex;
        loadingScreenSwitchTimer = Time.time + switchLoadingScreenTime;
        moveCharacterOnLoadScreen = true;
    }

    private void Update()
    {
        if (moveCharacterOnLoadScreen)
        {
            var position = character.rectTransform.anchoredPosition;
            position.x += characterMoveSpeed * Time.deltaTime;
            character.rectTransform.anchoredPosition = position;
            
            if(Time.time > loadingScreenSwitchTimer)
            {
                loadingScreenSwitchTimer = Time.time + switchLoadingScreenTime;

                var pos = character.rectTransform.anchoredPosition;
                pos.x = 512;
                character.rectTransform.anchoredPosition = pos;

                int randomScreenSelected = Random.Range(0, loadingTypesCount);
                var backgroundTex = backgroundTextures[randomScreenSelected];
                var characterTex = characterTextures[randomScreenSelected];

                background.texture = backgroundTex;
                character.texture = characterTex;
            }
        }
    }

    public static void SetupLoadingImages(File textureFile)
    {
        MainThreadDispatcher.ExecuteOnMainThread(() =>
        {
            using (MemoryStream memoryStream = new MemoryStream(textureFile.GetData()))
            {
                instance.loadingScreenTextures = new TextureFile();
                instance.loadingScreenTextures.Open(memoryStream);
                instance.screenTexturesLoaded = true;
                instance.SetupScreenLoadingImages();
            }
        });
    }

    public static void SetupLoadingTarget(int max)
    {
        MainThreadDispatcher.ExecuteOnMainThread(() =>
        {
            instance.progressBar.maxValue = max;
        });
    }

    public static void Finish()
    {
        MainThreadDispatcher.ExecuteOnMainThread(() =>
        {
            instance.loadingScreen.SetActive(false);
        });
    }

    public static void ResetProgress()
    {
        MainThreadDispatcher.ExecuteOnMainThread(() =>
        {
            instance.progressBar.value = 0;
        });
    }

    public static void AdvanceProgress(string text)
    {
        MainThreadDispatcher.ExecuteOnMainThread(() =>
        {
            instance.loadingText.text = $"Loading {text}";
            instance.progressBar.value++;
        });
    }


    public static void AdvanceProgress(string text, int count)
    {
        MainThreadDispatcher.ExecuteOnMainThread(() =>
        {
            instance.loadingText.text = $"Loading {text}";
            instance.progressBar.value += count;
        });
    }
}
