using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movement Parameters")]
    public float motorForce = 5000f;    
    public float maxSpeed = 30f;        
    public float steerAngle = 45f;      
    public float brakeForce = 10000f;   
    public float downForce = 100f;      

    [Header("Physics Parameters")]
    public float airDrag = 0.5f;        
    public float groundDrag = 3f;       
    public Transform centerOfMass;      

    private Rigidbody rb;
    public float verticalInput;
    public float horizontalInput;
    private bool isBraking;
    
    [Header("Collision Settings")]
    public float collisionResponseMultiplier = 1f; // Player collision impact
    [Header("Drift System")]
    [Tooltip("Lateral friction during drift (lower values make drifting easier)")]
    public float driftFriction = 0.5f;  
    [Tooltip("Lateral friction in normal conditions")]
    public float normalFriction = 1.0f; 
    [Tooltip("Drift smoke effect (red)")]
    public ParticleSystem[] driftSmoke; 
    [Tooltip("Drift input threshold: requires Space + W input above this value")]
    public float driftThreshold = 0.7f; 

    [Header("Camera System")]
    public Camera[] cameras;                    
    private int currentCameraIndex = 0;

    [Header("Particle Effects")]
    [Tooltip("Rear wheel particles (white smoke) for acceleration")]
    public ParticleSystem[] rearWheelParticles; 
    [Tooltip("Front wheel particles (white smoke) for turning")]
    public ParticleSystem[] frontWheelParticles;
    public float emissionRate = 30f;            

    public float wheelSpinSpeed = 720f; 
    private float wheelSpinAngle = 0f;  

    [Header("Wheel Settings")]
    public Transform wheel_LF;  
    public Transform wheel_RF;  
    public float maxWheelAngle = 15f; 

    [Header("Sound System")]
    public AudioClip engineIdleClip;   // Engine idle sound
    public AudioClip accelerateClip;   // Acceleration sound
    public AudioClip brakeClip;        // Braking sound

    [Range(0,1)] public float engineIdleVolume = 0.5f;   // Idle sound volume
    [Range(0,1)] public float accelerateVolume = 0.7f;   // Acceleration sound volume
    [Range(0,1)] public float brakeVolume = 0.7f;        // Brake sound volume

    public float fadeOutDuration = 1f;  // Sound fade-out duration (seconds)

    private AudioSource engineIdleAudio; 
    private AudioSource accelerateAudio; 
    private AudioSource brakeAudio;      

    private bool isDrifting = false;

    [Header("Wheel Colliders")]
    public WheelCollider wheelCollider_FL;
    public WheelCollider wheelCollider_FR;
    public WheelCollider wheelCollider_RL;
    public WheelCollider wheelCollider_RR;

    [Header("Tire Grip Coefficients")]
    [Range(0f,2f)] public float frontTireGrip = 1f; 
    [Range(0f,2f)] public float rearTireGrip = 1f;

    [Header("Other Parameters")]
    [Tooltip("Steering sensitivity: higher values make steering more responsive")]
    public float steerSensitivity = 1f;

    [Header("Lighting System")]
    public Renderer leftBrakeLight;  
    public Renderer rightBrakeLight; 
    public float brakeLightIntensity = 5f; 

    private Material brakeLightMaterial;
    private Color defaultEmissionColor;

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

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        props.SetColor("_EmissionColor", targetColor);
        
        leftBrakeLight.SetPropertyBlock(props);
        rightBrakeLight.SetPropertyBlock(props);

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
        foreach(var ps in driftSmoke) ps.Stop();
        foreach(var ps in rearWheelParticles) ps.Stop();
        foreach(var ps in frontWheelParticles) ps.Stop();
    }

    void SetupAudio()
    {
        engineIdleAudio = gameObject.AddComponent<AudioSource>();
        engineIdleAudio.clip = engineIdleClip;
        engineIdleAudio.loop = true;
        engineIdleAudio.volume = engineIdleVolume;
        engineIdleAudio.playOnAwake = true;
        engineIdleAudio.Play();

        brakeAudio = gameObject.AddComponent<AudioSource>();
        brakeAudio.clip = brakeClip;
        brakeAudio.loop = true;
        brakeAudio.volume = 0;  
        brakeAudio.playOnAwake = false;

        accelerateAudio = gameObject.AddComponent<AudioSource>();
        accelerateAudio.clip = accelerateClip;
        accelerateAudio.loop = true;
        accelerate
