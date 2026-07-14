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

    Vector3? LeftAnchor { get; }
    Vector3? RightAnchor { get; }
    Vector3 LeftNormal { get; }
    Vector3 RightNormal { get; }

    void SetLeftAnchor(Vector3? point, Vector3 normal);
    void SetRightAnchor(Vector3? point, Vector3 normal);

    bool IsFreeLook { get; set; }
    void ResetFreeLook();

    void SetVelocity(Vector3 velocity);
    void SwitchState(PlayerBaseState newState);
    void ResetJump();
    void SetControllerEnabled(bool isEnabled);

    Vector2 MoveInput { get; }
    bool JumpInput { get; }
    bool SprintInput { get; }
    bool LeftGripInput { get; }
    bool RightGripInput { get; }

    bool TryGetGripPoint(
        Vector3? oppositeHandAnchor,
        Vector3 oppositeHandNormal,
        out Vector3 hitPoint,
        out Vector3 hitNormal);

    bool CheckLedgeVault(out Vector3 vaultTarget);
}
