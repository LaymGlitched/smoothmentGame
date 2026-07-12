using UnityEngine;

namespace GameCode.Spirits.Core
{
    /// <summary>
    /// Describes a Spirit's depth of engagement with the external world.
    /// This is not a strict state machine, but a consciousness model representing
    /// how present and aware the Spirit is at any given moment.
    /// </summary>
    public enum PresenceMode
    {
        /// <summary>
        /// The Spirit's attention is turned inward. They are present but not actively 
        /// processing external stimuli unless forced to by a strong event.
        /// </summary>
        Withdrawn,

        /// <summary>
        /// The baseline state. The Spirit is passively observing the world through 
        /// the Vessel's senses, evaluating stimuli, and deciding whether to respond.
        /// </summary>
        Aware,

        /// <summary>
        /// The Spirit's attention is actively captured by something (e.g., combat, 
        /// a relevant environment, or a meaningful conversation). Reactions are faster 
        /// and carry more emotional weight.
        /// </summary>
        Focused,

        /// <summary>
        /// The Spirit is mechanically and narratively central to the current moment. 
        /// They are actively lending their power (e.g., their magical affinity is equipped).
        /// </summary>
        Foregrounded
    }
}
