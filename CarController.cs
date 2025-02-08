using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("运动参数")]
    public float motorForce = 5000f;    
    public float maxSpeed = 30f;        
    public float steerAngle = 45f;      
    public float brakeForce = 10000f;   
    public float downForce = 100f;      

    [Header("物理参数")]
    public float airDrag = 0.5f;        
    public float groundDrag = 3f;       
    public Transform centerOfMass;      

    private Rigidbody rb;
    public float verticalInput;
    public float horizontalInput;
    private bool isBraking;
    
    [Header("碰撞设置")]
    public float collisionResponseMultiplier = 1f; // 玩家受到的碰撞影响
    [Header("漂移系统")]
    [Tooltip("漂移时的侧向摩擦(轮胎抓地)越小，越容易甩尾")]
    public float driftFriction = 0.5f;  
    [Tooltip("正常时的侧向摩擦")]
    public float normalFriction = 1.0f; 
    [Tooltip("漂移烟雾特效（红色）")]
    public ParticleSystem[] driftSmoke; 
    [Tooltip("漂移的输入阈值：按住空格 + W 大于这个值才触发漂移")]
    public float driftThreshold = 0.7f; 

    [Header("视角系统")]
    public Camera[] cameras;                    
    private int currentCameraIndex = 0;

    [Header("粒子特效")]
    [Tooltip("后轮粒子（白烟），例如加速时冒烟")]
    public ParticleSystem[] rearWheelParticles; 
    [Tooltip("前轮粒子（白烟），例如转向时冒烟")]
    public ParticleSystem[] frontWheelParticles;
    public float emissionRate = 30f;            

    public float wheelSpinSpeed = 720f; 
    private float wheelSpinAngle = 0f;  

    [Header("车轮设置")]
    public Transform wheel_LF;  
    public Transform wheel_RF;  
    public float maxWheelAngle = 15f; 


    [Header("音效系统")]
    public AudioClip engineIdleClip;   // 发动机怠速声音
    public AudioClip accelerateClip;   // 加速时的声音（原来 windClip）
    public AudioClip brakeClip;        // 制动声音

    [Range(0,1)] public float engineIdleVolume = 0.5f;   // 背景怠速声音音量
    [Range(0,1)] public float accelerateVolume = 0.7f;   // 加速声音音量
    [Range(0,1)] public float brakeVolume = 0.7f;        // 制动声音音量

    public float fadeOutDuration = 1f;  // 渐弱消失时间（秒）

    private AudioSource engineIdleAudio; // 背景怠速声音（始终播放）
    private AudioSource accelerateAudio; // 加速声音
    private AudioSource brakeAudio;      // 制动声音

    private bool isDrifting = false;

    [Header("车轮碰撞体 (WheelColliders)")]
    public WheelCollider wheelCollider_FL;
    public WheelCollider wheelCollider_FR;
    public WheelCollider wheelCollider_RL;
    public WheelCollider wheelCollider_RR;

    [Header("抓地力系数 (可微调前后轮差异)")]
    [Range(0f,2f)] public float frontTireGrip = 1f; 
    [Range(0f,2f)] public float rearTireGrip = 1f;

    [Header("其他参数")]
    [Tooltip("转向敏感度：越大转向越灵敏")]
    public float steerSensitivity = 1f;

    [Header("灯光系统")]
    public Renderer leftBrakeLight;  // 左尾灯渲染器
    public Renderer rightBrakeLight; // 右尾灯渲染器
    public float brakeLightIntensity = 5f; // 刹车时发光强度

    private Material brakeLightMaterial;
    private Color defaultEmissionColor;
    // 添加获取速度的方法
    public float GetSpeed()
    {
        return rb.velocity.magnitude;
    }
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        SetupPhysics();
        SetupAudio();
        InitializeCameras();
        InitializeParticles();
        // 初始化灯光材质
        if(leftBrakeLight != null)
        {
            brakeLightMaterial = leftBrakeLight.material;
            defaultEmissionColor = brakeLightMaterial.GetColor("_EmissionColor");
        }
            
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("AI"))
        {
            Vector3 forceDir = collision.contacts[0].point - transform.position;
            forceDir = forceDir.normalized;
            
            rb.AddForce(forceDir * collisionResponseMultiplier, ForceMode.Impulse);
        }
    }

    void UpdateBrakeLights()
    {
        float targetIntensity = isBraking ? brakeLightIntensity : 0f;
        Color targetColor = Color.red * targetIntensity;

        // 使用MaterialPropertyBlock优化性能
        MaterialPropertyBlock props = new MaterialPropertyBlock();
        props.SetColor("_EmissionColor", targetColor);
        
        leftBrakeLight.SetPropertyBlock(props);
        rightBrakeLight.SetPropertyBlock(props);

        // 启用/禁用碰撞发光效果
        leftBrakeLight.gameObject.SetActive(isBraking);
        rightBrakeLight.gameObject.SetActive(isBraking);
    }

    void InitializeCameras()
    {
        if(cameras.Length > 0)
        {
            foreach(Camera cam in cameras) cam.gameObject.SetActive(false);
            cameras[currentCameraIndex].gameObject.SetActive(true);
        }
    }

    void InitializeParticles()
    {
        // 初始都停掉
        foreach(var ps in driftSmoke) ps.Stop();
        foreach(var ps in rearWheelParticles) ps.Stop();
        foreach(var ps in frontWheelParticles) ps.Stop();
    }

    void SetupAudio()
    {
        // 背景怠速声音（始终播放且循环）
        engineIdleAudio = gameObject.AddComponent<AudioSource>();
        engineIdleAudio.clip = engineIdleClip;
        engineIdleAudio.loop = true;
        engineIdleAudio.volume = engineIdleVolume;
        engineIdleAudio.playOnAwake = true;
        engineIdleAudio.Play();

        // 制动声音（初始不播放）
        brakeAudio = gameObject.AddComponent<AudioSource>();
        brakeAudio.clip = brakeClip;
        brakeAudio.loop = true;
        brakeAudio.volume = 0;  
        brakeAudio.playOnAwake = false;

        // 加速声音（初始不播放）
        accelerateAudio = gameObject.AddComponent<AudioSource>();
        accelerateAudio.clip = accelerateClip;
        accelerateAudio.loop = true;
        accelerateAudio.volume = 0;  
        accelerateAudio.playOnAwake = false;
    }

    void Update()
    {
        GetInput();
        UpdateDrag();
        UpdateAudio();
        HandleCameraSwitch();
        HandleParticles();      // <---- 在 Update() 中处理粒子
        UpdateWheelSpin();
        UpdateBrakeLights();
    }

    void HandleCameraSwitch()
    {
        if(Input.GetKeyDown(KeyCode.V))
        {
            cameras[currentCameraIndex].gameObject.SetActive(false);
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
            cameras[currentCameraIndex].gameObject.SetActive(true);
        }
    }

    void UpdateWheelSpin()
    {
        if (verticalInput > 0 && IsGrounded())
        {
            wheelSpinAngle += wheelSpinSpeed * Time.deltaTime;
            if (wheelSpinAngle > 360f)
                wheelSpinAngle -= 360f;
        }
    }


    void UpdateAudio()
    {
        // 背景怠速声音始终播放，这里无需处理

        // 加速声音处理
        if (verticalInput > 0.1f)
        {
            // 当按下加速键时，若声音未播放则启动播放，并设定为目标音量
            if (!accelerateAudio.isPlaying)
                accelerateAudio.Play();
            accelerateAudio.volume = accelerateVolume;
        }
        else
        {
            // 松开加速键时，渐弱消失
            if (accelerateAudio.isPlaying)
            {
                // 每秒降低 accelerateVolume 的量
                accelerateAudio.volume = Mathf.MoveTowards(accelerateAudio.volume, 0, (accelerateVolume / fadeOutDuration) * Time.deltaTime);
                // 当音量降低到足够小的时候，停止播放并重置音量
                if (accelerateAudio.volume <= 0.01f)
                {
                    accelerateAudio.Stop();
                    accelerateAudio.volume = 0;
                }
            }
        }

        // 刹车声音处理
        if (isBraking)
        {
            if (!brakeAudio.isPlaying)
                brakeAudio.Play();
            brakeAudio.volume = brakeVolume;
        }
        else
        {
            if (brakeAudio.isPlaying)
            {
                brakeAudio.volume = Mathf.MoveTowards(brakeAudio.volume, 0, (brakeVolume / fadeOutDuration) * Time.deltaTime);
                if (brakeAudio.volume <= 0.01f)
                {
                    brakeAudio.Stop();
                    brakeAudio.volume = 0;
                }
            }
        }
    }



    void FixedUpdate()
    {
        ApplyEngineForce();
        ApplySteering();
        ApplyBrakes();
        ApplyDownForce();

        // 先判断是否漂移，再根据漂移状态动态调整摩擦
        HandleDrift();
        HandleDriftFriction();

        // 若想额外施加横向力促进漂移
        ApplyDriftPhysics();
    }

    void SetupPhysics()
    {
        rb.mass = 1500f; 
        if(centerOfMass) rb.centerOfMass = centerOfMass.localPosition;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void GetInput()
    {
        verticalInput = Input.GetAxis("Vertical");
        horizontalInput = Input.GetAxis("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);
    }

    void UpdateDrag()
    {
        rb.drag = IsGrounded() ? groundDrag : airDrag;
    }


    void ApplyEngineForce()
    {
        if (verticalInput > 0) // 前进
        {
            if (rb.velocity.magnitude < maxSpeed)
            {
                Vector3 engineVector = transform.forward * verticalInput * motorForce;
                rb.AddForce(engineVector, ForceMode.Force);
            }
        }
        else if (verticalInput < 0) // 后退
        {
            // 设定后退时的最大力，减小后退速度
            float reverseMotorForce = motorForce * 0.5f; // 50% 的力量
            if (rb.velocity.magnitude < maxSpeed)
            {
                Vector3 reverseEngineVector = transform.forward * verticalInput * reverseMotorForce;
                rb.AddForce(reverseEngineVector, ForceMode.Force);
            }
        }
    }


    /// <summary>
    /// 结合速度做转向：速度越快，转向越明显
    /// 并在后退时反向转向（若你想保留真实车逻辑，可去掉 directionSign）
    /// </summary>
    void ApplySteering()
    {
        float currentSpeed = rb.velocity.magnitude;
        float speedFactor = (maxSpeed > 0f) ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
        // 后退时反转方向：如果想要真实车逻辑，可删除 directionSign
        float directionSign = (verticalInput >= 0) ? 1f : -1f;

        float scaledSteerAngle = steerAngle * horizontalInput * speedFactor * steerSensitivity * directionSign;

        Quaternion steerRotation = Quaternion.Euler(0, scaledSteerAngle, 0);
        rb.MoveRotation(rb.rotation * steerRotation);

        if (wheel_LF && wheel_RF)
        {
            float wheelTurn = maxWheelAngle * horizontalInput;
            Quaternion steerQuat = Quaternion.Euler(0, wheelTurn, 0);
            Quaternion spinQuat = Quaternion.Euler(wheelSpinAngle, 0, 0);
            wheel_LF.localRotation = steerQuat * spinQuat;
            wheel_RF.localRotation = steerQuat * spinQuat;
        }
    }

    /// <summary>
    /// 当按住空格 + W 时，进入漂移状态（四轮红烟）
    /// </summary>
    void HandleDrift()
    {
        // 用户需求：按住 空格 + W 才漂移，不再依赖转向键
        bool driftCondition = isBraking && verticalInput > 0.1f;

        if(driftCondition && !isDrifting)
        {
            StartDrift();
        }
        else if(!driftCondition && isDrifting)
        {
            EndDrift();
        }
    }

    void StartDrift()
    {
        isDrifting = true;
        // 如果你想在这里就 Play() 漂移烟，可以保留，但后面我们会在 HandleParticles() 中统一管理
        foreach(var ps in driftSmoke)
        {
            ps.Play();
        }
    }

    void EndDrift()
    {
        isDrifting = false;
        // 同理，这里 Stop()，但 HandleParticles() 会实时控制
        foreach(var ps in driftSmoke)
        {
            ps.Stop();
        }
    }

    /// <summary>
    /// 动态调整轮子的侧向摩擦，模拟打滑
    /// </summary>
    void HandleDriftFriction()
    {
        Vector3 velocity = rb.velocity;
        if (velocity.sqrMagnitude < 0.1f)
        {
            // 几乎静止时直接设为普通抓地
            SetAllWheelFriction(normalFriction);
            return;
        }

        Vector3 velDir = velocity.normalized;
        Vector3 fwd = transform.forward;

        float angle = Vector3.Angle(fwd, velDir);
        float angleFactor = angle / 180f; // 0 ~ 1
        float baseFriction = Mathf.Lerp(normalFriction, normalFriction * 0.5f, angleFactor);

        if (isDrifting)
        {
            baseFriction = Mathf.Lerp(baseFriction, driftFriction, 0.7f);
        }

        SetWheelSidewaysFriction(wheelCollider_FL, baseFriction * frontTireGrip);
        SetWheelSidewaysFriction(wheelCollider_FR, baseFriction * frontTireGrip);
        SetWheelSidewaysFriction(wheelCollider_RL, baseFriction * rearTireGrip);
        SetWheelSidewaysFriction(wheelCollider_RR, baseFriction * rearTireGrip);
    }

    /// <summary>
    /// 漂移时可加一股横向力，让后轮甩得更明显
    /// </summary>
    void ApplyDriftPhysics()
    {
        if(isDrifting)
        {
            Vector3 driftForce = transform.right * (horizontalInput * 5000f);
            rb.AddForce(driftForce, ForceMode.Force);
        }
    }

    /// <summary>
    /// 控制各种粒子特效的播放/停止
    /// 满足用户需求：
    /// - W => 后轮白烟
    /// - A/D => 前轮白烟
    /// - Space + W => 四轮红烟
    /// </summary>



    void HandleParticles()
    {
        // 1) 后轮白烟 (rearWheelParticles): 只有在按住W且未漂移时
        if (verticalInput > 0.1f && !isDrifting && IsGrounded())
        {
            foreach (var ps in rearWheelParticles)
            {
                // 确保粒子系统正在播放
                if (!ps.isPlaying)
                {
                    ps.Play();
                }
                var emission = ps.emission;
                emission.rateOverTime = emissionRate;
            }
        }
        else
        {
            // 确保当不再按 W 时停止粒子
            foreach (var ps in rearWheelParticles)
            {
                if (ps.isPlaying)
                {
                    ps.Stop();
                }
            }
        }

        // 2) 前轮白烟 (frontWheelParticles): 只有在速度大于50且按下A或D时
        if (Mathf.Abs(horizontalInput) > 0.3f && !isDrifting && IsGrounded() && rb.velocity.magnitude > 50f)
        {
            foreach (var ps in frontWheelParticles)
            {
                if (!ps.isPlaying)
                {
                    ps.Play();
                }
                var emission = ps.emission;
                emission.rateOverTime = emissionRate;
            }
        }
        else
        {
            foreach (var ps in frontWheelParticles)
            {
                if (ps.isPlaying)
                {
                    ps.Stop();
                }
            }
        }

        // 3) 漂移红烟 (driftSmoke): 只要 isDrifting 即播放 (四轮)
        if (isDrifting && IsGrounded())
        {
            foreach (var ps in driftSmoke)
            {
                if (!ps.isPlaying)
                {
                    ps.Play();
                }
                var emission = ps.emission;
                emission.rateOverTime = emissionRate;
            }
        }
        else
        {
            foreach (var ps in driftSmoke)
            {
                if (ps.isPlaying)
                {
                    ps.Stop();
                }
            }
        }
    }





    void HandleParticles_OLD()
    {
        // 如你想保留旧逻辑可留着；当前示例已替换为上面版本
    }

    void ApplyBrakes()
    {
        if (isBraking)
        {
            rb.AddForce(-rb.velocity.normalized * brakeForce, ForceMode.Force);
        }
    }

    void ApplyDownForce()
    {
        rb.AddForce(-transform.up * downForce * rb.velocity.magnitude);
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, -Vector3.up, 0.5f);
    }

    void SetAllWheelFriction(float friction)
    {
        SetWheelSidewaysFriction(wheelCollider_FL, friction * frontTireGrip);
        SetWheelSidewaysFriction(wheelCollider_FR, friction * frontTireGrip);
        SetWheelSidewaysFriction(wheelCollider_RL, friction * rearTireGrip);
        SetWheelSidewaysFriction(wheelCollider_RR, friction * rearTireGrip);
    }

    void SetWheelSidewaysFriction(WheelCollider wc, float frictionValue)
    {
        if (!wc) return;
        WheelFrictionCurve frictionCurve = wc.sidewaysFriction;
        frictionCurve.stiffness = frictionValue;
        wc.sidewaysFriction = frictionCurve;
    }
}
