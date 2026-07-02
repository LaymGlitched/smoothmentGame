using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    /// <summary>
    /// Forces the caster forward in the direction they're looking.
    /// This is designed for self-cast dash spells.
    /// </summary>
    public class ForceDashBehaviour : SpellBehaviourBase
    {
        [Header("Dash Settings")]
        public float DashForce = 30f;
        public float UpwardForce = 2f;
        public float DashDuration = 0.3f;

        [Header("Visuals")]
        public GameObject DashEffectPrefab;
        public Color DashColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        public float EffectDuration = 1f;

        [Header("Dash Direction Mode")]
        public ForceAffinity.WindBlastDirection DirectionMode = ForceAffinity
            .WindBlastDirection
            .Forward;
        public Vector3 CustomDirection = Vector3.forward;

        private bool hasTriggered = false;
        private GameObject cachedCaster;
        private Rigidbody cachedRigidbody;
        private Vector3 dashDirection;

        public override void OnAttach(GameObject projectile)
        {
            // Get the caster and their rigidbody
            var spellProjectile = GetProjectile(projectile);
            if (spellProjectile != null)
            {
                cachedCaster = spellProjectile.Caster;
                if (cachedCaster != null)
                {
                    cachedRigidbody = cachedCaster.GetComponent<Rigidbody>();

                    // Calculate the dash direction based on the caster's orientation
                    dashDirection = GetDashDirection();

                    // Apply the dash force immediately
                    ApplyDashForce();

                    // Create visual effect at the caster's position
                    CreateDashEffect();

                    hasTriggered = true;

                    Debug.Log(
                        $"Force Dash triggered! Direction: {dashDirection}, Force: {DashForce}"
                    );
                }
            }
        }

        public override void OnHit(GameObject projectile, Collision collision)
        {
            // Do nothing on hit - dash already applied on attach
            // This prevents double-triggering
        }

        private Vector3 GetDashDirection()
        {
            if (cachedCaster == null)
                return Vector3.forward;

            switch (DirectionMode)
            {
                case ForceAffinity.WindBlastDirection.Forward:
                    // Use the camera/caster's forward direction
                    return cachedCaster.transform.forward;

                case ForceAffinity.WindBlastDirection.Upward:
                    return Vector3.up;

                case ForceAffinity.WindBlastDirection.Downward:
                    return Vector3.down;

                case ForceAffinity.WindBlastDirection.Radial:
                    // For dash, radial means the caster's forward direction
                    return cachedCaster.transform.forward;

                case ForceAffinity.WindBlastDirection.Custom:
                    return CustomDirection.normalized;

                default:
                    return cachedCaster.transform.forward;
            }
        }

        private void ApplyDashForce()
        {
            if (cachedRigidbody == null)
            {
                Debug.LogWarning("ForceDashBehaviour: No Rigidbody found on caster!");
                return;
            }

            // Calculate the force
            Vector3 force = dashDirection * DashForce;
            force.y += UpwardForce;

            // Apply the force as an impulse
            cachedRigidbody.AddForce(force, ForceMode.Impulse);

            // Optional: Add a small upward boost for style
            if (DashForce > 0 && cachedRigidbody.linearVelocity.y < 0)
            {
                cachedRigidbody.linearVelocity = new Vector3(
                    cachedRigidbody.linearVelocity.x,
                    Mathf.Abs(cachedRigidbody.linearVelocity.y) * 0.5f,
                    cachedRigidbody.linearVelocity.z
                );
            }

            Debug.Log($"Applied dash force: {force} (Direction: {dashDirection})");
        }

        private void CreateDashEffect()
        {
            if (cachedCaster == null)
                return;

            Vector3 effectPosition = cachedCaster.transform.position + Vector3.down * 0.5f;
            Vector3 effectDirection = dashDirection;

            if (DashEffectPrefab != null)
            {
                // Use the provided prefab
                GameObject effect = Object.Instantiate(
                    DashEffectPrefab,
                    effectPosition,
                    Quaternion.LookRotation(effectDirection)
                );

                // Set color
                var renderers = effect.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = DashColor;
                    }
                }

                Object.Destroy(effect, EffectDuration);
                Debug.Log($"Created dash effect from prefab at {effectPosition}");
            }
            else
            {
                // Create particle effect
                CreateParticleDashEffect(effectPosition, effectDirection);
                Debug.Log($"Created dash particle effect at {effectPosition}");
            }

            // Create a ring effect at the starting position
            CreateRingEffect(effectPosition, effectDirection);
        }

        private void CreateParticleDashEffect(Vector3 position, Vector3 direction)
        {
            GameObject particleObj = new GameObject("DashEffect");
            particleObj.transform.position = position;
            particleObj.transform.rotation = Quaternion.LookRotation(direction);

            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 15f;
            main.startSize = 1f;
            main.maxParticles = 50;
            main.startColor = DashColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Cone shape pointing in the dash direction (backwards for a trail effect)
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 1f;
            shape.angle = 30f;
            shape.length = 2f;
            shape.position = new Vector3(0, 0, -2f); // Behind the caster

            // Emission burst
            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

            // Force in the dash direction
            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.x = new ParticleSystem.MinMaxCurve(0f, direction.x * -20f);
            force.y = new ParticleSystem.MinMaxCurve(0f, direction.y * -20f);
            force.z = new ParticleSystem.MinMaxCurve(0f, direction.z * -20f);

            var renderer = ps.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.color = DashColor;
                renderer.material = mat;
            }

            ps.Play();
            Object.Destroy(particleObj, EffectDuration);
        }

        private void CreateRingEffect(Vector3 position, Vector3 direction)
        {
            GameObject ringObj = new GameObject("DashRing");
            ringObj.transform.position = position;
            ringObj.transform.rotation = Quaternion.LookRotation(direction);

            ParticleSystem ps = ringObj.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 2f;
            main.maxParticles = 30;
            main.startColor = DashColor;

            // Circle ring
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;
            shape.radiusThickness = 0.1f;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

            var renderer = ps.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.color = DashColor;
                renderer.material = mat;
            }

            ps.Play();
            Object.Destroy(ringObj, EffectDuration);
        }
    }
}
