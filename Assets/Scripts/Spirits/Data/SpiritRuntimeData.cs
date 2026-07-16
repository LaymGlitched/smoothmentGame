using GameCode.Spirits.Core;
using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Holds the mutable, per-playthrough state of a single Spirit.
    /// This class is strictly a data container. All mutation is restricted to internal 
    /// scope so that only the owning Spirit object can alter its state.
    /// </summary>
    [System.Serializable]
    public class SpiritRuntimeData
    {
        [SerializeField] private PresenceMode currentPresenceMode = PresenceMode.Aware;
        [SerializeField] private BehavioralLayer activeBehavioralLayers = BehavioralLayer.None;

        /// <summary>
        /// The Spirit's current depth of engagement with the world.
        /// </summary>
        public PresenceMode CurrentPresenceMode => currentPresenceMode;

        /// <summary>
        /// The currently active behavioral overlays (e.g., Speaking, Channeling).
        /// </summary>
        public BehavioralLayer ActiveBehavioralLayers => activeBehavioralLayers;

        /// <summary>
        /// Default constructor setting the Spirit to a standard baseline state.
        /// </summary>
        public SpiritRuntimeData()
        {
            currentPresenceMode = PresenceMode.Aware;
            activeBehavioralLayers = BehavioralLayer.None;
        }

        // ----------------------------------------------------------------------
        // Internal Mutation API (Restricted to the GameCode.Spirits assembly)
        // ----------------------------------------------------------------------

        internal void SetPresenceMode(PresenceMode newMode)
        {
            currentPresenceMode = newMode;
        }

        internal void AddBehavioralLayer(BehavioralLayer layer)
        {
            activeBehavioralLayers |= layer;
        }

        internal void RemoveBehavioralLayer(BehavioralLayer layer)
        {
            activeBehavioralLayers &= ~layer;
        }

        /// <summary>
        /// Checks if a specific behavioral layer is currently active.
        /// </summary>
        public bool HasBehavioralLayer(BehavioralLayer layer)
        {
            // Bitwise check for flags
            return (activeBehavioralLayers & layer) == layer;
        }
    }
}
