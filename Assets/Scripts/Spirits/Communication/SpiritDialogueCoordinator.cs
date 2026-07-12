using System.Collections;
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
    /// Crucially, this class contains NO decision-making logic regarding WHAT a spirit should say.
    /// </summary>
    public class SpiritDialogueCoordinator : MonoBehaviour
    {
        private readonly List<DialogueRequest> requestQueue = new List<DialogueRequest>();
        private DialogueRequest? currentSpeaker;
        private Coroutine speakingCoroutine;

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
            speakingCoroutine = StartCoroutine(SimulateSpeaking(request));
            
            // Critical Architecture Step: Complete the emergent conversation loop by 
            // broadcasting this dialogue back into the system as a gameplay stimulus.
            var heardEvent = new DialogueHeardEventData(request.SourceSpirit, request.TextKey);
            SpiritManager.Instance?.BroadcastEvent(heardEvent);
        }

        private void InterruptCurrentSpeaker()
        {
            if (speakingCoroutine != null)
            {
                StopCoroutine(speakingCoroutine);
                speakingCoroutine = null;
            }
            
            // In a full implementation, we might requeue the interrupted line if it was Standard/Urgent.
            // For now, it is simply dropped.
            currentSpeaker = null;
        }

        private IEnumerator SimulateSpeaking(DialogueRequest request)
        {
            // Phase 3 Placeholder: Simulate UI/Audio delivery
            Debug.Log($"[DIALOGUE] Spirit {request.SourceSpirit.Id} says: '{request.TextKey}' (Priority: {request.Priority})");
            
            // Simulate channel lock for 2.5 seconds
            yield return new WaitForSeconds(2.5f);
            
            currentSpeaker = null;
            speakingCoroutine = null;
            
            // Auto-play the next item in the queue
            PlayNext();
        }
    }
}
