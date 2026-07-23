using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reiteki.Localization.Providers;
using UnityEngine;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// The central manager that orchestrates the localization providers and serves localized strings.
    /// Supports automatic unique random selection for keys defined with variant arrays.
    /// </summary>
    public class LocalizationManager
    {
        private Dictionary<string, LocalizedEntry> _currentLocaleData = new Dictionary<string, LocalizedEntry>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, LocalizedEntry> _fallbackLocaleData = new Dictionary<string, LocalizedEntry>(StringComparer.OrdinalIgnoreCase);
        private string _currentLocale;
        private string _defaultLocale = "en-US";
        private HashSet<string> _missingKeys = new HashSet<string>();
        private int _currentLoadRequestId = 0;

        // Tracks the index of the last selected variant for each key to prevent selecting the same variant twice in a row
        private readonly Dictionary<string, int> _lastPlayedVariantIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
        /// Asynchronously loads a locale.
        /// </summary>
        public async Task LoadLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
                return;

            int requestId = ++_currentLoadRequestId;
            IsLoading = true;
            _currentLocale = locale;
            _missingKeys.Clear();
            _lastPlayedVariantIndices.Clear();

            _currentLocaleData = new Dictionary<string, LocalizedEntry>(StringComparer.OrdinalIgnoreCase);
            LocaleChanged?.Invoke();

            bool loadedOffline = false;

            // 1. Try Cache
            var cacheData = await _cachedProvider.LoadLocaleAsync(locale);
            if (requestId != _currentLoadRequestId) return;

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
                if (requestId != _currentLoadRequestId) return;

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
                    fetchTask = ghProvider.LoadLocaleAsync(locale, forceDownload: true);
                }
                else
                {
                    fetchTask = _gitHubProvider.LoadLocaleAsync(locale);
                }

                var newData = await fetchTask;

                if (requestId != _currentLoadRequestId)
                    return;

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
        /// If the key defines multiple variants, randomly selects a variant, ensuring
        /// the exact same variant is never selected twice in a row.
        /// </summary>
        /// <param name="key">The translation key (e.g., 'zenka.warning.vessel_safety').</param>
        /// <returns>The translated text, or default fallback, or missing key error.</returns>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_currentLocaleData.TryGetValue(key, out LocalizedEntry entry))
            {
                return ResolveEntryText(key, entry);
            }

            if (_fallbackLocaleData.TryGetValue(key, out LocalizedEntry fallbackEntry))
            {
                return ResolveEntryText(key, fallbackEntry);
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
        /// Attempts to get the translated text for a key, resolving variants if present.
        /// </summary>
        public bool TryGet(string key, out string value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (_currentLocaleData.TryGetValue(key, out LocalizedEntry entry))
                {
                    value = ResolveEntryText(key, entry);
                    return true;
                }

                if (_fallbackLocaleData.TryGetValue(key, out LocalizedEntry fallbackEntry))
                {
                    value = ResolveEntryText(key, fallbackEntry);
                    return true;
                }
            }

            value = key;
            return false;
        }

        /// <summary>
        /// Attempts to get the raw LocalizedEntry for a key.
        /// </summary>
        public bool TryGetEntry(string key, out LocalizedEntry entry)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (_currentLocaleData.TryGetValue(key, out entry))
                    return true;
                if (_fallbackLocaleData.TryGetValue(key, out entry))
                    return true;
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// Resolves the text payload for an entry. If entry contains variants,
        /// performs unique random selection excluding the last played variant index.
        /// </summary>
        private string ResolveEntryText(string key, LocalizedEntry entry)
        {
            if (!entry.HasVariants)
            {
                return entry.Text;
            }

            var variants = entry.Variants;
            int count = variants.Length;

            if (count == 1)
            {
                _lastPlayedVariantIndices[key] = 0;
                return variants[0].Text;
            }

            // Retrieve last selected variant index for this key
            _lastPlayedVariantIndices.TryGetValue(key, out int lastIndex);

            int selectedIndex;
            if (lastIndex >= 0 && lastIndex < count)
            {
                // Exclude lastIndex from random selection
                int roll = UnityEngine.Random.Range(0, count - 1);
                selectedIndex = (roll >= lastIndex) ? roll + 1 : roll;
            }
            else
            {
                selectedIndex = UnityEngine.Random.Range(0, count);
            }

            _lastPlayedVariantIndices[key] = selectedIndex;
            return variants[selectedIndex].Text;
        }
    }
}
