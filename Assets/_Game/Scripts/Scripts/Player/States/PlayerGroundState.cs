using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    private float _jumpTimeoutDelta;

    public PlayerGroundedState(IPlayerController currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory) { }

    public override void EnterState()
    {
        Vector3 vel = _ctx.Velocity;
        vel.y = _ctx.Stats.GroundStickVelocity;
        _ctx.SetVelocity(vel);
        _jumpTimeoutDelta = _ctx.Stats.JumpTimeout;
    }

    public override void UpdateState()
    {
        if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;

        if ((_ctx.LeftGripInput || _ctx.RightGripInput) && _ctx.CanGrip)
        {
            _ctx.SwitchState(_factory.Climb);
            return;
        }

        HandleMovement();
        CheckSwitchStates();
    }

    public override void ExitState()
    {
        _ctx.ResetJump();
    }

    public override void CheckSwitchStates()
    {
        if (_ctx.JumpInput)
        {
            if (_jumpTimeoutDelta <= 0.0f && _ctx.IsGrounded)
            {
                float jumpVelocity = Mathf.Sqrt(_ctx.Stats.JumpHeight * -2f * _ctx.Stats.Gravity);

                Vector3 vel = _ctx.Velocity;
                vel.y = jumpVelocity;
                _ctx.SetVelocity(vel);

                _ctx.SwitchState(_factory.Air);
                _ctx.ResetJump();
                return;
            }
        }

        if (!_ctx.IsGrounded)
        {
            _ctx.SwitchState(_factory.Air);
        }
    }

    private void HandleMovement()
    {
        PlayerStats stats = _ctx.Stats;
        float targetSpeed = _ctx.SprintInput ? stats.SprintSpeed : stats.MoveSpeed;
        if (_ctx.MoveInput == Vector2.zero) targetSpeed = 0.0f;

        float currentHorizontalSpeed = _ctx.HorizontalSpeed;
        float speedOffset = stats.SpeedBlendThreshold;
        float finalSpeed;

        float currentRate = (_ctx.MoveInput == Vector2.zero) ? stats.DecelerationRate : stats.AccelerationRate;

        if (Mathf.Abs(currentHorizontalSpeed - targetSpeed) > speedOffset)
        {
            finalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * currentRate);
            finalSpeed = Mathf.Round(finalSpeed * 1000f) / 1000f;
        }
        else
        {
            finalSpeed = targetSpeed;
        }

        Vector3 vel = _ctx.Velocity;
        Vector3 inputDirection;

        if (_ctx.MoveInput != Vector2.zero)
        {
            Transform t = _ctx.PlayerTransform;
            inputDirection = t.right * _ctx.MoveInput.x + t.forward * _ctx.MoveInput.y;
        }
        else if (currentHorizontalSpeed > 0.001f)
        {
            inputDirection = new Vector3(vel.x, 0f, vel.z);
            inputDirection.Normalize();
        }
        else
        {
            inputDirection = Vector3.zero;
        }

        Vector3 horizontal = inputDirection * finalSpeed;
        vel.x = horizontal.x;
        vel.y = stats.GroundStickVelocity;
        vel.z = horizontal.z;
        _ctx.SetVelocity(vel);
    }
}
