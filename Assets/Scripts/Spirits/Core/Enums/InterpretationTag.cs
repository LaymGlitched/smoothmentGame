namespace GameCode.Spirits.Core
{
    /// <summary>
    /// Represents a Spirit's subjective emotional or logical reaction to an event.
    /// By using a strongly-typed enum instead of strings, we ensure future Agency 
    /// logic and Dialogue triggers remain maintainable and free of typo-driven bugs.
    /// </summary>
    public enum InterpretationTag
    {
        Neutral = 0,
        
        // Emotional reactions
        Alarm,
        Amusement,
        Disgust,
        Sorrow,
        Pride,

        // Logical/Tactical reactions
        TacticalAdvantage,
        TacticalError,
        Wasteful,
        Efficient,
        
        // Narrative/World reactions
        LoreRecognition,
        Sacrilege,
        Reverence
    }
}
