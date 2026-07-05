using UnityEngine;
using UnityEngine.UI;

public class MomentumUIHandler : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Text speedValueText; // Assign the large number text object
    [SerializeField] private Text speedUnitText;  // Assign the small "KM/H" text object
    [SerializeField] private Rigidbody playerRigidbody;

    private const float MpsToKmh = 3.6f;

    private void Start()
    {
        // Pre-initialize the unit text so you don't have to set it manually or every frame
        if (speedUnitText != null)
        {
            speedUnitText.text = "KM/H";
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
    }
}