using System.Collections.Generic;
using System.Linq;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using GameCode.Spirits.Memory;
using UnityEngine;

namespace GameCode.Spirits.Agency
{
    /// <summary>
    /// The decision-making engine for an individual Spirit.
    /// Evaluates subjective memories, builds and maintains internal Concerns, 
    /// and generates AgencyImpulses when those concerns cross critical thresholds.
    /// Owned via composition by the Spirit class.
    /// </summary>
    public class SpiritAgencyCore
    {
        // Threshold above which a Concern generates an active Impulse
        private const float ImpulseThreshold = 0.8f;
        
        // How much intensity is lost per second of real-time
        private const float DecayRatePerSecond = 0.002f;

        private readonly Dictionary<string, Concern> activeConcerns;
        private float lastDecayTime;

        /// <summary>
        /// The current set of active worries/motivations for this Spirit.
        /// </summary>
        public IReadOnlyCollection<Concern> ActiveConcerns => activeConcerns.Values;

        public SpiritAgencyCore()
        {
            activeConcerns = new Dictionary<string, Concern>();
            lastDecayTime = Time.time;
        }

        /// <summary>
        /// Evaluates a newly formed subjective memory. 
        /// Modifies existing concerns or creates new ones based on the memory's interpretation,
        /// optionally influenced by the Spirit's relationships.
        /// Opportunistically decays existing concerns during this call to avoid Update() polling loops.
        /// </summary>
        /// <returns>An AgencyImpulse if a concern crosses the critical threshold, otherwise null.</returns>
        public AgencyImpulse? EvaluateMemory(MemoryRecord memory, SpiritDefinition identity, Relationships.SpiritRelationshipCore relationships)
        {
            // 1. Opportunistic decay
            DecayConcerns();

            // 2. Identify the relevant subject based on the memory's tag
            string subject = DetermineConcernSubject(memory);
            if (string.IsNullOrEmpty(subject))
            {
                return null; // Memory doesn't map to a clear concern
            }

            // 3. Retrieve or create the concern
            if (!activeConcerns.TryGetValue(subject, out Concern concern))
            {
                concern = new Concern(subject, 0f);
                activeConcerns.Add(subject, concern);
            }

            // 4. Increase intensity based on the memory's significance and relationships
            float intensityIncrease = memory.Significance * 0.5f;

            // Phase 3 Relationship Influence:
            // If this event was caused by someone the Spirit likes, it might mitigate a negative concern.
            // (Placeholder logic: we extract the source entity from the event if possible. 
            // For now, we assume the player is the source for demonstration).
            float playerAffinity = relationships.GetAffinity("Player");
            if (playerAffinity > 0.8f && (memory.Tag == InterpretationTag.TacticalError || memory.Tag == InterpretationTag.Wasteful))
            {
                // Forgive the player's mistakes more easily
                intensityIncrease *= 0.5f;
            }

            concern.IncreaseIntensity(intensityIncrease);

            // 5. Evaluate if the concern warrants an impulse
            if (concern.Intensity >= ImpulseThreshold)
            {
                // We reset the concern slightly so it doesn't immediately fire again 
                // on the very next minor event, acting as a natural "cooldown".
                float impulseIntensity = concern.Intensity;
                concern.DecreaseIntensity(0.3f);

                return new AgencyImpulse(concern, impulseIntensity);
            }

            return null;
        }

        /// <summary>
        /// Applies time-based decay to all active concerns.
        /// Concerns that drop to 0 are removed to prevent bloat.
        /// </summary>
        private void DecayConcerns()
        {
            float currentTime = Time.time;
            float timePassed = currentTime - lastDecayTime;
            lastDecayTime = currentTime;

            if (timePassed <= 0f) return;

            float decayAmount = timePassed * DecayRatePerSecond;
            
            // Need a list of keys to remove while iterating
            List<string> keysToRemove = null;

            foreach (var kvp in activeConcerns)
            {
                kvp.Value.DecreaseIntensity(decayAmount);
                if (kvp.Value.Intensity <= 0f)
                {
                    keysToRemove ??= new List<string>();
                    keysToRemove.Add(kvp.Key);
                }
            }

            if (keysToRemove != null)
            {
                foreach (string key in keysToRemove)
                {
                    activeConcerns.Remove(key);
                }
            }
        }

        /// <summary>
        /// Maps an InterpretationTag to a specific subject of concern.
        /// </summary>
        private string DetermineConcernSubject(MemoryRecord memory)
        {
            return memory.Tag switch
            {
                InterpretationTag.Alarm or InterpretationTag.TacticalError => "VesselSafety",
                InterpretationTag.Wasteful => "ResourceManagement",
                InterpretationTag.LoreRecognition or InterpretationTag.Sacrilege => "AncientHistory",
                _ => null
            };
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears all active concerns. For Editor/Debugging purposes only.
        /// </summary>
        public void DebugClear()
        {
            activeConcerns.Clear();
        }
#endif
    }
}
