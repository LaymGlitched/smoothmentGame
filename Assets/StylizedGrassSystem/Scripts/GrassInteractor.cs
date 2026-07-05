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

        private void OnEnable()
        {
            if (GrassInteractionManager.Instance != null)
            {
                GrassInteractionManager.Instance.Register(this);
            }
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
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1.0f, 0.2f, 0.3f);
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = new Color(0.2f, 1.0f, 0.2f, 1.0f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
