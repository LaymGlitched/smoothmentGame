using System;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Provides an air-dash mechanic. When pressing sprint mid-air, the player
    /// is pushed forward (or in their input direction) with a temporary FOV boost.
    /// </summary>
    [RequireComponent(typeof(RigidbodyFPController))]
    public class AirDashController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private FPMovementSettings settings;
        
        [Tooltip("Optional. If empty, it will attempt to find one in the scene.")]
        [SerializeField]
        private DynamicFOVController fovController;

        [Header("Feature Toggles")]
        public bool enableAirDash = true;

        public event Action OnAirDash;

        private RigidbodyFPController controller;
        private Rigidbody rb;
        private PlayerInputHandler input;

        private float lastDashTime = -999f;
        private float currentFovOffset = 0f;
        private bool isInitialized;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            if (controller == null)
            {
                Debug.LogError("AirDashController: RigidbodyFPController not found!");
                enabled = false;
                return;
            }

            if (settings == null)
            {
                Debug.LogWarning("AirDashController: FPMovementSettings not assigned, trying to get it from controller.");
                settings = controller.Settings;
            }
        }

        private void Start()
        {
            InitializeReferences();
        }

        private void InitializeReferences()
        {
            if (isInitialized) return;

            rb = controller.Body;
            input = controller.Input;

            if (fovController == null)
            {
                fovController = FindObjectOfType<DynamicFOVController>();
            }

            isInitialized = true;

            // We must subscribe here if input wasn't ready in OnEnable
            if (input != null)
            {
                input.OnSprintPressed += HandleSprintPressed;
            }
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.OnSprintPressed += HandleSprintPressed;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.OnSprintPressed -= HandleSprintPressed;
            }
        }

        private void HandleSprintPressed()
        {
            if (!isInitialized) return;
            TryAirDash();
        }

        private void TryAirDash()
        {
            if (!enableAirDash || settings == null || controller == null || rb == null)
                return;

            if (controller.IsExternallyControlled)
                return; // E.g., currently wall-running or mantling

            if (controller.IsGrounded)
                return; // Only dash in the air

            if (Time.time - lastDashTime < settings.airDashCooldown)
                return; // On cooldown

            PerformAirDash();
        }

        private void PerformAirDash()
        {
            lastDashTime = Time.time;

            // Determine dash direction
            Vector3 dashDir = controller.Orientation.forward;
            if (input != null && input.MoveInput.sqrMagnitude > 0.01f)
            {
                dashDir = (controller.Orientation.forward * input.MoveInput.y + controller.Orientation.right * input.MoveInput.x).normalized;
            }

            // Zero out current velocity in the dash direction to prevent compounding speeds inconsistently,
            // or just set the velocity directly for a snappy feel.
            // We'll set the horizontal velocity and apply a slight vertical bump.
            Vector3 newVelocity = dashDir * settings.airDashForce;
            
            // Apply upward force
            newVelocity.y = rb.linearVelocity.y > 0 ? rb.linearVelocity.y : 0f; // Keep upward momentum if already going up, otherwise reset fall speed
            newVelocity.y += settings.airDashUpwardForce;

            rb.linearVelocity = newVelocity;

            // Trigger FOV boost
            currentFovOffset = settings.airDashFovAdd;

            OnAirDash?.Invoke();
        }

        private void Update()
        {
            if (settings == null) return;

            // Smoothly decay the FOV offset
            if (currentFovOffset > 0.01f)
            {
                currentFovOffset = Mathf.Lerp(currentFovOffset, 0f, Time.deltaTime * settings.airDashFovTransitionSpeed);
            }
            else
            {
                currentFovOffset = 0f;
            }

            // Apply to DynamicFOVController if it exists
            if (fovController != null)
            {
                // This handles the FOV kick directly by taking over the ExternalFovOffset.
                // In a more complex game, multiple systems might need a prioritized FOV system,
                // but this works perfectly for a simple setup.
                fovController.ExternalFovOffset = currentFovOffset;
            }
        }
    }
}
