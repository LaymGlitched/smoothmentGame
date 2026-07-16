using System.Collections.Generic;
using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Data-driven authoring container for a Spirit's dialogue.
    /// Maps abstract CommunicationIntents (Topic + Priority) to specific localized entries.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Spirit Communication Profile", fileName = "NewCommunicationProfile")]
    public class SpiritCommunicationProfile : ScriptableObject
    {
        [Tooltip("The list of all authored dialogue lines for this Spirit.")]
        [SerializeField] private List<DialogueEntry> entries = new List<DialogueEntry>();

        /// <summary>
        /// Read-only access to the authored entries.
        /// </summary>
        public IReadOnlyList<DialogueEntry> Entries => entries;
    }
}
