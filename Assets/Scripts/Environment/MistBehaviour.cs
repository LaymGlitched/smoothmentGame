using UnityEngine;
using VLB;

namespace GameCode.Environment
{
    [ExecuteAlways]
    public class MistBehaviour : KillTrigger
    {
        public float WorldSpaceMistYLevel;
        public GameObject Player;
        public Light Sun;
        public VolumetricLightBeamHD beam;

        private Transform playerTransform;
        private Transform mistTransform;
        private float lastTemperature = -1f;

        private void OnEnable()
        {
            if (beam == null) beam = GetComponent<VolumetricLightBeamHD>();
            mistTransform = transform;
            CachePlayerTransform();
        }

        private void Start()
        {
            CachePlayerTransform();
        }

        private void CachePlayerTransform()
        {
            if (Player != null) playerTransform = Player.transform;
        }

        void Update()
        {
            if (playerTransform != null)
            {
                Vector3 playerPos = playerTransform.position;
                mistTransform.position = new Vector3(playerPos.x, WorldSpaceMistYLevel, playerPos.z);
            }
            else if (Player != null)
            {
                playerTransform = Player.transform;
            }

            if (Sun != null && beam != null)
            {
                float currentTemp = Sun.colorTemperature;

                if (!Mathf.Approximately(currentTemp, lastTemperature))
                {
                    lastTemperature = currentTemp;

                    Color originalColor = Mathf.CorrelatedColorTemperatureToRGB(currentTemp * 0.5f);
                    float gray = 0.2126f * originalColor.r + 0.7152f * originalColor.g + 0.0722f * originalColor.b;
                    Color grayscaleColor = new Color(gray, gray, gray, originalColor.a);
                    beam.colorFlat = Color.Lerp(originalColor, grayscaleColor, 0.85f);
                }
            }
        }
    }
}