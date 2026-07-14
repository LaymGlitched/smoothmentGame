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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                resolver = new DialogueResolverService();
            }
            else
            {
                Destroy(gameObject);
            }
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
            var heardEvent = new DialogueHeardEventData(request.SourceSpirit, request.TextKey);
            SpiritManager.Instance?.BroadcastEvent(heardEvent);
        }

        private void InterruptCurrentSpeaker()
        {
            if (currentSpeaker.HasValue)
            {
                OnDialogueInterrupted?.Invoke(currentSpeaker.Value);
                currentSpeaker = null;
            }
        }

        /// <summary>
        /// Called by the UI/Presentation layer when it has finished displaying the current dialogue.
        /// </summary>
        public void NotifyDialogueFinished()
        {
            currentSpeaker = null;
            PlayNext();
        }
    }
}
