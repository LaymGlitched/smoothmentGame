using System;
using System.Collections;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Dying Light-style "stepped" obstacle traversal:
    ///   - Low obstacles (up to ~hip height) with clear space beyond  -> VAULT (quick hop over)
    ///   - Medium obstacles (up to roughly eye height)                -> MANTLE (grab + pull up)
    ///   - Tall obstacles (up to a bit above the player's head)       -> WALL CLIMB up to the
    ///                                                                    ledge, then auto-mantle
    /// Anything taller than climbMaxHeight is ignored entirely.
    ///
    /// Vault/Mantle auto-trigger by walking into a short/medium obstacle.
    /// Wall Climb triggers on a Jump press while facing a tall obstacle
    /// (grounded or airborne), mirroring "jump at the wall to climb it".
    /// Each sub-behaviour has its own toggle.
    /// </summary>
    public class LedgeTraversalController : MonoBehaviour
    {
        [SerializeField]
        private RigidbodyFPController controller;

        [SerializeField]
        private StaminaSystem stamina; // optional, used only for wall climb cost

        [Header("Toggles")]
        public bool enableVaulting = true;
        public bool enableMantling = true;
        public bool enableWallClimb = true;

        public bool IsTraversing { get; private set; }

        public event Action ClimbSmallStarted;
        public event Action ClimbMediumStarted;
        public event Action ClimbLargeStarted;
        public event Action MantleStarted;
        public event Action TraversalEnded;

        private FPMovementSettings settings;
        private PlayerInputHandler input;
        private float cooldownUntil;

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
            if (IsTraversing || Time.time < cooldownUntil || controller.IsExternallyControlled)
                return;
            if (!enableVaulting && !enableMantling)
                return;
            if (input == null || input.MoveInput.y < 0.5f)
                return;

            if (TryDetectObstacle(out ObstacleInfo obstacle))
            {
                if (obstacle.Height <= settings.vaultMaxHeight)
                {
                    if (enableVaulting && HasClearanceBeyond(obstacle))
                        StartCoroutine(DoVault(obstacle));
                    else if (enableMantling)
                        StartCoroutine(DoMantle(obstacle));
                }
                else if (obstacle.Height <= settings.mantleMaxHeight && enableMantling)
                {
                    StartCoroutine(DoMantle(obstacle));
                }
                // taller obstacles are handled via HandleJumpPressed -> wall climb, not here
            }
        }

        private void HandleJumpPressed()
        {
            if (
                !enableWallClimb
                || IsTraversing
                || Time.time < cooldownUntil
                || controller.IsExternallyControlled
            )
                return;

            if (
                TryDetectObstacle(out ObstacleInfo obstacle)
                && obstacle.Height > settings.mantleMaxHeight
                && obstacle.Height <= settings.climbMaxHeight
            )
            {
                StartCoroutine(DoWallClimb(obstacle));
            }
        }

        // -----------------------------------------------------------------
        // Detection
        // -----------------------------------------------------------------
        private struct ObstacleInfo
        {
            public Vector3 WallHitPoint;
            public Vector3 WallNormal;
            public float TopY;
            public float Height; // above the player's feet
        }

        private bool TryDetectObstacle(out ObstacleInfo obstacle)
        {
            obstacle = default;

            Vector3 origin = controller.FeetPosition + Vector3.up * 0.35f;
            Vector3 dir = controller.Orientation.forward;

            if (
                !Physics.Raycast(
                    origin,
                    dir,
                    out RaycastHit wallHit,
                    settings.ledgeCheckDistance,
                    settings.wallMask,
                    QueryTriggerInteraction.Ignore
                )
            )
                return false;

            // near-vertical surfaces only (walls, not ramps)
            if (Vector3.Angle(wallHit.normal, Vector3.up) < 60f)
                return false;

            float feetY = controller.FeetPosition.y;
            float maxProbeY = feetY + settings.climbMaxHeight + 0.3f;
            Vector3 downOrigin = wallHit.point + dir * 0.15f + Vector3.up * (maxProbeY - feetY);

            if (
                !Physics.Raycast(
                    downOrigin,
                    Vector3.down,
                    out RaycastHit topHit,
                    settings.climbMaxHeight + 0.5f,
                    settings.wallMask,
                    QueryTriggerInteraction.Ignore
                )
            )
                return false; // no ledge found within climbable range - too tall or open air, ignore

            float height = topHit.point.y - feetY;
            if (height <= 0.15f)
                return false; // not really an obstacle (curb / step noise)

            // Prevent climbing through thin walls/ceilings (e.g. inside caves with space behind them)
            // 1. Check if there is a ceiling directly above the player that blocks the climb
            Vector3 headTop = controller.transform.position + Vector3.up * (controller.ColliderHeight * 0.5f);
            if (topHit.point.y > headTop.y)
            {
                float climbDist = topHit.point.y - headTop.y + 0.1f;
                if (Physics.Raycast(
                        headTop,
                        Vector3.up,
                        climbDist,
                        settings.wallMask,
                        QueryTriggerInteraction.Ignore))
                {
                    return false; // Blocked by ceiling above player
                }
            }

            // 2. Check if there is a solid wall blocking horizontal movement AT the height of the ledge.
            // This prevents detecting a "ledge" that is actually the floor behind a thin wall.
            Vector3 highOrigin = controller.FeetPosition;
            highOrigin.y = topHit.point.y + 0.1f; // Just above the detected ledge

            Vector3 toLedge = (topHit.point - highOrigin);
            toLedge.y = 0;
            float distToLedge = toLedge.magnitude;

            if (distToLedge > 0.01f)
            {
                if (Physics.Raycast(
                        highOrigin,
                        toLedge.normalized,
                        out RaycastHit blockHit,
                        distToLedge,
                        settings.wallMask,
                        QueryTriggerInteraction.Ignore))
                {
                    return false; // Ledge is behind a solid wall (e.g. clipping through a cave mesh)
                }
            }

            obstacle = new ObstacleInfo
            {
                WallHitPoint = wallHit.point,
                WallNormal = wallHit.normal,
                TopY = topHit.point.y,
                Height = height,
            };
            return true;
        }

        private bool HasClearanceBeyond(ObstacleInfo obstacle)
        {
            Vector3 checkOrigin =
                obstacle.WallHitPoint
                - obstacle.WallNormal * 0.1f
                + Vector3.up * 0.4f
                + controller.Orientation.forward * (settings.vaultClearanceDistance * 0.05f);
            // cast forward past the wall at just-above-ledge height - if something solid is there, it's too thick to vault
            return !Physics.Raycast(
                checkOrigin,
                controller.Orientation.forward,
                settings.vaultClearanceDistance,
                settings.wallMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private Vector3 LandingPosition(ObstacleInfo obstacle)
        {
            Vector3 horizontal =
                obstacle.WallHitPoint
                - obstacle.WallNormal * -0.05f
                + controller.Orientation.forward * settings.ledgeLandingOffset;
            horizontal.y = obstacle.TopY + controller.ColliderHeight * 0.5f + 0.02f;
            return horizontal;
        }

        // -----------------------------------------------------------------
        // Vault - quick arcing hop over a low obstacle
        // -----------------------------------------------------------------
        private IEnumerator DoVault(ObstacleInfo obstacle)
        {
            IsTraversing = true;
            ClimbSmallStarted?.Invoke();
            
            float entrySpeed = controller.HorizontalVelocity.magnitude;
            controller.BeginExternalControl();

            Rigidbody rb = controller.Body;
            Vector3 start = rb.position;
            
            // Phase 1: Vertical Climb (Small)
            float climbDuration = settings.vaultDuration * 0.4f;
            Vector3 climbEnd = new Vector3(start.x, obstacle.TopY + controller.ColliderHeight * 0.5f - 0.2f, start.z);
            yield return MoveLinear(start, climbEnd, climbDuration);

            // Phase 2: Mantle
            MantleStarted?.Invoke();
            Vector3 end = LandingPosition(obstacle);
            float apexY = Mathf.Max(rb.position.y, end.y) + 0.2f;
            yield return MoveArc(rb.position, end, apexY, settings.vaultDuration * 0.6f);

            if (entrySpeed > 0.01f)
            {
                rb.linearVelocity = controller.Orientation.forward * entrySpeed;
            }

            FinishTraversal();
        }

        // -----------------------------------------------------------------
        // Mantle - grab the ledge and pull straight up and over
        // -----------------------------------------------------------------
        private IEnumerator DoMantle(ObstacleInfo obstacle)
        {
            IsTraversing = true;
            ClimbMediumStarted?.Invoke();
            
            float entrySpeed = controller.HorizontalVelocity.magnitude;
            controller.BeginExternalControl();

            Rigidbody rb = controller.Body;
            Vector3 start = rb.position;
            
            // Phase 1: Vertical Climb (Medium)
            float climbDuration = settings.mantleDuration * 0.4f;
            Vector3 climbEnd = new Vector3(start.x, obstacle.TopY + controller.ColliderHeight * 0.5f - 0.2f, start.z);
            yield return MoveLinear(start, climbEnd, climbDuration);

            // Phase 2: Mantle
            MantleStarted?.Invoke();
            Vector3 end = LandingPosition(obstacle);
            float apexY = end.y + 0.1f;
            yield return MoveArc(rb.position, end, apexY, settings.mantleDuration * 0.6f);

            // Mantle is harder than vault, preserve 80% of momentum
            if (entrySpeed > 0.01f)
            {
                rb.linearVelocity = controller.Orientation.forward * (entrySpeed * 0.8f);
            }

            FinishTraversal();
        }

        // -----------------------------------------------------------------
        // Wall Climb - ascend the wall face at a steady speed until level
        // with the ledge, then finish with a short mantle onto the top
        // -----------------------------------------------------------------
        private IEnumerator DoWallClimb(ObstacleInfo obstacle)
        {
            IsTraversing = true;
            ClimbLargeStarted?.Invoke();

            float entrySpeed = controller.HorizontalVelocity.magnitude;
            controller.BeginExternalControl();

            Rigidbody rb = controller.Body;
            float targetTopFeetY = obstacle.TopY; // feet should reach this before the final mantle
            Vector3 wallHugPos =
                obstacle.WallHitPoint + obstacle.WallNormal * controller.ColliderRadius * 1.05f;

            bool useStamina = enableWallClimb && stamina != null;

            while (true)
            {
                float currentFeetY = rb.position.y - controller.ColliderHeight * 0.5f;
                if (currentFeetY >= targetTopFeetY - 0.15f)
                    break;

                if (useStamina)
                {
                    if (stamina.Depleted)
                        break; // ran out of stamina mid-climb, drop off
                    stamina.Drain(
                        Time.fixedDeltaTime
                            * (
                                settings.wallClimbStaminaPerSecond
                                / Mathf.Max(1f, settings.staminaDrainPerSecond)
                            )
                    );
                }

                Vector3 next =
                    rb.position + Vector3.up * (settings.wallClimbSpeed * Time.fixedDeltaTime);
                next.x = wallHugPos.x;
                next.z = wallHugPos.z;
                rb.MovePosition(next);

                yield return new WaitForFixedUpdate();
            }

            // final push up onto the ledge
            MantleStarted?.Invoke();
            Vector3 start = rb.position;
            Vector3 end = LandingPosition(obstacle);
            yield return MoveArc(start, end, end.y + 0.1f, settings.mantleDuration);

            if (settings.preserveMomentumAfterWallClimb && entrySpeed > 0.01f)
            {
                Vector3 exitDir = controller.Orientation.forward;
                rb.linearVelocity = exitDir * (entrySpeed * settings.wallClimbExitSpeedMultiplier);
            }

            FinishTraversal();
        }

        // -----------------------------------------------------------------
        // Shared arc mover
        // -----------------------------------------------------------------
        private IEnumerator MoveArc(Vector3 start, Vector3 end, float apexY, float duration)
        {
            Rigidbody rb = controller.Body;
            float t = 0f;

            while (t < duration)
            {
                t += Time.fixedDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = p * p * (3f - 2f * p); // smoothstep

                Vector3 pos = Vector3.Lerp(start, end, eased);
                float arc =
                    Mathf.Sin(eased * Mathf.PI) * (apexY - Mathf.Lerp(start.y, end.y, eased));
                pos.y = Mathf.Lerp(start.y, end.y, eased) + Mathf.Max(0f, arc);

                rb.MovePosition(pos);
                yield return new WaitForFixedUpdate();
            }

            rb.MovePosition(end);
        }

        private IEnumerator MoveLinear(Vector3 start, Vector3 end, float duration)
        {
            Rigidbody rb = controller.Body;
            float t = 0f;

            while (t < duration)
            {
                t += Time.fixedDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = p * p * (3f - 2f * p);

                rb.MovePosition(Vector3.Lerp(start, end, eased));
                yield return new WaitForFixedUpdate();
            }

            rb.MovePosition(end);
        }

        private void FinishTraversal()
        {
            controller.EndExternalControl();
            IsTraversing = false;
            cooldownUntil = Time.time + settings.ledgeTraversalCooldown;
            TraversalEnded?.Invoke();
        }
    }
}
