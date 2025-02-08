using UnityEngine;

public class AdvancedCarCamera : MonoBehaviour
{
    [Header("Follow Parameters")]
    public Transform target;              // Follow target (vehicle)
    public float maxSpeed = 30f;          // Maximum speed (can be retrieved from vehicle script)
    public Vector3 offset = new Vector3(0, 2.5f, -5f);  // Initial offset
    public float distanceDamping = 5f;    // (Originally used for smoothing movement, no longer using interpolation)
    public float rotationDamping = 3f;    // (Originally used for smoothing rotation, no longer using interpolation)

    [Header("Dynamic Adjustment")]
    [Tooltip("Zoom factor, higher values cause more zooming with speed changes")]
    public float speedZoomFactor = 1f;    // Zoom factor
    [Tooltip("Maximum zoom out distance (meters)")]
    public float maxZoomOut = 10f;        // Maximum zoom out distance
    public float tiltAngle = 15f;         // Tilt angle based on speed
    public float collisionOffset = 0.5f;  // Offset to prevent clipping

    [Header("Mouse Control")]
    public float mouseSensitivity = 3f;   // Mouse sensitivity (adjustable)
    public float minPitch = -20f;         // Minimum downward viewing angle
    public float maxPitch = 60f;          // Maximum upward viewing angle

    // Current mouse control angles
    private float mouseX = 0f;  // Horizontal angle
    private float mouseY = 0f;  // Vertical angle

    private Vector3 velocity = Vector3.zero; 
    private float currentTilt;             // Current tilt angle based on speed
    private CarController carController;   // Reference to the vehicle controller

    [Header("Camera Control")]
    public Transform[] cameraPositions;   // Preset camera position array
    private int currentCameraIndex = 0;   // Current camera index

    void Start()
    {
        // Ensure using the first camera position at start
        if (cameraPositions.Length > 0)
        {
            offset = cameraPositions[currentCameraIndex].localPosition;  // Assign current camera position to offset
        }

        if (target != null)
        {
            // Automatically get vehicle controller if attached to target
            carController = target.GetComponent<CarController>();
            if (carController != null)
            {
                maxSpeed = carController.maxSpeed;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Update speed-based tilt
        UpdateSpeedTilt();

        // 2. Calculate dynamic offset based on speed
        Vector3 dynamicOffset = CalculateDynamicOffset();
        Vector3 targetPosition = target.TransformPoint(dynamicOffset);

        // 3. Handle environment collision to prevent clipping
        HandleCameraCollision(ref targetPosition);

        // 4. Instantly move camera to target position (no smoothing)
        transform.position = targetPosition;

        // 5. Handle mouse input for camera angles
        HandleMouseInput();

        // 6. Calculate final rotation: vehicle direction + speed tilt + mouse rotation
        Quaternion finalRot = CalculateFinalRotation();

        // 7. Apply camera rotation
        transform.rotation = finalRot;

        // 8. Handle camera switching
        HandleCameraSwitch();
    }

    void HandleCameraSwitch()
    {
        // Switch to next camera position when pressing V key
        if (Input.GetKeyDown(KeyCode.V)) 
        {
            currentCameraIndex = (currentCameraIndex + 1) % cameraPositions.Length;
            offset = cameraPositions[currentCameraIndex].localPosition;
        }
    }

    /// <summary>
    /// Handle mouse input to update horizontal/vertical angles
    /// </summary>
    void HandleMouseInput()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        mouseX += mx;
        mouseY -= my; // Subtract because upward mouse movement typically corresponds to negative angle

        // Clamp pitch angle
        mouseY = Mathf.Clamp(mouseY, minPitch, maxPitch);
    }

    /// <summary>
    /// Update currentTilt based on vehicle speed
    /// </summary>
    void UpdateSpeedTilt()
    {
        float currentSpeed = 0f;
        if (carController != null)
        {
            currentSpeed = carController.GetComponent<Rigidbody>().velocity.magnitude;
        }

        // Calculate speed ratio
        float speedFactor = (maxSpeed > 0f) ? currentSpeed / maxSpeed : 0f;

        // currentTilt represents vehicle's downward tilt (e.g., camera looks slightly down at high speed)
        currentTilt = Mathf.Lerp(currentTilt, -tiltAngle * speedFactor, 2f * Time.deltaTime);
    }

    /// <summary>
    /// Calculate final rotation: vehicle direction + speed tilt + mouse rotation
    /// </summary>
    Quaternion CalculateFinalRotation()
    {
        // (A) Base rotation: look at vehicle
        Quaternion baseRot = Quaternion.LookRotation(target.position - transform.position, Vector3.up);

        // (B) Add speed-based tilt
        baseRot *= Quaternion.Euler(currentTilt, 0, 0);

        // (C) Apply mouse rotation
        Quaternion mouseRot = Quaternion.Euler(mouseY, mouseX, 0);

        // Combine rotations
        Quaternion finalRot = baseRot * mouseRot;

        // (D) Lock Z-axis to prevent roll
        Vector3 euler = finalRot.eulerAngles;
        euler.z = 0;
        finalRot = Quaternion.Euler(euler);

        return finalRot;
    }

    /// <summary>
    /// Calculate dynamic offset: adjust Z (zoom) based on speed and Y elevation
    /// </summary>
    Vector3 CalculateDynamicOffset()
    {
        float currentSpeed = 0f;
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            currentSpeed = targetRb.velocity.magnitude;
        }
        float speedFactor = (maxSpeed > 0f) ? currentSpeed / maxSpeed : 0f;

        // offset.z increases backward distance at high speed
        float dynamicZoom = Mathf.Lerp(offset.z, offset.z - maxZoomOut, speedFactor * speedZoomFactor);

        // offset.y slightly elevates at high speed
        float yOffset = offset.y + Mathf.Abs(speedFactor) * 0.5f;

        return new Vector3(offset.x, yOffset, dynamicZoom);
    }

    /// <summary>
    /// Prevent camera clipping using SphereCast
    /// </summary>
    void HandleCameraCollision(ref Vector3 targetPos)
    {
        Vector3 dir = targetPos - target.position;
        float dist = dir.magnitude;

        if (Physics.SphereCast(target.position, 0.3f, dir.normalized, out RaycastHit hit, dist + collisionOffset))
        {
            targetPos = hit.point - dir.normalized * collisionOffset;
        }
    }

    void OnDrawGizmos()
    {
        if (target)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position, transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
