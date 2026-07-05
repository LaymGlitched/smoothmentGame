using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// All tunable numbers for the controller live here so you can build
    /// multiple "feel" presets (Titanfall-y, ULTRAKILL-y, Dying Light-y, ...)
    /// as separate assets and hot-swap them without touching code.
    /// Right click in Project window -> Create -> FPMovement -> Movement Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "New Movement Settings", menuName = "FPMovement/Movement Settings")]
    public class FPMovementSettings : ScriptableObject
    {
        [Header("Speeds")]
        public float walkSpeed = 6f;
        public float sprintSpeed = 10f;
        public float crouchSpeed = 3.5f;
        public float maxAirSpeed = 10f;

        [Header("Acceleration (Source/Quake style)")]
        [Tooltip("How fast you reach target speed while grounded.")]
        public float groundAccelerate = 14f;

        [Tooltip(
            "How fast you can change direction/speed while airborne. Lower = floatier, more momentum-preserving (Titanfall feel)."
        )]
        public float airAccelerate = 2.5f;

        [Tooltip("How aggressively velocity bleeds off when grounded and not accelerating.")]
        public float groundFriction = 8f;

        [Header("Jumping")]
        public float jumpForce = 7.5f;
        public float gravityMultiplier = 2.5f;

        [Tooltip("Grace period after walking off a ledge where you can still jump.")]
        public float coyoteTime = 0.12f;

        [Tooltip("How early before landing a jump press is still remembered.")]
        public float jumpBufferTime = 0.12f;

        [Header("Landing")]
        [Tooltip("How long after landing before ground friction returns to full strength.")]
        public float landingFrictionGraceDuration = 0.15f;

        [Tooltip("Friction multiplier at the instant of landing (0 = no friction, 1 = full).")]
        [Range(0f, 1f)]
        public float landingFrictionMinMultiplier = 0.3f;

        [Header("Crouching")]
        public float crouchHeightMultiplier = 0.55f;
        public float crouchTransitionSpeed = 10f;

        [Header("Ground Check")]
        public float groundCheckDistance = 0.25f;
        public LayerMask groundMask = ~0;

        [Range(0f, 89f)]
        public float slopeLimit = 50f;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float staminaDrainPerSecond = 18f;
        public float staminaRegenPerSecond = 12f;

        [Tooltip("Delay before regen starts after stamina was last used.")]
        public float staminaRegenDelay = 1f;

        [Tooltip("Minimum stamina required to START a sprint.")]
        public float minStaminaToSprint = 5f;

        [Header("Mouse Look")]
        public float mouseSensitivity = 22f;
        public float lookSmoothTime = 0.03f;
        public float minPitch = -89f;
        public float maxPitch = 89f;

        [Header("Field of View")]
        public float baseFov = 90f;
        public float sprintFovAdd = 8f;
        public float fovLerpSpeed = 8f;

        [Header("Head Bob")]
        public float bobFrequencyWalk = 8f;
        public float bobFrequencySprint = 12f;
        public float bobAmplitude = 0.05f;
        public float bobSmoothSpeed = 10f;

        [Tooltip("How much sideways sway accompanies the vertical bob.")]
        public float bobHorizontalRatio = 0.5f;

        [Header("Landing Bob")]
        [Tooltip("Intensity of the landing bob impact.")]
        public float landingBobAmplitude = 0.15f;

        [Tooltip("How fast the landing bob settles back to neutral.")]
        public float landingBobSettleSpeed = 12f;

        [Tooltip("Minimum landing speed to trigger landing bob.")]
        public float landingBobMinSpeed = 3f;

        [Tooltip("Maximum landing speed for full landing bob effect.")]
        public float landingBobMaxSpeed = 15f;

        [Header("Sliding")]
        [Tooltip("Speed maintained while sliding.")]
        public float slideSpeed = 12f;

        [Tooltip("How quickly you decelerate during a slide.")]
        public float slideDeceleration = 4f;

        [Tooltip("Minimum speed required to initiate a slide.")]
        public float slideMinSpeed = 6f;

        [Tooltip("Minimum time (in seconds) you must be sliding before a jump grants the slide-jump boost (so it only boosts near the end).")]
        public float slideJumpMinDuration = 0.5f;

        [Tooltip("Speed boost applied in the forward direction when jumping out of a slide.")]
        public float slideJumpBoost = 5f;

        [Tooltip("Maximum allowed speed after a slide-jump boost.")]
        public float slideJumpMaxSpeed = 18f;

        [Tooltip("FOV offset while sliding.")]
        public float slideFovAdd = -5f;

        [Tooltip("Camera height offset while sliding (lower = closer to ground).")]
        public float slideHeightOffset = 0.3f;

        [Tooltip("How fast the camera transitions to/from slide height.")]
        public float slideHeightTransitionSpeed = 15f;

        [Tooltip("How much camera-relative strafe input steers the slide direction (0 = locked, 1 = full control).")]
        [Range(0f, 1f)]
        public float slideSteeringInfluence = 0.15f;

        [Tooltip("Proportional drag coefficient for slides. Higher = faster deceleration. Drag is speed-proportional so high-speed slides feel fast and low-speed slides coast out.")]
        public float slideDragFactor = 2f;

        [Tooltip("How long (seconds) the slide survives after briefly leaving the ground (e.g. small bumps, uneven terrain).")]
        public float slideAirborneGrace = 0.15f;

        [Header("Air Dash")]
        [Tooltip("Forward force applied when air dashing.")]
        public float airDashForce = 15f;

        [Tooltip("Slight upward force to fight gravity and give a 'hop' feel.")]
        public float airDashUpwardForce = 2f;

        [Tooltip("Cooldown in seconds before you can dash again.")]
        public float airDashCooldown = 1.2f;

        [Tooltip("Maximum amount of FOV added during the dash.")]
        public float airDashFovAdd = 8f;

        [Tooltip("How fast the FOV kicks in and recovers.")]
        public float airDashFovTransitionSpeed = 12f;

        [Header("Wall Run")]
        public LayerMask wallMask = ~0;

        [Tooltip("How far to the side a wall is detected.")]
        public float wallCheckDistance = 0.7f;

        [Tooltip("Horizontal speed maintained while wall running.")]
        public float wallRunSpeed = 9f;

        [Tooltip("0 = no gravity while wall running (floaty), 1 = full gravity.")]
        [Range(0f, 1f)]
        public float wallRunGravityScale = 0.15f;
        public float maxWallRunDuration = 1.75f;

        [Tooltip("Minimum forward input required to start/continue a wall run.")]
        public float wallRunMinForwardInput = 0.3f;

        [Tooltip("Upward component of a wall jump.")]
        public float wallJumpUpForce = 7f;

        [Tooltip("Push-away-from-wall component of a wall jump.")]
        public float wallJumpAwayForce = 6f;

        [Tooltip("How much forward speed along the wall is preserved during a wall jump (0 = none, 1 = all).")]
        [Range(0f, 1f)]
        public float wallJumpForwardPreservation = 0.75f;

        [Tooltip("How long after leaving a wall before you can wall run on the SAME wall again.")]
        public float wallReattachCooldown = 0.4f;

        [Tooltip("Camera roll angle (degrees) while wall running.")]
        public float wallRunCameraTilt = 6f;
        public float cameraRollLerpSpeed = 8f;

        [Header("Ledge Traversal - Vault / Mantle / Climb")]
        [Tooltip("How far ahead to probe for an obstacle.")]
        public float ledgeCheckDistance = 0.8f;

        [Tooltip(
            "Obstacles at or below this height above the feet are VAULTED (quick hop over), if there's clear space beyond."
        )]
        public float vaultMaxHeight = 1.0f;

        [Tooltip(
            "Obstacles at or below this height (roughly eye level) are MANTLED (grab + pull up)."
        )]
        public float mantleMaxHeight = 1.6f;

        [Tooltip(
            "Obstacles at or below this height (a bit taller than the player) can be WALL CLIMBED up to the top ledge."
        )]
        public float climbMaxHeight = 2.4f;

        [Tooltip("How far past the wall to check for clear landing space to count as a vault.")]
        public float vaultClearanceDistance = 1.0f;

        [Tooltip("How far onto the top surface to land after a vault/mantle/climb.")]
        public float ledgeLandingOffset = 0.5f;
        public float vaultDuration = 0.35f;
        public float mantleDuration = 0.45f;

        [Tooltip(
            "Vertical speed while ascending a tall wall climb, before the final mantle onto the ledge."
        )]
        public float wallClimbSpeed = 4f;

        [Tooltip(
            "Stamina drained per second while wall climbing (ignored if stamina disabled or unassigned)."
        )]
        public float wallClimbStaminaPerSecond = 15f;

        [Tooltip("Cooldown after finishing any ledge traversal before another can start.")]
        public float ledgeTraversalCooldown = 0.25f;

        [Tooltip(
            "If ON, forward speed you had entering a wall climb is restored when you top out (momentum carries through, Titanfall-style). If OFF, you land on the ledge at a standstill (Dying Light-style)."
        )]
        public bool preserveMomentumAfterWallClimb = false;

        [Tooltip(
            "Multiplier applied to the restored speed when preserveMomentumAfterWallClimb is ON. 1 = exact speed you had before climbing."
        )]
        public float wallClimbExitSpeedMultiplier = 1f;

        [Header("State Transitions")]
        [Tooltip("Grace period after external control ends (traversal, wall run) before another external system can claim the player. Prevents accidental state grabs like climb into wall run.")]
        public float stateTransitionGracePeriod = 0.3f;
    }
}
