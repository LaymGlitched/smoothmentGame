using System;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// High-quality, AAA-inspired procedural camera motion system.
    /// Design Philosophy: Grounded, physical, responsive, subtle, and high comfort.
    /// All procedural offsets layer purely additively on bobPivot without fighting raw mouse look input.
    /// </summary>
    public class ProceduralCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ProceduralCameraSettings settings;

        [SerializeField]
        private FPMovementSettings movementSettings;

        [SerializeField]
        private RigidbodyFPController controller;

        [SerializeField]
        private MouseLookController mouseLook;

        [SerializeField]
        private SlideController slideController;

        [SerializeField]
        private WallRunController wallRunController;

        [SerializeField]
        private LedgeTraversalController traversalController;

        [SerializeField]
        [Tooltip("The camera local pivot transform (parent of camera or camera itself) modified by procedural offsets.")]
        private Transform bobPivot;

        // ---- Second-Order Spring Damper Physics Solver ----
        private struct SpringStateVector3
        {
            public Vector3 current;
            public Vector3 velocity;
            public Vector3 target;

            public void Update(float stiffness, float damping, float deltaTime)
            {
                Vector3 force = (target - current) * stiffness - velocity * damping;
                velocity += force * deltaTime;
                current += velocity * deltaTime;
            }

            public void Reset()
            {
                current = Vector3.zero;
                velocity = Vector3.zero;
                target = Vector3.zero;
            }
        }

        private struct SpringStateFloat
        {
            public float current;
            public float velocity;
            public float target;

            public void Update(float stiffness, float damping, float deltaTime)
            {
                float force = (target - current) * stiffness - velocity * damping;
                velocity += force * deltaTime;
                current += velocity * deltaTime;
            }

            public void Reset()
            {
                current = 0f;
                velocity = 0f;
                target = 0f;
            }
        }

        // Springs
        private SpringStateVector3 landingSpringPos;
        private SpringStateFloat landingSpringPitch;
        private SpringStateVector3 damageSpringRot;

        // State trackers & timers
        private Vector3 startLocalPos;
        private Quaternion startLocalRot;

        private float bobTimer;
        private float bobWeight;
        private float idleWeight = 1f;

        // Inertia tracking
        private Vector3 prevVelocity;
        private float prevYaw;
        private float smoothedYawRate;
        private Vector3 currentInertiaPosOffset;
        private Vector3 inertiaPosVelocity;
        private Vector3 currentInertiaRotOffset;
        private Vector3 inertiaRotVelocity;

        // Damage impact micro-jitter shockwave
        private float damageJitterTimer;

        // Traversal / Vault / Mantle state
        private Vector3 traversalPosOffset;
        private Vector3 traversalRotOffset;
        private float traversalTimer;
        private bool isTraversing;
        private float traversalDuration = 0.4f;

        // Speed shake
        private float currentShakeIntensity;

        // Public getters / settings access
        public ProceduralCameraSettings Settings => settings;

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ProceduralCameraSettings>();
            }

            if (controller == null) controller = GetComponentInParent<RigidbodyFPController>();
            if (mouseLook == null) mouseLook = GetComponentInParent<MouseLookController>();
            if (slideController == null) slideController = GetComponentInParent<SlideController>();
            if (wallRunController == null) wallRunController = GetComponentInParent<WallRunController>();
            if (traversalController == null) traversalController = GetComponentInParent<LedgeTraversalController>();

            if (bobPivot == null)
            {
                bobPivot = transform;
            }

            startLocalPos = bobPivot.localPosition;
            startLocalRot = bobPivot.localRotation;
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (controller != null)
            {
                controller.Jumped += OnJumped;
                controller.Landed += OnLanded;
            }

            if (traversalController != null)
            {
                traversalController.VaultStarted += OnVaultStarted;
                traversalController.MantleStarted += OnMantleStarted;
                traversalController.ClimbSmallStarted += OnVaultStarted;
                traversalController.ClimbMediumStarted += OnMantleStarted;
                traversalController.ClimbLargeStarted += OnMantleStarted;
                traversalController.TraversalEnded += OnTraversalEnded;
            }
        }

        private void UnsubscribeEvents()
        {
            if (controller != null)
            {
                controller.Jumped -= OnJumped;
                controller.Landed -= OnLanded;
            }

            if (traversalController != null)
            {
                traversalController.VaultStarted -= OnVaultStarted;
                traversalController.MantleStarted -= OnMantleStarted;
                traversalController.ClimbSmallStarted -= OnVaultStarted;
                traversalController.ClimbMediumStarted -= OnMantleStarted;
                traversalController.ClimbLargeStarted -= OnMantleStarted;
                traversalController.TraversalEnded -= OnTraversalEnded;
            }
        }

        private void OnJumped()
        {
            if (settings == null || !settings.enableSystem || !settings.enableAirDynamics) return;

            landingSpringPitch.velocity -= settings.jumpPopDegrees * 10f;
            landingSpringPos.velocity += Vector3.down * (settings.jumpAnticipationDip * 8f);
        }

        private void OnLanded(Vector3 landingVelocity)
        {
            if (settings == null || !settings.enableSystem || !settings.enableLandingSpring) return;

            float verticalSpeed = Mathf.Abs(landingVelocity.y);
            if (verticalSpeed < settings.landingMinSpeed) return;

            float speedNorm = Mathf.Clamp01(
                (verticalSpeed - settings.landingMinSpeed) / Mathf.Max(0.1f, settings.landingMaxSpeed - settings.landingMinSpeed)
            );

            // Compress vertical displacement spring
            float posImpulse = Mathf.Min(settings.landingMaxDisplacement, speedNorm * settings.landingDisplacementFactor * verticalSpeed);
            landingSpringPos.velocity += Vector3.down * (posImpulse * 35f);

            // Pitch kick spring (head dips forward on impact)
            float pitchImpulse = speedNorm * settings.landingPitchKickDegrees;
            landingSpringPitch.velocity += pitchImpulse * 25f;
        }

        private void OnVaultStarted()
        {
            TriggerTraversalImpulse(settings.vaultPitchDegrees * 0.5f, Vector3.up * (settings.mantleVerticalBump * 0.5f), 0.3f);
        }

        private void OnMantleStarted()
        {
            TriggerTraversalImpulse(settings.vaultPitchDegrees, Vector3.up * settings.mantleVerticalBump, 0.45f);
        }

        private void OnTraversalEnded()
        {
            isTraversing = false;
        }

        private void TriggerTraversalImpulse(float pitchDegrees, Vector3 posBump, float duration)
        {
            if (settings == null || !settings.enableSystem || !settings.enableTraversalMotion) return;
            isTraversing = true;
            traversalTimer = 0f;
            traversalDuration = duration;
            traversalRotOffset = new Vector3(pitchDegrees, 0f, 0f);
            traversalPosOffset = posBump;
        }

        /// <summary>
        /// Public API for damage reactions (e.g. called by Health component).
        /// Triggers instant impact recoil, a 20-80ms shockwave micro-jitter, and rapid spring recovery.
        /// </summary>
        public void TriggerDamageReaction(float damageAmount, Vector3 hitDirection)
        {
            if (settings == null || !settings.enableSystem || !settings.enableDamageImpulse) return;

            float intensity = Mathf.Clamp(damageAmount, 1f, 100f);
            float pitchKick = Mathf.Clamp(intensity * settings.damagePitchFactor, -settings.damageMaxPitchDegrees, settings.damageMaxPitchDegrees);
            float rollKick = intensity * settings.damageRollFactor;

            // Project hit direction to determine roll jerk relative to orientation
            if (controller != null && controller.Orientation != null && hitDirection.sqrMagnitude > 0.01f)
            {
                Vector3 localHit = controller.Orientation.InverseTransformDirection(hitDirection);
                rollKick *= Mathf.Sign(localHit.x != 0 ? localHit.x : 1f);
            }

            // Crisp, punchy hit impulse kick
            damageSpringRot.velocity += new Vector3(-pitchKick * 45f, 0f, rollKick * 45f);

            // Trigger 20-80ms high-frequency shockwave micro-jitter
            damageJitterTimer = settings.damageJitterDuration;
        }

        /// <summary>
        /// Compatibility method for existing calls to HeadBobEffect.TriggerLandingBob.
        /// </summary>
        public void TriggerLandingBob(float intensityMultiplier = 1f)
        {
            if (settings == null || !settings.enableSystem || !settings.enableLandingSpring) return;
            float speed = settings.landingMaxSpeed * Mathf.Clamp01(intensityMultiplier);
            OnLanded(new Vector3(0f, -speed, 0f));
        }

        private void Update()
        {
            if (settings == null || !settings.enableSystem || bobPivot == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Evaluate Physics Springs
            UpdateSprings(dt);

            // 2. Accumulate Offset Vectors
            Vector3 targetPosOffset = Vector3.zero;
            Vector3 targetRotOffset = Vector3.zero;

            // Movement state values
            float speed = controller != null ? controller.CurrentSpeed : 0f;
            bool isGrounded = controller != null && controller.IsGrounded;
            bool isSprinting = controller != null && controller.IsSprinting;
            bool isSliding = controller != null && controller.IsSliding;
            bool isWallRunning = wallRunController != null && wallRunController.IsWallRunning;

            // A. Idle Breathing & Sway (Subtle)
            if (settings.enableIdleMotion)
            {
                float targetIdleWeight = (speed < 0.2f && isGrounded && !isSliding) ? 1f : 0f;
                idleWeight = Mathf.Lerp(idleWeight, targetIdleWeight, dt * 4f);

                if (idleWeight > 0.001f)
                {
                    float idleTime = Time.time * settings.idleFrequency;
                    float idleY = Mathf.Sin(idleTime) * settings.idlePosAmplitude * idleWeight;
                    float idleX = Mathf.Cos(idleTime * 0.5f) * settings.idlePosAmplitude * settings.idleHorizontalRatio * idleWeight;

                    float noiseTime = Time.time * settings.idleNoiseSpeed;
                    float pitchDrift = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 2f * settings.idleRotDriftDegrees * idleWeight;
                    float rollDrift = (Mathf.PerlinNoise(0f, noiseTime) - 0.5f) * 2f * settings.idleRotDriftDegrees * idleWeight;

                    targetPosOffset += new Vector3(idleX, idleY, 0f);
                    targetRotOffset += new Vector3(pitchDrift, 0f, rollDrift);
                }
            }

            // B. Movement Head Bob (Walk / Sprint)
            if (settings.enableHeadBob)
            {
                bool isMovingGround = isGrounded && speed > 0.3f && !isSliding;
                float targetBobWeight = isMovingGround ? 1f : 0f;
                bobWeight = Mathf.MoveTowards(bobWeight, targetBobWeight, dt * settings.bobSmoothSpeed);

                if (bobWeight > 0.001f)
                {
                    float freq = isSprinting ? settings.bobFrequencySprint : settings.bobFrequencyWalk;
                    float amp = (isSprinting ? settings.bobAmplitudeWalk * settings.sprintBobMultiplier : settings.bobAmplitudeWalk) * bobWeight;

                    bobTimer += dt * freq;
                    if (bobTimer > Mathf.PI * 2f) bobTimer -= Mathf.PI * 2f;

                    // Lissajous figure-8 pattern
                    float bobY = Mathf.Sin(bobTimer) * amp;
                    float bobX = Mathf.Cos(bobTimer * 0.5f) * amp * settings.bobHorizontalRatio;
                    float stepPitch = Mathf.Sin(bobTimer) * settings.bobPitchTiltDegrees * bobWeight;

                    targetPosOffset += new Vector3(bobX, Mathf.Abs(bobY), 0f);
                    targetRotOffset += new Vector3(stepPitch, 0f, 0f);
                }
                else
                {
                    bobTimer = 0f;
                }
            }

            // C. Inertia & Directional Leaning (Ultra-Subtle)
            if (settings.enableInertia && controller != null)
            {
                Vector3 currentVel = controller.Body != null ? controller.Body.linearVelocity : Vector3.zero;
                Vector3 accel = (currentVel - prevVelocity) / dt;
                prevVelocity = currentVel;

                // Forward acceleration pitch (backward on speed up, forward on braking)
                Vector3 localAccel = controller.Orientation != null ? controller.Orientation.InverseTransformDirection(accel) : Vector3.zero;
                float targetPitchInertia = Mathf.Clamp(-localAccel.z * 0.02f * settings.accelPitchDegrees, -1f, 1f);

                // Strafe roll & horizontal position shift
                Vector2 moveInput = controller.Input != null ? controller.Input.MoveInput : Vector2.zero;
                float targetStrafeRoll = -moveInput.x * settings.strafeRollDegrees;
                float targetStrafePos = moveInput.x * settings.strafePosShift;

                // Yaw turn rate lean (extremely subtle to avoid rubber-neck feel)
                float currentYaw = controller.Orientation != null ? controller.Orientation.eulerAngles.y : 0f;
                float yawDelta = Mathf.DeltaAngle(prevYaw, currentYaw) / dt;
                prevYaw = currentYaw;
                smoothedYawRate = Mathf.Lerp(smoothedYawRate, yawDelta, dt * 10f);
                float targetTurnRoll = Mathf.Clamp(-smoothedYawRate * settings.turnRollDegrees, -1.2f, 1.2f);

                Vector3 targetInertiaRot = new Vector3(targetPitchInertia, 0f, targetStrafeRoll + targetTurnRoll);
                Vector3 targetInertiaPos = new Vector3(targetStrafePos, 0f, 0f);

                currentInertiaRotOffset = Vector3.SmoothDamp(currentInertiaRotOffset, targetInertiaRot, ref inertiaRotVelocity, settings.inertiaSmoothTime);
                currentInertiaPosOffset = Vector3.SmoothDamp(currentInertiaPosOffset, targetInertiaPos, ref inertiaPosVelocity, settings.inertiaSmoothTime);

                targetRotOffset += currentInertiaRotOffset;
                targetPosOffset += currentInertiaPosOffset;
            }

            // D. Airborne Dynamics & Fall Wind
            if (settings.enableAirDynamics && controller != null && !isGrounded && !isWallRunning)
            {
                float verticalVel = controller.Body != null ? controller.Body.linearVelocity.y : 0f;

                // Air strafe roll
                Vector2 moveInput = controller.Input != null ? controller.Input.MoveInput : Vector2.zero;
                float airStrafeRoll = -moveInput.x * settings.airStrafeRollDegrees;
                targetRotOffset.z += airStrafeRoll;

                // High fall tilt & wind turbulence
                if (verticalVel < -3f)
                {
                    float fallNorm = Mathf.Clamp01(Mathf.Abs(verticalVel) / settings.maxFallSpeedThreshold);
                    float fallPitch = fallNorm * settings.fallTiltMaxDegrees;
                    targetRotOffset.x += fallPitch;

                    float windTime = Time.time * settings.fallWindFrequency;
                    float windRoll = (Mathf.PerlinNoise(windTime, 0f) - 0.5f) * 2f * settings.fallWindRotAmplitude * fallNorm;
                    float windPitch = (Mathf.PerlinNoise(0f, windTime) - 0.5f) * 2f * settings.fallWindRotAmplitude * fallNorm;

                    targetRotOffset += new Vector3(windPitch, 0f, windRoll);
                }
            }

            // E. Slide Motion
            if (settings.enableSlideMotion && isSliding)
            {
                targetRotOffset.x += settings.slideForwardLeanDegrees;

                // Slide steering roll tilt
                Vector2 moveInput = controller.Input != null ? controller.Input.MoveInput : Vector2.zero;
                targetRotOffset.z += -moveInput.x * settings.slideSteerRollDegrees;

                // Slide grinding noise
                if (speed > 5f)
                {
                    float grindTime = Time.time * 30f;
                    float grindNoiseX = (Mathf.PerlinNoise(grindTime, 0f) - 0.5f) * settings.slideGrindNoisePos;
                    float grindNoiseY = (Mathf.PerlinNoise(0f, grindTime) - 0.5f) * settings.slideGrindNoisePos;
                    float grindRotZ = (Mathf.PerlinNoise(grindTime, grindTime) - 0.5f) * settings.slideGrindNoiseRot;

                    targetPosOffset += new Vector3(grindNoiseX, grindNoiseY, 0f);
                    targetRotOffset.z += grindRotZ;
                }
            }

            // F. Ledge Traversal Curve
            if (isTraversing)
            {
                traversalTimer += dt;
                float t = Mathf.Clamp01(traversalTimer / traversalDuration);

                // Smooth sinusoidal pop and decay curve
                float ease = Mathf.Sin(t * Mathf.PI);
                targetPosOffset += traversalPosOffset * ease;
                targetRotOffset += traversalRotOffset * ease;

                if (t >= 1f) isTraversing = false;
            }

            // G. Restored High Speed Shake Baseline (From HeadBobEffect)
            if (settings.enableSpeedShake)
            {
                float targetShake = 0f;
                if (speed >= settings.speedShakeMinSpeed)
                {
                    targetShake = Mathf.Clamp01((speed - settings.speedShakeMinSpeed) / Mathf.Max(0.1f, settings.speedShakeMaxSpeed - settings.speedShakeMinSpeed));
                }
                currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, targetShake, dt * 8f);

                if (currentShakeIntensity > 0.001f)
                {
                    float timeX = Time.time * settings.speedShakeFrequency;
                    float timeY = timeX + 100f;
                    float timeZ = timeX + 200f;

                    Vector3 shakePos = new Vector3(
                        (Mathf.PerlinNoise(timeX, 0f) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(0f, timeY) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(timeZ, timeZ) - 0.5f) * 2f
                    ) * (settings.speedShakePosAmplitude * currentShakeIntensity);

                    Vector3 shakeRot = new Vector3(
                        (Mathf.PerlinNoise(timeX + 300f, 0f) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(0f, timeY + 300f) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(timeZ + 300f, timeZ + 300f) - 0.5f) * 2f
                    ) * (settings.speedShakeRotAmplitude * currentShakeIntensity);

                    targetPosOffset += shakePos;
                    targetRotOffset += shakeRot;
                }
            }

            // H. Damage Shockwave Micro-Jitter (20-80ms Crisp High Frequency Jitter)
            if (damageJitterTimer > 0f)
            {
                damageJitterTimer -= dt;
                float jitterX = (UnityEngine.Random.value - 0.5f) * 2f * settings.damageJitterAmplitude;
                float jitterY = (UnityEngine.Random.value - 0.5f) * 2f * settings.damageJitterAmplitude;
                targetPosOffset += new Vector3(jitterX, jitterY, 0f);
            }

            // I. Combine Spring Physics Offsets
            targetPosOffset += landingSpringPos.current;
            targetRotOffset += new Vector3(landingSpringPitch.current, 0f, 0f);
            targetRotOffset += damageSpringRot.current;

            // 3. Apply Purely Additively to Bob Pivot Transform
            bobPivot.localPosition = startLocalPos + targetPosOffset;
            bobPivot.localRotation = startLocalRot * Quaternion.Euler(targetRotOffset);
        }

        private void UpdateSprings(float dt)
        {
            if (settings == null) return;

            // Landing positional & pitch springs
            landingSpringPos.target = Vector3.zero;
            landingSpringPos.Update(settings.landingSpringStiffness, settings.landingSpringDamping, dt);

            landingSpringPitch.target = 0f;
            landingSpringPitch.Update(settings.landingSpringStiffness, settings.landingSpringDamping, dt);

            // Damage recoil spring (ultra-snappy recovery)
            damageSpringRot.target = Vector3.zero;
            damageSpringRot.Update(settings.damageSpringStiffness, settings.damageSpringDamping, dt);
        }
    }
}
