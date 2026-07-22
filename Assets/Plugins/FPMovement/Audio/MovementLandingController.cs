using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Listens to landing events from <see cref="RigidbodyFPController"/> and triggers surface-aware landing audio.
    /// Evaluates impact speed into Soft, Medium, or Heavy landing tiers.
    /// </summary>
    public class MovementLandingController : MonoBehaviour
    {
        [Header("Feature Toggles")]
        public bool enableLandingSounds = true;

        [Header("Impact Speed Thresholds (m/s)")]
        [Tooltip("Minimum downward velocity magnitude to trigger a soft landing sound.")]
        [SerializeField]
        private float softLandingThreshold = 3.5f;

        [Tooltip("Minimum downward velocity magnitude to trigger a medium landing sound.")]
        [SerializeField]
        private float mediumLandingThreshold = 7.5f;

        [Tooltip("Minimum downward velocity magnitude to trigger a heavy landing sound.")]
        [SerializeField]
        private float heavyLandingThreshold = 13.5f;

        [Tooltip("If true, landing with horizontal speed also triggers a subtle foot drag shuffle.")]
        [SerializeField]
        private bool triggerLandShuffle = true;

        private RigidbodyFPController controller;
        private SurfaceDetector detector;
        private MovementAudioPlayer player;
        private MovementShuffleController shuffleController;
        private MovementAudioProfile profile;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            detector = GetComponent<SurfaceDetector>();
            player = GetComponent<MovementAudioPlayer>();
            shuffleController = GetComponent<MovementShuffleController>();
        }

        public void Configure(MovementAudioProfile audioProfile)
        {
            profile = audioProfile;
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.Landed += OnPlayerLanded;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.Landed -= OnPlayerLanded;
            }
        }

        private void OnPlayerLanded(Vector3 landingVelocity)
        {
            if (!enableLandingSounds || detector == null || player == null || profile == null)
                return;

            float verticalImpactSpeed = Mathf.Abs(landingVelocity.y);
            if (verticalImpactSpeed < softLandingThreshold)
                return;

            SurfaceType surface = detector.DetectSurface();
            SurfaceAudioConfig config = profile.GetConfig(surface);
            if (config == null) return;

            AudioClip[] selectedClips = null;

            if (verticalImpactSpeed >= heavyLandingThreshold)
            {
                selectedClips = config.landHeavyClips.Length > 0 ? config.landHeavyClips : config.landMediumClips;
            }
            else if (verticalImpactSpeed >= mediumLandingThreshold)
            {
                selectedClips = config.landMediumClips.Length > 0 ? config.landMediumClips : config.landSoftClips;
            }
            else
            {
                selectedClips = config.landSoftClips;
            }

            if (selectedClips != null && selectedClips.Length > 0)
            {
                player.PlayClip(selectedClips, config, profile, AudioCategory.Landing);
            }

            if (triggerLandShuffle && shuffleController != null)
            {
                Vector3 horizontalVel = new Vector3(landingVelocity.x, 0f, landingVelocity.z);
                if (horizontalVel.magnitude > 2.0f)
                {
                    shuffleController.TriggerShuffle();
                }
            }
        }
    }
}
