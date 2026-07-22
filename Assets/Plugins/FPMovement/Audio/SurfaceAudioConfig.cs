using System;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Configuration data for audio clips and parameters mapped to a specific <see cref="SurfaceType"/>.
    /// Holds clips for footsteps, shuffles, and landing tiers along with volume, pitch, and stereo randomization settings.
    /// </summary>
    [Serializable]
    public class SurfaceAudioConfig
    {
        [Tooltip("The surface type this configuration applies to.")]
        public SurfaceType surfaceType;

        [Header("Audio Clips")]
        [Tooltip("Footstep audio clips. Avoids repeating clips twice in a row during playback.")]
        public AudioClip[] footstepClips = new AudioClip[0];

        [Tooltip("Shuffle / foot drag audio clips played during sharp turns or rapid rotations.")]
        public AudioClip[] shuffleClips = new AudioClip[0];

        [Tooltip("Soft landing audio clips for low-speed impacts.")]
        public AudioClip[] landSoftClips = new AudioClip[0];

        [Tooltip("Medium landing audio clips for moderate-speed impacts.")]
        public AudioClip[] landMediumClips = new AudioClip[0];

        [Tooltip("Heavy landing audio clips for high-speed impacts.")]
        public AudioClip[] landHeavyClips = new AudioClip[0];

        [Header("Randomization & Variation")]
        [Tooltip("Random volume range multiplier [min, max].")]
        public Vector2 volumeRange = new Vector2(0.85f, 1.0f);

        [Tooltip("Random pitch range multiplier [min, max].")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Maximum stereo pan variation (-stereoPanRange to +stereoPanRange). 0 disables pan variation.")]
        [Range(0f, 0.5f)]
        public float stereoPanRange = 0.1f;
    }
}
