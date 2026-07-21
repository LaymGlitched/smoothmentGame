using System.Collections;
using GameCode.PlayerScripts;
using GameCode.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

        [Header("Post Processing Desaturation Settings")]
        [Tooltip("Optional post-processing Volume reference. Will auto-find or create if unassigned.")]
        [SerializeField]
        private Volume postProcessVolume;

        [SerializeField]
        private bool enableLowHealthDesaturation = true;

        [Tooltip("Health threshold below which screen desaturation starts.")]
        [SerializeField]
        private float desaturationStartHealth = 30f;

        [Tooltip("Health threshold at which maximum desaturation is reached.")]
        [SerializeField]
        private float desaturationMaxHealth = 5f;

        [Tooltip("Saturation value at maximum desaturation (-100 is complete grayscale).")]
        [Range(-100f, 0f)]
        [SerializeField]
        private float maxDesaturationValue = -100f;

        [Tooltip("Normal saturation value when health is above the desaturation start threshold.")]
        [SerializeField]
        private float defaultSaturationValue = 0f;

        private float currentFlashAlpha = 0f;
        private Coroutine vignetteFlashCoroutine;
        private ColorAdjustments colorAdjustments;

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

            EnsureVolumeAndColorAdjustments();
        }

        private void OnDisable()
        {
            ResetDesaturation();
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDamaged.RemoveListener(OnPlayerDamaged);
            }

            ResetDesaturation();
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
            UpdateDesaturation();
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

        private void EnsureVolumeAndColorAdjustments()
        {
            if (!enableLowHealthDesaturation) return;

            if (postProcessVolume == null)
            {
                postProcessVolume = GetComponent<Volume>();
            }
            if (postProcessVolume == null)
            {
                postProcessVolume = FindAnyObjectByType<Volume>();
            }
            if (postProcessVolume == null)
            {
                postProcessVolume = gameObject.AddComponent<Volume>();
                postProcessVolume.isGlobal = true;
                postProcessVolume.priority = 10f;
            }

            if (postProcessVolume != null)
            {
                if (postProcessVolume.profile == null)
                {
                    postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                }

                if (colorAdjustments == null)
                {
                    if (!postProcessVolume.profile.TryGet<ColorAdjustments>(out colorAdjustments))
                    {
                        colorAdjustments = postProcessVolume.profile.Add<ColorAdjustments>();
                    }
                }
            }
        }

        private void UpdateDesaturation()
        {
            if (!enableLowHealthDesaturation) return;

            EnsureVolumeAndColorAdjustments();

            if (colorAdjustments == null) return;

            float targetSaturation = defaultSaturationValue;

            if (healthComponent != null)
            {
                if (healthComponent.IsDead)
                {
                    targetSaturation = maxDesaturationValue;
                }
                else
                {
                    float currentHealth = healthComponent.CurrentHealth;
                    if (currentHealth < desaturationStartHealth)
                    {
                        float t = Mathf.InverseLerp(desaturationStartHealth, desaturationMaxHealth, currentHealth);
                        targetSaturation = Mathf.Lerp(defaultSaturationValue, maxDesaturationValue, t);
                    }
                }
            }

            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = targetSaturation;
        }

        private void ResetDesaturation()
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = defaultSaturationValue;
            }
        }
    }
}
