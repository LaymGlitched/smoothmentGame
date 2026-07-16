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
    /// Downloads localization files from the official GitHub repository.
    /// It checks locale.json for version updates and downloads newer files to the cache.
    /// </summary>
    public class GitHubLocalizationProvider : ILocalizationProvider
    {
        private const string RepoBaseUrl = "https://raw.githubusercontent.com/LaymGlitched/ReitekiLocalization/main/locales";

        public async Task<Dictionary<string, LocalizedEntry>> LoadLocaleAsync(string locale)
        {
            try
            {
                string localeJsonUrl = $"{RepoBaseUrl}/{locale}/locale.json";
                string remoteLocaleJson = await DownloadTextAsync(localeJsonUrl);

                if (string.IsNullOrEmpty(remoteLocaleJson))
                {
                    return null; // Failed to fetch or no internet
                }

                LocaleInfo remoteInfo = JsonConvert.DeserializeObject<LocaleInfo>(remoteLocaleJson);
                if (remoteInfo == null)
                {
                    Debug.LogError($"[Localization] Failed to parse remote locale.json for {locale}");
                    return null;
                }

                string cachePath = Path.Combine(Application.persistentDataPath, "Localization", locale);
                string cachedLocalePath = Path.Combine(cachePath, "locale.json");

                int localVersion = -1;
                if (File.Exists(cachedLocalePath))
                {
                    try
                    {
                        string localJson = await Task.Run(() => File.ReadAllText(cachedLocalePath));
                        LocaleInfo localInfo = JsonConvert.DeserializeObject<LocaleInfo>(localJson);
                        if (localInfo != null)
                        {
                            localVersion = localInfo.Version;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Localization] Failed to read cached locale.json for version check: {ex.Message}");
                    }
                }

                // If remote version is not newer, we don't need to update.
                if (remoteInfo.Version <= localVersion)
                {
                    // Return null indicating no update was performed.
                    return null;
                }

                Debug.Log($"[Localization] New localization version found for {locale} (Remote: {remoteInfo.Version}, Local: {localVersion}). Downloading update...");

                // Download all files specified in the remote locale.json
                var newEntries = new Dictionary<string, LocalizedEntry>();

                if (remoteInfo.Files != null && remoteInfo.Files.Length > 0)
                {
                    foreach (string relativeFilePath in remoteInfo.Files)
                    {
                        string fileUrl = $"{RepoBaseUrl}/{locale}/{relativeFilePath.Replace('\\', '/')}";
                        string fileContent = await DownloadTextAsync(fileUrl);

                        if (string.IsNullOrEmpty(fileContent))
                        {
                            Debug.LogWarning($"[Localization] Failed to download {fileUrl}. Skipping.");
                            continue;
                        }

                        // Parse immediately to populate the dictionary
                        try
                        {
                            var entries = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, LocalizedEntry>>(fileContent));
                            if (entries != null)
                            {
                                foreach (var kvp in entries)
                                {
                                    if (!newEntries.TryAdd(kvp.Key, kvp.Value))
                                    {
                                        Debug.LogError($"[Localization] Duplicate key found in GitHub repo: {kvp.Key}. Overwriting.");
                                        newEntries[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Localization] Failed to parse downloaded JSON for {relativeFilePath}: {ex.Message}");
                        }

                        // Save to cache
                        string targetFilePath = Path.Combine(cachePath, relativeFilePath);
                        string targetDir = Path.GetDirectoryName(targetFilePath);
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        await Task.Run(() => File.WriteAllText(targetFilePath, fileContent));
                    }
                }

                // Finally, update the cached locale.json so we don't download it again next time
                if (!Directory.Exists(cachePath))
                {
                    Directory.CreateDirectory(cachePath);
                }
                await Task.Run(() => File.WriteAllText(cachedLocalePath, remoteLocaleJson));

                Debug.Log($"[Localization] Successfully updated localization cache for {locale}.");
                return newEntries;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Localization] Error during GitHub sync for {locale}: {ex.Message}");
                return null;
            }
        }

        private async Task<string> DownloadTextAsync(string url)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler.text;
                }
                else
                {
                    // Standard to fail silently for network issues as it might just be no internet.
                    return null;
                }
            }
        }
    }
}
