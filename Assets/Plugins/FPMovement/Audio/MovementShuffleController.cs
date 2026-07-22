using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Monitors grounded rotation speed and sharp directional changes to trigger shuffle / foot drag audio.
    /// Operates independently from footsteps with turn rate thresholds and a configurable cooldown.
    /// </summary>
    public class MovementShuffleController : MonoBehaviour
    {
        [Header("Feature Toggles")]
        public bool enableShuffleSounds = true;

        [Header("Thresholds")]
        [Tooltip("Yaw rotation speed threshold (degrees per second) to trigger a turn shuffle.")]
        [SerializeField]
        private float yawTurnThreshold = 180f;

        [Tooltip("Minimum angle change in movement direction (degrees) to trigger a direction shift shuffle.")]
        [SerializeField]
        private float directionChangeThreshold = 65f;

        [Tooltip("Minimum horizontal velocity required for direction-change shuffles.")]
        [SerializeField]
        private float minSpeedForDirectionShuffle = 1.5f;

        [Tooltip("Cooldown period (seconds) between shuffle sounds to prevent audio spam.")]
        [SerializeField]
        private float shuffleCooldown = 0.35f;

        private RigidbodyFPController controller;
        private SurfaceDetector detector;
        private MovementAudioPlayer player;
        private MovementAudioProfile profile;

        private float lastYaw;
        private Vector3 lastMoveDir;
        private float lastShuffleTime = -999f;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            detector = GetComponent<SurfaceDetector>();
            player = GetComponent<MovementAudioPlayer>();
        }

        public void Configure(MovementAudioProfile audioProfile)
        {
            profile = audioProfile;
        }

        private void Start()
        {
            lastYaw = transform.eulerAngles.y;
            if (controller != null)
            {
                Vector3 hVel = controller.HorizontalVelocity;
                lastMoveDir = hVel.sqrMagnitude > 0.01f ? hVel.normalized : transform.forward;
            }
        }

        private void Update()
        {
            if (!enableShuffleSounds || controller == null || detector == null || player == null || profile == null)
                return;

            if (!controller.IsGrounded)
            {
                lastYaw = transform.eulerAngles.y;
                return;
            }

            float currentYaw = transform.eulerAngles.y;
            float yawDelta = Mathf.Abs(Mathf.DeltaAngle(lastYaw, currentYaw));
            float yawSpeed = yawDelta / Mathf.Max(Time.deltaTime, 0.0001f);
            lastYaw = currentYaw;

            Vector3 currentVel = controller.HorizontalVelocity;
            float currentSpeed = currentVel.magnitude;
            Vector3 currentMoveDir = currentSpeed > 0.1f ? currentVel / currentSpeed : transform.forward;

            bool shouldShuffle = false;

            // 1. Rapid Turn Shuffle
            if (yawSpeed >= yawTurnThreshold && Time.time - lastShuffleTime >= shuffleCooldown)
            {
                shouldShuffle = true;
            }

            // 2. Sharp Direction Change Shuffle
            if (!shouldShuffle && currentSpeed >= minSpeedForDirectionShuffle && lastMoveDir.sqrMagnitude > 0.01f)
            {
                float dirAngle = Vector3.Angle(lastMoveDir, currentMoveDir);
                if (dirAngle >= directionChangeThreshold && Time.time - lastShuffleTime >= shuffleCooldown)
                {
                    shouldShuffle = true;
                }
            }

            if (currentSpeed > 0.5f)
            {
                lastMoveDir = currentMoveDir;
            }

            if (shouldShuffle)
            {
                TriggerShuffle();
            }
        }

        /// <summary>
        /// Manually triggers a shuffle sound on the current ground surface.
        /// </summary>
        public void TriggerShuffle()
        {
            if (Time.time - lastShuffleTime < shuffleCooldown) return;

            SurfaceType surface = detector != null ? detector.DetectSurface() : null;
            SurfaceAudioConfig config = profile != null ? profile.GetConfig(surface) : null;

            if (config != null && config.shuffleClips != null && config.shuffleClips.Length > 0)
            {
                lastShuffleTime = Time.time;
                player.PlayClip(config.shuffleClips, config, profile, AudioCategory.Shuffle);
            }
        }
    }
}
