using System.Collections.Generic;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Category of movement sound for clip index tracking and audio source selection.
    /// </summary>
    public enum AudioCategory
    {
        Footstep,
        Shuffle,
        Landing
    }

    /// <summary>
    /// Handles audio playback, volume/pitch randomization, non-repeating clip selection,
    /// stereo panning variation, and 3D AudioSource spatial attenuation.
    /// </summary>
    public class MovementAudioPlayer : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("AudioSource dedicated to footstep sounds.")]
        [SerializeField]
        private AudioSource footstepSource;

        [Tooltip("AudioSource dedicated to shuffle / foot drag sounds.")]
        [SerializeField]
        private AudioSource shuffleSource;

        [Tooltip("AudioSource dedicated to landing impact sounds.")]
        [SerializeField]
        private AudioSource landingSource;

        [Header("Spatial & 3D Settings")]
        [Tooltip("Spatial blend (0 = 2D, 1 = 3D). Default is 1.0 for 3D positional audio.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float spatialBlend = 1.0f;

        [Tooltip("Min distance for 3D attenuation.")]
        [SerializeField]
        private float minDistance = 1.0f;

        [Tooltip("Max distance for 3D attenuation.")]
        [SerializeField]
        private float maxDistance = 25.0f;

        [Tooltip("Audio rolloff mode for 3D attenuation.")]
        [SerializeField]
        private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        // Tracks last played clip index per category to prevent repeating the same clip twice in a row
        private int lastFootstepIndex = -1;
        private int lastShuffleIndex = -1;
        private int lastLandingIndex = -1;

        private void Awake()
        {
            EnsureAudioSources();
        }

        private void EnsureAudioSources()
        {
            if (footstepSource == null) footstepSource = CreateDedicatedSource("FootstepAudioSource");
            if (shuffleSource == null) shuffleSource = CreateDedicatedSource("ShuffleAudioSource");
            if (landingSource == null) landingSource = CreateDedicatedSource("LandingAudioSource");

            ConfigureSource(footstepSource);
            ConfigureSource(shuffleSource);
            ConfigureSource(landingSource);
        }

        private AudioSource CreateDedicatedSource(string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform, false);
            return child.AddComponent<AudioSource>();
        }

        private void ConfigureSource(AudioSource source)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = rolloffMode;
        }

        /// <summary>
        /// Plays a sound clip from the given clip array using the configuration and category parameters.
        /// </summary>
        /// <param name="clips">Array of audio clips to pick from.</param>
        /// <param name="config">Surface configuration containing volume, pitch, and stereo settings.</param>
        /// <param name="profile">Global movement audio profile for volume multipliers.</param>
        /// <param name="category">Audio category (Footstep, Shuffle, Landing).</param>
        /// <param name="stereoSideMultiplier">-1.0 for Left foot, +1.0 for Right foot, 0.0 for center.</param>
        /// <param name="volumeModifier">Extra state modifier volume multiplier (e.g. crouch/sprint).</param>
        /// <param name="pitchModifier">Extra state modifier pitch multiplier (e.g. crouch/sprint).</param>
        public void PlayClip(
            AudioClip[] clips,
            SurfaceAudioConfig config,
            MovementAudioProfile profile,
            AudioCategory category,
            float stereoSideMultiplier = 0f,
            float volumeModifier = 1.0f,
            float pitchModifier = 1.0f)
        {
            if (clips == null || clips.Length == 0 || config == null || profile == null)
                return;

            AudioSource targetSource = GetSourceForCategory(category);
            if (targetSource == null) return;

            int lastIndex = GetLastIndexForCategory(category);
            int selectedIndex = SelectNonRepeatingIndex(clips, lastIndex);
            SetLastIndexForCategory(category, selectedIndex);

            AudioClip clip = clips[selectedIndex];
            if (clip == null) return;

            // Calculate randomized volume and pitch
            float baseVol = Random.Range(config.volumeRange.x, config.volumeRange.y);
            float basePitch = Random.Range(config.pitchRange.x, config.pitchRange.y);

            float catMultiplier = GetCategoryVolumeMultiplier(profile, category);
            float finalVolume = baseVol * profile.masterVolume * catMultiplier * volumeModifier;
            float finalPitch = basePitch * pitchModifier;

            // Calculate stereo panning
            float pan = 0f;
            if (config.stereoPanRange > 0f)
            {
                pan = Random.Range(-config.stereoPanRange, config.stereoPanRange);
            }
            if (stereoSideMultiplier != 0f)
            {
                pan = Mathf.Clamp(pan + (stereoSideMultiplier * 0.15f), -1f, 1f);
            }

            // Play one-shot sound
            targetSource.pitch = finalPitch;
            targetSource.panStereo = pan;
            targetSource.PlayOneShot(clip, finalVolume);
        }

        private int SelectNonRepeatingIndex(AudioClip[] clips, int lastIndex)
        {
            if (clips.Length <= 1) return 0;

            int newIndex = Random.Range(0, clips.Length);
            if (newIndex == lastIndex)
            {
                newIndex = (newIndex + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            }
            return newIndex;
        }

        private AudioSource GetSourceForCategory(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Footstep: return footstepSource;
                case AudioCategory.Shuffle: return shuffleSource;
                case AudioCategory.Landing: return landingSource;
                default: return footstepSource;
            }
        }

        private float GetCategoryVolumeMultiplier(MovementAudioProfile profile, AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Footstep: return profile.footstepVolumeMultiplier;
                case AudioCategory.Shuffle: return profile.shuffleVolumeMultiplier;
                case AudioCategory.Landing: return profile.landingVolumeMultiplier;
                default: return 1.0f;
            }
        }

        private int GetLastIndexForCategory(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Footstep: return lastFootstepIndex;
                case AudioCategory.Shuffle: return lastShuffleIndex;
                case AudioCategory.Landing: return lastLandingIndex;
                default: return -1;
            }
        }

        private void SetLastIndexForCategory(AudioCategory category, int index)
        {
            switch (category)
            {
                case AudioCategory.Footstep: lastFootstepIndex = index; break;
                case AudioCategory.Shuffle: lastShuffleIndex = index; break;
                case AudioCategory.Landing: lastLandingIndex = index; break;
            }
        }
    }
}
