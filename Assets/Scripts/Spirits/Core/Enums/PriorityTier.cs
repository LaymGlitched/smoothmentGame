namespace GameCode.Spirits.Core
{
    /// <summary>
    /// Represents the urgency of a requested action or dialogue.
    /// Used by the Dialogue Coordinator to sort and interrupt pending intents.
    /// </summary>
    public enum PriorityTier
    {
        /// <summary>
        /// Low priority background chatter. Safe to drop if the queue is busy.
        /// </summary>
        Ambient = 0,

        /// <summary>
        /// Standard interactions and normal conversational flow.
        /// </summary>
        Standard = 1,

        /// <summary>
        /// Important reactions to gameplay (e.g., taking heavy damage). May interrupt Ambient.
        /// </summary>
        Urgent = 2,

        /// <summary>
        /// Critical narrative or life-saving warnings. Interrupts all lower tiers immediately.
        /// </summary>
        Critical = 3
    }
}
