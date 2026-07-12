using System.Collections.Generic;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using UnityEngine;

namespace GameCode.Spirits.Memory
{
    /// <summary>
    /// The memory engine for an individual Spirit. 
    /// Translates raw gameplay events into subjective memory records, maintains the 
    /// rolling buffer of immediate memory, and opportunistically forgets decaying memories.
    /// Owned via composition by the Spirit class.
    /// </summary>
    public class SpiritMemoryCore
    {
        // To prevent memory bloat, we strictly cap the rolling immediate buffer.
        private const int MaxImmediateMemorySize = 20;
        
        // Threshold below which a memory is considered "forgotten"
        private const float SignificanceDecayThreshold = 0.1f;
        // How much significance is lost per second of real-time
        private const float DecayRatePerSecond = 0.005f;

        private readonly Queue<SpiritEventData> immediateMemory;
        private readonly List<MemoryRecord> recentMemory;

        /// <summary>
        /// The rolling buffer of events that just occurred, prior to any fading or pruning.
        /// </summary>
        public IReadOnlyCollection<SpiritEventData> ImmediateMemory => immediateMemory;

        /// <summary>
        /// Subjective memories that have passed the significance threshold and have not yet decayed.
        /// </summary>
        public IReadOnlyCollection<MemoryRecord> RecentMemory => recentMemory;

        public SpiritMemoryCore()
        {
            immediateMemory = new Queue<SpiritEventData>(MaxImmediateMemorySize);
            recentMemory = new List<MemoryRecord>();
        }

        /// <summary>
        /// Evaluates a raw event, adds it to immediate memory, and potentially promotes it 
        /// to recent memory based on the Spirit's identity profile.
        /// Opportunistically prunes old memories during this call to avoid Update() polling loops.
        /// </summary>
        /// <param name="eventData">The raw gameplay event.</param>
        /// <param name="identity">The immutable identity profile of this specific spirit.</param>
        /// <returns>The generated MemoryRecord if promoted, or null if deemed insignificant.</returns>
        public MemoryRecord? ProcessEvent(SpiritEventData eventData, SpiritDefinition identity)
        {
            // 1. Opportunistic memory maintenance
            PruneMemory();

            // 2. Add to Immediate Memory (rolling buffer)
            if (immediateMemory.Count >= MaxImmediateMemorySize)
            {
                immediateMemory.Dequeue();
            }
            immediateMemory.Enqueue(eventData);

            // 3. Evaluate Subjective Significance
            MemoryRecord? subjectiveRecord = EvaluateSignificance(eventData, identity.IdentityProfile);

            // 4. Promote to Recent Memory if significant enough
            if (subjectiveRecord.HasValue && subjectiveRecord.Value.Significance > SignificanceDecayThreshold)
            {
                recentMemory.Add(subjectiveRecord.Value);
            }

            return subjectiveRecord;
        }

        /// <summary>
        /// Applies time-based decay to all recent memories and removes those that 
        /// fall below the threshold.
        /// </summary>
        public void PruneMemory()
        {
            float currentTime = Time.time;

            for (int i = recentMemory.Count - 1; i >= 0; i--)
            {
                var record = recentMemory[i];
                float age = currentTime - record.Timestamp;
                
                // Calculate decayed significance
                float decayedSignificance = record.Significance - (age * DecayRatePerSecond);

                if (decayedSignificance <= SignificanceDecayThreshold)
                {
                    // The spirit has "forgotten" this event
                    recentMemory.RemoveAt(i);
                }
                else
                {
                    // Update the struct with the newly decayed significance
                    record.Significance = decayedSignificance;
                    recentMemory[i] = record;
                }
            }
        }

        /// <summary>
        /// Evaluates how much this specific Spirit cares about a given event based on their personality.
        /// </summary>
        private MemoryRecord? EvaluateSignificance(SpiritEventData eventData, SpiritIdentityProfile profile)
        {
            // Fallback for Phase 1 missing data
            if (profile == null) 
            {
                return new MemoryRecord(eventData, 0.5f, InterpretationTag.Neutral);
            }

            // Example Evaluation Logic:
            // A highly empathetic and cautious spirit cares deeply about damage.
            if (eventData is DamageEventData damageEvent)
            {
                // Baseline significance based on damage amount
                float baseSignificance = Mathf.Clamp01(damageEvent.Amount / 100f);
                
                // Modulate by personality (Caution and Empathy amplify the impact of taking damage)
                float personalSignificance = baseSignificance * (1f + profile.Caution + profile.Empathy);
                personalSignificance = Mathf.Clamp01(personalSignificance);

                InterpretationTag tag = profile.Caution > 0.7f ? InterpretationTag.TacticalError : InterpretationTag.Alarm;

                return new MemoryRecord(eventData, personalSignificance, tag);
            }

            if (eventData is SpellCastEventData spellEvent)
            {
                // An aggressive spirit finds charged spells significant/prideful.
                if (spellEvent.IsCharged)
                {
                    float sig = Mathf.Clamp01(0.3f + profile.Aggression);
                    return new MemoryRecord(eventData, sig, InterpretationTag.TacticalAdvantage);
                }
            }

            // If the event doesn't resonate strongly with the spirit's profile, it is deemed insignificant.
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears all memories. For Editor/Debugging purposes only.
        /// </summary>
        public void DebugClear()
        {
            immediateMemory.Clear();
            recentMemory.Clear();
        }
#endif
    }
}
