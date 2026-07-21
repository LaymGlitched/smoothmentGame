using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Configuration asset for the AAA-inspired Procedural Camera Motion System.
    /// Exposes comprehensive, modular serialized settings tuned for subtle, physical, high-comfort camera motion.
    /// </summary>
    [CreateAssetMenu(fileName = "New Camera Motion Settings", menuName = "FPMovement/Procedural Camera Settings")]
    public class ProceduralCameraSettings : ScriptableObject
    {
        [Header("--- Master Toggles ---")]
        [Tooltip("Master toggle for all procedural camera motion effects.")]
        public bool enableSystem = true;

        [Header("--- Idle Breathing & Sway ---")]
        [Tooltip("Enable subtle breathing motion and rotational drift when standing still.")]
        public bool enableIdleMotion = true;
        [Tooltip("Frequency of idle breathing oscillation.")]
        public float idleFrequency = 1.2f;
        [Tooltip("Vertical positional sway amplitude during idle.")]
        public float idlePosAmplitude = 0.005f;
        [Tooltip("Horizontal Lissajous sway ratio relative to vertical sway.")]
        public float idleHorizontalRatio = 0.5f;
        [Tooltip("Maximum rotational drift angle (degrees) for idle breathing.")]
        public float idleRotDriftDegrees = 0.1f;
        [Tooltip("Perlin noise frequency for subtle random idle camera drift.")]
        public float idleNoiseSpeed = 0.4f;

        [Header("--- Walk & Sprint Head Bob ---")]
        [Tooltip("Enable movement-driven procedural head bobbing.")]
        public bool enableHeadBob = true;
        [Tooltip("Bob frequency while walking.")]
        public float bobFrequencyWalk = 7.5f;
        [Tooltip("Bob frequency while sprinting.")]
        public float bobFrequencySprint = 11.5f;
        [Tooltip("Vertical bob amplitude while walking.")]
        public float bobAmplitudeWalk = 0.018f;
        [Tooltip("Vertical bob amplitude multiplier when sprinting.")]
        public float sprintBobMultiplier = 1.25f;
        [Tooltip("Horizontal Lissajous sway ratio (0.5 = natural figure-8).")]
        [Range(0.1f, 1.0f)]
        public float bobHorizontalRatio = 0.5f;
        [Tooltip("Pitch tilt angle accompanying step footfalls.")]
        public float bobPitchTiltDegrees = 0.12f;
        [Tooltip("How fast the bob position blends into and out of motion.")]
        public float bobSmoothSpeed = 12f;

        [Header("--- Camera Inertia & Directional Leaning (Extremely Subtle) ---")]
        [Tooltip("Enable velocity-based acceleration lag and turning tilt.")]
        public bool enableInertia = true;
        [Tooltip("Camera pitch tilt when accelerating forward (sags backward) or braking (pitches forward).")]
        public float accelPitchDegrees = 0.2f;
        [Tooltip("Maximum camera roll angle when strafing sideways (A/D).")]
        public float strafeRollDegrees = 0.5f;
        [Tooltip("Camera roll tilt when panning yaw quickly (leaning into sharp turns).")]
        public float turnRollDegrees = 0.012f;
        [Tooltip("Camera lateral position shift when strafing.")]
        public float strafePosShift = 0.005f;
        [Tooltip("Smooth time for inertia transitions.")]
        public float inertiaSmoothTime = 0.12f;

        [Header("--- Jump & Air Dynamics ---")]
        [Tooltip("Enable jump takeoff impulse and airborne strafe/fall reactions.")]
        public bool enableAirDynamics = true;
        [Tooltip("Downward anticipation pitch dip prior to jump takeoff.")]
        public float jumpAnticipationDip = 0.015f;
        [Tooltip("Upward pop impulse angle on jump takeoff.")]
        public float jumpPopDegrees = 0.8f;
        [Tooltip("Camera roll tilt angle when strafing in mid-air.")]
        public float airStrafeRollDegrees = 0.8f;
        [Tooltip("Maximum downward pitch tilt when falling at high vertical speed.")]
        public float fallTiltMaxDegrees = 1.2f;
        [Tooltip("Vertical fall speed required to reach maximum fall pitch tilt.")]
        public float maxFallSpeedThreshold = 25f;
        [Tooltip("Subtle wind turbulence wobble frequency during long falls.")]
        public float fallWindFrequency = 16f;
        [Tooltip("Subtle wind turbulence rotational amplitude during falls.")]
        public float fallWindRotAmplitude = 0.2f;

        [Header("--- Landing Impact Compression ---")]
        [Tooltip("Enable spring-damped landing impact compression.")]
        public bool enableLandingSpring = true;
        [Tooltip("Spring stiffness for landing impact recovery (higher = snappier).")]
        public float landingSpringStiffness = 260f;
        [Tooltip("Spring damping ratio (1.0 = critical damping, < 1.0 = bouncier).")]
        public float landingSpringDamping = 24f;
        [Tooltip("Vertical downward displacement factor per unit of landing speed.")]
        public float landingDisplacementFactor = 0.012f;
        [Tooltip("Maximum vertical compression distance on landing.")]
        public float landingMaxDisplacement = 0.1f;
        [Tooltip("Forward pitch kick angle on heavy landings.")]
        public float landingPitchKickDegrees = 2.0f;
        [Tooltip("Minimum vertical landing speed to trigger landing compression.")]
        public float landingMinSpeed = 3.0f;
        [Tooltip("Landing speed for maximum compression effect.")]
        public float landingMaxSpeed = 20.0f;

        [Header("--- Slide Dynamics ---")]
        [Tooltip("Enable sliding camera effects (forward lean, steering tilt, grinding noise).")]
        public bool enableSlideMotion = true;
        [Tooltip("Forward pitch lean angle during a slide.")]
        public float slideForwardLeanDegrees = 1.5f;
        [Tooltip("Camera roll tilt when steering left/right during a slide.")]
        public float slideSteerRollDegrees = 1.2f;
        [Tooltip("High-frequency low-amplitude grinding noise during fast slides.")]
        public float slideGrindNoisePos = 0.006f;
        [Tooltip("Rotational grinding noise during fast slides (degrees).")]
        public float slideGrindNoiseRot = 0.2f;

        [Header("--- Wall Running & Traversal ---")]
        [Tooltip("Enable wall run camera roll and ledge traversal weight shifts.")]
        public bool enableTraversalMotion = true;
        [Tooltip("Camera roll angle while wall running.")]
        public float wallRunRollDegrees = 6.0f;
        [Tooltip("Pitch kick during vaulting over low obstacles.")]
        public float vaultPitchDegrees = 1.5f;
        [Tooltip("Vertical displacement bump during mantling / wall climbing.")]
        public float mantleVerticalBump = 0.04f;
        [Tooltip("Transition speed for wall run and traversal roll/pitch.")]
        public float traversalSmoothSpeed = 10f;

        [Header("--- Damage Impulse Reaction (Punchy & Crisp) ---")]
        [Tooltip("Enable directional damage impact reaction.")]
        public bool enableDamageImpulse = true;
        [Tooltip("Pitch recoil angle per unit of damage received.")]
        public float damagePitchFactor = 0.25f;
        [Tooltip("Roll tilt angle per unit of damage received.")]
        public float damageRollFactor = 0.35f;
        [Tooltip("Maximum pitch angle cap for damage reactions.")]
        public float damageMaxPitchDegrees = 6.0f;
        [Tooltip("Spring stiffness for damage recovery (higher = snappier return).")]
        public float damageSpringStiffness = 350f;
        [Tooltip("Spring damping ratio for damage recovery.")]
        public float damageSpringDamping = 28f;
        [Tooltip("Duration in seconds for high-frequency shockwave micro-jitter (20-80ms).")]
        public float damageJitterDuration = 0.05f;
        [Tooltip("Amplitude of high-frequency shockwave micro-jitter.")]
        public float damageJitterAmplitude = 0.015f;

        [Header("--- High Speed Shake (Restored Baseline) ---")]
        [Tooltip("Enable velocity-driven high-speed Perlin camera shake.")]
        public bool enableSpeedShake = true;
        [Tooltip("Minimum speed to initiate high speed camera shake.")]
        public float speedShakeMinSpeed = 14f;
        [Tooltip("Speed at which high speed shake reaches maximum intensity.")]
        public float speedShakeMaxSpeed = 28f;
        [Tooltip("Maximum positional shake amplitude at max speed.")]
        public float speedShakePosAmplitude = 0.025f;
        [Tooltip("Maximum rotational shake amplitude at max speed (degrees).")]
        public float speedShakeRotAmplitude = 0.3f;
        [Tooltip("Oscillation frequency for high speed noise.")]
        public float speedShakeFrequency = 25f;
    }
}
