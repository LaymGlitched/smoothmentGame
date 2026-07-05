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
        private float currentSlideHeightOffset;
        private bool wasCrouching;
        private bool canStartSlideThisCrouch;

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

            try
            {
                SlideStateChanged?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"SlideController: Error in SlideStateChanged event: {e.Message}");
            }

            // Apply initial slide velocity in the direction we're facing
            if (controller.Orientation != null)
            {
                Vector3 slideDir = controller.Orientation.forward;
                float speed = Mathf.Max(cachedCurrentSpeed, slideSpeed);
                rb.linearVelocity = new Vector3(
                    slideDir.x * speed,
                    rb.linearVelocity.y,
                    slideDir.z * speed
                );
            }
            else
            {
                // Fallback: use current velocity direction
                Vector3 horizontalVel = controller.HorizontalVelocity;
                if (horizontalVel.magnitude > 0.1f)
                {
                    float speed = Mathf.Max(horizontalVel.magnitude, slideSpeed);
                    rb.linearVelocity = new Vector3(
                        horizontalVel.normalized.x * speed,
                        rb.linearVelocity.y,
                        horizontalVel.normalized.z * speed
                    );
                }
            }
        }

        private void UpdateSlide()
        {
            if (controller == null || rb == null)
            {
                StopSlide();
                return;
            }

            slideTimer += Time.fixedDeltaTime;

            // Decelerate during slide
            float currentSpeed = cachedCurrentSpeed;

            if (currentSpeed > 0.01f)
            {
                float decelAmount = slideDeceleration * Time.fixedDeltaTime;
                float newSpeed = Mathf.Max(0f, currentSpeed - decelAmount);

                if (cachedHorizontalVelocity.magnitude > 0.01f)
                {
                    Vector3 newVel = cachedHorizontalVelocity.normalized * newSpeed;
                    rb.linearVelocity = new Vector3(newVel.x, rb.linearVelocity.y, newVel.z);
                }
            }

            // Stop sliding if conditions aren't met
            bool shouldStop = false;

            // Stop if speed drops too low
            if (cachedCurrentSpeed < 0.5f)
                shouldStop = true;

            // Stop if player tries to move backward
            if (input != null && input.MoveInput.y < -0.1f)
                shouldStop = true;

            // Stop if we're not crouching anymore
            if (!wasCrouching)
                shouldStop = true;

            // Optional: Stop if not grounded
            // if (!controller.IsGrounded)
            //     shouldStop = true;

            if (shouldStop)
                StopSlide();
        }

        private void StopSlide()
        {
            if (!IsSliding)
                return;

            IsSliding = false;

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
