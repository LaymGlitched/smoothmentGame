using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Lightweight bridge component placed on the Animator GameObject.
    /// Exposes simple methods for Animation Events to trigger footstep, shuffle, and landing audio on the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerFootstepEvents : MonoBehaviour
    {
        [Tooltip("Reference to the player's MovementAudioController. If unassigned, automatically finds it on root or parent.")]
        [SerializeField]
        private MovementAudioController audioController;

        private bool hasLoggedMissingWarning;

        private void Awake()
        {
            FindAudioController();
        }

        private void FindAudioController()
        {
            if (audioController == null)
            {
                audioController = GetComponentInParent<MovementAudioController>();
            }
            if (audioController == null)
            {
                audioController = GetComponent<MovementAudioController>();
            }
        }

        /// <summary>
        /// Animation Event trigger for Left Footstep.
        /// </summary>
        public void OnFootstepLeft()
        {
            if (EnsureController())
            {
                audioController.PlayFootstepLeft();
            }
        }

        /// <summary>
        /// Animation Event trigger for Right Footstep.
        /// </summary>
        public void OnFootstepRight()
        {
            if (EnsureController())
            {
                audioController.PlayFootstepRight();
            }
        }

        /// <summary>
        /// Animation Event trigger for generic/center Footstep.
        /// </summary>
        public void OnFootstep()
        {
            if (EnsureController())
            {
                audioController.PlayFootstepCenter();
            }
        }

        /// <summary>
        /// Animation Event trigger for Shuffle / Foot Drag.
        /// </summary>
        public void OnShuffle()
        {
            if (EnsureController())
            {
                audioController.PlayShuffle();
            }
        }

        private bool EnsureController()
        {
            if (audioController == null)
            {
                FindAudioController();
            }

            if (audioController == null)
            {
                if (!hasLoggedMissingWarning)
                {
                    Debug.LogWarning($"PlayerFootstepEvents on {gameObject.name}: Could not find MovementAudioController on root or parent!", this);
                    hasLoggedMissingWarning = true;
                }
                return false;
            }

            return true;
        }
    }
}
