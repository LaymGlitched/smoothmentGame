// © 2025 Nanodogs Studios. All rights reserved.

using UnityEngine;
using UnityEngine.InputSystem;

namespace Nanodogs.UniversalScripts
{
    /// <summary>
    /// Handles first-person camera rotation with separate yaw pivot for smooth look.
    /// </summary>
    public class FirstPersonPlayerCamera : MonoBehaviour
    {
        [Header("Input")]
        public InputActionReference lookAction;
        public InputActionReference moveAction;

        [Header("Rotation Settings")]
        public float mouseSensitivity = 100f;
        public float gamepadSensitivity = 200f;
        public float smoothTime = 0.05f;
        [Tooltip("Assign an empty GameObject used for yaw (Y-axis rotation).")]
        public Transform yawPivot;

        private float xRotation = 0f;
        private Vector2 smoothedDelta;
        private Vector2 targetDelta;

        [Header("Head Bob Settings")]
        public bool enableHeadBob = true;
        public float bobFrequency = 2.0f;
        public float bobAmplitude = 0.05f;
        public float bobFadeSpeed = 6f;
        public float bobStartThreshold = 0.1f;
        public AnimationCurve bobCurveX;
        public AnimationCurve bobCurveY;

        private float bobTimer;
        private Vector3 initialCameraLocalPos;
        private float bobWeight;

        [Header("Landing Bob Settings")]
        public bool enableLandingBob = true;
        public float landingBobAmount = 0.1f;
        public float landingBobSpeed = 10f;
        public float landingBobReturnSpeed = 6f;
        public float landingVelocityThreshold = -4f;
        public Rigidbody playerRigidbody;

        private bool wasGroundedLastFrame;
        private float landingOffsetY;
        private float landingVelocity;
        private float landingLerpVelocity;

        [Header("Ground Check (for landing bob)")]
        public LayerMask groundMask;
        public float groundCheckDistance = 0.2f;
        public Transform groundCheckOrigin;

        private void OnEnable()
        {
            if (lookAction != null) lookAction.action.Enable();
            if (moveAction != null) moveAction.action.Enable();
        }

        private void OnDisable()
        {
            if (lookAction != null) lookAction.action.Disable();
            if (moveAction != null) moveAction.action.Disable();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            initialCameraLocalPos = transform.localPosition;
        }

        private void Update()
        {
            HandleLook();
            HandleHeadBob();
            HandleLandingBob();
        }

        private void HandleLook()
        {
            if (lookAction == null || yawPivot == null) return;

            // Determine sensitivity based on input device
            float sensitivity = mouseSensitivity;
            var lastControl = lookAction.action.activeControl;
            if (lastControl != null)
            {
                if (lastControl.device is Gamepad)
                    sensitivity = gamepadSensitivity;
                else if (lastControl.device is Mouse)
                    sensitivity = mouseSensitivity;
            }

            Vector2 rawDelta = lookAction.action.ReadValue<Vector2>() * sensitivity;
            float smoothFactor = 1f - Mathf.Exp(-smoothTime * 60f * Time.deltaTime);
            targetDelta = rawDelta;
            smoothedDelta = Vector2.Lerp(smoothedDelta, targetDelta, smoothFactor);

            Vector2 finalDelta = smoothedDelta * Time.deltaTime;

            // Pitch (X) on the camera itself
            xRotation -= finalDelta.y;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            // Yaw (Y) on separate pivot, not Rigidbody
            yawPivot.Rotate(Vector3.up * finalDelta.x);
        }

        private void HandleHeadBob()
        {
            if (!enableHeadBob || moveAction == null) return;

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            bool isMoving = moveInput.magnitude > bobStartThreshold;

            float targetWeight = isMoving ? 1f : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, targetWeight, bobFadeSpeed * Time.deltaTime);

            Vector3 basePos = initialCameraLocalPos;

            if (bobWeight > 0.001f)
            {
                bobTimer += Time.deltaTime * bobFrequency;
                if (bobTimer > 1f) bobTimer -= 1f;

                float xOffset = bobCurveX.Evaluate(bobTimer) * bobAmplitude * bobWeight;
                float yOffset = bobCurveY.Evaluate(bobTimer) * bobAmplitude * bobWeight;

                basePos += new Vector3(xOffset, yOffset, 0f);
            }

            basePos.y += landingOffsetY;
            transform.localPosition = Vector3.Lerp(transform.localPosition, basePos, bobFadeSpeed * Time.deltaTime);
        }

        private void HandleLandingBob()
        {
            if (!enableLandingBob || playerRigidbody == null || groundCheckOrigin == null) return;

            bool isGrounded = Physics.Raycast(
                groundCheckOrigin.position,
                Vector3.down,
                groundCheckDistance,
                groundMask
            );

            if (!wasGroundedLastFrame && isGrounded)
            {
                if (playerRigidbody.linearVelocity.y < landingVelocityThreshold)
                {
                    landingVelocity = Mathf.Abs(playerRigidbody.linearVelocity.y);
                    float intensity = Mathf.Clamp01(landingVelocity / Mathf.Abs(landingVelocityThreshold));
                    landingOffsetY = -landingBobAmount * intensity;
                }
            }

            landingOffsetY = Mathf.SmoothDamp(
                landingOffsetY,
                0f,
                ref landingLerpVelocity,
                1f / landingBobReturnSpeed
            );

            wasGroundedLastFrame = isGrounded;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheckOrigin != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(groundCheckOrigin.position, groundCheckOrigin.position + Vector3.down * groundCheckDistance);
            }
        }
    }
}
