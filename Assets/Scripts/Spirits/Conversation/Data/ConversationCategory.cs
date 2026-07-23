namespace GameCode.Spirits.Conversation.Data
{
    /// <summary>
    /// Classifies conversations for variety scoring and category-level cooldowns.
    /// The ConversationDirector uses this to prevent the system from playing
    /// too many conversations of the same type in succession.
    /// </summary>
    public enum ConversationCategory
    {
        /// <summary>
        /// Idle chatter, environmental observations, banter.
        /// Lowest priority category. Freely droppable when busier events occur.
        /// </summary>
        Ambient = 0,

        /// <summary>
        /// Reactive responses to gameplay events (HP low, enemy killed, puzzle solved).
        /// Contextual and time-sensitive.
        /// </summary>
        Reaction = 1,

        /// <summary>
        /// Story-critical conversations that advance the narrative.
        /// Often one-shot. Highest authored importance.
        /// </summary>
        Story = 2,

        /// <summary>
        /// Relationship-building exchanges between Spirits.
        /// Reveals personality depth and inter-Spirit dynamics.
        /// </summary>
        Bond = 3
    }
}
