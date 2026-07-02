using System.Collections.Generic;
using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class SpellProjectile : MonoBehaviour
    {
        // Core spell data
        public Spell Spell { get; private set; }
        public GameObject Caster { get; private set; }

        // Public stats
        public float Speed { get; set; }
        public float Damage { get; set; }
        public Vector3 Direction { get; set; }

        // Runtime state
        public float Lifetime { get; private set; }
        public bool IsExpired { get; private set; }

        // Modular behaviour system
        public List<ISpellBehaviour> Behaviours = new();

        // References
        private Rigidbody rb;
        private float lifeTimer;
        private bool hasExploded = false;
        private bool shouldDestroy = true; // Track if we should destroy after hit

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void Start()
        {
            foreach (var behaviour in Behaviours)
            {
                behaviour.OnAttach(gameObject);
            }

            // CRITICAL FIX: Apply the direction immediately if not already moving
            if (rb != null && Direction != Vector3.zero)
            {
                // Rotate the projectile to face the direction
                if (Direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(Direction);
                }

                // Apply velocity
                rb.linearVelocity = Direction * Speed;
            }
        }

        void Update()
        {
            if (IsExpired)
                return;

            lifeTimer += Time.deltaTime;
            if (lifeTimer >= Lifetime)
            {
                Expire();
                return;
            }

            foreach (var behaviour in Behaviours)
            {
                behaviour.OnUpdate(gameObject);
            }
        }

        void FixedUpdate()
        {
            // CRITICAL FIX: Keep the projectile moving in the correct direction
            if (rb != null && !IsExpired && !hasExploded)
            {
                // Ensure the projectile maintains its speed and direction
                // This prevents any physics interference from slowing it down
                rb.linearVelocity = Direction * Speed;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsExpired || hasExploded)
                return;

            // Reset destroy flag
            shouldDestroy = true;

            // Notify behaviours of hit FIRST
            foreach (var behaviour in Behaviours)
            {
                behaviour.OnHit(gameObject, collision);
            }

            foreach (var behaviour in Behaviours)
            {
                behaviour.OnDestroy(gameObject);
            }

            // Check if any behaviour prevented destruction (like Ricochet)
            // If the projectile is still moving and not destroyed, skip destruction
            if (!shouldDestroy)
                return;

            // If we get here, no behaviour prevented destruction, so destroy
            hasExploded = true;
            StartCoroutine(SmoothDestroyRoutine());
        }

        public void PreventDestruction()
        {
            shouldDestroy = false;
        }

        System.Collections.IEnumerator SmoothDestroyRoutine()
        {
            // Disable colliders and physics
            if (TryGetComponent<Collider>(out var col))
                col.enabled = false;
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            // Stop particle emission
            ParticleSystem[] childParticles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in childParticles)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // Shrink the projectile
            float duration = 0.1f;
            float elapsed = 0f;
            Vector3 originalScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, percent);
                yield return null;
            }

            transform.localScale = Vector3.zero;

            // Wait for particles to finish
            bool particlesStillAlive = true;
            while (particlesStillAlive)
            {
                particlesStillAlive = false;
                foreach (var ps in childParticles)
                {
                    if (ps != null && ps.IsAlive(true))
                    {
                        particlesStillAlive = true;
                        break;
                    }
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (IsExpired)
                return;

            IsExpired = true;

            if (!hasExploded)
            {
                foreach (var behaviour in Behaviours)
                {
                    behaviour.OnDestroy(gameObject);
                }
            }
        }

        public void Initialize(SpellContext context)
        {
            Spell = context.Spell;
            Caster = context.Caster;

            Speed = Spell.Stats.Speed;
            Damage = Spell.Stats.Power;
            Lifetime = Spell.Stats.Lifetime;

            Direction = context.Direction;

            if (rb != null)
            {
                // CRITICAL FIX: Rotate the projectile to face the direction
                if (Direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(Direction);
                }

                // Apply velocity
                rb.linearVelocity = Direction * Speed;
            }
        }

        public void Expire()
        {
            if (IsExpired)
                return;
            IsExpired = true;

            foreach (var behaviour in Behaviours)
            {
                behaviour.OnDestroy(gameObject);
            }

            Destroy(gameObject);
        }

        public void AddBehaviour(ISpellBehaviour behaviour)
        {
            Behaviours.Add(behaviour);
            if (gameObject.activeInHierarchy)
            {
                behaviour.OnAttach(gameObject);
            }
        }

        public T GetBehaviour<T>()
            where T : class, ISpellBehaviour
        {
            foreach (var behaviour in Behaviours)
            {
                if (behaviour is T typedBehaviour)
                    return typedBehaviour;
            }
            return null;
        }

        public bool HasBehaviour<T>()
            where T : class, ISpellBehaviour
        {
            return GetBehaviour<T>() != null;
        }
    }
}
