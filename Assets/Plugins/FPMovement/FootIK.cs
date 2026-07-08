using UnityEngine;

namespace FPMovement
{
    [RequireComponent(typeof(Animator))]
    public class FootIK : MonoBehaviour
    {
        [Header("IK Settings")]
        [Tooltip("Which layers should the feet collide with?")]
        public LayerMask groundMask = ~0;
        
        [Tooltip("Height offset for the foot above the ground. Tweak this if the foot clips into the floor.")]
        public float footOffset = 0.1f;
        
        [Tooltip("How far above the foot to start the downward raycast.")]
        public float raycastUpOffset = 0.5f;
        
        [Tooltip("Total length of the downward raycast.")]
        public float raycastDistance = 1.0f;
        
        [Tooltip("Max height the foot can be above the ground before IK is disabled (allows foot to lift during walking).")]
        public float stepHeightThreshold = 0.3f;

        [Tooltip("How fast the IK blends in and out.")]
        public float ikSmoothSpeed = 15f;

        // FPAnimationController will automatically control this via reflection
        // if this script is added to its 'Lower Ik Rigs' array!
        public float weight { get; set; } = 1f;

        private Animator animator;

        private float leftFootIKWeight;
        private float rightFootIKWeight;

        private void Start()
        {
            animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            // Target weight combined from our internal logic (grounded) and the controller's master weight
            float masterWeight = this.weight;

            if (masterWeight <= 0.01f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                return;
            }

            ProcessFoot(AvatarIKGoal.LeftFoot, ref leftFootIKWeight, masterWeight);
            ProcessFoot(AvatarIKGoal.RightFoot, ref rightFootIKWeight, masterWeight);
        }

        private void ProcessFoot(AvatarIKGoal foot, ref float currentWeight, float masterWeight)
        {
            Vector3 ikPos = animator.GetIKPosition(foot);
            Quaternion ikRot = animator.GetIKRotation(foot);
            
            float targetWeight = 0f;
            Vector3 targetPos = ikPos;
            Quaternion targetRot = ikRot;

            // Raycast down from above the foot
            Vector3 rayOrigin = ikPos + Vector3.up * raycastUpOffset;
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Calculate how high the foot's animation position is above the detected ground
                float heightDiff = ikPos.y - hit.point.y;
                
                if (heightDiff < stepHeightThreshold)
                {
                    // Fade in IK as foot gets closer to ground, to allow natural stepping mid-walk
                    targetWeight = 1f - Mathf.Clamp01(Mathf.Max(0f, heightDiff) / stepHeightThreshold);
                    
                    targetPos = hit.point;
                    targetPos.y += footOffset;

                    // Align rotation to ground normal
                    Quaternion tilt = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    targetRot = tilt * ikRot;
                }
            }

            // Smooth the weight transition
            currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * ikSmoothSpeed);
            
            float finalWeight = currentWeight * masterWeight;
            
            // Apply IK properties
            if (finalWeight > 0.001f)
            {
                animator.SetIKPositionWeight(foot, finalWeight);
                animator.SetIKRotationWeight(foot, finalWeight);
                
                // Blend between the animation's natural position and the grounded IK position
                animator.SetIKPosition(foot, Vector3.Lerp(ikPos, targetPos, finalWeight));
                animator.SetIKRotation(foot, Quaternion.Slerp(ikRot, targetRot, finalWeight));
            }
            else
            {
                animator.SetIKPositionWeight(foot, 0f);
                animator.SetIKRotationWeight(foot, 0f);
            }
        }
    }
}
