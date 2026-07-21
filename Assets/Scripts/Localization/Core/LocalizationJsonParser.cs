using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// Flexible JSON parser for localization files.
    /// Supports:
    /// 1. Simple key-value pairs: { "key": "translation" }
    /// 2. Objects: { "key": { "id": 123, "text": "translation" } }
    /// 3. Nested objects: { "category": { "key": "translation" } } -> "category.key"
    /// </summary>
    public static class LocalizationJsonParser
    {
        public static Dictionary<string, LocalizedEntry> Parse(string json)
        {
            var result = new Dictionary<string, LocalizedEntry>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                JToken token = JToken.Parse(json);
                if (token is JObject obj)
                {
                    FlattenJObject(obj, "", result);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationJsonParser] Error parsing JSON: {ex.Message}");
            }

            return result;
        }

        private static void FlattenJObject(JObject obj, string prefix, Dictionary<string, LocalizedEntry> result)
        {
            foreach (var property in obj.Properties())
            {
                string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                JToken val = property.Value;

                if (val == null || val.Type == JTokenType.Null)
                    continue;

                if (val.Type == JTokenType.String || val.Type == JTokenType.Integer || val.Type == JTokenType.Float || val.Type == JTokenType.Boolean)
                {
                    // Simple primitive value (e.g. "key": "translation text")
                    result[key] = new LocalizedEntry { Id = 0, Text = val.ToString() };
                }
                else if (val is JObject childObj)
                {
                    // Check if childObj is a LocalizedEntry object with "text"/"Text"/"value"/"Value"
                    if (TryExtractLocalizedEntry(childObj, out LocalizedEntry entry))
                    {
                        result[key] = entry;
                    }
                    else
                    {
                        // Recursively flatten nested JSON object
                        FlattenJObject(childObj, key, result);
                    }
                }
            }
        }

        private static bool TryExtractLocalizedEntry(JObject obj, out LocalizedEntry entry)
        {
            entry = default;
            JToken textToken = obj["text"] ?? obj["Text"] ?? obj["value"] ?? obj["Value"];
            if (textToken != null && textToken.Type == JTokenType.String)
            {
                ulong id = 0;
                JToken idToken = obj["id"] ?? obj["Id"];
                if (idToken != null && ulong.TryParse(idToken.ToString(), out ulong parsedId))
                {
                    id = parsedId;
                }

                entry = new LocalizedEntry
                {
                    Id = id,
                    Text = textToken.ToString()
                };
                return true;
            }
            return false;
        }
    }
}
