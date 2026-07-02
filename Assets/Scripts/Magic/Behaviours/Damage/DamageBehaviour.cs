using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class DamageBehaviour : SpellBehaviourBase
    {
        public DamageType DamageType { get; set; }
        public float KnockbackForce { get; set; }
        public float BurnDamage { get; set; }
        public float BurnDuration { get; set; }

        public override void OnHit(GameObject projectileObject, Collision collision)
        {
            var projectile = GetProjectile(projectileObject);
            if (projectile == null)
                return;

            // Apply damage using IDamageable interface from Shared assembly
            var damageable = collision.gameObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(projectile.Damage, DamageType);
            }

            // Apply knockback
            var rb = collision.gameObject.GetComponent<Rigidbody>();
            if (rb != null && KnockbackForce > 0)
            {
                Vector3 knockbackDirection = collision.contacts[0].normal;
                rb.AddForce(knockbackDirection * KnockbackForce, ForceMode.Impulse);
            }

            // Apply burn
            if (BurnDamage > 0 && BurnDuration > 0)
            {
                var burnable = collision.gameObject.GetComponent<IBurnable>();
                if (burnable != null)
                {
                    burnable.ApplyBurn(BurnDamage, BurnDuration);
                }
            }
        }
    }
}
