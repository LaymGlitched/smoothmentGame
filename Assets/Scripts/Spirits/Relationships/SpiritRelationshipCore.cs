using System.Collections.Generic;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Relationships
{
    /// <summary>
    /// Tracks a Spirit's affinity and bonds toward other entities (Player, other Spirits).
    /// Owned via composition by the Spirit class. Evaluated by the Agency system to 
    /// influence motivation and interpretation of events.
    /// </summary>
    public class SpiritRelationshipCore
    {
        // Entity ID -> Affinity (0.0 = Hostile, 0.5 = Neutral, 1.0 = Bonded)
        private readonly Dictionary<string, float> affinities;

        public SpiritRelationshipCore()
        {
            affinities = new Dictionary<string, float>();
        }

        /// <summary>
        /// Retrieves the current affinity toward a given entity ID.
        /// Defaults to 0.5 (Neutral) if the entity is unknown.
        /// </summary>
        public float GetAffinity(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return 0.5f;
            
            return affinities.TryGetValue(entityId, out float affinity) ? affinity : 0.5f;
        }

        /// <summary>
        /// Retrieves the current affinity toward another Spirit.
        /// Strongly-typed convenience wrapper.
        /// </summary>
        public float GetAffinity(Spirit targetSpirit)
        {
            if (targetSpirit == null) return 0.5f;
            return GetAffinity(targetSpirit.Id);
        }

        /// <summary>
        /// Adjusts the affinity toward a specific entity ID.
        /// </summary>
        public void AdjustAffinity(string entityId, float amount)
        {
            if (string.IsNullOrEmpty(entityId)) return;

            float current = GetAffinity(entityId);
            affinities[entityId] = Mathf.Clamp01(current + amount);
        }

        /// <summary>
        /// Adjusts the affinity toward another Spirit.
        /// Strongly-typed convenience wrapper.
        /// </summary>
        public void AdjustAffinity(Spirit targetSpirit, float amount)
        {
            if (targetSpirit == null) return;
            AdjustAffinity(targetSpirit.Id, amount);
        }

        /// <summary>
        /// Read-only access to all tracked affinities for debugging and UI.
        /// </summary>
        public IReadOnlyDictionary<string, float> Bonds => affinities;

#if UNITY_EDITOR
        /// <summary>
        /// Clears all relationships. For Editor/Debugging purposes only.
        /// </summary>
        public void DebugClear()
        {
            affinities.Clear();
        }
#endif
    }
}
