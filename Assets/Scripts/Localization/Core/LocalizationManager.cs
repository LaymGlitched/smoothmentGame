using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reiteki.Localization.Providers;
using UnityEngine;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// The central manager that orchestrates the localization providers and serves localized strings.
    /// </summary>
    public class LocalizationManager
    {
        private Dictionary<string, LocalizedEntry> _currentLocaleData = new Dictionary<string, LocalizedEntry>();
        private string _currentLocale;
        private HashSet<string> _missingKeys = new HashSet<string>();

        private readonly ILocalizationProvider _cachedProvider;
        private readonly ILocalizationProvider _streamingAssetsProvider;
        private readonly ILocalizationProvider _gitHubProvider;

        /// <summary>
        /// Triggered whenever the localization data is loaded or updated (e.g., from GitHub hot-reload).
        /// </summary>
        public event Action LocaleChanged;

        /// <summary>
        /// Initializes a new LocalizationManager with default providers.
        /// The caller is responsible for maintaining the lifetime of this manager (e.g. via Dependency Injection or Service Locator).
        /// </summary>
        public LocalizationManager()
        {
            _cachedProvider = new CachedLocalizationProvider();
            _streamingAssetsProvider = new StreamingAssetsLocalizationProvider();
            _gitHubProvider = new GitHubLocalizationProvider();
        }

        /// <summary>
        /// Initializes a new LocalizationManager with custom providers (for testing or extensibility).
        /// </summary>
        public LocalizationManager(ILocalizationProvider cachedProvider, ILocalizationProvider streamingAssetsProvider, ILocalizationProvider gitHubProvider)
        {
            _cachedProvider = cachedProvider;
            _streamingAssetsProvider = streamingAssetsProvider;
            _gitHubProvider = gitHubProvider;
        }

        /// <summary>
        /// Asynchronously loads a locale following the startup flow:
        /// 1. Try Cache
        /// 2. If no cache, try StreamingAssets
        /// 3. In the background, check GitHub and hot-reload if a newer version is downloaded.
        /// </summary>
        /// <param name="locale">The locale code, e.g., "en-US".</param>
        public async Task LoadLocale(string locale)
        {
            _currentLocale = locale;
            _missingKeys.Clear();
            bool loadedOffline = false;

            // 1. Try Cache
            var cacheData = await _cachedProvider.LoadLocaleAsync(locale);
            if (cacheData != null && cacheData.Count > 0)
            {
                _currentLocaleData = cacheData;
                loadedOffline = true;
                Debug.Log($"[Localization] Loaded '{locale}' from Cache.");
                LocaleChanged?.Invoke();
            }
            else
            {
                // 2. Try StreamingAssets
                var streamingData = await _streamingAssetsProvider.LoadLocaleAsync(locale);
                if (streamingData != null && streamingData.Count > 0)
                {
                    _currentLocaleData = streamingData;
                    loadedOffline = true;
                    Debug.Log($"[Localization] Loaded '{locale}' from StreamingAssets.");
                    LocaleChanged?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[Localization] Could not find any offline data for locale '{locale}'.");
                }
            }

            // 3. Check GitHub in the background
            // We do not await this task here so that it does not block the startup flow.
            _ = CheckGitHubBackground(locale, loadedOffline);
        }

        private async Task CheckGitHubBackground(string locale, bool loadedOffline)
        {
            try
            {
                var newData = await _gitHubProvider.LoadLocaleAsync(locale);

                if (newData != null && newData.Count > 0)
                {
                    _currentLocaleData = newData;
                    Debug.Log($"[Localization] Applied hot-reload from GitHub for '{locale}'.");
                    LocaleChanged?.Invoke();
                }
                else if (!loadedOffline)
                {
                    Debug.LogError($"[Localization] Failed to load any localization data for '{locale}' (Offline and GitHub both failed or returned no data).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Localization] Background GitHub check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the translated text for a given key.
        /// </summary>
        /// <param name="key">The translation key (e.g., 'zenka.warning.vesselsafety.01').</param>
        /// <returns>The translated text, or the key itself if not found.</returns>
        public string Get(string key)
        {
            if (_currentLocaleData.TryGetValue(key, out LocalizedEntry entry))
            {
                return entry.Text;
            }

            if (_missingKeys.Add(key))
            {
                Debug.LogWarning($"[Localization] Missing translation for key: {key}");
            }
            return $"[MISSING] {key}";
        }

        /// <summary>
        /// Checks if a translation key exists in the current locale.
        /// </summary>
        public bool Has(string key)
        {
            return _currentLocaleData.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to get the translated text for a key.
        /// </summary>
        public bool TryGet(string key, out string value)
        {
            if (_currentLocaleData.TryGetValue(key, out LocalizedEntry entry))
            {
                value = entry.Text;
                return true;
            }

            value = key;
            return false;
        }
    }
}
