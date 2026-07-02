using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Casts left/right from the player to detect walls suitable for wall
    /// running. Kept separate from WallRunController so the raw detection
    /// could be reused (e.g. for a "wall slide" or lean system) later.
    /// </summary>
    public class WallDetector : MonoBehaviour
    {
        public bool LeftWall { get; private set; }
        public bool RightWall { get; private set; }
        public Vector3 LeftNormal { get; private set; }
        public Vector3 RightNormal { get; private set; }

        /// <summary>Probes from originTransform's position, using its right/forward axes.</summary>
        public void Probe(Transform origin, float distance, LayerMask mask)
        {
            LeftWall = Physics.Raycast(
                origin.position,
                -origin.right,
                out RaycastHit lHit,
                distance,
                mask,
                QueryTriggerInteraction.Ignore
            );
            LeftNormal = LeftWall ? lHit.normal : Vector3.zero;

            RightWall = Physics.Raycast(
                origin.position,
                origin.right,
                out RaycastHit rHit,
                distance,
                mask,
                QueryTriggerInteraction.Ignore
            );
            RightNormal = RightWall ? rHit.normal : Vector3.zero;
        }
    }
}
