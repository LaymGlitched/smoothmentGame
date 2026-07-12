using UnityEngine;

namespace GameCode.Spirits.Core
{
    /// <summary>
    /// Base class for all events passed from the gameplay layer (via SpiritEventBridge) 
    /// into the Spirit System. This ensures the Spirit System remains decoupled from 
    /// concrete gameplay classes.
    /// </summary>
    public abstract class SpiritEventData
    {
        /// <summary>
        /// The time (Unity Time.time) at which the event occurred.
        /// Useful for calculating cooldowns, recency, or sequencing in future memory systems.
        /// </summary>
        public float Timestamp { get; }

        protected SpiritEventData()
        {
            Timestamp = Time.time;
        }
    }

    /// <summary>
    /// Event dispatched when the player takes damage.
    /// </summary>
    public class DamageEventData : SpiritEventData
    {
        public float Amount { get; }
        public GameCode.Shared.DamageType DamageType { get; }

        public DamageEventData(float amount, GameCode.Shared.DamageType damageType)
        {
            Amount = amount;
            DamageType = damageType;
        }
    }

    /// <summary>
    /// Event dispatched when the player equips a new spell affinity.
    /// </summary>
    public class SpellEquippedEventData : SpiritEventData
    {
        public string SpellName { get; }

        public SpellEquippedEventData(string spellName)
        {
            SpellName = spellName;
        }
    }

    /// <summary>
    /// Event dispatched when the player casts or begins channeling a spell.
    /// </summary>
    public class SpellCastEventData : SpiritEventData
    {
        public string SpellName { get; }
        public bool IsCharged { get; }

        public SpellCastEventData(string spellName, bool isCharged)
        {
            SpellName = spellName;
            IsCharged = isCharged;
        }
    }
}
