using GameCode.Spirits.Agency;
using GameCode.Spirits.Core;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Communication
{
    /// <summary>
    /// The translation layer for an individual Spirit.
    /// Consumes raw Agency impulses and decides IF and HOW the Spirit wants to express them.
    /// Outputs a conceptual CommunicationIntent, completely decoupled from actual dialogue assets.
    /// Owned via composition by the Spirit class.
    /// </summary>
    public class SpiritCommunicationCore
    {
        private readonly Spirit owner;
        
        // Cooldown to prevent the Spirit from spamming intent requests
        private float lastIntentTime;
        private const float DefaultCooldown = 3.0f;
        
        private CommunicationIntent? latestIntent;

        public SpiritCommunicationCore(Spirit owner)
        {
            this.owner = owner ?? throw new System.ArgumentNullException(nameof(owner));
            // Initialize so the spirit can speak immediately
            lastIntentTime = -DefaultCooldown; 
        }

        /// <summary>
        /// Read-only access to the last generated intent for debugging and UI.
        /// </summary>
        public CommunicationIntent? LatestIntent => latestIntent;

        /// <summary>
        /// Read-only access to the time the last intent was generated.
        /// </summary>
        public float LastIntentTime => lastIntentTime;

        /// <summary>
        /// Evaluates an internal impulse from the Agency system and decides whether 
        /// it should be translated into an outward desire to communicate.
        /// </summary>
        /// <param name="impulse">The internal motivation/worry.</param>
        /// <returns>A CommunicationIntent if the Spirit decides to speak, otherwise null.</returns>
        public CommunicationIntent? TranslateImpulse(AgencyImpulse impulse)
        {
            float currentTime = Time.time;
            bool isCooldownActive = (currentTime - lastIntentTime) < DefaultCooldown;

            // Determine how urgently the Spirit wants to express this concern
            PriorityTier priority = DeterminePriority(impulse.Intensity);

            // If we are on cooldown, suppress lower-priority ambient chatter.
            // Critical warnings bypass the cooldown.
            if (isCooldownActive && priority < PriorityTier.Urgent)
            {
                return null;
            }

            // The conceptual topic maps directly to what the Spirit is concerned about
            string topic = impulse.DrivingConcern.Subject;

            lastIntentTime = currentTime;

            latestIntent = new CommunicationIntent(owner, topic, priority);
            return latestIntent;
        }

        /// <summary>
        /// Translates the raw intensity of an impulse into a standardized priority tier 
        /// that the global Dialogue Coordinator can understand.
        /// </summary>
        private PriorityTier DeterminePriority(float intensity)
        {
            if (intensity >= 0.9f) return PriorityTier.Critical;
            if (intensity >= 0.7f) return PriorityTier.Urgent;
            if (intensity >= 0.4f) return PriorityTier.Standard;
            return PriorityTier.Ambient;
        }
    }
}
