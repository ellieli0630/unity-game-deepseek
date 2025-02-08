using UnityEngine;
using UnityEngine.UI;

public class SpeedometerUI : MonoBehaviour
{
    public CarController carController;   // 关联的车辆控制器
    public Image needleImage;              // 指针的 Image 组件
    public float minSpeed = 0f;            // 最小速度
    public float maxSpeed = 30f;           // 最大速度

    private float currentSpeed = 0f;       // 当前车速

    void Update()
    {
        // 获取当前车速（根据车辆的 Rigidbody 速度）
        currentSpeed = carController.GetComponent<Rigidbody>().velocity.magnitude;

        // 计算指针应该旋转的角度
        float speedRatio = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);  // 归一化速度为 0 到 1 的值
        float rotationAngle = Mathf.Lerp(-90f, 90f, speedRatio);  // -90 到 90 之间的角度

        // 更新指针的旋转角度
        needleImage.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
    }
}
