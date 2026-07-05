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

            // Momentum-preserving dash: add dash force to current velocity instead of replacing it
            Vector3 hv = controller.HorizontalVelocity;
            float currentSpeed = hv.magnitude;

            Vector3 addedVelocity = dashDir * settings.airDashForce;
            Vector3 newVelocity = hv + addedVelocity;
            float newSpeed = newVelocity.magnitude;

            // Soft cap: If we are already going fast, don't let the dash stack speed infinitely.
            // But if we are already above the cap (e.g. from a fast slide-jump), don't slow us down either.
            float maxSpeed = settings.airDashForce * 1.5f; // Sensible soft cap based on dash force
            if (newSpeed > maxSpeed)
            {
                float cap = Mathf.Max(currentSpeed, maxSpeed);
                newVelocity = newVelocity.normalized * cap;
            }

            // Preserve 50% of downward velocity instead of zeroing it completely
            float currentY = rb.linearVelocity.y;
            if (currentY < 0)
            {
                newVelocity.y = (currentY * 0.5f) + settings.airDashUpwardForce;
            }
            else
            {
                newVelocity.y = currentY + settings.airDashUpwardForce;
            }

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
