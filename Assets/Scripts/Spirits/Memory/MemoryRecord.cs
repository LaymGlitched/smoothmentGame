using GameCode.Spirits.Core;

namespace GameCode.Spirits.Memory
{
    /// <summary>
    /// Represents a Spirit's subjective interpretation of a gameplay event.
    /// This struct wraps the objective raw event with the Spirit's personal 
    /// emotional/tactical reaction and assigned significance.
    /// </summary>
    [System.Serializable]
    public struct MemoryRecord
    {
        /// <summary>
        /// The objective gameplay event that occurred.
        /// </summary>
        public readonly SpiritEventData RawEvent;

        /// <summary>
        /// The exact time the event was recorded. Cached here for fast access during memory pruning.
        /// </summary>
        public readonly float Timestamp;

        /// <summary>
        /// How significant THIS specific spirit feels the event was (0.0 to 1.0).
        /// High significance memories resist forgetting/decay.
        /// </summary>
        public float Significance;

        /// <summary>
        /// The Spirit's emotional or logical reaction to the event.
        /// </summary>
        public InterpretationTag Tag;

        /// <summary>
        /// Creates a new subjective memory record from a raw event.
        /// </summary>
        public MemoryRecord(SpiritEventData rawEvent, float significance, InterpretationTag tag)
        {
            RawEvent = rawEvent;
            Timestamp = rawEvent?.Timestamp ?? UnityEngine.Time.time;
            Significance = significance;
            Tag = tag;
        }
    }
}
