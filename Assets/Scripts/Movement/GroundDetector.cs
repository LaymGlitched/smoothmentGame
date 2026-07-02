using UnityEngine;

namespace ParkourMovement
{
    /// <summary>
    /// Robust ground detection system using SphereCast.
    /// Handles slope detection, stair detection, and provides detailed ground information.
    /// </summary>
    public class GroundDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField]
        private float sphereCastRadius = 0.3f;

        [SerializeField]
        private float maxGroundDistance = 0.15f;

        [SerializeField]
        private float maxSlopeAngle = 50f;

        [SerializeField]
        private float stepOffset = 0.1f;

        [Header("Layer Configuration")]
        [SerializeField]
        private LayerMask groundLayers = -1;

        [SerializeField]
        private LayerMask stepLayers = -1;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugInfo = false;

        // Ground state
        private bool isGrounded;
        private Vector3 groundNormal = Vector3.up;
        private Vector3 groundPoint;
        private float groundAngle;
        private Collider groundCollider;
        private bool isOnStairs;

        // Cached components
        private Rigidbody rb;
        private CapsuleCollider playerCollider;

        public bool IsGrounded => isGrounded;
        public Vector3 GroundNormal => groundNormal;
        public Vector3 GroundPoint => groundPoint;
        public float GroundAngle => groundAngle;
        public Collider GroundCollider => groundCollider;
        public bool IsOnStairs => isOnStairs;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerCollider = GetComponent<CapsuleCollider>();
        }

        private void FixedUpdate()
        {
            DetectGround();
        }

        private void DetectGround()
        {
            if (playerCollider == null)
            {
                Debug.LogError("No collider found on player for ground detection!");
                return;
            }

            // Calculate the origin for the sphere cast
            Vector3 origin = transform.position + Vector3.up * (playerCollider.radius + stepOffset);
            float sphereCastDistance = maxGroundDistance + playerCollider.radius;

            // Perform sphere cast downward
            RaycastHit hit;
            isGrounded = Physics.SphereCast(
                origin,
                sphereCastRadius,
                Vector3.down,
                out hit,
                sphereCastDistance,
                groundLayers
            );

            if (isGrounded)
            {
                // Get ground information
                groundNormal = hit.normal;
                groundPoint = hit.point;
                groundCollider = hit.collider;

                // Calculate ground angle
                groundAngle = Vector3.Angle(Vector3.up, groundNormal);

                // Check if the ground is too steep
                if (groundAngle > maxSlopeAngle)
                {
                    isGrounded = false;
                }

                // Check for stairs
                CheckForStairs();

                // Additional ground verification with raycast
                PerformSecondaryGroundCheck();
            }
            else
            {
                groundNormal = Vector3.up;
                groundAngle = 0f;
                groundCollider = null;
                isOnStairs = false;
            }

            if (showDebugInfo)
            {
                DebugGroundDetection();
            }
        }

        private void CheckForStairs()
        {
            // Simple stair detection - cast additional rays forward
            Vector3 forward = transform.forward;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;

            RaycastHit stepHit;
            isOnStairs = Physics.Raycast(rayOrigin, forward, out stepHit, 0.5f, stepLayers);
        }

        private void PerformSecondaryGroundCheck()
        {
            // Perform a secondary raycast for more precise ground detection
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

            RaycastHit hit;
            if (
                Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out hit,
                    maxGroundDistance + 0.1f,
                    groundLayers
                )
            )
            {
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                if (angle <= maxSlopeAngle)
                {
                    isGrounded = true;
                    groundNormal = hit.normal;
                    groundPoint = hit.point;
                    groundAngle = angle;
                }
            }
        }

        private void DebugGroundDetection()
        {
            Color groundColor = isGrounded ? Color.green : Color.red;
            Debug.DrawRay(groundPoint, groundNormal * 0.5f, groundColor, 0.1f);

            // Draw sphere cast
            Vector3 origin = transform.position + Vector3.up * (playerCollider.radius + stepOffset);
            Debug.DrawRay(
                origin,
                Vector3.down * (maxGroundDistance + playerCollider.radius),
                groundColor,
                0.1f
            );
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCollider == null)
                return;

            // Visualize ground detection in editor
            Vector3 origin = transform.position + Vector3.up * (playerCollider.radius + stepOffset);

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(origin, sphereCastRadius);
            Gizmos.DrawWireSphere(
                origin + Vector3.down * (maxGroundDistance + playerCollider.radius),
                sphereCastRadius
            );
        }
    }
}
