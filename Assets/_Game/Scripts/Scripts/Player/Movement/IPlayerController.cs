using UnityEngine;
using Ascent.Player.Sensors;

public interface IPlayerController
{
    PlayerStats Stats { get; }
    PlayerGripSensor Sensor { get; }

    Vector3 Velocity { get; }
    float HorizontalSpeed { get; }

    Transform PlayerTransform { get; }
    Transform CameraTransform { get; }

    // --- BAĞIMSIZ EL (FREE-CLIMB) SİSTEMİ ---
    Vector3? LeftAnchor { get; }
    Vector3? RightAnchor { get; }
    Vector3 LeftNormal { get; }
    Vector3 RightNormal { get; }
    bool IsClimbing { get; set; }
    void ApplyClimbForce(Vector3 force);
    void SetLeftAnchor(Vector3? point, Vector3 normal);
    void SetRightAnchor(Vector3? point, Vector3 normal);

    bool IsFreeLook { get; set; }
    void ResetFreeLook();
    void ExecuteJump(float jumpVelocity);
    void SetVelocity(Vector3 velocity);
    void SwitchState(PlayerBaseState newState);
    void ResetJump();

    void SetKinematic(bool isKinematic);
    void LogEvent(string category, string message, bool isError = false);
    float TargetYaw { get; set; }
    void SmoothAlignBody(Vector3 targetNormal, float speed);
    public void LockLeftGrip();
    public void LockRightGrip();
    Vector2 MoveInput { get; }
    bool JumpInput { get; }
    bool SprintInput { get; }
    bool LeftGripInput { get; }
    bool RightGripInput { get; }

    bool IsTensioning { get; }
    float TensionCharge { get; }
    void BuildTension();
    void ExecuteDynoJump();
    void ResetTension();
    bool TryGetGripPoint(Vector3? oppositeHandAnchor, out Vector3 hitPoint, out Vector3 hitNormal);
    bool CheckLedgeVault(out Vector3 vaultTarget);
    public Vector3 VaultTargetPos { get; set; }
    StaminaController Stamina { get; }
    void LockGrips();
    bool JumpHeld { get; }

    // --- YENİ EKLENEN JUMP BUFFER DEĞİŞKENLERİ ---
    float JumpBufferTimer { get; set; }
    void ConsumeJumpBuffer();
}