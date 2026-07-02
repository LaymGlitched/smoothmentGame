using System;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Titanfall-style wall running. While airborne, moving forward, and
    /// beside a wall, gravity is reduced and horizontal velocity is bent to
    /// run along the wall surface. Jumping while attached kicks the player
    /// away from the wall and up. Fully independent toggle - if turned off
    /// (or this component is absent) nothing else in the system changes.
    /// </summary>
    [RequireComponent(typeof(WallDetector))]
    public class WallRunController : MonoBehaviour
    {
        [SerializeField]
        private RigidbodyFPController controller;

        [SerializeField]
        private MouseLookController mouseLook; // optional, for camera tilt

        public bool enableWallRun = true;

        public bool IsWallRunning { get; private set; }
        public event Action<bool> WallRunStateChanged; // true = right wall, false = left wall

        private WallDetector detector;
        private FPMovementSettings settings;
        private PlayerInputHandler input;

        private float wallRunTimer;
        private float lastLeftWallTime = -999f;
        private float lastRightWallTime = -999f;
        private Vector3 currentWallNormal;
        private int currentSide; // +1 right, -1 left, 0 none

        private void Awake()
        {
            detector = GetComponent<WallDetector>();
        }

        private void Start()
        {
            settings = controller.Settings;
            input = controller.Input;
            if (input != null)
                input.OnJumpPressed += HandleJumpPressed;
        }

        private void OnDestroy()
        {
            if (input != null)
                input.OnJumpPressed -= HandleJumpPressed;
        }

        private void FixedUpdate()
        {
            if (!enableWallRun)
            {
                if (IsWallRunning)
                    StopWallRun();
                return;
            }

            if (IsWallRunning)
                ContinueWallRun();
            else
                TryStartWallRun();
        }

        private void TryStartWallRun()
        {
            if (controller.IsGrounded || controller.IsExternallyControlled)
                return;
            if (input == null || input.MoveInput.y < settings.wallRunMinForwardInput)
                return;
            if (controller.Body.linearVelocity.y > 8f)
                return; // don't grab a wall while rocketing upward off a jump

            detector.Probe(controller.Orientation, settings.wallCheckDistance, settings.wallMask);

            bool canUseRight =
                detector.RightWall && Time.time - lastRightWallTime > settings.wallReattachCooldown;
            bool canUseLeft =
                detector.LeftWall && Time.time - lastLeftWallTime > settings.wallReattachCooldown;

            if (canUseRight)
            {
                StartWallRun(1, detector.RightNormal);
            }
            else if (canUseLeft)
            {
                StartWallRun(-1, detector.LeftNormal);
            }
        }

        private void StartWallRun(int side, Vector3 wallNormal)
        {
            currentSide = side;
            currentWallNormal = wallNormal;
            wallRunTimer = 0f;
            IsWallRunning = true;

            controller.BeginExternalControl(zeroVelocity: false);

            // preserve forward speed, just redirect it onto the wall plane
            Vector3 flatVel = controller.HorizontalVelocity;
            float speed = Mathf.Max(flatVel.magnitude, settings.wallRunSpeed * 0.6f);
            Vector3 wallForward = GetWallRunDirection(wallNormal);
            controller.Body.linearVelocity =
                wallForward * speed + Vector3.up * Mathf.Max(controller.Body.linearVelocity.y, 0f);

            WallRunStateChanged?.Invoke(side > 0);
            if (mouseLook != null)
                mouseLook.TargetRoll = settings.wallRunCameraTilt * side;
        }

        private void ContinueWallRun()
        {
            wallRunTimer += Time.fixedDeltaTime;

            detector.Probe(
                controller.Orientation,
                settings.wallCheckDistance * 1.3f,
                settings.wallMask
            );
            bool stillOnWall = currentSide > 0 ? detector.RightWall : detector.LeftWall;
            Vector3 latestNormal = currentSide > 0 ? detector.RightNormal : detector.LeftNormal;

            bool inputStillForward = input != null && input.MoveInput.y >= 0f;
            bool timeUp = wallRunTimer >= settings.maxWallRunDuration;

            if (controller.IsGrounded || !stillOnWall || !inputStillForward || timeUp)
            {
                StopWallRun();
                return;
            }

            currentWallNormal = latestNormal;
            Vector3 wallForward = GetWallRunDirection(currentWallNormal);

            Vector3 vel = controller.Body.linearVelocity;
            Vector3 horizontal = wallForward * settings.wallRunSpeed;
            // slight pull into the wall so the player hugs it instead of drifting off
            Vector3 stick = -currentWallNormal * 1.5f;

            float verticalVel =
                vel.y + Physics.gravity.y * settings.wallRunGravityScale * Time.fixedDeltaTime;
            verticalVel = Mathf.Max(verticalVel, -settings.wallRunSpeed * 0.5f); // don't let it snowball into a fast fall

            controller.Body.linearVelocity = horizontal + stick + Vector3.up * verticalVel;

            if (mouseLook != null)
                mouseLook.TargetRoll = settings.wallRunCameraTilt * currentSide;
        }

        private void HandleJumpPressed()
        {
            if (!IsWallRunning)
                return;

            Vector3 jumpVel =
                currentWallNormal * settings.wallJumpAwayForce
                + Vector3.up * settings.wallJumpUpForce;
            StopWallRun();
            controller.Body.linearVelocity =
                new Vector3(jumpVel.x, jumpVel.y, jumpVel.z) + controller.HorizontalVelocity * 0.3f;
        }

        private void StopWallRun()
        {
            if (!IsWallRunning)
                return;

            if (currentSide > 0)
                lastRightWallTime = Time.time;
            else if (currentSide < 0)
                lastLeftWallTime = Time.time;

            IsWallRunning = false;
            controller.EndExternalControl();
            WallRunStateChanged?.Invoke(false);
            if (mouseLook != null)
                mouseLook.TargetRoll = 0f;

            currentSide = 0;
        }

        private Vector3 GetWallRunDirection(Vector3 wallNormal)
        {
            // direction along the wall, biased toward where the player is facing/moving
            Vector3 along = Vector3.Cross(wallNormal, Vector3.up).normalized;
            if (Vector3.Dot(along, controller.Orientation.forward) < 0f)
                along = -along;
            return along;
        }
    }
}
