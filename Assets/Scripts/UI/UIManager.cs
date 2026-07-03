using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameCode.PlayerScripts;
using GameCode.Magic;
using GameCode.Shared;

namespace GameCode.UI
{
    /// <summary>
    /// Centralized UI manager that hooks into existing Player and Magic systems externally.
    /// Requires zero modifications to modular gameplay scripts.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Target References")]
        [Tooltip("The player's Health component. Auto-assigns on Start if left blank.")]
        [SerializeField] private Health healthComponent;

        [Tooltip("The player's Mana component. Auto-assigns on Start if left blank.")]
        [SerializeField] private Mana manaComponent;

        [Tooltip("The player's SpellCaster component. Auto-assigns on Start if left blank.")]
        [SerializeField] private SpellCaster spellCaster;

        [Header("Health UI Settings")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Color healthNormalColor = Color.green;
        [SerializeField] private Color healthLowColor = Color.red;
        [Range(0f, 1f)] [SerializeField] private float healthLowThreshold = 0.3f;
        [SerializeField] private bool pulseHealthBarWhenLow = true;

        [Header("Mana UI Settings")]
        [SerializeField] private Slider manaSlider;
        [SerializeField] private Image manaFillImage;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private Color manaNormalColor = Color.blue;
        [SerializeField] private Color manaLowColor = new Color(0.5f, 0.7f, 1f); // Lighter blue

        [Header("Damage Vignette Settings")]
        [SerializeField] private Image damageVignetteImage;
        [SerializeField] private Color flashColor = new Color(0.8f, 0f, 0f, 0.6f);
        [SerializeField] private float flashDuration = 0.35f;
        [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [SerializeField] private float lowHealthVignetteThreshold = 0.3f;
        [SerializeField] private Color lowHealthVignetteColor = new Color(0.6f, 0f, 0f, 0.4f);
        [SerializeField] private bool pulseVignetteAtLowHealth = true;
        [SerializeField] private float vignettePulseSpeed = 2f;
        [SerializeField] private float vignetteMinPulseAlpha = 0.15f;
        [SerializeField] private float vignetteMaxPulseAlpha = 0.5f;

        [Header("Spell UI Settings")]
        [SerializeField] private TMP_Text currentSpellNameText;
        [SerializeField] private Image spellIconImage;
        [SerializeField] private Image spellCooldownOverlay;
        [SerializeField] private TMP_Text spellCooldownText;
        [SerializeField] private bool hideCooldownWhenReady = true;

        [Header("Spell Charging UI")]
        [SerializeField] private GameObject chargeContainer;
        [SerializeField] private Slider chargeSlider;
        [SerializeField] private Image chargeFillImage;
        [SerializeField] private TMP_Text chargeText;
        [SerializeField] private Color chargeColorNormal = Color.cyan;
        [SerializeField] private Color chargeColorFull = Color.yellow;

        [Header("Hotbar Slots")]
        [Tooltip("The outlines or highlights corresponding to available spells in the SpellCaster's AvailableSpells array.")]
        [SerializeField] private GameObject[] spellSlotHighlights;

        [Header("UI Polishing")]
        [SerializeField] private float barSmoothSpeed = 10f;
        [SerializeField] private float activeSpellIconScalePulse = 1.25f;
        [SerializeField] private float iconPulseDuration = 0.15f;

        // Internal values for smooth interpolation
        private float healthTargetFill = 1f;
        private float healthCurrentFill = 1f;
        private float manaTargetFill = 1f;
        private float manaCurrentFill = 1f;

        private float currentFlashAlpha = 0f;
        private Coroutine vignetteFlashCoroutine;
        private Vector3 originalIconScale = Vector3.one;
        private Spell lastEquippedSpell = null;
        private Coroutine iconPulseCoroutine;

        private void Start()
        {
            // Auto-assign components on player if not set
            if (healthComponent == null)
                healthComponent = FindObjectOfType<Health>();

            if (manaComponent == null)
                manaComponent = FindObjectOfType<Mana>();

            if (spellCaster == null)
                spellCaster = FindObjectOfType<SpellCaster>();

            // Subscribe to Health events
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged.AddListener(OnHealthChanged);
                healthComponent.OnDamaged.AddListener(OnPlayerDamaged);
                
                // Initialize health values
                OnHealthChanged(healthComponent.CurrentHealth, healthComponent.MaxHealth);
            }

            // Subscribe to Mana events
            if (manaComponent != null)
            {
                manaComponent.OnManaChanged.AddListener(OnManaChanged);
                
                // Initialize mana values
                OnManaChanged(manaComponent.CurrentMana, manaComponent.MaxMana);
            }

            // Initialize vignette state
            if (damageVignetteImage != null)
            {
                damageVignetteImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
                damageVignetteImage.enabled = true;
            }

            // Initialize spell icon scale
            if (spellIconImage != null)
            {
                originalIconScale = spellIconImage.rectTransform.localScale;
            }

            // Initialize active charging container state
            if (chargeContainer != null)
            {
                chargeContainer.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged.RemoveListener(OnHealthChanged);
                healthComponent.OnDamaged.RemoveListener(OnPlayerDamaged);
            }

            if (manaComponent != null)
            {
                manaComponent.OnManaChanged.RemoveListener(OnManaChanged);
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            healthTargetFill = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            
            // If smooth speed is zero, update immediately
            if (barSmoothSpeed <= 0f)
            {
                healthCurrentFill = healthTargetFill;
            }

            UpdateHealthText(current, max);
        }

        private void OnManaChanged(float current, float max)
        {
            manaTargetFill = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            
            // If smooth speed is zero, update immediately
            if (barSmoothSpeed <= 0f)
            {
                manaCurrentFill = manaTargetFill;
            }

            UpdateManaText(current, max);
        }

        private void OnPlayerDamaged(float damage, DamageType type)
        {
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
            UpdateResourceBarSmoothing();
            UpdateVignetteOverlay();
            UpdateSpellSystemUI();
        }

        private void UpdateResourceBarSmoothing()
        {
            // Health Bar
            if (barSmoothSpeed > 0f)
            {
                healthCurrentFill = Mathf.Lerp(healthCurrentFill, healthTargetFill, Time.deltaTime * barSmoothSpeed);
                if (Mathf.Abs(healthCurrentFill - healthTargetFill) < 0.001f)
                    healthCurrentFill = healthTargetFill;
            }
            else
            {
                healthCurrentFill = healthTargetFill;
            }

            if (healthSlider != null)
            {
                healthSlider.value = healthCurrentFill;
            }
            else if (healthFillImage != null)
            {
                healthFillImage.fillAmount = healthCurrentFill;
            }

            // Health color low pulse
            if (healthFillImage != null)
            {
                if (healthTargetFill <= healthLowThreshold)
                {
                    if (pulseHealthBarWhenLow)
                    {
                        float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                        healthFillImage.color = Color.Lerp(healthLowColor * 0.7f, healthLowColor, pulse);
                    }
                    else
                    {
                        healthFillImage.color = healthLowColor;
                    }
                }
                else
                {
                    healthFillImage.color = healthNormalColor;
                }
            }

            // Mana Bar
            if (barSmoothSpeed > 0f)
            {
                manaCurrentFill = Mathf.Lerp(manaCurrentFill, manaTargetFill, Time.deltaTime * barSmoothSpeed);
                if (Mathf.Abs(manaCurrentFill - manaTargetFill) < 0.001f)
                    manaCurrentFill = manaTargetFill;
            }
            else
            {
                manaCurrentFill = manaTargetFill;
            }

            if (manaSlider != null)
            {
                manaSlider.value = manaCurrentFill;
            }
            else if (manaFillImage != null)
            {
                manaFillImage.fillAmount = manaCurrentFill;
            }

            if (manaFillImage != null)
            {
                manaFillImage.color = (manaTargetFill < 0.2f) ? manaLowColor : manaNormalColor;
            }
        }

        private void UpdateVignetteOverlay()
        {
            if (damageVignetteImage == null) return;

            float lowHealthVignetteAlpha = 0f;
            if (healthComponent != null && !healthComponent.IsDead)
            {
                float hpPercent = healthComponent.HealthPercentage;
                if (hpPercent < lowHealthVignetteThreshold)
                {
                    float severity = 1f - (hpPercent / lowHealthVignetteThreshold);
                    if (pulseVignetteAtLowHealth)
                    {
                        float pulse = Mathf.Lerp(vignetteMinPulseAlpha, vignetteMaxPulseAlpha, (Mathf.Sin(Time.time * vignettePulseSpeed * Mathf.PI) + 1f) * 0.5f);
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

            damageVignetteImage.color = new Color(finalVignetteColor.r, finalVignetteColor.g, finalVignetteColor.b, finalAlpha);
        }

        private void UpdateSpellSystemUI()
        {
            if (spellCaster == null) return;

            Spell currentSpell = spellCaster.CurrentSpell;

            // Spell Equip Change Check (Polling detection)
            if (currentSpell != lastEquippedSpell)
            {
                OnSpellEquipChanged(currentSpell);
                lastEquippedSpell = currentSpell;
            }

            // Update Cooldown UI
            if (currentSpell != null)
            {
                float cd = spellCaster.CurrentCooldown;
                float maxCd = currentSpell.Stats.Cooldown;

                if (cd > 0f)
                {
                    if (spellCooldownOverlay != null)
                    {
                        spellCooldownOverlay.fillAmount = maxCd > 0f ? Mathf.Clamp01(cd / maxCd) : 0f;
                        spellCooldownOverlay.enabled = true;
                    }

                    if (spellCooldownText != null)
                    {
                        spellCooldownText.text = $"{cd:F1}s";
                        spellCooldownText.enabled = true;
                    }
                }
                else
                {
                    if (spellCooldownOverlay != null && hideCooldownWhenReady)
                    {
                        spellCooldownOverlay.fillAmount = 0f;
                    }
                    if (spellCooldownText != null && hideCooldownWhenReady)
                    {
                        spellCooldownText.text = "";
                    }
                }
            }
            else
            {
                if (spellCooldownOverlay != null) spellCooldownOverlay.fillAmount = 0f;
                if (spellCooldownText != null) spellCooldownText.text = "";
            }

            // Update Charging UI
            if (spellCaster.IsCharging)
            {
                if (chargeContainer != null && !chargeContainer.activeSelf)
                {
                    chargeContainer.SetActive(true);
                }

                float chargeAmt = spellCaster.ChargeAmount;

                if (chargeSlider != null)
                {
                    chargeSlider.value = chargeAmt;
                }
                else if (chargeFillImage != null)
                {
                    chargeFillImage.fillAmount = chargeAmt;
                }

                if (chargeFillImage != null)
                {
                    chargeFillImage.color = Color.Lerp(chargeColorNormal, chargeColorFull, chargeAmt);
                }

                if (chargeText != null)
                {
                    chargeText.text = $"{Mathf.RoundToInt(chargeAmt * 100)}%";
                }
            }
            else
            {
                if (chargeContainer != null && chargeContainer.activeSelf)
                {
                    chargeContainer.SetActive(false);
                }
            }
        }

        private void OnSpellEquipChanged(Spell newSpell)
        {
            if (currentSpellNameText != null)
            {
                currentSpellNameText.text = newSpell != null ? newSpell.Name : "None";
            }

            // Dynamic Icon Update (attempts to load sprite at Resources/SpellIcons/{SpellName})
            if (spellIconImage != null)
            {
                if (newSpell != null)
                {
                    Sprite loadedIcon = Resources.Load<Sprite>($"SpellIcons/{newSpell.Name}");
                    if (loadedIcon != null)
                    {
                        spellIconImage.sprite = loadedIcon;
                        spellIconImage.color = Color.white;
                    }
                    else
                    {
                        spellIconImage.sprite = null;
                        spellIconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
                    }
                }
                else
                {
                    spellIconImage.sprite = null;
                    spellIconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
                }

                // Scale switch pulse animation
                if (iconPulseCoroutine != null)
                {
                    StopCoroutine(iconPulseCoroutine);
                }
                iconPulseCoroutine = StartCoroutine(DoIconEquipPulse());
            }

            // Update Hotbar Slot Highlights
            if (spellSlotHighlights != null && spellCaster != null && spellCaster.AvailableSpells != null)
            {
                Spell[] spells = spellCaster.AvailableSpells;
                for (int i = 0; i < spellSlotHighlights.Length; i++)
                {
                    if (spellSlotHighlights[i] != null)
                    {
                        bool isSelected = false;
                        if (i < spells.Length && spells[i] == newSpell)
                        {
                            isSelected = true;
                        }
                        spellSlotHighlights[i].SetActive(isSelected);
                    }
                }
            }
        }

        private IEnumerator DoIconEquipPulse()
        {
            if (spellIconImage == null) yield break;

            float halfDuration = iconPulseDuration * 0.5f;

            // Scale up
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                spellIconImage.rectTransform.localScale = Vector3.Lerp(originalIconScale, originalIconScale * activeSpellIconScalePulse, t);
                yield return null;
            }

            // Scale down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                spellIconImage.rectTransform.localScale = Vector3.Lerp(originalIconScale * activeSpellIconScalePulse, originalIconScale, t);
                yield return null;
            }

            spellIconImage.rectTransform.localScale = originalIconScale;
            iconPulseCoroutine = null;
        }

        private void UpdateHealthText(float current, float max)
        {
            if (healthText != null)
            {
                healthText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
            }
        }

        private void UpdateManaText(float current, float max)
        {
            if (manaText != null)
            {
                manaText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
            }
        }
    }
}
