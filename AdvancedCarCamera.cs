using UnityEngine;

public class AdvancedCarCamera : MonoBehaviour
{
    [Header("跟随参数")]
    public Transform target;              // 跟随目标（车辆）
    public float maxSpeed = 30f;          // 最大速度（可从车辆脚本中获取）
    public Vector3 offset = new Vector3(0, 2.5f, -5f);  // 初始偏移
    public float distanceDamping = 5f;    // （原本用于平滑移动，这里不再使用插值了）
    public float rotationDamping = 3f;    // （原本用于平滑旋转，这里不再使用插值了）

    [Header("动态调整")]
    [Tooltip("缩放因子，数值越大，随着速度变化缩放幅度越大")]
    public float speedZoomFactor = 1f;    // 缩放因子
    [Tooltip("最大缩放距离（单位：米）")]
    public float maxZoomOut = 10f;        // 最大缩放（额外的后移量）
    public float tiltAngle = 15f;         // 根据速度产生的倾斜角度
    public float collisionOffset = 0.5f;  // 防止穿模时的偏移

    [Header("鼠标控制")]
    public float mouseSensitivity = 3f;   // 鼠标灵敏度（可调）
    public float minPitch = -20f;         // 向下俯视的最小角度
    public float maxPitch = 60f;          // 向上仰视的最大角度

    // 记录当前鼠标控制的角度
    private float mouseX = 0f;  // 水平角度
    private float mouseY = 0f;  // 垂直角度

    private Vector3 velocity = Vector3.zero; 
    private float currentTilt;             // 当前基于速度的倾斜角度
    private CarController carController;   // 车辆控制器引用

    [Header("镜头控制")]
    public Transform[] cameraPositions;   // 预设镜头位置数组
    private int currentCameraIndex = 0;   // 当前镜头索引

    void Start()
    {
        // 确保在开始时使用第一个镜头位置
        if (cameraPositions.Length > 0)
        {
            offset = cameraPositions[currentCameraIndex].localPosition;  // 将当前镜头位置赋给 offset
        }

        if (target != null)
        {
            // 自动获取车辆控制器（如果目标上有该脚本）
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

        // 1. 更新速度倾斜
        UpdateSpeedTilt();

        // 2. 计算基于速度的动态偏移
        Vector3 dynamicOffset = CalculateDynamicOffset();
        Vector3 targetPosition = target.TransformPoint(dynamicOffset);

        // 3. 检测与环境碰撞，修正相机位置，防止穿模
        HandleCameraCollision(ref targetPosition);

        // 4. 将相机“瞬间”放到目标位置（不做平滑）
        transform.position = targetPosition;

        // 5. 处理鼠标输入，用来修改相机的水平/垂直角度
        HandleMouseInput();

        // 6. 计算最终旋转：汽车朝向 + 速度倾斜 + 鼠标自由旋转
        Quaternion finalRot = CalculateFinalRotation();

        // 7. 赋值相机旋转
        transform.rotation = finalRot;

        // 8. 处理镜头切换
        HandleCameraSwitch();
    }


    void HandleCameraSwitch()
    {
        // 每次按下 V 键切换到下一个镜头位置
        if (Input.GetKeyDown(KeyCode.V)) 
        {
            currentCameraIndex = (currentCameraIndex + 1) % cameraPositions.Length; // 切换到下一个镜头位置
            offset = cameraPositions[currentCameraIndex].localPosition; // 更新相机偏移
        }
    }

    /// <summary>
    /// 处理鼠标的水平/垂直输入，更新 mouseX / mouseY
    /// </summary>
    void HandleMouseInput()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        mouseX += mx;
        mouseY -= my; // 减是因为向上移动鼠标时，角度通常为负

        // 限制俯仰角
        mouseY = Mathf.Clamp(mouseY, minPitch, maxPitch);
    }

    /// <summary>
    /// 根据车辆速度更新 currentTilt（供后续计算使用）
    /// </summary>
    void UpdateSpeedTilt()
    {
        float currentSpeed = 0f;
        if (carController != null)
        {
            currentSpeed = carController.GetComponent<Rigidbody>().velocity.magnitude;
        }

        // 计算速度比例
        float speedFactor = (maxSpeed > 0f) ? currentSpeed / maxSpeed : 0f;

        // currentTilt 指车辆本身的“向下”倾斜量 (例如高速时相机往下看些)
        currentTilt = Mathf.Lerp(currentTilt, -tiltAngle * speedFactor, 2f * Time.deltaTime);
    }

    /// <summary>
    /// 计算相机的最终旋转 = 先朝向目标，再附加速度倾斜，再加上鼠标自由旋转
    /// </summary>
    /// <returns></returns>
    Quaternion CalculateFinalRotation()
    {
        // （A）先得到相机"基础"朝向：看向车辆
        //     注意：如果你觉得“必须保证相机正后方对着车辆”，那就保留此 LookRotation。
        //     否则如果想让玩家鼠标全权控制方向，可以不做 LookRotation。
        Quaternion baseRot = Quaternion.LookRotation(target.position - transform.position, Vector3.up);

        // （B）对 “基础”朝向 添加车辆速度带来的倾斜
        baseRot *= Quaternion.Euler(currentTilt, 0, 0);

        // （C）最后用鼠标输入进行额外的水平/垂直旋转
        //     mouseX 控制水平旋转，mouseY 控制上下俯仰
        //     这里将鼠标旋转作为一个“叠加旋转”
        Quaternion mouseRot = Quaternion.Euler(mouseY, mouseX, 0);

        // 两者叠加
        // 你可以先乘 mouseRot 再乘 baseRot，具体看想要什么效果：
        //   - baseRot * mouseRot：先朝向车辆，再局部追加鼠标旋转
        //   - mouseRot * baseRot：先按鼠标计算一个全球角度，再把车辆朝向叠加
        // 可以自行试验，或视设计需求选择。
        Quaternion finalRot = baseRot * mouseRot;

        // （D）可选：锁定 Z 轴，避免 roll
        Vector3 euler = finalRot.eulerAngles;
        euler.z = 0;
        finalRot = Quaternion.Euler(euler);

        return finalRot;
    }

    /// <summary>
    /// 计算动态偏移：根据车辆速度动态改变 Z (zoom)，并考虑 Y 提升
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

        // offset.z 在高速时会向后拉远
        float dynamicZoom = Mathf.Lerp(offset.z, offset.z - maxZoomOut, speedFactor * speedZoomFactor);

        // offset.y 在高速时可适当抬高
        float yOffset = offset.y + Mathf.Abs(speedFactor) * 0.5f;

        return new Vector3(offset.x, yOffset, dynamicZoom);
    }

    /// <summary>
    /// 通过 SphereCast 防止相机穿透物体
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
