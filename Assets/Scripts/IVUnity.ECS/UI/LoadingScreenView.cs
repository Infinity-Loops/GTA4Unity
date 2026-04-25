using System.Collections.Generic;
using System.IO;
using System.Linq;
using RageLib.Textures;
using UnityEngine;
using UnityEngine.UIElements;
using File = RageLib.FileSystem.Common.File;
using Random = UnityEngine.Random;

namespace IVUnity.ECS.UI
{
    /// <summary>
    /// UI Toolkit renderer for the loading screen. Purely a view: receives values via
    /// Render(...) (from LoadingViewSystem) and mutates VisualElement properties. All
    /// UI Toolkit access is main-thread; writers never touch this class directly.
    ///
    /// Required scene setup:
    ///   GameObject with:
    ///     - UIDocument        (Source Asset: LoadingScreen.uxml, Panel Settings: your default)
    ///     - LoadingScreenView (this component)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoadingScreenView : MonoBehaviour
    {
        public static LoadingScreenView Instance { get; private set; }

        [Header("Animation")]
        [SerializeField] private float characterMoveSpeed = 25f;
        [SerializeField] private float switchLoadingScreenTime = 5f;

        [Header("Stylesheet (LoadingScreen.uss)")]
        [SerializeField] private StyleSheet styleSheet;

        [Header("Element names in UXML")]
        [SerializeField] private string rootName       = "loading-root";
        [SerializeField] private string backgroundName = "background";
        [SerializeField] private string characterName  = "character";
        [SerializeField] private string progressName   = "progress-bar";
        [SerializeField] private string textName       = "loading-text";

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement backgroundElement;
        private VisualElement characterElement;
        private ProgressBar progressBar;
        private Label loadingText;

        private readonly List<Texture2D> backgroundTextures = new List<Texture2D>();
        private readonly List<Texture2D> characterTextures  = new List<Texture2D>();
        private bool splashesReady;
        private float characterX;
        private float switchTimer;
        private bool hidden;

        private void Awake()
        {
            Instance = this;
            uiDocument = GetComponent<UIDocument>();
        }

        /// <summary>
        /// Lazily resolve VisualElement references. UIDocument builds its element tree in
        /// its own OnEnable; if it hasn't run yet (Awake/OnEnable ordering varies per
        /// build/Play), queries return null. Retry from Render/Update.
        /// </summary>
        private bool TryResolveElements()
        {
            if (root != null && backgroundElement != null) return true;

            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return false;

            root = uiDocument.rootVisualElement;
            if (root == null) return false;

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            backgroundElement = root.Q<VisualElement>(backgroundName);
            characterElement  = root.Q<VisualElement>(characterName);
            progressBar       = root.Q<ProgressBar>(progressName);
            loadingText       = root.Q<Label>(textName);
            return backgroundElement != null; // treat as resolved when we have at least the background
        }

        private void Update()
        {
            if (hidden) return;
            if (!TryResolveElements()) return;
            if (!splashesReady || characterElement == null) return;

            characterX += characterMoveSpeed * Time.deltaTime;
            characterElement.style.left = characterX;

            if (Time.time > switchTimer)
            {
                switchTimer = Time.time + switchLoadingScreenTime;
                characterX = 0f;
                characterElement.style.left = characterX;
                ShuffleSplash();
            }
        }

        // =========================================================================
        //  Called by LoadingViewSystem — pure data → view mapping
        // =========================================================================
        public void Render(int current, int total, string label, bool finished)
        {
            if (!TryResolveElements()) return;

            if (progressBar != null)
            {
                progressBar.lowValue  = 0;
                progressBar.highValue = Mathf.Max(1, total);
                progressBar.value     = current;
            }
            if (loadingText != null)
            {
                loadingText.text = string.IsNullOrEmpty(label) ? "Loading..." : $"Loading {label}";
            }

            if (finished && !hidden)
            {
                hidden = true;
                root.style.display = DisplayStyle.None;
            }
            else if (!finished && hidden)
            {
                hidden = false;
                root.style.display = DisplayStyle.Flex;
            }
        }

        // =========================================================================
        //  Splash texture loading (called once at bootstrap from the main thread)
        // =========================================================================
        public void LoadSplashTextures(File textureFile)
        {
            if (textureFile == null) return;

            using var memStream = new MemoryStream(textureFile.GetData());
            var tex = new TextureFile();
            tex.Open(memStream);

            var ordered = tex.Textures.OrderBy(t => t.Name).ToList();
            int pairCount = Mathf.Max(0, (ordered.Count / 2) - 2);
            for (int i = 0; i < pairCount; i++)
            {
                int bg = i * 2;
                int ch = i * 2 + 1;
                if (bg < ordered.Count && ch < ordered.Count)
                {
                    backgroundTextures.Add(ordered[bg].Decode().GetUnityTexture());
                    characterTextures.Add(ordered[ch].Decode().GetUnityTexture());
                }
            }

            splashesReady = pairCount > 0;
            if (splashesReady) ShuffleSplash();
            switchTimer = Time.time + switchLoadingScreenTime;
        }

        private void ShuffleSplash()
        {
            if (backgroundTextures.Count == 0) return;
            int idx = Random.Range(0, backgroundTextures.Count);
            if (backgroundElement != null) backgroundElement.style.backgroundImage = new StyleBackground(backgroundTextures[idx]);
            if (characterElement != null)  characterElement.style.backgroundImage  = new StyleBackground(characterTextures[idx]);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
