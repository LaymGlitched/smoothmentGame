using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// ScriptableObject representing a distinct ground surface type (e.g. Grass, Stone, Metal, Water, Wood).
    /// Used by <see cref="SurfaceDetector"/> and <see cref="MovementAudioProfile"/> to dynamically map materials to audio clip sets.
    /// Designers can create new SurfaceTypes in the project without altering any code.
    /// </summary>
    [CreateAssetMenu(fileName = "New Surface Type", menuName = "FPMovement/Audio/Surface Type")]
    public class SurfaceType : ScriptableObject
    {
        [Tooltip("Human-readable name for this surface type.")]
        public string displayName = "Default Surface";

        [Tooltip("Optional fallback surface if audio clips for this specific surface are missing in a profile.")]
        public SurfaceType fallbackSurface;

        /// <summary>
        /// Returns the name of the surface for debug or logging purposes.
        /// </summary>
        public override string ToString() => string.IsNullOrEmpty(displayName) ? name : displayName;
    }
}
