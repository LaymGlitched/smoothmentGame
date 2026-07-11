using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Procedural sine-wave head bob driven by the controller's speed/grounded
    /// state. Also handles landing impact bob. Purely additive - it offsets a
    /// local "bob pivot" transform that should sit between orientation and the
    /// camera, so it never fights with MouseLookController's rotation.
    /// </summary>
    public class HeadBobEffect : MonoBehaviour
    {
        [SerializeField]
        private FPMovementSettings settings;

        [SerializeField]
        private RigidbodyFPController controller;

        [SerializeField]
        private Transform bobPivot; // local transform, parent of the camera

        [Header("Feature Toggles")]
        public bool enableHeadBob = true;
        public bool enableLandingBob = true;

        [Header("Landing Bob Overrides")]
        [Tooltip("Override settings for landing bob amplitude (0 = use settings value).")]
        public float landingBobAmplitudeOverride = 0f;

        [Tooltip("Override settings for landing bob settle speed (0 = use settings value).")]
        public float landingBobSettleSpeedOverride = 0f;

        [Tooltip("Override settings for landing bob min speed (0 = use settings value).")]
        public float landingBobMinSpeedOverride = 0f;

        [Tooltip("Override settings for landing bob max speed (0 = use settings value).")]
        public float landingBobMaxSpeedOverride = 0f;

        private Vector3 startLocalPos;
        private Quaternion startLocalRot;
        private Vector3 currentSmoothedPos;
        private float currentShakeIntensity;
        private float bobTimer;
        private float landingBobTimer;
        private float landingBobIntensity;
        private bool isLandingBobActive;

        // Cached values
        private float landingBobAmplitude;
        private float landingBobSettleSpeed;
        private float landingBobMinSpeed;
        private float landingBobMaxSpeed;

        private void Awake()
        {
            startLocalPos = bobPivot.localPosition;
            startLocalRot = bobPivot.localRotation;
            currentSmoothedPos = startLocalPos;

            // Cache values with override support
            landingBobAmplitude =
                landingBobAmplitudeOverride > 0f
                    ? landingBobAmplitudeOverride
                    : settings.landingBobAmplitude;
            landingBobSettleSpeed =
                landingBobSettleSpeedOverride > 0f
                    ? landingBobSettleSpeedOverride
                    : settings.landingBobSettleSpeed;
            landingBobMinSpeed =
                landingBobMinSpeedOverride > 0f
                    ? landingBobMinSpeedOverride
                    : settings.landingBobMinSpeed;
            landingBobMaxSpeed =
                landingBobMaxSpeedOverride > 0f
                    ? landingBobMaxSpeedOverride
                    : settings.landingBobMaxSpeed;
        }

        private void OnEnable()
        {
            if (controller != null)
                controller.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.Landed -= OnLanded;
        }

        private void OnLanded(Vector3 landingVelocity)
        {
            if (!enableLandingBob)
                return;

            float speed = landingVelocity.magnitude;
            if (speed < landingBobMinSpeed)
                return;

            // Calculate intensity based on landing speed
            float normalizedSpeed = Mathf.Clamp01(
                (speed - landingBobMinSpeed) / (landingBobMaxSpeed - landingBobMinSpeed)
            );
            landingBobIntensity = normalizedSpeed * landingBobAmplitude;
            landingBobTimer = 0f;
            isLandingBobActive = true;
        }

        private void Update()
        {
            if (!enableHeadBob)
            {
                currentSmoothedPos = Vector3.Lerp(
                    currentSmoothedPos,
                    startLocalPos,
                    Time.deltaTime * settings.bobSmoothSpeed
                );
                currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, 0f, Time.deltaTime * 5f);
                bobPivot.localPosition = currentSmoothedPos;
                bobPivot.localRotation = Quaternion.Slerp(
                    bobPivot.localRotation,
                    startLocalRot,
                    Time.deltaTime * settings.bobSmoothSpeed
                );
                return;
            }

            // Handle landing bob
            Vector3 landingOffset = Vector3.zero;
            if (isLandingBobActive)
            {
                landingBobTimer += Time.deltaTime * landingBobSettleSpeed;

                // Damped sine wave for landing bob
                float decay = Mathf.Exp(-landingBobTimer);
                float wave = Mathf.Sin(landingBobTimer * 6f) * decay;
                landingOffset = Vector3.down * (wave * landingBobIntensity);

                // Reset when settled
                if (decay < 0.01f)
                {
                    isLandingBobActive = false;
                    landingBobIntensity = 0f;
                }
            }

            // Regular movement bob (only when grounded and not sliding)
            bool moving =
                controller.IsGrounded && controller.CurrentSpeed > 0.5f && !controller.IsSliding;

            Vector3 targetPos = startLocalPos + landingOffset;

            if (moving)
            {
                float frequency = controller.IsSprinting
                    ? settings.bobFrequencySprint
                    : settings.bobFrequencyWalk;
                bobTimer += Time.deltaTime * frequency;

                float bobY = Mathf.Sin(bobTimer) * settings.bobAmplitude;
                float bobX =
                    Mathf.Cos(bobTimer * 0.5f)
                    * settings.bobAmplitude
                    * settings.bobHorizontalRatio;

                targetPos += new Vector3(bobX, Mathf.Abs(bobY), 0f);
            }
            else
            {
                // reset timer so the next step starts from a neutral point instead of a random phase
                bobTimer = 0f;
            }

            Vector3 shakePos = Vector3.zero;
            Vector3 shakeRot = Vector3.zero;

            float speed = controller.CurrentSpeed;
            float targetShakeIntensity = 0f;
            
            if (speed >= settings.speedShakeMinSpeed)
            {
                targetShakeIntensity = Mathf.Clamp01((speed - settings.speedShakeMinSpeed) / Mathf.Max(0.1f, settings.speedShakeMaxSpeed - settings.speedShakeMinSpeed));
            }
            
            currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, targetShakeIntensity, Time.deltaTime * 8f);

            if (currentShakeIntensity > 0.001f)
            {
                float timeX = Time.time * settings.speedShakeFrequency;
                float timeY = Time.time * settings.speedShakeFrequency + 100f;
                float timeZ = Time.time * settings.speedShakeFrequency + 200f;
                
                shakePos = new Vector3(
                    (Mathf.PerlinNoise(timeX, 0f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(0f, timeY) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(timeZ, timeZ) - 0.5f) * 2f
                ) * (settings.speedShakePosAmplitude * currentShakeIntensity);

                shakeRot = new Vector3(
                    (Mathf.PerlinNoise(timeX + 300f, 0f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(0f, timeY + 300f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(timeZ + 300f, timeZ + 300f) - 0.5f) * 2f
                ) * (settings.speedShakeRotAmplitude * currentShakeIntensity);
            }

            currentSmoothedPos = Vector3.Lerp(
                currentSmoothedPos,
                targetPos,
                Time.deltaTime * settings.bobSmoothSpeed
            );
            
            bobPivot.localPosition = currentSmoothedPos + shakePos;
            bobPivot.localRotation = startLocalRot * Quaternion.Euler(shakeRot);
        }

        /// <summary>
        /// Manually trigger a landing bob (useful for external systems like grapple hooks).
        /// </summary>
        public void TriggerLandingBob(float intensityMultiplier = 1f)
        {
            if (!enableLandingBob)
                return;

            landingBobIntensity = landingBobAmplitude * Mathf.Clamp01(intensityMultiplier);
            landingBobTimer = 0f;
            isLandingBobActive = true;
        }
    }
}
