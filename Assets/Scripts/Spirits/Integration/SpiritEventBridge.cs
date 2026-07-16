using GameCode.Magic;
using GameCode.PlayerScripts;
using GameCode.Shared;
using GameCode.Spirits.Core;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Integration
{
    /// <summary>
    /// Translates gameplay events into neutral Spirit events.
    /// This is the ONLY class in the Spirit System allowed to reference 
    /// GameCode.PlayerScripts or GameCode.Magic.
    /// Maintains strict one-way dependency: Gameplay -> Bridge -> SpiritManager.
    /// </summary>
    public class SpiritEventBridge : MonoBehaviour
    {
        [Header("Gameplay References")]
        [Tooltip("Reference to the player's Health component.")]
        [SerializeField] private Health playerHealth;

        [Tooltip("Reference to the player's SpellCaster component.")]
        [SerializeField] private SpellCaster spellCaster;

        [Tooltip("Reference to the player's Mana component.")]
        [SerializeField] private Mana playerMana;

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged.AddListener(HandlePlayerDamaged);
            }

            if (playerMana != null)
            {
                playerMana.OnManaChanged.AddListener(HandlePlayerManaChange);
                playerMana.OnManaUsed.AddListener(HandlePlayerManaUsed);
                playerMana.OnManaDepleted.AddListener(HandlePlayerManaDepletion);
            }

            if (spellCaster != null)
            {
                spellCaster.OnSpellEquippedEvent += HandleSpellEquipped;
                spellCaster.OnSpellCastedEvent += HandleSpellCasted;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged.RemoveListener(HandlePlayerDamaged);
            }

            if (spellCaster != null)
            {
                spellCaster.OnSpellEquippedEvent -= HandleSpellEquipped;
                spellCaster.OnSpellCastedEvent -= HandleSpellCasted;
            }
        }

        // ----------------------------------------------------------------------
        // Gameplay Event Handlers
        // ----------------------------------------------------------------------

        private void HandlePlayerDamaged(float amount, DamageType type)
        {
            var eventData = new DamageEventData(amount, type);
            SpiritManager.Instance?.BroadcastEvent(eventData);
        }

        private void HandlePlayerManaChange(float current, float max)
        {
            var eventData = new ManaChangeEventData(current, max);
            SpiritManager.Instance?.BroadcastEvent(eventData);
        }

        private void HandlePlayerManaUsed(float amount)
        {
            var eventData = new ManaUsedEventData(amount);
            SpiritManager.Instance?.BroadcastEvent(eventData);
        }

        private void HandlePlayerManaDepletion()
        {
            var eventData = new ManaDepletedEventData();
            SpiritManager.Instance?.BroadcastEvent(eventData);
        }

        private void HandleSpellEquipped(Spell spell)
        {
            if (spell == null) return;
            
            var eventData = new SpellEquippedEventData(spell.Name);
            SpiritManager.Instance?.BroadcastEvent(eventData);
        }

        private void HandleSpellCasted(Spell spell, bool isCharged)
        {
            if (spell == null) return;

            var eventData = new SpellCastEventData(spell.Name, isCharged);
            SpiritManager.Instance?.BroadcastEvent(eventData);
        }
    }
}
