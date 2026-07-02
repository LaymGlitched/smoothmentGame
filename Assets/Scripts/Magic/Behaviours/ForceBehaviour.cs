using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class ForceBehaviour : SpellBehaviourBase
    {
        [Header("Force Settings")]
        public float KnockbackForce = 15f;
        public float UpwardForce = 5f;
        public float ForceRadius = 5f;
        public DamageType DamageType = DamageType.Force;

        [Header("Wind Settings")]
        public bool CreateWindEffect = true;
        public float WindDuration = 2f;
        public float WindForce = 10f;
        public float WindRadius = 8f;

        [Header("Visuals")]
        public GameObject WindEffectPrefab;
        public Color WindColor = new Color(0.5f, 0.8f, 1f, 0.5f);

        [Header("Direction")]
        public ForceAffinity.WindBlastDirection BlastDirection = ForceAffinity
            .WindBlastDirection
            .Radial;
        public Vector3 CustomDirection = Vector3.forward;

        private GameObject windEffectInstance;
        private bool hasHit = false;
        private Vector3 projectileDirection;

        public override void OnAttach(GameObject projectile)
        {
            // Cache the projectile's direction
            var spellProjectile = GetProjectile(projectile);
            if (spellProjectile != null)
            {
                projectileDirection = spellProjectile.Direction;
            }
            else
            {
                projectileDirection = projectile.transform.forward;
            }
        }

        public override void OnHit(GameObject projectile, Collision collision)
        {
            // Only trigger once
            if (hasHit)
                return;
            hasHit = true;

            // Get the hit point and normal
            Vector3 hitPoint =
                collision.contacts.Length > 0
                    ? collision.contacts[0].point
                    : projectile.transform.position;

            Vector3 hitNormal =
                collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.up;

            // Get the blast direction
            Vector3 blastDirection = GetBlastDirection(hitPoint, hitNormal);

            // Apply force to the hit object and nearby objects
            ApplyForce(collision.gameObject, hitPoint, hitNormal, blastDirection, projectile);

            // Create wind effect at hit point aligned to the surface
            if (CreateWindEffect)
            {
                CreateWindEffectAt(hitPoint, hitNormal, blastDirection);
            }
        }

        public override void OnDestroy(GameObject projectile)
        {
            // Clean up any lingering effects
            if (windEffectInstance != null)
            {
                Object.Destroy(windEffectInstance, WindDuration);
            }
        }

        private Vector3 GetBlastDirection(Vector3 hitPoint, Vector3 hitNormal)
        {
            switch (BlastDirection)
            {
                case ForceAffinity.WindBlastDirection.Radial:
                    return hitNormal;

                case ForceAffinity.WindBlastDirection.Forward:
                    return projectileDirection;

                case ForceAffinity.WindBlastDirection.Upward:
                    return Vector3.up;

                case ForceAffinity.WindBlastDirection.Downward:
                    return Vector3.down;

                case ForceAffinity.WindBlastDirection.Custom:
                    return CustomDirection.normalized;

                default:
                    return hitNormal;
            }
        }

        private void ApplyForce(
            GameObject target,
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 blastDirection,
            GameObject projectile
        )
        {
            // Apply damage to the target
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(1f, DamageType);
            }

            // Apply physics force to the main target
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 forceDirection = blastDirection;
                // Add some upward force
                forceDirection.y += 0.5f;
                forceDirection.Normalize();

                Vector3 force = forceDirection * KnockbackForce;
                force.y += UpwardForce;

                rb.AddForce(force, ForceMode.Impulse);

                Debug.Log(
                    $"Applied {KnockbackForce} force to {target.name} in direction {forceDirection}"
                );
            }
            else
            {
                // If no rigidbody, try to apply to all rigidbodies in children
                Rigidbody[] childRbs = target.GetComponentsInChildren<Rigidbody>();
                foreach (var childRb in childRbs)
                {
                    Vector3 forceDirection = blastDirection;
                    forceDirection.y += 0.5f;
                    forceDirection.Normalize();

                    Vector3 force = forceDirection * KnockbackForce * 0.5f;
                    force.y += UpwardForce * 0.5f;

                    childRb.AddForce(force, ForceMode.Impulse);
                }
            }

            // Apply force to ALL nearby objects in radius (including player!)
            Collider[] nearbyObjects = Physics.OverlapSphere(hitPoint, ForceRadius);
            foreach (var col in nearbyObjects)
            {
                // Skip the projectile itself
                if (col.gameObject == projectile)
                    continue;

                Rigidbody nearbyRb = col.GetComponent<Rigidbody>();
                if (nearbyRb != null)
                {
                    // Blend between blast direction and direction to object
                    Vector3 toObject = (nearbyRb.transform.position - hitPoint).normalized;
                    float blend = Mathf.Clamp01(
                        Vector3.Distance(nearbyRb.transform.position, hitPoint) / ForceRadius
                    );
                    Vector3 forceDirection = Vector3
                        .Lerp(blastDirection, toObject, blend * 0.5f)
                        .normalized;
                    forceDirection.y += 0.5f;
                    forceDirection.Normalize();

                    float distance = Vector3.Distance(nearbyRb.transform.position, hitPoint);
                    float falloff = 1f - (distance / ForceRadius);

                    Vector3 force = forceDirection * KnockbackForce * falloff * 0.3f;
                    nearbyRb.AddForce(force, ForceMode.Impulse);
                }
            }
        }

        private void CreateWindEffectAt(Vector3 position, Vector3 normal, Vector3 direction)
        {
            // Calculate rotation to align with the surface normal
            Vector3 up = normal;
            Vector3 forward = direction;

            if (Mathf.Abs(Vector3.Dot(normal, direction)) > 0.99f)
            {
                forward = Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(normal, forward)) > 0.99f)
                {
                    forward = Vector3.right;
                }
            }

            Quaternion rotation = Quaternion.LookRotation(forward, up);

            if (WindEffectPrefab != null)
            {
                windEffectInstance = Object.Instantiate(WindEffectPrefab, position, rotation);

                var renderers = windEffectInstance.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = WindColor;
                    }
                }

                windEffectInstance.transform.localScale = Vector3.one * (WindRadius / 5f);
                Object.Destroy(windEffectInstance, WindDuration);

                Debug.Log($"Created wind effect from prefab at {position}");
            }
            else
            {
                CreateParticleWindEffect(position, normal, direction);
                Debug.Log($"Created wind effect particles (no prefab) at {position}");
            }
        }

        private void CreateParticleWindEffect(Vector3 position, Vector3 normal, Vector3 direction)
        {
            GameObject particleObj = new GameObject("WindParticles");
            particleObj.transform.position = position;

            Vector3 up = normal;
            Vector3 forward = direction;
            if (Mathf.Abs(Vector3.Dot(normal, direction)) > 0.99f)
            {
                forward = Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(normal, forward)) > 0.99f)
                {
                    forward = Vector3.right;
                }
            }
            particleObj.transform.rotation = Quaternion.LookRotation(forward, up);

            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 1f;
            main.startSpeed = 5f;
            main.startSize = 0.5f;
            main.maxParticles = 100;
            main.startColor = WindColor;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = WindRadius * 0.3f;
            shape.angle = 15f;
            shape.length = WindRadius;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

            // Force in the blast direction
            ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime;
            force.enabled = true;
            force.x = new ParticleSystem.MinMaxCurve(0f, direction.x * 10f);
            force.y = new ParticleSystem.MinMaxCurve(0f, direction.y * 10f);
            force.z = new ParticleSystem.MinMaxCurve(0f, direction.z * 10f);

            var renderer = ps.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.color = WindColor;
                renderer.material = mat;
            }

            ps.Play();
            Object.Destroy(particleObj, WindDuration);
        }
    }
}
