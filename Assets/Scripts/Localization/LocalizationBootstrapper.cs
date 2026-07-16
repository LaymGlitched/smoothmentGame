using UnityEngine;
using Reiteki.Localization.Core;

namespace Reiteki.Localization
{
    /// <summary>
    /// A simple bootstrapper to initialize the LocalizationManager in a scene.
    /// Place this on an empty GameObject (e.g., "GameManager" or "Services") in your initial scene.
    /// </summary>
    public class LocalizationBootstrapper : MonoBehaviour
    {
        // For simplicity in this example, we provide global access to the manager.
        // If you are using Dependency Injection (like VContainer or Zenject), 
        // you would bind LocalizationManager there instead of using a static property.
        public static LocalizationManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private string defaultLocale = "en-US";

        private async void Awake()
        {
            // Ensure only one instance exists if the scene reloads
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = new LocalizationManager();
            DontDestroyOnLoad(gameObject);

            // Subscribe to the LocaleChanged event to update UI when hot-reloading finishes
            Instance.LocaleChanged += OnLocaleChanged;

            // Start loading the locale. 
            // This is asynchronous, but won't block the main thread.
            await Instance.LoadLocale(defaultLocale);
        }

        private void Start()
        {
            // Wire dependencies to the Spirit System in Start to ensure SpiritDialogueCoordinator's Awake has finished.
            if (GameCode.Spirits.Communication.SpiritDialogueCoordinator.Instance != null)
            {
                GameCode.Spirits.Communication.SpiritDialogueCoordinator.Instance.InitializeLocalization(Instance);
            }
        }

        private void OnLocaleChanged()
        {
            Debug.Log($"[LocalizationBootstrapper] Locale data is ready or was just updated!");
            
            // Example usage:
            // string translatedWarning = Instance.Get("zenka.warning.vesselsafety.01");
            // Debug.Log(translatedWarning);
        }

        private void OnDestroy()
        {
            if (Instance != null)
            {
                Instance.LocaleChanged -= OnLocaleChanged;
            }
        }
    }
}
