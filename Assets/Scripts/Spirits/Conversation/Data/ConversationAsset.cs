using System.Collections.Generic;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Data
{
    /// <summary>
    /// Authored conversation data — the "script" for a multi-turn, multi-speaker exchange.
    /// Each asset represents one complete conversation that can be triggered, scored, and
    /// played by the ConversationDirector at runtime.
    /// 
    /// Conversations are NOT dialogue trees with player choices. They are overheard exchanges
    /// between Spirits that play non-interactively during gameplay. The player never pauses
    /// or selects responses.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Conversation/Conversation Asset", fileName = "NewConversation")]
    public class ConversationAsset : ScriptableObject
    {
        // ──────────────────────────────────────────────────────────────
        // Identity
        // ──────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Unique identifier for this conversation. Must be globally unique across all ConversationAssets.")]
        [SerializeField] private ConversationId id;

        [Tooltip("Human-readable name shown in Editor tools and debug overlays.")]
        [SerializeField] private string displayName;

        [TextArea(2, 4)]
        [Tooltip("Writer's notes describing this conversation's purpose and context.")]
        [SerializeField] private string description;

        // ──────────────────────────────────────────────────────────────
        // Triggering
        // ──────────────────────────────────────────────────────────────

        [Header("Triggering")]
        [Tooltip("Gameplay events that can start this conversation. The ConversationBank indexes assets by these triggers for O(1) lookup.")]
        [SerializeField] private ConversationTrigger[] triggers;

        [Tooltip("All condition groups must pass for this conversation to be eligible. Uses the existing ConditionNode system.")]
        [SerializeField] private List<ConditionGroup> entryConditions = new List<ConditionGroup>();

        // ──────────────────────────────────────────────────────────────
        // Metadata
        // ──────────────────────────────────────────────────────────────

        [Header("Metadata")]
        [Tooltip("Conversation-level priority floor. Determines interruption behavior and scoring bonus.")]
        [SerializeField] private PriorityTier basePriority = PriorityTier.Standard;

        [Tooltip("Category for variety scoring and category-level cooldowns.")]
        [SerializeField] private ConversationCategory category = ConversationCategory.Reaction;

        [Tooltip("Seconds before this conversation can play again after completing. 0 = no cooldown.")]
        [Min(0f)]
        [SerializeField] private float cooldownDuration = 120f;

        [Tooltip("Shared cooldown group name. If set, ALL conversations in this group share a single cooldown timer.")]
        [SerializeField] private string cooldownGroup;

        [Tooltip("If true, this conversation can only ever play once per playthrough. Persisted across save/load.")]
        [SerializeField] private bool isOneShot;

        [Tooltip("If true, higher-priority conversations can interrupt this one mid-playback.")]
        [SerializeField] private bool isInterruptible = true;

        [Tooltip("If true AND interrupted, this conversation can resume from where it left off after the interrupting conversation finishes.")]
        [SerializeField] private bool canResumeAfterInterrupt;

        // ──────────────────────────────────────────────────────────────
        // Participants
        // ──────────────────────────────────────────────────────────────

        [Header("Participants")]
        [Tooltip("All of these Spirits must be currently active in SpiritManager for this conversation to be eligible.")]
        [SerializeField] private SpiritId[] requiredSpirits;

        [Tooltip("These Spirits can participate if present but are not required. Nodes assigned to absent optional Spirits are skipped.")]
        [SerializeField] private SpiritId[] optionalSpirits;

        // ──────────────────────────────────────────────────────────────
        // Scoring
        // ──────────────────────────────────────────────────────────────

        [Header("Scoring")]
        [Tooltip("Starting relevance score. Higher = more likely to be selected when multiple conversations match the same trigger.")]
        [SerializeField] private float baseScore = 50f;

        [Tooltip("Contextual tags for scoring bonuses. Matched against the current gameplay context (e.g., 'combat', 'exploration', 'boss').")]
        [SerializeField] private string[] contextTags;

        // ──────────────────────────────────────────────────────────────
        // Flow
        // ──────────────────────────────────────────────────────────────

        [Header("Conversation Flow")]
        [Tooltip("All nodes in the conversation. Nodes reference each other by index via NextNodeIds.")]
        [SerializeField] private ConversationNode[] nodes;

        [Tooltip("The index of the first node to play. Usually 0.")]
        [SerializeField] private int rootNodeId;

        // ──────────────────────────────────────────────────────────────
        // Exit
        // ──────────────────────────────────────────────────────────────

        [Header("Exit")]
        [Tooltip("If any of these condition groups pass during playback, the conversation ends early.")]
        [SerializeField] private List<ConditionGroup> exitConditions = new List<ConditionGroup>();

        [Tooltip("Gameplay events that immediately abort this conversation when received.")]
        [SerializeField] private ConversationTrigger[] cancelTriggers;

        // ──────────────────────────────────────────────────────────────
        // Public API (Read-Only)
        // ──────────────────────────────────────────────────────────────

        public ConversationId Id => id;
        public string DisplayName => displayName;
        public string Description => description;

        public ConversationTrigger[] Triggers => triggers;
        public IReadOnlyList<ConditionGroup> EntryConditions => entryConditions;

        public PriorityTier BasePriority => basePriority;
        public ConversationCategory Category => category;
        public float CooldownDuration => cooldownDuration;
        public string CooldownGroup => cooldownGroup;
        public bool IsOneShot => isOneShot;
        public bool IsInterruptible => isInterruptible;
        public bool CanResumeAfterInterrupt => canResumeAfterInterrupt;

        public SpiritId[] RequiredSpirits => requiredSpirits;
        public SpiritId[] OptionalSpirits => optionalSpirits;

        public float BaseScore => baseScore;
        public string[] ContextTags => contextTags;

        public ConversationNode[] Nodes => nodes;
        public int RootNodeId => rootNodeId;

        public IReadOnlyList<ConditionGroup> ExitConditions => exitConditions;
        public ConversationTrigger[] CancelTriggers => cancelTriggers;

        // ──────────────────────────────────────────────────────────────
        // Node Lookup
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the node at the given ID, or null if out of range.
        /// Node IDs map directly to array indices.
        /// </summary>
        public ConversationNode GetNode(int nodeId)
        {
            if (nodes == null || nodeId < 0 || nodeId >= nodes.Length)
                return null;
            return nodes[nodeId];
        }

        /// <summary>
        /// Returns the root node of this conversation.
        /// </summary>
        public ConversationNode GetRootNode()
        {
            return GetNode(rootNodeId);
        }

        /// <summary>
        /// Returns the total number of nodes in this conversation.
        /// </summary>
        public int NodeCount => nodes != null ? nodes.Length : 0;

        // ──────────────────────────────────────────────────────────────
        // Validation
        // ──────────────────────────────────────────────────────────────

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            // Auto-assign NodeIds to match array indices
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i] != null)
                    {
                        nodes[i].NodeId = i;
                    }
                }
            }
        }
    }
}
