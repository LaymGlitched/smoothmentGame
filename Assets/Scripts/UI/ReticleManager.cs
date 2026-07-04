using System;
using GameCode.PlayerScripts;
using GameCode.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace GameCode.UI
{
    public class ReticleManager : MonoBehaviour
    {
        [Tooltip("The player's Health component. Auto-assigns on Start if left blank.")]
        [SerializeField]
        private Health healthComponent;

        [Tooltip("The player's Mana component. Auto-assigns on Start if left blank.")]
        [SerializeField]
        private Mana manaComponent;

        [Tooltip("The UI Image rendering the reticle.")]
        [SerializeField]
        private Image reticleImage;

        [Tooltip("How fast the reticle transitions to new values. Higher values are faster.")]
        [SerializeField]
        private float lerpSpeed = 10f;

        private Material reticleMaterial;

        // Cached shader property IDs for better performance
        private static readonly int ManaProperty = Shader.PropertyToID("_Mana");
        private static readonly int HealthProperty = Shader.PropertyToID("_Health");

        // Target positions to lerp towards
        private float targetHealthRatio;
        private float targetManaRatio;

        // Current actual values applied to the shader
        private float currentHealthRatio;
        private float currentManaRatio;

        private void Start()
        {
            // Instantiate material so changes only affect this specific instance
            reticleMaterial = Instantiate(reticleImage.material);
            reticleImage.material = reticleMaterial;

            // Auto-assign components if missing
            if (healthComponent == null)
                healthComponent = FindAnyObjectByType<Health>();
            if (manaComponent == null)
                manaComponent = FindAnyObjectByType<Mana>();

            // Subscribe to events
            if (healthComponent != null)
                healthComponent.OnHealthChanged.AddListener(OnHealthChanged);
            if (manaComponent != null)
                manaComponent.OnManaChanged.AddListener(OnManaChanged);
        }

        private void Update()
        {
            if (reticleMaterial == null)
                return;

            // Smoothly interpolate health ratio
            currentHealthRatio = Mathf.Lerp(
                currentHealthRatio,
                targetHealthRatio,
                Time.deltaTime * lerpSpeed
            );
            reticleMaterial.SetFloat(HealthProperty, currentHealthRatio);

            // Smoothly interpolate mana ratio
            currentManaRatio = Mathf.Lerp(
                currentManaRatio,
                targetManaRatio,
                Time.deltaTime * lerpSpeed
            );
            reticleMaterial.SetFloat(ManaProperty, currentManaRatio);
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (healthComponent != null)
                healthComponent.OnHealthChanged.RemoveListener(OnHealthChanged);
            if (manaComponent != null)
                manaComponent.OnManaChanged.RemoveListener(OnManaChanged);

            // Clean up instantiated material from memory
            if (reticleMaterial != null)
                Destroy(reticleMaterial);
        }

        private void OnManaChanged(float current, float max)
        {
            if (max <= 0)
                return;
            targetManaRatio = 1.0f - (current / max);
        }

        private void OnHealthChanged(float current, float max)
        {
            if (max <= 0)
                return;
            targetHealthRatio = 1.0f - (current / max);
        }
    }
}
