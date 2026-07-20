using GameCode.Shared;
using Nanodogs.API.Explosion;
using UnityEngine;

namespace GameCode.Magic
{
    public static class CustomExplosion
    {
        public static void CreateExplosion(
            Vector3 position,
            ExplosionSettings settings,
            float damage,
            float damageMultiplier,
            float radius,
            GameObject caster = null
        )
        {
            // 1. Apply damage immediately
            ApplyDamage(position, radius, damage, damageMultiplier, caster);

            // 2. Apply forces using the API (this handles rigidbodies)
            if (settings != null)
            {
                // Use the existing API for effects and forces
                ExplosionAPI.Explosion(position, settings);
            }
        }

        private static void ApplyDamage(
            Vector3 position,
            float radius,
            float baseDamage,
            float damageMultiplier,
            GameObject caster
        )
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;

                if (caster != null && collider.transform.IsChildOf(caster.transform))
                    continue;

                float distance = Vector3.Distance(position, collider.transform.position);
                float falloff = Mathf.Clamp01(1f - (distance / radius));
                float damage = baseDamage * damageMultiplier * falloff;

                var damageable = collider.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage, DamageType.Physical);
                }
            }
        }
    }
}
