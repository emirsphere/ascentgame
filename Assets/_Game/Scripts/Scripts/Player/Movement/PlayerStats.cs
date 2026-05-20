using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "VerticalVoid/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("Movement Dynamics")]
    public float MoveSpeed = 4.0f;
    public float SprintSpeed = 6.0f;
    public float RotationSpeed = 2.0f;

    [Tooltip("Hızlanma ivmesi (yüksek = anında hızlanma)")]
    public float AccelerationRate = 10.0f;

    [Tooltip("Durma ivmesi (yüksek = hızlı durma)")]
    public float DecelerationRate = 20.0f;

    [Tooltip("Hedef hıza blend eşiği")]
    public float SpeedBlendThreshold = 0.1f;

    [Header("Physics")]
    public float JumpHeight = 1.2f;
    public float Gravity = -15.0f;
    public float AirControlRate = 0.5f;
    public float JumpTimeout = 0.1f;
    public float FallTimeout = 0.15f;
    public float TerminalVelocity = 53.0f;
    public float AirDragRate = 0.5f;

    [Header("Ground Probe")]
    public float GroundedOffset = -0.14f;
    public float GroundedRadius = 0.5f;

    [Tooltip("Yerdeyken CharacterController'ı yere yapıştıran dikey hız")]
    public float GroundStickVelocity = -2f;

    [Header("Surface Detection")]
    [Tooltip("Physics queries only — walk/climb decisions use SurfaceClassifier, not this mask.")]
    public LayerMask SurfaceDetectionMask;

    [Tooltip("Legacy fallback: colliders on these layers are treated as walkable when no SurfaceData.")]
    public LayerMask GroundLayers;

    [Tooltip("Legacy fallback: colliders on these layers are treated as climbable when no SurfaceData.")]
    public LayerMask ClimbableLayers;

    public SurfaceClassificationConfig SurfaceClassification = new SurfaceClassificationConfig();

    [Header("Air")]
    public float AirGraceDuration = 0.15f;
    public float AirGraceUpwardVelocity = 0.1f;

    [Header("Climbing – Detection")]
    public float GripReachDistance = 2.5f;
    public float WallSensorCheckDistance = 0.6f;
    public float GripProbeRadius = 0.4f;
    public float GripEnterHoldTime = 0.1f;
    public float GripExitLoseTime = 0.15f;

    [Header("Climbing – Body")]
    public float RestOffset = 1.4f;
    public float OneHandDrop = 0.5f;
    public float PullOffset = 0.4f;
    public float BaseWallDistance = 0.6f;
    public float LeanWallDistance = 1.8f;

    [Tooltip("Yukarı çekilirken duvar mesafesi çarpanı")]
    public float PullWallDistanceMultiplier = 0.8f;

    public float MuscleSpeed = 6f;
    public float SpringStiffness = 150f;
    public float ClimbInputThreshold = 0.1f;
    public float ClimbSnapThreshold = 0.05f;

    [Header("Climbing – Jump Off Wall")]
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
}
