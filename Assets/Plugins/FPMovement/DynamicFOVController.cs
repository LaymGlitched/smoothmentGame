using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Smoothly lerps camera FOV based on movement state - a base FOV plus a
    /// sprint "kick" and slide "dip". Fully independent toggle; leave it off and FOV stays
    /// fixed at settings.baseFov.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DynamicFOVController : MonoBehaviour
    {
        [SerializeField]
        private FPMovementSettings settings;

        [SerializeField]
        private RigidbodyFPController controller;

        public bool enableDynamicFov = true;

        /// <summary>Other systems (e.g. SlideController) can set this to hold a
        /// persistent FOV offset on top of the base/sprint FOV. 0 = none.</summary>
        public float ExternalFovOffset { get; set; }

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = settings.baseFov;
        }

        private void Update()
        {
            float target = settings.baseFov + ExternalFovOffset;

            if (enableDynamicFov)
            {
                if (controller.IsSprinting)
                    target += settings.sprintFovAdd;

                if (controller.IsSliding)
                    target += settings.slideFovAdd;
            }

            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                target,
                Time.deltaTime * settings.fovLerpSpeed
            );
        }

        /// <summary>Manual FOV kick hook for other systems (e.g. a dash ability, weapon zoom).</summary>
        public void SetTemporaryFovOffset(float offset, float lerpSpeedOverride = -1f)
        {
            float speed = lerpSpeedOverride > 0f ? lerpSpeedOverride : settings.fovLerpSpeed;
            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                settings.baseFov + offset,
                Time.deltaTime * speed
            );
        }
    }
}
