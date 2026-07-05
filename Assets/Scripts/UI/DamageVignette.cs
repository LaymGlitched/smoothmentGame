using System.Collections;
using GameCode.PlayerScripts;
using GameCode.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace GameCode.UI
{
    public class DamageVignette : MonoBehaviour
    {
        [Header("Target References")]
        [Tooltip("The player's Health component. Auto-assigns on Start if left blank.")]
        [SerializeField]
        private Health healthComponent;

        [Header("Damage Vignette Settings")]
        [SerializeField]
        private Image damageVignetteImage;

        [SerializeField]
        private Color flashColor = new Color(0.8f, 0f, 0f, 0.6f);

        [SerializeField]
        private float flashDuration = 0.35f;

        [SerializeField]
        private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [SerializeField]
        private float lowHealthVignetteThreshold = 0.3f;

        [SerializeField]
        private Color lowHealthVignetteColor = new Color(0.6f, 0f, 0f, 0.4f);

        [SerializeField]
        private bool pulseVignetteAtLowHealth = true;

        [SerializeField]
        private float vignettePulseSpeed = 2f;

        [SerializeField]
        private float vignetteMinPulseAlpha = 0.15f;

        [SerializeField]
        private float vignetteMaxPulseAlpha = 0.5f;

        private float currentFlashAlpha = 0f;
        private Coroutine vignetteFlashCoroutine;

        private void Start()
        {
            if (healthComponent == null)
            {
                healthComponent = FindAnyObjectByType<Health>();
            }

            if (healthComponent != null)
            {
                healthComponent.OnDamaged.AddListener(OnPlayerDamaged);
            }

            if (damageVignetteImage != null)
            {
                damageVignetteImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
                damageVignetteImage.enabled = true;
            }
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDamaged.RemoveListener(OnPlayerDamaged);
            }
        }

        private void OnPlayerDamaged(float damage, DamageType type)
        {
            if (!gameObject.activeInHierarchy) return;

            if (vignetteFlashCoroutine != null)
            {
                StopCoroutine(vignetteFlashCoroutine);
            }
            vignetteFlashCoroutine = StartCoroutine(DoDamageVignetteFlash());
        }

        private IEnumerator DoDamageVignetteFlash()
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flashDuration);
                float val = flashCurve.Evaluate(t);
                currentFlashAlpha = val * flashColor.a;
                yield return null;
            }
            currentFlashAlpha = 0f;
            vignetteFlashCoroutine = null;
        }

        private void Update()
        {
            UpdateVignetteOverlay();
        }

        private void UpdateVignetteOverlay()
        {
            if (damageVignetteImage == null)
                return;

            float lowHealthVignetteAlpha = 0f;
            if (healthComponent != null && !healthComponent.IsDead)
            {
                float hpPercent = healthComponent.HealthPercentage;
                if (hpPercent < lowHealthVignetteThreshold)
                {
                    float severity = 1f - (hpPercent / lowHealthVignetteThreshold);
                    if (pulseVignetteAtLowHealth)
                    {
                        float pulse = Mathf.Lerp(
                            vignetteMinPulseAlpha,
                            vignetteMaxPulseAlpha,
                            (Mathf.Sin(Time.time * vignettePulseSpeed * Mathf.PI) + 1f) * 0.5f
                        );
                        lowHealthVignetteAlpha = pulse * severity;
                    }
                    else
                    {
                        lowHealthVignetteAlpha = vignetteMaxPulseAlpha * severity;
                    }
                }
            }

            float finalAlpha = Mathf.Max(currentFlashAlpha, lowHealthVignetteAlpha);

            Color finalVignetteColor;
            if (currentFlashAlpha > 0f)
            {
                float blend = Mathf.Clamp01(currentFlashAlpha / Mathf.Max(flashColor.a, 0.001f));
                finalVignetteColor = Color.Lerp(lowHealthVignetteColor, flashColor, blend);
            }
            else
            {
                finalVignetteColor = lowHealthVignetteColor;
            }

            damageVignetteImage.color = new Color(
                finalVignetteColor.r,
                finalVignetteColor.g,
                finalVignetteColor.b,
                finalAlpha
            );
        }
    }
}
