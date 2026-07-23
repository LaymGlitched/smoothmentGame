using System;
using System.Collections;
using System.Collections.Generic;
using GameCode.Spirits.Communication;
using GameCode.Spirits.Conversation.Data;
using GameCode.Spirits.Core;
using GameCode.Spirits.Data;
using GameCode.Spirits.Data.Conditions;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Runtime
{
    /// <summary>
    /// The central brain of the Dynamic Spirit Conversation System.
    /// 
    /// Responsibilities:
    /// • Receives gameplay trigger signals from ConversationEventBridge
    /// • Queries the ConversationBank for candidate conversations
    /// • Filters candidates by conditions, cooldowns, and speaker availability
    /// • Scores candidates and selects the best one
    /// • Manages active conversation playback (node-by-node advancement)
    /// • Handles interruption, cancellation, and optional resumption
    /// • Emits DialogueRequests to the existing SpiritDialogueCoordinator
    /// • Records completed conversations to ConversationHistory
    /// • Suppresses one-shot intents from participating Spirits during active conversations
    /// 
    /// This is the ONLY MonoBehaviour in the Conversation runtime layer.
    /// All other runtime classes are pure C# for testability and decoupling.
    /// </summary>
    public class ConversationDirector : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────
        // Configuration
        // ──────────────────────────────────────────────────────────────

        [Header("Data")]
        [Tooltip("The bank containing all authored conversation assets.")]
        [SerializeField] private ConversationBank conversationBank;

        [Header("Cooldown Tuning")]
        [Tooltip("Minimum seconds between any two conversations.")]
        [SerializeField] private float globalCooldownDuration = 3.0f;

        [Tooltip("Default category cooldown in seconds.")]
        [SerializeField] private float defaultCategoryCooldown = 15.0f;

        // ──────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────

        public static ConversationDirector Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────
        // Runtime State
        // ──────────────────────────────────────────────────────────────

        private ConversationInstance activeConversation;
        private readonly Stack<ConversationInstance> interruptStack = new Stack<ConversationInstance>();

        // Subsystems (pure C#)
        private ConversationHistory history;
        private ConversationCooldownManager cooldownManager;
        private ConversationScorer scorer;
        private DialogueLineResolver lineResolver;

        // Context tags describing current gameplay state for scoring
        private readonly HashSet<string> activeContextTags = new HashSet<string>();

        // Coroutine handle for reply delay timing
        private Coroutine replyDelayCoroutine;

        // Set of Spirit IDs currently participating in the active conversation.
        // Used to suppress their independent one-shot dialogue.
        private readonly HashSet<string> suppressedSpiritIds = new HashSet<string>();

        // ──────────────────────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when a new conversation starts playing.
        /// </summary>
        public event Action<ConversationInstance> OnConversationStarted;

        /// <summary>
        /// Fired when a conversation completes naturally (all nodes played).
        /// </summary>
        public event Action<ConversationInstance> OnConversationCompleted;

        /// <summary>
        /// Fired when a conversation is cancelled or interrupted permanently.
        /// </summary>
        public event Action<ConversationInstance> OnConversationCancelled;

        /// <summary>
        /// Fired when a conversation line is about to be emitted.
        /// Provides (ConversationInstance, ConversationNode).
        /// </summary>
        public event Action<ConversationInstance, ConversationNode> OnLineEmitted;

        // ──────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            history = new ConversationHistory();
            cooldownManager = new ConversationCooldownManager
            {
                GlobalCooldownDuration = globalCooldownDuration,
                DefaultCategoryCooldown = defaultCategoryCooldown
            };
            scorer = new ConversationScorer(history);
        }

        private void Start()
        {
            // Initialize the line resolver with the localization system
            var locBootstrapper = Reiteki.Localization.LocalizationBootstrapper.Instance;
            if (locBootstrapper != null)
            {
                lineResolver = new DialogueLineResolver(locBootstrapper);
            }

            // Subscribe to the Coordinator's line-finished notification
            if (SpiritDialogueCoordinator.Instance != null)
            {
                SpiritDialogueCoordinator.Instance.OnConversationLineFinished += HandleLineFinished;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (SpiritDialogueCoordinator.Instance != null)
            {
                SpiritDialogueCoordinator.Instance.OnConversationLineFinished -= HandleLineFinished;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Public API: Trigger Reception
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by ConversationEventBridge when a gameplay event occurs
        /// that might trigger a conversation.
        /// </summary>
        public void OnTrigger(string triggerValue)
        {
            OnTrigger(new ConversationTrigger(triggerValue));
        }

        /// <summary>
        /// Called by ConversationEventBridge when a gameplay event occurs
        /// that might trigger a conversation.
        /// </summary>
        public void OnTrigger(ConversationTrigger trigger)
        {
            if (conversationBank == null) return;

            // Ensure line resolver is ready
            EnsureLineResolverInitialized();

            // Check if this trigger cancels the active conversation
            if (activeConversation != null)
            {
                if (ShouldCancelFromTrigger(activeConversation, trigger))
                {
                    CancelActiveConversation();
                }
            }

            // Query the bank for candidates
            var candidates = conversationBank.GetByTrigger(trigger);
            if (candidates.Count == 0) return;

            // Filter candidates
            var filtered = FilterCandidates(candidates);
            if (filtered.Count == 0) return;

            // Score and select
            var ranked = scorer.ScoreAndRank(filtered, activeContextTags);
            if (ranked.Count == 0) return;

            var winner = ranked[0];

            // If there's an active conversation, check interruption rules
            if (activeConversation != null)
            {
                if (CanInterrupt(activeConversation, winner.Asset))
                {
                    InterruptActiveConversation();
                }
                else
                {
                    // Cannot interrupt — drop the incoming conversation
                    return;
                }
            }

            // Start the winning conversation
            StartConversation(winner.Asset);
        }

        // ──────────────────────────────────────────────────────────────
        // Public API: Context Tags
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a context tag to the active gameplay context.
        /// Context tags are matched against ConversationAsset.ContextTags during scoring.
        /// </summary>
        public void AddContextTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
                activeContextTags.Add(tag);
        }

        /// <summary>
        /// Removes a context tag from the active gameplay context.
        /// </summary>
        public void RemoveContextTag(string tag)
        {
            activeContextTags.Remove(tag);
        }

        /// <summary>
        /// Clears all context tags.
        /// </summary>
        public void ClearContextTags()
        {
            activeContextTags.Clear();
        }

        // ──────────────────────────────────────────────────────────────
        // Public API: State Queries
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the currently playing conversation, or null if idle.
        /// </summary>
        public ConversationInstance ActiveConversation => activeConversation;

        /// <summary>
        /// Returns true if a conversation is currently playing.
        /// </summary>
        public bool IsConversationActive => activeConversation != null;

        /// <summary>
        /// Returns true if the given Spirit is currently participating in
        /// an active conversation (and should have one-shot dialogue suppressed).
        /// </summary>
        public bool IsSpiritSuppressed(string spiritId)
        {
            return suppressedSpiritIds.Contains(spiritId);
        }

        /// <summary>
        /// Read-only access to conversation history for debugging and save/load.
        /// </summary>
        public ConversationHistory History => history;

        // ──────────────────────────────────────────────────────────────
        // Conversation Lifecycle
        // ──────────────────────────────────────────────────────────────

        private void StartConversation(ConversationAsset asset)
        {
            activeConversation = new ConversationInstance(asset);

            // Build suppression set
            suppressedSpiritIds.Clear();
            if (asset.RequiredSpirits != null)
            {
                foreach (var id in asset.RequiredSpirits)
                    suppressedSpiritIds.Add(id);
            }
            if (asset.OptionalSpirits != null)
            {
                foreach (var id in asset.OptionalSpirits)
                    suppressedSpiritIds.Add(id);
            }

            Debug.Log($"[ConversationSystem] Starting conversation: {asset.DisplayName} ({asset.Id})");

            OnConversationStarted?.Invoke(activeConversation);

            // Play the root node
            PlayCurrentNode();
        }

        private void PlayCurrentNode()
        {
            if (activeConversation == null) return;

            var node = activeConversation.GetCurrentNode();
            if (node == null)
            {
                // Invalid node — complete the conversation
                CompleteConversation();
                return;
            }

            // Check exit conditions before playing
            if (CheckExitConditions(activeConversation))
            {
                CancelActiveConversation();
                return;
            }

            // Resolve the speaker
            var speaker = SpiritManager.Instance?.GetSpirit(node.Speaker);

            // If speaker is not available (optional Spirit absent), skip to next node
            if (speaker == null)
            {
                if (!IsRequiredSpeaker(activeConversation.Asset, node.Speaker))
                {
                    AdvanceToNextNode();
                    return;
                }

                // Required speaker missing — shouldn't happen (filtered during selection)
                Debug.LogWarning($"[ConversationSystem] Required speaker '{node.Speaker}' not found. Cancelling conversation.");
                CancelActiveConversation();
                return;
            }

            // Resolve the dialogue line
            var request = lineResolver?.Resolve(node, activeConversation.Asset.BasePriority, speaker);
            if (!request.HasValue)
            {
                // Line couldn't be resolved — skip to next node
                Debug.LogWarning($"[ConversationSystem] Failed to resolve line for node {node.NodeId}. Skipping.");
                AdvanceToNextNode();
                return;
            }

            // Set behavioral state on the speaker
            speaker.BeginBehavior(BehavioralLayer.Speaking);

            // Emit to the Coordinator
            activeConversation.State = ConversationState.WaitingForLineFinish;
            OnLineEmitted?.Invoke(activeConversation, node);

            if (SpiritDialogueCoordinator.Instance != null)
            {
                SpiritDialogueCoordinator.Instance.SubmitConversationRequest(request.Value, this);
            }
        }

        private void HandleLineFinished()
        {
            if (activeConversation == null) return;
            if (activeConversation.State != ConversationState.WaitingForLineFinish) return;

            // Clear the speaking behavior on the previous speaker
            var currentNode = activeConversation.GetCurrentNode();
            if (currentNode != null)
            {
                var previousSpeaker = SpiritManager.Instance?.GetSpirit(currentNode.Speaker);
                previousSpeaker?.EndBehavior(BehavioralLayer.Speaking);
            }

            // Advance to the next node
            AdvanceToNextNode();
        }

        private void AdvanceToNextNode()
        {
            if (activeConversation == null) return;

            var currentNode = activeConversation.GetCurrentNode();
            if (currentNode == null || currentNode.IsTerminal)
            {
                // Terminal node — conversation complete
                activeConversation.MarkCurrentNodePlayed();
                CompleteConversation();
                return;
            }

            // Determine the next node
            int nextNodeId = SelectNextNode(currentNode);
            if (nextNodeId < 0)
            {
                // No valid next node — conversation complete
                activeConversation.MarkCurrentNodePlayed();
                CompleteConversation();
                return;
            }

            // Prevent infinite loops
            if (activeConversation.HasVisitedNode(nextNodeId))
            {
                Debug.LogWarning($"[ConversationSystem] Circular reference detected at node {nextNodeId}. Ending conversation.");
                activeConversation.MarkCurrentNodePlayed();
                CompleteConversation();
                return;
            }

            // Advance
            activeConversation.AdvanceToNode(nextNodeId);

            // Get the next node's reply delay
            var nextNode = activeConversation.GetCurrentNode();
            float delay = nextNode != null ? nextNode.ReplyDelay : 0f;

            if (delay > 0f)
            {
                activeConversation.State = ConversationState.DelayBeforeNextLine;
                replyDelayCoroutine = StartCoroutine(DelayThenPlay(delay));
            }
            else
            {
                PlayCurrentNode();
            }
        }

        private IEnumerator DelayThenPlay(float delay)
        {
            yield return new WaitForSeconds(delay);

            replyDelayCoroutine = null;

            if (activeConversation != null && activeConversation.State == ConversationState.DelayBeforeNextLine)
            {
                activeConversation.State = ConversationState.Playing;
                PlayCurrentNode();
            }
        }

        private void CompleteConversation()
        {
            if (activeConversation == null) return;

            activeConversation.State = ConversationState.Completed;

            Debug.Log($"[ConversationSystem] Conversation completed: {activeConversation.Asset.DisplayName}");

            // Record to history and start cooldowns
            history.Record(activeConversation);
            cooldownManager.StartCooldowns(activeConversation.Asset);

            var completed = activeConversation;
            activeConversation = null;
            suppressedSpiritIds.Clear();

            OnConversationCompleted?.Invoke(completed);

            // Check if there's an interrupted conversation to resume
            TryResumeInterrupted();
        }

        private void CancelActiveConversation()
        {
            if (activeConversation == null) return;

            // Stop any pending delay
            if (replyDelayCoroutine != null)
            {
                StopCoroutine(replyDelayCoroutine);
                replyDelayCoroutine = null;
            }

            // Clear speaking behavior on current speaker
            var currentNode = activeConversation.GetCurrentNode();
            if (currentNode != null)
            {
                var speaker = SpiritManager.Instance?.GetSpirit(currentNode.Speaker);
                speaker?.EndBehavior(BehavioralLayer.Speaking);
            }

            activeConversation.State = ConversationState.Cancelled;

            Debug.Log($"[ConversationSystem] Conversation cancelled: {activeConversation.Asset.DisplayName}");

            history.Record(activeConversation);
            cooldownManager.StartCooldowns(activeConversation.Asset);

            var cancelled = activeConversation;
            activeConversation = null;
            suppressedSpiritIds.Clear();

            OnConversationCancelled?.Invoke(cancelled);
        }

        private void InterruptActiveConversation()
        {
            if (activeConversation == null) return;

            // Stop any pending delay
            if (replyDelayCoroutine != null)
            {
                StopCoroutine(replyDelayCoroutine);
                replyDelayCoroutine = null;
            }

            // Clear speaking behavior on current speaker
            var currentNode = activeConversation.GetCurrentNode();
            if (currentNode != null)
            {
                var speaker = SpiritManager.Instance?.GetSpirit(currentNode.Speaker);
                speaker?.EndBehavior(BehavioralLayer.Speaking);
            }

            // Interrupt the current dialogue display
            SpiritDialogueCoordinator.Instance?.InterruptCurrentSpeaker();

            if (activeConversation.Asset.CanResumeAfterInterrupt)
            {
                activeConversation.State = ConversationState.Interrupted;
                interruptStack.Push(activeConversation);
                Debug.Log($"[ConversationSystem] Conversation interrupted (resumable): {activeConversation.Asset.DisplayName}");
            }
            else
            {
                activeConversation.State = ConversationState.Cancelled;
                history.Record(activeConversation);
                Debug.Log($"[ConversationSystem] Conversation interrupted (not resumable): {activeConversation.Asset.DisplayName}");
                OnConversationCancelled?.Invoke(activeConversation);
            }

            activeConversation = null;
            suppressedSpiritIds.Clear();
        }

        private void TryResumeInterrupted()
        {
            while (interruptStack.Count > 0)
            {
                var interrupted = interruptStack.Pop();

                // Check if the interrupted conversation's exit conditions have been met
                if (CheckExitConditions(interrupted))
                {
                    interrupted.State = ConversationState.Cancelled;
                    history.Record(interrupted);
                    OnConversationCancelled?.Invoke(interrupted);
                    continue;
                }

                // Check if required speakers are still available
                if (!AreRequiredSpiritsActive(interrupted.Asset))
                {
                    interrupted.State = ConversationState.Cancelled;
                    history.Record(interrupted);
                    OnConversationCancelled?.Invoke(interrupted);
                    continue;
                }

                // Resume the conversation
                activeConversation = interrupted;
                activeConversation.State = ConversationState.Playing;

                // Rebuild suppression set
                suppressedSpiritIds.Clear();
                if (interrupted.Asset.RequiredSpirits != null)
                    foreach (var id in interrupted.Asset.RequiredSpirits)
                        suppressedSpiritIds.Add(id);
                if (interrupted.Asset.OptionalSpirits != null)
                    foreach (var id in interrupted.Asset.OptionalSpirits)
                        suppressedSpiritIds.Add(id);

                Debug.Log($"[ConversationSystem] Resuming interrupted conversation: {interrupted.Asset.DisplayName}");

                // Continue from the next node
                AdvanceToNextNode();
                return;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Filtering & Selection
        // ──────────────────────────────────────────────────────────────

        private List<ConversationAsset> FilterCandidates(IReadOnlyList<ConversationAsset> candidates)
        {
            var filtered = new List<ConversationAsset>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                var asset = candidates[i];
                if (asset == null) continue;

                // One-shot check
                if (asset.IsOneShot && history.HasCompleted(asset.Id))
                    continue;

                // Cooldown check
                if (cooldownManager.IsBlocked(asset))
                    continue;

                // Required speakers check
                if (!AreRequiredSpiritsActive(asset))
                    continue;

                // Entry conditions check
                if (!EvaluateConditionGroups(asset.EntryConditions))
                    continue;

                filtered.Add(asset);
            }

            return filtered;
        }

        private bool AreRequiredSpiritsActive(ConversationAsset asset)
        {
            if (asset.RequiredSpirits == null || asset.RequiredSpirits.Length == 0)
                return true;

            if (SpiritManager.Instance == null)
                return false;

            foreach (var spiritId in asset.RequiredSpirits)
            {
                if (SpiritManager.Instance.GetSpirit(spiritId) == null)
                    return false;
            }

            return true;
        }

        private bool IsRequiredSpeaker(ConversationAsset asset, SpiritId speakerId)
        {
            if (asset.RequiredSpirits == null) return false;

            foreach (var id in asset.RequiredSpirits)
            {
                if (id == speakerId) return true;
            }

            return false;
        }

        private bool EvaluateConditionGroups(IReadOnlyList<ConditionGroup> groups)
        {
            if (groups == null || groups.Count == 0) return true;

            foreach (var group in groups)
            {
                if (!EvaluateConditionGroup(group))
                    return false;
            }

            return true;
        }

        private bool EvaluateConditionGroup(ConditionGroup group)
        {
            if (group == null || group.Nodes == null || group.Nodes.Count == 0)
                return true;

            switch (group.Operator)
            {
                case ConditionOperator.AND:
                    foreach (var node in group.Nodes)
                    {
                        if (node != null && !node.Evaluate())
                            return false;
                    }
                    return true;

                case ConditionOperator.OR:
                    foreach (var node in group.Nodes)
                    {
                        if (node != null && node.Evaluate())
                            return true;
                    }
                    return false;

                case ConditionOperator.NOT:
                    foreach (var node in group.Nodes)
                    {
                        if (node != null && node.Evaluate())
                            return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Interruption Logic
        // ──────────────────────────────────────────────────────────────

        private bool CanInterrupt(ConversationInstance active, ConversationAsset incoming)
        {
            if (!active.Asset.IsInterruptible) return false;

            // Higher priority always wins
            if (incoming.BasePriority > active.Asset.BasePriority) return true;

            // Same priority: Critical never interrupts Critical
            if (incoming.BasePriority == active.Asset.BasePriority) return false;

            return false;
        }

        private bool ShouldCancelFromTrigger(ConversationInstance active, ConversationTrigger trigger)
        {
            if (active.Asset.CancelTriggers == null) return false;

            foreach (var cancelTrigger in active.Asset.CancelTriggers)
            {
                if (cancelTrigger == trigger) return true;
            }

            return false;
        }

        private bool CheckExitConditions(ConversationInstance instance)
        {
            if (instance.Asset.ExitConditions == null || instance.Asset.ExitConditions.Count == 0)
                return false;

            // ANY exit condition group passing means the conversation should end
            foreach (var group in instance.Asset.ExitConditions)
            {
                if (EvaluateConditionGroup(group))
                    return true;
            }

            return false;
        }

        // ──────────────────────────────────────────────────────────────
        // Node Flow Selection
        // ──────────────────────────────────────────────────────────────

        private int SelectNextNode(ConversationNode currentNode)
        {
            if (currentNode.NextNodeIds == null || currentNode.NextNodeIds.Length == 0)
                return -1;

            // Single path — no branching
            if (currentNode.NextNodeIds.Length == 1)
                return currentNode.NextNodeIds[0];

            // Conditional branching: evaluate conditions in order, first pass wins
            if (currentNode.NextNodeConditions != null && currentNode.NextNodeConditions.Length > 0)
            {
                for (int i = 0; i < currentNode.NextNodeIds.Length; i++)
                {
                    // If we have a condition for this branch and it passes, select it
                    if (i < currentNode.NextNodeConditions.Length && currentNode.NextNodeConditions[i] != null)
                    {
                        if (currentNode.NextNodeConditions[i].Evaluate())
                            return currentNode.NextNodeIds[i];
                    }
                    else
                    {
                        // No condition = unconditional fallback (default branch)
                        return currentNode.NextNodeIds[i];
                    }
                }

                // No condition passed — end conversation
                return -1;
            }

            // Weighted random branching
            if (currentNode.NextNodeWeights != null &&
                currentNode.NextNodeWeights.Length == currentNode.NextNodeIds.Length)
            {
                float totalWeight = 0f;
                for (int i = 0; i < currentNode.NextNodeWeights.Length; i++)
                    totalWeight += currentNode.NextNodeWeights[i];

                if (totalWeight > 0f)
                {
                    float roll = UnityEngine.Random.Range(0f, totalWeight);
                    float accumulated = 0f;

                    for (int i = 0; i < currentNode.NextNodeIds.Length; i++)
                    {
                        accumulated += currentNode.NextNodeWeights[i];
                        if (roll < accumulated)
                            return currentNode.NextNodeIds[i];
                    }
                }
            }

            // Uniform random fallback
            return currentNode.NextNodeIds[UnityEngine.Random.Range(0, currentNode.NextNodeIds.Length)];
        }

        // ──────────────────────────────────────────────────────────────
        // Initialization Helpers
        // ──────────────────────────────────────────────────────────────

        private void EnsureLineResolverInitialized()
        {
            if (lineResolver != null) return;

            var locBootstrapper = Reiteki.Localization.LocalizationBootstrapper.Instance;
            if (locBootstrapper != null)
            {
                lineResolver = new DialogueLineResolver(locBootstrapper);
            }
            else
            {
                Debug.LogWarning("[ConversationSystem] LocalizationManager not yet initialized. Conversation lines may not resolve.");
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Public API: Manual Control
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Cancels the currently active conversation. Use for scene transitions,
        /// game over, or other hard stops.
        /// </summary>
        public void ForceCancel()
        {
            if (activeConversation != null)
            {
                CancelActiveConversation();
            }

            // Also clear the interrupt stack
            while (interruptStack.Count > 0)
            {
                var stale = interruptStack.Pop();
                stale.State = ConversationState.Cancelled;
                history.Record(stale);
            }
        }

        /// <summary>
        /// Pauses the active conversation. Resume with ResumeConversation().
        /// </summary>
        public void PauseConversation()
        {
            if (activeConversation == null) return;

            if (replyDelayCoroutine != null)
            {
                StopCoroutine(replyDelayCoroutine);
                replyDelayCoroutine = null;
            }

            activeConversation.State = ConversationState.Paused;
        }

        /// <summary>
        /// Resumes a paused conversation.
        /// </summary>
        public void ResumeConversation()
        {
            if (activeConversation == null) return;
            if (activeConversation.State != ConversationState.Paused) return;

            activeConversation.State = ConversationState.Playing;
            PlayCurrentNode();
        }

        /// <summary>
        /// Purges expired cooldowns. Call periodically (not every frame) to prevent
        /// dictionary bloat during long play sessions.
        /// </summary>
        public void PurgeCooldowns()
        {
            cooldownManager.PurgeExpired();
        }
    }
}
