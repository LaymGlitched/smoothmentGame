using Newtonsoft.Json;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// Represents a single translated entry.
    /// </summary>
    public struct LocalizedEntry
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
