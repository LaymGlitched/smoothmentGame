using UnityEngine;

namespace ParkourMovement
{
    /// <summary>
    /// Master controller that initializes and manages all parkour movement systems.
    /// Provides a single point of configuration and ensures proper system initialization.
    ///
    /// Extension Guide:
    /// To add new movement mechanics, create a new component that follows this pattern:
    /// 1. Reference PlayerMovement and GroundDetector
    /// 2. Process in FixedUpdate for physics-based mechanics
    /// 3. Add to this controller's initialization list
    /// 4. Use InputActionReference fields for input
    ///
    /// Example mechanics to add:
    /// - WallRunning: Detect walls at sprint speed, apply force along wall
    /// - Sliding: Reduce collider height, maintain momentum on slopes
    /// - Mantling: Grab ledges, pull up using animation curves
    /// - GrapplingHook: Raycast to point, apply spring force toward it
    /// - Climbing: Latch onto climbable surfaces, free movement on surface
    /// - Dash: Burst of velocity in movement direction with cooldown
    /// </summary>
    public class ParkourController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField]
        private PlayerMovement playerMovement;

        [SerializeField]
        private PlayerLook playerLook;

        [SerializeField]
        private GroundDetector groundDetector;

        [SerializeField]
        private StairStepper stairStepper;

        [SerializeField]
        private VaultAssist vaultAssist;

        [Header("Player Setup")]
        [SerializeField]
        private Rigidbody playerRigidbody;

        [SerializeField]
        private CapsuleCollider playerCollider;

        [SerializeField]
        private Camera playerCamera;

        [SerializeField]
        private Transform cameraHolder;

        [Header("Physics Configuration")]
        [SerializeField]
        private float playerMass = 80f;

        [SerializeField]
        private float playerHeight = 1.8f;

        [SerializeField]
        private float playerRadius = 0.3f;

        [SerializeField]
        private PhysicsMaterial playerPhysicsMaterial;

        private void Awake()
        {
            InitializeComponents();
            ConfigurePhysics();
        }

        private void InitializeComponents()
        {
            if (playerRigidbody == null)
                playerRigidbody = GetComponent<Rigidbody>();

            if (playerCollider == null)
                playerCollider = GetComponent<CapsuleCollider>();

            if (playerMovement == null)
                playerMovement = GetComponent<PlayerMovement>();

            if (playerLook == null)
                playerLook = GetComponentInChildren<PlayerLook>();

            if (groundDetector == null)
                groundDetector = GetComponent<GroundDetector>();

            if (stairStepper == null)
                stairStepper = GetComponent<StairStepper>();

            if (vaultAssist == null)
                vaultAssist = GetComponent<VaultAssist>();

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
        }

        private void ConfigurePhysics()
        {
            if (playerRigidbody != null)
            {
                playerRigidbody.mass = playerMass;
                playerRigidbody.linearDamping = 0f;
                playerRigidbody.angularDamping = 0.05f;
                playerRigidbody.useGravity = false; // We handle gravity in PlayerMovement
                playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                playerRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (playerCollider != null)
            {
                playerCollider.height = playerHeight;
                playerCollider.radius = playerRadius;
                playerCollider.center = new Vector3(0f, playerHeight * 0.5f, 0f);
            }
        }

        private void ValidateSetup()
        {
            // Ensure all critical components are present
            if (playerRigidbody == null)
                Debug.LogError(
                    "ParkourController: No Rigidbody found! Player requires a Rigidbody component."
                );

            if (playerCollider == null)
                Debug.LogError(
                    "ParkourController: No CapsuleCollider found! Player requires a CapsuleCollider."
                );

            if (playerMovement == null)
                Debug.LogError("ParkourController: PlayerMovement component missing!");

            if (playerLook == null)
                Debug.LogError("ParkourController: PlayerLook component missing!");

            if (groundDetector == null)
                Debug.LogWarning(
                    "ParkourController: GroundDetector not found. Ground detection will not work."
                );

            if (playerCamera == null)
                Debug.LogWarning(
                    "ParkourController: No camera assigned. Player will have no view."
                );

            // Validate camera setup
            if (playerCamera != null && cameraHolder == null)
                Debug.LogWarning(
                    "ParkourController: Camera should be child of a holder object for proper rotation."
                );
        }

        // Public methods for external systems

        /// <summary>
        /// Enable or disable all movement systems
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            if (playerMovement != null)
                playerMovement.enabled = enabled;
            if (playerLook != null)
                playerLook.enabled = enabled;
            if (stairStepper != null)
                stairStepper.enabled = enabled;
            if (vaultAssist != null)
                vaultAssist.enabled = enabled;
        }

        /// <summary>
        /// Reset player to a specific position with zero velocity
        /// </summary>
        public void ResetPosition(Vector3 position, Quaternion rotation)
        {
            if (playerRigidbody != null)
            {
                playerRigidbody.position = position;
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            transform.position = position;
            transform.rotation = rotation;
        }

        /// <summary>
        /// Get current player state for external systems
        /// </summary>
        public PlayerState GetPlayerState()
        {
            return new PlayerState
            {
                position = transform.position,
                velocity = playerRigidbody != null ? playerRigidbody.linearVelocity : Vector3.zero,
                isGrounded = groundDetector != null && groundDetector.IsGrounded,
                isSprinting = playerMovement != null && playerMovement.IsSprinting,
                isVaulting = vaultAssist != null && vaultAssist.IsVaulting,
                currentSpeed = playerMovement != null ? playerMovement.CurrentSpeed : 0f,
                groundNormal = groundDetector != null ? groundDetector.GroundNormal : Vector3.up,
            };
        }
    }

    /// <summary>
    /// Serializable player state for external systems
    /// </summary>
    [System.Serializable]
    public struct PlayerState
    {
        public Vector3 position;
        public Vector3 velocity;
        public bool isGrounded;
        public bool isSprinting;
        public bool isVaulting;
        public float currentSpeed;
        public Vector3 groundNormal;
    }
}
