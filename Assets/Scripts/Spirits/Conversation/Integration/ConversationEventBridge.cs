using GameCode.PlayerScripts;
using GameCode.Spirits.Conversation.Runtime;
using GameCode.Spirits.Core;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Integration
{
    /// <summary>
    /// Translates gameplay events into ConversationTrigger signals for the ConversationDirector.
    /// 
    /// This follows the exact same pattern as SpiritEventBridge: it subscribes to gameplay
    /// systems and forwards neutral signals into the Spirit domain. The only difference is
    /// the output — ConversationTrigger strings instead of SpiritEventData objects.
    /// 
    /// This is the ONLY class in the Conversation System allowed to reference
    /// GameCode.PlayerScripts, GameCode.Magic, or other gameplay assemblies.
    /// </summary>
    public class ConversationEventBridge : MonoBehaviour
    {
        [Header("Gameplay References")]
        [Tooltip("Reference to the player's Health component.")]
        [SerializeField] private Health playerHealth;

        [Tooltip("Reference to the player's Mana component.")]
        [SerializeField] private Mana playerMana;

        [Header("Idle Detection")]
        [Tooltip("Seconds of no input before the PlayerIdle trigger fires.")]
        [SerializeField] private float idleThreshold = 30f;

        [Header("Ambient Chatter")]
        [Tooltip("Minimum seconds between ambient chatter triggers.")]
        [SerializeField] private float ambientMinInterval = 45f;

        [Tooltip("Maximum seconds between ambient chatter triggers.")]
        [SerializeField] private float ambientMaxInterval = 90f;

        // Idle tracking
        private float lastInputTime;

        // Ambient timer
        private float nextAmbientTime;

        // HP threshold tracking (prevent re-firing the same threshold)
        private bool hasFiredHPLow;
        private bool hasFiredHPCritical;

        // Death tracking
        private int deathCountInArea;

        private void OnEnable()
        {
            lastInputTime = Time.time;
            ScheduleNextAmbient();

            if (playerHealth != null)
            {
                playerHealth.OnDamaged.AddListener(HandlePlayerDamaged);
                playerHealth.OnHealed.AddListener(HandlePlayerHealed);
            }

            if (playerMana != null)
            {
                playerMana.OnManaDepleted.AddListener(HandleManaDepleted);
            }

            // Subscribe to Spirit presence changes for swap detection
            if (SpiritManager.Instance != null)
            {
                SpiritManager.Instance.OnSpiritPresenceChanged += HandleSpiritPresenceChanged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged.RemoveListener(HandlePlayerDamaged);
                playerHealth.OnHealed.RemoveListener(HandlePlayerHealed);
            }

            if (playerMana != null)
            {
                playerMana.OnManaDepleted.RemoveListener(HandleManaDepleted);
            }

            if (SpiritManager.Instance != null)
            {
                SpiritManager.Instance.OnSpiritPresenceChanged -= HandleSpiritPresenceChanged;
            }
        }

        private void Update()
        {
            // ── Input Detection (for idle tracking) ──
            if (Input.anyKey || Input.GetAxis("Mouse X") != 0f || Input.GetAxis("Mouse Y") != 0f)
            {
                lastInputTime = Time.time;
            }

            // ── Idle Detection ──
            float idleTime = Time.time - lastInputTime;
            if (idleTime >= idleThreshold)
            {
                FireTrigger("PlayerIdle");
                // Reset so it doesn't spam
                lastInputTime = Time.time;
            }

            // ── Ambient Chatter Timer ──
            if (Time.time >= nextAmbientTime)
            {
                FireTrigger("AmbientChatter");
                ScheduleNextAmbient();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Gameplay Event Handlers
        // ──────────────────────────────────────────────────────────────

        private void HandlePlayerDamaged(float amount, GameCode.Shared.DamageType type)
        {
            if (playerHealth == null) return;

            float hpPercent = playerHealth.HealthPercentage;

            if (hpPercent < 0.2f && !hasFiredHPCritical)
            {
                hasFiredHPCritical = true;
                FireTrigger("PlayerHPCritical");

                // Also set context tags
                ConversationDirector.Instance?.AddContextTag("danger");
                ConversationDirector.Instance?.AddContextTag("health");
            }
            else if (hpPercent < 0.4f && !hasFiredHPLow)
            {
                hasFiredHPLow = true;
                FireTrigger("PlayerHPLow");

                ConversationDirector.Instance?.AddContextTag("health");
            }
        }

        private void HandlePlayerHealed(float amount)
        {
            if (playerHealth == null) return;

            float hpPercent = playerHealth.HealthPercentage;

            // Reset threshold flags when healed above thresholds
            if (hpPercent >= 0.4f)
            {
                hasFiredHPLow = false;
                ConversationDirector.Instance?.RemoveContextTag("health");
            }
            if (hpPercent >= 0.2f)
            {
                hasFiredHPCritical = false;
                ConversationDirector.Instance?.RemoveContextTag("danger");
            }
        }

        private void HandleManaDepleted()
        {
            FireTrigger("ManaDepleted");
        }

        private void HandleSpiritPresenceChanged(Spirit spirit, PresenceMode oldMode, PresenceMode newMode)
        {
            if (newMode == PresenceMode.Foregrounded)
            {
                FireTrigger("SpiritSwapped");
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Public API (for other gameplay systems to call)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires a conversation trigger from an external gameplay system.
        /// Call this from boss spawners, puzzle completion handlers, etc.
        /// </summary>
        public void FireTrigger(string triggerValue)
        {
            ConversationDirector.Instance?.OnTrigger(triggerValue);
        }

        /// <summary>
        /// Notifies the bridge that the player has died. Call from PlayerDeathHandler.
        /// </summary>
        public void NotifyPlayerDeath()
        {
            deathCountInArea++;
            FireTrigger("PlayerDied");

            if (deathCountInArea >= 3)
            {
                FireTrigger("RepeatedFailure");
            }
        }

        /// <summary>
        /// Notifies the bridge that the player has killed an enemy.
        /// </summary>
        public void NotifyEnemyKilled()
        {
            FireTrigger("EnemyKilled");
        }

        /// <summary>
        /// Notifies the bridge that a boss has appeared.
        /// </summary>
        public void NotifyBossAppeared()
        {
            FireTrigger("BossAppears");
            ConversationDirector.Instance?.AddContextTag("boss");
            ConversationDirector.Instance?.AddContextTag("combat");
        }

        /// <summary>
        /// Notifies the bridge that the player entered a new area.
        /// Resets area-specific counters.
        /// </summary>
        public void NotifyAreaEntered()
        {
            deathCountInArea = 0;
            FireTrigger("PlayerExplores");
        }

        /// <summary>
        /// Notifies the bridge that the player discovered a secret.
        /// </summary>
        public void NotifySecretFound()
        {
            FireTrigger("SecretFound");
        }

        /// <summary>
        /// Notifies the bridge that a puzzle was solved.
        /// </summary>
        public void NotifyPuzzleSolved()
        {
            FireTrigger("PuzzleSolved");
        }

        /// <summary>
        /// Notifies the bridge that a story flag has changed.
        /// </summary>
        public void NotifyStoryProgression()
        {
            FireTrigger("StoryProgression");
        }

        /// <summary>
        /// Adds a custom context tag to the ConversationDirector.
        /// </summary>
        public void AddContextTag(string tag)
        {
            ConversationDirector.Instance?.AddContextTag(tag);
        }

        /// <summary>
        /// Removes a custom context tag from the ConversationDirector.
        /// </summary>
        public void RemoveContextTag(string tag)
        {
            ConversationDirector.Instance?.RemoveContextTag(tag);
        }

        // ──────────────────────────────────────────────────────────────
        // Internal
        // ──────────────────────────────────────────────────────────────

        private void ScheduleNextAmbient()
        {
            nextAmbientTime = Time.time + Random.Range(ambientMinInterval, ambientMaxInterval);
        }
    }
}
