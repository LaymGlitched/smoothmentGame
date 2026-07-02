using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Applies mouse/stick look input. Yaw rotates the body (orientation),
    /// pitch rotates only the camera. Both are smoothed with SmoothDamp so
    /// look feels fluid rather than 1:1 twitchy - tweak lookSmoothTime to taste.
    /// </summary>
    public class MouseLookController : MonoBehaviour
    {
        [SerializeField]
        private FPMovementSettings settings;

        [SerializeField]
        private PlayerInputHandler input;

        [SerializeField]
        private Transform orientation; // yaw pivot, same one the controller uses

        [SerializeField]
        private Transform cameraTransform; // pitch pivot, e.g. the Camera itself

        [SerializeField]
        private RigidbodyFPController controller; // for slide height offset

        public bool enableLook = true;

        /// <summary>Other systems (e.g. WallRunController) set this to bank the camera; 0 = level.</summary>
        public float TargetRoll { get; set; }

        private float currentYaw;
        private float currentPitch;
        private float currentRoll;
        private float yawVelocity;
        private float pitchVelocity;
        private float targetYaw;
        private float targetPitch;
        private Vector3 defaultCameraLocalPosition;

        private void Start()
        {
            currentYaw = targetYaw = orientation.eulerAngles.y;
            currentPitch = targetPitch = cameraTransform.localEulerAngles.x;
            defaultCameraLocalPosition = cameraTransform.localPosition;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!enableLook || input == null)
                return;

            Vector2 look = input.LookInput * settings.mouseSensitivity * Time.deltaTime;

            targetYaw += look.x;
            targetPitch -= look.y;
            targetPitch = Mathf.Clamp(targetPitch, settings.minPitch, settings.maxPitch);

            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawVelocity,
                settings.lookSmoothTime
            );
            currentPitch = Mathf.SmoothDampAngle(
                currentPitch,
                targetPitch,
                ref pitchVelocity,
                settings.lookSmoothTime
            );
            currentRoll = Mathf.Lerp(
                currentRoll,
                TargetRoll,
                Time.deltaTime * settings.cameraRollLerpSpeed
            );

            orientation.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(currentPitch, 0f, currentRoll);

            // Apply slide height offset to camera position
            if (controller != null)
            {
                Vector3 slideOffset = controller.GetSlideHeightOffset();
                Vector3 targetPosition = defaultCameraLocalPosition + slideOffset;
                cameraTransform.localPosition = Vector3.Lerp(
                    cameraTransform.localPosition,
                    targetPosition,
                    Time.deltaTime * settings.slideHeightTransitionSpeed
                );
            }
        }
    }
}
