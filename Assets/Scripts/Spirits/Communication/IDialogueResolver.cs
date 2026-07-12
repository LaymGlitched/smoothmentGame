namespace GameCode.Spirits.Communication
{
    /// <summary>
    /// Abstraction for the Dialogue Resolution Layer.
    /// This interface ensures the core Spirit System remains completely decoupled 
    /// from whatever data structures, localization files, or tools the game uses 
    /// to store actual dialogue text.
    /// </summary>
    public interface IDialogueResolver
    {
        /// <summary>
        /// Attempts to translate a conceptual intent into a concrete, localized dialogue request.
        /// </summary>
        /// <param name="intent">The conceptual motivation from the Spirit.</param>
        /// <returns>A DialogueRequest if a matching line is found, otherwise null.</returns>
        DialogueRequest? ResolveIntent(CommunicationIntent intent);
    }
}
