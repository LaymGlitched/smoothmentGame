using System.Collections.Generic;
using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class SpellProjectile : MonoBehaviour
    {
        // Core spell data - now with public setters
        public Spell Spell { get; set; }
        public GameObject Caster { get; set; }

        // Public stats
        public float Speed { get; set; }
        public float Damage { get; set; }
        public Vector3 Direction { get; set; }

        // Runtime state - now with public setter
        public float Lifetime { get; set; }
        public bool IsExpired { get; private set; }

        // Modular behaviour system
        public List<ISpellBehaviour> Behaviours = new();

        // References
        private Rigidbody rb;
        private float lifeTimer;
        private bool hasExploded = false;
        private bool shouldDestroy = true;
        private bool isInitialized = false;

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

            if (!isInitialized && rb != null && Direction != Vector3.zero)
            {
                ApplyDirection();
            }
        }

        void ApplyDirection()
        {
            if (Direction == Vector3.zero)
                return;

            transform.rotation = Quaternion.LookRotation(Direction.normalized);

            if (rb != null)
            {
                rb.linearVelocity = Direction.normalized * Speed;
            }

            isInitialized = true;
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
            if (rb != null && !IsExpired && !hasExploded && Direction != Vector3.zero)
            {
                Vector3 currentVel = rb.linearVelocity;
                Vector3 targetVel = Direction.normalized * Speed;
                
                if (Vector3.Distance(currentVel, targetVel) > 0.1f)
                {
                    rb.linearVelocity = targetVel;
                }
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsExpired || hasExploded)
                return;

            shouldDestroy = true;

            foreach (var behaviour in Behaviours)
            {
                behaviour.OnHit(gameObject, collision);
            }

            foreach (var behaviour in Behaviours)
            {
                behaviour.OnDestroy(gameObject);
            }

            if (!shouldDestroy)
                return;

            hasExploded = true;
            StartCoroutine(SmoothDestroyRoutine());
        }

        public void PreventDestruction()
        {
            shouldDestroy = false;
        }

        System.Collections.IEnumerator SmoothDestroyRoutine()
        {
            if (TryGetComponent<Collider>(out var col))
                col.enabled = false;
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            ParticleSystem[] childParticles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in childParticles)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

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

            if (rb != null && Direction != Vector3.zero)
            {
                ApplyDirection();
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