using System.Collections.Generic;
using FPMovement;
using UnityEngine;

namespace GameCode.Magic
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Magic/Movement Override")]
    public class MovementSpellOverride : ScriptableObject
    {
        [Header("Conditions (All true conditions must be met)")]
        public bool RequiresSliding;
        public bool RequiresSprinting;
        public bool RequiresCrouching;
        public bool RequiresAirborne;
        public bool RequiresGrounded;

        [Header("Overrides")]
        [Tooltip("Leave empty to keep the original shape")]
        public ShapeDefinition OverrideShape;

        [Tooltip("These modifiers are ADDED to the base spell's modifiers")]
        public List<SpellModifierDefinition> AdditionalModifiers = new();

        /// <summary>
        /// Checks if the current RigidbodyFPController state matches all required conditions.
        /// </summary>
        public bool IsConditionMet(RigidbodyFPController controller)
        {
            if (controller == null)
                return false;

            if (RequiresSliding && !controller.IsSliding) return false;
            if (RequiresSprinting && !controller.IsSprinting) return false;
            if (RequiresCrouching && !controller.IsCrouching) return false;
            if (RequiresAirborne && controller.IsGrounded) return false;
            if (RequiresGrounded && !controller.IsGrounded) return false;

            return true;
        }
    }
}
