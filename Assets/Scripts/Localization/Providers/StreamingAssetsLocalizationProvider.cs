using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reiteki.Localization.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Reiteki.Localization.Providers
{
    /// <summary>
    /// Loads localization data from the built-in StreamingAssets folder.
    /// This acts as the offline fallback provider.
    /// </summary>
    public class StreamingAssetsLocalizationProvider : ILocalizationProvider
    {
        public async Task<Dictionary<string, LocalizedEntry>> LoadLocaleAsync(string locale)
        {
            var result = new Dictionary<string, LocalizedEntry>(StringComparer.OrdinalIgnoreCase);
            string basePath = Path.Combine(Application.streamingAssetsPath, "Localization", locale);

            // Note: On Android/WebGL, Directory.Exists and Directory.GetFiles do not work for StreamingAssets.
            // If Android/WebGL support is required later, we can read locale.json first using UnityWebRequest 
            // and iterate over the 'files' array. For now, assuming PC/Console where System.IO works.
            
            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"[Localization] StreamingAssets fallback not found for locale: {locale} at {basePath}");
                return null;
            }

            try
            {
                // To avoid blocking the main thread during IO and parsing, we run this in a background thread.
                await Task.Run(() =>
                {
                    string[] files = Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        if (Path.GetFileName(file).Equals("locale.json", StringComparison.OrdinalIgnoreCase))
                            continue;

                        try
                        {
                            string json = File.ReadAllText(file);
                            var entries = LocalizationJsonParser.Parse(json);

                            if (entries != null)
                            {
                                foreach (var kvp in entries)
                                {
                                    // Overwrite or ignore duplicate keys. We'll overwrite and log.
                                    if (!result.TryAdd(kvp.Key, kvp.Value))
                                    {
                                        Debug.LogError($"[Localization] Duplicate key found in StreamingAssets: {kvp.Key}. Overwriting.");
                                        result[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Localization] Failed to parse JSON file {file}: {ex.Message}");
                        }
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Localization] Error loading StreamingAssets locale {locale}: {ex.Message}");
                return null;
            }
        }
    }
}
