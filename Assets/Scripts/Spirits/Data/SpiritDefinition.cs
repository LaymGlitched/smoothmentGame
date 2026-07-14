using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Immutable authoring data that defines who a Spirit is.
    /// This ScriptableObject acts as the template from which runtime Spirits are instantiated.
    /// It contains only static configuration and should never contain mutable state.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Spirit Definition", fileName = "NewSpiritDefinition")]
    public class SpiritDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique internal identifier for this Spirit (e.g., 'ignis', 'zenka').")]
        [SerializeField] private string id;

        [Tooltip("The localized, player-facing name of the Spirit.")]
        [SerializeField] private string displayName;

        [Tooltip("A brief narrative description of the Spirit.")]
        [TextArea(3, 6)]
        [SerializeField] private string description;

        // Future placeholders - explicitly leaving architectural slots for Phase 2 systems
        // without building the underlying types yet.
        
        // [Header("Phase 2 Placeholders")]
        // [Tooltip("Defines the elemental magic this Spirit channels.")]
        // [SerializeField] private PowerBindingDefinition powerBinding;

        // [Tooltip("Defines the Spirit's temperament, philosophy, and knowledge domain.")]
        // [SerializeField] private PersonalityProfile personality;

        [Header("Phase 2: Inner Life")]
        [Tooltip("Defines the Spirit's temperament and psychological axes.")]
        [SerializeField] private SpiritIdentityProfile identityProfile;

        [Header("Phase 4: Presentation")]
        [Tooltip("Defines the dialogue lines available to this Spirit.")]
        [SerializeField] private SpiritCommunicationProfile communicationProfile;

        /// <summary>
        /// Unique internal identifier for this Spirit.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// The localized, player-facing name of the Spirit.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// A brief narrative description of the Spirit.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Defines the Spirit's temperament and psychological axes.
        /// </summary>
        public SpiritIdentityProfile IdentityProfile => identityProfile;

        /// <summary>
        /// Defines the authored dialogue entries for this Spirit.
        /// </summary>
        public SpiritCommunicationProfile CommunicationProfile => communicationProfile;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[SpiritSystem] SpiritDefinition '{name}' is missing a unique ID.");
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                Debug.LogWarning($"[SpiritSystem] SpiritDefinition '{name}' is missing a Display Name.");
            }
        }
    }
}
