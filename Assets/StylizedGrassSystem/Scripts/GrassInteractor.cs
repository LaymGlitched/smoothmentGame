using UnityEngine;

namespace StylizedGrassSystem
{
    public class GrassInteractor : MonoBehaviour
    {
        [Tooltip("Radius of the interaction effect")]
        public float radius = 2.0f;
        
        [Tooltip("How strongly the grass is bent away from the center")]
        [Range(0f, 1f)]
        public float bendStrength = 1.0f;
        
        [Tooltip("How much the grass is pushed down (flattened)")]
        [Range(0f, 1f)]
        public float trailIntensity = 0.5f;
        
        [Tooltip("XZ offset from the transform position (useful for aligning to feet, etc.)")]
        public Vector2 positionOffset = Vector2.zero;

        [Header("Height Settings")]
        [Tooltip("Maximum height above ground where the interaction still has effect")]
        public float maxInteractionHeight = 3f;
        
        [Tooltip("Layers considered as ground for height detection")]
        public LayerMask groundMask = ~0;

        [Header("Movement Feel")]
        [Tooltip("How much the interactor's movement direction biases the bend (0 = pure radial, 1 = fully directional)")]
        [Range(0f, 1f)]
        public float velocityInfluence = 0.3f;

        // Runtime state
        private Vector3 previousPosition;
        private Vector3 smoothVelocity;
        private float currentHeightAttenuation = 1f;

        /// <summary>
        /// World-space position with the XZ offset applied.
        /// </summary>
        public Vector3 EffectPosition
        {
            get
            {
                Vector3 pos = transform.position;
                pos.x += positionOffset.x;
                pos.z += positionOffset.y;
                return pos;
            }
        }

        /// <summary>
        /// 0 = too high (no effect), 1 = on the ground (full effect).
        /// </summary>
        public float HeightAttenuation => currentHeightAttenuation;

        /// <summary>
        /// Smoothed XZ velocity in world space.
        /// </summary>
        public Vector3 Velocity => smoothVelocity;

        private void OnEnable()
        {
            if (GrassInteractionManager.Instance != null)
            {
                GrassInteractionManager.Instance.Register(this);
            }
            previousPosition = transform.position;
            smoothVelocity = Vector3.zero;
        }

        private void OnDisable()
        {
            if (GrassInteractionManager.Instance != null)
            {
                GrassInteractionManager.Instance.Unregister(this);
            }
        }

        private void Start()
        {
            // Register here as well in case this component was enabled before the manager initialized
            if (GrassInteractionManager.Instance != null)
            {
                GrassInteractionManager.Instance.Register(this);
            }
            previousPosition = transform.position;
        }

        private void Update()
        {
            UpdateVelocity();
            UpdateHeightAttenuation();
        }

        private void UpdateVelocity()
        {
            if (Time.deltaTime < 0.0001f) return;

            Vector3 currentPos = transform.position;
            Vector3 rawVelocity = (currentPos - previousPosition) / Time.deltaTime;
            previousPosition = currentPos;

            // Smooth to avoid jitter — fast lerp for responsiveness
            smoothVelocity = Vector3.Lerp(smoothVelocity, rawVelocity, Time.deltaTime * 15f);
        }

        private void UpdateHeightAttenuation()
        {
            Vector3 pos = EffectPosition;

            // Cast from slightly above (handles being exactly on a surface)
            Vector3 rayOrigin = new Vector3(pos.x, pos.y + 0.5f, pos.z);
            float maxRayDist = maxInteractionHeight + 0.5f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRayDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                float heightAboveGround = Mathf.Max(0f, pos.y - hit.point.y);
                float t = Mathf.Clamp01(1f - (heightAboveGround / maxInteractionHeight));
                // Quadratic falloff — feels more natural than linear
                currentHeightAttenuation = t * t;
            }
            else
            {
                // No ground within range — suppress interaction
                currentHeightAttenuation = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = EffectPosition;

            // Color reflects height attenuation (green = active, red = suppressed)
            Color activeColor = Color.Lerp(Color.red, new Color(0.2f, 1f, 0.2f), currentHeightAttenuation);

            Gizmos.color = activeColor * new Color(1, 1, 1, 0.3f);
            Gizmos.DrawSphere(pos, radius);
            Gizmos.color = activeColor;
            Gizmos.DrawWireSphere(pos, radius);

            // Draw line to ground if applicable
            if (currentHeightAttenuation > 0.01f)
            {
                Vector3 rayOrigin = new Vector3(pos.x, pos.y + 0.5f, pos.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxInteractionHeight + 0.5f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(pos, hit.point);
                }
            }
        }
    }
}

