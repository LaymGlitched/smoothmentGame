using UnityEngine;
using TMPro; 
using GameCode.Spirits.Communication;
using System.Collections;

namespace GameCode.Spirits.UI
{
    /// <summary>
    /// Pure presentation layer for displaying Spirit Dialogue.
    /// It blindly renders what it is given by the Coordinator and manages fading animations.
    /// </summary>
    public class SpiritSubtitleUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Settings")]
        [Tooltip("Fade transition duration in seconds.")]
        [SerializeField] private float fadeDuration = 0.25f;

        private Coroutine activeDisplayRoutine;

        private void Start()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            
            if (SpiritDialogueCoordinator.Instance != null)
            {
                SpiritDialogueCoordinator.Instance.OnDialogueStarted += HandleDialogueStarted;
                SpiritDialogueCoordinator.Instance.OnDialogueInterrupted += HandleDialogueInterrupted;
            }
        }

        private void OnDestroy()
        {
            if (SpiritDialogueCoordinator.Instance != null)
            {
                SpiritDialogueCoordinator.Instance.OnDialogueStarted -= HandleDialogueStarted;
                SpiritDialogueCoordinator.Instance.OnDialogueInterrupted -= HandleDialogueInterrupted;
            }
        }

        private void HandleDialogueStarted(DialogueRequest request)
        {
            if (activeDisplayRoutine != null)
            {
                StopCoroutine(activeDisplayRoutine);
            }

            // In Phase 4, TextKey is just the localization key. 
            // In the future, this is where we query the Localization System.
            if (speakerNameText != null) speakerNameText.text = request.SourceSpirit.Definition.DisplayName;
            if (subtitleText != null) subtitleText.text = request.TextKey; 

            activeDisplayRoutine = StartCoroutine(DisplaySubtitleRoutine(request.Duration));
        }

        private void HandleDialogueInterrupted(DialogueRequest request)
        {
            if (activeDisplayRoutine != null)
            {
                StopCoroutine(activeDisplayRoutine);
            }
            activeDisplayRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator DisplaySubtitleRoutine(float duration)
        {
            // Fade In
            yield return FadeRoutine(0f, 1f, fadeDuration);

            // Wait for specified duration
            yield return new WaitForSeconds(duration);

            // Fade Out
            yield return FadeRoutine(1f, 0f, fadeDuration);

            activeDisplayRoutine = null;

            // Notify coordinator we're done
            SpiritDialogueCoordinator.Instance?.NotifyDialogueFinished();
        }

        private IEnumerator FadeOutRoutine()
        {
            if (canvasGroup != null)
            {
                yield return FadeRoutine(canvasGroup.alpha, 0f, fadeDuration);
            }
            activeDisplayRoutine = null;
            // No need to notify the coordinator on interrupt, it handles it internally
        }

        private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float time)
        {
            if (canvasGroup == null) yield break;

            if (time <= 0f)
            {
                canvasGroup.alpha = endAlpha;
                yield break;
            }

            float elapsed = 0f;
            canvasGroup.alpha = startAlpha;

            while (elapsed < time)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / time);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
        }
    }
}
