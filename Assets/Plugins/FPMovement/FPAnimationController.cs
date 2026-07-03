using System;
using System.Reflection;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Controls an Animator component based on the state of the RigidbodyFPController.
    /// Drives animation parameters and can blend IK Rig weights during special actions (like Sliding or Wallrunning).
    /// </summary>
    [RequireComponent(typeof(RigidbodyFPController))]
    public class FPAnimationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Animator component, typically on a child model object.")]
        public Animator animator;

        [Header("Animation Rigging (IK)")]
        [Tooltip("Drag Animation Rigging 'Rig' components here. Their weights will be blended down during extreme movements like Wallrunning or Sliding to let full-body animations take over.")]
        public MonoBehaviour[] ikRigs;
        
        [Tooltip("Speed at which IK weights blend in and out.")]
        public float ikBlendSpeed = 5f;
        
        [Tooltip("Enable this to completely disable IK during a wallrun.")]
        public bool disableIkDuringWallRun = true;
        
        [Tooltip("Enable this to completely disable IK during a slide.")]
        public bool disableIkDuringSlide = true;

        [Header("Parameter Names")]
        public string speedParam = "Speed";
        public string verticalVelocityParam = "VerticalVelocity";
        public string isGroundedParam = "IsGrounded";
        public string isSprintingParam = "IsSprinting";
        public string isSlidingParam = "IsSliding";
        public string isWallRunningParam = "IsWallRunning";
        public string wallRunSideParam = "WallRunSide"; // 1 for right, -1 for left
        public string jumpTriggerParam = "Jump";

        private RigidbodyFPController controller;
        private WallRunController wallRunController;
        
        // Parameter hashes for performance
        private int speedHash;
        private int verticalVelocityHash;
        private int isGroundedHash;
        private int isSprintingHash;
        private int isSlidingHash;
        private int isWallRunningHash;
        private int wallRunSideHash;
        private int jumpHash;

        private float targetIkWeight = 1f;
        private float currentIkWeight = 1f;

        private bool wasGrounded;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            wallRunController = GetComponent<WallRunController>(); // Might be null, that's okay

            // Cache parameter hashes
            speedHash = Animator.StringToHash(speedParam);
            verticalVelocityHash = Animator.StringToHash(verticalVelocityParam);
            isGroundedHash = Animator.StringToHash(isGroundedParam);
            isSprintingHash = Animator.StringToHash(isSprintingParam);
            isSlidingHash = Animator.StringToHash(isSlidingParam);
            isWallRunningHash = Animator.StringToHash(isWallRunningParam);
            wallRunSideHash = Animator.StringToHash(wallRunSideParam);
            jumpHash = Animator.StringToHash(jumpTriggerParam);
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.Jumped += OnJumped;
            }
            if (wallRunController != null)
            {
                wallRunController.WallRunStateChanged += OnWallRunStateChanged;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.Jumped -= OnJumped;
            }
            if (wallRunController != null)
            {
                wallRunController.WallRunStateChanged -= OnWallRunStateChanged;
            }
        }

        private void OnJumped()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(jumpHash);
            }
        }

        private void OnWallRunStateChanged(bool isRightWall)
        {
            if (animator != null && animator.gameObject.activeInHierarchy && wallRunController.IsWallRunning)
            {
                animator.SetFloat(wallRunSideHash, isRightWall ? 1f : -1f);
            }
        }

        private void Update()
        {
            if (animator == null || !animator.gameObject.activeInHierarchy)
                return;

            UpdateAnimatorParameters();
            UpdateIKWeights();
        }

        private void UpdateAnimatorParameters()
        {
            // 1. Speed (Horizontal)
            float currentSpeed = controller.HorizontalVelocity.magnitude;
            float walkSpeed = controller.Settings != null ? controller.Settings.walkSpeed : 5f;
            float sprintSpeed = controller.Settings != null ? controller.Settings.sprintSpeed : 10f;
            
            float animSpeed = 0f;
            if (currentSpeed > 0.1f)
            {
                if (currentSpeed <= walkSpeed + 0.5f) // Walking or slower (with slight margin)
                {
                    // Map 0 -> walkSpeed to 0 -> 0.5
                    animSpeed = Mathf.Lerp(0f, 0.5f, currentSpeed / walkSpeed);
                }
                else
                {
                    // Map walkSpeed -> sprintSpeed to 0.5 -> 1.0
                    animSpeed = Mathf.Lerp(0.5f, 1f, (currentSpeed - walkSpeed) / Mathf.Max(0.1f, sprintSpeed - walkSpeed));
                }
            }
            
            animator.SetFloat(speedHash, animSpeed);

            // 2. Vertical Velocity (Falling/Jumping)
            float verticalVelocity = controller.Body.linearVelocity.y;
            animator.SetFloat(verticalVelocityHash, verticalVelocity);

            // 3. Grounded State
            bool isGrounded = controller.IsGrounded;
            animator.SetBool(isGroundedHash, isGrounded);

            // Reset jump trigger if we just landed to prevent weird state transitions
            if (isGrounded && !wasGrounded)
            {
                animator.ResetTrigger(jumpHash);
            }
            wasGrounded = isGrounded;

            // 4. Sprinting
            animator.SetBool(isSprintingHash, controller.IsSprinting);

            // 5. Sliding
            bool isSliding = controller.IsSliding;
            animator.SetBool(isSlidingHash, isSliding);

            // 6. Wall Running
            bool isWallRunning = wallRunController != null && wallRunController.IsWallRunning;
            animator.SetBool(isWallRunningHash, isWallRunning);
        }

        private void UpdateIKWeights()
        {
            if (ikRigs == null || ikRigs.Length == 0) return;

            bool isSliding = controller.IsSliding;
            bool isWallRunning = wallRunController != null && wallRunController.IsWallRunning;

            // Determine if IK should be overridden by a full body animation
            bool disableIk = (isSliding && disableIkDuringSlide) || (isWallRunning && disableIkDuringWallRun);

            targetIkWeight = disableIk ? 0f : 1f;

            if (Mathf.Abs(currentIkWeight - targetIkWeight) > 0.01f)
            {
                currentIkWeight = Mathf.Lerp(currentIkWeight, targetIkWeight, Time.deltaTime * ikBlendSpeed);
                
                // Use reflection to set weight so we don't need a hard dependency on the Animation Rigging package asmdef
                foreach (var rig in ikRigs)
                {
                    if (rig != null)
                    {
                        var prop = rig.GetType().GetProperty("weight", BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null && prop.PropertyType == typeof(float))
                        {
                            prop.SetValue(rig, currentIkWeight);
                        }
                    }
                }
            }
        }
    }
}
