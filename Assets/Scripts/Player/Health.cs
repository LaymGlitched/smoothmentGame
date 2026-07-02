using GameCode.Shared;
using Nanodogs.API.Nanoshake;
using UnityEngine;
using UnityEngine.Events;

namespace GameCode.PlayerScripts
{
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField]
        private float maxHealth = 100f;

        [SerializeField]
        private float currentHealth;

        [Header("Events")]
        public UnityEvent OnDie;
        public UnityEvent<float, float> OnHealthChanged; // current, max
        public UnityEvent<float, DamageType> OnDamaged; // damage, type
        public UnityEvent<float> OnHealed; // amount

        [Header("Debug")]
        [SerializeField]
        private bool showDebugLogs = true;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthPercentage => currentHealth / maxHealth;
        public bool IsDead => currentHealth <= 0;

        private void Start()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(float damage, DamageType damageType)
        {
            if (IsDead)
                return;

            float actualDamage = Mathf.Abs(damage);
            currentHealth = Mathf.Max(0f, currentHealth - actualDamage);

            // Trigger effects
            Nanoshake.Shake(false, null, 0.5f, 0.5f, 2f);

            if (showDebugLogs)
                Debug.Log(
                    $"Took {actualDamage} {damageType} damage. Health: {currentHealth}/{maxHealth}"
                );

            OnDamaged?.Invoke(actualDamage, damageType);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (IsDead)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead)
                return;

            float actualHeal = Mathf.Abs(amount);
            float oldHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + actualHeal);
            float healedAmount = currentHealth - oldHealth;

            if (showDebugLogs && healedAmount > 0)
                Debug.Log($"Healed {healedAmount}. Health: {currentHealth}/{maxHealth}");

            if (healedAmount > 0)
            {
                OnHealed?.Invoke(healedAmount);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }

        private void Die()
        {
            if (showDebugLogs)
                Debug.Log("Player died!");

            OnDie?.Invoke();
        }

        /// <summary>
        /// Resets health to full. Useful for respawning.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Sets health to a specific value.
        /// </summary>
        public void SetHealth(float value)
        {
            currentHealth = Mathf.Clamp(value, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
