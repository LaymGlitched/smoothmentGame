using FPMovement;
using GameCode.Magic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCode.Magic
{
    public class SpellEquipper : MonoBehaviour
    {
        [Header("Spells")]
        public Spell[] AvailableSpells;
        private int currentSpellIndex = 0;

        [Header("Input Actions")]
        public InputActionReference[] SpellKeys; // Array of input actions for each spell slot
        public InputActionReference NextSpell;
        public InputActionReference PreviousSpell;

        [Header("UI")]
        [SerializeField]
        private GameObject spellWheelUI; // Optional spell wheel

        private SpellCaster spellCaster;
        private RigidbodyFPController controller;

        private void Start()
        {
            spellCaster = GetComponent<SpellCaster>();
            if (spellCaster == null)
            {
                Debug.LogError("SpellEquipper requires a SpellCaster component!");
                return;
            }

            controller = GetComponent<RigidbodyFPController>();

            if (AvailableSpells.Length > 0)
            {
                spellCaster.EquipSpell(AvailableSpells[0]);
            }
        }

        private void OnEnable()
        {
            // Subscribe to spell key events
            for (int i = 0; i < SpellKeys.Length && i < AvailableSpells.Length; i++)
            {
                if (SpellKeys[i] != null)
                {
                    int index = i; // Capture for closure
                    SpellKeys[i].action.performed += ctx => EquipSpellAtIndex(index);
                }
            }

            // Subscribe to next/previous events
            if (NextSpell != null)
                NextSpell.action.performed += ctx => CycleSpell(1);

            if (PreviousSpell != null)
                PreviousSpell.action.performed += ctx => CycleSpell(-1);
        }

        private void OnDisable()
        {
            // Unsubscribe from spell key events
            for (int i = 0; i < SpellKeys.Length && i < AvailableSpells.Length; i++)
            {
                if (SpellKeys[i] != null)
                {
                    int index = i;
                    SpellKeys[i].action.performed -= ctx => EquipSpellAtIndex(index);
                }
            }

            if (NextSpell != null)
                NextSpell.action.performed -= ctx => CycleSpell(1);

            if (PreviousSpell != null)
                PreviousSpell.action.performed -= ctx => CycleSpell(-1);
        }

        private void EquipSpellAtIndex(int index)
        {
            // Don't switch spells if dead
            if (controller != null && !controller.enabled)
                return;

            if (index >= 0 && index < AvailableSpells.Length)
            {
                currentSpellIndex = index;
                spellCaster.EquipSpell(AvailableSpells[index]);
                Debug.Log($"Equipped: {AvailableSpells[index].Name}");
            }
        }

        private void CycleSpell(int direction)
        {
            // Don't switch spells if dead
            if (controller != null && !controller.enabled)
                return;

            if (AvailableSpells.Length == 0)
                return;

            currentSpellIndex =
                (currentSpellIndex + direction + AvailableSpells.Length) % AvailableSpells.Length;
            spellCaster.EquipSpell(AvailableSpells[currentSpellIndex]);
            Debug.Log($"Equipped: {AvailableSpells[currentSpellIndex].Name}");
        }

        public void ToggleSpellWheel(bool show)
        {
            if (spellWheelUI != null)
                spellWheelUI.SetActive(show);
        }
    }
}
