using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPMovement
{
    /// <summary>
    /// Thin wrapper around the New Input System. Everything else in the
    /// controller talks to THIS class rather than to InputActions directly,
    /// so swapping control schemes never touches movement code.
    ///
    /// Setup: Create an Input Actions asset with an action map containing
    /// Move (Vector2), Look (Vector2), Jump (Button), Sprint (Button),
    /// Crouch (Button). Drag each action onto the matching slot below.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Input Actions (assign from your .inputactions asset)")]
        [SerializeField]
        private InputActionReference moveAction;

        [SerializeField]
        private InputActionReference lookAction;

        [SerializeField]
        private InputActionReference jumpAction;

        [SerializeField]
        private InputActionReference sprintAction;

        [SerializeField]
        private InputActionReference crouchAction;

        [SerializeField]
        private InputActionReference kickAction;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }

        /// <summary>Fired the frame Jump is pressed. Controller buffers this itself.</summary>
        public event Action OnJumpPressed;

        /// <summary>Fired the frame Sprint is pressed. Useful for actions like Air Dash.</summary>
        public event Action OnSprintPressed;

        /// <summary>Fired the frame Kick is pressed.</summary>
        public event Action OnKickPressed;

        private void OnEnable()
        {
            Enable(moveAction);
            Enable(lookAction);
            Enable(crouchAction);
            Enable(kickAction);

            if (sprintAction != null && sprintAction.action != null)
            {
                sprintAction.action.Enable();
                sprintAction.action.performed += HandleSprintPerformed;
            }

            if (jumpAction != null && jumpAction.action != null)
            {
                jumpAction.action.Enable();
                jumpAction.action.performed += HandleJumpPerformed;
            }

            if (kickAction != null && kickAction.action != null)
            {
                kickAction.action.Enable();
                kickAction.action.performed += HandleKickPerformed;
            }
        }

        private void OnDisable()
        {
            Disable(moveAction);
            Disable(lookAction);
            Disable(crouchAction);
            Disable(kickAction);

            if (sprintAction != null && sprintAction.action != null)
            {
                sprintAction.action.performed -= HandleSprintPerformed;
                sprintAction.action.Disable();
            }

            if (jumpAction != null && jumpAction.action != null)
            {
                jumpAction.action.performed -= HandleJumpPerformed;
                jumpAction.action.Disable();
            }

            if (kickAction != null && kickAction.action != null)
            {
                kickAction.action.performed -= HandleKickPerformed;
                kickAction.action.Disable();
            }
        }

        private void Update()
        {
            MoveInput =
                moveAction != null && moveAction.action != null
                    ? moveAction.action.ReadValue<Vector2>()
                    : Vector2.zero;

            LookInput =
                lookAction != null && lookAction.action != null
                    ? lookAction.action.ReadValue<Vector2>()
                    : Vector2.zero;

            SprintHeld =
                sprintAction != null
                && sprintAction.action != null
                && sprintAction.action.IsPressed();
            CrouchHeld =
                crouchAction != null
                && crouchAction.action != null
                && crouchAction.action.IsPressed();
        }

        private void HandleJumpPerformed(InputAction.CallbackContext ctx) =>
            OnJumpPressed?.Invoke();

        private void HandleSprintPerformed(InputAction.CallbackContext ctx) =>
            OnSprintPressed?.Invoke();

        private void HandleKickPerformed(InputAction.CallbackContext ctx) =>
            OnKickPressed?.Invoke();

        private static void Enable(InputActionReference reference) => reference?.action?.Enable();

        private static void Disable(InputActionReference reference) => reference?.action?.Disable();
    }
}
