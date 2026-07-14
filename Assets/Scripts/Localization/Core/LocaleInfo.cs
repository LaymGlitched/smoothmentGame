using Newtonsoft.Json;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// Contains metadata about a specific locale.
    /// </summary>
    public class LocaleInfo
    {
        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("nativeName")]
        public string NativeName { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("minGameVersion")]
        public string MinGameVersion { get; set; }

        [JsonProperty("authors")]
        public string[] Authors { get; set; }

        /// <summary>
        /// A list of relative file paths (e.g., "ui.json", "dialogue/spirits.json") 
        /// that contain the translation data for this locale.
        /// Used by the GitHub provider to fetch exactly these files.
        /// </summary>
        [JsonProperty("files")]
        public string[] Files { get; set; }
    }
}
