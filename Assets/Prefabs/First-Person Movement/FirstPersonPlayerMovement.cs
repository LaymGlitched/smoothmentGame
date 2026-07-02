// © 2025 Nanodogs Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nanodogs.UniversalScripts
{
    [RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
    public class FirstPersonPlayerMovement : NanoMovementBase
    {
        // ─── Input Actions ──────────────────────────────────────────────────────
        [Header("Input Actions")]
        public InputActionReference moveAction;
        public InputActionReference jumpAction;
        public InputActionReference dashAction;

        private Vector3 wishDir;
        private bool isGrounded;

        // ─── Movement ───────────────────────────────────────────────────────────
        [Header("Movement")]
        [Tooltip("Top speed on the ground (m/s).")]
        public float maxSpeed = 10f;

        [Tooltip(
            "Ground acceleration. Higher = reach max speed faster.\n"
                + "Rule of thumb: keep this higher than groundFriction. (10–30)"
        )]
        public float groundAcceleration = 15f;

        [Tooltip(
            "Ground friction / stopping strength. Higher = quicker stop.\n"
                + "Must be lower than groundAcceleration or you'll never reach maxSpeed. (6–12)"
        )]
        public float groundFriction = 8f;

        [Tooltip("How much directional control you have in the air. (5–15)")]
        public float airAcceleration = 8f;

        [Tooltip(
            "Max horizontal speed you can *gain* per second in the air, as a fraction of maxSpeed.\n"
                + "0.1 = Quake-tight, 0.3 = casual, 1.0 = full ground-level control."
        )]
        [Range(0f, 1f)]
        public float airSpeedCap = 0.3f;

        // ─── Jump ───────────────────────────────────────────────────────────────
        [Header("Jump")]
        public float jumpCooldown = 0.15f;

        [Tooltip("Grace window (sec) after pressing jump where it still fires when you land.")]
        public float jumpBufferTime = 0.12f;

        [Tooltip("Grace window (sec) after walking off a ledge where you can still jump.")]
        public float coyoteTime = 0.12f;

        private float lastJumpTime = -10f;
        private float lastJumpPressTime = -10f;
        private float lastGroundedTime = -10f;

        // ─── Dash ───────────────────────────────────────────────────────────────
        [Header("Dash")]
        public float dashForce = 20f;
        public float dashDuration = 0.15f;
        public float dashCooldown = 1.0f;

        [Tooltip("Allow dashing while grounded.")]
        public bool allowGroundDash = false;

        private bool isDashing;
        private float dashTimer;
        private float dashCooldownTimer;
        private Vector3 dashDirection;

        // ─── Ladder ─────────────────────────────────────────────────────────────
        [Header("Ladder")]
        public bool onLadder = false;
        public float ladderClimbSpeed = 5f;
        public float ladderStickStrength = 10f;
        public float ladderPushOffForce = 6f;

        private Vector3 ladderForward;
        private Vector3 ladderCenter;

        // ─── Footsteps ──────────────────────────────────────────────────────────
        [Header("Footsteps")]
        public float footstepInterval = 0.4f;
        public float footstepRayDistance = 1.2f;
        public LayerMask footstepLayerMask;
        public List<FootstepSurface> footstepSurfaces = new();

        private AudioSource audioSource;
        private float footstepTimer;

        // ────────────────────────────────────────────────────────────────────────
        // Unity messages
        // ────────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            moveAction?.action.Enable();
            jumpAction?.action.Enable();
            dashAction?.action.Enable();
        }

        private void OnDisable()
        {
            moveAction?.action.Disable();
            jumpAction?.action.Disable();
            dashAction?.action.Disable();
        }

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            rb.useGravity = true;
        }

        private void Update()
        {
            if (moveAction == null || jumpAction == null)
                return;

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();

            // Camera-relative, flat wish direction
            Transform cam = Camera.main.transform;
            Vector3 camForward = FlattenNormalize(cam.forward);
            Vector3 camRight = FlattenNormalize(cam.right);

            wishDir = camRight * moveInput.x + camForward * moveInput.y;
            if (wishDir.sqrMagnitude > 1f)
                wishDir.Normalize();

            // Grounded state + coyote tracking
            isGrounded = IsGrounded();
            if (isGrounded)
                lastGroundedTime = Time.time;

            // Buffer jump presses — actual execution happens in FixedUpdate
            if (jumpAction.action.WasPressedThisFrame() && !onLadder)
                lastJumpPressTime = Time.time;

            HandleDash();
            HandleLadder(moveInput);
            HandleFootsteps(moveInput);
        }

        private void FixedUpdate()
        {
            if (onLadder)
                return;
            if (isDashing)
            {
                HandleDashMovement();
                return;
            }

            // Jump BEFORE friction so landing never kills bhop momentum —
            // if you jump the same physics frame you land, GroundMove never runs.
            bool jumped = TryJump();

            if (isGrounded && !jumped)
                GroundMove();
            else if (!isGrounded)
                AirMove();
        }

        // ────────────────────────────────────────────────────────────────────────
        // Jump
        // ────────────────────────────────────────────────────────────────────────

        private bool TryJump()
        {
            bool jumpBuffered = Time.time - lastJumpPressTime <= jumpBufferTime;
            bool coyoteOk = Time.time - lastGroundedTime <= coyoteTime;
            bool cooldownOk = Time.time - lastJumpTime >= jumpCooldown;

            if (!jumpBuffered || !coyoteOk || !cooldownOk)
                return false;

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            lastJumpTime = Time.time;
            lastJumpPressTime = -10f; // consume buffer so we don't double-jump
            return true;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Ground / Air movement — Quake-style
        // ────────────────────────────────────────────────────────────────────────

        private void GroundMove()
        {
            // Quake order: friction first, then accelerate.
            // The dot-product check inside QuakeAccelerate guarantees friction and
            // acceleration reach equilibrium exactly at maxSpeed, so the two values
            // no longer fight each other — they have independent, predictable effects.
            ApplyFriction(groundFriction);
            QuakeAccelerate(wishDir, maxSpeed, groundAcceleration);
        }

        private void AirMove()
        {
            // No friction in the air — momentum is preserved.
            // airSpeedCap limits how much lateral speed you can gain per frame,
            // giving directional influence without letting you teleport sideways.
            float airWishSpeed = Mathf.Min(wishDir.magnitude * maxSpeed, maxSpeed * airSpeedCap);
            QuakeAccelerate(wishDir, airWishSpeed, airAcceleration);
        }

        /// <summary>
        /// Adds velocity toward <paramref name="wishDir"/> up to <paramref name="wishSpeed"/>.
        ///
        /// The dot-product check (currentSpeed) measures how fast we're already moving
        /// in the wish direction. We only add the *difference*, so we can never overshoot
        /// the cap no matter how high <paramref name="accel"/> is set — values stay sane.
        /// </summary>
        private void QuakeAccelerate(Vector3 wishDir, float wishSpeed, float accel)
        {
            if (wishDir.sqrMagnitude < 0.0001f || wishSpeed < 0.001f)
                return;

            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 wishDirNorm = wishDir.normalized;

            float currentSpeed = Vector3.Dot(flatVel, wishDirNorm); // speed already in wish direction
            float addSpeed = wishSpeed - currentSpeed; // headroom before we hit the cap
            if (addSpeed <= 0f)
                return;

            float accelSpeed = Mathf.Min(accel * wishSpeed * Time.fixedDeltaTime, addSpeed);

            rb.linearVelocity += new Vector3(
                wishDirNorm.x * accelSpeed,
                0f,
                wishDirNorm.z * accelSpeed
            );
        }

        /// <summary>
        /// Linearly drags horizontal velocity by <c>friction × speed × dt</c> each physics step.
        /// Unlike exponential drag this gives consistent, frame-rate-stable stopping distances.
        /// </summary>
        private void ApplyFriction(float friction)
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float speed = flatVel.magnitude;
            if (speed < 0.001f)
                return;

            float drop = speed * friction * Time.fixedDeltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f);
            float scale = newSpeed / speed;

            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x * scale,
                rb.linearVelocity.y,
                rb.linearVelocity.z * scale
            );
        }

        // ────────────────────────────────────────────────────────────────────────
        // Dash
        // ────────────────────────────────────────────────────────────────────────

        private void HandleDash()
        {
            if (dashCooldownTimer > 0f)
                dashCooldownTimer -= Time.deltaTime;

            if (isDashing)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    isDashing = false;
                    rb.useGravity = true;
                }
                return;
            }

            if (dashAction == null || !dashAction.action.WasPressedThisFrame())
                return;
            if (dashCooldownTimer > 0f || onLadder)
                return;
            if (isGrounded && !allowGroundDash)
                return;

            Transform cam = Camera.main.transform;
            Vector2 input = moveAction.action.ReadValue<Vector2>();
            Vector3 inputDir =
                FlattenNormalize(cam.right) * input.x + FlattenNormalize(cam.forward) * input.y;
            if (inputDir.sqrMagnitude > 1f)
                inputDir.Normalize();

            dashDirection =
                inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : FlattenNormalize(cam.forward);
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }

        private void HandleDashMovement()
        {
            rb.linearVelocity = new Vector3(
                dashDirection.x * dashForce,
                Mathf.Max(0f, rb.linearVelocity.y),
                dashDirection.z * dashForce
            );
        }

        // ────────────────────────────────────────────────────────────────────────
        // Ladder
        // ────────────────────────────────────────────────────────────────────────

        private void HandleLadder(Vector2 moveInput)
        {
            if (!onLadder)
                return;

            rb.linearVelocity = Vector3.up * moveInput.y * ladderClimbSpeed;

            Vector3 toCenter = ladderCenter - transform.position;
            toCenter.y = 0f;
            rb.AddForce(toCenter * ladderStickStrength, ForceMode.Acceleration);

            if (jumpAction.action.WasPressedThisFrame())
            {
                onLadder = false;
                rb.useGravity = true;
                rb.AddForce(
                    -ladderForward.normalized * ladderPushOffForce + Vector3.up * 2f,
                    ForceMode.VelocityChange
                );
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // Footsteps
        // ────────────────────────────────────────────────────────────────────────

        private void HandleFootsteps(Vector2 moveInput)
        {
            if (!isGrounded || onLadder)
                return;

            bool moving = moveInput.sqrMagnitude > 0.01f && rb.linearVelocity.sqrMagnitude > 0.25f;
            if (!moving)
            {
                footstepTimer = 0f;
                return;
            }

            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepInterval)
            {
                footstepTimer = 0f;
                PlayFootstepSound();
            }
        }

        private void PlayFootstepSound()
        {
            if (
                Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    out RaycastHit hit,
                    footstepRayDistance,
                    footstepLayerMask
                )
            )
            {
                AudioClip clip = GetClipForSurface(hit.collider.tag);
                if (clip != null)
                    audioSource.PlayOneShot(clip);
            }
        }

        private AudioClip GetClipForSurface(string tag)
        {
            foreach (var surface in footstepSurfaces)
            {
                if (
                    surface.tag.Equals(tag, System.StringComparison.OrdinalIgnoreCase)
                    && surface.clips.Length > 0
                )
                    return surface.clips[Random.Range(0, surface.clips.Length)];
            }
            return null;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Utility
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>Zero out Y then normalize — used to flatten camera vectors to the horizontal plane.</summary>
        private static Vector3 FlattenNormalize(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        public void SetLadderData(Vector3 forward, Vector3 center)
        {
            ladderForward = forward;
            ladderCenter = center;
        }

        public bool IsDashing => isDashing;
        public bool PlayerGrounded => isGrounded; // NOTE: renamed from IsGrounded to avoid shadowing the base class method
    }

    [System.Serializable]
    public class FootstepSurface
    {
        public string tag;
        public AudioClip[] clips;
    }
}
