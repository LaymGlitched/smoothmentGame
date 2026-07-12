using GameCode.Spirits.Runtime;

namespace GameCode.Spirits.Core
{
    /// <summary>
    /// Represents the event of a Spirit speaking a line of dialogue.
    /// This is broadcasted back into the system by the Dialogue Coordinator, 
    /// acting as a gameplay stimulus that other Spirits can hear, remember, 
    /// and react to via their own Agency.
    /// </summary>
    public class DialogueHeardEventData : SpiritEventData
    {
        /// <summary>
        /// The Spirit who spoke the line.
        /// </summary>
        public readonly Spirit Speaker;

        /// <summary>
        /// The conceptual key or topic that was spoken.
        /// </summary>
        public readonly string SpokenKey;

        public DialogueHeardEventData(Spirit speaker, string spokenKey)
        {
            Speaker = speaker;
            SpokenKey = spokenKey;
        }
    }
}
