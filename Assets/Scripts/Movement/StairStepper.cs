using UnityEngine;

namespace ParkourMovement
{
    [RequireComponent(typeof(Rigidbody))]
    public class StairStepper : MonoBehaviour
    {
        [Header("Step Detection")]
        [SerializeField]
        private float maxStepHeight = 0.4f;

        [SerializeField]
        private float stepSearchDistance = 0.5f;

        [SerializeField]
        private float stepSmoothingSpeed = 20f;

        [SerializeField]
        private float lowerRayHeight = 0.1f;

        [SerializeField]
        private float upperRayHeight = 0.65f;

        [Header("Layer Configuration")]
        [SerializeField]
        private LayerMask stepLayers = -1;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugRays = true;

        private Rigidbody rb;
        private GroundDetector groundDetector;
        private bool isStepping;
        private float stepProgress;
        private Vector3 stepStartPosition;
        private Vector3 targetStepPosition;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundDetector = GetComponent<GroundDetector>();
        }

        public void ProcessStep(Rigidbody playerRb, Vector3 moveDirection, bool isGrounded)
        {
            if (playerRb == null || !isGrounded || moveDirection.magnitude < 0.1f)
            {
                isStepping = false;
                return;
            }

            // Detect steps
            StepInfo stepInfo = DetectStep(moveDirection);

            if (stepInfo.found && stepInfo.canStep && !isStepping)
            {
                StartStep(stepInfo);
            }

            // Update stepping
            if (isStepping)
            {
                UpdateStepSmoothing();
            }
        }

        private StepInfo DetectStep(Vector3 moveDirection)
        {
            StepInfo info = new StepInfo();
            Vector3 forward = moveDirection.normalized;
            Vector3 origin = transform.position;

            // Lower ray - detects step face
            Vector3 lowerOrigin = origin + Vector3.up * lowerRayHeight;
            RaycastHit lowerHit;

            if (showDebugRays)
                Debug.DrawRay(lowerOrigin, forward * stepSearchDistance, Color.yellow);

            if (Physics.Raycast(lowerOrigin, forward, out lowerHit, stepSearchDistance, stepLayers))
            {
                // Upper ray - checks if we can step up
                Vector3 upperOrigin = lowerHit.point + Vector3.up * maxStepHeight + forward * 0.01f;

                if (showDebugRays)
                    Debug.DrawRay(upperOrigin, Vector3.down * maxStepHeight, Color.cyan);

                if (!Physics.Raycast(upperOrigin, Vector3.down, maxStepHeight + 0.1f, stepLayers))
                {
                    float stepHeight = lowerHit.point.y - origin.y;

                    if (stepHeight > 0.01f && stepHeight <= maxStepHeight)
                    {
                        info.found = true;
                        info.canStep = true;
                        info.stepHeight = stepHeight;
                    }
                }
            }

            return info;
        }

        private void StartStep(StepInfo stepInfo)
        {
            isStepping = true;
            stepProgress = 0f;
            stepStartPosition = transform.position;
            targetStepPosition = stepStartPosition + Vector3.up * stepInfo.stepHeight;
        }

        private void UpdateStepSmoothing()
        {
            stepProgress += Time.fixedDeltaTime * stepSmoothingSpeed;

            if (stepProgress >= 1f)
            {
                // Step complete - use MovePosition for smooth physics transition
                rb.MovePosition(targetStepPosition);
                isStepping = false;
            }
            else
            {
                // Smooth interpolation
                Vector3 newPosition = Vector3.Lerp(
                    stepStartPosition,
                    targetStepPosition,
                    stepProgress
                );
                rb.MovePosition(newPosition);
            }
        }

        private struct StepInfo
        {
            public bool found;
            public bool canStep;
            public float stepHeight;
        }
    }
}
