using System;
using Nanodogs.API.Nanoshake;
using UnityEngine;

namespace FPMovement
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(GroundSensor))]
    public class RigidbodyFPController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private FPMovementSettings settings;

        [SerializeField]
        private PlayerInputHandler input;

        [SerializeField]
        private Transform orientation; // yaw-only transform, forward = move direction

        [SerializeField]
        private StaminaSystem stamina; // optional, leave empty to disable stamina cost

        [Header("Visual Effects")]
        [SerializeField]
        [Tooltip("The wind particle system to control based on speed.")]
        private ParticleSystem windParticles;

        [SerializeField]
        [Tooltip("The speed at which wind particles reach their maximum emission rate and speed.")]
        private float maxWindSpeed = 35f;

        [SerializeField]
        [Tooltip("The minimum speed required before wind particles start emitting.")]
        private float windSpeedThreshold = 15f;

        [SerializeField]
        [Tooltip("The maximum emission rate of the wind particles.")]
        private float maxWindEmissionRate = 50f;

        [Header("Feature Toggles")]
        [Tooltip("Master switch for sprinting.")]
        public bool enableSprint = true;

        [Tooltip("Master switch for jumping.")]
        public bool enableJump = true;

        [Tooltip("Master switch for crouching.")]
        public bool enableCrouch = true;

        [Tooltip("If off, sprint is unlimited even if a StaminaSystem is assigned.")]
        public bool enableStamina = true;

        // ---- Public state other systems (FOV, head bob, animator, UI) read ----
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSliding => slideController != null && slideController.IsSliding;

        private Vector3 _horizontalVelocity;
        public Vector3 HorizontalVelocity
        {
            get
            {
                if (rb != null)
                {
                    _horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    return _horizontalVelocity;
                }
                return Vector3.zero;
            }
        }

        private float _currentSpeed;
        public float CurrentSpeed
        {
            get
            {
                if (rb != null)
                {
                    Vector3 hVel = HorizontalVelocity;
                    _currentSpeed = hVel.magnitude;
                    return _currentSpeed;
                }
                return 0f;
            }
        }

        public float SpeedNormalized =>
            Mathf.Clamp01(
                CurrentSpeed / Mathf.Max(0.01f, settings != null ? settings.sprintSpeed : 10f)
            );

        /// <summary>True while a system like WallRunController or LedgeTraversalController
        /// has taken temporary manual control of the Rigidbody. Normal movement, jump and
        /// gravity are skipped while this is true so the two never fight each other.</summary>
        public bool IsExternallyControlled { get; private set; }

        /// <summary>Time.time when the last external control ended. Subsystems use this
        /// to implement grace periods that prevent competing state grabs.</summary>
        public float LastExternalControlEndTime { get; private set; } = -999f;

        public Rigidbody Body => rb;
        public Transform Orientation => orientation;
        public PlayerInputHandler Input => input;
        public FPMovementSettings Settings => settings;

        private float _colliderRadius;
        public float ColliderRadius
        {
            get
            {
                if (capsule != null)
                    _colliderRadius = capsule.radius;
                return _colliderRadius;
            }
        }

        private float _colliderHeight;
        public float ColliderHeight
        {
            get
            {
                if (capsule != null)
                    _colliderHeight = capsule.height;
                return _colliderHeight;
            }
        }

        public Vector3 FeetPosition =>
            transform.position + Vector3.down * (capsule != null ? capsule.height * 0.5f : 0.5f);
        public Vector3 EyePosition =>
            transform.position
            + Vector3.up * (capsule != null ? capsule.height * 0.5f * 0.85f : 0.5f);

        public event Action Jumped;
        public event Action Kicked;
        public event Action<Vector3> Landed; // passes landing velocity
        public event Action<bool> SprintStateChanged;
        public event Action<bool> CrouchStateChanged;

        private Rigidbody rb;
        private CapsuleCollider capsule;
        private GroundSensor ground;
        private SlideController slideController;

        private float defaultHeight;
        private float targetHeight;
        private float lastGroundedTime;
        private float lastJumpPressedTime;
        private bool wasGroundedLastFrame;
        private bool jumpConsumedThisPress;
        private float lastLandingTime = -999f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            ground = GetComponent<GroundSensor>();
            slideController = GetComponent<SlideController>();

            if (rb == null)
                Debug.LogError("RigidbodyFPController: Rigidbody component missing!");

            if (capsule == null)
                Debug.LogError("RigidbodyFPController: CapsuleCollider component missing!");

            if (settings == null)
                Debug.LogError("RigidbodyFPController: FPMovementSettings not assigned!");

            if (ground == null)
                Debug.LogError("RigidbodyFPController: GroundSensor component missing!");

            if (rb != null)
            {
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (capsule != null)
            {
                defaultHeight = capsule.height;
                targetHeight = defaultHeight;
            }

            if (settings != null && ground != null)
            {
                ground.Configure(
                    settings.groundCheckDistance,
                    settings.groundMask,
                    settings.slopeLimit
                );
            }
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.OnJumpPressed += HandleJumpPressed;
                input.OnKickPressed += HandleKickPressed;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.OnJumpPressed -= HandleJumpPressed;
                input.OnKickPressed -= HandleKickPressed;
            }
        }

        private void HandleJumpPressed()
        {
            lastJumpPressedTime = Time.time;
            jumpConsumedThisPress = false;
        }

        private void HandleKickPressed()
        {
            Kicked?.Invoke();
            Nanoshake.Shake(false, null, 0.5f, 0.5f, 1f);
        }

        private void Update()
        {
            UpdateWindParticles();
        }

        private void UpdateWindParticles()
        {
            if (windParticles != null)
            {
                float speed = CurrentSpeed;
                var emission = windParticles.emission;
                var main = windParticles.main;

                if (speed <= windSpeedThreshold)
                {
                    emission.rateOverTime = 0f;
                    main.startSpeed = 4f;
                }
                else
                {
                    // Scale from threshold to max speed
                    float speedRange = Mathf.Max(0.1f, maxWindSpeed - windSpeedThreshold);
                    float speedRatio = Mathf.Clamp01((speed - windSpeedThreshold) / speedRange);
                    
                    emission.rateOverTime = Mathf.Lerp(0f, maxWindEmissionRate, speedRatio);
                    main.startSpeed = Mathf.Lerp(4f, 12f, speedRatio);
                }
            }
        }

        private void FixedUpdate()
        {
            if (ground != null)
            {
                ground.Probe();
                IsGrounded = ground.IsGrounded && ground.OnWalkableSlope;
            }
            else
            {
                IsGrounded = false;
            }

            if (IsGrounded)
                lastGroundedTime = Time.time;

            HandleLandingEvent();

            if (IsExternallyControlled)
            {
                wasGroundedLastFrame = IsGrounded;
                return;
            }

            HandleCrouch();
            HandleSprintAndStamina();
            HandleMovement();
            HandleJump();
            ApplyExtraGravity();

            wasGroundedLastFrame = IsGrounded;
        }

        /// <summary>
        /// Called by external traversal systems (wall run, vault, mantle, wall
        /// climb) to take manual ownership of the Rigidbody for a move. Normal
        /// movement/jump/gravity are skipped until EndExternalControl() is called.
        /// </summary>
        public void BeginExternalControl(bool zeroVelocity = true)
        {
            IsExternallyControlled = true;
            if (rb != null)
            {
                rb.useGravity = false;
                if (zeroVelocity)
                    rb.linearVelocity = Vector3.zero;
            }
        }

        public void EndExternalControl()
        {
            IsExternallyControlled = false;
            LastExternalControlEndTime = Time.time;
            if (rb != null)
                rb.useGravity = true;
            lastGroundedTime = Time.time; // treat hand-off point as "grounded enough" so jump isn't stuck in coyote limbo
        }

        /// <summary>Returns true if external control ended less than graceDuration seconds ago.
        /// Subsystems use this to avoid grabbing the player immediately after another
        /// system releases them (e.g. preventing wall run right after a climb).</summary>
        public bool InStateTransitionGrace(float graceDuration)
        {
            return Time.time - LastExternalControlEndTime < graceDuration;
        }

        private void HandleLandingEvent()
        {
            if (IsGrounded && !wasGroundedLastFrame && rb != null)
            {
                lastLandingTime = Time.time;
                Landed?.Invoke(rb.linearVelocity);
            }
        }

        // ---------------------------------------------------------------
        // Movement (Quake/Source style acceleration -> gives Titanfall-ish
        // air control and speed-preserving momentum for free)
        // ---------------------------------------------------------------
        private void HandleMovement()
        {
            if (rb == null || settings == null)
                return;

            // During a slide, apply reduced steering instead of full movement.
            // This gives the player subtle directional influence without overriding
            // the slide's own velocity management.
            if (IsSliding)
            {
                ApplySlideSteering();
                return;
            }

            Vector2 rawInput = input != null ? input.MoveInput : Vector2.zero;
            Vector3 wishDir = (orientation.forward * rawInput.y + orientation.right * rawInput.x);
            wishDir.y = 0f;
            wishDir.Normalize();

            float targetSpeed = IsCrouching
                ? settings.crouchSpeed
                : (IsSprinting ? settings.sprintSpeed : settings.walkSpeed);

            Vector3 horizontalVel = HorizontalVelocity;

            if (IsGrounded)
            {
                // reorient velocity onto the slope so you don't lose speed walking up gentle inclines
                if (ground != null)
                    horizontalVel = Vector3.ProjectOnPlane(horizontalVel, ground.GroundNormal);
                horizontalVel = ApplyFriction(horizontalVel);
                horizontalVel = Accelerate(
                    horizontalVel,
                    wishDir,
                    targetSpeed,
                    settings.groundAccelerate
                );
            }
            else
            {
                float airCap = Mathf.Min(targetSpeed, settings.maxAirSpeed);
                horizontalVel = Accelerate(horizontalVel, wishDir, airCap, settings.airAccelerate);
            }

            rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
        }

        /// <summary>Applies gentle camera-relative steering during slides.
        /// Only strafe input is used (not forward/back) so the player can nudge
        /// the slide trajectory without fully overriding the slide direction.</summary>
        private void ApplySlideSteering()
        {
            if (settings.slideSteeringInfluence <= 0f)
                return;

            Vector2 rawInput = input != null ? input.MoveInput : Vector2.zero;
            // Only use strafe (left/right) for steering — forward/back is ignored
            // so the slide's own velocity management stays in control.
            float strafeInput = rawInput.x;
            if (Mathf.Abs(strafeInput) < 0.05f)
                return;

            Vector3 strafeDir = orientation.right * strafeInput;
            strafeDir.y = 0f;
            strafeDir.Normalize();

            Vector3 horizontalVel = HorizontalVelocity;
            float speed = horizontalVel.magnitude;
            if (speed < 0.1f)
                return;

            // Blend a small steering force into the current velocity direction.
            // The force is proportional to current speed so steering feels consistent
            // at different velocities rather than overpowering slow slides.
            float steerForce = speed * settings.slideSteeringInfluence * Time.fixedDeltaTime;
            Vector3 steered = horizontalVel + strafeDir * steerForce;

            // Preserve speed — steering redirects, it doesn't add or remove energy.
            steered = steered.normalized * speed;
            rb.linearVelocity = new Vector3(steered.x, rb.linearVelocity.y, steered.z);
        }

        private Vector3 ApplyFriction(Vector3 velocity)
        {
            if (settings == null)
                return velocity;

            float speed = velocity.magnitude;
            if (speed < 0.001f)
                return Vector3.zero;

            // Reduce friction briefly after landing to preserve momentum from
            // aerial mechanics (wall jump, air dash, etc.) instead of instantly
            // bleeding speed the frame the player touches ground.
            float frictionMult = 1f;
            if (settings.landingFrictionGraceDuration > 0f)
            {
                float timeSinceLanding = Time.time - lastLandingTime;
                if (timeSinceLanding < settings.landingFrictionGraceDuration)
                {
                    float t = timeSinceLanding / settings.landingFrictionGraceDuration;
                    frictionMult = Mathf.Lerp(settings.landingFrictionMinMultiplier, 1f, t);
                }
            }

            float drop = speed * settings.groundFriction * frictionMult * Time.fixedDeltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f) / speed;
            return velocity * newSpeed;
        }

        private Vector3 Accelerate(Vector3 velocity, Vector3 wishDir, float wishSpeed, float accel)
        {
            if (settings == null)
                return velocity;

            float currentSpeedInWishDir = Vector3.Dot(velocity, wishDir);
            float addSpeed = wishSpeed - currentSpeedInWishDir;
            if (addSpeed <= 0f)
                return velocity;

            float accelSpeed = Mathf.Min(accel * wishSpeed * Time.fixedDeltaTime, addSpeed);
            return velocity + wishDir * accelSpeed;
        }

        // ---------------------------------------------------------------
        // Sprint / Stamina
        // ---------------------------------------------------------------
        private void HandleSprintAndStamina()
        {
            bool wantsSprint =
                enableSprint
                && input != null
                && input.SprintHeld
                && input.MoveInput.sqrMagnitude > 0.01f
                && !IsCrouching
                && !IsSliding;

            bool staminaAllows = true;
            if (enableStamina && stamina != null)
                staminaAllows = IsSprinting ? !stamina.Depleted : stamina.CanStartSprint;

            bool sprintingNow = wantsSprint && staminaAllows;

            if (sprintingNow != IsSprinting)
                SprintStateChanged?.Invoke(sprintingNow);

            IsSprinting = sprintingNow;

            if (enableStamina && stamina != null)
            {
                if (IsSprinting)
                    stamina.Drain(Time.fixedDeltaTime);
                else
                    stamina.Regen(Time.fixedDeltaTime);
            }
        }

        // ---------------------------------------------------------------
        // Crouch
        // ---------------------------------------------------------------
        private void HandleCrouch()
        {
            if (capsule == null || settings == null)
                return;

            // Don't allow crouch input during slide (slide overrides)
            if (IsSliding)
                return;

            bool wantsCrouch = enableCrouch && input != null && input.CrouchHeld;

            if (wantsCrouch != IsCrouching)
                CrouchStateChanged?.Invoke(wantsCrouch);

            IsCrouching = wantsCrouch;
            targetHeight = IsCrouching
                ? defaultHeight * settings.crouchHeightMultiplier
                : defaultHeight;

            float newHeight = Mathf.Lerp(
                capsule.height,
                targetHeight,
                Time.fixedDeltaTime * settings.crouchTransitionSpeed
            );
            float heightDelta = newHeight - capsule.height;
            capsule.height = newHeight;
            capsule.center += Vector3.up * (heightDelta * 0.5f);
        }

        // ---------------------------------------------------------------
        // Jump (coyote time + jump buffering, both toggled off automatically
        // if enableJump is false)
        // ---------------------------------------------------------------
        private void HandleJump()
        {
            if (!enableJump || settings == null || rb == null)
                return;

            bool withinCoyote = Time.time - lastGroundedTime <= settings.coyoteTime;
            bool bufferedPress = Time.time - lastJumpPressedTime <= settings.jumpBufferTime;

            if (bufferedPress && withinCoyote && !jumpConsumedThisPress)
            {
                jumpConsumedThisPress = true;
                lastGroundedTime = -999f; // prevent double jump from coyote time

                Vector3 v = rb.linearVelocity;
                
                if (IsSliding && slideController != null && slideController.SlideDuration > 0f)
                {
                    // Gradual ramp: boost scales linearly from 0% at t=0 to 100%
                    // at slideJumpMinDuration. Early jumps get partial benefit;
                    // committing to the full slide earns the full reward.
                    float slideT = Mathf.Clamp01(slideController.SlideDuration / Mathf.Max(0.01f, settings.slideJumpMinDuration));
                    float boost = settings.slideJumpBoost * slideT;

                    Vector3 flatVel = new Vector3(v.x, 0f, v.z);
                    Vector3 forwardDir = orientation.forward;
                    forwardDir.y = 0f;
                    forwardDir.Normalize();
                    
                    flatVel += forwardDir * boost;
                    if (flatVel.magnitude > settings.slideJumpMaxSpeed)
                    {
                        flatVel = flatVel.normalized * settings.slideJumpMaxSpeed;
                    }
                    
                    v.x = flatVel.x;
                    v.z = flatVel.z;
                }
                
                v.y = settings.jumpForce;
                rb.linearVelocity = v;

                Jumped?.Invoke();
            }
        }

        private void ApplyExtraGravity()
        {
            if (rb == null || settings == null)
                return;

            // extra downward acceleration for a snappier, less floaty arc
            rb.AddForce(
                Physics.gravity * (settings.gravityMultiplier - 1f),
                ForceMode.Acceleration
            );
        }

        // Helper for slide controller to get slide height offset
        public Vector3 GetSlideHeightOffset()
        {
            return slideController != null ? slideController.GetSlideHeightOffset() : Vector3.zero;
        }
    }
}
