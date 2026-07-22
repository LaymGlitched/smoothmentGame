using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Master hub component for player footstep and movement audio.
    /// Coordinates surface detection, audio playback, shuffle detection, and landing mechanics.
    /// Exposes public methods for Animation Events and state-based volume/pitch modifiers.
    /// </summary>
    [RequireComponent(typeof(RigidbodyFPController))]
    [RequireComponent(typeof(SurfaceDetector))]
    [RequireComponent(typeof(MovementAudioPlayer))]
    [RequireComponent(typeof(MovementShuffleController))]
    [RequireComponent(typeof(MovementLandingController))]
    public class MovementAudioController : MonoBehaviour
    {
        [Header("Configuration Profile")]
        [Tooltip("ScriptableObject profile containing surface sound mappings and volume/pitch parameters.")]
        [SerializeField]
        private MovementAudioProfile profile;

        [Header("Feature Toggles")]
        public bool enableFootsteps = true;

        private RigidbodyFPController controller;
        private SurfaceDetector surfaceDetector;
        private MovementAudioPlayer audioPlayer;
        private MovementShuffleController shuffleController;
        private MovementLandingController landingController;

        public MovementAudioProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                ReconfigureSubsystems();
            }
        }

        public SurfaceDetector SurfaceDetector => surfaceDetector;
        public MovementAudioPlayer AudioPlayer => audioPlayer;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            surfaceDetector = GetComponent<SurfaceDetector>();
            audioPlayer = GetComponent<MovementAudioPlayer>();
            shuffleController = GetComponent<MovementShuffleController>();
            landingController = GetComponent<MovementLandingController>();

            ReconfigureSubsystems();
        }

        private void OnValidate()
        {
            ReconfigureSubsystems();
        }

        private void ReconfigureSubsystems()
        {
            if (shuffleController != null) shuffleController.Configure(profile);
            if (landingController != null) landingController.Configure(profile);
        }

        /// <summary>
        /// Plays a footstep sound for the surface beneath the player's feet.
        /// Intended to be invoked directly by Animation Events.
        /// </summary>
        /// <param name="stereoSide">-1.0 for Left foot, +1.0 for Right foot, 0.0 for Center.</param>
        public void PlayFootstep(float stereoSide = 0f)
        {
            if (!enableFootsteps || controller == null || surfaceDetector == null || audioPlayer == null || profile == null)
                return;

            // Optional safeguard: do not play animation footsteps when airborne unless desired
            if (!controller.IsGrounded)
                return;

            SurfaceType surface = surfaceDetector.DetectSurface();
            SurfaceAudioConfig config = profile.GetConfig(surface);

            if (config == null || config.footstepClips == null || config.footstepClips.Length == 0)
                return;

            // Calculate state volume and pitch modifiers
            float volumeMod = 1.0f;
            float pitchMod = 1.0f;

            if (controller.IsCrouching)
            {
                volumeMod *= profile.crouchVolumeMultiplier;
                pitchMod *= profile.crouchPitchMultiplier;
            }
            else if (controller.IsSprinting)
            {
                volumeMod *= profile.sprintVolumeMultiplier;
                pitchMod *= profile.sprintPitchMultiplier;
            }

            audioPlayer.PlayClip(
                config.footstepClips,
                config,
                profile,
                AudioCategory.Footstep,
                stereoSide,
                volumeMod,
                pitchMod
            );
        }

        /// <summary>
        /// Public method for Left Footstep Animation Event.
        /// </summary>
        public void PlayFootstepLeft() => PlayFootstep(-1.0f);

        /// <summary>
        /// Public method for Right Footstep Animation Event.
        /// </summary>
        public void PlayFootstepRight() => PlayFootstep(1.0f);

        /// <summary>
        /// Public method for generic Footstep Animation Event.
        /// </summary>
        public void PlayFootstepCenter() => PlayFootstep(0.0f);

        /// <summary>
        /// Public method for Shuffle Animation Event.
        /// </summary>
        public void PlayShuffle()
        {
            if (shuffleController != null)
            {
                shuffleController.TriggerShuffle();
            }
        }
    }
}
