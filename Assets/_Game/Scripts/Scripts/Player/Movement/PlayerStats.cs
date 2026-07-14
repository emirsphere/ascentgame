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

    [Header("Physics")]
    public float JumpHeight = 1.2f;
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
    public float GripReachDistance = 1.5f;
    [Tooltip("Sensör kaydığında tutunmayı x saniye daha geçerli sayar (Coyote Time)")]
    public float GripBufferTime = 0.15f; // YENİ: Hata toleransı
    public LayerMask ClimbableLayers;

    [Header("Climbing - Legacy Spring Tuning (unused by Hang/Climb)")]
    public float RestOffset = 1.4f;
    public float PullOffset = 0.4f;
    public float BaseWallDistance = 0.6f;
    public float LeanWallDistance = 1.8f;
    public float PullWallDistanceMultiplier = 0.8f;
    public float SwingAmplitude = 1.5f;

    public float MuscleSpeed = 6f;
    public float SpringStiffness = 150f;
    public float ClimbInputThreshold = 0.1f;
    public float ClimbSnapThreshold = 0.01f; // Fiziğin daha yumuşak sönümlenmesi için düşürüldü

    [Header("Climbing - Virtual Arms")]
    public float VirtualShoulderHeight = 1.4f;
    public float VirtualShoulderHalfWidth = 0.25f;
    public float MaxArmReach = 1.5f;
    public float GripReachTolerance = 0.15f;
    public float ClimbingGravity = -15.0f;
    [Range(1, 4)] public int ClimbingConstraintIterations = 2;

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
    [Header("Climbing - Two Handed Mechanics")]
    public float MaxArmSpan = 1.8f; // Bir el sabitken diğer elin gidebileceği maksimum mesafe
}
