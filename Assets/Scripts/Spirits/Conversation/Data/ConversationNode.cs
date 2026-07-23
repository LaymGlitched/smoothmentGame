using System;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using GameCode.Spirits.Data.Conditions;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Data
{
    /// <summary>
    /// A single beat in a conversation — one speaker says one line.
    /// Supports branching (multiple NextNodeIds), weighted random selection
    /// among branches, conditional branching, and line variants for natural variety.
    /// </summary>
    [Serializable]
    public class ConversationNode
    {
        [Tooltip("Unique identifier within this conversation (auto-set to array index).")]
        public int NodeId;

        [Header("Speaker")]
        [Tooltip("Which Spirit speaks this line.")]
        public SpiritId Speaker;

        [Header("Dialogue")]
        [Tooltip("Primary localization key for this line's text.")]
        public Reiteki.Localization.Core.LocalizationKey LineKey;

        [Tooltip("Alternative localization keys selected at random for variety. If empty, LineKey is always used.")]
        public Reiteki.Localization.Core.LocalizationKey[] Variants;

        [Tooltip("Selection weights for Variants. Must match Variants length. If empty, uniform distribution is used.")]
        public float[] VariantWeights;

        [Header("Timing")]
        [Tooltip("How long this line displays. 0 = auto-calculate from word count.")]
        [Min(0f)]
        public float Duration;

        [Tooltip("Pause in seconds before this line plays after the previous line finishes. Creates natural conversational rhythm.")]
        [Min(0f)]
        public float ReplyDelay = 0.3f;

        [Header("Priority")]
        [Tooltip("If true, this node overrides the conversation-level priority with LinePriority.")]
        public bool OverridePriority;

        [Tooltip("Line-level priority override. Only used when OverridePriority is true.")]
        public PriorityTier LinePriority;

        [Header("Flow")]
        [Tooltip("Node IDs that can follow this node. Empty = terminal node (conversation ends). Multiple = branching.")]
        public int[] NextNodeIds;

        [Tooltip("Selection weights for NextNodeIds when multiple branches exist. Must match NextNodeIds length. If empty, uniform distribution is used.")]
        public float[] NextNodeWeights;

        [Tooltip("Conditions evaluated per branch to determine which NextNodeId is selected. Evaluated in order; first passing branch wins. If none pass and no unconditional branch exists, conversation ends.")]
        [SerializeReference]
        public ConditionNode[] NextNodeConditions;

        /// <summary>
        /// Returns true if this node has no follow-up nodes (terminal node).
        /// </summary>
        public bool IsTerminal => NextNodeIds == null || NextNodeIds.Length == 0;
    }
}
