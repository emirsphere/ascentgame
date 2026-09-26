using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "VerticalVoid/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("Movement Dynamics")]
    public float MoveSpeed = 4.0f;
    public float SprintSpeed = 6.0f;
    public float RotationSpeed = 2.0f;
    public float AccelerationRate = 10.0f;
    public float DecelerationRate = 20.0f;
    public float SpeedBlendThreshold = 0.1f;
    [Header("Slope & Ground Physics")]
    [Tooltip("Yokuşta dururken uygulanacak karşıt yerçekimi çarpanı (Normalde 1)")]
    public float AntiSlipGravityMultiplier = 1.0f;

    [Tooltip("Yokuş aşağı koşarken karakteri zemine bastıran kuvvet")]
    public float GroundStickForce = 10.0f;

    [Header("Physics")]
    public float JumpHeight = 1.2f;
    public float FallGravityMultiplier = 2.5f; // Zıplamanın tepe noktasından sonra hızlı/tok düşüş (Balon gibi süzülmeyi önler)
    public float CoyoteTime = 0.25f;           // Uçurumdan düştükten 0.25 saniye sonra bile zıplayabilme (Hata toleransı)
    public float JumpBufferTime = 0.2f;        // Yere inmeden saliseler önce zıplamaya basılırsa, yere değer değmez zıplar
    public float Gravity = -15.0f;
    public float AirControlRate = 0.5f;
    public float JumpTimeout = 0.1f;
    public float TerminalVelocity = 53.0f;
    public float AirDragRate = 0.5f;

    [Header("Ground Detection")]
    public float GroundedOffset = -0.14f;
    public float GroundedRadius = 0.5f;
    public LayerMask GroundLayers;
    public float GroundStickVelocity = -2f;

    [Header("Air")]
    public float AirGraceDuration = 0.15f;
    public float AirGraceUpwardVelocity = 0.1f;

    [Header("Climbing - Detection")]
    public float ShoulderHeight = 2.6f;
    public float GripReachDistance = 1.3f;
    [Tooltip("Sensör kaydığında tutunmayı x saniye daha geçerli sayar (Coyote Time)")]
    public float GripBufferTime = 0.15f; // YENİ: Hata toleransı
    public LayerMask ClimbableLayers;

    [Header("Climbing - Juice & Physics")]
    public float RestOffset = 1.35f;
    public float PullOffset = 0.4f;
    public float BaseWallDistance = 0.3f;
    public float LeanWallDistance = 0.8f;
    public float PullWallDistanceMultiplier = 0.8f;
    public float SwingAmplitude = 1.2f;

    public float MuscleSpeed = 6f;
    public float SpringStiffness = 140f;
    public float ClimbInputThreshold = 0.1f;
    public float ClimbSnapThreshold = 0.05f; // Fiziğin daha yumuşak sönümlenmesi için düşürüldü
    public float GripCooldown = 0.4f; // Bıraktıktan sonra tekrar tutunma süresi
    [Header("Climbing - Two Handed Mechanics")]
    public float MaxArmSpan = 1.6f; // Bir el sabitken diğer elin gidebileceği maksimum mesafe
    [Header("Climbing - Jump Off Wall")]
    public float ClimbJumpNormalScale = 1.5f;
    public float ClimbJumpUpScale = 1.5f;
    public float ClimbJumpVelocityRetain = 0.3f;
    public float ClimbJumpImpulse = 8f;

    [Header("Juice (Camera Effects)")]
    public bool EnableCameraTilt = true;
    public float TiltAngle = 1.5f;
    public float TiltSpeed = 5.0f;
    public bool EnableHeadBob = true;
    public float BobFrequency = 10.0f;
    public float BobAmplitude = 0.05f;

    [Header("Stamina System")]
    public float MaxStamina = 100f;
    public float ClimbDrainRate = 15f; // Saniyede duvarda asılı kalma maliyeti
    public float JumpStaminaCost = 25f; // Duvardan zıplama maliyeti
    public float StaminaRegenRate = 35f; // Yerdeyken saniyede dolma hızı
}