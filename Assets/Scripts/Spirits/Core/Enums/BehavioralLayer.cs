namespace GameCode.Spirits.Core
{
    /// <summary>
    /// Describes activities a Spirit can perform that overlay their current PresenceMode.
    /// Designed as a Flags enum so multiple behaviors (e.g. Speaking AND Channeling)
    /// can occur simultaneously.
    /// </summary>
    [System.Flags]
    public enum BehavioralLayer
    {
        /// <summary>
        /// The Spirit is performing no active behavioral overlays.
        /// </summary>
        None = 0,

        /// <summary>
        /// The Spirit is currently delivering dialogue to the Vessel.
        /// </summary>
        Speaking = 1 << 0,

        /// <summary>
        /// The Spirit is actively powering a spell or ability.
        /// </summary>
        Channeling = 1 << 1,

        /// <summary>
        /// The Spirit has projected a visible, localized presence into the physical world.
        /// </summary>
        Manifesting = 1 << 2,

        /// <summary>
        /// The Spirit has assumed direct influence over the Vessel's body.
        /// </summary>
        Possessing = 1 << 3
    }
}
