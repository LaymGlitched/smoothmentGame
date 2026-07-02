using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkourMovement
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Rigidbody rb;

        [SerializeField]
        private PlayerLook playerLook; // Changed from cameraTransform

        [SerializeField]
        private GroundDetector groundDetector;

        [SerializeField]
        private StairStepper stairStepper;

        [SerializeField]
        private VaultAssist vaultAssist;

        [Header("Input Actions")]
        [SerializeField]
        private InputActionReference moveAction;

        [SerializeField]
        private InputActionReference jumpAction;

        [SerializeField]
        private InputActionReference sprintAction;

        [Header("Ground Movement")]
        [SerializeField]
        private float walkSpeed = 8f;

        [SerializeField]
        private float sprintSpeed = 14f;

        [SerializeField]
        private float accelerationForce = 800f;

        [SerializeField]
        private float maxForce = 1200f;

        [SerializeField]
        private float frictionAmount = 12f;

        [SerializeField]
        private float stopThreshold = 0.5f;

        [Header("Air Movement")]
        [SerializeField]
        private float airAccelerationForce = 200f;

        [SerializeField]
        private float airMaxForce = 400f;

        [SerializeField]
        private float airControl = 0.4f;

        [SerializeField]
        private float maxAirSpeed = 20f;

        [Header("Jump")]
        [SerializeField]
        private float jumpForce = 10f;

        [SerializeField]
        private float coyoteTime = 0.15f;

        [SerializeField]
        private float jumpBufferTime = 0.12f;

        [SerializeField]
        private float jumpCooldown = 0.1f;

        [Header("Ground Stick")]
        [SerializeField]
        private float groundStickForce = -5f;

        [SerializeField]
        private float slopeForce = 15f;

        // Input state
        private Vector2 moveInput;
        private bool sprintHeld;
        private bool jumpPressed;

        // Timers
        private float coyoteTimer;
        private float jumpBufferTimer;
        private float lastJumpTime;

        // Movement state
        private Vector3 desiredMoveDirection;
        private bool isGrounded;
        private float currentMaxSpeed;

        private void OnEnable()
        {
            // Enable input actions
            if (moveAction != null)
                moveAction.action.Enable();
            if (jumpAction != null)
                jumpAction.action.Enable();
            if (sprintAction != null)
                sprintAction.action.Enable();

            // Subscribe to jump
            if (jumpAction != null)
                jumpAction.action.performed += OnJump;

            // Get components
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (groundDetector == null)
                groundDetector = GetComponent<GroundDetector>();
            if (playerLook == null)
                playerLook = GetComponentInChildren<PlayerLook>();

            // Configure Rigidbody for smooth movement
            rb.linearDamping = 0f;
            rb.angularDamping = 5f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.mass = 80f;
        }

        private void OnDisable()
        {
            if (moveAction != null)
                moveAction.action.Disable();
            if (jumpAction != null)
                jumpAction.action.Disable();
            if (sprintAction != null)
                sprintAction.action.Disable();

            if (jumpAction != null)
                jumpAction.action.performed -= OnJump;
        }

        private void OnJump(InputAction.CallbackContext ctx)
        {
            jumpPressed = true;
            jumpBufferTimer = jumpBufferTime;
        }

        private void Update()
        {
            // Read input in Update for responsiveness
            ReadInput();

            // Update timers
            if (jumpBufferTimer > 0f)
                jumpBufferTimer -= Time.deltaTime;
        }

        private void ReadInput()
        {
            moveInput = moveAction?.action.ReadValue<Vector2>() ?? Vector2.zero;
            sprintHeld = sprintAction?.action.ReadValue<float>() > 0.5f;
        }

        private void FixedUpdate()
        {
            // Get ground state
            isGrounded = groundDetector != null && groundDetector.IsGrounded;

            // Update coyote timer
            if (isGrounded)
                coyoteTimer = coyoteTime;
            else if (coyoteTimer > 0f)
                coyoteTimer -= Time.fixedDeltaTime;

            // Calculate desired movement direction from camera
            CalculateMovementDirection();

            // Apply movement forces
            ApplyMovementForces();

            // Handle jumping
            HandleJump();

            // Keep player on ground when walking
            if (isGrounded && !jumpPressed)
                StickToGround();

            // Process stair stepping
            if (stairStepper != null && isGrounded)
                stairStepper.ProcessStep(rb, desiredMoveDirection, isGrounded);

            // Reset per-frame state
            jumpPressed = false;
        }

        private void CalculateMovementDirection()
        {
            if (playerLook != null)
            {
                // Use PlayerLook to get camera-relative movement direction
                desiredMoveDirection = playerLook.GetMoveDirection(moveInput);
            }
            else
            {
                // Fallback to transform-based movement
                desiredMoveDirection = (
                    transform.forward * moveInput.y + transform.right * moveInput.x
                ).normalized;
            }

            // Set max speed based on sprint
            currentMaxSpeed = sprintHeld ? sprintSpeed : walkSpeed;
        }

        private void ApplyMovementForces()
        {
            if (isGrounded)
                ApplyGroundForces();
            else
                ApplyAirForces();
        }

        private void ApplyGroundForces()
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float currentSpeed = horizontalVelocity.magnitude;

            if (desiredMoveDirection.magnitude > 0.1f)
            {
                // Calculate target velocity
                Vector3 targetVelocity = desiredMoveDirection * currentMaxSpeed;

                // Calculate the force needed to reach target velocity
                Vector3 velocityDelta = targetVelocity - horizontalVelocity;

                // Scale force based on how far we are from target
                float forceScale = Mathf.Clamp01(velocityDelta.magnitude / currentMaxSpeed);
                Vector3 movementForce = velocityDelta.normalized * accelerationForce * forceScale;

                // Clamp force
                movementForce = Vector3.ClampMagnitude(movementForce, maxForce);

                // Apply force
                rb.AddForce(movementForce, ForceMode.Force);

                // Speed limiting
                if (currentSpeed > currentMaxSpeed)
                {
                    Vector3 damping =
                        -horizontalVelocity.normalized * (currentSpeed - currentMaxSpeed) * 5f;
                    rb.AddForce(damping, ForceMode.Force);
                }
            }
            else
            {
                // No input - apply friction
                if (currentSpeed > stopThreshold)
                {
                    Vector3 friction =
                        -horizontalVelocity.normalized * frictionAmount * currentSpeed;
                    rb.AddForce(friction, ForceMode.Force);
                }
                else if (currentSpeed > 0.01f)
                {
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                }
            }
        }

        private void ApplyAirForces()
        {
            if (desiredMoveDirection.magnitude < 0.1f)
                return;

            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float currentAirSpeed = horizontalVelocity.magnitude;

            if (currentAirSpeed < maxAirSpeed)
            {
                Vector3 airForce = desiredMoveDirection * airAccelerationForce * airControl;

                // Scale force based on direction alignment
                float directionAlignment = Vector3.Dot(
                    horizontalVelocity.normalized,
                    desiredMoveDirection
                );
                float alignmentMultiplier = Mathf.Lerp(1f, 0.2f, (directionAlignment + 1f) * 0.5f);

                airForce *= alignmentMultiplier;
                airForce = Vector3.ClampMagnitude(airForce, airMaxForce);

                rb.AddForce(airForce, ForceMode.Force);
            }

            // Limit air speed
            if (currentAirSpeed > maxAirSpeed)
            {
                Vector3 damped = horizontalVelocity.normalized * maxAirSpeed;
                rb.linearVelocity = new Vector3(damped.x, rb.linearVelocity.y, damped.z);
            }
        }

        private void HandleJump()
        {
            bool canJump =
                jumpPressed
                && jumpBufferTimer > 0f
                && (isGrounded || coyoteTimer > 0f)
                && Time.time - lastJumpTime > jumpCooldown;

            if (canJump)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                lastJumpTime = Time.time;
            }
        }

        private void StickToGround()
        {
            if (rb.linearVelocity.y < 0.1f)
            {
                rb.AddForce(Vector3.up * groundStickForce, ForceMode.Force);
            }

            if (groundDetector != null && groundDetector.GroundAngle > 5f)
            {
                Vector3 slopeVelocity = Vector3.ProjectOnPlane(
                    rb.linearVelocity,
                    groundDetector.GroundNormal
                );
                Vector3 slopeForceVector = (slopeVelocity - rb.linearVelocity) * slopeForce;
                rb.AddForce(slopeForceVector, ForceMode.Force);
            }
        }

        // Public accessors
        public Vector3 MoveDirection => desiredMoveDirection;
        public bool IsSprinting => sprintHeld;
        public float CurrentSpeed => rb.linearVelocity.magnitude;
        public float CurrentMaxSpeed => currentMaxSpeed;
        public Vector3 CurrentVelocity => rb.linearVelocity;
    }
}
