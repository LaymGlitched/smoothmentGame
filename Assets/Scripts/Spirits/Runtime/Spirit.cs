using System;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using GameCode.Spirits.Memory;
using GameCode.Spirits.Agency;
using GameCode.Spirits.Relationships;
using GameCode.Spirits.Communication;

namespace GameCode.Spirits.Runtime
{
    /// <summary>
    /// Represents an individual living Spirit during gameplay.
    /// This is a pure C# class (not a MonoBehaviour) to ensure its logic remains 
    /// completely decoupled from Unity's update loop and physics systems.
    /// It owns its runtime state and evaluates incoming gameplay events.
    /// </summary>
    public class Spirit
    {
        private readonly SpiritMemoryCore memory;
        private readonly SpiritAgencyCore agency;
        private readonly SpiritRelationshipCore relationships;
        private readonly SpiritCommunicationCore communication;

        /// <summary>
        /// Fired when this Spirit's PresenceMode changes. 
        /// Provides (SourceSpirit, OldMode, NewMode).
        /// </summary>
        public event Action<Spirit, PresenceMode, PresenceMode> OnPresenceChanged;

        /// <summary>
        /// Fired when a BehavioralLayer is toggled. 
        /// Provides (SourceSpirit, Layer, IsActive).
        /// </summary>
        public event Action<Spirit, BehavioralLayer, bool> OnBehaviorChanged;

        /// <summary>
        /// Fired when the Spirit generates an intent to communicate outwardly.
        /// </summary>
        public event Action<CommunicationIntent> OnIntentGenerated;

        /// <summary>
        /// The unique identifier of this Spirit.
        /// </summary>
        public string Id => Definition.Id;

        /// <summary>
        /// The immutable authoring data defining who this Spirit is.
        /// </summary>
        public SpiritDefinition Definition { get; }

        /// <summary>
        /// The mutable runtime state of this Spirit.
        /// </summary>
        public SpiritRuntimeData RuntimeData { get; }

        // Read-only access to cores for Editor debugging and UI
        public SpiritMemoryCore Memory => memory;
        public SpiritAgencyCore Agency => agency;
        public SpiritRelationshipCore Relationships => relationships;
        public SpiritCommunicationCore Communication => communication;

        /// <summary>
        /// Instantiates a new living Spirit.
        /// </summary>
        /// <param name="definition">The immutable blueprint for this Spirit.</param>
        /// <param name="initialData">Optional initial state (e.g., loaded from a save file). If null, creates fresh state.</param>
        public Spirit(SpiritDefinition definition, SpiritRuntimeData initialData = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition), "A Spirit cannot exist without a Definition.");
            }

            Definition = definition;
            RuntimeData = initialData ?? new SpiritRuntimeData();

            memory = new SpiritMemoryCore();
            agency = new SpiritAgencyCore();
            relationships = new SpiritRelationshipCore();
            communication = new SpiritCommunicationCore(this);
        }

        /// <summary>
        /// Transitions the Spirit to a new depth of engagement.
        /// </summary>
        public void TransitionPresence(PresenceMode newMode)
        {
            if (RuntimeData.CurrentPresenceMode == newMode)
                return;

            PresenceMode oldMode = RuntimeData.CurrentPresenceMode;
            RuntimeData.SetPresenceMode(newMode);

            OnPresenceChanged?.Invoke(this, oldMode, newMode);
        }

        /// <summary>
        /// Activates a behavioral overlay (e.g., Speaking, Channeling).
        /// </summary>
        public void BeginBehavior(BehavioralLayer layer)
        {
            if (RuntimeData.HasBehavioralLayer(layer))
                return;

            RuntimeData.AddBehavioralLayer(layer);
            OnBehaviorChanged?.Invoke(this, layer, true);
        }

        /// <summary>
        /// Deactivates a behavioral overlay.
        /// </summary>
        public void EndBehavior(BehavioralLayer layer)
        {
            if (!RuntimeData.HasBehavioralLayer(layer))
                return;

            RuntimeData.RemoveBehavioralLayer(layer);
            OnBehaviorChanged?.Invoke(this, layer, false);
        }

        /// <summary>
        /// Receives neutral gameplay events forwarded by the SpiritManager.
        /// Routes the event through the subjective Memory, Agency, and Communication systems.
        /// </summary>
        /// <param name="eventData">The neutral event data.</param>
        public void HandleEvent(SpiritEventData eventData)
        {
            if (eventData == null) return;

            // 1. Memory evaluates the raw event objectively
            MemoryRecord? subjectiveMemory = memory.ProcessEvent(eventData, Definition);

            if (subjectiveMemory.HasValue)
            {
                // 2. Agency evaluates the subjective memory, influenced by Relationships
                AgencyImpulse? impulse = agency.EvaluateMemory(subjectiveMemory.Value, Definition, relationships);

                if (impulse.HasValue)
                {
                    // 3. The Spirit becomes Focused because it is deeply concerned
                    TransitionPresence(PresenceMode.Focused);
                    
                    // 4. Communication translates the internal impulse into an outward intent
                    CommunicationIntent? intent = communication.TranslateImpulse(impulse.Value);

                    if (intent.HasValue)
                    {
                        // 5. Broadcast the intent to be resolved by higher-level dialogue systems
                        OnIntentGenerated?.Invoke(intent.Value);
                    }
                }
            }
        }
    }
}
