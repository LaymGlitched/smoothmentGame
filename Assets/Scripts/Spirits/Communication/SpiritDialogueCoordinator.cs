using System;
using System.Collections.Generic;
using GameCode.Spirits.Core;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Communication
{
    /// <summary>
    /// Global coordinator for Spirit Dialogue. 
    /// Manages the speaker channel, queues incoming dialogue requests, handles interruptions,
    /// and broadcasts DialogueHeardEvents to close the emergent conversation loop.
    /// Uses a pure C# Resolver to translate Intents into Requests.
    /// </summary>
    public class SpiritDialogueCoordinator : MonoBehaviour
    {
        private readonly List<DialogueRequest> requestQueue = new List<DialogueRequest>();
        private DialogueRequest? currentSpeaker;
        
        private DialogueResolverService resolver;

        public static SpiritDialogueCoordinator Instance { get; private set; }

        public event Action<DialogueRequest> OnDialogueStarted;
        public event Action<DialogueRequest> OnDialogueInterrupted;

        /// <summary>
        /// Fired when a conversation-sourced dialogue line finishes displaying.
        /// The ConversationDirector subscribes to this to advance to the next node.
        /// </summary>
        public event Action OnConversationLineFinished;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Resolver will be instantiated when InitializeLocalization is called.
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void InitializeLocalization(Reiteki.Localization.Core.LocalizationManager localizationManager)
        {
            resolver = new DialogueResolverService(localizationManager);
        }

        private void Start()
        {
            if (SpiritManager.Instance != null)
            {
                SpiritManager.Instance.OnSpiritIntentGenerated += HandleIntentGenerated;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (SpiritManager.Instance != null)
            {
                SpiritManager.Instance.OnSpiritIntentGenerated -= HandleIntentGenerated;
            }
        }

        private void HandleIntentGenerated(Spirit spirit, CommunicationIntent intent)
        {
            if (resolver == null)
            {
                if (Reiteki.Localization.LocalizationBootstrapper.Instance != null)
                {
                    InitializeLocalization(Reiteki.Localization.LocalizationBootstrapper.Instance);
                }
                else
                {
                    Debug.LogWarning("[SpiritDialogueCoordinator] Ignored intent because LocalizationManager is not yet initialized. Did you forget to add the LocalizationBootstrapper to the scene?");
                    return;
                }
            }

            // Suppress one-shot intents from Spirits currently in an active conversation.
            // The ConversationDirector handles their dialogue — independent impulses would conflict.
            if (Conversation.Runtime.ConversationDirector.Instance != null &&
                Conversation.Runtime.ConversationDirector.Instance.IsSpiritSuppressed(spirit.Id))
            {
                return;
            }

            // The Resolver determines IF and HOW the intent becomes a concrete request.
            var request = resolver.ResolveIntent(intent);
            if (request.HasValue)
            {
                SubmitRequest(request.Value);
            }
        }

        /// <summary>
        /// Submits a fully resolved dialogue request to the global channel.
        /// </summary>
        public void SubmitRequest(DialogueRequest request)
        {
            // Interruption Logic: If the new request is strictly more important than 
            // the current speaker, cut them off.
            if (currentSpeaker.HasValue && request.Priority > currentSpeaker.Value.Priority)
            {
                InterruptCurrentSpeaker();
                PlayDialogue(request);
                return;
            }

            requestQueue.Add(request);
            SortQueue();

            if (!currentSpeaker.HasValue)
            {
                PlayNext();
            }
        }

        /// <summary>
        /// Submits a dialogue request originating from the Conversation System.
        /// When this line finishes, the ConversationDirector is notified so it can advance.
        /// </summary>
        /// <param name="request">The fully resolved dialogue request.</param>
        /// <param name="source">The ConversationDirector that owns this line.</param>
        public void SubmitConversationRequest(DialogueRequest request, Conversation.Runtime.ConversationDirector source)
        {
            isCurrentLineConversationSourced = true;

            // Use the same interruption and queuing logic
            if (currentSpeaker.HasValue && request.Priority > currentSpeaker.Value.Priority)
            {
                InterruptCurrentSpeaker();
                PlayDialogue(request);
                return;
            }

            requestQueue.Add(request);
            SortQueue();

            if (!currentSpeaker.HasValue)
            {
                PlayNext();
            }
        }

        // Tracks whether the current line was submitted by the Conversation System
        private bool isCurrentLineConversationSourced;

        private void SortQueue()
        {
            // Sort by priority descending (Critical -> Ambient)
            requestQueue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private void PlayNext()
        {
            if (requestQueue.Count == 0) return;

            var next = requestQueue[0];
            requestQueue.RemoveAt(0);
            PlayDialogue(next);
        }

        private void PlayDialogue(DialogueRequest request)
        {
            currentSpeaker = request;
            
            // Notify UI to display this line for 'request.Duration' seconds
            OnDialogueStarted?.Invoke(request);
            
            // Critical Architecture Step: Complete the emergent conversation loop by 
            // broadcasting this dialogue back into the system as a gameplay stimulus.
            var heardEvent = new DialogueHeardEventData(request.SourceSpirit, request.LocalizedText);
            SpiritManager.Instance?.BroadcastEvent(heardEvent);
        }

        /// <summary>
        /// Interrupts the current speaker. Made public so the ConversationDirector
        /// can interrupt during conversation takeover.
        /// </summary>
        public void InterruptCurrentSpeaker()
        {
            if (currentSpeaker.HasValue)
            {
                OnDialogueInterrupted?.Invoke(currentSpeaker.Value);
                isCurrentLineConversationSourced = false;
                currentSpeaker = null;
            }
        }

        /// <summary>
        /// Called by the UI/Presentation layer when it has finished displaying the current dialogue.
        /// </summary>
        public void NotifyDialogueFinished()
        {
            bool wasConversationLine = isCurrentLineConversationSourced;
            isCurrentLineConversationSourced = false;
            currentSpeaker = null;

            // Notify the ConversationDirector if this was a conversation line
            if (wasConversationLine)
            {
                OnConversationLineFinished?.Invoke();
            }

            PlayNext();
        }
    }
}
