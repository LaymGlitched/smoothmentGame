using System;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Titanfall/Apex-style sliding. Hold crouch while moving fast to initiate a slide.
    /// Slide maintains momentum with gradual deceleration. Fully independent component
    /// that can be toggled or removed without affecting the core controller.
    /// </summary>
    [RequireComponent(typeof(RigidbodyFPController))]
    public class SlideController : MonoBehaviour
    {
        [SerializeField]
        private FPMovementSettings settings;

        [Header("Feature Toggles")]
        public bool enableSliding = true;

        [Header("Slide Overrides")]
        [Tooltip("Override slide speed (0 = use settings value).")]
        public float slideSpeedOverride = 0f;

        [Tooltip("Override slide deceleration (0 = use settings value).")]
        public float slideDecelerationOverride = 0f;

        [Tooltip("Override minimum speed to slide (0 = use settings value).")]
        public float slideMinSpeedOverride = 0f;

        public bool IsSliding { get; private set; }

        public event Action<bool> SlideStateChanged; // true = started, false = stopped

        private RigidbodyFPController controller;
        private PlayerInputHandler input;
        private Rigidbody rb;

        private float slideTimer;
        public float SlideDuration => IsSliding ? slideTimer : 0f;
        public float LastSlideEndTime { get; private set; } = -999f;
        public float LastSlideDuration { get; private set; } = 0f;
        
        private float currentSlideHeightOffset;
        private bool wasCrouching;
        private bool canStartSlideThisCrouch;
        private float lastGroundedDuringSlide = -999f;

        private Vector3 lastCollisionNormal = Vector3.zero;
        private int lastContactCount = 0;

        // Cached values
        private float slideSpeed;
        private float slideDeceleration;
        private float slideMinSpeed;

        // Cache for performance
        private Vector3 cachedHorizontalVelocity;
        private float cachedCurrentSpeed;
        private bool isInitialized;

        private void Awake()
        {
            // Get controller reference but don't access its properties yet
            controller = GetComponent<RigidbodyFPController>();
            if (controller == null)
            {
                Debug.LogError(
                    "SlideController: RigidbodyFPController not found on " + gameObject.name
                );
                enabled = false;
                return;
            }

            // Don't access controller.Body or controller.Input here yet
            // They might not be initialized until Start()

            // Cache settings values
            if (settings == null)
            {
                Debug.LogError("SlideController: FPMovementSettings not assigned!");
                enabled = false;
                return;
            }

            slideSpeed = slideSpeedOverride > 0f ? slideSpeedOverride : settings.slideSpeed;
            slideDeceleration =
                slideDecelerationOverride > 0f
                    ? slideDecelerationOverride
                    : settings.slideDeceleration;
            slideMinSpeed =
                slideMinSpeedOverride > 0f ? slideMinSpeedOverride : settings.slideMinSpeed;
        }

        private void Start()
        {
            // Initialize after all Awake() calls are done
            InitializeReferences();
        }

        private void InitializeReferences()
        {
            if (isInitialized)
                return;

            if (controller == null)
            {
                controller = GetComponent<RigidbodyFPController>();
                if (controller == null)
                {
                    Debug.LogError("SlideController: RigidbodyFPController not found!");
                    enabled = false;
                    return;
                }
            }

            // Now it's safe to access controller properties
            rb = controller.Body;
            if (rb == null)
            {
                Debug.LogError(
                    $"SlideController: Rigidbody not found on {gameObject.name}. "
                        + "Make sure RigidbodyFPController has initialized its Rigidbody reference."
                );
                // Don't disable - retry in LateUpdate
                return;
            }

            input = controller.Input;
            isInitialized = true;
        }

        private void LateUpdate()
        {
            // If initialization failed in Start, try again
            if (!isInitialized)
            {
                InitializeReferences();
            }
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.CrouchStateChanged += OnCrouchStateChanged;
                controller.Jumped += OnJumped;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.CrouchStateChanged -= OnCrouchStateChanged;
                controller.Jumped -= OnJumped;
            }
        }

        private void OnCrouchStateChanged(bool crouching)
        {
            if (!isInitialized)
                return;

            wasCrouching = crouching;

            if (crouching)
            {
                canStartSlideThisCrouch = true;
                if (enableSliding && !IsSliding)
                {
                    TryStartSlide();
                }
            }
            else
            {
                if (IsSliding)
                {
                    StopSlide();
                }
            }
        }

        private void OnJumped()
        {
            // Jumping cancels slide
            if (IsSliding)
                StopSlide();
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!IsSliding) return;
            
            lastContactCount = collision.contactCount;
            if (collision.contactCount > 0)
            {
                lastCollisionNormal = collision.GetContact(0).normal;
            }
        }
        
        private void OnCollisionExit(Collision collision)
        {
            lastCollisionNormal = Vector3.zero;
            lastContactCount = 0;
        }

        private void FixedUpdate()
        {
            if (!isInitialized)
            {
                InitializeReferences();
                if (!isInitialized)
                    return;
            }

            if (!enableSliding || controller == null)
            {
                if (IsSliding)
                    StopSlide();
                return;
            }

            // Ensure we have valid references
            if (rb == null)
            {
                rb = controller.Body;
                if (rb == null)
                    return;
            }

            // Cache current speed for performance
            cachedHorizontalVelocity = controller.HorizontalVelocity;
            cachedCurrentSpeed = cachedHorizontalVelocity.magnitude;

            // Check if we should try to start sliding (crouch held + moving fast)
            if (!IsSliding && wasCrouching)
            {
                TryStartSlide();
            }

            // Update slide if active
            if (IsSliding)
            {
                UpdateSlide();
            }

            // Apply slide height offset (camera lowering)
            if (settings != null)
            {
                float targetOffset = IsSliding ? settings.slideHeightOffset : 0f;
                currentSlideHeightOffset = Mathf.Lerp(
                    currentSlideHeightOffset,
                    targetOffset,
                    Time.fixedDeltaTime * settings.slideHeightTransitionSpeed
                );
            }
        }

        private void TryStartSlide()
        {
            if (!canStartSlideThisCrouch)
                return;

            if (!enableSliding || IsSliding || controller == null)
                return;

            if (controller.IsExternallyControlled)
                return;

            // Must be grounded, moving fast, and have forward input
            if (!controller.IsGrounded)
                return;

            if (cachedCurrentSpeed < slideMinSpeed)
                return;

            // Must have forward input (can't slide backwards)
            if (input == null || input.MoveInput.y < 0.1f)
                return;

            StartSlide();
        }

        private void StartSlide()
        {
            if (IsSliding || controller == null || rb == null)
                return;

            IsSliding = true;
            slideTimer = 0f;
            canStartSlideThisCrouch = false;
            lastGroundedDuringSlide = Time.time;

            try
            {
                SlideStateChanged?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"SlideController: Error in SlideStateChanged event: {e.Message}");
            }

            // Use the player's current momentum direction for the slide, not the
            // camera's forward. This means a diagonal sprint naturally becomes a
            // diagonal slide instead of snapping to wherever the player is looking.
            // Fall back to camera forward only when horizontal speed is near-zero.
            Vector3 horizontalVel = controller.HorizontalVelocity;
            Vector3 slideDir;
            if (horizontalVel.sqrMagnitude > 0.25f) // > 0.5 m/s
            {
                slideDir = horizontalVel.normalized;
            }
            else
            {
                // Near-zero speed fallback: use camera forward (rare edge case —
                // e.g. ForceSlide from an ability while standing nearly still).
                slideDir = controller.Orientation != null
                    ? controller.Orientation.forward
                    : Vector3.forward;
                slideDir.y = 0f;
                slideDir.Normalize();
            }

            float speed = Mathf.Max(cachedCurrentSpeed, slideSpeed);
            Vector3 startVel = new Vector3(slideDir.x * speed, 0f, slideDir.z * speed);
            if (controller.IsGrounded)
            {
                startVel = Vector3.ProjectOnPlane(startVel, controller.GroundNormal);
            }
            else
            {
                startVel.y = rb.linearVelocity.y;
            }
            rb.linearVelocity = startVel;
        }

        private void UpdateSlide()
        {
            if (controller == null || rb == null)
            {
                StopSlide();
                return;
            }

            slideTimer += Time.fixedDeltaTime;

            // Track grounded state for airborne grace period
            if (controller.IsGrounded)
                lastGroundedDuringSlide = Time.time;

            if (controller.IsGrounded)
            {
                Vector3 velBeforeProj = rb.linearVelocity;
                Vector3 currentVel = rb.linearVelocity;
                Vector3 normal = controller.GroundNormal;
                
                // Project velocity to stay attached to ramps/dips smoothly
                currentVel = Vector3.ProjectOnPlane(currentVel, normal);
                Vector3 velAfterProj = currentVel;

                // Add gravity along slope manually since RigidbodyFPController disables it
                float gravityMult = settings != null ? settings.gravityMultiplier : 1f;
                Vector3 gravity = Physics.gravity * gravityMult;
                Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(gravity, normal);
                
                currentVel += gravityAlongSlope * Time.fixedDeltaTime;
                Vector3 velAfterGrav = currentVel;

                // Apply slide steering
                if (settings != null && settings.slideSteeringInfluence > 0f)
                {
                    float strafeInput = input != null ? input.MoveInput.x : 0f;
                    if (Mathf.Abs(strafeInput) > 0.05f && controller.Orientation != null)
                    {
                        Vector3 strafeDir = controller.Orientation.right * strafeInput;
                        strafeDir = Vector3.ProjectOnPlane(strafeDir, normal).normalized;
                        
                        float speed = currentVel.magnitude;
                        float steerForce = speed * settings.slideSteeringInfluence * Time.fixedDeltaTime;
                        currentVel += strafeDir * steerForce;
                        currentVel = currentVel.normalized * speed; // steering doesn't add total energy
                    }
                }

                // Apply slide deceleration
                float currentSpeed = currentVel.magnitude;
                if (currentSpeed > 0.01f)
                {
                    float dragMult = settings != null ? settings.slideDragFactor : slideDeceleration * 0.1f;
                    float baseDecel = settings != null ? settings.slideBaseDeceleration : slideDeceleration * 0.5f;

                    float retention = 1f - (dragMult * Time.fixedDeltaTime);
                    retention = Mathf.Max(retention, 0f);
                    
                    float newSpeed = (currentSpeed * retention) - (baseDecel * Time.fixedDeltaTime);
                    newSpeed = Mathf.Max(newSpeed, 0f);

                    currentVel = currentVel.normalized * newSpeed;
                }
                Vector3 velAfterDrag = currentVel;
                
                rb.linearVelocity = currentVel;
                Vector3 velAfterAssign = rb.linearVelocity;

                Debug.Log($"[SlideDebug] " +
                    $"VelBeforeProj: {velBeforeProj.magnitude:F2} | " +
                    $"VelAfterProj: {velAfterProj.magnitude:F2} | " +
                    $"VelAfterGrav: {velAfterGrav.magnitude:F2} | " +
                    $"VelAfterDrag: {velAfterDrag.magnitude:F2} | " +
                    $"VelAfterAssign: {velAfterAssign.magnitude:F2} | " +
                    $"ColNormal: {lastCollisionNormal} | " +
                    $"Contacts: {lastContactCount} | " +
                    $"GroundNormal: {normal}");

                Vector3 debugPos = transform.position + Vector3.up * 1f;
                Debug.DrawRay(debugPos, currentVel.normalized * 2f, Color.green);
                Debug.DrawRay(debugPos, normal * 2f, Color.blue);
                if (lastContactCount > 0)
                {
                    Debug.DrawRay(debugPos, lastCollisionNormal * 2f, Color.red);
                }
            }

            // --- Stop conditions ---
            bool shouldStop = false;

            // Stop if speed drops too low
            if (rb.linearVelocity.magnitude < 0.5f)
                shouldStop = true;

            // Stop if player tries to move backward
            if (input != null && input.MoveInput.y < -0.1f)
                shouldStop = true;

            // Stop if we're not crouching anymore
            if (!wasCrouching)
                shouldStop = true;

            // Airborne grace: small bumps and uneven terrain shouldn't instantly
            // kill the slide. Only stop if we've been airborne longer than the
            // grace period.
            if (!controller.IsGrounded)
            {
                float airborneGrace = settings != null ? settings.slideAirborneGrace : 0.15f;
                if (Time.time - lastGroundedDuringSlide > airborneGrace)
                    shouldStop = true;
            }

            if (shouldStop)
                StopSlide();
        }

        private void StopSlide()
        {
            if (!IsSliding)
                return;

            LastSlideEndTime = Time.time;
            LastSlideDuration = slideTimer;
            IsSliding = false;

            if (controller != null)
                controller.TriggerLandingFrictionGrace();

            try
            {
                SlideStateChanged?.Invoke(false);
            }
            catch (Exception e)
            {
                Debug.LogError($"SlideController: Error in SlideStateChanged event: {e.Message}");
            }
        }

        /// <summary>
        /// Get the current slide height offset for camera positioning.
        /// </summary>
        public Vector3 GetSlideHeightOffset()
        {
            return Vector3.down * currentSlideHeightOffset;
        }

        /// <summary>
        /// Check if the player can initiate a slide based on current conditions.
        /// </summary>
        public bool CanSlide()
        {
            if (!isInitialized)
                return false;

            if (!enableSliding || IsSliding || controller == null)
                return false;

            if (controller.IsExternallyControlled)
                return false;

            if (!controller.IsGrounded)
                return false;

            if (cachedCurrentSpeed < slideMinSpeed)
                return false;

            if (input == null || input.MoveInput.y < 0.1f)
                return false;

            return true;
        }

        /// <summary>
        /// Manually force a slide start (useful for abilities, power-ups, etc.)
        /// </summary>
        public void ForceSlide()
        {
            if (!enableSliding || IsSliding)
                return;

            if (!isInitialized)
                InitializeReferences();

            // Update cached values first
            if (controller != null)
            {
                cachedHorizontalVelocity = controller.HorizontalVelocity;
                cachedCurrentSpeed = cachedHorizontalVelocity.magnitude;
            }

            StartSlide();
        }

        /// <summary>
        /// Manually force slide to stop.
        /// </summary>
        public void ForceStopSlide()
        {
            if (IsSliding)
                StopSlide();
        }
    }
}
