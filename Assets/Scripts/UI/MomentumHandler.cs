using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameCode.UI
{
    public class MomentumUIHandler : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TMP_Text speedValueText; // Assign the large number text object
        [SerializeField] private TMP_Text speedUnitText;  // Assign the small "KM/H" text object
        [SerializeField] private Rigidbody playerRigidbody;

        [Header("Reticle FX")]
        [SerializeField] private Image reticleImage; // The UI Image using the ReticleHUD material
        [Tooltip("The speed (in m/s) at which max momentum visual effects apply.")]
        [SerializeField] private float maxMomentumSpeed = 15f; 
        
        [Tooltip("Multiplier for outer ring rotation speed based on momentum.")]
        [SerializeField] private float maxRotationSpeedMultiplier = 5f;

        private Material reticleMaterial;
        private const float MpsToKmh = 3.6f;
        
        private float currentReticleAngle = 0f;

        // Shader property IDs for performance
        private readonly int centripetalRotationID = Shader.PropertyToID("_CentripetalSpeed"); // Keeping name to match your shader property

        private readonly int maxSpeedGlowID = Shader.PropertyToID("_MaxSpeedGlow");
        private readonly int crosshairOpeningID = Shader.PropertyToID("_CrosshairOpening");

        private void Start()
        {
            // Pre-initialize the unit text so you don't have to set it manually or every frame
            if (speedUnitText != null)
            {
                speedUnitText.text = "KM/H";
            }

            if (reticleImage != null)
            {
                // Instantiate a unique material instance so we don't modify the shared asset
                reticleImage.material = new Material(reticleImage.material);
                reticleMaterial = reticleImage.material;
            }
        }

        private void Update()
        {
            if (speedValueText == null || playerRigidbody == null) return;

            // Calculate horizontal speed every frame (ignoring Y falling/climbing speed)
            Vector3 horizontalVelocity = new Vector3(playerRigidbody.linearVelocity.x, 0f, playerRigidbody.linearVelocity.z);
            float currentSpeed = horizontalVelocity.magnitude;

            // Convert velocity into stylized display metrics
            int displaySpeed = Mathf.RoundToInt(currentSpeed * MpsToKmh);

            // Update just the number string
            speedValueText.text = displaySpeed.ToString();

            // Update Reticle Shader Feedback
            if (reticleMaterial != null)
            {
                float speedRatio = Mathf.Clamp01(currentSpeed / maxMomentumSpeed);
                
                // 1. Centripetal Rotation: Accumulate rotation in C# to prevent snapping
                float currentRotationSpeed = Mathf.Lerp(1f, maxRotationSpeedMultiplier, speedRatio);
                currentReticleAngle += currentRotationSpeed * Time.deltaTime;
                currentReticleAngle %= (Mathf.PI * 2f); // Wrap angle to prevent precision issues over time

                
                reticleMaterial.SetFloat(centripetalRotationID, currentReticleAngle);

                // 2. Feedback: pulse white-hot and open up at max momentum
                // We use a sharp threshold (e.g., above 0.95 ratio) or just straight mapping
                bool isTopSpeed = speedRatio >= 0.95f;
                
                // Lerp glow towards 1 when near top speed
                float glowIntensity = isTopSpeed ? 1f : 0f;
                // Add a pulse effect if at top speed using time
                if (isTopSpeed)
                {
                    glowIntensity = 1f + Mathf.Sin(Time.time * 15f) * 0.5f; // Pulsing 
                }
                
                reticleMaterial.SetFloat(maxSpeedGlowID, glowIntensity);

                // 3. Crosshair "Opening up"
                // 0 = normal closed, 1 = fully open. Only opens when moving fast
                float openingAmount = isTopSpeed ? 1f : Mathf.Lerp(0f, 0.2f, speedRatio); 
                reticleMaterial.SetFloat(crosshairOpeningID, openingAmount);
            }
        }
        
        private void OnDestroy()
        {
            if (reticleMaterial != null)
            {
                Destroy(reticleMaterial);
            }
        }
    }
}