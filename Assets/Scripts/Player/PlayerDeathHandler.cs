using FPMovement;
using GameCode.PlayerScripts;
using UnityEngine;

namespace GameCode.PlayerScripts
{
    /// <summary>
    /// Handles player death events and disables the appropriate systems
    /// based on which movement system is active.
    /// </summary>
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Health health;

        [SerializeField]
        private Rigidbody rb;

        [Header("FPMovement System (New)")]
        [SerializeField]
        private RigidbodyFPController fPController;

        [SerializeField]
        private MouseLookController mouseLook;

        [SerializeField]
        private WallRunController wallRun;

        [SerializeField]
        private SlideController slide;

        [SerializeField]
        private LedgeTraversalController ledgeTraversal;

        [SerializeField]
        private DynamicFOVController dynamicFOV;

        [SerializeField]
        private HeadBobEffect headBob;

        [SerializeField]
        private FPAnimationController animationController;

        [SerializeField]
        private PlayerInputHandler inputHandler;

        [Header("Legacy Systems (Optional)")]
        [SerializeField]
        private MonoBehaviour[] legacySystemsToDisable;

        [Header("Visual")]
        [SerializeField]
        private GameObject[] objectsToDisableOnDeath;

        [SerializeField]
        private GameObject deathEffectPrefab;

        [SerializeField]
        private Transform deathEffectSpawnPoint;

        [Header("Physics")]
        [SerializeField]
        private bool enableRagdoll = true;

        [SerializeField]
        private Vector3 ragdollForce = new Vector3(0f, 2f, 2f);

        [SerializeField]
        private float ragdollForceRandomness = 0.5f;

        [Header("Death Settings")]
        [SerializeField]
        private float respawnDelay = 3f;

        [SerializeField]
        private bool autoRespawn = false;

        private bool isDead = false;
        private Vector3 respawnPosition;
        private Quaternion respawnRotation;

        private void Awake()
        {
            // Auto-find references if not set
            if (health == null)
                health = GetComponent<Health>();

            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (fPController == null)
                fPController = GetComponent<RigidbodyFPController>();

            if (mouseLook == null)
                mouseLook = GetComponentInChildren<MouseLookController>();

            if (animationController == null)
                animationController = GetComponent<FPAnimationController>();
            if (animationController == null)
                animationController = GetComponentInChildren<FPAnimationController>();

            if (inputHandler == null)
                inputHandler = GetComponent<PlayerInputHandler>();
            if (inputHandler == null)
                inputHandler = GetComponentInChildren<PlayerInputHandler>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDie.AddListener(HandleDeath);
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDie.RemoveListener(HandleDeath);
        }

        private void Start()
        {
            // Store respawn position
            respawnPosition = transform.position;
            respawnRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (isDead && !enableRagdoll && rb != null)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void HandleDeath()
        {
            if (isDead)
                return;

            isDead = true;

            // Trigger death animation
            if (animationController != null)
                animationController.Die();

            // Disable new FPMovement systems
            DisableFPMovementSystems();

            // Disable legacy systems if any
            DisableLegacySystems();

            // Disable visual objects
            DisableVisualObjects();

            // Spawn death effect
            SpawnDeathEffect();

            // Enable ragdoll physics
            if (enableRagdoll)
                EnableRagdoll();

            // Handle respawn
            if (autoRespawn)
                Invoke(nameof(Respawn), respawnDelay);
        }

        private void DisableFPMovementSystems()
        {
            if (fPController != null)
            {
                fPController.enabled = false;
                // If externally controlled, end it
                if (fPController.IsExternallyControlled)
                    fPController.EndExternalControl();
            }

            if (mouseLook != null)
                mouseLook.enabled = false;

            if (wallRun != null)
                wallRun.enabled = false;

            if (slide != null)
                slide.enabled = false;

            if (ledgeTraversal != null)
                ledgeTraversal.enabled = false;

            if (dynamicFOV != null)
                dynamicFOV.enabled = false;

            if (headBob != null)
                headBob.enabled = false;

            if (inputHandler != null)
                inputHandler.enabled = false;

            // Immediately stop linear and angular momentum so the player doesn't slide
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void DisableLegacySystems()
        {
            if (legacySystemsToDisable != null)
            {
                foreach (var system in legacySystemsToDisable)
                {
                    if (system != null)
                        system.enabled = false;
                }
            }
        }

        private void DisableVisualObjects()
        {
            if (objectsToDisableOnDeath != null)
            {
                foreach (var obj in objectsToDisableOnDeath)
                {
                    if (obj != null)
                        obj.SetActive(false);
                }
            }
        }

        private void SpawnDeathEffect()
        {
            if (deathEffectPrefab != null)
            {
                Vector3 spawnPos =
                    deathEffectSpawnPoint != null
                        ? deathEffectSpawnPoint.position
                        : transform.position;

                var effect = Instantiate(deathEffectPrefab, spawnPos, Quaternion.identity);
                Destroy(effect, 3f);
            }
        }

        private void EnableRagdoll()
        {
            if (rb == null)
                return;

            // Freeze rotation constraints for ragdoll effect
            rb.constraints = RigidbodyConstraints.None;

            // Apply random force for dramatic effect
            Vector3 force = ragdollForce;
            force.x += Random.Range(-ragdollForceRandomness, ragdollForceRandomness);
            force.z += Random.Range(-ragdollForceRandomness, ragdollForceRandomness);
            rb.AddForce(force, ForceMode.Impulse);

            // Add some random torque
            Vector3 torque = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f)
            );
            rb.AddTorque(torque, ForceMode.Impulse);
        }

        private void Respawn()
        {
            // Reset position
            transform.position = respawnPosition;
            transform.rotation = respawnRotation;

            // Reset physics
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Reset health
            if (health != null)
                health.ResetHealth();

            // Re-enable FPMovement systems
            EnableFPMovementSystems();

            // Re-enable visual objects
            EnableVisualObjects();

            // Re-enable legacy systems
            EnableLegacySystems();

            // Reset animation controller death state
            if (animationController != null)
                animationController.Revive();

            isDead = false;
        }

        private void EnableFPMovementSystems()
        {
            if (fPController != null)
                fPController.enabled = true;

            if (mouseLook != null)
                mouseLook.enabled = true;

            if (wallRun != null)
                wallRun.enabled = true;

            if (slide != null)
                slide.enabled = true;

            if (ledgeTraversal != null)
                ledgeTraversal.enabled = true;

            if (dynamicFOV != null)
                dynamicFOV.enabled = true;

            if (headBob != null)
                headBob.enabled = true;

            if (inputHandler != null)
                inputHandler.enabled = true;
        }

        private void EnableLegacySystems()
        {
            if (legacySystemsToDisable != null)
            {
                foreach (var system in legacySystemsToDisable)
                {
                    if (system != null)
                        system.enabled = true;
                }
            }
        }

        private void EnableVisualObjects()
        {
            if (objectsToDisableOnDeath != null)
            {
                foreach (var obj in objectsToDisableOnDeath)
                {
                    if (obj != null)
                        obj.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Set the respawn point.
        /// </summary>
        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            respawnPosition = position;
            respawnRotation = rotation;
        }

        /// <summary>
        /// Manually trigger respawn.
        /// </summary>
        public void ManualRespawn()
        {
            if (isDead)
                Respawn();
        }

        /// <summary>
        /// Check if player is dead.
        /// </summary>
        public bool IsDead => isDead;
    }
}
