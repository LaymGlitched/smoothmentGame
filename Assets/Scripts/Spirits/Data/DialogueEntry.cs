using System;
using GameCode.Spirits.Core;
using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Represents a single authored line of dialogue mapped to a cognitive intent.
    /// Uses localization keys exclusively to separate content from logic.
    /// </summary>
    [Serializable]
    public struct DialogueEntry
    {
        [Tooltip("The cognitive topic this line responds to (e.g., 'VesselSafety').")]
        public TopicId Topic;

        [Tooltip("The required urgency level for this line to be selected.")]
        public PriorityTier Priority;

        [Tooltip("The localization key used to fetch the actual text.")]
        public Reiteki.Localization.Core.LocalizationKey LocalizationKey;

        [Header("Presentation Metadata")]
        [Tooltip("How long this line should display. If 0, the Resolver calculates a fallback duration.")]
        public float Duration;

        [Header("Phase 5+ Placeholders")]
        [Tooltip("Future: Determines probability when multiple valid entries exist.")]
        public float Weight;

        [Tooltip("Future: Prevents repeating lines in the same group too often.")]
        public string CooldownGroup;

        [Tooltip("Future: Contextual tags for dynamic selection.")]
        public string[] Tags;
    }
}
