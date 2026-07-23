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
    /// 3. Variants: { "key": { "variants": [ { "id": 1, "text": "var1" }, { "id": 2, "text": "var2" } ] } }
    /// 4. Nested objects: { "category": { "key": "translation" } } -> "category.key"
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
                    result[key] = new LocalizedEntry { Id = 0, Text = val.ToString(), Variants = null };
                }
                else if (val is JObject childObj)
                {
                    // Check if childObj is a LocalizedEntry object with "text"/"variants"
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
            JToken variantsToken = obj["variants"] ?? obj["Variants"];

            LocalizedVariant[] variants = null;

            if (variantsToken is JArray jArray && jArray.Count > 0)
            {
                var variantList = new List<LocalizedVariant>();
                foreach (var item in jArray)
                {
                    if (item is JObject varObj)
                    {
                        JToken varText = varObj["text"] ?? varObj["Text"] ?? varObj["value"] ?? varObj["Value"];
                        if (varText != null)
                        {
                            ulong varId = 0;
                            JToken varIdToken = varObj["id"] ?? varObj["Id"];
                            if (varIdToken != null && ulong.TryParse(varIdToken.ToString(), out ulong parsedVarId))
                            {
                                varId = parsedVarId;
                            }

                            variantList.Add(new LocalizedVariant
                            {
                                Id = varId,
                                Text = varText.ToString()
                            });
                        }
                    }
                    else if (item != null && (item.Type == JTokenType.String || item.Type == JTokenType.Integer || item.Type == JTokenType.Float))
                    {
                        variantList.Add(new LocalizedVariant
                        {
                            Id = 0,
                            Text = item.ToString()
                        });
                    }
                }

                if (variantList.Count > 0)
                {
                    variants = variantList.ToArray();
                }
            }

            if (variants != null || (textToken != null && textToken.Type == JTokenType.String))
            {
                ulong id = 0;
                JToken idToken = obj["id"] ?? obj["Id"];
                if (idToken != null && ulong.TryParse(idToken.ToString(), out ulong parsedId))
                {
                    id = parsedId;
                }

                string mainText = textToken != null ? textToken.ToString() : (variants != null ? variants[0].Text : string.Empty);

                entry = new LocalizedEntry
                {
                    Id = id,
                    Text = mainText,
                    Variants = variants
                };
                return true;
            }

            return false;
        }
    }
}
