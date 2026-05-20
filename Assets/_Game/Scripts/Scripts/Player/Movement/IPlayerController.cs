using UnityEngine;

public interface IPlayerController : ISurfaceReadModel
{
    PlayerStats Stats { get; }
    Vector3 Velocity { get; }
    float HorizontalSpeed { get; }

    /// <summary>Alias for <see cref="ISurfaceReadModel.IsStableGround"/> — preserves movement API.</summary>
    bool IsGrounded { get; }

    Transform PlayerTransform { get; }
    Transform CameraTransform { get; }

    /// <summary>True while the climb state machine state is active (drives climb hysteresis).</summary>
    bool IsClimbing { get; }

    void SetVelocity(Vector3 velocity);
    void MoveCharacter(Vector3 motion);
    void SwitchState(PlayerBaseState newState);
    void ResetJump();
    void ClearGripAnchors();
    void SetClimbSolverInput(ClimbInputData inputData);

    Vector2 MoveInput { get; }
    bool JumpInput { get; }
    bool SprintInput { get; }
    bool LeftGripInput { get; }
    bool RightGripInput { get; }
    ClimbInputData ClimbInput { get; }
}

public static class PlayerControllerSurfaceExtensions
{
    public static bool ValidateGripAnchor(this IPlayerController controller, Vector3 anchor, Vector3 surfaceNormal)
    {
        int stamp = 0;
        return controller.ValidateGripAnchor(anchor, surfaceNormal, ref stamp);
    }
}
