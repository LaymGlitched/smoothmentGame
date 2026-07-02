using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkourMovement
{
    public class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Transform playerBody;

        [SerializeField]
        private Camera playerCamera;

        [SerializeField]
        private PlayerMovement playerMovement;

        [Header("Input")]
        [SerializeField]
        private InputActionReference lookAction;

        [Header("Camera Settings")]
        [SerializeField]
        private float cameraHeight = 1.7f;

        [Header("Sensitivity")]
        [SerializeField]
        private float horizontalSensitivity = 2f;

        [SerializeField]
        private float verticalSensitivity = 2f;

        [SerializeField]
        private bool invertY = false;

        [Header("Smoothing")]
        [SerializeField]
        private bool useSmoothing = true;

        [SerializeField]
        private float smoothTime = 0.05f;

        [Header("Constraints")]
        [SerializeField]
        private float minPitch = -90f;

        [SerializeField]
        private float maxPitch = 90f;

        [Header("FOV")]
        [SerializeField]
        private float defaultFOV = 90f;

        [SerializeField]
        private float sprintFOV = 100f;

        [SerializeField]
        private float fovLerpSpeed = 8f;

        // Rotation state
        private float xRotation = 0f;
        private float yRotation = 0f;

        // Smoothing
        private Vector2 smoothVelocity;
        private Vector2 currentLookVelocity;

        // FOV
        private float currentFOV;
        private float targetFOV;

        // Cursor lock state
        private bool isCursorLocked = true;

        private void Awake()
        {
            // Auto-find references if not set
            if (playerCamera == null)
                playerCamera = GetComponent<Camera>();
            if (playerBody == null)
                playerBody = transform.parent;
            if (playerMovement == null)
                playerMovement = GetComponentInParent<PlayerMovement>();

            // Set initial FOV
            currentFOV = defaultFOV;
            targetFOV = defaultFOV;

            if (playerCamera != null)
                playerCamera.fieldOfView = defaultFOV;
        }

        private void Start()
        {
            // Set camera at correct height
            transform.localPosition = new Vector3(0f, cameraHeight, 0f);

            // Lock cursor immediately
            LockCursor();
        }

        private void OnEnable()
        {
            // Enable input
            if (lookAction != null)
                lookAction.action.Enable();

            // Re-lock cursor when enabled
            LockCursor();
        }

        private void OnDisable()
        {
            // Disable input
            if (lookAction != null)
                lookAction.action.Disable();

            // Only unlock cursor if this component is being destroyed/disabled permanently
            // NOT during scene reloads or play mode changes
        }

        private void OnDestroy()
        {
            // Unlock cursor only when object is destroyed
            UnlockCursor();
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.isPressed)
            {
                ToggleCursorLock();
            }

            // Only process look input if cursor is locked
            if (isCursorLocked)
            {
                UpdateLookInput();
            }

            // Update FOV (visual only, safe in Update)
            UpdateFOV();
        }

        private void LateUpdate()
        {
            // Apply camera rotation in LateUpdate to match Rigidbody interpolation
            if (isCursorLocked)
            {
                ApplyRotation();
            }
        }

        private void UpdateLookInput()
        {
            if (lookAction == null)
                return;

            // Read raw input
            Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

            // Deadzone to prevent drift
            if (lookInput.magnitude < 0.01f)
                return;

            // Apply sensitivity
            float mouseX = lookInput.x * horizontalSensitivity;
            float mouseY = lookInput.y * verticalSensitivity * (invertY ? -1f : 1f);

            if (useSmoothing)
            {
                // Smooth the input using SmoothDamp
                currentLookVelocity = Vector2.SmoothDamp(
                    currentLookVelocity,
                    new Vector2(mouseX, mouseY),
                    ref smoothVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );
            }
            else
            {
                // Use raw input
                currentLookVelocity = new Vector2(mouseX, mouseY);
            }

            // Calculate rotation
            xRotation -= currentLookVelocity.y * Time.deltaTime * 50f; // Scale for proper feel
            xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

            yRotation += currentLookVelocity.x * Time.deltaTime * 50f; // Scale for proper feel

            // Keep yRotation normalized
            if (yRotation > 360f)
                yRotation -= 360f;
            if (yRotation < -360f)
                yRotation += 360f;
        }

        private void ApplyRotation()
        {
            // Apply camera pitch (vertical look)
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            // Apply body yaw (horizontal look)
            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
        }

        private void UpdateFOV()
        {
            if (playerCamera == null)
                return;

            targetFOV =
                (playerMovement != null && playerMovement.IsSprinting) ? sprintFOV : defaultFOV;
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovLerpSpeed);
            playerCamera.fieldOfView = currentFOV;
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            isCursorLocked = true;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isCursorLocked = false;
        }

        private void ToggleCursorLock()
        {
            if (isCursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

        // For repositioning camera
        public void SetCameraHeight(float height)
        {
            cameraHeight = height;
            transform.localPosition = new Vector3(0f, cameraHeight, 0f);
        }

        // Public accessor for movement direction
        public Vector3 GetLookDirection()
        {
            return transform.forward;
        }

        public Vector3 GetMoveDirection(Vector2 input)
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Project to horizontal plane for ground movement
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }
    }
}
