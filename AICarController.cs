using UnityEngine;
using System.Collections.Generic;

public class AICarController : MonoBehaviour
{
    [Header("Path Settings")]
    public List<Transform> waypoints = new List<Transform>();  // Path points
    public float waypointThreshold = 3f; // Waypoint reach distance
    public bool loop = true; // Loop path

    [Header("AI Parameters")]
    public float maxSpeed = 25f;
    public float acceleration = 10f;  // Acceleration
    public float brakeForce = 100f;   // Braking force
    public float reverseDistance = 5f; // Obstacle reverse distance

    [Header("Wheel Settings")]
    public WheelCollider wheelCollider_FL;
    public WheelCollider wheelCollider_FR;
    public WheelCollider wheelCollider_RL;
    public WheelCollider wheelCollider_RR;
    public Transform wheelTransform_FL;
    public Transform wheelTransform_FR;
    public Transform wheelTransform_RL;
    public Transform wheelTransform_RR;

    private Rigidbody rb;
    private int currentWaypointIndex = 0;
    private float currentSpeed;

    [Header("Collision Parameters")]
    public float collisionForceMultiplier = 1f; // Collision force multiplier

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (waypoints.Count == 0) return;

        // Get target waypoint direction
        Vector3 targetDirection = waypoints[currentWaypointIndex].position - transform.position;
        targetDirection.y = 0;  // Ignore vertical axis

        // Rotate vehicle towards target
        transform.forward = Vector3.RotateTowards(transform.forward, targetDirection, 2f * Time.deltaTime, 0f);

        // Calculate speed and acceleration
        float distanceToTarget = targetDirection.magnitude;
        float speedFactor = currentSpeed / maxSpeed;
        float forwardInput = 1f - speedFactor;  // Throttle input

        if (distanceToTarget < waypointThreshold)
        {
            UpdateWaypoint();
            return;
        }

        // Reverse if obstacle detected
        if (Physics.Raycast(transform.position, transform.forward, reverseDistance))
        {
            MoveCar(-0.5f);  // Reverse
        }
        else
        {
            MoveCar(forwardInput);  // Normal acceleration
        }

        // Update wheel rotation
        UpdateWheelRotation();
    }

    void MoveCar(float forwardInput)
    {
        if (forwardInput > 0)
        {
            rb.AddForce(transform.forward * forwardInput * acceleration, ForceMode.Force);
        }
        else
        {
            rb.AddForce(transform.forward * forwardInput * brakeForce, ForceMode.Force);
        }
    }

    void UpdateWheelRotation()
    {
        UpdateWheelPosition(wheelCollider_FL, wheelTransform_FL);
        UpdateWheelPosition(wheelCollider_FR, wheelTransform_FR);
        UpdateWheelPosition(wheelCollider_RL, wheelTransform_RL);
        UpdateWheelPosition(wheelCollider_RR, wheelTransform_RR);
    }

    void UpdateWheelPosition(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    void UpdateWaypoint()
    {
        currentWaypointIndex++;
        if (currentWaypointIndex >= waypoints.Count)
        {
            if (loop) currentWaypointIndex = 0;
            else enabled = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 forceDir = collision.contacts[0].point - transform.position;
            forceDir = forceDir.normalized;
            rb.AddForce(-forceDir * collisionForceMultiplier, ForceMode.Impulse);
        }
    }

    void OnDrawGizmos()
    {
        if (waypoints.Count == 0) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.5f);
            if (i > 0 && waypoints[i - 1] != null)
                Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
        }
        if (loop && waypoints.Count > 1)
            Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);
    }
}
