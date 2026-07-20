using GameCode.Shared;
using Nanodogs.API.Explosion;
using UnityEngine;

namespace GameCode.Magic
{
    public class ExplosiveBehaviour : SpellBehaviourBase
    {
        public float Radius = 5f;
        public float DamageMultiplier = 1.5f;
        public float KnockbackMultiplier = 1.0f;
        public ExplosionSettings Settings;

        public override void OnDestroy(GameObject projectileObject)
        {

            var projectile = GetProjectile(projectileObject);
            if (projectile == null)
                return;

            Vector3 explosionPosition = projectile.transform.position;

            // Use custom explosion with immediate damage
            CustomExplosion.CreateExplosion(
                explosionPosition,
                Settings,
                projectile.Damage,
                DamageMultiplier,
                Radius,
                projectile.Caster
            );
        }

        private void DealExplosionDamage(SpellProjectile projectile, Vector3 explosionPosition)
        {
            // Find all targets in radius
            Collider[] colliders = Physics.OverlapSphere(explosionPosition, Radius);

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;

                // Skip the caster to prevent self-damage
                if (collider.gameObject == projectile.Caster)
                    continue;

                // Calculate distance and falloff
                float distance = Vector3.Distance(explosionPosition, collider.transform.position);
                float falloff = Mathf.Clamp01(1f - (distance / Radius));

                // Apply damage using IDamageable interface
                var damageable = collider.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float damage = projectile.Damage * DamageMultiplier * falloff;
                    damageable.TakeDamage(damage, DamageType.Physical);
                    Debug.Log($"Explosion dealt {damage:F1} damage to {collider.gameObject.name}");
                }

                // Apply additional knockback (the ExplosionAPI already applies force, but this is extra)
                var rb = collider.GetComponent<Rigidbody>();
                if (rb != null && Settings != null)
                {
                    // The ExplosionAPI already handles this with AddExplosionForce
                    // We'll let the API handle it to avoid double knockback
                    // But if you want extra knockback, uncomment below:
                    /*
                    Vector3 direction = (collider.transform.position - explosionPosition).normalized;
                    float force = Settings.force * KnockbackMultiplier * falloff * 0.5f;
                    rb.AddForce(direction * force, ForceMode.Impulse);
                    */
                }
            }
        }
    }
}
