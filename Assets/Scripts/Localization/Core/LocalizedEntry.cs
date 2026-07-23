using Newtonsoft.Json;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// Represents a single translated variant line.
    /// </summary>
    public struct LocalizedVariant
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    /// <summary>
    /// Represents a single translated entry, which can contain a direct string,
    /// a single text payload, or an array of text variants.
    /// </summary>
    public struct LocalizedEntry
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("variants")]
        public LocalizedVariant[] Variants { get; set; }

        /// <summary>
        /// True if this entry contains multiple dialogue variants.
        /// </summary>
        public bool HasVariants => Variants != null && Variants.Length > 0;
    }
}
