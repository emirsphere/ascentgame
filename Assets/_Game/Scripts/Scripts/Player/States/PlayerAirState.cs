using UnityEngine;

public class PlayerAirState : PlayerBaseState
{
    private float _gracePeriodTimer;

    public PlayerAirState(IPlayerController currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        _ctx.ResetJump();
        _gracePeriodTimer = _ctx.Stats.AirGraceDuration;
    }

    public override void UpdateState()
    {
        if (_ctx.JumpInput) _ctx.ResetJump();

        _gracePeriodTimer -= Time.deltaTime;

        CheckSwitchStates();
        if (_ctx.IsClimbing) return;

        HandleGravity();
        HandleAirMovement();
    }

    public override void ExitState() { }

    public override void CheckSwitchStates()
    {
        PlayerStats stats = _ctx.Stats;

        if (_gracePeriodTimer > 0 && _ctx.Velocity.y > stats.AirGraceUpwardVelocity) return;

        bool gripPressed = _ctx.LeftGripInput || _ctx.RightGripInput;

        if (gripPressed && _ctx.CanGrip)
        {
            _ctx.SwitchState(_factory.Climb);
            return;
        }

        if (_ctx.IsGrounded && _ctx.Velocity.y < 0.0f)
        {
            _ctx.SwitchState(_factory.Grounded);
        }
    }

    private void HandleGravity()
    {
        PlayerStats stats = _ctx.Stats;
        float verticalVel = _ctx.Velocity.y;

        if (verticalVel > -stats.TerminalVelocity)
        {
            verticalVel += stats.Gravity * Time.deltaTime;
        }

        Vector3 vel = _ctx.Velocity;
        vel.y = verticalVel;
        _ctx.SetVelocity(vel);
    }

    private void HandleAirMovement()
    {
        PlayerStats stats = _ctx.Stats;
        float targetSpeed = _ctx.MoveInput == Vector2.zero ? 0.0f : stats.MoveSpeed;
        float currentHorizontalSpeed = _ctx.HorizontalSpeed;

        float finalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * stats.AirControlRate);
        finalSpeed = Mathf.Round(finalSpeed * 1000f) / 1000f;

        Vector3 vel = _ctx.Velocity;

        if (_ctx.MoveInput == Vector2.zero)
        {
            float vy = vel.y;
            float drag = Time.deltaTime * stats.AirDragRate;
            vel.x = Mathf.Lerp(vel.x, 0f, drag);
            vel.z = Mathf.Lerp(vel.z, 0f, drag);
            vel.y = vy;
            _ctx.SetVelocity(vel);
            return;
        }

        Transform t = _ctx.PlayerTransform;
        Vector3 inputDirection = t.right * _ctx.MoveInput.x + t.forward * _ctx.MoveInput.y;
        inputDirection.Normalize();

        Vector3 horizontal = inputDirection * finalSpeed;
        vel.x = horizontal.x;
        vel.z = horizontal.z;
        _ctx.SetVelocity(vel);
    }
}
