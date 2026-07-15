using GameCode.Spirits.Core;
using GameCode.Spirits.Runtime;
using GameCode.Spirits.Data;

namespace GameCode.Spirits.Communication
{
    /// <summary>
    /// Represents a Spirit's desire to communicate, outputted by the Communication Core.
    /// Crucially, this struct expresses *intent* rather than resolving raw dialogue strings.
    /// It remains entirely decoupled from localization, UI, and audio assets.
    /// </summary>
    public struct CommunicationIntent
    {
        /// <summary>
        /// The Spirit attempting to communicate. Passed as a strong runtime reference 
        /// rather than a string ID to improve safety and avoid dictionary lookups.
        /// </summary>
        public readonly Spirit SourceSpirit;

        /// <summary>
        /// The conceptual topic the Spirit wishes to express (e.g., "WarnAboutHealth", "BanterGreeting").
        /// </summary>
        public readonly TopicId Topic;

        /// <summary>
        /// How urgently this intent must be expressed.
        /// </summary>
        public readonly PriorityTier Priority;

        /// <summary>
        /// Optional context object for future expansion (e.g., specific listeners, biome info).
        /// </summary>
        public readonly object Context;

        public CommunicationIntent(Spirit sourceSpirit, TopicId topic, PriorityTier priority, object context = null)
        {
            SourceSpirit = sourceSpirit;
            Topic = topic;
            Priority = priority;
            Context = context;
        }
    }
}
