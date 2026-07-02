using System;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Fully standalone stamina pool. The movement controller only touches
    /// this if enableStamina is turned on there AND a StaminaSystem is
    /// assigned - remove the component or leave the field empty and sprint
    /// becomes unlimited. Also handy for reuse with melee/dodge systems.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        [SerializeField]
        private FPMovementSettings settings;

        public float Current { get; private set; }
        public float Max => settings.maxStamina;
        public bool CanStartSprint => Current > settings.minStaminaToSprint;
        public bool Depleted => Current <= 0f;

        public event Action<float, float> OnStaminaChanged; // current, max

        private float lastUsedTime = -999f;

        private void Awake() => Current = settings.maxStamina;

        /// <summary>Call every physics step while sprinting.</summary>
        public void Drain(float deltaTime)
        {
            Current = Mathf.Max(0f, Current - settings.staminaDrainPerSecond * deltaTime);
            lastUsedTime = Time.time;
            OnStaminaChanged?.Invoke(Current, Max);
        }

        /// <summary>Call every physics step while NOT sprinting.</summary>
        public void Regen(float deltaTime)
        {
            if (Time.time - lastUsedTime < settings.staminaRegenDelay)
                return;
            if (Current >= Max)
                return;

            Current = Mathf.Min(Max, Current + settings.staminaRegenPerSecond * deltaTime);
            OnStaminaChanged?.Invoke(Current, Max);
        }
    }
}
