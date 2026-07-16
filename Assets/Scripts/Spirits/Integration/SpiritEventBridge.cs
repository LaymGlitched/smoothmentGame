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

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged.AddListener(HandlePlayerDamaged);
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
