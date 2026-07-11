using UnityEngine;

namespace GameCode.Magic
{
    public class IcePathEffect : MonoBehaviour
    {
        private FPMovement.SlideController slideController;
        private float timer;

        public void ApplyIcePath(FPMovement.SlideController controller, float duration, float iceDecel)
        {
            slideController = controller;
            timer = duration;
            slideController.slideDecelerationOverride = iceDecel; // E.g. 0.01f makes it nearly frictionless
        }

        void Update()
        {
            if (timer > 0)
            {
                timer -= Time.deltaTime;
                if (timer <= 0 && slideController != null)
                {
                    // Reset to settings value by setting to 0
                    slideController.slideDecelerationOverride = 0f;
                    Destroy(this);
                }
            }
        }

        void OnDisable()
        {
            if (slideController != null)
            {
                slideController.slideDecelerationOverride = 0f;
            }
        }
    }
}
