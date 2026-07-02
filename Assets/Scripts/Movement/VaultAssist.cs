using UnityEngine;

namespace ParkourMovement
{
    /// <summary>
    /// Vault assist system that helps players smoothly traverse obstacles.
    ///
    /// Algorithm Explanation:
    /// 1. Detect forward obstacles within vault height range
    /// 2. Check for valid landing surface on top of obstacle
    /// 3. Calculate optimal vault trajectory
    /// 4. Apply smooth positional correction over time
    ///
    /// The system preserves momentum by blending existing velocity into the vault motion.
    /// It detects when a player jumps near an edge (70-110 degree surfaces) and assists
    /// them over the obstacle without teleportation.
    ///
    /// Future extensibility: This system can be extended to support:
    /// - Wall running (detect vertical surfaces at sprint speed)
    /// - Mantling (climbing up from hanging position)
    /// - Slide mechanics (low obstacles with crouching)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VaultAssist : MonoBehaviour
    {
        // Enum defining the different states of vaulting
        private enum VaultState
        {
            None, // Not vaulting
            Detecting, // Checking for vaultable obstacles
            Ascending, // Moving upward over the obstacle
            Descending, // Coming down on the other side
            Completed, // Vault finished
        }

        [Header("Vault Detection")]
        [SerializeField]
        private float vaultDetectionDistance = 0.75f;

        [SerializeField]
        private float vaultHeightMin = 0.5f;

        [SerializeField]
        private float vaultHeightMax = 1.5f;

        [SerializeField]
        private float vaultAngleMin = 70f;

        [SerializeField]
        private float vaultAngleMax = 110f;

        [SerializeField]
        private float vaultCheckRadius = 0.3f;

        [Header("Vault Execution")]
        [SerializeField]
        private float vaultDuration = 0.3f;

        [SerializeField]
        private float momentumPreservationFactor = 0.8f;

        [SerializeField]
        private float vaultUpForce = 3f;

        [SerializeField]
        private float vaultForwardForce = 2f;

        [SerializeField]
        private float vaultSmoothSpeed = 15f;

        [Header("Layer Configuration")]
        [SerializeField]
        private LayerMask vaultLayers = -1;

        [SerializeField]
        private LayerMask groundLayers = -1;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugInfo = true;

        // Internal state
        private Rigidbody rb;
        private PlayerMovement playerMovement;
        private GroundDetector groundDetector;
        private VaultState currentVaultState;
        private float vaultTimer;
        private Vector3 vaultStartPosition;
        private Vector3 vaultTargetPosition;
        private Vector3 vaultVelocity;
        private bool isVaulting;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerMovement = GetComponent<PlayerMovement>();
            groundDetector = GetComponent<GroundDetector>();
            currentVaultState = VaultState.None;
        }

        private void FixedUpdate()
        {
            if (isVaulting)
            {
                UpdateVault();
                return;
            }

            // Check for vault opportunities when jumping or moving forward
            if (ShouldCheckForVault())
            {
                CheckForVault();
            }
        }

        private bool ShouldCheckForVault()
        {
            // Check for vault when jumping or moving forward at speed
            if (playerMovement == null)
                return false;

            bool isJumping = rb.linearVelocity.y > 0f;
            bool hasForwardMomentum = playerMovement.CurrentSpeed > 3f;
            bool isMovingForward = playerMovement.MoveDirection.magnitude > 0.5f;
            bool isNotGrounded = groundDetector != null && !groundDetector.IsGrounded;

            return (isJumping || hasForwardMomentum) && isMovingForward && isNotGrounded;
        }

        private void CheckForVault()
        {
            currentVaultState = VaultState.Detecting;

            Vector3 forward = transform.forward;
            Vector3 checkOrigin = transform.position + Vector3.up * 0.3f;

            // Cast multiple rays for obstacle detection
            RaycastHit hit;

            Debug.DrawRay(checkOrigin, forward * vaultDetectionDistance, Color.magenta, 0.1f);

            if (
                Physics.SphereCast(
                    checkOrigin,
                    vaultCheckRadius,
                    forward,
                    out hit,
                    vaultDetectionDistance,
                    vaultLayers
                )
            )
            {
                // Check surface angle (should be wall-like: 70-110 degrees)
                float surfaceAngle = Vector3.Angle(Vector3.up, hit.normal);

                if (surfaceAngle >= vaultAngleMin && surfaceAngle <= vaultAngleMax)
                {
                    // Check obstacle height
                    float obstacleHeight = hit.collider.bounds.max.y - transform.position.y;

                    if (obstacleHeight >= vaultHeightMin && obstacleHeight <= vaultHeightMax)
                    {
                        // Check for valid landing surface on top
                        if (CheckLandingSurface(hit))
                        {
                            // Calculate vault target position
                            Vector3 vaultTarget = CalculateVaultTarget(hit);

                            if (vaultTarget != Vector3.zero)
                            {
                                StartVault(vaultTarget, hit);
                            }
                        }
                    }
                }
            }

            if (!isVaulting)
            {
                currentVaultState = VaultState.None;
            }
        }

        private bool CheckLandingSurface(RaycastHit wallHit)
        {
            // Cast ray from above the obstacle to check for landing surface
            Vector3 checkPoint =
                wallHit.point + Vector3.up * vaultHeightMax + transform.forward * 0.3f;

            Debug.DrawRay(checkPoint, Vector3.down * (vaultHeightMax + 0.2f), Color.cyan, 0.5f);

            RaycastHit landHit;
            if (
                Physics.Raycast(
                    checkPoint,
                    Vector3.down,
                    out landHit,
                    vaultHeightMax + 0.2f,
                    groundLayers
                )
            )
            {
                // Check if the surface is walkable
                float landAngle = Vector3.Angle(Vector3.up, landHit.normal);

                // Check if landing point is above the obstacle
                float heightDifference = landHit.point.y - wallHit.point.y;

                return landAngle < 45f && heightDifference > 0.2f;
            }

            return false;
        }

        private Vector3 CalculateVaultTarget(RaycastHit wallHit)
        {
            // Calculate landing position
            Vector3 landPoint = wallHit.point + Vector3.up * vaultHeightMin;

            // Cast down to find exact surface
            RaycastHit surfaceHit;
            if (
                Physics.Raycast(
                    landPoint + Vector3.up * 0.5f,
                    Vector3.down,
                    out surfaceHit,
                    1f,
                    groundLayers
                )
            )
            {
                // Calculate target with offset for player height
                Vector3 targetPosition = surfaceHit.point + Vector3.up * 1f; // Player height offset

                // Add forward offset based on momentum
                Vector3 momentumDirection = rb.linearVelocity.normalized;
                targetPosition += momentumDirection * 0.3f;

                return targetPosition;
            }

            return Vector3.zero;
        }

        private void StartVault(Vector3 targetPosition, RaycastHit obstacleHit)
        {
            isVaulting = true;
            currentVaultState = VaultState.Ascending;
            vaultTimer = 0f;
            vaultStartPosition = transform.position;
            vaultTargetPosition = targetPosition;

            // Preserve existing momentum
            vaultVelocity = rb.linearVelocity * momentumPreservationFactor;

            // Add vault forces
            vaultVelocity += Vector3.up * vaultUpForce;
            vaultVelocity += transform.forward * vaultForwardForce;

            if (showDebugInfo)
            {
                Debug.Log(
                    $"Vault started! Target: {targetPosition}, Velocity: {vaultVelocity.magnitude:F1}"
                );
            }
        }

        private void UpdateVault()
        {
            vaultTimer += Time.fixedDeltaTime;
            float progress = vaultTimer / vaultDuration;

            // Update vault state based on progress
            if (progress < 0.5f)
            {
                currentVaultState = VaultState.Ascending;
            }
            else
            {
                currentVaultState = VaultState.Descending;
            }

            if (progress >= 1f)
            {
                CompleteVault();
                return;
            }

            // Smooth interpolation with easing
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            // Calculate current vault position
            Vector3 targetPosition = Vector3.Lerp(
                vaultStartPosition,
                vaultTargetPosition,
                smoothProgress
            );

            // Apply velocity-based movement
            Vector3 velocityMovement = vaultVelocity * Time.fixedDeltaTime;

            // Combine interpolation and momentum
            Vector3 newPosition = Vector3.Lerp(
                transform.position,
                targetPosition + velocityMovement,
                vaultSmoothSpeed * Time.fixedDeltaTime
            );

            // Use MovePosition for physics-safe movement
            rb.MovePosition(newPosition);

            // Gradually transfer control back to player
            if (progress > 0.5f)
            {
                float controlReturn = (progress - 0.5f) * 2f;
                rb.linearVelocity = Vector3.Lerp(vaultVelocity, rb.linearVelocity, controlReturn);
            }
            else
            {
                rb.linearVelocity = vaultVelocity;
            }

            if (showDebugInfo)
            {
                Debug.DrawLine(transform.position, vaultTargetPosition, Color.green, 0.1f);
                Debug.DrawLine(
                    transform.position,
                    transform.position + vaultVelocity,
                    Color.yellow,
                    0.1f
                );
            }
        }

        private void CompleteVault()
        {
            isVaulting = false;
            currentVaultState = VaultState.Completed;
            transform.position = vaultTargetPosition;

            // Return control to player with preserved momentum
            rb.linearVelocity = vaultVelocity * momentumPreservationFactor;

            if (showDebugInfo)
            {
                Debug.Log($"Vault completed! State: {currentVaultState}");
            }

            // Reset state after a frame
            Invoke(nameof(ResetVaultState), Time.fixedDeltaTime);
        }

        private void ResetVaultState()
        {
            if (!isVaulting)
            {
                currentVaultState = VaultState.None;
            }
        }

        // Public method to check if currently vaulting
        public bool IsVaulting => isVaulting;

        // Public method to get current vault state (useful for animation systems)
        public string GetVaultState() => currentVaultState.ToString();

        private void OnDrawGizmosSelected()
        {
            // Visualize vault detection
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Vector3 checkOrigin = transform.position + Vector3.up * 0.3f;
            Gizmos.DrawRay(checkOrigin, transform.forward * vaultDetectionDistance);
            Gizmos.DrawWireSphere(
                checkOrigin + transform.forward * vaultDetectionDistance,
                vaultCheckRadius
            );

            // Show current vault state if active
            if (isVaulting)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(vaultTargetPosition, 0.2f);
                Gizmos.DrawLine(transform.position, vaultTargetPosition);
            }
        }
    }
}
