using GameCode.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace GameCode.PlayerScripts
{
    public class Mana : MonoBehaviour
    {
        [Header("Mana Settings")]
        [SerializeField]
        private float maxMana = 100f;

        [SerializeField]
        private float currentMana;

        [SerializeField]
        private float regenRate = 5f; // per second

        [SerializeField]
        private float regenDelay = 1f; // seconds after use before regen starts

        [Header("Events")]
        public UnityEvent<float, float> OnManaChanged; // current, max
        public UnityEvent<float> OnManaUsed; // amount used
        public UnityEvent<float> OnManaGained; // amount gained
        public UnityEvent OnManaDepleted;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugLogs = true;

        private float lastUseTime = -999f;

        public float MaxMana => maxMana;
        public float CurrentMana => currentMana;
        public float ManaPercentage => currentMana / maxMana;
        public bool IsDepleted => currentMana <= 0;

        private void Start()
        {
            currentMana = maxMana;
            OnManaChanged?.Invoke(currentMana, maxMana);
        }

        private void Update()
        {
            // Regen mana
            if (currentMana < maxMana && Time.time - lastUseTime > regenDelay)
            {
                float regenAmount = regenRate * Time.deltaTime;
                currentMana = Mathf.Min(maxMana, currentMana + regenAmount);
                OnManaChanged?.Invoke(currentMana, maxMana);
            }
        }

        public void TakeMana(float amount)
        {
            float actualAmount = Mathf.Abs(amount);
            float oldMana = currentMana;
            currentMana = Mathf.Max(0f, currentMana - actualAmount);
            float usedAmount = oldMana - currentMana;

            lastUseTime = Time.time;

            if (showDebugLogs && usedAmount > 0)
                Debug.Log($"Used {usedAmount:F1} mana. Mana: {currentMana}/{maxMana}");

            if (usedAmount > 0)
            {
                OnManaUsed?.Invoke(usedAmount);
                OnManaChanged?.Invoke(currentMana, maxMana);

                if (IsDepleted)
                    OnManaDepleted?.Invoke();
            }
        }

        public void GainMana(float amount)
        {
            float actualAmount = Mathf.Abs(amount);
            float oldMana = currentMana;
            currentMana = Mathf.Min(maxMana, currentMana + actualAmount);
            float gainedAmount = currentMana - oldMana;

            if (showDebugLogs && gainedAmount > 0)
                Debug.Log($"Gained {gainedAmount:F1} mana. Mana: {currentMana}/{maxMana}");

            if (gainedAmount > 0)
            {
                OnManaGained?.Invoke(gainedAmount);
                OnManaChanged?.Invoke(currentMana, maxMana);
            }
        }

        public void ResetMana()
        {
            currentMana = maxMana;
            OnManaChanged?.Invoke(currentMana, maxMana);
        }

        public void SetMana(float value)
        {
            currentMana = Mathf.Clamp(value, 0f, maxMana);
            OnManaChanged?.Invoke(currentMana, maxMana);
        }

        public bool HasEnoughMana(float amount)
        {
            return currentMana >= amount;
        }
    }
}
