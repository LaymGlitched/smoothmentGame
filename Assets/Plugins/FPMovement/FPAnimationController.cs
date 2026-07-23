using System;
using System.Collections;
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

        [Header("Death Camera Follow")]
        [Tooltip("Transform of the Camera or Camera pivot that follows the head during death.")]
        public Transform cameraTransform;

        [Tooltip("Transform of the character's head bone. If null, auto-detected from Animator.")]
        public Transform headTransform;

        [Tooltip("Smoothing speed for the camera following the head during death.")]
        public float deathCameraSmoothSpeed = 10f;

        private Vector3 defaultCameraLocalPosition;
        private Quaternion defaultCameraLocalRotation;

        [Header("Animation Rigging (IK)")]
        [Tooltip("Upper body IK rigs (arms/hands).")]
        public MonoBehaviour[] upperIkRigs;

        [Tooltip("Lower body IK rigs (legs/feet). Can be disabled during Kicking.")]
        public MonoBehaviour[] lowerIkRigs;

        private IIKWeightTarget[] upperIkTargets;
        private IIKWeightTarget[] lowerIkTargets;

        [Tooltip("Speed at which IK weights blend in and out.")]
        public float ikBlendSpeed = 5f;

        [Tooltip("Enable this to completely disable IK during a wallrun.")]
        public bool disableIkDuringWallRun = true;

        [Tooltip("Enable this to completely disable IK during a slide.")]
        public bool disableIkDuringSlide = true;

        [Tooltip("Enable this to completely disable IK during traversal (vault, mantle, climb).")]
        public bool disableIkDuringTraversal = true;

        [Tooltip("Enable this to completely disable lower body IK during a kick.")]
        public bool disableIkDuringKick = true;

        [Header("Parameter Names")]
        public string speedParam = "Speed";
        public string verticalVelocityParam = "VerticalVelocity";
        public string isGroundedParam = "IsGrounded";
        public string isSprintingParam = "IsSprinting";
        public string isSlidingParam = "IsSliding";
        public string isWallRunningParam = "IsWallRunning";
        public string wallRunSideParam = "WallRunSide"; // 1 for right, -1 for left
        public string jumpTriggerParam = "Jump";
        public string climbSmallTriggerParam = "ClimbSmall";
        public string climbMediumTriggerParam = "ClimbMedium";
        public string climbLargeTriggerParam = "ClimbLarge";
        public string mantleTriggerParam = "Mantle";
        public string isTraversingParam = "IsTraversing";
        public string kickTriggerParam = "Kick";
        public string spellCastTriggerParam = "SpellCast";
        public string isHoldingSpellParam = "HoldingSpell";
        public string dashTriggerParam = "Dash";
        public string isDashingParam = "IsDashing";
        public string deathTriggerParam = "Die";
        public string isDeadParam = "IsDead";

        private RigidbodyFPController controller;
        private WallRunController wallRunController;
        private LedgeTraversalController traversalController;
        private AirDashController airDashController;

        // Parameter hashes for performance
        private int speedHash;
        private int verticalVelocityHash;
        private int isGroundedHash;
        private int isSprintingHash;
        private int isSlidingHash;
        private int isWallRunningHash;
        private int wallRunSideHash;
        private int jumpHash;
        private int climbSmallHash;
        private int climbMediumHash;
        private int climbLargeHash;
        private int mantleHash;
        private int isTraversingHash;
        private int kickHash;
        private int spellCastHash;
        private int isHoldingSpellHash;
        private int dashHash;
        private int isDashingHash;
        private int deathHash;
        private int isDeadHash;

        private bool isDead;
        public bool IsDead => isDead;

        public event Action SpellCasted;

        private float targetUpperIkWeight = 1f;
        private float currentUpperIkWeight = 1f;

        private float targetLowerIkWeight = 1f;
        private float currentLowerIkWeight = 1f;

        private bool wasGrounded;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            wallRunController = GetComponent<WallRunController>(); // Might be null, that's okay
            traversalController = GetComponent<LedgeTraversalController>(); // Might be null
            airDashController = GetComponent<AirDashController>(); // Might be null

            // Cache parameter hashes
            speedHash = Animator.StringToHash(speedParam);
            verticalVelocityHash = Animator.StringToHash(verticalVelocityParam);
            isGroundedHash = Animator.StringToHash(isGroundedParam);
            isSprintingHash = Animator.StringToHash(isSprintingParam);
            isSlidingHash = Animator.StringToHash(isSlidingParam);
            isWallRunningHash = Animator.StringToHash(isWallRunningParam);
            wallRunSideHash = Animator.StringToHash(wallRunSideParam);
            jumpHash = Animator.StringToHash(jumpTriggerParam);
            climbSmallHash = Animator.StringToHash(climbSmallTriggerParam);
            climbMediumHash = Animator.StringToHash(climbMediumTriggerParam);
            climbLargeHash = Animator.StringToHash(climbLargeTriggerParam);
            mantleHash = Animator.StringToHash(mantleTriggerParam);
            isTraversingHash = Animator.StringToHash(isTraversingParam);
            kickHash = Animator.StringToHash(kickTriggerParam);
            spellCastHash = Animator.StringToHash(spellCastTriggerParam);
            isHoldingSpellHash = Animator.StringToHash(isHoldingSpellParam);
            dashHash = Animator.StringToHash(dashTriggerParam);
            isDashingHash = Animator.StringToHash(isDashingParam);
            deathHash = Animator.StringToHash(deathTriggerParam);
            isDeadHash = Animator.StringToHash(isDeadParam);

            upperIkTargets = CacheIKTargets(upperIkRigs);
            lowerIkTargets = CacheIKTargets(lowerIkRigs);

            if (cameraTransform == null) cameraTransform = FindCameraTransform();
            if (headTransform == null) headTransform = FindHeadTransform();

            if (cameraTransform != null)
            {
                defaultCameraLocalPosition = cameraTransform.localPosition;
                defaultCameraLocalRotation = cameraTransform.localRotation;
            }
        }

        private IIKWeightTarget[] CacheIKTargets(MonoBehaviour[] rigs)
        {
            if (rigs == null) return new IIKWeightTarget[0];
            System.Collections.Generic.List<IIKWeightTarget> targets = new System.Collections.Generic.List<IIKWeightTarget>();
            foreach (var rig in rigs)
            {
                if (rig is IIKWeightTarget target)
                {
                    targets.Add(target);
                }
            }
            return targets.ToArray();
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.Jumped += OnJumped;
                controller.Kicked += OnKicked;
            }
            if (wallRunController != null)
            {
                wallRunController.WallRunStateChanged += OnWallRunStateChanged;
            }
            if (traversalController != null)
            {
                traversalController.ClimbSmallStarted += OnClimbSmallStarted;
                traversalController.ClimbMediumStarted += OnClimbMediumStarted;
                traversalController.ClimbLargeStarted += OnClimbLargeStarted;
                traversalController.MantleStarted += OnMantleStarted;
                traversalController.TraversalEnded += OnTraversalEnded;
            }
            if (airDashController != null)
            {
                airDashController.OnAirDash += OnAirDashed;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.Jumped -= OnJumped;
                controller.Kicked -= OnKicked;
            }
            if (wallRunController != null)
            {
                wallRunController.WallRunStateChanged -= OnWallRunStateChanged;
            }
            if (traversalController != null)
            {
                traversalController.ClimbSmallStarted -= OnClimbSmallStarted;
                traversalController.ClimbMediumStarted -= OnClimbMediumStarted;
                traversalController.ClimbLargeStarted -= OnClimbLargeStarted;
                traversalController.MantleStarted -= OnMantleStarted;
                traversalController.TraversalEnded -= OnTraversalEnded;
            }
            if (airDashController != null)
            {
                airDashController.OnAirDash -= OnAirDashed;
            }
        }

        private void OnJumped()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(jumpHash);
            }
        }

        private void OnAirDashed()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(dashHash);
            }
        }

        private void OnKicked()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(kickHash);
            }
        }

        /// <summary>
        /// Triggers the death animation and locks movement animation parameters.
        /// </summary>
        public void Die()
        {
            TriggerDeath();
        }

        /// <summary>
        /// Triggers the death animation and locks movement animation parameters.
        /// </summary>
        public void TriggerDeath()
        {
            isDead = true;
            if (cameraTransform == null) cameraTransform = FindCameraTransform();
            if (headTransform == null) headTransform = FindHeadTransform();

            if (cameraTransform != null && defaultCameraLocalPosition == Vector3.zero)
            {
                defaultCameraLocalPosition = cameraTransform.localPosition;
                defaultCameraLocalRotation = cameraTransform.localRotation;
            }

            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(deathHash);
                animator.SetBool(isDeadHash, true);
                animator.SetFloat(speedHash, 0f);
                animator.SetBool(isSprintingHash, false);
                animator.SetBool(isSlidingHash, false);
                animator.SetBool(isWallRunningHash, false);
                animator.SetBool(isTraversingHash, false);
                animator.SetBool(isDashingHash, false);
            }
        }

        /// <summary>
        /// Resets the death state for respawning.
        /// </summary>
        public void Revive()
        {
            isDead = false;
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.ResetTrigger(deathHash);
                animator.SetBool(isDeadHash, false);
            }

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = defaultCameraLocalPosition;
                cameraTransform.localRotation = defaultCameraLocalRotation;
            }
        }

        public void CastSpell()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(spellCastHash);
                StartCoroutine(SpellCastDelayRoutine());
            }
        }

        public void SetHoldingSpell(bool isHolding)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetBool(isHoldingSpellHash, isHolding);
            }
        }

        private IEnumerator SpellCastDelayRoutine()
        {
            // Wait 10 frames
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            SpellCasted?.Invoke();
        }

        private void OnWallRunStateChanged(bool isRightWall)
        {
            if (animator != null && animator.gameObject.activeInHierarchy && wallRunController.IsWallRunning)
            {
                animator.SetFloat(wallRunSideHash, isRightWall ? 1f : -1f);
            }
        }

        private void OnClimbSmallStarted()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(climbSmallHash);
            }
        }

        private void OnClimbMediumStarted()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(climbMediumHash);
            }
        }

        private void OnClimbLargeStarted()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(climbLargeHash);
            }
        }

        private void OnMantleStarted()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(mantleHash);
            }
        }

        private void OnTraversalEnded()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.ResetTrigger(climbSmallHash);
                animator.ResetTrigger(climbMediumHash);
                animator.ResetTrigger(climbLargeHash);
                animator.ResetTrigger(mantleHash);
            }
        }

        private void Update()
        {
            if (animator == null || !animator.gameObject.activeInHierarchy)
                return;

            if (isDead)
            {
                animator.SetFloat(speedHash, 0f);
                animator.SetBool(isDeadHash, true);

                targetUpperIkWeight = 0f;
                targetLowerIkWeight = 0f;
                if (Mathf.Abs(currentUpperIkWeight - targetUpperIkWeight) > 0.01f)
                {
                    currentUpperIkWeight = Mathf.Lerp(currentUpperIkWeight, 0f, Time.deltaTime * ikBlendSpeed * 3f);
                    SetRigWeights(upperIkTargets, currentUpperIkWeight);
                }
                if (Mathf.Abs(currentLowerIkWeight - targetLowerIkWeight) > 0.01f)
                {
                    currentLowerIkWeight = Mathf.Lerp(currentLowerIkWeight, 0f, Time.deltaTime * ikBlendSpeed * 3f);
                    SetRigWeights(lowerIkTargets, currentLowerIkWeight);
                }
                return;
            }

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

            // 7. Traversal
            bool isTraversing = traversalController != null && traversalController.IsTraversing;
            animator.SetBool(isTraversingHash, isTraversing);

            // 8. Dashing
            bool isDashing = airDashController != null && airDashController.IsDashing;
            animator.SetBool(isDashingHash, isDashing);
        }

        private void UpdateIKWeights()
        {
            bool isSliding = controller.IsSliding;
            bool isWallRunning = wallRunController != null && wallRunController.IsWallRunning;
            bool isTraversing = traversalController != null && traversalController.IsTraversing;

            // Check if ANY layer is currently playing the Kick animation
            bool isKicking = false;
            if (animator != null)
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    if (animator.GetCurrentAnimatorStateInfo(i).IsTag("Kick") ||
                        animator.GetNextAnimatorStateInfo(i).IsTag("Kick"))
                    {
                        isKicking = true;
                        break;
                    }
                }
            }

            // Determine if IK should be overridden by a full body animation
            bool disableAllIk = (isSliding && disableIkDuringSlide) ||
                             (isWallRunning && disableIkDuringWallRun) ||
                             (isTraversing && disableIkDuringTraversal);

            targetUpperIkWeight = disableAllIk ? 0f : 1f;
            targetLowerIkWeight = (disableAllIk || (isKicking && disableIkDuringKick)) ? 0f : 1f;

            float upperIkSpeed = disableAllIk ? ikBlendSpeed : ikBlendSpeed * 3f;
            float lowerIkSpeed = (disableAllIk || (isKicking && disableIkDuringKick)) ? ikBlendSpeed : ikBlendSpeed * 3f;

            if (Mathf.Abs(currentUpperIkWeight - targetUpperIkWeight) > 0.01f)
            {
                currentUpperIkWeight = Mathf.Lerp(currentUpperIkWeight, targetUpperIkWeight, Time.deltaTime * upperIkSpeed);
                SetRigWeights(upperIkTargets, currentUpperIkWeight);
            }

            if (Mathf.Abs(currentLowerIkWeight - targetLowerIkWeight) > 0.01f)
            {
                currentLowerIkWeight = Mathf.Lerp(currentLowerIkWeight, targetLowerIkWeight, Time.deltaTime * lowerIkSpeed);
                SetRigWeights(lowerIkTargets, currentLowerIkWeight);
            }
        }

        private void SetRigWeights(IIKWeightTarget[] targets, float weight)
        {
            if (targets == null || targets.Length == 0) return;
            foreach (var target in targets)
            {
                if (target != null)
                {
                    target.weight = weight;
                }
            }
        }

        private void LateUpdate()
        {
            if (!isDead)
                return;

            if (cameraTransform == null)
                cameraTransform = FindCameraTransform();
            if (headTransform == null)
                headTransform = FindHeadTransform();

            if (cameraTransform != null && headTransform != null)
            {
                cameraTransform.position = Vector3.Lerp(
                    cameraTransform.position,
                    headTransform.position,
                    Time.deltaTime * deathCameraSmoothSpeed
                );

                cameraTransform.rotation = Quaternion.Slerp(
                    cameraTransform.rotation,
                    headTransform.rotation,
                    Time.deltaTime * deathCameraSmoothSpeed
                );
            }
        }

        private Transform FindCameraTransform()
        {
            MouseLookController ml = GetComponentInChildren<MouseLookController>();
            if (ml != null && ml.transform != null) return ml.transform;
            if (Camera.main != null) return Camera.main.transform;
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) return cam.transform;
            return null;
        }

        private Transform FindHeadTransform()
        {
            if (animator != null)
            {
                if (animator.isHuman)
                {
                    Transform humanHead = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (humanHead != null) return humanHead;
                }

                Transform childHead = FindChildRecursive(animator.transform, "head");
                if (childHead != null) return childHead;
            }
            return null;
        }

        private Transform FindChildRecursive(Transform parent, string nameSubstring)
        {
            foreach (Transform child in parent)
            {
                if (child.name.IndexOf(nameSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
                Transform result = FindChildRecursive(child, nameSubstring);
                if (result != null) return result;
            }
            return null;
        }
    }
}