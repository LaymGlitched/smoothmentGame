using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    /// <summary>
    /// Creates a wind blast that pushes enemies AND the player.
    /// Only triggers on hit.
    /// </summary>
    public class WindBlastBehaviour : SpellBehaviourBase
    {
        [Header("Wind Blast Settings")]
        public float BlastForce = 20f;
        public float UpwardForce = 5f;
        public float BlastRadius = 10f;

        [Header("Visuals")]
        public GameObject WindEffectPrefab;
        public Color WindColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        public float EffectDuration = 2f;

        [Header("Direction")]
        public ForceAffinity.WindBlastDirection BlastDirection = ForceAffinity
            .WindBlastDirection
            .Radial;
        public Vector3 CustomDirection = Vector3.forward;

        private bool hasTriggered = false;
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
            if (hasTriggered)
                return;

            hasTriggered = true;

            // Get the hit point and normal
            Vector3 hitPoint =
                collision.contacts.Length > 0
                    ? collision.contacts[0].point
                    : projectile.transform.position;

            Vector3 hitNormal =
                collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.up;

            // Get the blast direction based on the selected mode
            Vector3 blastDirection = GetBlastDirection(hitPoint, hitNormal);

            // Apply force to ALL nearby objects (including player!)
            ApplyWindForce(hitPoint, blastDirection, BlastForce);

            // Create visual effect at hit point aligned to the surface
            CreateWindBlast(hitPoint, hitNormal, blastDirection);

            Debug.Log($"Wind blast triggered at {hitPoint} with direction {blastDirection}");
        }

        public override void OnDestroy(GameObject projectile)
        {
            if (!hasTriggered)
            {
                Debug.Log("Wind blast expired without hitting anything");
            }
        }

        private Vector3 GetBlastDirection(Vector3 hitPoint, Vector3 hitNormal)
        {
            switch (BlastDirection)
            {
                case ForceAffinity.WindBlastDirection.Radial:
                    // Radial uses the hit normal (pushes outward from surface)
                    return hitNormal;

                case ForceAffinity.WindBlastDirection.Forward:
                    // Forward uses the projectile's travel direction
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

        private void CreateWindBlast(Vector3 position, Vector3 normal, Vector3 direction)
        {
            // Calculate rotation to align with the surface normal
            Vector3 up = normal;
            Vector3 forward = direction;

            // If the normal is parallel to the direction, use a different up vector
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
                GameObject effect = Object.Instantiate(WindEffectPrefab, position, rotation);

                var renderers = effect.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = WindColor;
                    }
                }

                effect.transform.localScale = Vector3.one * (BlastRadius / 5f);
                Object.Destroy(effect, EffectDuration);

                Debug.Log($"Created wind blast effect from prefab at {position}");
            }
            else
            {
                CreateParticleWindBlast(position, normal, direction);
                CreateRingEffect(position, normal, direction);
                Debug.Log($"Created wind blast particles (no prefab) at {position}");
            }
        }

        private void CreateParticleWindBlast(Vector3 position, Vector3 normal, Vector3 direction)
        {
            GameObject particleObj = new GameObject("WindBlastParticles");
            particleObj.transform.position = position;

            // Align to surface normal
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
            main.startLifetime = 1.5f;
            main.startSpeed = 10f;
            main.startSize = 2f;
            main.maxParticles = 200;
            main.startColor = WindColor;

            // Sphere shape for radial blast
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 100) });

            // Force in the blast direction
            ParticleSystem.ForceOverLifetimeModule forceModule = ps.forceOverLifetime;
            forceModule.enabled = true;
            forceModule.x = new ParticleSystem.MinMaxCurve(0f, direction.x * 20f);
            forceModule.y = new ParticleSystem.MinMaxCurve(0f, direction.y * 20f);
            forceModule.z = new ParticleSystem.MinMaxCurve(0f, direction.z * 20f);

            var renderer = ps.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.color = WindColor;
                renderer.material = mat;
            }

            ps.Play();
            Object.Destroy(particleObj, EffectDuration);
        }

        private void CreateRingEffect(Vector3 position, Vector3 normal, Vector3 direction)
        {
            GameObject ringObj = new GameObject("WindRing");
            ringObj.transform.position = position;

            // Align the ring to the surface
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
            ringObj.transform.rotation = Quaternion.LookRotation(forward, up);

            ParticleSystem ps = ringObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.maxParticles = 50;
            main.startColor = WindColor;

            // Circle shape on the surface
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = BlastRadius;
            shape.radiusThickness = 0.1f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

            // Force in the blast direction for ring particles
            ParticleSystem.ForceOverLifetimeModule forceModule = ps.forceOverLifetime;
            forceModule.enabled = true;
            forceModule.x = new ParticleSystem.MinMaxCurve(0f, direction.x * 5f);
            forceModule.y = new ParticleSystem.MinMaxCurve(0f, direction.y * 5f);
            forceModule.z = new ParticleSystem.MinMaxCurve(0f, direction.z * 5f);

            var renderer = ps.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.color = WindColor;
                renderer.material = mat;
            }

            ps.Play();
            Object.Destroy(ringObj, EffectDuration);
        }

        private void ApplyWindForce(Vector3 center, Vector3 direction, float force)
        {
            // Find ALL objects in radius (including the player!)
            Collider[] objects = Physics.OverlapSphere(center, BlastRadius);

            foreach (var obj in objects)
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Calculate the force direction
                    Vector3 forceDirection = direction;

                    // Add some spread based on position
                    Vector3 toObject = (obj.transform.position - center).normalized;

                    // Blend between the blast direction and the direction to the object
                    // This creates a more natural feel - objects in the center go straight,
                    // objects at the edges get pushed outward
                    float blend = Mathf.Clamp01(
                        Vector3.Distance(obj.transform.position, center) / BlastRadius
                    );
                    forceDirection = Vector3.Lerp(direction, toObject, blend * 0.5f).normalized;

                    float distance = Vector3.Distance(obj.transform.position, center);
                    float falloff = 1f - (distance / BlastRadius);

                    Vector3 blastForce = forceDirection * force * falloff * 1.5f;
                    blastForce.y += UpwardForce * falloff;

                    rb.AddForce(blastForce, ForceMode.Impulse);

                    Debug.Log(
                        $"Applied wind force to {obj.name}: {blastForce.magnitude} in direction {forceDirection}"
                    );
                }

                IDamageable damageable = obj.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float damage = force * 0.1f;
                    damageable.TakeDamage(damage, DamageType.Force);
                }
            }
        }
    }
}
