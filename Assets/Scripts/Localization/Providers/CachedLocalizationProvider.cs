using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reiteki.Localization.Core;
using UnityEngine;

namespace Reiteki.Localization.Providers
{
    /// <summary>
    /// Loads localization data that has been downloaded and cached previously.
    /// This allows fast offline loading if the player has played before.
    /// </summary>
    public class CachedLocalizationProvider : ILocalizationProvider
    {
        public async Task<Dictionary<string, LocalizedEntry>> LoadLocaleAsync(string locale)
        {
            var result = new Dictionary<string, LocalizedEntry>(StringComparer.OrdinalIgnoreCase);
            string basePath = Path.Combine(Application.persistentDataPath, "Localization", locale);

            if (!Directory.Exists(basePath))
            {
                return null;
            }

            try
            {
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
                                    if (!result.TryAdd(kvp.Key, kvp.Value))
                                    {
                                        Debug.LogError($"[Localization] Duplicate key found in Cache: {kvp.Key}. Overwriting.");
                                        result[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Localization] Failed to parse JSON file {file} in Cache: {ex.Message}");
                        }
                    }
                });

                // If cache directory existed but was empty, return null so we can fallback.
                if (result.Count == 0)
                {
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Localization] Error loading cached locale {locale}: {ex.Message}");
                return null;
            }
        }
    }
}
