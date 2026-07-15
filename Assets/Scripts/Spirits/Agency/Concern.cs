using UnityEngine;

namespace GameCode.Spirits.Agency
{
    /// <summary>
    /// Represents something the Spirit is actively worried about or focused on.
    /// Concerns are the foundational building blocks of Spirit Agency. They grow 
    /// when subjective memories reinforce them, and slowly decay over time.
    /// </summary>
    [System.Serializable]
    public class Concern
    {
        [SerializeField] private Data.ConcernId subject;
        [SerializeField] private float intensity;

        /// <summary>
        /// The topic or focus of this concern (e.g., "VesselHealth", "ManaWaste", "LoreMystery").
        /// </summary>
        public Data.ConcernId Subject => subject;

        /// <summary>
        /// How strongly the Spirit feels about this concern (0.0 to 1.0).
        /// </summary>
        public float Intensity => intensity;

        /// <summary>
        /// Creates a new active concern.
        /// </summary>
        public Concern(Data.ConcernId subject, float initialIntensity)
        {
            this.subject = subject;
            this.intensity = Mathf.Clamp01(initialIntensity);
        }

        /// <summary>
        /// Increases the intensity of the concern (e.g., after witnessing a reinforcing event).
        /// Restricted to internal agency logic.
        /// </summary>
        internal void IncreaseIntensity(float amount)
        {
            intensity = Mathf.Clamp01(intensity + amount);
        }

        /// <summary>
        /// Decreases the intensity of the concern (e.g., due to time decay or resolving action).
        /// Restricted to internal agency logic.
        /// </summary>
        internal void DecreaseIntensity(float amount)
        {
            intensity = Mathf.Clamp01(intensity - amount);
        }
    }
}
