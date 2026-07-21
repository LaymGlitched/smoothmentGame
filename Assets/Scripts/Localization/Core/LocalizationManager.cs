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
        private Dictionary<string, LocalizedEntry> _fallbackLocaleData = new Dictionary<string, LocalizedEntry>();
        private string _currentLocale;
        private string _defaultLocale = "en-US";
        private HashSet<string> _missingKeys = new HashSet<string>();
        private int _currentLoadRequestId = 0;

        private readonly ILocalizationProvider _cachedProvider;
        private readonly ILocalizationProvider _streamingAssetsProvider;
        private readonly ILocalizationProvider _gitHubProvider;

        /// <summary>
        /// Triggered whenever the localization data is loaded or updated (e.g., from GitHub hot-reload).
        /// </summary>
        public event Action LocaleChanged;

        /// <summary>
        /// Gets the code of the currently active locale (e.g., "en-US").
        /// </summary>
        public string CurrentLocale => _currentLocale;

        /// <summary>
        /// Gets whether a locale is currently being loaded.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Gets or sets the default fallback locale code (defaults to "en-US").
        /// </summary>
        public string DefaultLocale
        {
            get => _defaultLocale;
            set => _defaultLocale = value;
        }

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
            if (string.IsNullOrEmpty(locale))
                return;

            int requestId = ++_currentLoadRequestId;
            IsLoading = true;
            _currentLocale = locale;
            _missingKeys.Clear();

            // Reset current locale data so entries from previous language don't persist
            _currentLocaleData = new Dictionary<string, LocalizedEntry>();
            LocaleChanged?.Invoke();

            bool loadedOffline = false;

            // 1. Try Cache
            var cacheData = await _cachedProvider.LoadLocaleAsync(locale);
            if (requestId != _currentLoadRequestId) return; // Discard if superseded

            if (cacheData != null && cacheData.Count > 0)
            {
                _currentLocaleData = cacheData;
                loadedOffline = true;
                Debug.Log($"[Localization] Loaded '{locale}' from Cache.");
            }
            else
            {
                // 2. Try StreamingAssets
                var streamingData = await _streamingAssetsProvider.LoadLocaleAsync(locale);
                if (requestId != _currentLoadRequestId) return; // Discard if superseded

                if (streamingData != null && streamingData.Count > 0)
                {
                    _currentLocaleData = streamingData;
                    loadedOffline = true;
                    Debug.Log($"[Localization] Loaded '{locale}' from StreamingAssets.");
                }
                else
                {
                    Debug.LogWarning($"[Localization] Could not find any offline data for locale '{locale}'.");
                }
            }

            // Keep fallback data if this is the default locale
            if (locale.Equals(_defaultLocale, StringComparison.OrdinalIgnoreCase) && _currentLocaleData.Count > 0)
            {
                _fallbackLocaleData = new Dictionary<string, LocalizedEntry>(_currentLocaleData);
            }

            if (loadedOffline && requestId == _currentLoadRequestId)
            {
                IsLoading = false;
                LocaleChanged?.Invoke();
            }

            // 3. Check GitHub in the background
            _ = CheckGitHubBackground(locale, loadedOffline, requestId);
        }

        private async Task CheckGitHubBackground(string locale, bool loadedOffline, int requestId)
        {
            try
            {
                Task<Dictionary<string, LocalizedEntry>> fetchTask;
                if (_gitHubProvider is GitHubLocalizationProvider ghProvider)
                {
                    fetchTask = ghProvider.LoadLocaleAsync(locale, forceDownload: !loadedOffline);
                }
                else
                {
                    fetchTask = _gitHubProvider.LoadLocaleAsync(locale);
                }

                var newData = await fetchTask;

                if (requestId != _currentLoadRequestId)
                    return; // Request superseded by another LoadLocale call

                if (newData != null && newData.Count > 0)
                {
                    _currentLocaleData = newData;
                    Debug.Log($"[Localization] Applied localization data from GitHub for '{locale}'.");

                    if (locale.Equals(_defaultLocale, StringComparison.OrdinalIgnoreCase))
                    {
                        _fallbackLocaleData = new Dictionary<string, LocalizedEntry>(_currentLocaleData);
                    }

                    IsLoading = false;
                    LocaleChanged?.Invoke();
                }
                else if (!loadedOffline)
                {
                    IsLoading = false;
                    Debug.LogError($"[Localization] Failed to load any localization data for '{locale}' (Offline and GitHub both failed or returned no data).");
                }
                else
                {
                    IsLoading = false;
                }
            }
            catch (Exception ex)
            {
                if (requestId == _currentLoadRequestId)
                {
                    IsLoading = false;
                    Debug.LogError($"[Localization] Background GitHub check failed for '{locale}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the translated text for a given key.
        /// </summary>
        /// <param name="key">The translation key (e.g., 'zenka.warning.vesselsafety.01').</param>
        /// <returns>The translated text, or default fallback, or missing key error.</returns>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_currentLocaleData.TryGetValue(key, out LocalizedEntry entry))
            {
                return entry.Text;
            }

            if (_fallbackLocaleData.TryGetValue(key, out LocalizedEntry fallbackEntry))
            {
                return fallbackEntry.Text;
            }

            if (_missingKeys.Add(key))
            {
                Debug.LogWarning($"[Localization] Missing translation for key: {key} in locale '{_currentLocale}'");
            }
            return $"[MISSING] {key}";
        }

        /// <summary>
        /// Checks if a translation key exists in the current locale (or fallback locale).
        /// </summary>
        public bool Has(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _currentLocaleData.ContainsKey(key) || _fallbackLocaleData.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to get the translated text for a key.
        /// </summary>
        public bool TryGet(string key, out string value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (_currentLocaleData.TryGetValue(key, out LocalizedEntry entry))
                {
                    value = entry.Text;
                    return true;
                }

                if (_fallbackLocaleData.TryGetValue(key, out LocalizedEntry fallbackEntry))
                {
                    value = fallbackEntry.Text;
                    return true;
                }
            }

            value = key;
            return false;
        }
    }
}
