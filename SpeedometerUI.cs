using UnityEngine;
using UnityEngine.UI;

public class SpeedometerUI : MonoBehaviour
{
    public CarController carController;   // Linked vehicle controller
    public Image needleImage;              // Needle Image component
    public float minSpeed = 0f;            // Minimum speed
    public float maxSpeed = 30f;           // Maximum speed

    private float currentSpeed = 0f;       // Current vehicle speed

    void Update()
    {
        // Retrieve current speed (based on vehicle Rigidbody velocity)
        currentSpeed = carController.GetComponent<Rigidbody>().velocity.magnitude;

        // Calculate the needle rotation angle
        float speedRatio = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);  // Normalize speed to 0-1
        float rotationAngle = Mathf.Lerp(-90f, 90f, speedRatio);  // Map to -90° to 90°

        // Update needle rotation
        needleImage.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
    }
}
