using UnityEngine;

namespace FPMovement
{
    [RequireComponent(typeof(Animator))]
    public class FootIK : MonoBehaviour, IIKWeightTarget
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

        [Header("Animation Rigging Targets (Generic Rig)")]
        [Tooltip("The Target transform for the Left Leg TwoBoneIKConstraint.")]
        public Transform leftLegTarget;
        [Tooltip("The Target transform for the Right Leg TwoBoneIKConstraint.")]
        public Transform rightLegTarget;
        [Tooltip("The actual Left Foot bone.")]
        public Transform leftFootBone;
        [Tooltip("The actual Right Foot bone.")]
        public Transform rightFootBone;

        private Animator animator;
        private float leftFootIKWeight;
        private float rightFootIKWeight;

        private void Start()
        {
            animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            // Execute before RigBuilder evaluates (which also uses LateUpdate but usually later in the execution order)
            float masterWeight = this.weight;

            if (leftLegTarget != null && leftFootBone != null)
                ProcessFootRigging(leftFootBone, leftLegTarget, ref leftFootIKWeight, masterWeight);

            if (rightLegTarget != null && rightFootBone != null)
                ProcessFootRigging(rightFootBone, rightLegTarget, ref rightFootIKWeight, masterWeight);
        }

        private void ProcessFootRigging(Transform footBone, Transform ikTarget, ref float currentWeight, float masterWeight)
        {
            if (masterWeight <= 0.01f)
            {
                currentWeight = 0f;
                // If weight is 0, just snap target to the bone so it doesn't drift
                ikTarget.position = footBone.position;
                ikTarget.rotation = footBone.rotation;
                return;
            }

            Vector3 animPos = footBone.position;
            Quaternion animRot = footBone.rotation;
            
            float targetWeight = 0f;
            Vector3 targetPos = animPos;
            Quaternion targetRot = animRot;

            Vector3 rayOrigin = animPos + Vector3.up * raycastUpOffset;
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                float heightDiff = animPos.y - hit.point.y;
                
                if (heightDiff < stepHeightThreshold)
                {
                    targetWeight = 1f - Mathf.Clamp01(Mathf.Max(0f, heightDiff) / stepHeightThreshold);
                    
                    targetPos = hit.point;
                    targetPos.y += footOffset;

                    Quaternion tilt = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    targetRot = tilt * animRot;
                }
            }

            currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * ikSmoothSpeed);
            float finalWeight = currentWeight * masterWeight;

            // Move the IK target to the blended position
            ikTarget.position = Vector3.Lerp(animPos, targetPos, finalWeight);
            ikTarget.rotation = Quaternion.Slerp(animRot, targetRot, finalWeight);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (leftFootBone != null) DrawFootGizmo(leftFootBone);
            if (rightFootBone != null) DrawFootGizmo(rightFootBone);
        }

        private void DrawFootGizmo(Transform footTransform)
        {
            if (footTransform == null) return;

            Vector3 footPos = footTransform.position;
            Vector3 rayOrigin = footPos + Vector3.up * raycastUpOffset;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rayOrigin, 0.03f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * raycastDistance);
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(rayOrigin, hit.point);
                Gizmos.DrawSphere(hit.point, 0.04f);
                
                Gizmos.color = Color.cyan;
                Vector3 targetPos = hit.point;
                targetPos.y += footOffset;
                Gizmos.DrawWireCube(targetPos, new Vector3(0.15f, 0.02f, 0.15f));
            }
        }
#endif
    }
}
