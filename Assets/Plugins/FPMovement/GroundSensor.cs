using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Small, reusable "am I standing on something" component. Kept separate
    /// from the controller so other systems (footsteps, landing FX, etc.)
    /// can read IsGrounded / GroundNormal without depending on the controller.
    /// </summary>
    public class GroundSensor : MonoBehaviour
    {
        [SerializeField]
        private CapsuleCollider capsule;

        [SerializeField]
        private float checkDistance = 0.25f;

        [SerializeField]
        private LayerMask groundMask = ~0;

        [Range(0f, 89f)]
        [SerializeField]
        private float slopeLimit = 50f;

        public bool IsGrounded { get; private set; }
        public bool OnWalkableSlope { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        private void Reset() => capsule = GetComponent<CapsuleCollider>();

        public void Configure(float distance, LayerMask mask, float slope)
        {
            checkDistance = distance;
            groundMask = mask;
            slopeLimit = slope;
        }

        /// <summary>Call once per FixedUpdate from the controller.</summary>
        public void Probe()
        {
            float castRadius = capsule.radius * 0.9f;

            // True center of the capsule's bottom hemisphere.
            Vector3 bottomSphereCenter =
                transform.TransformPoint(capsule.center)
                + Vector3.down * (capsule.height * 0.5f - capsule.radius);

            // Start the cast a little ABOVE that point so it never begins
            // already overlapping the ground (which can silently return no
            // hit) and cover that same margin in the cast distance.
            const float skin = 0.1f;
            Vector3 origin = bottomSphereCenter + Vector3.up * skin;
            float castDistance = checkDistance + skin;

            bool hit = Physics.SphereCast(
                origin,
                castRadius,
                Vector3.down,
                out RaycastHit info,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

            IsGrounded = hit;
            GroundNormal = hit ? info.normal : Vector3.up;
            OnWalkableSlope = hit && Vector3.Angle(Vector3.up, info.normal) <= slopeLimit;
        }
    }
}
