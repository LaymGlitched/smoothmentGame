using System.Collections.Generic;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Data-driven ScriptableObject holding footstep, shuffle, and landing sound configurations for all surfaces.
    /// Hot-swappable at runtime to support stealth modes, different footwear, or environmental modifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "New Movement Audio Profile", menuName = "FPMovement/Audio/Movement Audio Profile")]
    public class MovementAudioProfile : ScriptableObject
    {
        [Header("Surface Configurations")]
        [Tooltip("List of surface configurations mapping SurfaceTypes to sound clip sets.")]
        public List<SurfaceAudioConfig> surfaceConfigs = new List<SurfaceAudioConfig>();

        [Tooltip("Fallback configuration to use when a SurfaceType is not found in the surfaceConfigs list.")]
        public SurfaceAudioConfig defaultFallbackConfig;

        [Header("Global Volume Multipliers")]
        [Range(0f, 2f)]
        public float masterVolume = 1.0f;

        [Range(0f, 2f)]
        public float footstepVolumeMultiplier = 1.0f;

        [Range(0f, 2f)]
        public float shuffleVolumeMultiplier = 0.7f;

        [Range(0f, 2f)]
        public float landingVolumeMultiplier = 1.0f;

        [Header("State Modifiers")]
        [Tooltip("Volume multiplier applied when the player is crouching.")]
        [Range(0f, 2f)]
        public float crouchVolumeMultiplier = 0.5f;

        [Tooltip("Pitch multiplier applied when the player is crouching.")]
        [Range(0.5f, 1.5f)]
        public float crouchPitchMultiplier = 0.9f;

        [Tooltip("Volume multiplier applied when the player is sprinting.")]
        [Range(0f, 2f)]
        public float sprintVolumeMultiplier = 1.3f;

        [Tooltip("Pitch multiplier applied when the player is sprinting.")]
        [Range(0.5f, 1.5f)]
        public float sprintPitchMultiplier = 1.1f;

        // Runtime cached dictionary for zero-allocation O(1) lookups
        private Dictionary<SurfaceType, SurfaceAudioConfig> lookupTable;

        private void OnEnable()
        {
            InitializeLookupTable();
        }

        /// <summary>
        /// Rebuilds the O(1) lookup dictionary for fast runtime surface configuration access.
        /// </summary>
        public void InitializeLookupTable()
        {
            if (lookupTable == null)
                lookupTable = new Dictionary<SurfaceType, SurfaceAudioConfig>();
            else
                lookupTable.Clear();

            if (surfaceConfigs == null) return;

            foreach (var config in surfaceConfigs)
            {
                if (config != null && config.surfaceType != null && !lookupTable.ContainsKey(config.surfaceType))
                {
                    lookupTable.Add(config.surfaceType, config);
                }
            }
        }

        /// <summary>
        /// Retrieves the <see cref="SurfaceAudioConfig"/> for the specified <see cref="SurfaceType"/>.
        /// Returns fallback config or default if missing.
        /// </summary>
        public SurfaceAudioConfig GetConfig(SurfaceType surfaceType)
        {
            if (lookupTable == null || lookupTable.Count != surfaceConfigs.Count)
            {
                InitializeLookupTable();
            }

            if (surfaceType != null && lookupTable.TryGetValue(surfaceType, out SurfaceAudioConfig config))
            {
                return config;
            }

            // Check if surfaceType has a fallback
            if (surfaceType != null && surfaceType.fallbackSurface != null)
            {
                if (lookupTable.TryGetValue(surfaceType.fallbackSurface, out SurfaceAudioConfig fallbackMatch))
                {
                    return fallbackMatch;
                }
            }

            return defaultFallbackConfig;
        }
    }
}
