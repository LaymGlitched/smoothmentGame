using System;
using System.Collections.Generic;
using System.Linq;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using GameCode.Magic;
using UnityEngine;

namespace GameCode.Spirits.Runtime
{
    /// <summary>
    /// The central coordinator for the Spirit System.
    /// Responsibilities: Instantiates the Spirits at startup, routes events from the 
    /// SpiritEventBridge to the individual Spirits, and provides a query API for 
    /// other systems to read Spirit state.
    /// </summary>
    public class SpiritManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The definitions for the Spirits that should be active in this playthrough.")]
        [SerializeField] private SpiritDefinition[] initialSpirits;

        private readonly Dictionary<string, Spirit> activeSpirits = new Dictionary<string, Spirit>();

        /// <summary>
        /// Fired when any Spirit changes its PresenceMode.
        /// Payload: (Spirit, OldMode, NewMode)
        /// </summary>
        public event Action<Spirit, PresenceMode, PresenceMode> OnSpiritPresenceChanged;

        /// <summary>
        /// Fired when any Spirit toggles a BehavioralLayer.
        /// Payload: (Spirit, Layer, IsActive)
        /// </summary>
        public event Action<Spirit, BehavioralLayer, bool> OnSpiritBehaviorChanged;

        /// <summary>
        /// Fired when any active Spirit generates an intent to speak.
        /// </summary>
        public event Action<Spirit, Communication.CommunicationIntent> OnSpiritIntentGenerated;

        /// <summary>
        /// Singleton access. Used strictly for routing events from the EventBridge and 
        /// allowing UI/Dialogue systems to query state.
        /// </summary>
        public static SpiritManager Instance { get; private set; }

        // ----------------------------------------------------------------------
        // Initialization & Lifecycle
        // ----------------------------------------------------------------------

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                SpellCaster.OnSpellCasterCreated += HandleSpellCasterCreated;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private SpellCaster cachedSpellCaster;

        private void Start()
        {
            InitializeSpirits();
            cachedSpellCaster = FindObjectOfType<SpellCaster>();
            
            // Automatically equip the first spirit on startup so the player gets their spells
            if (GetForegroundedSpirit() == null && activeSpirits.Count > 0)
            {
                var firstSpirit = activeSpirits.Values.First();
                firstSpirit.TransitionPresence(PresenceMode.Foregrounded);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SpellCaster.OnSpellCasterCreated -= HandleSpellCasterCreated;
            }

            // Clean up event subscriptions to prevent memory leaks
            foreach (var spirit in activeSpirits.Values)
            {
                spirit.OnPresenceChanged -= HandlePresenceChanged;
                spirit.OnBehaviorChanged -= HandleBehaviorChanged;
                spirit.OnIntentGenerated -= HandleIntentGenerated;
            }
        }

        private void InitializeSpirits()
        {
            if (initialSpirits == null) return;

            foreach (var definition in initialSpirits)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                // Create the runtime Spirit instance
                var spirit = new Spirit(definition);

                // Subscribe to the Spirit's internal events so we can bubble them up
                spirit.OnPresenceChanged += HandlePresenceChanged;
                spirit.OnBehaviorChanged += HandleBehaviorChanged;
                spirit.OnIntentGenerated += HandleIntentGenerated;

                activeSpirits.Add(definition.Id, spirit);
            }
        }

        // ----------------------------------------------------------------------
        // Event Handlers (Bubbling up from individual Spirits)
        // ----------------------------------------------------------------------

        private void HandlePresenceChanged(Spirit sourceSpirit, PresenceMode oldMode, PresenceMode newMode)
        {
            OnSpiritPresenceChanged?.Invoke(sourceSpirit, oldMode, newMode);

            if (cachedSpellCaster == null)
            {
                cachedSpellCaster = FindObjectOfType<SpellCaster>();
            }

            if (cachedSpellCaster != null && sourceSpirit.Definition.GrantedSpells != null)
            {
                if (newMode == PresenceMode.Foregrounded)
                {
                    if (sourceSpirit.Definition.GrantedSpells.Spells != null)
                    {
                        foreach (var spell in sourceSpirit.Definition.GrantedSpells.Spells)
                        {
                            cachedSpellCaster.AddSpell(spell);
                        }
                    }
                    if (sourceSpirit.Definition.GrantedSpells.MovementOverrides != null)
                    {
                        foreach (var overrideDef in sourceSpirit.Definition.GrantedSpells.MovementOverrides)
                        {
                            cachedSpellCaster.AddGlobalMovementOverride(overrideDef);
                        }
                    }
                }
                else if (oldMode == PresenceMode.Foregrounded)
                {
                    if (sourceSpirit.Definition.GrantedSpells.Spells != null)
                    {
                        foreach (var spell in sourceSpirit.Definition.GrantedSpells.Spells)
                        {
                            cachedSpellCaster.RemoveSpell(spell);
                        }
                    }
                    if (sourceSpirit.Definition.GrantedSpells.MovementOverrides != null)
                    {
                        foreach (var overrideDef in sourceSpirit.Definition.GrantedSpells.MovementOverrides)
                        {
                            cachedSpellCaster.RemoveGlobalMovementOverride(overrideDef);
                        }
                    }
                }
            }
        }

        private void HandleBehaviorChanged(Spirit sourceSpirit, BehavioralLayer layer, bool isActive)
        {
            OnSpiritBehaviorChanged?.Invoke(sourceSpirit, layer, isActive);
        }

        private void HandleIntentGenerated(Communication.CommunicationIntent intent)
        {
            // Debug log to visualize the successful generation of an intent before Phase 4 UI exists
            UnityEngine.Debug.Log($"[SpiritSystem] Spirit '{intent.SourceSpirit.Id}' generated a {intent.Priority} intent to talk about: {intent.Topic}");
            
            OnSpiritIntentGenerated?.Invoke(intent.SourceSpirit, intent);
        }

        private void HandleSpellCasterCreated(SpellCaster newCaster)
        {
            cachedSpellCaster = newCaster;

            var foregrounded = GetForegroundedSpirit();
            if (foregrounded != null && foregrounded.Definition.GrantedSpells != null)
            {
                if (foregrounded.Definition.GrantedSpells.Spells != null)
                {
                    foreach (var spell in foregrounded.Definition.GrantedSpells.Spells)
                    {
                        cachedSpellCaster.AddSpell(spell);
                    }
                }
                if (foregrounded.Definition.GrantedSpells.MovementOverrides != null)
                {
                    foreach (var overrideDef in foregrounded.Definition.GrantedSpells.MovementOverrides)
                    {
                        cachedSpellCaster.AddGlobalMovementOverride(overrideDef);
                    }
                }
            }
        }

        // ----------------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------------

        /// <summary>
        /// Returns all currently active Spirits.
        /// </summary>
        public IReadOnlyCollection<Spirit> GetAllSpirits()
        {
            return activeSpirits.Values;
        }

        /// <summary>
        /// Returns a specific Spirit by its unique ID.
        /// </summary>
        public Spirit GetSpirit(string id)
        {
            if (activeSpirits.TryGetValue(id, out var spirit))
            {
                return spirit;
            }
            return null;
        }

        /// <summary>
        /// Returns the Spirit currently in the Foregrounded state, if any.
        /// Assumes only one Spirit should be Foregrounded at a time for spellcasting purposes.
        /// </summary>
        public Spirit GetForegroundedSpirit()
        {
            // Implementation improvement: Use LINQ to cleanly find the foregrounded spirit.
            // If performance becomes a bottleneck in the future, we can cache this reference 
            // when OnSpiritPresenceChanged fires.
            return activeSpirits.Values.FirstOrDefault(s => s.RuntimeData.CurrentPresenceMode == PresenceMode.Foregrounded);
        }

        /// <summary>
        /// Distributes an event from the gameplay layer to all active Spirits.
        /// </summary>
        public void BroadcastEvent(SpiritEventData eventData)
        {
            if (eventData == null) return;

            foreach (var spirit in activeSpirits.Values)
            {
                spirit.HandleEvent(eventData);
            }
        }
    }
}
