using UnityEngine;
using System.Collections.Generic;

public class AICarController : MonoBehaviour
{
    [Header("路径设置")]
    public List<Transform> waypoints = new List<Transform>();  // 路径点
    public float waypointThreshold = 3f; // 到达路径点的判定距离
    public bool loop = true; // 是否循环路径

    [Header("AI参数")]
    public float maxSpeed = 25f;
    public float acceleration = 10f;  // 加速度
    public float brakeForce = 100f;   // 刹车力度
    public float reverseDistance = 5f; // 距离障碍物多远开始倒车

    [Header("车轮设置")]
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

    [Header("碰撞参数")]
    public float collisionForceMultiplier = 1f; // 碰撞力影响系数

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (waypoints.Count == 0) return;

        // 获取目标路径点方向
        Vector3 targetDirection = waypoints[currentWaypointIndex].position - transform.position;
        targetDirection.y = 0;  // 忽略 y 轴的影响，保持水平

        // 计算目标方向并让车体朝向目标路径点
        transform.forward = Vector3.RotateTowards(transform.forward, targetDirection, 2f * Time.deltaTime, 0f);

        // 计算速度和加速
        float distanceToTarget = targetDirection.magnitude;
        float speedFactor = currentSpeed / maxSpeed;
        float forwardInput = 1f - speedFactor;  // 油门输入，越远离目标路径点，油门越大

        if (distanceToTarget < waypointThreshold)
        {
            UpdateWaypoint();
            return;
        }

        // 障碍物检测：如果前方有障碍物，则倒车
        if (Physics.Raycast(transform.position, transform.forward, reverseDistance))
        {
            MoveCar(-0.5f);  // 倒车
        }
        else
        {
            MoveCar(forwardInput);  // 正常加速
        }

        // 更新车轮的旋转
        UpdateWheelRotation();
    }

    // 控制车辆的运动（加速、刹车）
    void MoveCar(float forwardInput)
    {
        // 加速或刹车
        if (forwardInput > 0)
        {
            rb.AddForce(transform.forward * forwardInput * acceleration, ForceMode.Force);
        }
        else
        {
            rb.AddForce(transform.forward * forwardInput * brakeForce, ForceMode.Force);
        }
    }

    // 更新车轮的旋转（加速时车轮转动）
    void UpdateWheelRotation()
    {
        // 控制所有轮子的旋转（加速时）
        UpdateWheelPosition(wheelCollider_FL, wheelTransform_FL);
        UpdateWheelPosition(wheelCollider_FR, wheelTransform_FR);
        UpdateWheelPosition(wheelCollider_RL, wheelTransform_RL);
        UpdateWheelPosition(wheelCollider_RR, wheelTransform_RR);
    }

    // 更新轮子位置
    void UpdateWheelPosition(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    // 路径点更新
    void UpdateWaypoint()
    {
        currentWaypointIndex++;
        if (currentWaypointIndex >= waypoints.Count)
        {
            if (loop) currentWaypointIndex = 0;  // 如果启用循环路径
            else enabled = false;  // 如果不循环，禁用 AI 控制
        }
    }

    // 碰撞处理
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 forceDir = collision.contacts[0].point - transform.position;
            forceDir = forceDir.normalized;
            rb.AddForce(-forceDir * collisionForceMultiplier, ForceMode.Impulse);  // 施加反向碰撞力
        }
    }

    // 可视化路径（在Scene视图中显示）
    void OnDrawGizmos()
    {
        if (waypoints.Count == 0) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.5f);  // 绘制路径点
            if (i > 0 && waypoints[i - 1] != null)
                Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);  // 绘制路径连线
        }
        if (loop && waypoints.Count > 1)
            Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);  // 绘制循环路径
    }
}
