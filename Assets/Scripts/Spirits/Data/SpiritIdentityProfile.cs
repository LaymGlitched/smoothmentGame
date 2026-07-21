using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Defines the immutable personality and psychological framework of a Spirit.
    /// Extracted from SpiritDefinition into its own ScriptableObject to prevent 
    /// the base definition from becoming a bloated God Object as the identity 
    /// system expands with more axes and traits.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Spirit Identity Profile", fileName = "NewIdentityProfile")]
    public class SpiritIdentityProfile : ScriptableObject
    {
        [SerializeField] private Color primaryColor;

        [Header("Temperament Axes (0.0 to 1.0)")]
        
        [Tooltip("0 = Detached/Unfeeling, 1 = Deeply Feeling/Compassionate")]
        [Range(0f, 1f)]
        [SerializeField] private float empathy = 0.5f;

        [Tooltip("0 = Reckless/Impulsive, 1 = Overcautious/Calculated")]
        [Range(0f, 1f)]
        [SerializeField] private float caution = 0.5f;

        [Tooltip("0 = Pacifistic, 1 = Combative/Aggressive")]
        [Range(0f, 1f)]
        [SerializeField] private float aggression = 0.5f;

        /// <summary>
        /// How much the Spirit cares about the wellbeing of the Vessel and others.
        /// </summary>
        public float Empathy => empathy;

        /// <summary>
        /// How much the Spirit prioritizes safety and risk-aversion.
        /// </summary>
        public float Caution => caution;

        /// <summary>
        /// How predisposed the Spirit is toward conflict and decisive force.
        /// </summary>
        public float Aggression => aggression;

        /// <summary>
        /// The spirit's main color.
        /// </summary>
        public Color PrimaryColor => primaryColor;
    }
}
